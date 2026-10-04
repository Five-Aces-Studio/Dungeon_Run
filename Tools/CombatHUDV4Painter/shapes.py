"""Signed-distance shape primitives for the V4 painter.

Conventions
-----------
* Pixel-centre grid, x right, y down, units = output pixels (2x reference).
* Signed distance is NEGATIVE inside the shape.
* Polygon builders return lists of (x, y) points; ``sd_polygon`` turns them into
  an exact SDF. Curved outlines (pointed arches, shields) are sampled densely.
* ``perturb`` adds low-frequency noise so no edge is perfectly straight.
"""
from __future__ import annotations

import math

import numpy as np

SQRT3 = math.sqrt(3.0)


# --------------------------------------------------------------------------- grid

def grid(w: int, h: int):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    return xs + 0.5, ys + 0.5


# ---------------------------------------------------------------- analytic SDFs

def sd_circle(X, Y, cx, cy, r):
    return np.hypot(X - cx, Y - cy) - r


def sd_ellipse_approx(X, Y, cx, cy, rx, ry):
    """Cheap ellipse distance (exact on the axes, good enough for soft dabs)."""
    k = np.hypot((X - cx) / rx, (Y - cy) / ry)
    return (k - 1.0) * min(rx, ry)


def sd_box(X, Y, cx, cy, hw, hh, r=0.0):
    """Rounded box centred at (cx, cy) with half extents (hw, hh)."""
    qx = np.abs(X - cx) - (hw - r)
    qy = np.abs(Y - cy) - (hh - r)
    outside = np.hypot(np.maximum(qx, 0.0), np.maximum(qy, 0.0))
    inside = np.minimum(np.maximum(qx, qy), 0.0)
    return outside + inside - r


def sd_rect(X, Y, x0, y0, x1, y1, r=0.0):
    return sd_box(X, Y, (x0 + x1) * 0.5, (y0 + y1) * 0.5, (x1 - x0) * 0.5, (y1 - y0) * 0.5, r)


def sd_segment(X, Y, ax, ay, bx, by):
    """Unsigned distance to a segment."""
    px, py = X - ax, Y - ay
    ex, ey = bx - ax, by - ay
    t = np.clip((px * ex + py * ey) / max(ex * ex + ey * ey, 1e-9), 0.0, 1.0)
    return np.hypot(px - ex * t, py - ey * t)


def sd_capsule(X, Y, ax, ay, bx, by, r):
    return sd_segment(X, Y, ax, ay, bx, by) - r


def sd_tapered_capsule(X, Y, ax, ay, bx, by, ra, rb):
    """Segment whose radius varies linearly from ra (at a) to rb (at b)."""
    px, py = X - ax, Y - ay
    ex, ey = bx - ax, by - ay
    t = np.clip((px * ex + py * ey) / max(ex * ex + ey * ey, 1e-9), 0.0, 1.0)
    return np.hypot(px - ex * t, py - ey * t) - (ra + (rb - ra) * t)


def sd_arc(X, Y, cx, cy, r, a0, a1, thickness):
    """Ring segment between angles a0..a1 (radians, image space: y down)."""
    ang = np.arctan2(Y - cy, X - cx)
    mid = (a0 + a1) * 0.5
    half = (a1 - a0) * 0.5
    d_ang = np.abs(np.angle(np.exp(1j * (ang - mid))))
    ring = np.abs(np.hypot(X - cx, Y - cy) - r) - thickness * 0.5
    # outside the angular range use the distance to the arc end caps
    ex0, ey0 = cx + r * math.cos(a0), cy + r * math.sin(a0)
    ex1, ey1 = cx + r * math.cos(a1), cy + r * math.sin(a1)
    cap = np.minimum(np.hypot(X - ex0, Y - ey0), np.hypot(X - ex1, Y - ey1)) - thickness * 0.5
    return np.where(d_ang <= half, ring, cap)


def sd_polygon(X, Y, pts, pad: float = 48.0):
    """Exact signed distance to a simple polygon (Inigo Quilez' formulation).

    Evaluated inside the polygon bounding box grown by ``pad``; farther pixels
    receive a conservative large positive distance.
    """
    pts = np.asarray(pts, np.float32)
    h, w = X.shape
    x0 = max(int(math.floor(pts[:, 0].min() - pad)), 0)
    x1 = min(int(math.ceil(pts[:, 0].max() + pad)), w)
    y0 = max(int(math.floor(pts[:, 1].min() - pad)), 0)
    y1 = min(int(math.ceil(pts[:, 1].max() + pad)), h)
    out = np.full(X.shape, pad + 1.0, np.float32)
    if x1 <= x0 or y1 <= y0:
        return out
    px = X[y0:y1, x0:x1]
    py = Y[y0:y1, x0:x1]
    d = (px - pts[0, 0]) ** 2 + (py - pts[0, 1]) ** 2
    s = np.ones_like(px)
    n = len(pts)
    for i in range(n):
        j = i - 1
        vix, viy = float(pts[i, 0]), float(pts[i, 1])
        ex, ey = float(pts[j, 0]) - vix, float(pts[j, 1]) - viy
        wx, wy = px - vix, py - viy
        ee = ex * ex + ey * ey
        if ee < 1e-12:
            continue
        t = np.clip((wx * ex + wy * ey) / ee, 0.0, 1.0)
        bx, by = wx - ex * t, wy - ey * t
        d = np.minimum(d, bx * bx + by * by)
        c1 = py >= viy
        c2 = py < float(pts[j, 1])
        c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        s = np.where(flip, -s, s)
    out[y0:y1, x0:x1] = s * np.sqrt(d)
    return out


# ----------------------------------------------------------------- boolean ops

def union(*sds):
    out = sds[0]
    for s in sds[1:]:
        out = np.minimum(out, s)
    return out


def intersect(*sds):
    out = sds[0]
    for s in sds[1:]:
        out = np.maximum(out, s)
    return out


def subtract(a, b):
    return np.maximum(a, -b)


def smooth_union(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)


def offset(sd, r):
    """Grow (r > 0) or shrink (r < 0) a shape."""
    return sd - r


def perturb(sd, noise, amp):
    """Irregular silhouette: add low-frequency noise (in -1..1) scaled to ``amp`` px."""
    return sd + noise * amp


# ------------------------------------------------------------ polygon builders

def chamfer_rect(x0, y0, x1, y1, c):
    """Rectangle with 45-degree chamfered corners; c may be a scalar or 4-tuple (tl, tr, br, bl)."""
    if np.isscalar(c):
        c = (c, c, c, c)
    tl, tr, br, bl = c
    return [(x0 + tl, y0), (x1 - tr, y0), (x1, y0 + tr), (x1, y1 - br),
            (x1 - br, y1), (x0 + bl, y1), (x0, y1 - bl), (x0, y0 + tl)]


def _pointed_curve(x0, x1, y_spring, depth, n, downward=False):
    """Squashed equilateral pointed arc from the right springer to the apex and back.

    Returns points from (x1, y_spring) over the apex to (x0, y_spring). ``depth`` is
    the apex offset from the springline (positive = up for arches, down for shields).
    """
    a = (x1 - x0) * 0.5
    k = depth / (a * SQRT3)
    sign = 1.0 if downward else -1.0
    right = []
    for th in np.linspace(0.0, math.pi / 3.0, n):
        x = x0 + 2 * a * math.cos(th)
        y = y_spring + sign * k * 2 * a * math.sin(th)
        right.append((x, y))
    mid = (x0 + x1) * 0.5
    left = [(2 * mid - x, y) for (x, y) in reversed(right[:-1])]
    return right + left


def pointed_arch(x0, x1, y_bottom, y_spring, y_apex, n=36):
    """Gothic window: vertical sides up to the springline, pointed top at y_apex."""
    pts = [(x0, y_bottom), (x1, y_bottom)]
    pts += _pointed_curve(x0, x1, y_spring, y_spring - y_apex, n)
    return pts


def heater_shield(x0, y0, x1, y1, y_spring, chamfer=0.0, n=36):
    """Flat top (optionally chamfered), straight sides, pointed-arch bottom ending at y1."""
    pts = []
    if chamfer > 0:
        pts += [(x0 + chamfer, y0), (x1 - chamfer, y0), (x1, y0 + chamfer)]
    else:
        pts += [(x0, y0), (x1, y0)]
    curve = _pointed_curve(x0, x1, y_spring, y1 - y_spring, n, downward=True)
    pts += curve  # right springer -> bottom point -> left springer
    if chamfer > 0:
        pts.append((x0, y0 + chamfer))
    return pts


def inverted_shield(x0, y0, x1, y1, y_spring, corner_r, n=36):
    """Pointed top (apex at y0), straight sides, rounded bottom corners."""
    pts = []
    cy = y1 - corner_r
    # bottom-right corner arc (0 -> 90 deg), bottom-left (90 -> 180)
    for th in np.linspace(0.0, math.pi / 2, n // 2):
        pts.append((x1 - corner_r + corner_r * math.cos(th), cy + corner_r * math.sin(th)))
    for th in np.linspace(math.pi / 2, math.pi, n // 2):
        pts.append((x0 + corner_r + corner_r * math.cos(th), cy + corner_r * math.sin(th)))
    # left side up to the springline, then the pointed top back to the right side
    top = _pointed_curve(x0, x1, y_spring, y_spring - y0, n)
    pts += list(reversed(top))
    return pts


def hex_plate(x0, y0, x1, y1, point):
    """Plate with pointed left/right ends (a stretched hexagon)."""
    cy = (y0 + y1) * 0.5
    return [(x0, cy), (x0 + point, y0), (x1 - point, y0), (x1, cy), (x1 - point, y1), (x0 + point, y1)]


def notched_hex_plate(x0, y0, x1, y1, point, tip_half):
    """Hex plate whose tips are cut flat (flat tip of height 2 * tip_half): a chunkier tab."""
    cy = (y0 + y1) * 0.5
    return [(x0, cy - tip_half), (x0 + point, y0), (x1 - point, y0), (x1, cy - tip_half),
            (x1, cy + tip_half), (x1 - point, y1), (x0 + point, y1), (x0, cy + tip_half)]


def diamond(cx, cy, rx, ry):
    return [(cx, cy - ry), (cx + rx, cy), (cx, cy + ry), (cx - rx, cy)]


def triangle(cx, cy, r, angle_deg=-90.0):
    """Equilateral triangle with circumradius r; angle_deg points the first vertex (-90 = up)."""
    a = math.radians(angle_deg)
    return [(cx + r * math.cos(a + k * 2 * math.pi / 3), cy + r * math.sin(a + k * 2 * math.pi / 3))
            for k in range(3)]


def iso_triangle(apex, base_a, base_b):
    return [apex, base_a, base_b]


def regular_polygon(cx, cy, r, n, angle_deg=-90.0):
    a = math.radians(angle_deg)
    return [(cx + r * math.cos(a + k * 2 * math.pi / n), cy + r * math.sin(a + k * 2 * math.pi / n))
            for k in range(n)]


def star(cx, cy, r_out, r_in, points, angle_deg=-90.0):
    pts = []
    a = math.radians(angle_deg)
    for k in range(points * 2):
        r = r_out if k % 2 == 0 else r_in
        th = a + k * math.pi / points
        pts.append((cx + r * math.cos(th), cy + r * math.sin(th)))
    return pts


def ribbon_band(x0, y0, x1, y1, bow=0.0, n=24):
    """Ribbon body with a gentle bow (positive bow lifts the middle)."""
    top = [(x, y0 - bow * math.sin(math.pi * (x - x0) / (x1 - x0))) for x in np.linspace(x0, x1, n)]
    bot = [(x, y1 - bow * math.sin(math.pi * (x - x0) / (x1 - x0))) for x in np.linspace(x1, x0, n)]
    return top + bot


def ribbon_tail(x_inner, x_outer, y0, y1, notch):
    """Ribbon end with a V (swallowtail) notch on the outer side."""
    cy = (y0 + y1) * 0.5
    if x_outer < x_inner:  # left tail
        return [(x_inner, y0), (x_outer, y0), (x_outer + notch, cy), (x_outer, y1), (x_inner, y1)]
    return [(x_inner, y0), (x_outer, y0), (x_outer - notch, cy), (x_outer, y1), (x_inner, y1)]


def rotate_points(pts, cx, cy, angle_deg):
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * ca - (y - cy) * sa, cy + (x - cx) * sa + (y - cy) * ca) for x, y in pts]


def translate_points(pts, dx, dy):
    return [(x + dx, y + dy) for x, y in pts]


def scale_points(pts, cx, cy, s):
    return [(cx + (x - cx) * s, cy + (y - cy) * s) for x, y in pts]


def boot_outline(cx, cy, s):
    """Stylised boot (toe to the right) for glyphs/chips; s = overall height.

    Round 3: the heel now steps OUT past the back of the leg (a clear protruding block,
    not a shallow 0.04-unit notch), so the silhouette reads as a boot rather than the
    letter 'L' at small (18-20 px) chip sizes.
    """
    raw = [(-0.26, -0.50), (0.14, -0.50), (0.16, 0.06), (0.40, 0.12), (0.52, 0.28),
           (0.46, 0.46), (0.10, 0.50), (-0.22, 0.50), (-0.38, 0.50), (-0.38, 0.30),
           (-0.22, 0.26), (-0.24, -0.02)]
    return [(cx + x * s, cy + y * s) for x, y in raw]


def heart_sd(X, Y, cx, cy, s):
    """Heart shape SDF built from two circles and a rotated square (s = width)."""
    r = s * 0.27
    c1 = sd_circle(X, Y, cx - s * 0.22, cy - s * 0.12, r)
    c2 = sd_circle(X, Y, cx + s * 0.22, cy - s * 0.12, r)
    tri = sd_polygon(X, Y, [(cx - s * 0.47, cy - s * 0.05), (cx + s * 0.47, cy - s * 0.05), (cx, cy + s * 0.48)])
    return smooth_union(union(c1, c2), tri, s * 0.05)
