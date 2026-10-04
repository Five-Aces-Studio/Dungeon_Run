"""Derive lining-figure variants of Alegreya SC / Alegreya Sans for the Combat HUD V4.

Alegreya ships old-style figures by default ("FLOOR 01" reads as "FLOOR o1") and TextMesh Pro
cannot apply the OpenType `lnum`/`tnum` features. This script remaps the default digit code
points to the tabular lining glyphs already inside each font (`zero.tf` ... `nine.tf`) and
renames the family so the derived files never collide with upstream Alegreya installs.

Both fonts are SIL Open Font License 1.1 (no Reserved Font Name); the derived fonts stay OFL
and ship with the upstream OFL.txt files.

Usage (requires fontTools, e.g. `pip install fonttools` or PYTHONPATH to an extracted wheel):
    python make_lining_fonts.py <folder with upstream TTFs> <output folder>
Upstream sources: https://github.com/google/fonts/tree/main/ofl/alegreyasc and /alegreyasans
"""
import os
import sys

from fontTools.ttLib import TTFont

FAMILIES = {
    "AlegreyaSC": "Alegreya SC DR",
    "AlegreyaSans": "Alegreya Sans DR",
}
DIGITS = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"]


def derive(source_path, output_folder):
    font = TTFont(source_path)
    glyphs = set(font.getGlyphOrder())
    missing = [d + ".tf" for d in DIGITS if d + ".tf" not in glyphs]
    if missing:
        raise ValueError(f"{source_path}: missing lining glyphs {missing}")
    for table in font["cmap"].tables:
        if not table.isUnicode():
            continue
        for index, name in enumerate(DIGITS):
            if ord("0") + index in table.cmap:
                table.cmap[ord("0") + index] = name + ".tf"

    stem = os.path.splitext(os.path.basename(source_path))[0]
    upstream_family, style = stem.split("-", 1)
    family = FAMILIES[upstream_family]
    names = font["name"]
    for record in list(names.names):
        if record.nameID in (1, 16):
            names.setName(family, record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 4:
            names.setName(f"{family} {style}", 4, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 6:
            names.setName(f"{family.replace(' ', '')}-{style}", 6, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 3:
            names.setName(f"{family.replace(' ', '')}-{style};derived-lining", 3, record.platformID, record.platEncID, record.langID)

    output = os.path.join(output_folder, f"{upstream_family}DR-{style}.ttf")
    font.save(output)
    return output


def main():
    source, output = sys.argv[1], sys.argv[2]
    os.makedirs(output, exist_ok=True)
    for file in sorted(os.listdir(source)):
        if file.endswith(".ttf") and file.split("-", 1)[0] in FAMILIES:
            print(derive(os.path.join(source, file), output))


if __name__ == "__main__":
    main()
