import re
from pathlib import Path

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

    # 1. Extract Codes from CodesList
    codes_match = re.search(r'CodesList\s*=\s*\{([^}]+)\};', content, re.DOTALL)
    
    codes = []
    if codes_match:
        raw_codes = codes_match.group(1)
        # Find all strings inside quotes
        codes = re.findall(r'"([^"]+)"', raw_codes)

    # 2. Extract Titles from Level objects
    titles = re.findall(r'Title\s*=\s*"([^"]+)"', content)

    # 3. Generate Markdown Content
    md_content = "# Abitur Elite Code - Level Übersicht\n\n"
    md_content += "Hier findst du alle Skip-Codes für die Level. Gebe diese im Level-Wählen-Fenster ein, um direkt zu einem Level zu springen.\n\n"
    md_content += "| Level | Code | Titel |\n"
    md_content += "| :---: | :---: | :--- |\n"

    # Zip lists together (Code[0] is for Level 1, etc.)
    for i, (code, title) in enumerate(zip(codes, titles), 1):
        md_content += f"| {i} | `{code}` | {title} |\n"

    # 4. Write to file
    with open(output_md_path, "w", encoding="utf-8") as f:
        f.write(md_content)

    print(f"Successfully generated {output_md_path}")

if __name__ == "__main__":
    extract_level_data()