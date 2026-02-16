#!/usr/bin/env python3
"""
PlantUML Diagram Generator for Abitur Elite Code
Updated for Multi-Diagram Support and SQL Levels
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

def extract_level_data(file_path, level_class_name="Level", shared_diagrams_class=None):
    """
    Generic extractor for both Level.cs and SqlLevel.cs
    """
    if not file_path.exists():
        print(f"File not found: {file_path}")
        return {}, []

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    shared_diagrams = {}
    if shared_diagrams_class:
        # Regex to find the shared diagrams block
        # Look for public static class ClassName { ... }
        # Then inside extract string fields
        shared_pattern = re.compile(r'public static string (\w+)\s*=\s*(@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*");', re.DOTALL)
        # Note: This scans the whole file, but unique var names usually prevent collisions
        for match in shared_pattern.finditer(content):
            key = match.group(1)
            source = clean_source(match.group(2))
            # Only add if it belongs to the intended scope (simple heuristic)
            shared_diagrams[key] = source

    levels = []
    
    # Regex for "new ClassName {"
    level_start_regex = re.compile(r'new ' + level_class_name + r'\s*\{')
    
    cursor = 0
    while True:
        match = level_start_regex.search(content[cursor:])
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
            if line.strip().startswith('@startuml') or line.strip().startswith('@startchen'):
                new_lines.append('skinparam backgroundcolor transparent')
                new_lines.append('skinparam classAttributeIconSize 0')
                # Add monochrome/plain styling to mimic generic SQL/UML standard if preferred
                # or keep default.
        return '\n'.join(new_lines)
    return plantuml_source

def generate_single_diagram(source, output_path, cache_key, cache):
    if not source: return False
    source_hash = hashlib.md5(source.encode('utf-8')).hexdigest()
    
    if (cache_key in cache and 
        str(cache[cache_key].get('hash')) == str(source_hash) and 
        output_path.exists()):
        return False 

    # extract chen notation keys before formatting
    chen_keys = set(re.findall(r'(\w+)\s*<<key>>', source))

    themed_source = add_theme(source)
    encoded = encode_plantuml(themed_source)
    url = PLANTUML_SERVER + encoded
    
    try:
        response = requests.get(url, timeout=30)
        response.raise_for_status()
        
        svg_content = response.content.decode('utf-8')
        
        # apply unicode underlines for chen keys directly in the svg xml
        if chen_keys:
            for key in chen_keys:
                unicode_key = convert_to_unicode_underline(key)
                # replace exact matches inside <text> tags
                svg_content = re.sub(
                    r'(<text[^>]*>)\s*' + re.escape(key) + r'\s*(</text>)', 
                    r'\g<1>' + unicode_key + r'\g<2>', 
                    svg_content
                )
                
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'wb') as f:
            f.write(svg_content.encode('utf-8'))
            
        cache[cache_key] = {'hash': source_hash, 'path': str(output_path)}
        save_cache(cache)
        return True
    except Exception as e:
        print(f"    Error: {e}")
        return False

def process_levels(levels, output_dir, cache, prefix="lvl"):
    gen_count = 0
    for item in levels:
        level_id = item['id']
        # Convert "Sektion 1..." to "sec1"
        section_folder = item['section'].replace('Sektion ', 'sec').split(':')[0].strip().lower()

        sources = item['sources']
        
        for index, source in enumerate(sources):
            diag_num = index + 1
            filename = f"lvl{level_id}-{diag_num}.svg"
            
            # Save to specific directory (img or imgsql)
            main_path = output_dir / section_folder / filename
            
            # Unique cache key 
            key_main = f"{prefix}_{level_id}_{diag_num}"
            
            print(f"Processing {prefix.upper()} Level {level_id} (Diagram {diag_num})...")
            if generate_single_diagram(source, main_path, key_main, cache):
                print(f"  -> Generated: {filename}")
                gen_count += 1
            else:
                print(f"  -> Cached: {filename}")
    return gen_count

def main():
    script_dir = Path(__file__).parent.parent
    
    # Paths
    cs_dir = script_dir / "cs"
    level_cs_path = cs_dir / "Level.cs"
    sql_level_cs_path = cs_dir / "SqlLevel.cs"
    
    # Output Directories
    output_dir_csharp = script_dir / "assets" / "img"
    output_dir_sql = script_dir / "assets" / "imgsql"
    
    print("PlantUML Generator - Abitur Elite Code")
    
    cache = load_cache()
    total_gen = 0
    
    # --- 1. Process Standard C# Levels ---
    if level_cs_path.exists():
        print(f"\nScanning {level_cs_path.name}...")
        shared_diags, levels = extract_level_data(level_cs_path, "Level", "SharedDiagrams")
        print(f"Found {len(levels)} levels and {len(shared_diags)} shared diagrams.")
        
        # Shared C#
        for key, source in shared_diags.items():
            out_path = output_dir_csharp / f"aux_{key}.svg" 
            cache_key = f"shared_{key}"
            if generate_single_diagram(source, out_path, cache_key, cache):
                print(f"  -> Shared Generated: {key}")
                total_gen += 1
        
        # Level C#
        total_gen += process_levels(levels, output_dir_csharp, cache, prefix="lvl")
    else:
        print(f"Skipping {level_cs_path.name} (not found)")

    # --- 2. Process SQL Levels ---
    if sql_level_cs_path.exists():
        print(f"\nScanning {sql_level_cs_path.name}...")
        # Note: shared diagrams class is SqlSharedDiagrams
        sql_shared, sql_levels = extract_level_data(sql_level_cs_path, "SqlLevel", "SqlSharedDiagrams")
        print(f"Found {len(sql_levels)} SQL levels and {len(sql_shared)} shared diagrams.")

        # Shared SQL (if any)
        for key, source in sql_shared.items():
            # Saving to imgsql root for shared SQL aux items
            out_path = output_dir_sql / f"aux_{key}.svg" 
            cache_key = f"sql_shared_{key}"
            if generate_single_diagram(source, out_path, cache_key, cache):
                print(f"  -> SQL Shared Generated: {key}")
                total_gen += 1

        # Level SQL
        # This will save to assets/imgsql/secX/lvlY-Z.svg
        total_gen += process_levels(sql_levels, output_dir_sql, cache, prefix="sql_lvl")
    else:
        print(f"Skipping {sql_level_cs_path.name} (not found)")

    print(f"\nDone. Generated {total_gen} new images total.")

if __name__ == "__main__":
    main()