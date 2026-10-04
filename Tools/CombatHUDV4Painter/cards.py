"""Card family: category frames, shadow, focus rim, detail frame and detail art washes."""
from __future__ import annotations

import numpy as np
from scipy import ndimage

import kit
import paint as PT
import palette as P
import shapes as S
from kit import k, kpts

CARD_W, CARD_H = 225, 300

# Text-safe zones (1080 reference units, card top-left origin) — shared with the registry/tests.
CARD_ZONES = {
    "title": (30, 14, 195, 40),
    "effect": (26, 190, 199, 258),
    "label": (54, 270, 199, 288),
}
DETAIL_ZONES = {
    "title": (40, 14, 320, 58),
    "target": (30, 214, 330, 240),
    "body": (30, 246, 330, 430),
}

# Round 3 (new): the enlarged in-socket card art window, from just under the ribbon down to
# ~88% of the card height (300 * 0.88 = 264).
SOCKET_WIN_Y0, SOCKET_WIN_BOTTOM, SOCKET_WIN_DEPTH = 58, 264, 52
# Round 4: the socketed card is shown at ~0.5 scale, so its ribbon is taller (39 -> 56 units) and the
# title zone grows with it; at 1080 the title reads ~15 px instead of ~9 px.
SOCKET_RIBBON = (6, 219, 6, 62)
SOCKET_TITLE_ZONE = (26, 12, 199, 55)
SOCKET_FOOTER_Y0, SOCKET_FOOTER_Y1 = 270, 293.5

# Round 3 (new): the category detail frame is the hand-card frame recipe scaled up 1.6x
# (225x300 -> 360x480).
DETAIL_CAT_SCALE = 1.6
DETAIL_CAT_W, DETAIL_CAT_H = 360, 480
DETAIL_CAT_ZONES = {
    "title": (48, 22, 312, 64),
    "target": (30, 288, 330, 308),
    "effect": (30, 312, 330, 442),
    "footer_label": (100, 448, 300, 466),
}


def _zones_px(zones):
    return [PT.zone_px(z) for z in zones]


# ----------------------------------------------------------------- components

def card_outline(ox=0.0, oy=0.0, w=CARD_W, h=CARD_H, chamfer=10.0, margin=1.3):
    return kpts(S.chamfer_rect(ox + margin, oy + margin, ox + w - margin, oy + h - margin, chamfer))


def arch_window_sd(ctx, x0, x1, y_apex, y_bottom, depth, *, amp=0.5):
    pts = kpts(S.pointed_arch(x0, x1, y_bottom, y_apex + depth, y_apex, n=40))
    return kit.silhouette(ctx, pts, amp=amp, cell=44.0)


def drop_shadow(layer: PT.Layer, sd, *, dy=4.0, dx=1.0, blur=3.0, alpha=0.45, colour=P.SHADOW_WARM, mask=None):
    m = PT.coverage(sd)
    m = ndimage.shift(m, (dy, dx), order=1)
    m = PT.blur(m, blur) * alpha
    if mask is not None:
        m = m * mask
    layer.paint(colour, m)


def ribbon(layer: PT.Layer, ctx: PT.Ctx, x0, x1, y0, y1, *, tail=13.0, tail_drop=4.0, notch=6.0, bow=1.5,
           tip_ramp=None, zones_px=None):
    """Parchment title ribbon (reference units) with folded, notched, category-tinted tails behind it."""
    tip_ramp = tip_ramp or (P.BRONZE_DARK, P.BRONZE, P.BRONZE_LIGHT)
    ty0, ty1 = y0 + tail_drop, y1 + tail_drop * 0.5
    left = kpts(S.ribbon_tail(x0 + tail + 6, x0, ty0, ty1, notch))
    right = kpts(S.ribbon_tail(x1 - tail - 6, x1, ty0, ty1, notch))
    for pts, fold_x in ((left, k(x0 + tail + 6)), (right, k(x1 - tail - 6))):
        sd = kit.silhouette(ctx, pts, amp=0.7, cell=30.0)
        drop_shadow(layer, sd, dy=3.0, blur=2.5, alpha=0.5)
        # cloth shading: lit top band, darker toward the fold under the main band
        t = (ctx.Y - k(ty0)) / max(k(ty1 - ty0), 1.0)
        fold = 1.0 - PT.smoothstep(0.0, 26.0, np.abs(ctx.X - fold_x))
        g = ctx.grain(texel=1.6, angle=5.0, stretch=2.0)
        v = 0.75 - 0.55 * t - 0.45 * fold + 0.12 * (g.r - 0.5)
        lvl = PT.blur(PT.cel(v, (0.30, 0.58), soft=0.02), 0.6)
        col = PT.ramp(lvl, tip_ramp)
        layer.paint(col, PT.coverage(sd))
        PT.ink_contour(layer, ctx, sd, w_min=1.8, w_max=2.6)
    # main band
    band = kpts(S.ribbon_band(x0 + tail * 0.9, y0, x1 - tail * 0.9, y1, bow=bow, n=40))
    sd = kit.silhouette(ctx, band, amp=0.7, cell=36.0)
    drop_shadow(layer, sd, dy=5.0, blur=3.5, alpha=0.5)
    clean = PT.zone_mask(ctx, zones_px, feather=6.0) if zones_px else None
    PT.parchment(layer, ctx, sd, clean=clean, edge=6.0, burn=0.35, grain_strength=0.05,
                 base=P.mix(P.PARCH, P.PARCH_SHADOW, 0.2), light=P.mix(P.PARCH_LIGHT, P.PARCH, 0.2))
    kit.cel_edges(layer, ctx, sd, P.PARCH_LIGHT, P.scale(P.PARCH_SHADOW, 0.92), width=7.0, alpha=0.75,
                  mask=None if clean is None else 1.0 - clean)
    # painted fold shading at the band ends (the band curls back into the tails)
    ends = np.maximum(1.0 - PT.smoothstep(0.0, 22.0, ctx.X - k(x0 + tail * 0.9)),
                      1.0 - PT.smoothstep(0.0, 22.0, k(x1 - tail * 0.9) - ctx.X))
    layer.multiply(1.0 - 0.30 * PT.cel(ends, (0.4, 0.75), soft=0.05) / 2.0, mask=PT.coverage(sd))
    PT.ink_contour(layer, ctx, sd, w_min=1.9, w_max=2.8)
    return sd


def notched_panel_sd(ctx: PT.Ctx, x0, y0, x1, y1, notch):
    """Rectangle with concave (quarter-circle) notched corners, reference units in, pixels out."""
    sd = S.sd_rect(ctx.X, ctx.Y, k(x0), k(y0), k(x1), k(y1))
    r = k(notch)
    for cx, cy in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)):
        sd = S.subtract(sd, S.sd_circle(ctx.X, ctx.Y, k(cx), k(cy), r))
    return sd


def parchment_panel(layer: PT.Layer, ctx: PT.Ctx, x0, y0, x1, y1, *, chamfer=5.0, zones_px=None, edge=9.0,
                    burn=0.30, tone=0.32):
    sd = kit.silhouette(ctx, notched_panel_sd(ctx, x0, y0, x1, y1, chamfer), amp=0.8, cell=40.0)
    drop_shadow(layer, sd, dy=3.0, blur=2.5, alpha=0.55)
    clean = PT.zone_mask(ctx, zones_px, feather=6.0) if zones_px else None
    PT.parchment(layer, ctx, sd, clean=clean, edge=edge, burn=burn, grain_strength=0.055,
                 base=P.mix(P.PARCH, P.PARCH_SHADOW, tone), light=P.mix(P.PARCH_LIGHT, P.PARCH, tone))
    kit.cel_edges(layer, ctx, sd, P.PARCH_LIGHT, P.scale(P.PARCH_SHADOW, 0.9), width=6.0, alpha=0.7,
                  mask=None if clean is None else 1.0 - clean)
    # gentle painted light: warmer/lighter at the top, a touch darker toward the bottom
    t = np.clip((ctx.Y - k(y0)) / max(k(y1 - y0), 1.0), 0.0, 1.0)
    layer.multiply(1.04 - 0.08 * t, mask=PT.coverage(sd))
    PT.ink_contour(layer, ctx, sd, w_min=1.5, w_max=2.3)
    return sd


def footer_tab(layer: PT.Layer, ctx: PT.Ctx, x0, y0, x1, y1, ramp, *, socket_cx, socket_r, zones_px=None):
    pts = kpts(S.notched_hex_plate(x0, y0, x1, y1, 7.0, 3.0))
    sd = kit.silhouette(ctx, pts, amp=0.6, cell=30.0)
    drop_shadow(layer, sd, dy=3.0, blur=2.0, alpha=0.5)
    face = kit.inset(ctx, sd, 3.2)
    PT.metal(layer, ctx, sd, sd_hole=face, rim_out=3.0, rim_in=2.4, style=PT.BRONZE)
    clean = PT.zone_mask(ctx, zones_px, feather=5.0) if zones_px else None
    PT.stone(layer, ctx, S.offset(face, 0.6), ramp_cols=(ramp["deep"], ramp["body"], ramp["body_light"]),
             vignette=9.0, grain_strength=0.14, recess=0.4, clean=clean, texel=1.8)
    PT.ink_line(layer, ctx, face, at=0.3, width=1.2, alpha=0.85)
    # glyph socket: a darker round recess with a bronze lip
    cy = k((y0 + y1) * 0.5)
    ssd = S.sd_circle(ctx.X, ctx.Y, k(socket_cx), cy, k(socket_r))
    PT.metal(layer, ctx, S.offset(ssd, 2.6), sd_hole=ssd, rim_out=2.0, rim_in=1.8, style=PT.BRONZE)
    PT.stone(layer, ctx, ssd, ramp_cols=(P.CHAR_DEEP, P.scale(ramp["deep"], 0.9), ramp["deep"]), vignette=10.0,
             grain_strength=0.1, recess=0.6)
    PT.ink_contour(layer, ctx, S.offset(ssd, 2.6), w_min=1.2, w_max=1.7)
    PT.ink_contour(layer, ctx, sd, w_min=1.8, w_max=2.6)
    return sd


# ------------------------------------------------------------------ card frame

def paint_card_frame(ctx: PT.Ctx, category: str) -> PT.Layer:
    ramp = P.category_ramp(category)
    L = PT.Layer(ctx.w, ctx.h)
    sd_card = kit.silhouette(ctx, card_outline(), amp=0.9)
    body_ramp = (ramp["deep"], ramp["body"], ramp["body_light"])
    info = kit.plate(L, ctx, sd_card, rim=k(10.5), face="stone",
                     face_kw=dict(ramp_cols=body_ramp, vignette=16.0, grain_strength=0.18, recess=0.5, catch=0.2,
                                  texel=2.0, mottle=0.45),
                     dabs=16, chip_count=7, halo=None)
    # category-tinted inner bevel line (enamel inlay just inside the bronze band)
    face = info["face"]
    band = PT.smoothstep(-0.3, 0.6, -face) * (1.0 - PT.smoothstep(3.2, 4.6, -face))
    L.paint(ramp["accent_light"], band * 0.85)
    PT.ink_line(L, ctx, face, at=-4.4, width=1.0, alpha=0.55)

    # trigonal studs on the side bands between window and effect area
    for x, ang in ((k(5.6), 0.0), (k(CARD_W - 5.6), 180.0)):
        kit.stud_triangle(L, ctx, x, k(181), k(3.8), angle=ang)

    # art window: pointed arch with a thin bronze lip and a category wash
    win = arch_window_sd(ctx, 20, 205, 38, 178, 52)
    kit.edge_shade(L, ctx, S.offset(win, 7.0), width=8.0, amount=0.25)
    PT.metal(L, ctx, S.offset(win, 5.0), sd_hole=win, rim_out=3.5, rim_in=2.5, style=PT.BRONZE)
    centre = (k(112.5), k(112))
    kit.painted_wash(L, ctx, S.offset(win, 0.5), [ramp["wash_dark"], ramp["wash"], ramp["wash_light"]],
                     centre=centre, icon_r=k(46))
    PT.ink_line(L, ctx, win, at=0.3, width=1.5, alpha=0.9)
    PT.ink_contour(L, ctx, S.offset(win, 5.0), w_min=1.4, w_max=2.1)

    # effect parchment + footer tab
    parchment_panel(L, ctx, 18, 184, 207, 262, chamfer=4.0, zones_px=[PT.zone_px(CARD_ZONES["effect"])])
    footer_tab(L, ctx, 16, 264.5, 209, 293.5, ramp, socket_cx=37, socket_r=10.5,
               zones_px=[PT.zone_px(CARD_ZONES["label"])])

    # corner gems (category colour)
    for cx, cy in ((6.0, 62.0), (CARD_W - 6.0, 62.0), (11.0, 289.0), (CARD_W - 11.0, 289.0)):
        kit.gem(L, ctx, kpts(S.diamond(cx, cy, 4.6, 6.2)), ramp["accent_light"], setting=2.2)

    # title ribbon overlapping the arch
    ribbon(L, ctx, 8, 217, 8, 47, tail=14.0, bow=2.5, tip_ramp=(ramp["accent_dark"], ramp["accent"], ramp["accent_light"]),
           zones_px=[PT.zone_px(CARD_ZONES["title"])])

    PT.flatten_zones(L, ctx, _zones_px(CARD_ZONES.values()), feather=3.0)
    kit.tidy_alpha(L)
    return L


def paint_card_frame_socket(ctx: PT.Ctx, category: str) -> PT.Layer:
    """Round 3 (new): the socket variant of the hand-card frame -- same outer silhouette, rail,
    category colour and title ribbon, but the art window is enlarged (down to ~88% of the card
    height) and there is no effect parchment box, only a thin footer band for the category glyph."""
    ramp = P.category_ramp(category)
    L = PT.Layer(ctx.w, ctx.h)
    sd_card = kit.silhouette(ctx, card_outline(), amp=0.9)
    body_ramp = (ramp["deep"], ramp["body"], ramp["body_light"])
    kit.plate(L, ctx, sd_card, rim=k(10.5), face="stone",
             face_kw=dict(ramp_cols=body_ramp, vignette=16.0, grain_strength=0.18, recess=0.5, catch=0.2,
                          texel=2.0, mottle=0.45),
             dabs=16, chip_count=7, halo=None)
    info = {"face": kit.inset(ctx, sd_card, k(10.5))}
    face = info["face"]
    band = PT.smoothstep(-0.3, 0.6, -face) * (1.0 - PT.smoothstep(3.2, 4.6, -face))
    L.paint(ramp["accent_light"], band * 0.85)
    PT.ink_line(L, ctx, face, at=-4.4, width=1.0, alpha=0.55)

    for x, ang in ((k(5.6), 0.0), (k(CARD_W - 5.6), 180.0)):
        kit.stud_triangle(L, ctx, x, k(181), k(3.8), angle=ang)

    # enlarged art window: pointed arch from just under the ribbon down to ~88% of the card height
    win = arch_window_sd(ctx, 20, 205, SOCKET_WIN_Y0, SOCKET_WIN_BOTTOM, SOCKET_WIN_DEPTH)
    kit.edge_shade(L, ctx, S.offset(win, 7.0), width=8.0, amount=0.25)
    PT.metal(L, ctx, S.offset(win, 5.0), sd_hole=win, rim_out=3.5, rim_in=2.5, style=PT.BRONZE)
    icon_cy = (SOCKET_WIN_Y0 + SOCKET_WIN_BOTTOM) / 2.0 + 3.0
    centre = (k(112.5), k(icon_cy))
    kit.painted_wash(L, ctx, S.offset(win, 0.5), [ramp["wash_dark"], ramp["wash"], ramp["wash_light"]],
                     centre=centre, icon_r=k(74))
    PT.ink_line(L, ctx, win, at=0.3, width=1.5, alpha=0.9)
    PT.ink_contour(L, ctx, S.offset(win, 5.0), w_min=1.4, w_max=2.1)

    # thin footer band: category glyph slot only, no effect parchment box
    footer_tab(L, ctx, 16, SOCKET_FOOTER_Y0, 209, SOCKET_FOOTER_Y1, ramp, socket_cx=37, socket_r=9.5)

    for cx, cy in ((6.0, 78.0), (CARD_W - 6.0, 78.0), (11.0, 289.0), (CARD_W - 11.0, 289.0)):
        kit.gem(L, ctx, kpts(S.diamond(cx, cy, 4.6, 6.2)), ramp["accent_light"], setting=2.2)

    # title ribbon overlapping the arch (round 4: taller than the hand card's, see SOCKET_RIBBON)
    rx0, rx1, ry0, ry1 = SOCKET_RIBBON
    ribbon(L, ctx, rx0, rx1, ry0, ry1, tail=15.0, bow=2.5,
           tip_ramp=(ramp["accent_dark"], ramp["accent"], ramp["accent_light"]),
           zones_px=[PT.zone_px(SOCKET_TITLE_ZONE)])

    PT.flatten_zones(L, ctx, [PT.zone_px(SOCKET_TITLE_ZONE)], feather=3.0)
    kit.tidy_alpha(L)
    return L


def paint_card_shadow(ctx: PT.Ctx) -> PT.Layer:
    """Soft painted contact shadow for a 225x300 card centred in a 245x320 canvas."""
    L = PT.Layer(ctx.w, ctx.h)
    pts = kpts(S.chamfer_rect(10 + 4, 10 + 4, 10 + CARD_W - 4, 10 + CARD_H - 4, 12))
    sd = S.perturb(S.sd_polygon(ctx.X, ctx.Y, pts), ctx.noise(cell=70, octaves=2), 3.0)
    a = PT.contact_shadow(ctx, sd, blur_px=9.0, alpha=0.5, offset=(0.0, 12.0), bottom_weight=0.4)
    # fade to zero before the canvas edge
    edge = S.sd_rect(ctx.X, ctx.Y, 0, 0, ctx.w, ctx.h)
    a = a * PT.smoothstep(0.0, 10.0, -edge)
    col = PT.warm_cool(np.broadcast_to(P.SHADOW_WARM, (ctx.h, ctx.w, 3)).copy(), 0.1 * ctx.noise(cell=90))
    L.paint(col, a)
    kit.tidy_alpha(L)
    return L


def paint_card_focus_rim(ctx: PT.Ctx) -> PT.Layer:
    """Painted rim light around the card silhouette; neutral values, tinted at runtime."""
    L = PT.Layer(ctx.w, ctx.h)
    ox, oy = 6, 6
    sd = S.sd_polygon(ctx.X, ctx.Y, card_outline(ox, oy))
    sd = S.perturb(sd, ctx.noise(cell=50, octaves=2), 1.2)
    g = ctx.grain(texel=2.4, angle=30.0, stretch=1.8)
    # drawn ABOVE the card frame at runtime: a painted core over the frame edge plus an outer glow
    outside = PT.smoothstep(-5.0, 0.0, sd) * (1.0 - PT.smoothstep(1.0, 13.0 + 3.0 * g.r, sd))
    core = PT.smoothstep(-8.0, -3.5, sd) * (1.0 - PT.smoothstep(0.5, 4.0, sd))
    a = np.clip(0.5 * outside + 0.92 * core, 0.0, 1.0)
    a = a * (0.78 + 0.34 * (g.r - 0.5) + 0.18 * ctx.noise(cell=60))
    # a touch stronger toward the top-left (the light) so it reads as a painted rim, not a stroke
    f = np.clip(PT.facing_light(sd), -1.0, 1.0)
    a = a * (0.85 + 0.2 * f)
    edge = S.sd_rect(ctx.X, ctx.Y, 0, 0, ctx.w, ctx.h)
    a = a * PT.smoothstep(0.0, 3.0, -edge)
    col = P.mix(np.array([0.93, 0.91, 0.86], np.float32), np.array([1.0, 0.98, 0.93], np.float32),
                np.clip(core, 0.0, 1.0))
    L.paint(col, np.clip(a, 0.0, 0.92))
    kit.tidy_alpha(L)
    return L


# ---------------------------------------------------------------- detail view

def paint_detail_frame(ctx: PT.Ctx) -> PT.Layer:
    L = PT.Layer(ctx.w, ctx.h)
    W, H = 360, 480
    sd_card = kit.silhouette(ctx, kpts(S.chamfer_rect(1.3, 1.3, W - 1.3, H - 1.3, 14)), amp=1.0)
    body = (P.CHAR_DEEP, P.CHAR, P.CHAR_LIGHT)
    info = kit.plate(L, ctx, sd_card, rim=k(10), face="stone",
                     face_kw=dict(ramp_cols=body, vignette=22.0, grain_strength=0.18, recess=0.5, texel=2.2,
                                  mottle=0.45),
                     dabs=24, chip_count=9, halo=None)
    band = PT.smoothstep(-0.3, 0.6, -info["face"]) * (1.0 - PT.smoothstep(3.4, 4.8, -info["face"]))
    L.paint(P.mix(P.BRONZE_LIGHT, P.TEAL, 0.35), band * 0.7)
    PT.ink_line(L, ctx, info["face"], at=-4.8, width=1.0, alpha=0.5)
    for x, ang in ((k(5.2), 0.0), (k(W - 5.2), 180.0)):
        kit.stud_triangle(L, ctx, x, k(212), k(4.4), angle=ang)

    win = arch_window_sd(ctx, 28, 332, 60, 208, 58)
    kit.edge_shade(L, ctx, S.offset(win, 8.0), width=9.0, amount=0.25)
    PT.metal(L, ctx, S.offset(win, 6.0), sd_hole=win, rim_out=4.0, rim_in=3.0)
    kit.painted_wash(L, ctx, S.offset(win, 0.5), [P.scale(P.CHAR_DEEP, 0.9), P.CHAR, P.CHAR_LIGHT],
                     centre=(k(180), k(140)), icon_r=k(56), strokes=0.8, rimwash=0.12, spot=0.2)
    PT.ink_line(L, ctx, win, at=0.3, width=1.6, alpha=0.9)
    PT.ink_contour(L, ctx, S.offset(win, 6.0), w_min=1.5, w_max=2.2)

    parchment_panel(L, ctx, 22, 243.5, 338, 436, chamfer=5.0, zones_px=[PT.zone_px(DETAIL_ZONES["body"])], edge=10.0)
    # target line: a slim dark engraved strip (text is light on dark)
    tgt = kit.silhouette(ctx, kpts(S.notched_hex_plate(24, 209, 336, 244, 8, 4)), amp=0.5)
    drop_shadow(L, tgt, dy=3.0, blur=2.0, alpha=0.45)
    tface = kit.inset(ctx, tgt, 3.0, amp=0.3)
    PT.metal(L, ctx, tgt, sd_hole=tface, rim_out=2.8, rim_in=2.2)
    PT.stone(L, ctx, S.offset(tface, 0.6), ramp_cols=(P.CHAR_DEEP, P.scale(P.CHAR, 0.95), P.CHAR),
             vignette=8.0, grain_strength=0.1, recess=0.35,
             clean=PT.zone_mask(ctx, [PT.zone_px(DETAIL_ZONES["target"])], feather=3.0))
    PT.ink_line(L, ctx, tface, at=0.3, width=1.1, alpha=0.85)
    PT.ink_contour(L, ctx, tgt, w_min=1.6, w_max=2.3)

    footer_tab(L, ctx, 22, 440, 338, 468, {"deep": P.CHAR_DEEP, "body": P.CHAR, "body_light": P.CHAR_LIGHT},
               socket_cx=44, socket_r=11.0)
    for cx, cy in ((9.0, 82.0), (W - 9.0, 82.0), (13.0, 468.0), (W - 13.0, 468.0)):
        kit.gem(L, ctx, kpts(S.diamond(cx, cy, 5.0, 6.6)), P.mix(P.TEAL, P.TEAL_LIGHT, 0.4), setting=2.4)
    ribbon(L, ctx, 14, 346, 7, 64, tail=18.0, tail_drop=5.0, notch=8.0, bow=2.0,
           tip_ramp=(P.BRONZE_DARK, P.BRONZE, P.BRONZE_LIGHT), zones_px=[PT.zone_px(DETAIL_ZONES["title"])])
    PT.flatten_zones(L, ctx, _zones_px(DETAIL_ZONES.values()), feather=2.0, max_std=0.8)
    kit.tidy_alpha(L)
    return L


def paint_detail_frame_category(ctx: PT.Ctx, category: str) -> PT.Layer:
    """Round 3 (new): the detail frame built from the SAME hand-card frame recipe, scaled 1.6x
    (225x300 -> 360x480) and category-coloured, so it reads as the hand card enlarged: same rail,
    category colour, title ribbon, pointed-arch art window and footer glyph slot. Adds a slim
    parchment target-line zone between the window and the effect area, and a clean parchment
    effect box (~130 tall) instead of the old dark strip / oversized empty panel. Keeps
    ``Cards/DetailFrame.png`` as a category-agnostic fallback (painted separately)."""
    ramp = P.category_ramp(category)
    m = DETAIL_CAT_SCALE
    W, H = DETAIL_CAT_W, DETAIL_CAT_H
    L = PT.Layer(ctx.w, ctx.h)
    sd_card = kit.silhouette(ctx, kpts(S.chamfer_rect(1.3 * m, 1.3 * m, W - 1.3 * m, H - 1.3 * m, 10.0 * m)),
                             amp=0.9)
    body_ramp = (ramp["deep"], ramp["body"], ramp["body_light"])
    info = kit.plate(L, ctx, sd_card, rim=k(10.5 * m), face="stone",
                     face_kw=dict(ramp_cols=body_ramp, vignette=16.0 * m, grain_strength=0.18, recess=0.5,
                                  catch=0.2, texel=2.0, mottle=0.45),
                     dabs=24, chip_count=10, halo=None)
    face = info["face"]
    band = PT.smoothstep(-0.3, 0.6, -face) * (1.0 - PT.smoothstep(3.2 * m, 4.6 * m, -face))
    L.paint(ramp["accent_light"], band * 0.85)
    PT.ink_line(L, ctx, face, at=-4.4 * m, width=1.0, alpha=0.55)

    for x, ang in ((k(5.6 * m), 0.0), (k(W - 5.6 * m), 180.0)):
        kit.stud_triangle(L, ctx, x, k(181 * m), k(3.8 * m), angle=ang)

    win = arch_window_sd(ctx, 20 * m, 205 * m, 38 * m, 282, 52 * m)
    kit.edge_shade(L, ctx, S.offset(win, 7.0 * m), width=8.0 * m, amount=0.25)
    PT.metal(L, ctx, S.offset(win, 5.0 * m), sd_hole=win, rim_out=3.5 * m, rim_in=2.5 * m, style=PT.BRONZE)
    centre = (k(112.5 * m), k(112 * m))
    kit.painted_wash(L, ctx, S.offset(win, 0.5), [ramp["wash_dark"], ramp["wash"], ramp["wash_light"]],
                     centre=centre, icon_r=k(46 * m))
    PT.ink_line(L, ctx, win, at=0.3, width=1.5, alpha=0.9)
    PT.ink_contour(L, ctx, S.offset(win, 5.0 * m), w_min=1.4, w_max=2.1)

    # slim parchment target-line zone (ink text goes here at runtime, NOT a dark strip). The outer
    # pointed-end padding must exceed the taper's own point width (8) so the zone rect never
    # dips into the tapered corners (partial alpha there would fail the text-zone check).
    tz = DETAIL_CAT_ZONES["target"]
    tgt = kit.silhouette(ctx, kpts(S.notched_hex_plate(tz[0] - 16, tz[1] - 3, tz[2] + 16, tz[3] + 3, 8, 4)),
                         amp=0.4)
    drop_shadow(L, tgt, dy=2.5, blur=2.0, alpha=0.4)
    PT.parchment(L, ctx, tgt, clean=PT.zone_mask(ctx, [PT.zone_px(tz)], feather=3.0), edge=5.0, burn=0.28,
                 grain_strength=0.05)
    kit.cel_edges(L, ctx, tgt, P.PARCH_LIGHT, P.scale(P.PARCH_SHADOW, 0.92), width=4.0, alpha=0.6)
    PT.ink_contour(L, ctx, tgt, w_min=1.3, w_max=1.9)

    # clean parchment effect box (~130 tall). A small chamfer keeps the corner notch well clear
    # of the zone rect even with modest padding.
    ez = DETAIL_CAT_ZONES["effect"]
    parchment_panel(L, ctx, ez[0] - 6, ez[1] - 2, ez[2] + 6, ez[3] + 6, chamfer=3.0,
                    zones_px=[PT.zone_px(ez)], edge=9.0)

    # thin footer band: category glyph slot
    fz = DETAIL_CAT_ZONES["footer_label"]
    footer_tab(L, ctx, 22, fz[1] - 2, W - 22, fz[3] + 2, ramp, socket_cx=44, socket_r=10.0,
               zones_px=[PT.zone_px(fz)])

    for cx, cy in ((9.0 * m, 62.0 * m), (W - 9.0 * m, 62.0 * m), (13.0 * m, 289.0 * m), (W - 13.0 * m, 289.0 * m)):
        kit.gem(L, ctx, kpts(S.diamond(cx, cy, 4.6 * m, 6.2 * m)), ramp["accent_light"], setting=2.2)

    # title ribbon overlapping the arch (same position/recipe as the hand card, scaled)
    ribbon(L, ctx, 8 * m, 217 * m, 8 * m, 47 * m, tail=14.0 * m, bow=2.5 * m,
           tip_ramp=(ramp["accent_dark"], ramp["accent"], ramp["accent_light"]),
           zones_px=[PT.zone_px(DETAIL_CAT_ZONES["title"])])

    PT.flatten_zones(L, ctx, _zones_px(DETAIL_CAT_ZONES.values()), feather=2.5, max_std=0.9)
    kit.tidy_alpha(L)
    return L


def paint_detail_art(ctx: PT.Ctx, category: str) -> PT.Layer:
    ramp = P.category_ramp(category)
    L = PT.Layer(ctx.w, ctx.h)
    # 300x146 art rectangle with the same pointed-arch mask as the detail window
    sd = kit.silhouette(ctx, kpts(S.pointed_arch(1.2, 298.8, 144.8, 1.2 + 56, 1.2, n=40)), amp=0.5, cell=44.0)
    kit.painted_wash(L, ctx, sd, [ramp["wash_dark"], ramp["wash"], ramp["wash_light"]],
                     centre=(k(150), k(78)), icon_r=k(52))
    kit.tidy_alpha(L)
    return L
