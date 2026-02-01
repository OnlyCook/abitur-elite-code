#!/usr/bin/env python3
"""
PlantUML Diagram Generator for Abitur Elite Code
"""

import os
import re
import zlib
import requests
import json
from pathlib import Path

# PlantUML server URL
PLANTUML_SERVER = "http://www.plantuml.com/plantuml/png/"
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
        with open(CACHE_FILE, 'r', encoding='utf-8') as f:
            return json.load(f)
    return {}

def save_cache(cache):
    with open(CACHE_FILE, 'w', encoding='utf-8') as f:
        json.dump(cache, indent=2, fp=f)

def clean_source(source):
    if not source: return None
    # Remove C# verbatim string literals @ or standard quotes
    return source.replace('\\n', '\n').replace('\\r', '').replace('""', '"').replace('\\"', '"').strip('"').strip()

def extract_level_data(level_cs_path):
    with open(level_cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Extract Shared Diagrams from static class SharedDiagrams
    shared_diagrams = {}
    # Finds: public static string Key = "..."
    # Handles verbatim @"..." and normal "..."
    shared_pattern = re.compile(r'public static string (\w+)\s*=\s*(@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*");', re.DOTALL)
    
    for match in shared_pattern.finditer(content):
        key = match.group(1)
        source = clean_source(match.group(2))
        shared_diagrams[key] = source

    # 2. Extract Level objects
    level_blocks = re.findall(r'new Level\s*\{(.*?)\}\s*(?:,|;)', content, re.DOTALL)
    levels = []
    
    for block in level_blocks:
        id_match = re.search(r'Id\s*=\s*(\d+)', block)
        sec_match = re.search(r'Section\s*=\s*"([^"]*)"', block)
        main_match = re.search(r'PlantUMLSource\s*=\s*"((?:[^"\\]|\\.)*)"', block)
        
        if id_match and sec_match and main_match:
            levels.append({
                'id': int(id_match.group(1)),
                'section': sec_match.group(1),
                'main': clean_source(main_match.group(1))
            })
            
    return shared_diagrams, levels

def add_blueprint_theme(plantuml_source):
    if not plantuml_source: return ""
    if '!theme' not in plantuml_source:
        lines = plantuml_source.split('\n')
        new_lines = []
        for line in lines:
            new_lines.append(line)
            if line.strip().startswith('@startuml'):
                new_lines.append('!theme blueprint')
        return '\n'.join(new_lines)
    return plantuml_source

def generate_single_diagram(source, output_path, cache_key, cache):
    if not source: return False
    
    source_hash = hash(source)
    if cache_key in cache and cache[cache_key]['hash'] == source_hash and output_path.exists():
        return False 

    themed_source = add_blueprint_theme(source)
    encoded = encode_plantuml(themed_source)
    url = PLANTUML_SERVER + encoded
    
    try:
        response = requests.get(url, timeout=30)
        response.raise_for_status()
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'wb') as f:
            f.write(response.content)
        cache[cache_key] = {'hash': source_hash, 'path': str(output_path)}
        return True
    except Exception as e:
        print(f"    Error: {e}")
        return False

def main():
    script_dir = Path(__file__).parent
    level_cs_path = script_dir / "Level.cs"
    output_dir = script_dir / "bin" / "Debug" / "net10.0-windows" / "img"
    
    print("PlantUML Generator - Abitur Elite Code")
    if not level_cs_path.exists():
        print("Level.cs not found.")
        return

    shared_diags, levels = extract_level_data(level_cs_path)
    cache = load_cache()
    gen_count = 0
    
    print(f"Found {len(levels)} levels and {len(shared_diags)} shared diagrams.")
    
    # 1. Generate Shared Diagrams
    print("--- Shared Diagrams ---")
    for key, source in shared_diags.items():
        # Output: img/aux_Key.png
        out_path = output_dir / f"aux_{key}.png"
        cache_key = f"shared_{key}"
        print(f"Processing Shared: {key}...")
        if generate_single_diagram(source, out_path, cache_key, cache):
            print("  -> Generated")
            gen_count += 1
        else:
            print("  -> Cached")

    # 2. Generate Level Diagrams
    print("--- Level Diagrams ---")
    for item in levels:
        level_id = item['id']
        section_folder = item['section'].replace('Sektion ', 'sec').split(':')[0].strip()
        
        main_path = output_dir / section_folder / f"lvl{level_id}.png"
        key_main = f"lvl_{level_id}_main"
        
        print(f"Processing Level {level_id}...")
        if generate_single_diagram(item['main'], main_path, key_main, cache):
            print("  -> Generated")
            gen_count += 1
        else:
            print("  -> Cached")

    save_cache(cache)
    print(f"Done. Generated {gen_count} new images.")

if __name__ == "__main__":
    main()