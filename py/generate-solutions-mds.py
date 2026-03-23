import re
from pathlib import Path
from collections import defaultdict


def parse_levels(file_path, list_regex, block_regex, usage_regex):
    if not file_path.exists():
        print(f"Error: Could not find file at {file_path}")
        return {}

    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    codes_match = re.search(list_regex, content, re.DOTALL)
    master_codes = re.findall(r'"([^"]+)"', codes_match.group(1)) if codes_match else []

    level_blocks = re.findall(block_regex, content, re.DOTALL)

    grouped_data = defaultdict(list)

    for block in level_blocks:
        lvl_id = re.search(r'Id\s*=\s*(\d+)', block)
        section = re.search(r'Section\s*=\s*"([^"]+)"', block)
        title = re.search(r'Title\s*=\s*"([^"]+)"', block)
        code_idx_match = re.search(usage_regex, block)

        if section and title and lvl_id and code_idx_match:
            idx = int(code_idx_match.group(1))
            grouped_data[section.group(1)].append({
                "id": lvl_id.group(1),
                "title": title.group(1)
            })

    return grouped_data


def make_solution_url(level_type: str, level_id: str) -> str:
    return f"https://github.com/OnlyCook/abitur-elite-code/wiki/{level_type}_LEVEL_{level_id}_SOLUTION"


def generate_solution_list(data_dict, level_type: str, level_prefix: str = "") -> str:
    text = ""
    for section_name, levels in data_dict.items():
        text += f"## {section_name}\n"
        for lvl in levels:
            display_id = f"{level_prefix}{lvl['id']}"
            url = make_solution_url(level_type, lvl['id'])
            text += f"- Level {display_id}: [{lvl['title']}]({url})\n"
        text += "\n"
    return text


def extract_solution_lists():
    script_dir = Path(__file__).parent
    level_cs_path = script_dir.parent / "cs" / "Level.cs"
    sql_level_cs_path = script_dir.parent / "cs" / "SqlLevel.cs"
    cs_output_path = script_dir / "CS_SOLUTIONS.md"
    sql_output_path = script_dir / "SQL_SOLUTIONS.md"

    # 1. Process C# Levels
    csharp_data = parse_levels(
        level_cs_path,
        r'CodesList\s*=\s*\{([^}]+)\};',
        r'new Level\s*\{(.*?)\}',
        r'LevelCodes\.CodesList\[(\d+)\]'
    )

    # 2. Process SQL Levels
    sql_data = parse_levels(
        sql_level_cs_path,
        r'CodesList\s*=\s*\{([^}]+)\};',
        r'new SqlLevel\s*\{(.*?)\}',
        r'SqlLevelCodes\.CodesList\[(\d+)\]'
    )

    # 3. Write CS_SOLUTIONS.md
    cs_content = "Eine Liste aller Lösungen zu den C#-Levels.\n\n"
    cs_content += generate_solution_list(csharp_data, level_type="CS")

    with open(cs_output_path, "w", encoding="utf-8") as f:
        f.write(cs_content)
    print(f"Successfully generated {cs_output_path}")

    # 4. Write SQL_SOLUTIONS.md
    sql_content = "Eine Liste aller Lösungen zu den SQL-Levels.\n\n"
    sql_content += generate_solution_list(sql_data, level_type="SQL", level_prefix="S")

    with open(sql_output_path, "w", encoding="utf-8") as f:
        f.write(sql_content)
    print(f"Successfully generated {sql_output_path}")


if __name__ == "__main__":
    extract_solution_lists()