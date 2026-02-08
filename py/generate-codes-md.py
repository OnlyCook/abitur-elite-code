import re
from pathlib import Path
from collections import defaultdict

def extract_level_data():
    # Setup paths
    script_dir = Path(__file__).parent
    level_cs_path = script_dir.parent / "cs" / "Level.cs"
    output_md_path = script_dir / "LEVEL_CODES.md"

    if not level_cs_path.exists():
        print(f"Error: Could not find file at {level_cs_path}")
        return

    with open(level_cs_path, "r", encoding="utf-8") as f:
        content = f.read()

    # 1. Extract the master list of codes from CodesList
    codes_match = re.search(r'CodesList\s*=\s*\{([^}]+)\};', content, re.DOTALL)
    master_codes = re.findall(r'"([^"]+)"', codes_match.group(1)) if codes_match else []

    # 2. Extract Level blocks to keep attributes grouped correctly
    level_blocks = re.findall(r'new Level\s*\{(.*?)\}', content, re.DOTALL)

    grouped_data = defaultdict(list)

    for block in level_blocks:
        # Extract individual fields from the block
        lvl_id = re.search(r'Id\s*=\s*(\d+)', block)
        section = re.search(r'Section\s*=\s*"([^"]+)"', block)
        title = re.search(r'Title\s*=\s*"([^"]+)"', block)
        
        # Find the index used in LevelCodes.CodesList[x]
        code_idx_match = re.search(r'LevelCodes\.CodesList\[(\d+)\]', block)

        if section and title and lvl_id and code_idx_match:
            idx = int(code_idx_match.group(1))
            code_str = master_codes[idx] if idx < len(master_codes) else "N/A"
            
            grouped_data[section.group(1)].append({
                "id": lvl_id.group(1),
                "code": code_str,
                "title": title.group(1)
            })

    # 3. Generate Formatted Markdown
    md_content = "# Abitur Elite Code - Level Übersicht\n\n"
    md_content += "Hier findest du alle Skip-Codes für die Standard Level. Gebe diese im Level-Auswählen-Fenster ein, um direkt zu einem Level zu springen.\n\n"

    for section_name, levels in grouped_data.items():
        md_content += f"## {section_name}\n\n"
        md_content += "| Level | Code | Titel |\n"
        md_content += "| :--- | :---: | :--- |\n"
        for lvl in levels:
            md_content += f"| {lvl['id']} | `{lvl['code']}` | {lvl['title']} |\n"
        md_content += "\n"

    # 4. Write to file
    with open(output_md_path, "w", encoding="utf-8") as f:
        f.write(md_content)

    print(f"Successfully generated {output_md_path} grouped by section.")

if __name__ == "__main__":
    extract_level_data()