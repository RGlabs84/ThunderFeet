"""Generated blocks for README.src.md (called by libs-Tools/readme-forge/forge_readme.py).

The Ledger is read from the code (Surface.cs: SurfaceInfo.All and SurfaceInfo.GroupOf), so the store page always shows
the defaults the DLL ships with.
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))

# What each rune seals on this page (the rune legend's last column).
RUNE_SEALS = {
    "ᚦ": "the name: thunder underfoot",
    "ᛞ": "what is new",
    "ᚾ": "what you need to know first",
    "ᚱ": "the speeds",
    "ᚨ": "the HUD icon",
    "ᚲ": "the rune trail",
    "ᛉ": "the server's authority",
    "ᛃ": "the ledger of speeds",
    "ᚠ": "the toolbox",
    "ᚷ": "multiplayer",
    "ᛗ": "other mods",
    "ᛚ": "the settings",
    "ᛟ": "the credits",
}

FAMILY_EMOJI = {"Road": "🛣️", "Stone": "🪨", "Wood": "🪵", "Earth": "🟤", "Grass": "🌿", "Frost": "❄️"}
FAMILY_ORDER = ["Road", "Stone", "Wood", "Grass", "Earth", "Frost"]
HIGHLIGHTS = ["PavedRoad", "BuiltStone", "BuiltWood", "DirtPath", "MeadowsGrass", "SwampMud", "Snow", "DeepSnow"]


def _surfaces():
    with open(os.path.join(HERE, "Surface.cs"), encoding="utf-8-sig") as fh:
        src = fh.read()
    rows = re.findall(r'I\(Surface\.(\w+),\s*"[^"]*",\s*"([^"]*)",\s*"(?:[^"\\]|\\.)*",\s*(\d+),\s*(\d+)', src)
    if len(rows) < 30:
        raise SystemExit(f"readme_gen: read only {len(rows)} surfaces from Surface.cs")

    body = re.search(r"static IconGroup GroupOf\(Surface s\)(.*?)\n        }", src, re.S).group(1)
    family, pending = {}, []
    for case, ret in re.findall(r"case Surface\.(\w+)|return IconGroup\.(\w+);", body):
        if case:
            pending.append(case)
        elif ret:
            for s in pending:
                family[s] = ret
            pending = []
    default = re.search(r"default:\s*return IconGroup\.(\w+);", body).group(1)

    return [dict(key=k, name=n, sprint=int(s), jog=int(j), family=family.get(k, default)) for k, n, s, j in rows]


def _pct(v):
    d = v - 100
    return "±0%" if d == 0 else f"{d:+d}%"


def _row(forge, s):
    sprint = forge.token(_Match("gauge", str(s["sprint"] - 100)))
    jog = forge.token(_Match("gauge", str(s["jog"] - 100)))
    return f"| **{s['name']}** | {FAMILY_EMOJI[s['family']]} {s['family']} | {sprint} **{_pct(s['sprint'])}** | {jog} **{_pct(s['jog'])}** |"


class _Match:
    def __init__(self, kind, arg):
        self._g = (kind, arg)

    def group(self, i):
        return self._g[i - 1]


def ledger(forge):
    surfaces = _surfaces()
    by_key = {s["key"]: s for s in surfaces}
    head = "| Ground | Icon | Sprint | Jog · walk |\n|---|---|---|---|"

    out = [head] + [_row(forge, by_key[k]) for k in HIGHLIGHTS]
    out += ["", "<details>", f"<summary>📜 <b>The whole ledger</b> — all {len(surfaces)} grounds, by icon</summary>", "", head]
    for fam in FAMILY_ORDER:
        for s in surfaces:
            if s["family"] == fam:
                out.append(_row(forge, s))
    out += ["", "</details>"]
    return "\n".join(out)
