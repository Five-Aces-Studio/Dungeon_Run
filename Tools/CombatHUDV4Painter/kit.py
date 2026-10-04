"""Shared composite recipes for the V4 painter (plates, gems, studs, finials).

Every recipe paints into a ``paint.Layer`` using a ``paint.Ctx`` so the
deterministic generator and the world brush grain are shared per sprite. All
coordinates are output pixels (2x the 1080 reference) unless a helper says
otherwise; ``K`` converts reference units.
"""
from __future__ import annotations

import math

import numpy as np
from scipy import ndimage

import paint as PT
import palette as P
import shapes as S

K = 2  # output pixels per 1080-reference unit


def k(*vals):
    """Reference units -> output pixels (scalar or tuple)."""
    if len(vals) == 1:
        return vals[0] * K
    return tuple(v * K for v in vals)


def kpts(pts):
    return [(x * K, y * K) for x, y in pts]


def silhouette(ctx: PT.Ctx, pts_or_sd, *, amp=0.9, cell=58.0):
    """SDF of an outline with low-frequency contour noise (no perfectly straight edge)."""
    sd = S.sd_polygon(ctx.X, ctx.Y, pts_or_sd) if isinstance(pts_or_sd, (list, tuple)) else pts_or_sd
    return S.perturb(sd, ctx.noise(cell=cell, octaves=2), amp)


def inset(ctx: PT.Ctx, sd, width, *, amp=0.6, cell=40.0):
    """Inner boundary of a rim band of ``width`` px, gently irregular."""
    return S.perturb(S.offset(sd, -width), ctx.noise(cell=cell, octaves=2), amp)


# ---------------------------------------------------------------- plate recipe

def plate(layer: PT.Layer, ctx: PT.Ctx, sd_outer, *, rim=12.0, style: PT.MetalStyle = PT.BRONZE, face="stone",
          face_sd=None, face_kw=None, zones=None, dabs=10, chip_count=5, ink=(2.2, 3.4), inner_ink=1.5,
          halo=(6.0, 0.42), grain=None, dab_colour=P.GOLD, zone_feather=3.0):
    """Bronze-rimmed plate: cel metal band, painted face, inner/outer ink, wear and a contact halo.

    ``face`` is "stone", "parch", "none" or a callable ``f(layer, ctx, sd_face)``.
    Returns a dict with the fields used for later decoration.
    """
    face_kw = face_kw or {}
    if face_sd is None:
        face_sd = inset(ctx, sd_outer, rim)
    g = grain if grain is not None else ctx.base_grain()
    lam, region = PT.metal(layer, ctx, sd_outer, sd_hole=face_sd, rim_out=min(rim * 0.55, 8.0),
                           rim_in=min(rim * 0.45, 6.0), style=style, grain=g)
    clean = PT.zone_mask(ctx, zones, feather=zone_feather + 1.0) if zones else None
    if face == "stone":
        PT.stone(layer, ctx, S.offset(face_sd, 0.6), clean=clean, **face_kw)
    elif face == "parch":
        PT.parchment(layer, ctx, S.offset(face_sd, 0.6), clean=clean, **face_kw)
    elif callable(face):
        face(layer, ctx, face_sd)
    if inner_ink:
        PT.ink_line(layer, ctx, face_sd, at=0.4, width=inner_ink, alpha=0.9)
    if dabs:
        PT.worn_dabs(layer, ctx, lam, region, sd_outer, count=dabs, grain=g, colour=dab_colour)
    if chip_count:
        PT.chips(layer, ctx, sd_outer, count=chip_count, band=(ink[1] + 0.3, ink[1] + 2.6))
    PT.ink_contour(layer, ctx, sd_outer, w_min=ink[0], w_max=ink[1])
    if zones:
        PT.flatten_zones(layer, ctx, zones, feather=zone_feather)
    if halo:
        PT.halo(layer, sd_outer, radius=halo[0], alpha=halo[1])
    return {"sd": sd_outer, "face": face_sd, "lam": lam, "region": region, "grain": g}


# ------------------------------------------------------------------ ornaments

def gem(layer: PT.Layer, ctx: PT.Ctx, pts, colour, *, lit=0.0, glint=True, ink_w=(1.2, 1.8), setting=2.4,
        setting_style=PT.BRONZE, facet=True):
    """Faceted gem in a thin bronze setting. ``lit`` (0..1) adds an inner glow (amber gems)."""
    sd = S.sd_polygon(ctx.X, ctx.Y, pts)
    if setting > 0:
        PT.metal(layer, ctx, S.offset(sd, setting), sd_hole=sd, rim_out=setting * 0.8, rim_in=setting * 0.6,
                 style=setting_style)
    col_dark = P.scale(colour, 0.55)
    col_light = P.mix(colour, P.PARCH_LIGHT, 0.45)
    f = PT.facing_light(sd)
    d = -sd
    depth = np.clip(d / max(float(d.max()), 1e-3), 0.0, 1.0)
    # facets: lit upper-left half vs shadowed lower-right half, split by the diagonal through the centre
    cx = float(np.mean([p[0] for p in pts]))
    cy = float(np.mean([p[1] for p in pts]))
    side = ((ctx.X - cx) * 0.62 + (ctx.Y - cy) * 0.78)
    lvl = 1.0 - PT.smoothstep(-0.8, 0.8, side) if facet else np.full_like(side, 0.5)
    lvl = lvl + 0.35 * (1.0 - PT.smoothstep(0.0, 3.0, d)) * (-f)  # darker rim facing the light (inset)
    col = PT.ramp(np.clip(lvl * 2.0, 0.0, 2.0), [col_dark, colour, col_light])
    if lit > 0:
        col = P.mix(col, P.mix(colour, P.EMBER, 0.6), depth * lit * 0.6)
    layer.paint(col, PT.coverage(sd))
    if glint:
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        gx = cx - (max(xs) - min(xs)) * 0.17
        gy = cy - (max(ys) - min(ys)) * 0.19
        r = max(min(max(xs) - min(xs), max(ys) - min(ys)) * 0.09, 0.9)
        PT.stamp_ellipse(layer, gx, gy, r * 1.6, r, -0.7, P.mix(P.PARCH_LIGHT, colour, 0.15), 0.85 + 0.1 * lit,
                         clip=PT.coverage(sd))
    PT.ink_contour(layer, ctx, S.offset(sd, setting), w_min=ink_w[0], w_max=ink_w[1])
    return sd


def stud_triangle(layer: PT.Layer, ctx: PT.Ctx, cx, cy, r, angle=-90.0, style=PT.BRONZE, ink_w=(1.1, 1.6)):
    """Small raised trigonal rivet (Dungeon Run motif)."""
    sd = S.sd_polygon(ctx.X, ctx.Y, S.triangle(cx, cy, r, angle))
    sd = S.offset(sd, 0.0)
    PT.metal(layer, ctx, sd, rim_out=r * 0.6, style=style)
    PT.ink_contour(layer, ctx, sd, w_min=ink_w[0], w_max=ink_w[1])
    return sd


def bolt(layer: PT.Layer, ctx: PT.Ctx, cx, cy, r, style=PT.BRONZE):
    sd = S.sd_circle(ctx.X, ctx.Y, cx, cy, r)
    PT.metal(layer, ctx, sd, rim_out=r * 0.9, style=style)
    PT.ink_contour(layer, ctx, sd, w_min=0.9, w_max=1.3)


def soft_disc(ctx: PT.Ctx, cx, cy, rx, ry, *, feather=1.0):
    """Soft elliptical mask 1 at the centre fading to 0 at the ellipse boundary * (1 + feather)."""
    k_ = np.hypot((ctx.X - cx) / rx, (ctx.Y - cy) / ry)
    return 1.0 - PT.smoothstep(1.0 - feather * 0.9, 1.0 + feather * 0.1, k_)


def painted_wash(layer: PT.Layer, ctx: PT.Ctx, sd, ramp_cols, *, centre=None, icon_r=60.0, strokes=1.0,
                 vignette=48.0, recess=0.55, hue=0.06, angle=-24.0, spot=0.35, rimwash=0.45, mask=None,
                 texel=3.0):
    """Big-stroke acrylic wash for art windows: the world's dab mosaic mapped onto a 3-colour ramp."""
    d = -sd
    g1 = ctx.grain(texel=texel, angle=8.0, stretch=1.35, base_angle=angle)
    g2 = ctx.grain(texel=texel * 0.55, angle=20.0, stretch=1.2, base_angle=angle + 28.0)
    lf = ctx.noise(cell=150, octaves=2)
    v = 1.0 + strokes * (1.45 * (g1.r - 0.5) + 0.35 * (g2.r - 0.5)) + 0.30 * lf
    v = v + 0.30 * (0.5 - ctx.Y / ctx.h)
    if centre is not None:
        cx, cy = centre
        v = v + rimwash * soft_disc(ctx, cx, cy - icon_r * 0.08, icon_r * 1.5, icon_r * 1.3, feather=1.0)
    col = PT.ramp(np.clip(v, 0.0, 2.0), ramp_cols)
    col = PT.warm_cool(col, hue * (g1.g - 0.5) * 2.0 + 0.04 * lf)
    # painted vignette toward the edges (value and a slight cool shift)
    t = PT.smoothstep(0.0, vignette, d)
    col = col * (0.55 + 0.45 * t)[..., None]
    col = PT.warm_cool(col, -0.06 * (1.0 - t))
    if centre is not None and spot > 0:
        cx, cy = centre
        sp = soft_disc(ctx, cx, cy + icon_r * 0.80, icon_r * 0.95, icon_r * 0.28, feather=1.2)
        col = col * (1.0 - spot * sp)[..., None]
    if recess:
        f = PT.facing_light(sd)
        sh = (1.0 - PT.smoothstep(0.0, 12.0, d)) * np.clip(f * 1.3, 0.0, 1.0) * recess
        col = col * (1.0 - sh)[..., None]
        c = (1.0 - PT.smoothstep(0.0, 2.5, d)) * np.clip(-f, 0.0, 1.0) * 0.30
        col = P.mix(col, ramp_cols[-1], c)
    region = PT.coverage(sd) if mask is None else PT.coverage(sd) * mask
    layer.paint(col, region)
    return region


def cel_edges(layer: PT.Layer, ctx: PT.Ctx, sd, light, shadow, *, width=5.0, alpha=0.8, mask=None):
    """Painted bevel strips just inside a shape: light on edges facing the light, shadow opposite."""
    d = -sd
    f = PT.facing_light(sd)
    band = (1.0 - PT.smoothstep(width * 0.65, width, d)) * PT.coverage(sd)
    if mask is not None:
        band = band * mask
    layer.paint(light, band * PT.smoothstep(0.15, 0.35, f) * alpha)
    layer.paint(shadow, band * PT.smoothstep(0.15, 0.35, -f) * alpha)


def tidy_alpha(layer: PT.Layer, floor=1.5 / 255.0):
    """Zero out imperceptible alpha so transparent corners stay exactly transparent."""
    m = layer.a < floor
    layer.a[m] = 0.0
    layer.c[m] = 0.0


def radial(ctx: PT.Ctx, cx, cy):
    return np.hypot(ctx.X - cx, ctx.Y - cy)


def ring_sd(ctx: PT.Ctx, cx, cy, r_out, r_in):
    d = radial(ctx, cx, cy)
    return np.maximum(d - r_out, r_in - d)


def trigon_ring_pts(cx, cy, r_ring, r_point, width, angle=-90.0, n=96):
    """Circle with three triangular points (the Dungeon Run command emblem outline)."""
    pts = []
    for i in range(n):
        th = math.radians(angle) + i * 2 * math.pi / n
        # distance to the nearest point direction
        rel = ((th - math.radians(angle)) % (2 * math.pi / 3))
        rel = min(rel, 2 * math.pi / 3 - rel)
        tri = max(0.0, 1.0 - rel / width)
        r = r_ring + (r_point - r_ring) * tri
        pts.append((cx + r * math.cos(th), cy + r * math.sin(th)))
    return pts


def edge_shade(layer: PT.Layer, ctx: PT.Ctx, sd, *, width=10.0, amount=0.35, mask=None):
    """Darken inside a shape near its edge (soft painted inner vignette)."""
    t = 1.0 - PT.smoothstep(0.0, width, -sd)
    if mask is not None:
        t = t * mask
    layer.multiply(1.0 - amount * t)


def shift_mask(m, dy, dx=0.0):
    return ndimage.shift(m, (dy, dx), order=1)


def gradient_grain(layer: PT.Layer, ctx: PT.Ctx, sd, *, zones_mask=None, top=0.08, bottom=-0.15,
                   grain_strength=0.06):
    """Painted vertical luminance gradient (top brighter, bottom darker) plus a subtle extra brush
    grain, kept outside text-safe zones. Breaks up the otherwise flat non-card chrome plates
    (Commit, Floor plaque, Terminal banner, draw/discard tokens, the ACTIONS medallion)."""
    region = PT.coverage(sd)
    if zones_mask is not None:
        region = region * (1.0 - zones_mask)
    t = np.clip(ctx.Y / ctx.h, 0.0, 1.0)
    g = top + (bottom - top) * t
    grain = ctx.grain(texel=2.4, angle=12.0)
    gfac = 1.0 + grain_strength * (grain.r - 0.5) * 2.0
    layer.multiply((1.0 + g) * gfac, mask=region)


def worn_dabs_10oclock(layer: PT.Layer, ctx: PT.Ctx, lam, region, sd, *, count=3):
    """2-3 worn bronze highlight dabs biased to the lit (roughly 10-11 o'clock) edge."""
    PT.worn_dabs(layer, ctx, lam, region, sd, count=count, colour=P.hex_rgb("#F0D89A"), alpha=(0.55, 0.65),
                 min_lam=0.5, size=(1.2, 2.2))
