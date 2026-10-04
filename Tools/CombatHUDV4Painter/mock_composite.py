"""Mock composites of the V4 HUD over the M2 world capture (fast art-direction loop before Unity).

    python Tools/CombatHUDV4Painter/mock_composite.py --round r1
    python Tools/CombatHUDV4Painter/mock_composite.py --round r2 --states planning,queued

Renders ``planning``, ``queued``, ``reveal`` and ``overlays`` at 1920x1080 and 2560x1440
(Unity CanvasScaler 1920x1080, match .5 -> uniform scale H/1080) using HudLayoutV4.json,
the painted V4 sprites and the DR fonts, plus ``sheet.png`` (every sprite on mid-grey and
on a world crop). Output: ``Captures/CombatHUDV4/mock/<round>/`` (path-guarded).
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import paint as PT  # noqa: E402
import paint_hud_v4 as painter  # noqa: E402
import registry  # noqa: E402

REPO = painter.REPO_ROOT
SPR = REPO / registry.SPRITE_ROOT_REL
WORLD = {1080: REPO / "Captures/AcrylicV3/47_M2_Final/normal_1920x1080.png",
         1440: REPO / "Captures/AcrylicV3/47_M2_Final/normal_2560x1440.png"}
INK = (27, 22, 18)
STATES = ("planning", "queued", "reveal", "overlays")

CARDS = {
    "ATTACK": ("Attack", "Attack", "Deal 1 damage."),
    "COMBO": ("Attack", "Combo", "2 hits. 1 damage each."),
    "MISS": ("Special", "Miss", "No effect."),
    "DEFENCE": ("Defence", "Defence", "Block 1 damage."),
    "HEAL": ("Support", "Heal", "Heal 1 HP."),
    "PIERCING": ("Attack", "Piercing", "Deal 1 damage. Ignores Block."),
    "COUNTER": ("Mobility", "Counterattack", "Evade 1 attack and strike back."),
}
GLYPH = {"Attack": "Sword", "Defence": "Shield", "Mobility": "Boot", "Support": "Heart", "Special": "Star"}


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# ------------------------------------------------------------------- loading

@lru_cache(maxsize=None)
def _rgba(path: str) -> Image.Image:
    return Image.open(path).convert("RGBA")


@lru_cache(maxsize=512)
def sprite(rel: str, w: int, h: int) -> Image.Image:
    """Sprite resized (premultiplied Lanczos) to the on-screen pixel size."""
    p = Path(rel) if Path(rel).is_absolute() else SPR / rel
    if rel.startswith("Assets/"):
        p = REPO / rel
    img = _rgba(str(p))
    if img.size == (w, h):
        return img
    return img.convert("RGBa").resize((max(w, 1), max(h, 1)), Image.LANCZOS).convert("RGBA")


def tint(img: Image.Image, colour, strength=1.0) -> Image.Image:
    arr = np.asarray(img, np.float32).copy()
    c = np.array(hex_rgb(colour) if isinstance(colour, str) else colour, np.float32)
    arr[..., :3] = arr[..., :3] * (1 - strength) + arr[..., :3] * (c / 255.0) * strength
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def with_alpha(img: Image.Image, k: float) -> Image.Image:
    arr = np.asarray(img).copy()
    arr[..., 3] = (arr[..., 3].astype(np.float32) * k).astype(np.uint8)
    return Image.fromarray(arr)


@lru_cache(maxsize=None)
def font(role: str, weight: str, px: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(PT.font_path(REPO, role, weight)), px)


# ---------------------------------------------------------------- the canvas

class Screen:
    def __init__(self, height: int):
        self.s = height / 1080.0
        self.W, self.H = int(round(1920 * self.s)), height
        self.img = _rgba(str(WORLD[height])).copy()

    def rect(self, anchor, pivot, pos, size):
        """Reference-space RectTransform -> pixel box (x0, y0, w, h) with a top-left origin."""
        ax, ay = anchor[0] * 1920.0, (1.0 - anchor[1]) * 1080.0
        px, py = ax + pos[0], ay - pos[1]
        w, h = size
        x0, y0 = px - pivot[0] * w, py - (1.0 - pivot[1]) * h
        return x0 * self.s, y0 * self.s, w * self.s, h * self.s

    def paste(self, img, x0, y0):
        self.img.alpha_composite(img, (int(round(x0)), int(round(y0))))

    def put(self, rel, anchor, pos, size, pivot=(0.5, 0.5), *, tint_col=None, tint_k=1.0, alpha=1.0):
        x0, y0, w, h = self.rect(anchor, pivot, pos, size)
        img = sprite(rel, int(round(w)), int(round(h)))
        if tint_col:
            img = tint(img, tint_col, tint_k)
        if alpha < 1.0:
            img = with_alpha(img, alpha)
        self.paste(img, x0, y0)
        return x0, y0, w, h

    def text(self, t, anchor, pos, size, spec, pivot=(0.5, 0.5), **kw):
        x0, y0, w, h = self.rect(anchor, pivot, pos, size)
        draw_text(self.img, t if t is not None else spec["sample"], spec["role"], spec["weight"],
                  spec["size"] * self.s, spec.get("color", "#EADCB8"), (x0, y0, w, h), scale=self.s,
                  outline=kw.pop("outline", spec.get("outline", 2.0 if spec["role"] == "display" else 1.5)), **kw)


def text_mask(t, f, box_w, box_h, align="center", wrap=False, leading=1.08, wrap_w=None):
    lines = [t]
    if wrap:
        words, lines, cur = t.split(), [], ""
        for wd in words:
            test = (cur + " " + wd).strip()
            if f.getlength(test) <= (wrap_w or box_w) or not cur:
                cur = test
            else:
                lines.append(cur)
                cur = wd
        lines.append(cur)
    img = Image.new("L", (int(math.ceil(box_w)), int(math.ceil(box_h))), 0)
    d = ImageDraw.Draw(img)
    asc, desc = f.getmetrics()
    lh = (asc + desc) * leading
    total = lh * len(lines)
    y = box_h / 2.0 - total / 2.0 + lh / 2.0
    for ln in lines:
        if align == "center":
            d.text((box_w / 2.0, y), ln, font=f, fill=255, anchor="mm")
        else:
            d.text((0, y), ln, font=f, fill=255, anchor="lm")
        y += lh
    return np.asarray(img, np.float32) / 255.0


def draw_text(base: Image.Image, t, role, weight, px, colour, box, *, scale=1.0, outline=2.0, underlay=True,
              align="center", wrap=False, alpha=1.0, outline_colour=INK, wrap_w=None):
    x0, y0, w, h = box
    pad = int(math.ceil((outline + 4) * scale)) + 2
    f = font(role, weight, max(int(round(px)), 6))
    m = text_mask(t, f, w, h, align=align, wrap=wrap, wrap_w=wrap_w)
    m = np.pad(m, pad)
    layer = np.zeros(m.shape + (4,), np.float32)
    if outline > 0:
        r = outline * scale
        dist = ndimage.distance_transform_edt(m < 0.5)
        ol = np.clip(r + 0.5 - dist, 0.0, 1.0)
        ol = np.maximum(ol, m)
        if underlay:
            under = ndimage.gaussian_filter(ndimage.shift(ol, (1.5 * scale, 0.5 * scale), order=1), 2.2 * scale) * 0.5
            layer = _over(layer, INK, under)
        layer = _over(layer, outline_colour, ol * 0.9)
    layer = _over(layer, hex_rgb(colour) if isinstance(colour, str) else colour, m)
    layer[..., 3] *= alpha
    img = Image.fromarray(np.clip(np.round(np.dstack([layer[..., :3] / np.maximum(layer[..., 3:], 1e-6) * 255,
                                                     layer[..., 3:] * 255])), 0, 255).astype(np.uint8))
    base.alpha_composite(img, (int(round(x0)) - pad, int(round(y0)) - pad))


def _over(layer, colour, a):
    c = np.array(colour, np.float32) / 255.0
    a = np.clip(a, 0, 1)[..., None]
    out = layer.copy()
    out[..., :3] = c * a + layer[..., :3] * (1 - a)
    out[..., 3:] = a + layer[..., 3:] * (1 - a)
    return out


# ---------------------------------------------------------------------- card

def render_card(name, s, *, focus=None, scale=1.0, icon_size=150):
    """Full card (shadow + frame + icon + texts), 245x320 reference canvas with the card at (10, 10)."""
    cat, icon, effect = CARDS[name]
    k = s * scale
    W, H = int(round(245 * k)), int(round(320 * k))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def box(x, y, w, h):
        return (10 + x) * k, (10 + y) * k, w * k, h * k

    img.alpha_composite(sprite("Cards/CardShadow.png", W, H), (0, 0))
    img.alpha_composite(sprite(f"Cards/CardFrame_{cat}.png", int(round(225 * k)), int(round(300 * k))),
                        (int(round(10 * k)), int(round(10 * k))))
    if focus:  # the rim sits above the frame so its painted core lights the frame edge
        rim = tint(sprite("Cards/CardFocusRim.png", int(round(237 * k)), int(round(312 * k))), focus)
        img.alpha_composite(rim, (int(round(4 * k)), int(round(4 * k))))
    ic = int(round(icon_size * k))
    img.alpha_composite(sprite(f"Icons/{icon}Icon.png", ic, ic),
                        (int(round((10 + 112.5) * k - ic / 2)), int(round((10 + 110) * k - ic / 2))))
    gs = int(round(16 * k))
    img.alpha_composite(sprite(f"Glyphs/Glyph{GLYPH[cat]}.png", gs, gs),
                        (int(round((10 + 37) * k - gs / 2)), int(round((10 + 279) * k - gs / 2))))
    draw_text(img, name, "display", "ExtraBold", 21 * k, "#2A1E14", box(30, 14, 165, 26), scale=k, outline=0)
    draw_text(img, effect, "body", "Bold", 18 * k, "#2A1E14", box(26, 190, 173, 68), scale=k, outline=0,
              wrap=True, wrap_w=132 * k)
    draw_text(img, cat.upper(), "display", "Bold", 13 * k, "#EADCB8", box(54, 270, 145, 18), scale=k, outline=1.2,
              underlay=False)
    return img


def render_socket_card(name, s, *, scale=1.0, socket_spec=None):
    """Round 3 (new): in-socket card mode -- Cards/CardFrameSocket_<Category>.png, an enlarged icon
    and just the title (no effect box, no footer label), per layout hand.socketCard."""
    cat, icon, _effect = CARDS[name]
    k = s * scale
    W, H = int(round(245 * k)), int(round(320 * k))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sc_spec = socket_spec or {"title": {"size": 26}, "icon": {"centre": [112.5, 157], "size": [148, 148]},
                              "glyph": {"centre": [37, 281], "size": [16, 16]}}

    def box(x, y, w, h):
        return (10 + x) * k, (10 + y) * k, w * k, h * k

    img.alpha_composite(sprite("Cards/CardShadow.png", W, H), (0, 0))
    img.alpha_composite(sprite(f"Cards/CardFrameSocket_{cat}.png", int(round(225 * k)), int(round(300 * k))),
                        (int(round(10 * k)), int(round(10 * k))))
    ic_spec = sc_spec["icon"]
    ic = int(round(ic_spec["size"][0] * k))
    icx, icy = ic_spec["centre"]
    img.alpha_composite(sprite(f"Icons/{icon}Icon.png", ic, ic),
                        (int(round((10 + icx) * k - ic / 2)), int(round((10 + icy) * k - ic / 2))))
    gl_spec = sc_spec["glyph"]
    gs = int(round(gl_spec["size"][0] * k))
    gx, gy = gl_spec["centre"]
    img.alpha_composite(sprite(f"Glyphs/Glyph{GLYPH[cat]}.png", gs, gs),
                        (int(round((10 + gx) * k - gs / 2)), int(round((10 + gy) * k - gs / 2))))
    draw_text(img, name, "display", "ExtraBold", sc_spec["title"]["size"] * k, "#2A1E14", box(30, 14, 165, 26),
              scale=k, outline=0)
    return img


def place_card(screen: Screen, img, cx, cy, rot_deg):
    """cx, cy: card centre in pixels (top-left origin); rot_deg: Unity z rotation (counter-clockwise)."""
    if abs(rot_deg) > 0.01:
        img = img.rotate(rot_deg, resample=Image.BICUBIC, expand=True)
    # the canvas centre is offset from the card centre by the shadow margin (card at 10,10 in 245x320)
    screen.paste(img, cx - img.width / 2.0, cy - img.height / 2.0)


def fan(names, hover=None):
    n = len(names)
    spacing = min(185.0, 850.0 / (n - 1)) if n > 1 else 0.0
    out = []
    for i, nm in enumerate(names):
        c = i - (n - 1) / 2.0
        t = c / ((n - 1) / 2.0) if n > 1 else 0.0
        x, y, rot = c * spacing, -27.0 * t * t, -t * 8.0
        out.append((nm, x, y, rot, nm == hover))
    return out


# -------------------------------------------------------------------- scenes

def render_state(state: str, height: int, L: dict) -> Image.Image:
    sc = Screen(height)
    s = sc.s
    E = {e["id"]: e for e in L["elements"]}

    def put(id_, rel=None, **kw):
        e = E[id_]
        return sc.put(rel or e["sprite"], e["anchor"], e["pos"], e["size"], e["pivot"], **kw)

    def txt(id_, t=None, **kw):
        e = E[id_]
        sc.text(t, e["anchor"], e["pos"], e["size"], e["text"], e["pivot"], **kw)

    # ---------------------------------------------------------- world-space enemy HUD
    en = L["enemy"]
    EE = {e["id"]: e for e in en["elements"]}
    intents = [("Attack", "Attack"), ("Attack", "Attack"), ("Dodge", "Dodge")]
    for i, a in enumerate(en["mockAnchors"]):
        ax, ay = a["screen"]

        def ep(id_, rel=None, *, tint_col=None, alpha=1.0, size=None):
            e = EE[id_]
            w, h = size or e["size"]
            x0 = (ax + e["pos"][0] - w / 2.0) * s
            y0 = (ay - e["pos"][1] - h / 2.0) * s
            img = sprite(rel or e["sprite"], int(round(w * s)), int(round(h * s)))
            if tint_col:
                img = tint(img, tint_col)
            if alpha < 1:
                img = with_alpha(img, alpha)
            sc.paste(img, x0, y0)
            return x0, y0

        def et(id_, t, **kw):
            e = EE[id_]
            w, h = e["size"]
            x0 = (ax + e["pos"][0] - w / 2.0) * s
            y0 = (ay - e["pos"][1] - h / 2.0) * s
            sp = e["text"]
            draw_text(sc.img, t, sp["role"], sp["weight"], sp["size"] * s, sp["color"], (x0, y0, w * s, h * s),
                      scale=s, outline=kw.pop("outline", 2.0 if sp["role"] == "display" else 1.5), **kw)

        ep("enemy_hp_track")
        ep("enemy_hp_fill")
        hp = a["hp"]
        if state == "reveal" and i == 0:
            et("enemy_hp_text", f"{hp[0] - 1} / {hp[1]}")
        else:
            et("enemy_hp_text", f"{hp[0]} / {hp[1]}")
        et("enemy_name", a["name"])
        if state in ("planning", "queued", "overlays"):
            ep("intent_socket", EE["intent_socket"]["unknown"])
        else:
            if i == 2:
                ep("reveal_flash")
            ep("intent_socket")
            icon, label = intents[i]
            ep("intent_socket", f"Icons/Small/{icon}Icon.png", size=tuple(EE["intent_socket"]["icon_size"]))
            et("intent_label", label)
        if i == 0:
            # observed history (past turns only): eye glyph + a parchment token behind each
            # ghosted small icon (round 3, new Enemy/HistoryToken.png)
            hx = ax + EE["history"]["pos"][0] - 22
            hy = ay - EE["history"]["pos"][1]
            sc.paste(sprite("Glyphs/GlyphEye.png", int(16 * s), int(16 * s)), (hx - 8) * s, (hy - 8) * s)
            hi = EE["history"]
            isz = hi["icon_size"][0]
            tsz = hi.get("token_size", [22, 22])[0]
            for j, ic in enumerate(("Attack", "Defence")):
                tcx, tcy = hx + hi["spacing"] * (j + 1), hy
                sc.paste(sprite(hi.get("token", "Enemy/HistoryToken.png"), int(tsz * s), int(tsz * s)),
                         (tcx - tsz / 2) * s, (tcy - tsz / 2) * s)
                sc.paste(with_alpha(sprite(f"Icons/Small/{ic}Icon.png", int(isz * s), int(isz * s)),
                                    hi["icon_alpha"]), (tcx - isz / 2) * s, (tcy - isz / 2) * s)
        if i == 2:
            # status chip row (round 3 redesign): show a lone DODGE chip on one enemy so the new
            # tag design (ivory disc + pointed-end number tag) is visible in the mock
            ep("enemy_chip_block", "Crest/ChipDodge.png")
            ce = EE["enemy_chip_block"]
            cx0 = ax + ce["pos"][0] - ce["size"][0] / 2.0
            cy0 = ay - ce["pos"][1] - ce["size"][1] / 2.0
            draw_text(sc.img, "1", "body", "ExtraBold", 15 * s, "#F3E9D2",
                      ((cx0 + 24) * s, (cy0 + 6) * s, 18 * s, 16 * s), scale=s, outline=1.5)
        if state == "overlays" and i == 1:
            ep("target_marker", tint_col="#D9B56E")

    # ------------------------------------------------------------------ static HUD
    put("status_wash")
    status = {"planning": "TURN 1  ·  PLANNING", "queued": "TURN 1  ·  PLANNING",
              "reveal": "TURN 1  ·  ENEMY REVEAL", "overlays": "TURN 1  ·  TARGETING"}[state]
    txt("status_text", status)
    put("inspect_wash")
    txt("inspect_hint")
    # player crest
    e = E["portrait"]
    sc.put(e["asset"], e["anchor"], e["pos"], e["size"], e["pivot"])
    put("player_hp_track")
    put("player_hp_ghost", tint_col=E["player_hp_ghost"].get("tint"), tint_k=0.0)
    put("player_hp_fill")
    txt("player_name")
    txt("player_hp_text")
    if state == "reveal":
        put("player_chip_block")
        e = E["player_chip_block"]
        num = dict(e["text"])
        sc.text("1", e["anchor"], (e["pos"][0] + e["number"]["pos"][0], e["pos"][1] + e["number"]["pos"][1]),
                e["number"]["size"], num)
    # floor
    put("floor_plaque")
    txt("floor_text")
    txt("floor_location")
    # actions
    put("actions_medallion")
    avail = 2 if state == "planning" else 0
    txt("actions_text", f"{avail} / 2")
    e = E["actions_pips"]
    for j in range(2):
        x = e["pos"][0] + (j - 0.5) * e["spacing"]
        sc.put(e["sprite_on"] if j < avail else e["sprite_off"], e["anchor"], (x, e["pos"][1]), e["size"])
    txt("actions_label")
    # draw / discard
    put("draw_token")
    txt("draw_count", "16")
    txt("draw_label")
    put("discard_token")
    txt("discard_count", "0")
    txt("discard_label")
    # commit
    cstate = {"planning": "Disabled", "queued": "Ready", "reveal": "Resolving", "overlays": "Disabled"}[state]
    put("commit", E["commit"]["states"][cstate])
    label = {"Disabled": ("COMMIT", "#8E7A55"), "Ready": ("COMMIT", "#F3E9D2"),
             "Resolving": ("RESOLVING", "#B9A57E")}[cstate]
    ce = E["commit_label"]
    spec = dict(ce["text"], color=label[1], size=ce["text"]["size"] if cstate != "Resolving" else 21)
    sc.text(label[0], ce["anchor"], ce["pos"], ce["size"], spec, ce["pivot"])

    # ------------------------------------------------------------------ sockets
    so = L["sockets"]
    n = 2
    queued = {"queued": ["ATTACK", "DEFENCE"], "reveal": ["ATTACK", "DEFENCE"]}.get(state, [])
    for i in range(n):
        x = so["slotCenterX"] + (i - (n - 1) / 2.0) * so["slotSpacing"]
        sc.put(so["sprite"], so["anchor"], (x, so["y"]), so["size"], so["pivot"])
        rune = so["rune"]
        if state == "reveal":
            sc.put(so["glow"], so["anchor"], (x, so["y"]), so["size"], so["pivot"],
                   tint_col=so["state_tints"]["RESOLVING"])
        sc.text(["I", "II", "III", "IV"][i], so["anchor"], (x + rune["pos"][0], so["y"] + rune["pos"][1]),
                rune["box"], rune, outline=1.0, underlay=False)
        if i < len(queued):
            x0, y0, w, h = sc.rect(so["anchor"], so["pivot"], (x, so["y"]), so["size"])
            cx = x0 + w / 2 + so["queuedCardOffset"][0] * s
            cy = y0 + h / 2 - so["queuedCardOffset"][1] * s
            # round 3: in-socket card mode -- Cards/CardFrameSocket_<Category>.png with an
            # enlarged icon and just the title (see layout hand.socketCard)
            sc_spec = L["hand"].get("socketCard")
            place_card(sc, render_socket_card(queued[i], s, scale=so["queuedCardScale"], socket_spec=sc_spec),
                      cx, cy, 0.0)
            rim_col = so["state_tints"]["RESOLVING" if state == "reveal" else "QUEUED"]
            sc.put(so["rim"], so["anchor"], (x, so["y"]), so["size"], so["pivot"], tint_col=rim_col)
        elif state == "queued" and False:
            pass

    # --------------------------------------------------------------------- hand
    hd = L["hand"]
    names = {"planning": ["ATTACK", "COMBO", "MISS", "DEFENCE", "HEAL"],
             "queued": ["COMBO", "MISS", "HEAL"], "reveal": ["COMBO", "MISS", "HEAL"],
             "overlays": ["COMBO", "PIERCING", "COUNTER", "HEAL"]}[state]
    hover = {"queued": "COMBO"}.get(state)
    selected = {"overlays": "PIERCING"}.get(state)
    order = fan(names, hover)
    hx, hy = 960.0 + hd["pos"][0], 1080.0 - hd["pos"][1]
    for nm, x, y, rot, is_hover in sorted(order, key=lambda o: o[4]):
        cx, cy = (hx + x) * s, (hy - y) * s
        sc_k = 1.0
        focus = None
        if is_hover:
            cy -= hd["hoverLift"] * s
            sc_k = hd["hoverScale"]
            focus = hd["focusRim"]["tints"]["hover"]
            rot = 0.0
        if nm == selected:
            cy -= 40 * s
            focus = hd["focusRim"]["tints"]["selected"]
        place_card(sc, render_card(nm, s, focus=focus, scale=sc_k), cx, cy, rot)

    # ----------------------------------------------------------------- feedback
    if state == "reveal":
        a = L["enemy"]["mockAnchors"][0]["screen"]
        # round 3: damage glyph repainted as a slash-spark, reads at 18 px (was 30 px)
        sc.paste(sprite("Glyphs/GlyphBurst.png", int(18 * s), int(18 * s)), (a[0] - 58) * s, (a[1] - 98) * s)
        draw_text(sc.img, "-1", "display", "ExtraBold", 44 * s, "#D0574A", ((a[0] - 34) * s, (a[1] - 118) * s,
                                                                             70 * s, 56 * s), scale=s, outline=2.4)
        sc.paste(sprite("Glyphs/GlyphShield.png", int(26 * s), int(26 * s)), 548 * s, 506 * s)
        draw_text(sc.img, "BLOCK 1", "display", "ExtraBold", 26 * s, "#C9D2D4", (576 * s, 500 * s, 130 * s, 38 * s),
                  scale=s, outline=2.2, align="left")

    # ----------------------------------------------------------------- overlays
    if state == "overlays":
        e = E["targeting_banner"]
        put("targeting_banner")
        sc.text(None, e["anchor"], e["pos"], (e["size"][0] - 70, e["size"][1] - 16), e["text"], e["pivot"])
        # round 3 (new): CANCEL / SPLIT HITS as small parchment chips on the ribbon's own row,
        # just past its right end (never a second row below it)
        btn = e.get("buttons")
        if btn:
            bw, bh = btn["size"]
            gap = btn["gap"]
            bx0, by0, bw0, bh0 = sc.rect(e["anchor"], e["pivot"], e["pos"], e["size"])
            by = by0 + (bh0 - bh * s) / 2.0
            bx = bx0 + bw0 + 8 * s
            for lab in ("CANCEL", "SPLIT HITS"):
                sc.paste(sprite("Controls/SmallButton.png", int(bw * s), int(bh * s)), bx, by)
                draw_text(sc.img, lab, btn["text"]["role"], btn["text"]["weight"], btn["text"]["size"] * s,
                          btn["text"]["color"], (bx, by, bw * s, bh * s), scale=s, outline=0)
                bx += (bw + gap) * s
        # round 3 (new): the category detail frame (same hand-card recipe scaled up), using
        # layout detail.zones for placement
        dz = L["detail"]["zones"]
        x0, y0, w, h = put("detail", rel="Cards/DetailFrame_Attack.png")
        ax_ = (lambda u, v, ww, hh: (x0 + u * s, y0 + v * s, ww * s, hh * s))
        art = dz["art"]
        art_img = sprite("Cards/DetailArt_Attack.png", int(round((art[2] - art[0]) * s)),
                         int(round((art[3] - art[1]) * s * 0.5)))
        sc.paste(art_img, x0 + art[0] * s, y0 + (art[1] + (art[3] - art[1]) * 0.25) * s)
        ic = dz["icon"]
        icw = int(round(ic["size"][0] * s))
        sc.paste(sprite("Icons/PiercingIcon.png", icw, icw), x0 + ic["centre"][0] * s - icw / 2,
                 y0 + ic["centre"][1] * s - icw / 2)
        tz = dz["title"]
        draw_text(sc.img, "PIERCING ATTACK", "display", "ExtraBold", 22 * s, "#2A1E14",
                  ax_(tz[0], tz[1], tz[2] - tz[0], tz[3] - tz[1]), scale=s, outline=0)
        gz = dz["target"]
        draw_text(sc.img, "TARGET: ONE ENEMY", "body", "Bold", 16 * s, "#5A3A28",
                  ax_(gz[0] + 6, gz[1], gz[2] - gz[0] - 12, gz[3] - gz[1]), scale=s, outline=0, align="left")
        ez = dz["effect"]
        draw_text(sc.img, "Deal 1 damage to one enemy. This damage ignores Block: the target's shield is broken "
                          "before the hit lands.", "body", "Medium", 20 * s, "#2A1E14",
                  ax_(ez[0] + 12, ez[1] + 8, ez[2] - ez[0] - 24, ez[3] - ez[1] - 16), scale=s, outline=0, wrap=True)
        fz = dz["footer_label"]
        draw_text(sc.img, "ATTACK", "display", "Bold", 14 * s, "#EADCB8",
                  ax_(fz[0], fz[1], fz[2] - fz[0], fz[3] - fz[1]), scale=s, outline=1.2, underlay=False)
        fg = dz["footer_glyph"]
        gs = int(round(fg["size"][0] * s))
        sc.paste(sprite("Glyphs/GlyphSword.png", gs, gs), x0 + fg["centre"][0] * s - gs / 2,
                 y0 + fg["centre"][1] * s - gs / 2)
        cz = dz["close"]
        sc.paste(sprite("Controls/CloseButton.png", int(32 * s), int(32 * s)), x0 + cz["centre"][0] * s - 16 * s,
                 y0 + cz["centre"][1] * s - 16 * s)
    return sc.img.convert("RGB")


def render_terminal(height: int, L: dict, title="VICTORY", colour="#D9B56E") -> Image.Image:
    sc = Screen(height)
    s = sc.s
    E = {e["id"]: e for e in L["elements"]}
    v = sprite("Static/Vignette.png", sc.W, sc.H)
    sc.paste(with_alpha(v, 0.5), 0, 0)
    e = E["terminal"]
    x0, y0, w, h = sc.put(e["sprite"], e["anchor"], e["pos"], e["size"], e["pivot"])
    draw_text(sc.img, title, "display", "ExtraBold", 54 * s, colour, (x0 + 60 * s, y0 + 40 * s, 520 * s, 70 * s),
              scale=s, outline=2.6)
    draw_text(sc.img, e["body"]["sample"], "body", "Medium", 20 * s, "#EADCB8",
              (x0 + 80 * s, y0 + 118 * s, 480 * s, 52 * s), scale=s, outline=1.5)
    return sc.img.convert("RGB")


# --------------------------------------------------------------- contact sheet

# Round 3: every sprite that is new or whose painter/size changed this round (used for the
# contact_r3.png changed-sprites contact sheet).
ROUND3_CHANGED = (
    [f"Cards/CardFrameSocket_{c}.png" for c in ("Attack", "Defence", "Mobility", "Support", "Special")] +
    [f"Cards/DetailFrame_{c}.png" for c in ("Attack", "Defence", "Mobility", "Support", "Special")] +
    ["Controls/Socket.png", "Controls/SocketRim.png"] +
    [f"Controls/Commit_{s}.png" for s in
     ("Disabled", "Ready", "Hover", "Pressed", "Locked", "Resolving")] +
    ["Controls/SmallButton.png", "Crest/ChipBlock.png", "Crest/ChipDodge.png", "Crest/PlayerHPFill.png",
     "Enemy/EnemyHPTrack.png", "Enemy/EnemyHPFill.png", "Enemy/TargetMarker.png", "Enemy/HistoryToken.png",
     "Static/FloorPlaque.png", "Static/ActionMedallion.png", "Static/DrawToken.png", "Static/DiscardToken.png",
     "Static/Banner.png", "Static/TerminalBanner.png", "Glyphs/GlyphBurst.png", "Icons/MissIcon.png",
     "Icons/Small/MissIcon.png"]
)


def contact_sheet(width=2400, only=None) -> Image.Image:
    world = _rgba(str(WORLD[1080]))
    cells = []
    lab_font = font("body", "Bold", 13)
    sprites = [sp for sp in registry.SPRITES if only is None or sp.rel in only]
    for sp in sprites:
        img = _rgba(str(SPR / sp.rel))
        w, h = img.size
        k = 0.5
        if max(w, h) <= 128:
            k = 1.0
        if max(w, h) >= 1000 and "Icons" in sp.rel:
            k = 150.0 / max(w, h)
        if sp.rel.endswith("Vignette.png"):
            k = 0.5
        dw, dh = max(int(round(w * k)), 1), max(int(round(h * k)), 1)
        disp = img.convert("RGBa").resize((dw, dh), Image.LANCZOS).convert("RGBA") if k != 1.0 else img
        pad = 8
        cw, ch = dw * 2 + pad * 3, dh + pad * 2 + 18
        cell = Image.new("RGBA", (cw, ch), (46, 44, 42, 255))
        grey = Image.new("RGBA", (dw, dh), (128, 128, 128, 255))
        grey.alpha_composite(disp)
        cx0, cy0 = 960 - dw // 2, 700 - dh // 2
        wc = world.crop((max(cx0, 0), max(cy0, 0), max(cx0, 0) + dw, max(cy0, 0) + dh)).copy()
        wc.alpha_composite(disp)
        cell.alpha_composite(grey, (pad, pad))
        cell.alpha_composite(wc, (pad * 2 + dw, pad))
        ImageDraw.Draw(cell).text((pad, ch - 16), sp.rel, font=lab_font, fill=(214, 194, 154, 255))
        cells.append(cell)
    # shelf packing
    rows, cur, cur_w = [], [], 0
    for c in cells:
        if cur and cur_w + c.width > width:
            rows.append(cur)
            cur, cur_w = [], 0
        cur.append(c)
        cur_w += c.width + 4
    rows.append(cur)
    H = sum(max(c.height for c in r) + 4 for r in rows)
    sheet = Image.new("RGBA", (width, H), (30, 28, 26, 255))
    y = 0
    for r in rows:
        x = 0
        for c in r:
            sheet.alpha_composite(c, (x, y))
            x += c.width + 4
        y += max(c.height for c in r) + 4
    return sheet.convert("RGB")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--round", required=True, help="round folder name, e.g. r1")
    ap.add_argument("--states", default=",".join(STATES))
    ap.add_argument("--heights", default="1080,1440")
    ap.add_argument("--no-sheet", action="store_true")
    ap.add_argument("--changed-sheet", action="store_true",
                    help="also write contact_r3.png: a contact sheet of only the round-3 new/changed sprites")
    a = ap.parse_args(argv)
    PT.opt_out_of_power_throttling()
    L = json.loads((REPO / painter.LAYOUT_REL).read_text(encoding="utf-8"))
    out_rel = Path("Captures/CombatHUDV4/mock") / a.round
    for hgt in [int(x) for x in a.heights.split(",")]:
        for st in [x.strip() for x in a.states.split(",") if x.strip()]:
            img = render_state(st, hgt, L)
            p = painter.guard(out_rel / f"mock_{st}_{img.width}x{img.height}.png")
            p.parent.mkdir(parents=True, exist_ok=True)
            img.save(p)
            print(p.relative_to(REPO).as_posix())
        img = render_terminal(hgt, L)
        p = painter.guard(out_rel / f"mock_terminal_{img.width}x{img.height}.png")
        img.save(p)
        print(p.relative_to(REPO).as_posix())
    if not a.no_sheet:
        p = painter.guard(out_rel / "sheet.png")
        p.parent.mkdir(parents=True, exist_ok=True)
        contact_sheet().save(p)
        print(p.relative_to(REPO).as_posix())
    if a.changed_sheet:
        p = painter.guard(out_rel / "contact_r3.png")
        p.parent.mkdir(parents=True, exist_ok=True)
        contact_sheet(only=set(ROUND3_CHANGED)).save(p)
        print(p.relative_to(REPO).as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
