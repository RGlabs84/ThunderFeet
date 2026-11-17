#!/usr/bin/env python3
"""Fold the ThunderFeet release package (same rules as BarrkUI/fold-packages.py and DwarvenDefender).

THE RULE (the user, 2026-10-18): a RELEASE (plain Major.Minor.Patch) is ONE zip uploaded unchanged to Hexium AND
Thunderstore: HexiumDist/ThunderFeet-v<ver>.zip. A PRE-RELEASE (-alpha.N / -beta.N / -rc.N, manifest only) is
Hexium-only: HexiumDist/ThunderFeet-v<ver>-hexium.zip.

LAYOUT
    HexiumDist/manifest.json, {README,CHANGELOG,LICENSE}.md, icon.png,
    plugins/ThunderFeet/ThunderFeet.dll, plugins/ThunderFeet/icons/{Road,Stone,Wood,Earth,Grass,Frost}.png  -> the zip

    README.md is FORGED from README.src.md (README schema v3, ../libs-Tools/readme-forge/forge_readme.py) at every fold;
    never edit HexiumDist/README.md by hand.

USAGE
    dotnet build -c Release && python3 fold-package.py           forge the README, build the zip if it does not exist yet, verify it
    python3 fold-package.py --check-badges                       ... and fetch every badge once (200 + SVG)
    python3 fold-package.py --out-dir DIR                         write to DIR instead (trial builds)
    python3 fold-package.py --force                               rebuild a zip that exists

An existing zip is left alone unless --force: an uploaded version is frozen; change anything that ships and bump the
version instead. The fold is deterministic (fixed timestamps and attributes).
"""
import hashlib
import json
import os
import re
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
HEX = os.path.join(ROOT, "HexiumDist")
BUILT_DLL = os.path.join(ROOT, "bin", "Release", "net48", "ThunderFeet.dll")
FORGE = os.path.join(os.environ.get("LIBS_TOOLS", os.path.join(ROOT, "..", "libs-Tools")), "readme-forge", "forge_readme.py")
ICONS = ["Road", "Stone", "Wood", "Earth", "Grass", "Frost"]

NAME = "ThunderFeet"
ENTRIES = (["manifest.json", "README.md", "CHANGELOG.md", "LICENSE.md", "icon.png", f"plugins/{NAME}/{NAME}.dll"]
           + [f"plugins/{NAME}/icons/{i}.png" for i in ICONS])
MAX_DESCRIPTION = 250   # Thunderstore's limit
PRE_RELEASE = r"-(alpha|beta|rc)\.[1-9][0-9]*"


def md5(data):
    return hashlib.md5(data).hexdigest()


def read(path):
    with open(path, "rb") as fh:
        return fh.read()


def code_versions():
    csproj = re.search(r"<Version>([^<]+)</Version>", read(os.path.join(ROOT, f"{NAME}.csproj")).decode("utf-8-sig"))
    plugin = re.search(r'const string Version\s*=\s*"([^"]+)"', read(os.path.join(ROOT, "Plugin.cs")).decode("utf-8-sig"))
    return (csproj.group(1) if csproj else None, plugin.group(1) if plugin else None)


def validate():
    errors, notes = [], []

    def need(ok, message):
        if not ok:
            errors.append(message)

    csproj_version, plugin_version = code_versions()
    need(csproj_version and csproj_version == plugin_version,
         f"csproj <Version> ({csproj_version}) and Plugin.Version ({plugin_version}) disagree")
    need(re.fullmatch(r"\d+\.\d+\.\d+", plugin_version or "") is not None,
         f"Plugin.Version {plugin_version!r} must stay plain Major.Minor.Patch")

    for name in ENTRIES:
        need(os.path.isfile(os.path.join(HEX, name)), f"missing: HexiumDist/{name}")

    staged = os.path.join(HEX, "plugins", NAME, f"{NAME}.dll")
    need(os.path.isfile(BUILT_DLL), "no Release build (dotnet build -c Release)")
    if os.path.isfile(staged) and os.path.isfile(BUILT_DLL):
        need(md5(read(staged)) == md5(read(BUILT_DLL)), "HexiumDist/plugins DLL is not the Release build (stale copy)")
    for i in ICONS:
        src, dst = os.path.join(ROOT, "icons", f"{i}.png"), os.path.join(HEX, "plugins", NAME, "icons", f"{i}.png")
        need(os.path.isfile(src) and os.path.isfile(dst) and md5(read(src)) == md5(read(dst)),
             f"icons/{i}.png is missing or not staged (dotnet build -c Release copies it)")

    m = json.loads(read(os.path.join(HEX, "manifest.json")).decode("utf-8-sig"))
    v = m.get("version_number") or ""
    need(m.get("name") == NAME, f"manifest name is {m.get('name')!r}, expected {NAME!r}")
    need(re.fullmatch(re.escape(csproj_version or "") + "(" + PRE_RELEASE + ")?", v) is not None,
         f"manifest version {v!r} does not match the plugin version {csproj_version}")
    need(len(m.get("description", "")) <= MAX_DESCRIPTION, f"description is over {MAX_DESCRIPTION} characters")
    need(bool(m.get("dependencies")), "manifest lists no dependencies")
    need(re.fullmatch(r"[A-Za-z0-9_]+", m.get("name") or "") is not None, "name may only hold letters, digits and _")
    need(m.get("website_url", "").startswith("https://"), "website_url must be https://")
    icon = read(os.path.join(HEX, "icon.png")) if os.path.isfile(os.path.join(HEX, "icon.png")) else b""
    need(icon[:8] == b"\x89PNG\r\n\x1a\n" and icon[16:24] == (256).to_bytes(4, "big") * 2, "icon.png must be a 256x256 PNG")

    readme = read(os.path.join(HEX, "README.md")).decode("utf-8") if os.path.isfile(os.path.join(HEX, "README.md")) else ""
    need(readme.startswith("<!-- README schema v3 -->"), "README line 1 must be the schema v3 marker (forge it)")
    need("](#" not in readme and '<a href="#' not in readme, "README has jump links (stores break them)")
    need(re.search(r"!\[[^\]]*\]\((?!https://)", readme) is None, "README has an image without an https:// address")
    need(f"VERSION-{v.replace('-', '--')}-" in readme, f"README VERSION badge does not say {v}")
    need(f"NEW_IN_{v.replace('-', '--')}" in readme, f"README has no NEW IN {v} banner")
    changelog = read(os.path.join(HEX, "CHANGELOG.md")).decode("utf-8") if os.path.isfile(os.path.join(HEX, "CHANGELOG.md")) else ""
    need(f"## {v}" in changelog, f"CHANGELOG has no '## {v}' section")

    release = re.fullmatch(r"\d+\.\d+\.\d+", v) is not None
    notes.append("RELEASE: one zip for Hexium AND Thunderstore" if release
                 else "PRE-RELEASE: Hexium only (Thunderstore takes plain Major.Minor.Patch only)")
    return m, release, errors, notes


def fold(out_path):
    tmp = out_path + ".tmp"
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as zf:
        for entry in ENTRIES:
            info = zipfile.ZipInfo(entry, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            zf.writestr(info, read(os.path.join(HEX, entry)))
    with zipfile.ZipFile(tmp) as zf:
        assert zf.testzip() is None, "zip integrity check failed"
        assert zf.namelist() == ENTRIES, f"unexpected entries: {zf.namelist()}"
        for entry in ENTRIES:
            assert zf.read(entry) == read(os.path.join(HEX, entry)), f"{entry} in the zip differs from the file on disk"
    os.replace(tmp, out_path)


def describe(path):
    with zipfile.ZipFile(path) as zf:
        m = json.loads(zf.read("manifest.json").decode("utf-8-sig"))
        dll = md5(zf.read(f"plugins/{NAME}/{NAME}.dll"))
        print(f"    {os.path.relpath(path, ROOT)}  {os.path.getsize(path):,} bytes  md5 {md5(read(path))[:12]}")
        for i in zf.infolist():
            print(f"      {i.file_size:>9,}  {i.filename}")
    print(f"    manifest: name={m['name']}  version={m['version_number']}  dependencies={', '.join(m['dependencies'])}")
    print(f"    inner DLL md5 {dll}")


def main(argv):
    force = "--force" in argv
    out_dir = None
    if "--out-dir" in argv:
        out_dir = os.path.abspath(argv[argv.index("--out-dir") + 1])
        os.makedirs(out_dir, exist_ok=True)

    forge = [sys.executable, "-I", FORGE, os.path.join(ROOT, "README.src.md"), os.path.join(HEX, "README.md"),
             "--code-root", ROOT] + (["--check-badges"] if "--check-badges" in argv else [])
    if subprocess.run(forge).returncode != 0:
        print("NOT FOLDING - the README did not forge")
        return 1

    m, release, errors, notes = validate()
    for note in notes:
        print("note:", note)
    if errors:
        print("NOT FOLDING - fix these first:")
        for e in errors:
            print("  -", e)
        return 1

    v = m["version_number"]
    out_path = os.path.join(out_dir or HEX, f"{NAME}-v{v}.zip" if release else f"{NAME}-v{v}-hexium.zip")
    if os.path.exists(out_path) and not force:
        print(f"    {os.path.relpath(out_path, ROOT)} already exists - left alone (--force to rebuild; "
              "an uploaded version is frozen, bump the version instead)")
    else:
        fold(out_path)
        describe(out_path)
    print("Upload this same zip to Hexium AND Thunderstore." if release else "Pre-release: upload to Hexium only.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
