#!/usr/bin/env python3
"""
PlantUML Diagram Generator for Abitur Elite Code
Updated for Multi-Diagram Support
"""

import os
import re
import zlib
import requests
import json
import hashlib
from pathlib import Path

# PlantUML server URL
PLANTUML_SERVER = "http://www.plantuml.com/plantuml/dsvg/"
CACHE_FILE = "plantuml_cache.json"

def encode_plantuml(plantuml_text):
    plantuml_alphabet = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_'
    def encode3bytes(b1, b2, b3):
        c1 = b1 >> 2
        c2 = ((b1 & 0x3) << 4) | (b2 >> 4)
        c3 = ((b2 & 0xF) << 2) | (b3 >> 6)
        c4 = b3 & 0x3F
        return (plantuml_alphabet[c1 & 0x3F] + plantuml_alphabet[c2 & 0x3F] + 
                plantuml_alphabet[c3 & 0x3F] + plantuml_alphabet[c4 & 0x3F])
    def encode3bytes_final(b1, b2=-1, b3=-1):
        if b2 == -1:
            c1 = b1 >> 2
            c2 = (b1 & 0x3) << 4
            return plantuml_alphabet[c1 & 0x3F] + plantuml_alphabet[c2 & 0x3F]
        elif b3 == -1:
            c1 = b1 >> 2
            c2 = ((b1 & 0x3) << 4) | (b2 >> 4)
            c3 = (b2 & 0xF) << 2
            return (plantuml_alphabet[c1 & 0x3F] + plantuml_alphabet[c2 & 0x3F] + plantuml_alphabet[c3 & 0x3F])
        else:
            return encode3bytes(b1, b2, b3)
    compressed = zlib.compress(plantuml_text.encode('utf-8'))
    compressed = compressed[2:-4]
    result = ""
    i = 0
    while i < len(compressed):
        if i + 2 < len(compressed):
            result += encode3bytes(compressed[i], compressed[i+1], compressed[i+2])
            i += 3
        elif i + 1 < len(compressed):
            result += encode3bytes_final(compressed[i], compressed[i+1])
            i += 2
        else:
            result += encode3bytes_final(compressed[i])
            i += 1
    return result

def load_cache():
    if os.path.exists(CACHE_FILE):
        try:
            with open(CACHE_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except json.JSONDecodeError:
            print("Warning: Corrupt cache file found. Starting fresh.")
            return {}
    return {}

def save_cache(cache):
    with open(CACHE_FILE, 'w', encoding='utf-8') as f:
        json.dump(cache, indent=2, fp=f)

def clean_source(source):
    if not source: return None

    if source.startswith('@"'):
        content = source[2:-1] 
        content = content.replace('""', '"')
        return content.strip()

    if source.startswith('"'):
        content = source[1:-1]
        PLACEHOLDER = "###PLANTUML_LITERAL_NEWLINE###"
        content = content.replace('\\n/', PLACEHOLDER)
        content = content.replace('\\n', '\n').replace('\\r', '').replace('\\"', '"')
        content = content.replace(PLACEHOLDER, '\\n')
        return content.strip()

    return source.strip()

def extract_level_data(level_cs_path):
    with open(level_cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    shared_diagrams = {}
    shared_pattern = re.compile(r'public static string (\w+)\s*=\s*(@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*");', re.DOTALL)
    
    for match in shared_pattern.finditer(content):
        key = match.group(1)
        source = clean_source(match.group(2))
        shared_diagrams[key] = source

    levels = []

    cursor = 0
    while True:
        # Find start of "new Level"
        match = re.search(r'new Level\s*\{', content[cursor:])
        if not match:
            break

        block_start = cursor + match.end() - 1 
        cursor = block_start + 1

        brace_count = 1
        i = block_start + 1
        in_string = False
        in_verbatim = False
        
        while i < len(content) and brace_count > 0:
            char = content[i]
            
            # Logic to ignore braces inside C# strings
            if in_verbatim:
                if char == '"':
                    if i + 1 < len(content) and content[i+1] == '"': i += 1
                    else: in_verbatim = False
            elif in_string:
                if char == '\\': i += 1
                elif char == '"': in_string = False
            else:
                if char == '@' and i + 1 < len(content) and content[i+1] == '"':
                    in_verbatim = True; i += 1
                elif char == '"': in_string = True
                elif char == '{': brace_count += 1
                elif char == '}': brace_count -= 1
            i += 1
            
        if brace_count == 0:
            block_content = content[block_start+1 : i-1]
            
            id_match = re.search(r'Id\s*=\s*(\d+)', block_content)
            sec_match = re.search(r'Section\s*=\s*"([^"]*)"', block_content)
            
            sources = []
            
            # Find the PlantUMLSources list start
            puml_start_match = re.search(r'PlantUMLSources\s*=\s*new\s*List<string>\s*\{', block_content)
            
            if puml_start_match:
                list_start = puml_start_match.end() - 1
                list_brace_count = 1
                k = list_start + 1
                l_in_str = False
                l_in_verb = False
                
                while k < len(block_content) and list_brace_count > 0:
                    c = block_content[k]
                    if l_in_verb:
                        if c == '"':
                            if k + 1 < len(block_content) and block_content[k+1] == '"': k += 1
                            else: l_in_verb = False
                    elif l_in_str:
                        if c == '\\': k += 1
                        elif c == '"': l_in_str = False
                    else:
                        if c == '@' and k + 1 < len(block_content) and block_content[k+1] == '"':
                            l_in_verb = True; k += 1
                        elif c == '"': l_in_str = True
                        elif c == '{': list_brace_count += 1
                        elif c == '}': list_brace_count -= 1
                    k += 1
                
                if list_brace_count == 0:
                    list_content = block_content[list_start+1 : k-1]
                    # Now it is safe to regex the string literals from the list body
                    matches = re.findall(r'(@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*")', list_content)
                    for m in matches:
                        sources.append(clean_source(m))

            if id_match and sec_match and sources:
                levels.append({
                    'id': int(id_match.group(1)),
                    'section': sec_match.group(1),
                    'sources': sources
                })

        cursor = i

    return shared_diagrams, levels

def convert_to_unicode_underline(text):
    result = []
    for char in text:
        result.append(char)
        result.append('\u0332')
    return ''.join(result)

def add_theme(plantuml_source):
    if not plantuml_source: return ""
    plantuml_source = re.sub(r'(?m)^(\s*[-+#].*?)$', r'\1 ', plantuml_source)
    def static_replacer(match):
        prefix = match.group(1)
        content = match.group(2)
        return f"{prefix} {convert_to_unicode_underline(content)}"
    plantuml_source = re.sub(r'^(\s*[-+#])\s*\{static\}\s*(.+)$', static_replacer, plantuml_source, flags=re.MULTILINE)
    if 'skinparam backgroundcolor transparent' not in plantuml_source and 'skinparam classAttributeIconSize 0' not in plantuml_source:
        lines = plantuml_source.split('\n')
        new_lines = []
        for line in lines:
            new_lines.append(line)
            if line.strip().startswith('@startuml'):
                new_lines.append('skinparam backgroundcolor transparent')
                new_lines.append('skinparam classAttributeIconSize 0')
        return '\n'.join(new_lines)
    return plantuml_source

def generate_single_diagram(source, output_path, cache_key, cache):
    if not source: return False
    source_hash = hashlib.md5(source.encode('utf-8')).hexdigest()
    
    if (cache_key in cache and 
        str(cache[cache_key].get('hash')) == str(source_hash) and 
        output_path.exists()):
        return False 

    themed_source = add_theme(source)
    encoded = encode_plantuml(themed_source)
    url = PLANTUML_SERVER + encoded
    
    try:
        response = requests.get(url, timeout=30)
        response.raise_for_status()
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'wb') as f:
            f.write(response.content)
        cache[cache_key] = {'hash': source_hash, 'path': str(output_path)}
        save_cache(cache)
        return True
    except Exception as e:
        print(f"    Error: {e}")
        return False

def main():
    script_dir = Path(__file__).parent.parent
    level_cs_path = script_dir / "cs" / "Level.cs"
    output_dir = script_dir / "assets" / "img"
    
    print("PlantUML Generator - Abitur Elite Code")
    if not level_cs_path.exists():
        print(f"Level.cs not found at: {level_cs_path}")
        return

    shared_diags, levels = extract_level_data(level_cs_path)
    cache = load_cache()
    gen_count = 0
    
    print(f"Found {len(levels)} levels and {len(shared_diags)} shared diagrams.")
    
    # 1. Generate Shared Diagrams
    print("--- Shared Diagrams ---")
    for key, source in shared_diags.items():
        out_path = output_dir / f"aux_{key}.svg" 
        cache_key = f"shared_{key}"
        print(f"Processing Shared: {key}...")
        if generate_single_diagram(source, out_path, cache_key, cache):
            print("  -> Generated & Saved")
            gen_count += 1
        else:
            print("  -> Cached")

    # 2. Generate Level Diagrams
    print("--- Level Diagrams ---")
    for item in levels:
        level_id = item['id']
        # Convert "Sektion 1..." to "sec1"
        section_folder = item['section'].replace('Sektion ', 'sec').split(':')[0].strip().lower()

        sources = item['sources']
        
        # Iterate over all sources in the list
        for index, source in enumerate(sources):
            # index + 1 ensures filename is lvlX-1.svg, lvlX-2.svg, etc.
            diag_num = index + 1
            
            # UPDATED NAMING CONVENTION: lvl{id}-{num}.svg
            filename = f"lvl{level_id}-{diag_num}.svg"
            main_path = output_dir / section_folder / filename
            
            # Unique cache key for each diagram
            key_main = f"lvl_{level_id}_{diag_num}"
            
            print(f"Processing Level {level_id} (Diagram {diag_num})...")
            if generate_single_diagram(source, main_path, key_main, cache):
                print(f"  -> Generated: {filename}")
                gen_count += 1
            else:
                print(f"  -> Cached: {filename}")

    print(f"Done. Generated {gen_count} new images.")

if __name__ == "__main__":
    main()