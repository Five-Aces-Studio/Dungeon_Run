"""HUD sprites: action sockets, Commit plates, player crest, enemy HUD and static plaques/tokens.

Coordinates in the helper calls are 1080-reference units converted with ``k``/``kpts``
(output = 2x). Text-safe zones are declared here and reused by the registry/tests.
"""
from __future__ import annotations

import math

import numpy as np

import cards
import glyphs
import kit
import paint as PT
import palette as P
import shapes as S
from kit import k, kpts

# ------------------------------------------------------------------ text zones
ZONES = {
    "Controls/Socket.png": [(40, 134, 78, 150)],
    "Controls/Commit": [(48, 29, 211, 68)],
    "Controls/SmallButton.png": [(10, 6, 100, 24)],
    "Crest/Chip": [(31, 8, 51, 20)],
    "Static/FloorPlaque.png": [(30, 8, 200, 40), (40, 40, 190, 54)],
    "Static/ActionMedallion.png": [(30, 34, 102, 80)],
    "Static/Token": [(22, 60, 74, 88)],
    "Static/Banner.png": [(50, 10, 390, 36)],
    "Static/TerminalBanner.png": [(52, 35, 508, 96), (70, 103, 490, 149)],
}


def zpx(key):
    return [PT.zone_px(z) for z in ZONES[key]]


STONE = (P.CHAR_DEEP, P.CHAR, P.CHAR_LIGHT)
STONE_DIM = (P.scale(P.CHAR_DEEP, 0.85), P.scale(P.CHAR, 0.82), P.scale(P.CHAR_LIGHT, 0.8))
# Round 3: the empty HP track interior reads as a dark oxblood recess, not a near-black slot.
TROUGH = (P.scale(P.OXBLOOD, 0.72), P.OXBLOOD, P.mix(P.OXBLOOD, P.RED_DARK, 0.35))
SOCKET_FACE = P.hex_rgb("#1E2A2E")


def trigon_gem(layer, ctx, cx, cy, r, colour, *, lit=0.0, angle=90.0, setting=2.4):
    """Trigonal gem (triangle, default pointing down) in a bronze setting."""
    return kit.gem(layer, ctx, S.triangle(cx, cy, r, angle), colour, lit=lit, setting=setting)


def amber_glow(layer, ctx, cx, cy, r, alpha):
    m = kit.soft_disc(ctx, cx, cy, r, r, feather=1.0)
    layer.paint(P.mix(P.AMBER, P.EMBER, 0.5), m * alpha)


# ================================================================ Controls

def socket_geometry(ctx):
    sd = kit.silhouette(ctx, kpts(S.chamfer_rect(1.3, 1.3, 116.7, 156.7, 9.0)), amp=0.9)
    face = kit.inset(ctx, sd, k(6.2))
    return sd, face


def paint_socket(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    sd, face = socket_geometry(ctx)
    info = kit.plate(L, ctx, sd, rim=k(6.2), face_sd=face, style=PT.BRONZE_SOCKET, face="stone",
                     face_kw=dict(ramp_cols=(P.scale(SOCKET_FACE, 0.72), SOCKET_FACE, P.scale(SOCKET_FACE, 1.35)),
                                  vignette=34.0, grain_strength=0.22, recess=0.6, catch=0.3, texel=2.6,
                                  mottle=0.4),
                     dabs=0, chip_count=5, halo=None)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    # 10-unit inner recess shadow, darker toward the top and left (the rim overhang's cast shadow)
    fx, fy = PT.normals(face)
    d_in = np.maximum(face, 0.0)
    edge_band = 1.0 - PT.smoothstep(0.0, k(10), d_in)
    dir_w = np.clip(np.clip(-fy, 0.0, 1.0) + np.clip(-fx, 0.0, 1.0), 0.0, 1.0)
    L.multiply(1.0 - edge_band * dir_w * 0.45, mask=PT.coverage(face))
    # ghost glyph (arch + triangle): a painted ink stroke, not an engraving, so it reads as a hint
    arch = S.sd_polygon(ctx.X, ctx.Y, kpts(S.pointed_arch(22, 96, 96, 50, 26, n=32)))
    tri = S.sd_polygon(ctx.X, ctx.Y, kpts(S.triangle(59, 66, 11, -90)))
    ghost_tint = P.hex_rgb("#C9B893")
    for shape in (arch, tri):
        PT.ink_contour(L, ctx, shape, w_min=k(2.5) * 0.8, w_max=k(2.5) * 1.2, colour=ghost_tint, alpha=0.30,
                       mask=PT.coverage(face))
    # numeral tab: parchment fill with an ink contour, so the runtime can draw an ink numeral on it
    rp = kit.silhouette(ctx, kpts(S.notched_hex_plate(30, 129, 88, 155, 7, 3)), amp=0.5, cell=30)
    cards.drop_shadow(L, rp, dy=3.0, blur=2.0, alpha=0.5)
    kit.plate(L, ctx, rp, rim=k(2.2), face="parch",
              face_kw=dict(edge=k(3.0), grain_strength=0.05, burn=0.32),
              zones=zpx("Controls/Socket.png"), zone_feather=2.0, dabs=2, chip_count=0, ink=(1.6, 2.3),
              inner_ink=1.1, halo=None)
    kit.tidy_alpha(L)
    return L


def paint_socket_rim(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    sd, face = socket_geometry(ctx)
    band = PT.coverage(sd) * PT.coverage(-face)
    f = np.clip(PT.facing_light(sd), -1.0, 1.0)
    g = ctx.grain(texel=1.8)
    # Round 3: brightened ~1.35x with a clearer cel bevel: inner top-left ~+20%, bottom-right ~-30%.
    base = 0.70 * 1.35
    v = np.clip(base + 0.20 * base * np.clip(f, 0.0, 1.0) - 0.30 * base * np.clip(-f, 0.0, 1.0)
               + 0.05 * (g.r - 0.5), 0.05, 0.93)
    col = np.stack([v, v * 0.97, v * 0.90], -1)
    inner = (1.0 - PT.smoothstep(0.0, 12.0, -face)) * PT.coverage(face) * 0.32
    L.paint(col, np.clip(band * 0.95, 0, 1))
    L.paint(np.array([0.92, 0.89, 0.83], np.float32), inner)
    kit.tidy_alpha(L)
    return L


def paint_socket_glow(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    _, face = socket_geometry(ctx)
    d = -face
    edge = 1.0 - PT.smoothstep(0.0, 34.0, d)
    g = ctx.grain(texel=3.0, stretch=1.4)
    a = PT.coverage(face) * (0.18 + 0.27 * edge) * (0.9 + 0.2 * (g.r - 0.5))
    L.paint(np.array([0.97, 0.95, 0.9], np.float32), np.clip(a, 0.0, 0.45))
    kit.tidy_alpha(L)
    return L


COMMIT_STATES = ("Disabled", "Ready", "Hover", "Pressed", "Locked", "Resolving")


def cap_gems(L, ctx, pts_angles, r, colour, lit=0.0):
    for (x, y, ang) in pts_angles:
        trigon_gem(L, ctx, k(x), k(y), k(r), colour, lit=lit, angle=ang, setting=2.2)


def paint_commit(ctx: PT.Ctx, state: str) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    style = {"Disabled": PT.BRONZE_DESAT, "Ready": PT.BRONZE, "Hover": PT.BRONZE_BRIGHT, "Pressed": PT.BRONZE,
             "Locked": PT.BRONZE_DIM, "Resolving": PT.BRONZE_DIM}[state]
    face_cols = {
        "Disabled": tuple(P.desaturate(P.scale(c, 0.85), 0.6) for c in STONE),
        "Ready": STONE,
        "Hover": tuple(P.scale(c, 1.1) for c in STONE),
        "Pressed": tuple(P.scale(c, 0.72) for c in STONE),
        "Locked": tuple(P.mix(P.scale(c, 0.8), P.TEAL, 0.08) for c in STONE),
        "Resolving": STONE_DIM,
    }[state]
    # Round 3: a heavier command plate, 10% larger (236x76 -> 260x84), long pointed bronze ends
    # (non-directional rivets, not left/right arrows) and a small pointed-arch crest on top that
    # carries the ONLY remaining directional cue: the flow-pointer gem.
    sd = kit.silhouette(ctx, kpts(S.hex_plate(1.5, 3.3, 258.1, 80.3, 26.4)), amp=0.9)
    face = kit.silhouette(ctx, kpts(S.chamfer_rect(38.5, 11.2, 221.1, 72.4, 5.5)), amp=0.4, cell=40)
    warm = {"Ready": 0.08, "Hover": 0.12}.get(state, 0.0)
    info = kit.plate(L, ctx, sd, rim=k(7.9), face_sd=face, style=style, face="stone",
                     face_kw=dict(ramp_cols=face_cols, vignette=26.0, grain_strength=0.28, recess=0.7, texel=2.6,
                                  warm=0.06 + warm, mottle=0.55),
                     zones=None, dabs=0, chip_count=6, halo=None)
    zmask = PT.zone_mask(ctx, zpx("Controls/Commit"), feather=3.0)
    kit.gradient_grain(L, ctx, face, zones_mask=zmask)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    d = -face
    if state in ("Ready", "Hover"):
        # restrained warm amber inner edge
        w = 9.0 if state == "Ready" else 12.0
        e = (1.0 - PT.smoothstep(0.0, w, d)) * PT.coverage(face)
        L.paint(P.mix(P.AMBER, P.EMBER, 0.3), e * (0.42 if state == "Ready" else 0.58))
    if state == "Pressed":
        top = (1.0 - PT.smoothstep(0.0, 26.0, ctx.Y - k(10.0))) * PT.coverage(face)
        L.multiply(1.0 - 0.45 * top)
    if state == "Locked":
        L.paint(P.TEAL, PT.coverage(face) * 0.06)
    gem_col = {"Disabled": P.scale(P.CHAR_LIGHT, 0.9), "Locked": P.scale(P.CHAR_LIGHT, 0.95),
               "Resolving": P.EMBER, "Ready": P.AMBER, "Hover": P.EMBER, "Pressed": P.AMBER}[state]
    lit = {"Ready": 0.8, "Hover": 1.0, "Pressed": 0.6, "Resolving": 0.7}.get(state, 0.0)
    side_col = P.mix(P.TEAL, P.TEAL_LIGHT, 0.35) if state not in ("Disabled", "Locked") else P.CHAR_LIGHT
    # non-directional upward trigon rivets at both caps (never left/right arrows)
    cap_gems(L, ctx, [(20.4, 41.8, -90.0), (239.3, 41.8, -90.0)], 6.8, side_col)
    # pointed-arch crest near the top edge (the fixed canvas leaves no room above y=0); carries
    # the flow-pointer gem, the ONLY directional cue left on the plate
    crest = kit.silhouette(ctx, kpts(S.pointed_arch(100.0, 160.0, 30.0, 14.0, 3.0, n=24)), amp=0.5, cell=20)
    PT.metal(L, ctx, crest, rim_out=k(4.5), style=style)
    PT.ink_contour(L, ctx, crest, w_min=1.8, w_max=2.6)
    if lit:
        amber_glow(L, ctx, k(130.0), k(15.0), k(15), 0.28 * lit)
    trigon_gem(L, ctx, k(130.0), k(15.0), k(8.6), gem_col, lit=lit, setting=3.0)
    PT.flatten_zones(L, ctx, zpx("Controls/Commit"), feather=3.0)
    kit.tidy_alpha(L)
    return L


def paint_small_button(ctx: PT.Ctx) -> PT.Layer:
    """Round 3: a small bronze-rimmed parchment chip (Cancel / Split hits), about 110x30, that
    sits on the targeting ribbon's row; ink text is drawn on it at runtime."""
    L = PT.Layer(ctx.w, ctx.h)
    sd = kit.silhouette(ctx, kpts(S.chamfer_rect(1.3, 1.3, 108.7, 28.7, 6.0)), amp=0.5, cell=22)
    face = kit.silhouette(ctx, kpts(S.chamfer_rect(6.5, 4.0, 103.5, 26.0, 3.0)), amp=0.25, cell=20)
    kit.plate(L, ctx, sd, rim=k(2.6), face_sd=face, face="parch", zone_feather=2.0,
              face_kw=dict(edge=k(2.6), grain_strength=0.05, burn=0.3),
              zones=zpx("Controls/SmallButton.png"), dabs=2, chip_count=2, ink=(1.5, 2.1), halo=None)
    for x, ang in ((5.5, -90.0), (104.5, -90.0)):
        kit.stud_triangle(L, ctx, k(x), k(15.0), k(1.9), angle=ang)
    kit.tidy_alpha(L)
    return L


def paint_close_button(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    c = k(16)
    sd = S.perturb(S.sd_circle(ctx.X, ctx.Y, c, c, k(14.6)), ctx.noise(cell=20), 0.5)
    face = S.sd_circle(ctx.X, ctx.Y, c, c, k(10.2))
    kit.plate(L, ctx, sd, rim=k(4.4), face_sd=face, face="stone",
              face_kw=dict(ramp_cols=(P.BRONZE_DARK, P.scale(P.BRONZE, 0.85), P.BRONZE), vignette=8.0,
                           grain_strength=0.12, recess=0.5),
              dabs=3, chip_count=2, ink=(1.8, 2.4), halo=None)
    r = k(5.6)
    x_sd = np.minimum(S.sd_capsule(ctx.X, ctx.Y, c - r, c - r, c + r, c + r, 3.1),
                      S.sd_capsule(ctx.X, ctx.Y, c - r, c + r, c + r, c - r, 3.1))
    L.paint(P.mix(P.PARCH_LIGHT, P.GOLD, 0.2), PT.coverage(S.offset(x_sd, 0.0)))
    PT.ink_contour(L, ctx, S.offset(x_sd, 1.8), w_min=1.7, w_max=2.3)
    kit.tidy_alpha(L)
    return L


# ================================================================== Crest

def paint_player_hp_track(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    body = S.sd_rect(ctx.X, ctx.Y, k(1.2), k(2.0), k(246.0), k(28.0), r=k(3.0))
    cap = S.sd_polygon(ctx.X, ctx.Y, kpts([(242.0, 0.8), (249.4, 15.0), (242.0, 29.2)]))
    sd = S.perturb(S.smooth_union(body, cap, 3.0), ctx.noise(cell=40), 0.7)
    hole = S.sd_rect(ctx.X, ctx.Y, k(10), k(7), k(242), k(23), r=k(3.0))
    hole = S.perturb(hole, ctx.noise(cell=34), 0.4)
    kit.plate(L, ctx, sd, rim=k(5), face_sd=hole, face="stone",
              face_kw=dict(ramp_cols=TROUGH, vignette=12.0, grain_strength=0.12, recess=0.8, catch=0.15,
                           texel=1.8, hue=0.02),
              dabs=10, chip_count=4, halo=None, ink=(2.0, 3.0))
    # left collar (tucks under the portrait) and a trigonal stud on the right cap
    collar = S.sd_rect(ctx.X, ctx.Y, k(1.2), k(0.8), k(8.6), k(29.2), r=k(2.0))
    collar = S.perturb(collar, ctx.noise(cell=20), 0.5)
    PT.metal(L, ctx, collar, rim_out=5.0, style=PT.BRONZE)
    PT.ink_contour(L, ctx, collar, w_min=1.8, w_max=2.6)
    kit.stud_triangle(L, ctx, k(245.2), k(15.0), k(2.6), angle=0.0)
    kit.tidy_alpha(L)
    return L


def _bar_sd(ctx, r):
    return S.sd_rect(ctx.X, ctx.Y, 0.6, 0.6, ctx.w - 0.6, ctx.h - 0.6, r=r)


def paint_player_hp_fill(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    sd = _bar_sd(ctx, k(3.0))
    # Round 3: cut the specular top streak (0.55 -> 0.15 alpha) and add a thin ink contour, so the
    # fill reads as a painted band rather than a glossy flat-red UI bar.
    PT.health_band(L, ctx, sd, 0.0, ctx.h, grain_strength=0.12, streak=0.15)
    PT.ink_contour(L, ctx, sd, w_min=1.3, w_max=1.9, alpha=0.85)
    kit.tidy_alpha(L)
    return L


def paint_player_hp_ghost(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    cols = (P.mix(P.GHOST, P.PARCH_LIGHT, 0.35), P.mix(P.GHOST, P.IVORY_TEXT, 0.55), P.mix(P.GHOST, P.IVORY_TEXT, 0.75))
    PT.health_band(L, ctx, _bar_sd(ctx, k(3.0)), 0.0, ctx.h, cols=cols, grain_strength=0.03, streak=0.0, flat=True)
    kit.tidy_alpha(L)
    return L


CHIP_W, CHIP_H = 58, 28


def paint_chip(ctx: PT.Ctx, glyph: str) -> PT.Layer:
    """Round 3: a small tag -- an ivory parchment disc (bronze rim) holding the ink icon on the
    left, plus a short dark tag with pointed ends for the number (the dodge boot no longer reads
    as the letter 'L')."""
    L = PT.Layer(ctx.w, ctx.h)
    disc = S.perturb(S.sd_circle(ctx.X, ctx.Y, k(14.0), k(14.0), k(12.0)), ctx.noise(cell=18), 0.4)
    dface = S.sd_circle(ctx.X, ctx.Y, k(14.0), k(14.0), k(9.4))
    kit.plate(L, ctx, disc, rim=k(2.6), face_sd=dface, face="parch",
              face_kw=dict(edge=k(2.2), grain_strength=0.04, burn=0.28, base=P.hex_rgb("#E8DCC0"),
                           light=P.PARCH_LIGHT, shadow=P.PARCH_SHADOW),
              dabs=2, chip_count=0, ink=(1.5, 2.1), inner_ink=1.0, halo=None)
    gl = glyphs.glyph_layer(glyph, k(18), ctx.repo_root)
    L.composite(gl, int(round(k(14.0) - gl.w / 2)), int(round(k(14.0) - gl.h / 2)))
    tag = kit.silhouette(ctx, kpts(S.hex_plate(25.0, 5.5, CHIP_W - 1.0, CHIP_H - 5.5, 5.0)), amp=0.4, cell=22)
    face = kit.inset(ctx, tag, k(2.2))
    kit.plate(L, ctx, tag, rim=k(2.2), face_sd=face, face="stone",
              face_kw=dict(ramp_cols=STONE, vignette=7.0, grain_strength=0.1, recess=0.4),
              zones=zpx("Crest/Chip"), zone_feather=2.0, dabs=2, chip_count=0, ink=(1.5, 2.1), inner_ink=1.0,
              halo=None)
    PT.flatten_zones(L, ctx, zpx("Crest/Chip"), feather=2.0, max_std=1.0)
    kit.tidy_alpha(L)
    return L


# ================================================================== Enemy

def paint_enemy_hp_track(ctx: PT.Ctx) -> PT.Layer:
    """Round 3: track display 132x16 -> 136x20; the empty interior is oxblood, not near-black."""
    L = PT.Layer(ctx.w, ctx.h)
    sd = S.perturb(S.sd_rect(ctx.X, ctx.Y, k(0.8), k(0.8), k(135.2), k(19.2), r=k(3.2)), ctx.noise(cell=30), 0.5)
    hole = S.sd_rect(ctx.X, ctx.Y, k(4), k(4), k(132), k(16), r=k(2.0))
    kit.plate(L, ctx, sd, rim=k(3.0), face_sd=hole, face="stone",
              face_kw=dict(ramp_cols=TROUGH, vignette=6.0, grain_strength=0.1, recess=0.6, catch=0.12),
              dabs=5, chip_count=2, ink=(1.6, 2.3), inner_ink=1.0, halo=None)
    kit.tidy_alpha(L)
    return L


def paint_enemy_hp_fill(ctx: PT.Ctx) -> PT.Layer:
    """Round 3: fill display 124x8 -> 128x12; specular streak cut to .15 alpha plus a thin ink contour."""
    L = PT.Layer(ctx.w, ctx.h)
    sd = _bar_sd(ctx, k(2.0))
    PT.health_band(L, ctx, sd, 0.0, ctx.h, grain_strength=0.1, streak=0.15)
    PT.ink_contour(L, ctx, sd, w_min=1.0, w_max=1.5, alpha=0.85)
    kit.tidy_alpha(L)
    return L


def intent_socket_layer(ctx: PT.Ctx, *, revealed=False) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    c = k(22)
    sd = S.perturb(S.sd_circle(ctx.X, ctx.Y, c, c, k(20.8)), ctx.noise(cell=24), 0.5)
    face = S.perturb(S.sd_circle(ctx.X, ctx.Y, c, c, k(15.2)), ctx.noise(cell=20), 0.35)
    if revealed:
        # Revealed intent: a lit parchment disc so the dark-ink enemy icon reads at 40 px.
        info = kit.plate(L, ctx, sd, rim=k(5.6), face_sd=face, face="parch",
                         face_kw=dict(edge=k(4.0), grain_strength=0.04, burn=0.35),
                         dabs=0, chip_count=3, ink=(1.9, 2.7), halo=None)
    else:
        info = kit.plate(L, ctx, sd, rim=k(5.6), face_sd=face, face="stone",
                         face_kw=dict(ramp_cols=(P.scale(P.CHAR_DEEP, 0.85), P.CHAR, P.CHAR_LIGHT), vignette=14.0,
                                      grain_strength=0.16, recess=0.7),
                         dabs=0, chip_count=3, ink=(1.9, 2.7), halo=None)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    for ang in (-90.0, 30.0, 150.0):
        a = math.radians(ang)
        kit.stud_triangle(L, ctx, c + k(18.0) * math.cos(a), c + k(18.0) * math.sin(a), k(2.4), angle=ang)
    return L


def paint_intent_socket(ctx: PT.Ctx) -> PT.Layer:
    L = intent_socket_layer(ctx, revealed=True)
    kit.tidy_alpha(L)
    return L


def paint_intent_unknown(ctx: PT.Ctx) -> PT.Layer:
    L = intent_socket_layer(ctx)
    c = k(22)
    amber_glow(L, ctx, c, c, k(12.5), 0.26)
    q = PT.text_sd(ctx, "?", "display", "ExtraBold", k(31), c, c - k(1.5))
    q = S.perturb(q, ctx.noise(cell=14), 0.35)
    fill = P.mix(P.AMBER, P.GOLD, 0.35)
    t = np.clip((ctx.Y - (c - k(9))) / k(18), 0.0, 1.0)
    col = P.mix(P.mix(fill, P.EMBER, 0.35), P.scale(fill, 0.8), t)
    L.paint(col, PT.coverage(q))
    PT.ink_contour(L, ctx, S.offset(q, 2.4), w_min=2.2, w_max=3.0)
    kit.tidy_alpha(L)
    return L


def paint_reveal_flash(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    c = ctx.w / 2.0
    r = kit.radial(ctx, c, c) / (ctx.w / 2.0)
    ang = np.arctan2(ctx.Y - c, ctx.X - c)
    rays = np.zeros_like(r)
    rng = ctx.rng
    for _ in range(9):
        a0 = rng.uniform(-math.pi, math.pi)
        width = rng.uniform(0.10, 0.22)
        d = np.abs(np.angle(np.exp(1j * (ang - a0))))
        rays = np.maximum(rays, (1.0 - PT.smoothstep(0.0, width, d)) * rng.uniform(0.6, 1.0))
    g = ctx.grain(texel=1.6)
    core = 1.0 - PT.smoothstep(0.0, 0.55, r)
    ray_a = rays * (1.0 - PT.smoothstep(0.2, 0.98, r))
    a = np.clip(0.6 * core + 0.42 * ray_a, 0.0, 1.0) * (0.9 + 0.2 * (g.r - 0.5))
    a = np.clip(a, 0.0, 0.6) * (1.0 - PT.smoothstep(0.9, 0.99, r))
    col = P.mix(P.AMBER, P.mix(P.EMBER, P.IVORY_TEXT, 0.6), core)
    L.paint(col, a)
    kit.tidy_alpha(L)
    return L


def paint_history_token(ctx: PT.Ctx) -> PT.Layer:
    """Round 3 (new): a small parchment disc with a bronze rim for the observed-history row,
    displayed 22x22 (sprite 44x44)."""
    L = PT.Layer(ctx.w, ctx.h)
    c = k(11.0)
    sd = S.perturb(S.sd_circle(ctx.X, ctx.Y, c, c, k(10.2)), ctx.noise(cell=14), 0.35)
    face = S.perturb(S.sd_circle(ctx.X, ctx.Y, c, c, k(7.4)), ctx.noise(cell=12), 0.25)
    kit.plate(L, ctx, sd, rim=k(2.6), face_sd=face, face="parch",
              face_kw=dict(edge=k(2.0), grain_strength=0.04, burn=0.3),
              dabs=2, chip_count=0, ink=(1.4, 1.9), inner_ink=0.9, halo=None)
    kit.tidy_alpha(L)
    return L


def paint_target_marker(ctx: PT.Ctx) -> PT.Layer:
    """Round 3: a painted antique-gold down-pointing trigon, not a neutral chevron.
    Round 4: 24x20 -> 36x30 display; at 24 px, tinted, it vanished against the stone."""
    L = PT.Layer(ctx.w, ctx.h)
    pts = kpts(S.triangle(18.0, 13.5, 13.5, angle_deg=90.0))
    sd = kit.silhouette(ctx, pts, amp=0.3, cell=14)
    PT.metal(L, ctx, sd, rim_out=k(3.0), style=PT.GOLD_TRIGON)
    # ivory highlight on the upper-left facet + a small cel bevel
    hl = S.sd_polygon(ctx.X, ctx.Y, kpts([(9.0, 5.7), (19.5, 6.3), (16.5, 13.5)]))
    L.paint(np.array([0.97, 0.95, 0.9], np.float32), PT.coverage(hl) * 0.55)
    PT.ink_contour(L, ctx, sd, w_min=1.6, w_max=2.3)
    kit.tidy_alpha(L)
    return L


# ================================================================= Static

def paint_floor_plaque(ctx: PT.Ctx) -> PT.Layer:
    """Round 3: a hanging plaque with a pointed bottom; rim pulled ~15% dimmer than the interactive
    controls (sockets, Commit) so static HUD chrome does not out-rank them."""
    L = PT.Layer(ctx.w, ctx.h)
    pts = kpts([(17, 1.6), (213, 1.6), (228.4, 29.5), (213, 57.4), (128, 57.4), (115, 67.0),
                (102, 57.4), (17, 57.4), (1.6, 29.5)])
    sd = kit.silhouette(ctx, pts, amp=0.9)
    face = kit.silhouette(ctx, kpts(S.chamfer_rect(24.0, 5.8, 206.0, 55.8, 3.0)), amp=0.3, cell=36)
    info = kit.plate(L, ctx, sd, rim=k(5), face_sd=face, style=PT.BRONZE_STATIC_DIM, face="stone",
                     face_kw=dict(ramp_cols=STONE, vignette=22.0, grain_strength=0.28, recess=0.65, texel=2.6,
                                  mottle=0.55),
                     zones=zpx("Static/FloorPlaque.png"), zone_feather=2.0, dabs=0, chip_count=6, halo=None)
    zmask = PT.zone_mask(ctx, zpx("Static/FloorPlaque.png"), feather=2.0)
    kit.gradient_grain(L, ctx, face, zones_mask=zmask)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    trigon_gem(L, ctx, k(115), k(62.5), k(3.8), P.mix(P.TEAL, P.TEAL_LIGHT, 0.5), setting=1.8)
    # non-directional upward trigon rivets (never left/right arrows)
    cap_gems(L, ctx, [(12.0, 29.5, -90.0), (218.0, 29.5, -90.0)], 4.6, P.mix(P.TEAL, P.TEAL_LIGHT, 0.4))
    PT.flatten_zones(L, ctx, zpx("Static/FloorPlaque.png"), feather=2.0)
    kit.tidy_alpha(L)
    return L


def paint_action_medallion(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    c = k(66)
    ring = S.sd_circle(ctx.X, ctx.Y, c, c, k(61.0))
    spikes = [S.sd_polygon(ctx.X, ctx.Y, kpts(S.triangle(66 + 53.4 * math.cos(math.radians(a)),
                                                          66 + 53.4 * math.sin(math.radians(a)), 11.8, a)))
              for a in (-90.0, 30.0, 150.0)]
    sd = ring
    for sp in spikes:
        sd = S.smooth_union(sd, sp, 4.0)
    sd = S.perturb(sd, ctx.noise(cell=50), 0.8)
    face = S.perturb(S.sd_circle(ctx.X, ctx.Y, c, c, k(51.5)), ctx.noise(cell=40), 0.4)
    # Round 3: the ACTIONS ring is static HUD chrome, so its peak brightness is pulled ~15% below
    # the interactive sockets/Commit.
    info = kit.plate(L, ctx, sd, rim=k(9.5), face_sd=face, style=PT.BRONZE_STATIC_DIM, face="stone",
                     face_kw=dict(ramp_cols=(P.scale(P.CHAR_DEEP, 0.9), P.CHAR, P.CHAR_LIGHT), vignette=34.0,
                                  grain_strength=0.28, recess=0.7, texel=2.6, mottle=0.55),
                     zones=zpx("Static/ActionMedallion.png"), dabs=0, chip_count=7, halo=None)
    zmask = PT.zone_mask(ctx, zpx("Static/ActionMedallion.png"), feather=3.0)
    kit.gradient_grain(L, ctx, face, zones_mask=zmask)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    # engraved arc under the number (pip row) and gems on the three trigonal points
    ring2 = kit.radial(ctx, c, c) - k(47.5)
    PT.engraved_line(L, ctx, ring2, width=1.5, depth=0.3, mask=PT.coverage(face) * (ctx.Y > k(84)))
    for a in (-90.0, 30.0, 150.0):
        rad = math.radians(a)
        trigon_gem(L, ctx, c + k(57.5) * math.cos(rad), c + k(57.5) * math.sin(rad), k(4.6),
                   P.mix(P.TEAL, P.TEAL_LIGHT, 0.45), angle=a, setting=1.8)
    PT.flatten_zones(L, ctx, zpx("Static/ActionMedallion.png"), feather=3.0)
    kit.tidy_alpha(L)
    return L


def paint_pip(ctx: PT.Ctx, on: bool) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    c = ctx.w / 2.0
    pts = S.diamond(c, c, k(5.6), k(6.8))
    if on:
        amber_glow(L, ctx, c, c, k(7.8), 0.35)
        kit.gem(L, ctx, pts, P.AMBER, lit=1.0, setting=2.0)
    else:
        kit.gem(L, ctx, pts, P.scale(P.CHAR_LIGHT, 0.9), lit=0.0, setting=2.0, setting_style=PT.BRONZE_DIM)
    kit.tidy_alpha(L)
    return L


def card_back(L, ctx, cx, cy, w, h, angle, *, scorched=0.0):
    """Tiny painted card back (parchment, ink edge, trigonal emblem); units in output pixels."""
    pts = S.rotate_points(S.chamfer_rect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2, w * 0.1), cx, cy, angle)
    sd = S.perturb(S.sd_polygon(ctx.X, ctx.Y, pts), ctx.noise(cell=16), 0.5)
    cards.drop_shadow(L, sd, dy=2.5, dx=1.0, blur=1.8, alpha=0.55)
    PT.parchment(L, ctx, sd, edge=4.0, burn=0.3 + scorched, grain_strength=0.05,
                 base=P.mix(P.PARCH, P.PARCH_SHADOW, 0.25))
    kit.cel_edges(L, ctx, sd, P.PARCH_LIGHT, P.PARCH_SHADOW, width=3.0, alpha=0.7)
    inner = S.sd_polygon(ctx.X, ctx.Y, S.rotate_points(
        S.chamfer_rect(cx - w * 0.32, cy - h * 0.34, cx + w * 0.32, cy + h * 0.34, w * 0.06), cx, cy, angle))
    PT.ink_line(L, ctx, inner, width=1.0, alpha=0.45)
    tri = S.sd_polygon(ctx.X, ctx.Y, S.rotate_points(S.triangle(cx, cy + h * 0.02, w * 0.17, -90), cx, cy, angle))
    L.paint(P.BRONZE, PT.coverage(tri) * 0.9)
    PT.ink_line(L, ctx, tri, width=0.9, alpha=0.7)
    if scorched:
        d = -sd
        burn = (1.0 - PT.smoothstep(0.0, 7.0 + 5.0 * ctx.noise(cell=10), d)) * PT.coverage(sd)
        L.paint(P.mix(P.SHADOW_WARM, P.RED_DARK, 0.25), np.clip(burn * scorched * 1.4, 0, 0.9))
        ember = burn * np.clip(ctx.noise(cell=6), 0, 1)
        L.paint(P.AMBER, np.clip(ember * scorched * 0.6, 0, 0.5))
    PT.ink_contour(L, ctx, sd, w_min=1.2, w_max=1.8)


def paint_token(ctx: PT.Ctx, kind: str) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    if kind == "Draw":
        pts = kpts(S.heater_shield(2.0, 2.0, 94.0, 111.0, 74.0, chamfer=9.0, n=40))
        face_pts = kpts(S.heater_shield(8.0, 8.0, 88.0, 103.0, 74.0, chamfer=6.5, n=40))
    else:
        pts = kpts(S.inverted_shield(2.0, 2.0, 94.0, 110.0, 44.0, 16.0, n=40))
        face_pts = kpts(S.inverted_shield(8.0, 11.5, 88.0, 104.0, 46.0, 11.0, n=40))
    sd = kit.silhouette(ctx, pts, amp=0.9)
    face = kit.silhouette(ctx, face_pts, amp=0.3, cell=36)
    info = kit.plate(L, ctx, sd, rim=k(6), face_sd=face, face="stone", zone_feather=2.0,
                     face_kw=dict(ramp_cols=STONE, vignette=26.0, grain_strength=0.28, recess=0.65, texel=2.6,
                                  mottle=0.55),
                     zones=zpx("Static/Token"), dabs=0, chip_count=5, halo=None)
    zmask = PT.zone_mask(ctx, zpx("Static/Token"), feather=2.0)
    kit.gradient_grain(L, ctx, face, zones_mask=zmask)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    if kind == "Draw":
        for i, (dx, dy, a) in enumerate(((-5.0, 3.0, -9.0), (0.0, 1.0, -2.0), (5.0, -1.0, 5.0))):
            card_back(L, ctx, k(48 + dx), k(33 + dy), k(24), k(32), a)
    else:
        card_back(L, ctx, k(42), k(42), k(22), k(29), -22.0, scorched=0.55)
        card_back(L, ctx, k(55), k(40), k(22), k(29), 14.0, scorched=0.7)
    PT.flatten_zones(L, ctx, zpx("Static/Token"), feather=2.0)
    kit.tidy_alpha(L)
    return L


def paint_banner(ctx: PT.Ctx) -> PT.Layer:
    """Round 3: an ivory parchment targeting ribbon (was a flat blue-black slab with '< >' arrows),
    440x46 display (880x92 sprite), with small bronze end caps and non-directional trigon studs."""
    L = PT.Layer(ctx.w, ctx.h)
    W = 440
    cloth = S.sd_rect(ctx.X, ctx.Y, k(15), k(4.0), k(W - 15), k(42.0))
    edge_n = ctx.noise(cell=16, octaves=2)
    cloth = cloth + 0.5 * edge_n * (np.abs(ctx.Y - k(23.0)) > k(17.0))
    cards.drop_shadow(L, cloth, dy=3.0, blur=3.0, alpha=0.45)
    clean = PT.zone_mask(ctx, zpx("Static/Banner.png"), feather=4.0)
    PT.parchment(L, ctx, cloth, clean=clean, edge=6.0, burn=0.32, grain_strength=0.05,
                 base=P.hex_rgb("#E6D8B8"), light=P.PARCH_LIGHT, shadow=P.PARCH_SHADOW)
    kit.cel_edges(L, ctx, cloth, P.PARCH_LIGHT, P.scale(P.PARCH_SHADOW, 0.92), width=6.0, alpha=0.7,
                  mask=1.0 - clean)
    fold = 1.0 - PT.smoothstep(1.5, 4.5, ctx.Y - k(4.0))
    L.paint(P.mix(P.TEAL, P.TEAL_LIGHT, 0.5), fold * PT.coverage(cloth) * 0.12)
    # hem inlays top and bottom (a continuous worn bronze line, not a dashed/stitched border)
    for yy in (k(6.5), k(39.5)):
        line = np.clip(0.5 - (np.abs(ctx.Y - yy) - 0.6), 0, 1)
        L.paint(P.mix(P.BRONZE, P.BRONZE_LIGHT, 0.4), line * PT.coverage(cloth) * 0.55)
    PT.ink_contour(L, ctx, cloth, w_min=1.7, w_max=2.4)
    # small bronze end caps, each with a NON-directional trigon stud (an upward rivet with a teal
    # inlay) -- never a left/right pointing arrow
    for side in (0, 1):
        if side == 0:
            pts = [(21.5, 1.5), (21.5, 44.5), (11.0, 44.5), (1.2, 23.0), (11.0, 1.5)]
        else:
            pts = [(W - 21.5, 1.5), (W - 11.0, 1.5), (W - 1.2, 23.0), (W - 11.0, 44.5), (W - 21.5, 44.5)]
        csd = kit.silhouette(ctx, kpts(pts), amp=0.45, cell=18)
        cards.drop_shadow(L, csd, dy=2.0, blur=2.0, alpha=0.45)
        hole = kit.inset(ctx, csd, k(3.6))
        kit.plate(L, ctx, csd, rim=k(3.6), face_sd=hole, face="stone",
                  face_kw=dict(ramp_cols=STONE, vignette=6.0, grain_strength=0.14, recess=0.5),
                  dabs=2, chip_count=1, halo=None, ink=(1.5, 2.1))
        cx = k(12.8) if side == 0 else k(W - 12.8)
        trigon_gem(L, ctx, cx, k(23.0), k(3.8), P.mix(P.TEAL, P.TEAL_LIGHT, 0.4), angle=-90.0, setting=1.6)
    PT.flatten_zones(L, ctx, zpx("Static/Banner.png"), feather=3.0)
    kit.tidy_alpha(L)
    return L


def paint_terminal_banner(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    # Round 3: display 640x190 -> 560x166 (uniform ~0.875 scale of every reference coordinate below),
    # and every left/right directional triangle becomes a non-directional trigon stud.
    W = 560
    sd = kit.silhouette(ctx, kpts(S.hex_plate(1.75, 17.5, W - 1.75, 162.75, 28.0)), amp=1.0)
    face = kit.silhouette(ctx, kpts(S.chamfer_rect(40.25, 27.1, W - 40.25, 153.1, 7.0)), amp=0.5, cell=50)
    info = kit.plate(L, ctx, sd, rim=k(9.6), face_sd=face, face="stone",
                     face_kw=dict(ramp_cols=STONE, vignette=38.5, grain_strength=0.28, recess=0.7, texel=2.8,
                                  mottle=0.6),
                     zones=zpx("Static/TerminalBanner.png"), dabs=0, chip_count=12, halo=None)
    zmask = PT.zone_mask(ctx, zpx("Static/TerminalBanner.png"), feather=3.0)
    kit.gradient_grain(L, ctx, face, zones_mask=zmask)
    kit.worn_dabs_10oclock(L, ctx, info["lam"], info["region"], sd, count=3)
    PT.engraved_line(L, ctx, face, at=-3.5, width=1.5, depth=0.3)
    # painted divider between title and body: engraved line fading out, bronze-set diamond at the centre
    seg = S.sd_segment(ctx.X, ctx.Y, k(131.25), k(99.75), k(W - 131.25), k(99.75))
    fade = 1.0 - PT.smoothstep(k(78.75), k(148.75), np.abs(ctx.X - k(W / 2)))
    PT.engraved_line(L, ctx, seg, width=1.6, depth=0.45, mask=fade)
    L.paint(P.BRONZE_LIGHT, np.clip(0.5 - (seg - 0.5), 0, 1) * fade * 0.55)
    kit.gem(L, ctx, kpts(S.diamond(W / 2, 99.75, 5.25, 1.9)), P.AMBER, lit=0.4, setting=1.4)
    # trigonal crest at the top centre
    crest = S.sd_polygon(ctx.X, ctx.Y, kpts([(W / 2, 1.3), (W / 2 + 38.5, 29.75), (W / 2 - 38.5, 29.75)]))
    crest = S.smooth_union(crest, S.sd_circle(ctx.X, ctx.Y, k(W / 2), k(26.25), k(13.1)), 5.25)
    crest = S.perturb(crest, ctx.noise(cell=30), 0.6)
    cards.drop_shadow(L, crest, dy=3.5, blur=2.6, alpha=0.5)
    cface = S.sd_polygon(ctx.X, ctx.Y, kpts([(W / 2, 10.5), (W / 2 + 21.9, 26.7), (W / 2 - 21.9, 26.7)]))
    kit.plate(L, ctx, crest, rim=k(5.25), face_sd=S.offset(cface, 1.75), face="stone",
              face_kw=dict(ramp_cols=STONE, vignette=8.75, grain_strength=0.2, recess=0.5),
              dabs=3, chip_count=3, halo=None)
    trigon_gem(L, ctx, k(W / 2), k(22.3), k(7.4), P.AMBER, lit=0.7, angle=-90.0, setting=2.1)
    # non-directional upward trigon rivets, never left/right arrows
    for x in (W / 2 - 131.25, W / 2 + 131.25):
        kit.stud_triangle(L, ctx, k(x), k(22.3), k(3.15), angle=-90.0)
    cap_gems(L, ctx, [(19.25, 90.1, -90.0), (W - 19.25, 90.1, -90.0)], 7.9, P.mix(P.TEAL, P.TEAL_LIGHT, 0.4))
    for x in (35.0, W - 35.0):
        for y in (33.25, 147.0):
            kit.stud_triangle(L, ctx, k(x), k(y), k(2.98), angle=-90.0)
    PT.flatten_zones(L, ctx, zpx("Static/TerminalBanner.png"), feather=3.0)
    kit.tidy_alpha(L)
    return L


def paint_vignette(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    c = ctx.w / 2.0
    r = kit.radial(ctx, c, c) / (math.sqrt(2.0) * c)
    a = 0.7 * PT.smoothstep(0.32, 1.0, r) ** 1.15
    a = a + ctx.rng.uniform(-0.5, 0.5, a.shape).astype(np.float32) / 255.0 * (a > 0.002)
    L.paint(P.SHADOW_WARM, np.clip(a, 0.0, 0.7))
    return L


def paint_text_wash(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    g = ctx.grain(texel=2.0, angle=2.0, stretch=6.0)
    x = ctx.X / ctx.w
    y = (ctx.Y - ctx.h / 2.0) / (ctx.h / 2.0)
    nz = ctx.noise(cell=60, octaves=2)
    ends = PT.smoothstep(0.0, 0.22 + 0.04 * nz, x) * PT.smoothstep(0.0, 0.22 - 0.04 * nz, 1.0 - x)
    vert = np.exp(-(y / (0.62 + 0.08 * nz)) ** 4)
    a = 0.55 * ends * vert * (0.85 + 0.3 * (g.r - 0.5))
    edge = S.sd_rect(ctx.X, ctx.Y, 0, 0, ctx.w, ctx.h)
    a = a * PT.smoothstep(0.0, 3.0, -edge)
    col = P.mix(P.INK, P.CHAR_DEEP, 0.5)
    L.paint(col, np.clip(a, 0.0, 0.55))
    kit.tidy_alpha(L)
    return L
