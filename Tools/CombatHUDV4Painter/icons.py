"""Action icons: V4 painted variants of the V3 icons plus Combo / Counterattack / Charge / Miss.

The V3 originals (``Assets/Art/UI/CombatHUDV3/*Icon.png``, 1254x1254) are only READ.
V4 variants shift the pure-black ink to the warm ink colour, add a restrained brush
grain inside the fills and a slight warm-light / cool-shadow shift so they sit in the
acrylic world. New icons are composited from the V3 parts (sword, arcs, boot) or
painted with the same ink weight, and every icon gets a 96 px "Small" copy with a
slightly thickened ink for 30-44 px use.
"""
from __future__ import annotations

import math
from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

import kit
import paint as PT
import palette as P
import shapes as S

V3_DIR_REL = Path("Assets/Art/UI/CombatHUDV3")
SIZE = 1254
V3_NAMES = ("Attack", "Defence", "Dodge", "Heal", "Piercing")
NEW_NAMES = ("Combo", "Counterattack", "Charge", "Miss")
ALL_NAMES = V3_NAMES + NEW_NAMES
INK_PX = 22.0  # V3 outline weight at 1254 px


# ------------------------------------------------------------------ V3 access

@lru_cache(maxsize=None)
def _v3_rgba(repo_root: str, name: str) -> np.ndarray:
    img = Image.open(Path(repo_root) / V3_DIR_REL / f"{name}Icon.png").convert("RGBA")
    return np.asarray(img, np.float32) / 255.0


def v3_layer(repo_root, name: str) -> PT.Layer:
    arr = _v3_rgba(str(Path(repo_root)), name)
    L = PT.Layer(arr.shape[1], arr.shape[0])
    L.a = arr[..., 3].copy()
    L.c = arr[..., :3] * L.a[..., None]
    return L


def _components(alpha: np.ndarray):
    lab, n = ndimage.label(alpha > 0.1)
    sizes = ndimage.sum(np.ones_like(alpha), lab, index=np.arange(1, n + 1))
    return lab, n, sizes


def part_masks(repo_root, name: str):
    """Split a V3 icon into its largest component (the object) and the rest (motion lines)."""
    arr = _v3_rgba(str(Path(repo_root)), name)
    lab, n, sizes = _components(arr[..., 3])
    main = int(np.argmax(sizes)) + 1
    grow = lambda m: ndimage.binary_dilation(m, iterations=3)  # noqa: E731 (include the AA fringe)
    obj = grow(lab == main)
    rest = grow((lab > 0) & (lab != main)) & ~obj
    return obj.astype(np.float32), rest.astype(np.float32)


def masked(L: PT.Layer, m: np.ndarray) -> PT.Layer:
    out = PT.Layer(L.w, L.h)
    out.a = L.a * m
    out.c = L.c * m[..., None]
    return out


def transformed(L: PT.Layer, *, scale=1.0, dx=0.0, dy=0.0, mirror=False, centre=(SIZE / 2, SIZE / 2)) -> PT.Layer:
    """Affine copy (scale about ``centre``, then translate; optional horizontal mirror), premultiplied."""
    cx, cy = centre
    out = PT.Layer(L.w, L.h)
    yy, xx = np.mgrid[0:L.h, 0:L.w].astype(np.float32)
    sx = (xx - cx - dx) / scale
    sy = (yy - cy - dy) / scale
    if mirror:
        sx = -sx
    src_x, src_y = sx + cx, sy + cy
    coords = [src_y, src_x]
    out.a = ndimage.map_coordinates(L.a, coords, order=1, mode="constant", cval=0.0)
    out.c = np.stack([ndimage.map_coordinates(L.c[..., i], coords, order=1, mode="constant", cval=0.0)
                      for i in range(3)], -1)
    return out


# --------------------------------------------------------------- V4 treatment

def painterly(L: PT.Layer, ctx: PT.Ctx, *, grain=0.07) -> PT.Layer:
    """Warm ink, restrained world-brush grain in the fills, warm light / cool shadow, no flat white."""
    a = L.a
    rgb = L.straight()
    lum = P.luminance(rgb)
    sat = rgb.max(-1) - rgb.min(-1)
    inkness = (1.0 - PT.smoothstep(0.05, 0.24, lum)) * (1.0 - PT.smoothstep(0.08, 0.3, sat))
    out = P.mix(rgb, P.INK, inkness)
    fill = 1.0 - inkness
    g = ctx.grain(texel=7.0, angle=20.0, stretch=1.3)
    out = out * (1.0 + grain * (g.r - 0.5) * 2.0 * fill)[..., None]
    out = PT.warm_cool(out, (0.07 * (lum - 0.45) * 2.0 + 0.03 * (g.g - 0.5) * 2.0) * fill)
    white = PT.smoothstep(0.82, 0.97, lum) * (1.0 - PT.smoothstep(0.05, 0.2, sat))
    out = P.mix(out, P.IVORY_TEXT, white * 0.75)
    res = PT.Layer(L.w, L.h)
    res.a = a.copy()
    res.c = np.clip(out, 0.0, 1.0) * a[..., None]
    return res


def v4_icon(ctx: PT.Ctx, name: str) -> PT.Layer:
    return painterly(v3_layer(ctx.repo_root, name), ctx)


def _sword_parts(repo_root):
    L = v3_layer(repo_root, "Attack")
    obj, rest = part_masks(repo_root, "Attack")
    return masked(L, obj), masked(L, rest)


def paint_combo(ctx: PT.Ctx) -> PT.Layer:
    sword, arcs = _sword_parts(ctx.repo_root)
    c = (666.0, 645.0)  # V3 sword centre
    back = transformed(sword, scale=0.8, dx=-104, dy=-120, centre=c)
    back.c *= np.array([0.74, 0.76, 0.82], np.float32)  # darker and a touch cooler: it sits behind
    back_arcs = transformed(arcs, scale=0.8, dx=-104, dy=-120, centre=c)
    front = transformed(sword, scale=0.8, dx=92, dy=70, centre=c)
    out = PT.Layer(SIZE, SIZE)
    out.composite(back_arcs)
    out.composite(back)
    # a thin warm-ink separation under the front sword so the overlap reads clearly
    sep = PT.blur(ndimage.binary_dilation(front.a > 0.5, iterations=9).astype(np.float32), 1.0)
    out.paint(P.INK, sep * (out.a > 0.02))
    out.composite(front)
    return painterly(out, ctx)


def paint_counterattack(ctx: PT.Ctx) -> PT.Layer:
    Lb = v3_layer(ctx.repo_root, "Dodge")
    boot_m, _ = part_masks(ctx.repo_root, "Dodge")
    boot = masked(Lb, boot_m)
    ys, xs = np.nonzero(boot.a > 0.1)
    bc = ((xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0)
    boot_t = transformed(boot, scale=0.74, dx=410 - bc[0], dy=700 - bc[1], centre=bc)
    sword, arcs = _sword_parts(ctx.repo_root)
    c = (666.0, 645.0)
    sw = transformed(sword, scale=0.82, dx=150, dy=-30, mirror=True, centre=c)
    ar = transformed(arcs, scale=0.82, dx=150, dy=-30, mirror=True, centre=c)
    out = PT.Layer(SIZE, SIZE)
    out.composite(boot_t)
    out.composite(ar)
    sep = PT.blur(ndimage.binary_dilation(sw.a > 0.5, iterations=9).astype(np.float32), 1.0)
    out.paint(P.INK, sep * (out.a > 0.02))
    out.composite(sw)
    return painterly(out, ctx)


def _ink(L, ctx, sd, w=INK_PX):
    PT.ink_contour(L, ctx, S.offset(sd, w * 0.5), w_min=w * 0.85, w_max=w * 1.12, light_bias=0.3)


def paint_charge(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(SIZE, SIZE)
    cx, cy = 627.0, 668.0
    X, Y = ctx.X, ctx.Y
    # faint radial "charge" arcs (behind the clock, upper right)
    for i, (r, a0, a1) in enumerate(((505, -1.25, -0.35), (560, -1.1, -0.55), (505, 2.55, 3.0))):
        arc = S.sd_arc(X, Y, cx, cy, r, a0, a1, 20.0)
        L.paint(P.mix(P.AMBER, P.EMBER, 0.4), PT.coverage(arc) * 0.55)
        _ink(L, ctx, arc, 10.0)
    ring = S.perturb(S.sd_circle(X, Y, cx, cy, 440), ctx.noise(cell=160), 2.5)
    face = S.sd_circle(X, Y, cx, cy, 372)
    crown = S.sd_capsule(X, Y, cx, cy - 470, cx, cy - 520, 42)
    crown = S.union(crown, S.sd_rect(X, Y, cx - 22, cy - 480, cx + 22, cy - 430))
    # crown + ring: bronze, cel two-tone
    side = (X - cx) * 0.62 + (Y - cy) * 0.78
    lit = 1.0 - PT.smoothstep(-6.0, 6.0, side)
    L.paint(P.mix(P.BRONZE_DARK, P.BRONZE_LIGHT, lit), PT.coverage(crown))
    _ink(L, ctx, crown)
    L.paint(P.mix(P.BRONZE, P.GOLD, lit * 0.8), PT.coverage(ring))
    band = PT.coverage(ring) * (1.0 - PT.smoothstep(-24.0, -16.0, -S.sd_circle(X, Y, cx, cy, 440)))
    L.paint(P.BRONZE_DARK, band * (1 - lit) * 0.6)
    # ivory face with a cel shadow crescent (lower right)
    shade = PT.smoothstep(-4.0, 4.0, np.hypot(X - cx + 70, Y - cy + 90) - 400)
    L.paint(P.mix(P.IVORY_TEXT, P.PARCH_SHADOW, shade * 0.85), PT.coverage(face))
    _ink(L, ctx, face, 14.0)
    # 12 ticks (longer at the quarters)
    for i in range(12):
        th = i * math.pi / 6
        r0 = 300 if i % 3 == 0 else 322
        t = S.sd_capsule(X, Y, cx + r0 * math.sin(th), cy - r0 * math.cos(th), cx + 348 * math.sin(th),
                         cy - 348 * math.cos(th), 13 if i % 3 == 0 else 9)
        L.paint(P.INK, PT.coverage(t))
    # hands: hour to 12, minute to 3 (as on the tabletop sheet)
    hour = S.sd_tapered_capsule(X, Y, cx, cy, cx, cy - 250, 26, 12)
    minute = S.sd_tapered_capsule(X, Y, cx, cy, cx + 300, cy, 22, 10)
    L.paint(P.INK, PT.coverage(S.union(hour, minute)))
    hub = S.sd_circle(X, Y, cx, cy, 40)
    L.paint(P.BRONZE_LIGHT, PT.coverage(hub))
    _ink(L, ctx, hub, 12.0)
    _ink(L, ctx, ring)
    PT.stamp_ellipse(L, cx - 250, cy - 250, 60, 22, -0.78, P.IVORY_TEXT, 0.55, clip=PT.coverage(ring))
    return painterly(L, ctx, grain=0.05)


def paint_miss(ctx: PT.Ctx) -> PT.Layer:
    sword, arcs = _sword_parts(ctx.repo_root)
    ys, xs = np.nonzero(arcs.a > 0.1)
    ac = ((xs.min() + xs.max()) / 2.0, (ys.min() + ys.max()) / 2.0)
    out = PT.Layer(SIZE, SIZE)
    # faded blade tip entering from the right (only the last part of the blade)
    dx, dy = 40.0, 80.0
    tip = transformed(sword, scale=1.0, dx=dx, dy=dy, centre=(666.0, 645.0))
    X, Y = S.grid(SIZE, SIZE)
    tx, ty = 1107.0 + dx, 146.0 + dy  # translated blade tip
    along = (tx - X) * 0.707 + (Y - ty) * 0.707  # distance from the tip toward the hilt
    keep = 1.0 - PT.smoothstep(200.0, 330.0, along)
    tip.a *= keep * 0.45
    tip.c *= (keep * 0.45)[..., None]
    out.composite(tip)
    arcs_t = transformed(arcs, scale=1.25, dx=560 - ac[0], dy=520 - ac[1], centre=ac)
    out.composite(arcs_t)
    # dust puff: a few overlapping painted blobs with the same ink weight. Round 3: about 2x the
    # area of round 2 (radii scaled by sqrt(2)) so the MISS card does not read as empty.
    L = PT.Layer(SIZE, SIZE)
    blobs = [(740, 870, 130), (840, 830, 158), (930, 890, 119), (640, 900, 88), (820, 925, 99)]
    sd = None
    for bx, by, br in blobs:
        c = S.sd_circle(X, Y, bx, by, br)
        sd = c if sd is None else S.smooth_union(sd, c, 18.0)
    sd = S.perturb(sd, ctx.noise(cell=90), 5.0)
    side = (X - 820) * 0.62 + (Y - 860) * 0.78
    lit = 1.0 - PT.smoothstep(-40.0, 40.0, side)
    L.paint(P.mix(P.mix(P.PARCH_SHADOW, P.STEEL_DARK, 0.4), P.mix(P.PARCH, P.STEEL, 0.3), lit), PT.coverage(sd))
    _ink(L, ctx, sd)
    for (px, py, pr) in ((600, 800, 16), (1010, 800, 20), (1060, 900, 13), (560, 960, 12)):
        pc = S.sd_circle(X, Y, px, py, pr)
        L.paint(P.mix(P.PARCH_SHADOW, P.STEEL, 0.3), PT.coverage(pc))
        _ink(L, ctx, pc, 9.0)
    out.composite(L)
    return painterly(out, ctx)


# ---------------------------------------------------------------- small copies

def thickened_small(L: PT.Layer, size=96, grow_px=10) -> PT.Layer:
    """Downscale an icon with a slightly thicker ink (outer contour grown, inner ink lines widened)."""
    rgb = L.straight()
    lum = P.luminance(rgb)
    sat = rgb.max(-1) - rgb.min(-1)
    ink = ((lum < 0.16) & (sat < 0.12) & (L.a > 0.5)).astype(np.float32)
    grown_ink = ndimage.maximum_filter(ink, size=7)
    work = PT.Layer(L.w, L.h)
    work.c, work.a = L.c.copy(), L.a.copy()
    inside = np.clip(L.a, 0, 1)
    work.paint(P.INK, grown_ink * inside * 0.9)
    outer = PT.blur(ndimage.binary_dilation(L.a > 0.5, iterations=grow_px).astype(np.float32), 1.5)
    work.paint_under(P.INK, outer)
    # thin warm parchment halo outside the ink: keeps 30-44 px icons legible on dark HUD stone
    halo = PT.blur(ndimage.binary_dilation(L.a > 0.5, iterations=grow_px + 16).astype(np.float32), 2.0)
    work.paint_under(P.mix(P.PARCH_SHADOW, P.PARCH, 0.3), halo * 0.6)
    img = work.to_image().convert("RGBa")  # premultiplied resampling: no dark fringes
    pad = 2
    inner = img.resize((size - 2 * pad, size - 2 * pad), Image.LANCZOS)
    canvas = Image.new("RGBa", (size, size), (0, 0, 0, 0))
    canvas.paste(inner, (pad, pad))
    return PT.Layer.from_image(canvas.convert("RGBA"))


_FULL_CACHE: dict = {}


def full_icon(ctx: PT.Ctx, name: str) -> PT.Layer:
    key = (str(ctx.repo_root), name)
    if key not in _FULL_CACHE:
        if name in V3_NAMES:
            L = v4_icon(ctx, name)
        else:
            L = {"Combo": paint_combo, "Counterattack": paint_counterattack, "Charge": paint_charge,
                 "Miss": paint_miss}[name](ctx)
        kit.tidy_alpha(L)
        _FULL_CACHE[key] = L
    return _FULL_CACHE[key]


def paint_icon(ctx: PT.Ctx, name: str) -> PT.Layer:
    return full_icon(PT.Ctx(f"Icons/{name}Icon", SIZE, SIZE, ctx.repo_root), name)


def paint_small_icon(ctx: PT.Ctx, name: str) -> PT.Layer:
    big = full_icon(PT.Ctx(f"Icons/{name}Icon", SIZE, SIZE, ctx.repo_root), name)
    out = thickened_small(big, ctx.w)
    kit.tidy_alpha(out)
    return out


def clear_cache():
    _FULL_CACHE.clear()
