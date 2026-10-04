"""Combat HUD V4 painter CLI.

Paints the complete, deterministic V4 sprite set (seed 4242) into
``Assets/Art/UI/CombatHUDV4/{Cards,Controls,Crest,Enemy,Static,Glyphs,Icons}`` and
writes ``Assets/Settings/CombatHUDV4/HudLayoutV4.json``.

    python Tools/CombatHUDV4Painter/paint_hud_v4.py --all
    python Tools/CombatHUDV4Painter/paint_hud_v4.py --only cards,controls
    python Tools/CombatHUDV4Painter/paint_hud_v4.py --list

A path guard refuses any write outside the three V4 output folders (and never
touches the Fonts folder). ``--out-root`` redirects the same relative layout into
another directory (used by the tests); inputs are always read from the repo.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import icons  # noqa: E402
import layout  # noqa: E402
import paint as PT  # noqa: E402
import registry  # noqa: E402

REPO_ROOT = HERE.parents[1]
ALLOWED_PREFIXES = ("Assets/Art/UI/CombatHUDV4/", "Assets/Settings/CombatHUDV4/", "Captures/CombatHUDV4/")
FORBIDDEN_PREFIXES = ("Assets/Art/UI/CombatHUDV4/Fonts/",)
LAYOUT_REL = "Assets/Settings/CombatHUDV4/HudLayoutV4.json"


class PathGuardError(RuntimeError):
    pass


def guard(path, out_root=REPO_ROOT) -> Path:
    """Return the resolved path if it lies inside an allowed V4 output folder, else raise."""
    root = Path(out_root).resolve()
    target = Path(path)
    if not target.is_absolute():
        target = root / target
    target = target.resolve()
    try:
        rel = target.relative_to(root).as_posix()
    except ValueError as exc:
        raise PathGuardError(f"refusing to write outside the output root: {target}") from exc
    if not any(rel.startswith(p) for p in ALLOWED_PREFIXES):
        raise PathGuardError(f"refusing to write outside the V4 folders: {rel}")
    if any(rel.startswith(p) for p in FORBIDDEN_PREFIXES):
        raise PathGuardError(f"refusing to write into a protected folder: {rel}")
    return target


def save_png(layer: PT.Layer, path, out_root=REPO_ROOT) -> Path:
    target = guard(path, out_root)
    target.parent.mkdir(parents=True, exist_ok=True)
    layer.to_image().save(target, format="PNG", optimize=False, compress_level=6)
    return target


def write_layout(out_root=REPO_ROOT) -> Path:
    target = guard(LAYOUT_REL, out_root)
    target.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(layout.build(), indent=2, ensure_ascii=False) + "\n"
    target.write_text(data, encoding="utf-8", newline="\n")
    return target


def select(groups=None, names=None):
    out = []
    for sp in registry.SPRITES:
        if groups and sp.group not in groups:
            continue
        if names and not any(n.lower() in sp.rel.lower() for n in names):
            continue
        out.append(sp)
    return out


def paint_sprite(sp, repo_root=REPO_ROOT) -> PT.Layer:
    w, h = sp.size
    ctx = PT.Ctx(sp.rel.rsplit(".", 1)[0], w, h, repo_root)
    layer = sp.fn(ctx)
    if (layer.w, layer.h) != (w, h):
        raise ValueError(f"{sp.rel}: painted {layer.w}x{layer.h}, expected {w}x{h}")
    return layer


def paint_all(groups=None, names=None, out_root=REPO_ROOT, repo_root=REPO_ROOT, verbose=True, layout_json=True):
    PT.opt_out_of_power_throttling()
    icons.clear_cache()
    written = []
    t0 = time.time()
    for sp in select(groups, names):
        t = time.time()
        layer = paint_sprite(sp, repo_root)
        path = save_png(layer, Path(registry.SPRITE_ROOT_REL) / sp.rel, out_root)
        written.append(path)
        if verbose:
            print(f"  {sp.rel:44s} {sp.size[0]:4d}x{sp.size[1]:<4d} {time.time() - t:5.1f}s", flush=True)
    if layout_json and (not groups or "layout" in groups):
        written.append(write_layout(out_root))
        if verbose:
            print(f"  {LAYOUT_REL}")
    if verbose:
        print(f"painted {len(written)} files in {time.time() - t0:.1f}s")
    return written


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--all", action="store_true", help="paint every sprite and write the layout JSON")
    g.add_argument("--only", help="comma-separated groups: " + ",".join(registry.GROUPS + ("layout",)))
    g.add_argument("--list", action="store_true", help="list sprites and exit")
    ap.add_argument("--name", help="comma-separated substrings to filter sprite paths (with --only/--all)")
    ap.add_argument("--out-root", default=str(REPO_ROOT), help="output root (default: the repo root)")
    a = ap.parse_args(argv)
    if a.list:
        for sp in registry.SPRITES:
            print(f"{sp.group:9s} {sp.rel:44s} {sp.size[0]}x{sp.size[1]}")
        return 0
    groups = None
    if a.only:
        groups = [x.strip() for x in a.only.split(",") if x.strip()]
        bad = [x for x in groups if x not in registry.GROUPS + ("layout",)]
        if bad:
            ap.error(f"unknown group(s): {', '.join(bad)}")
    names = [x.strip() for x in a.name.split(",")] if a.name else None
    paint_all(groups, names, out_root=Path(a.out_root))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
