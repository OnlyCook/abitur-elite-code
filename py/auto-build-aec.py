import subprocess
import sys
import os
import shutil
from pathlib import Path

# ─── CONFIGURATION ────────────────────────────────────────────────────────────

PROJECT_DIR   = r"F:\PERSONAL DATA\coding projects\AbiturEliteCode"
PUBLISH_BASE  = r"F:\PERSONAL DATA\coding projects\AbiturEliteCode\AbiturEliteCode\bin\Release\net10.0"
SEVEN_ZIP     = r"F:\APPLICATIONS\7-Zip\7z.exe"
DESKTOP       = Path.home() / "Desktop"
APP_NAME      = "AbiturEliteCode"

# (dotnet runtime id, display label, zip suffix)
TARGETS = [
    ("win",   "Windows", "win"),
    ("osx",   "macOS",   "mac"),
    ("linux", "Linux",   "linux"),
]

# ─── HELPERS ──────────────────────────────────────────────────────────────────

RESET  = "\033[0m"
BOLD   = "\033[1m"
CYAN   = "\033[96m"
GREEN  = "\033[92m"
YELLOW = "\033[93m"
RED    = "\033[91m"
GRAY   = "\033[90m"

BAR_WIDTH = 40

def progress_bar(step: int, total: int, label: str = "") -> None:
    """Print an in-place progress bar."""
    filled  = int(BAR_WIDTH * step / total)
    bar     = "█" * filled + "░" * (BAR_WIDTH - filled)
    pct     = int(100 * step / total)
    line    = f"\r  {CYAN}[{bar}]{RESET} {pct:3d}%  {GRAY}{label:<35}{RESET}"
    sys.stdout.write(line)
    sys.stdout.flush()
    if step == total:
        print()   # newline when done

def section(title: str) -> None:
    print(f"\n{BOLD}{YELLOW}{'─'*55}{RESET}")
    print(f"{BOLD}{YELLOW}  {title}{RESET}")
    print(f"{BOLD}{YELLOW}{'─'*55}{RESET}")

def ok(msg: str)   -> None: print(f"  {GREEN}✔  {msg}{RESET}")
def err(msg: str)  -> None: print(f"  {RED}✘  {msg}{RESET}")
def info(msg: str) -> None: print(f"  {GRAY}·  {msg}{RESET}")

def run(cmd: list[str], cwd: str | None = None) -> subprocess.CompletedProcess:
    """Run a command, raise on failure."""
    result = subprocess.run(
        cmd,
        cwd=cwd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if result.returncode != 0:
        print()
        err("Command failed:")
        print(f"{RED}{result.stderr.strip()}{RESET}")
        raise RuntimeError(f"Command failed: {' '.join(cmd)}")
    return result

# ─── STEPS (5 per target) ─────────────────────────────────────────────────────
# 1. Prepare temp dir
# 2. dotnet publish
# 3. Copy files into AbiturEliteCode/ sub-folder
# 4. Create zip with 7-Zip
# 5. Move zip to Desktop

def build_target(runtime_id: str, label: str, zip_suffix: str) -> None:
    section(f"Building for {label}  ({runtime_id}-x64)")
    STEPS = 5

    # ── Step 1: Prepare temp staging dir ──────────────────────────────────────
    progress_bar(1, STEPS, "Preparing staging directory …")
    publish_src = Path(PUBLISH_BASE) / f"{runtime_id}-x64" / "publish"
    staging     = DESKTOP / f"_tmp_{APP_NAME}_{runtime_id}"
    app_folder  = staging / APP_NAME          # the folder that ends up inside the zip
    zip_name    = f"{APP_NAME}-{zip_suffix}.zip"
    zip_dest    = DESKTOP / zip_name

    # Clean up any leftover staging dir from a previous run
    if staging.exists():
        shutil.rmtree(staging)
    app_folder.mkdir(parents=True)
    progress_bar(1, STEPS, "Staging directory ready")

    # ── Step 2: dotnet publish ─────────────────────────────────────────────────
    progress_bar(2, STEPS, "Running dotnet publish …")
    cmd = [
        "dotnet", "publish",
        "-c", "Release",
        "-r", f"{runtime_id}-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
    ]
    run(cmd, cwd=PROJECT_DIR)
    progress_bar(2, STEPS, "dotnet publish complete")

    # ── Step 3: Copy published files into the AbiturEliteCode sub-folder ──────
    progress_bar(3, STEPS, "Copying published files …")
    if not publish_src.exists():
        raise FileNotFoundError(f"Publish output not found: {publish_src}")
    files = list(publish_src.iterdir())
    if not files:
        raise FileNotFoundError(f"No files found in publish directory: {publish_src}")
    for f in files:
        dest = app_folder / f.name
        if f.is_dir():
            shutil.copytree(f, dest)
        else:
            shutil.copy2(f, dest)
    progress_bar(3, STEPS, f"Copied {len(files)} item(s)")

    # ── Step 4: Compress with 7-Zip ────────────────────────────────────────────
    progress_bar(4, STEPS, f"Creating {zip_name} …")
    # We zip the APP_NAME folder itself (so inside the zip: AbiturEliteCode/<files>)
    # 7z a <zip_path> <folder_to_zip>  (run from staging so path is relative)
    run(
        [SEVEN_ZIP, "a", str(zip_dest), APP_NAME],
        cwd=str(staging),
    )
    progress_bar(4, STEPS, "Zip created")

    # ── Step 5: Cleanup staging dir ───────────────────────────────────────────
    progress_bar(5, STEPS, "Cleaning up …")
    shutil.rmtree(staging)
    progress_bar(5, STEPS, "Done")

    ok(f"Saved → {zip_dest}")

# ─── MAIN ─────────────────────────────────────────────────────────────────────

def main() -> None:
    print(f"\n{BOLD}{'═'*55}")
    print(f"  AbiturEliteCode  —  Multi-Platform Build Script")
    print(f"{'═'*55}{RESET}")
    info(f"Project : {PROJECT_DIR}")
    info(f"Desktop : {DESKTOP}")
    info(f"7-Zip   : {SEVEN_ZIP}")

    # Sanity checks before we do anything
    if not Path(PROJECT_DIR).exists():
        err(f"Project directory not found:\n     {PROJECT_DIR}")
        sys.exit(1)
    if not Path(SEVEN_ZIP).exists():
        err(f"7-Zip not found at:\n     {SEVEN_ZIP}")
        sys.exit(1)

    failed = []
    for runtime_id, label, zip_suffix in TARGETS:
        try:
            build_target(runtime_id, label, zip_suffix)
        except Exception as exc:
            print()
            err(f"{label} build FAILED: {exc}")
            failed.append(label)

    # ── Summary ───────────────────────────────────────────────────────────────
    print(f"\n{BOLD}{'═'*55}")
    print(f"  Summary")
    print(f"{'═'*55}{RESET}")
    for _, label, zip_suffix in TARGETS:
        zip_path = DESKTOP / f"{APP_NAME}-{zip_suffix}.zip"
        if label in failed:
            err(f"{label:<10}  FAILED")
        elif zip_path.exists():
            size_mb = zip_path.stat().st_size / 1_048_576
            ok(f"{label:<10}  {zip_path.name}  ({size_mb:.1f} MB)")
        else:
            err(f"{label:<10}  zip not found (unexpected)")

    if failed:
        print(f"\n{RED}{BOLD}  {len(failed)} build(s) failed.{RESET}")
        sys.exit(1)
    else:
        print(f"\n{GREEN}{BOLD}  All 3 builds completed successfully! 🎉{RESET}\n")

if __name__ == "__main__":
    main()