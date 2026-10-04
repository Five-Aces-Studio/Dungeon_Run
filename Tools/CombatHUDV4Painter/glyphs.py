"""Small category/status glyphs (24x24 reference -> 48x48 PNG), painted supersampled.

Each glyph is drawn in unit coordinates on a supersampled canvas and box-filtered
down, so the living-width ink contour stays clean at 48 px. ``glyph_layer`` is also
used by other sprites (chips) to embed a glyph at any size.
"""
from __future__ import annotations

import math

import numpy as np

import kit
import paint as PT
import palette as P
import shapes as S

NAMES = ("Sword", "Shield", "Boot", "Heart", "Star", "Pierce", "Clock", "Burst", "Eye", "Question")

LEATHER = (P.hex_rgb("#4E2214"), P.hex_rgb("#7A3A22"), P.hex_rgb("#A0583A"))
GRIP = P.hex_rgb("#5A2A1A")


class _G:
    """Unit-space helper around a supersampled ctx."""

    def __init__(self, L: PT.Layer, ctx: PT.Ctx, size_px: int, ss: int):
        self.L, self.ctx, self.n = L, ctx, ctx.w
        self.ink_w = max(1.5, size_px * 0.046) * ss

    def u(self, v):
        return v * self.n

    def pts(self, pts):
        return [(x * self.n, y * self.n) for x, y in pts]

    def poly(self, pts):
        return S.sd_polygon(self.ctx.X, self.ctx.Y, self.pts(pts), pad=self.n)

    def circle(self, cx, cy, r):
        return S.sd_circle(self.ctx.X, self.ctx.Y, self.u(cx), self.u(cy), self.u(r))

    def capsule(self, ax, ay, bx, by, r):
        return S.sd_capsule(self.ctx.X, self.ctx.Y, self.u(ax), self.u(ay), self.u(bx), self.u(by), self.u(r))

    def wobble(self, sd, amp=0.6):
        return S.perturb(sd, self.ctx.noise(cell=self.n * 0.18, octaves=2), amp * self.n / 96.0)

    def fill_split(self, sd, light, dark, *, axis=(0.62, 0.78), origin=(0.5, 0.5), soft=0.004):
        """Cel two-tone fill: lit side toward the light, split by a line through ``origin``."""
        side = (self.ctx.X - self.u(origin[0])) * axis[0] + (self.ctx.Y - self.u(origin[1])) * axis[1]
        t = PT.smoothstep(-soft * self.n, soft * self.n, side)
        self.L.paint(P.mix(light, dark, t), PT.coverage(sd))

    def fill(self, sd, colour, alpha=1.0):
        self.L.paint(colour, PT.coverage(sd) * alpha)

    def ink(self, sd, scale=1.0):
        w = self.ink_w * scale
        PT.ink_contour(self.L, self.ctx, S.offset(sd, w * 0.55), w_min=w * 0.8, w_max=w * 1.15)

    def grain(self, sd, strength=0.07):
        g = self.ctx.grain(texel=self.n / 60.0, angle=20.0)
        self.L.multiply(1.0 + strength * (g.r - 0.5) * 2.0, mask=PT.coverage(sd))


# --------------------------------------------------------------- glyph shapes

def _sword(g: _G, *, tip=(0.84, 0.16), base=(0.34, 0.66), hw=0.068, guard=0.19, dim=1.0):
    dx, dy = tip[0] - base[0], tip[1] - base[1]
    ln = math.hypot(dx, dy)
    d = (dx / ln, dy / ln)
    p = (-d[1], d[0])  # perpendicular; for an up-right blade it points down-right (shadow side)
    sh = (base[0] + d[0] * ln * 0.80, base[1] + d[1] * ln * 0.80)
    blade = [(base[0] + p[0] * hw, base[1] + p[1] * hw), (sh[0] + p[0] * hw * 0.92, sh[1] + p[1] * hw * 0.92),
             tip, (sh[0] - p[0] * hw * 0.92, sh[1] - p[1] * hw * 0.92), (base[0] - p[0] * hw, base[1] - p[1] * hw)]
    blade_sd = g.poly(blade)
    gx0, gy0 = base[0] + p[0] * guard, base[1] + p[1] * guard
    gx1, gy1 = base[0] - p[0] * guard, base[1] - p[1] * guard
    guard_sd = g.capsule(gx0, gy0, gx1, gy1, 0.038)
    grip_end = (base[0] - d[0] * 0.17, base[1] - d[1] * 0.17)
    grip_sd = g.capsule(base[0], base[1], grip_end[0], grip_end[1], 0.036)
    pom = (base[0] - d[0] * 0.225, base[1] - d[1] * 0.225)
    pom_sd = g.circle(pom[0], pom[1], 0.052)
    whole = S.union(blade_sd, guard_sd, grip_sd, pom_sd)
    # shadow side of the blade = +p side (lower right for an up-right blade)
    g.fill(grip_sd, P.scale(GRIP, dim))
    g.fill_split(blade_sd, P.scale(P.mix(P.STEEL, P.IVORY_TEXT, 0.25), dim), P.scale(P.STEEL_DARK, dim),
                 axis=(p[0], p[1]), origin=base)
    fuller = S.sd_segment(g.ctx.X, g.ctx.Y, g.u(base[0] + d[0] * 0.04), g.u(base[1] + d[1] * 0.04),
                          g.u(sh[0] - d[0] * 0.05), g.u(sh[1] - d[1] * 0.05))
    g.L.paint(P.INK, np.clip(0.5 - (fuller - g.u(0.006)), 0, 1) * 0.35 * PT.coverage(blade_sd))
    g.fill_split(S.union(guard_sd, pom_sd), P.scale(P.BRONZE_LIGHT, dim), P.scale(P.BRONZE_DARK, dim),
                 axis=(p[0], p[1]), origin=base)
    return whole


def g_sword(g: _G):
    sd = _sword(g)
    g.ink(sd)


def _shield_sd(g: _G, x0, y0, x1, y1, spring):
    return g.poly(S.heater_shield(x0, y0, x1, y1, spring, chamfer=0.0, n=28))


def g_shield(g: _G):
    sd = g.wobble(_shield_sd(g, 0.2, 0.12, 0.8, 0.9, 0.46))
    g.fill_split(sd, P.mix(P.STEEL, P.IVORY_TEXT, 0.2), P.STEEL_DARK, axis=(1.0, 0.0), origin=(0.5, 0.5))
    inner = S.offset(sd, -g.u(0.07))
    PT.ink_line(g.L, g.ctx, inner, width=g.ink_w * 0.35, alpha=0.5)
    boss = g.circle(0.5, 0.42, 0.075)
    g.fill_split(boss, P.BRONZE_LIGHT, P.BRONZE_DARK, origin=(0.5, 0.42))
    g.ink(boss, 0.6)
    g.grain(sd)
    g.ink(sd)


def g_boot(g: _G):
    pts = S.boot_outline(0.47, 0.5, 0.74)
    sd = g.wobble(g.poly(pts), 0.5)
    t = (g.ctx.Y / g.n - 0.13)
    lvl = np.where(t < 0.2, 2.0, 1.0) - PT.smoothstep(0.52, 0.56, g.ctx.X / g.n + 0.25 * t) * 0.8
    col = PT.ramp(np.clip(lvl, 0, 2), LEATHER)
    g.L.paint(col, PT.coverage(sd))
    cuff = S.sd_segment(g.ctx.X, g.ctx.Y, g.u(0.26), g.u(0.30), g.u(0.55), g.u(0.30))
    g.L.paint(P.INK, np.clip(0.5 - (cuff - g.u(0.012)), 0, 1) * PT.coverage(sd) * 0.8)
    sole = S.intersect(sd, -(g.ctx.Y - g.u(0.80)))
    g.fill(sole, P.scale(LEATHER[0], 0.8))
    g.grain(sd)
    g.ink(sd)


def g_heart(g: _G):
    sd = g.wobble(S.heart_sd(g.ctx.X, g.ctx.Y, g.u(0.5), g.u(0.52), g.u(0.82)), 0.4)
    g.fill_split(sd, P.RED_LIGHT, P.RED_DARK, axis=(0.7, 0.72), origin=(0.56, 0.58))
    g.L.paint(P.RED, PT.coverage(sd) * 0.35)
    PT.stamp_ellipse(g.L, g.u(0.33), g.u(0.36), g.u(0.075), g.u(0.04), -0.8, P.mix(P.IVORY_TEXT, P.RED_LIGHT, 0.3),
                     0.85, clip=PT.coverage(sd))
    g.ink(sd)


def g_star(g: _G):
    sd = g.wobble(g.poly(S.star(0.5, 0.56, 0.44, 0.15, 3, -90.0)), 0.4)
    g.fill_split(sd, P.mix(P.GOLD, P.PARCH_LIGHT, 0.35), P.BRONZE, origin=(0.5, 0.56))
    core = g.poly(S.triangle(0.5, 0.56, 0.09, 90.0))
    g.fill(core, P.mix(P.CATEGORY["Special"], P.PARCH_LIGHT, 0.25))
    g.ink(core, 0.5)
    g.ink(sd)


def g_pierce(g: _G):
    sh = g.wobble(_shield_sd(g, 0.38, 0.16, 0.9, 0.86, 0.46), 0.4)
    g.fill_split(sh, P.mix(P.STEEL, P.IVORY_TEXT, 0.1), P.STEEL_DARK, axis=(1.0, 0.0), origin=(0.64, 0.5))
    crack = [(0.66, 0.2), (0.6, 0.38), (0.7, 0.5), (0.62, 0.68)]
    for (ax, ay), (bx, by) in zip(crack[:-1], crack[1:]):
        c = S.sd_segment(g.ctx.X, g.ctx.Y, g.u(ax), g.u(ay), g.u(bx), g.u(by))
        g.L.paint(P.INK, np.clip(0.5 - (c - g.u(0.012)), 0, 1) * PT.coverage(sh))
    g.ink(sh)
    sw = _sword(g, tip=(0.86, 0.14), base=(0.3, 0.7), hw=0.06, guard=0.17)
    g.ink(sw)


def g_clock(g: _G):
    cx, cy, R = 0.5, 0.535, 0.365
    ring = g.wobble(g.circle(cx, cy, R), 0.3)
    face = g.circle(cx, cy, R * 0.78)
    g.fill_split(ring, P.BRONZE_LIGHT, P.BRONZE_DARK, origin=(cx, cy))
    g.fill_split(face, P.IVORY_TEXT, P.PARCH_SHADOW, axis=(0.62, 0.78), origin=(cx + 0.12, cy + 0.14))
    PT.ink_line(g.L, g.ctx, face, width=g.ink_w * 0.45, alpha=0.8)
    for a in range(4):
        th = a * math.pi / 2
        t = g.capsule(cx + R * 0.58 * math.cos(th), cy + R * 0.58 * math.sin(th), cx + R * 0.70 * math.cos(th),
                      cy + R * 0.70 * math.sin(th), 0.018)
        g.fill(t, P.INK)
    g.fill(g.capsule(cx, cy, cx, cy - R * 0.52, 0.03), P.INK)
    g.fill(g.capsule(cx, cy, cx + R * 0.42, cy, 0.03), P.INK)
    g.fill(g.circle(cx, cy, 0.045), P.BRONZE_DARK)
    crown = g.capsule(cx, cy - R - 0.05, cx, cy - R + 0.01, 0.04)
    g.fill_split(crown, P.BRONZE_LIGHT, P.BRONZE_DARK, origin=(cx, cy - R))
    g.ink(S.union(ring, crown))


def g_burst(g: _G):
    """Round 3: the damage-feedback glyph repainted as a painted slash-spark (ivory core, warm
    red edge, ink contour) that reads at 18 px, instead of a red/amber starburst."""
    rng = g.ctx.rng
    a0 = (0.28, 0.72)
    a1 = (0.74, 0.26)
    slash = g.capsule(a0[0], a0[1], a1[0], a1[1], 0.10)
    tip = g.circle(a0[0], a0[1], 0.07)
    nick = g.circle(a1[0], a1[1], 0.04)
    sd = g.wobble(S.union(slash, tip, nick), 0.25)
    d = -sd / g.n
    g.L.paint(P.mix(P.RED, P.RED_LIGHT, 0.4), PT.coverage(sd))
    core = S.offset(sd, g.u(0.04))
    g.L.paint(P.mix(P.IVORY_TEXT, P.EMBER, 0.15), PT.coverage(core))
    g.L.paint(P.EMBER, PT.coverage(sd) * np.clip(d * 5.0, 0, 0.4))
    # small spark flecks off the leading tip
    for (px, py, pr) in ((0.76, 0.20, 0.036), (0.82, 0.30, 0.024), (0.24, 0.78, 0.028)):
        pr *= rng.uniform(0.9, 1.1)
        pc = g.circle(px, py, pr)
        g.fill(pc, P.mix(P.EMBER, P.IVORY_TEXT, 0.4))
        g.ink(pc, 0.5)
    g.ink(sd)


def g_eye(g: _G):
    R, c = 0.4985, 0.2685
    lens = S.intersect(g.circle(0.5, 0.5 + c, R), g.circle(0.5, 0.5 - c, R))
    lens = g.wobble(lens, 0.3)
    g.fill_split(lens, P.IVORY_TEXT, P.PARCH_SHADOW, axis=(0.0, 1.0), origin=(0.5, 0.56))
    iris = S.intersect(g.circle(0.5, 0.5, 0.16), lens)
    g.fill_split(iris, P.TEAL_LIGHT, P.TEAL, origin=(0.5, 0.5))
    g.fill(g.circle(0.5, 0.5, 0.072), P.INK)
    PT.stamp_ellipse(g.L, g.u(0.45), g.u(0.45), g.u(0.035), g.u(0.028), 0.0, P.IVORY_TEXT, 0.9)
    g.ink(lens)


def g_question(g: _G):
    q = PT.text_sd(g.ctx, "?", "display", "ExtraBold", g.n * 1.22, g.n * 0.5, g.n * 0.41, ss=1)
    q = g.wobble(q, 0.3)
    g.fill_split(q, P.mix(P.GOLD, P.PARCH_LIGHT, 0.4), P.BRONZE, axis=(0.0, 1.0), origin=(0.5, 0.52))
    g.ink(q)


PAINTERS = {"Sword": g_sword, "Shield": g_shield, "Boot": g_boot, "Heart": g_heart, "Star": g_star,
            "Pierce": g_pierce, "Clock": g_clock, "Burst": g_burst, "Eye": g_eye, "Question": g_question}

CATEGORY_GLYPH = {"Attack": "Sword", "Defence": "Shield", "Mobility": "Boot", "Support": "Heart", "Special": "Star"}


def glyph_layer(name: str, size_px: int, repo_root, ss: int = 4) -> PT.Layer:
    n = int(size_px) * ss
    ctx = PT.Ctx(f"Glyphs/{name}@{int(size_px)}", n, n, repo_root)
    L = PT.Layer(n, n)
    PAINTERS[name](_G(L, ctx, int(size_px), ss))
    out = L.downsample(ss)
    kit.tidy_alpha(out)
    return out


def paint_glyph(ctx: PT.Ctx, name: str) -> PT.Layer:
    return glyph_layer(name, ctx.w, ctx.repo_root)
