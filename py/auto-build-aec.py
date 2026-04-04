import subprocess
import sys
import os
import shutil
import stat
import zipfile
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

# Minimal Info.plist for the macOS .app bundle
MAC_INFO_PLIST = """\
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>{app_name}</string>
    <key>CFBundleIdentifier</key>
    <string>com.onlycook.abiturelitecode</string>
    <key>CFBundleName</key>
    <string>{app_name}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
</dict>
</plist>
""".format(app_name=APP_NAME)

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

# ─── macOS PACKAGING ──────────────────────────────────────────────────────────

def build_mac_bundle(publish_src: Path, staging: Path) -> Path:
    """
    Create a proper .app bundle from the published output.

    Structure:
        AbiturEliteCode.app/
            Contents/
                Info.plist
                MacOS/
                    AbiturEliteCode   ← main executable + any dylibs
    """
    app_bundle = staging / f"{APP_NAME}.app"
    macos_dir  = app_bundle / "Contents" / "MacOS"
    macos_dir.mkdir(parents=True)

    # Copy every published file into Contents/MacOS/
    for item in publish_src.iterdir():
        dest = macos_dir / item.name
        if item.is_dir():
            shutil.copytree(item, dest)
        else:
            shutil.copy2(item, dest)

    # Write Info.plist
    (app_bundle / "Contents" / "Info.plist").write_text(MAC_INFO_PLIST, encoding="utf-8")

    return app_bundle


def zip_mac_bundle(app_bundle: Path, zip_dest: Path) -> None:
    """
    Zip the .app bundle using Python's zipfile so that Unix permissions
    (especially the executable bit) are preserved correctly on macOS.

    7-Zip running on Windows cannot set Unix permission bits, which would
    cause macOS to refuse to launch the binary. This function sets:
      • 0o755 (rwxr-xr-x) on the main executable
      • 0o644 (rw-r--r--) on every other file
      • 0o755 on directories
    """
    base = app_bundle.parent   # paths in the zip are relative to the staging dir

    with zipfile.ZipFile(zip_dest, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
        for path in sorted(app_bundle.rglob("*")):
            arcname = str(path.relative_to(base)).replace("\\", "/")

            if path.is_dir():
                info = zipfile.ZipInfo(arcname + "/")
                info.external_attr = (stat.S_IFDIR | 0o755) << 16
                zf.writestr(info, b"")
            else:
                zinfo = zipfile.ZipInfo(arcname)
                zinfo.compress_type = zipfile.ZIP_DEFLATED

                # The main executable lives directly in Contents/MacOS/ with
                # no file extension — give it the executable bit.
                is_main_exe = (
                    path.parent.name == "MacOS"
                    and path.name == APP_NAME
                )
                unix_mode = 0o755 if is_main_exe else 0o644
                zinfo.external_attr = (stat.S_IFREG | unix_mode) << 16

                with open(path, "rb") as fh:
                    zf.writestr(zinfo, fh.read())

# ─── BUILD STEPS ──────────────────────────────────────────────────────────────
# 5 steps per target (same count for all platforms):
#   1  Prepare staging dir
#   2  dotnet publish
#   3  Stage files (copy / create .app bundle)
#   4  Compress (7-Zip for win/linux, Python zipfile for macOS)
#   5  Cleanup

def build_target(runtime_id: str, label: str, zip_suffix: str) -> None:
    section(f"Building for {label}  ({runtime_id}-x64)")
    STEPS = 5

    is_mac = runtime_id == "osx"

    # ── Step 1: Prepare temp staging dir ──────────────────────────────────────
    progress_bar(1, STEPS, "Preparing staging directory …")
    publish_src = Path(PUBLISH_BASE) / f"{runtime_id}-x64" / "publish"
    staging     = DESKTOP / f"_tmp_{APP_NAME}_{runtime_id}"
    zip_name    = f"{APP_NAME}-{zip_suffix}.zip"
    zip_dest    = DESKTOP / zip_name

    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
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

    # ── Step 3: Stage published files ─────────────────────────────────────────
    if not publish_src.exists():
        raise FileNotFoundError(f"Publish output not found: {publish_src}")
    files = list(publish_src.iterdir())
    if not files:
        raise FileNotFoundError(f"No files found in publish directory: {publish_src}")

    if is_mac:
        progress_bar(3, STEPS, "Creating .app bundle …")
        bundle_path = build_mac_bundle(publish_src, staging)
        progress_bar(3, STEPS, f".app bundle created")
    else:
        progress_bar(3, STEPS, "Copying published files …")
        app_folder = staging / APP_NAME
        app_folder.mkdir()
        for f in files:
            dest = app_folder / f.name
            if f.is_dir():
                shutil.copytree(f, dest)
            else:
                shutil.copy2(f, dest)
        progress_bar(3, STEPS, f"Copied {len(files)} item(s)")

    # ── Step 4: Compress ───────────────────────────────────────────────────────
    if is_mac:
        progress_bar(4, STEPS, f"Zipping .app bundle (Python) …")
        zip_mac_bundle(bundle_path, zip_dest)
        progress_bar(4, STEPS, "Zip created (permissions preserved)")
    else:
        progress_bar(4, STEPS, f"Creating {zip_name} via 7-Zip …")
        run(
            [SEVEN_ZIP, "a", str(zip_dest), APP_NAME],
            cwd=str(staging),
        )
        progress_bar(4, STEPS, "Zip created")

    # ── Step 5: Cleanup ───────────────────────────────────────────────────────
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