import re
from pathlib import Path
from collections import defaultdict

def parse_levels(file_path, list_regex, block_regex, usage_regex):
    """
    Helper function to parse a C# file and extract level codes.
    """
    if not file_path.exists():
        print(f"Error: Could not find file at {file_path}")
        return {}

    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Extract the master list of codes
    codes_match = re.search(list_regex, content, re.DOTALL)
    master_codes = re.findall(r'"([^"]+)"', codes_match.group(1)) if codes_match else []

    # Extract Level blocks
    level_blocks = re.findall(block_regex, content, re.DOTALL)

    grouped_data = defaultdict(list)

    for block in level_blocks:
        # Extract individual fields from the block
        lvl_id = re.search(r'Id\s*=\s*(\d+)', block)
        section = re.search(r'Section\s*=\s*"([^"]+)"', block)
        title = re.search(r'Title\s*=\s*"([^"]+)"', block)
        
        # Find the index used in CodesList[x] (varies by file)
        code_idx_match = re.search(usage_regex, block)

        if section and title and lvl_id and code_idx_match:
            idx = int(code_idx_match.group(1))
            code_str = master_codes[idx] if idx < len(master_codes) else "N/A"
            
            grouped_data[section.group(1)].append({
                "id": lvl_id.group(1),
                "code": code_str,
                "title": title.group(1)
            })
            
    return grouped_data

def extract_level_data():
    # Setup paths
    script_dir = Path(__file__).parent
    level_cs_path = script_dir.parent / "cs" / "Level.cs"
    sql_level_cs_path = script_dir.parent / "cs" / "SqlLevel.cs"
    output_md_path = script_dir / "LEVEL_CODES.md"

    # 1. Process C# Levels
    # Regex Explanation:
    # list: Matches CodesList = { ... };
    # block: Matches new Level { ... }
    # usage: Matches LevelCodes.CodesList[x]
    csharp_data = parse_levels(
        level_cs_path,
        r'CodesList\s*=\s*\{([^}]+)\};',
        r'new Level\s*\{(.*?)\}',
        r'LevelCodes\.CodesList\[(\d+)\]'
    )

    # 2. Process SQL Levels
    # Regex Explanation:
    # list: Matches CodesList = { ... }; (Same pattern, different file)
    # block: Matches new SqlLevel { ... }
    # usage: Matches SqlLevelCodes.CodesList[x]
    sql_data = parse_levels(
        sql_level_cs_path,
        r'CodesList\s*=\s*\{([^}]+)\};',
        r'new SqlLevel\s*\{(.*?)\}',
        r'SqlLevelCodes\.CodesList\[(\d+)\]'
    )

    # 3. Generate Formatted Markdown
    md_content = "# Abitur Elite Code - Level Übersicht\n\n"
    md_content += "Hier findest du alle Skip-Codes. Gebe diese im Level-Auswählen-Fenster ein, um direkt zu einem Level zu springen.\n\n"

    # Helper to write sections to MD string
    def append_sections(data_dict, level_prefix=""):
        text = ""
        for section_name, levels in data_dict.items():
            text += f"## {section_name}\n\n"
            text += "| Level | Code | Titel |\n"
            text += "| :--- | :---: | :--- |\n"
            for lvl in levels:
                level_id = f"{level_prefix}{lvl['id']}" if level_prefix else lvl['id']
                text += f"| {level_id} | `{lvl['code']}` | {lvl['title']} |\n"
            text += "\n"
        return text

    # Add C# Sections
    md_content += append_sections(csharp_data)

    # Add SQL Sections (if any found)
    if sql_data:
        md_content += "---\n\n"
        md_content += "# SQL Levels\n\n"
        md_content += append_sections(sql_data, level_prefix="S")

    # 4. Write to file
    with open(output_md_path, "w", encoding="utf-8") as f:
        f.write(md_content)

    print(f"Successfully generated {output_md_path} with C# and SQL levels.")

if __name__ == "__main__":
    extract_level_data()