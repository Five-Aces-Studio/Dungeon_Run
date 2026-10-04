"""HudLayoutV4.json: the shared layout table (Unity RectTransform semantics at 1920x1080).

Each element: id, anchor [x, y] (anchorMin = anchorMax), pivot [x, y], pos [x, y]
(anchoredPosition, y up), size [w, h], optional sprite (relative to
Assets/Art/UI/CombatHUDV4) or asset (project path), optional text
{role, weight, size, color, sample}. Values are the art-spec defaults, tuned by the
mock loop (see README "Iteration log").
"""
from __future__ import annotations

VERSION = 1


def text(role, weight, size, color, sample, **kw):
    t = {"role": role, "weight": weight, "size": size, "color": color, "sample": sample}
    t.update(kw)
    return t


def el(id_, anchor, pos, size, *, pivot=(0.5, 0.5), sprite=None, asset=None, txt=None, **kw):
    e = {"id": id_, "anchor": list(anchor), "pivot": list(pivot), "pos": list(pos), "size": list(size)}
    if sprite:
        e["sprite"] = sprite
    if asset:
        e["asset"] = asset
    if txt:
        e["text"] = txt
    e.update(kw)
    return e


GOLD_LABEL = "#C49E57"
IVORY = "#EADCB8"
NUMBER = "#F3E9D2"


def build() -> dict:
    TL, TR, BL, BR, BC, TC, ML, MC = (0, 1), (1, 1), (0, 0), (1, 0), (0.5, 0), (0.5, 1), (0, 0.5), (0.5, 0.5)
    elements = [
        # player crest
        el("portrait", TL, (84, -82), (118, 118), asset="Assets/Art/UI/CombatHUDV1/Portrait.png"),
        el("player_name", TL, (148, -60), (260, 30), pivot=(0, 0.5),
           txt=text("display", "ExtraBold", 24, IVORY, "THE WAYFARER", align="left")),
        el("player_hp_track", TL, (132, -94), (250, 30), pivot=(0, 0.5), sprite="Crest/PlayerHPTrack.png",
           slice_border_px={"left": 40, "right": 40, "top": 0, "bottom": 0}),
        el("player_hp_ghost", TL, (142, -94), (232, 16), pivot=(0, 0.5), sprite="Crest/PlayerHPGhost.png",
           image="Filled Horizontal", tint="#E0A08A"),
        el("player_hp_fill", TL, (142, -94), (232, 16), pivot=(0, 0.5), sprite="Crest/PlayerHPFill.png",
           image="Filled Horizontal"),
        el("player_hp_text", TL, (257, -94), (150, 24),
           txt=text("body", "ExtraBold", 18, NUMBER, "10 / 10", outline=1.5)),
        el("player_chip_block", TL, (170, -124), (58, 28), sprite="Crest/ChipBlock.png",
           number={"pos": [12, 0], "size": [18, 14]},
           txt=text("body", "ExtraBold", 15, NUMBER, "1", outline=1.5), visible_when="block > 0"),
        el("player_chip_dodge", TL, (226, -124), (58, 28), sprite="Crest/ChipDodge.png",
           number={"pos": [12, 0], "size": [18, 14]},
           txt=text("body", "ExtraBold", 15, NUMBER, "1", outline=1.5), visible_when="dodge > 0"),
        # floor plaque (top right)
        el("floor_plaque", TR, (-146, -54), (230, 64), sprite="Static/FloorPlaque.png"),
        el("floor_text", TR, (-146, -47), (170, 32), txt=text("display", "ExtraBold", 25, IVORY, "FLOOR 01")),
        el("floor_location", TR, (-146, -69), (150, 14),
           txt=text("display", "Bold", 12, GOLD_LABEL, "TRIGONAL ABYSS")),
        # actions medallion (left middle)
        el("actions_medallion", ML, (98, 24), (132, 132), sprite="Static/ActionMedallion.png"),
        el("actions_text", ML, (98, 31), (90, 46), txt=text("display", "ExtraBold", 34, IVORY, "2 / 2")),
        el("actions_pips", ML, (98, -5), (16, 16), sprite_on="Static/PipOn.png", sprite_off="Static/PipOff.png",
           spacing=22, max_visible=6),
        el("actions_label", ML, (98, -60), (110, 18), txt=text("display", "Bold", 13, GOLD_LABEL, "ACTIONS")),
        # draw / discard tokens
        el("draw_token", BL, (98, 252), (96, 112), sprite="Static/DrawToken.png"),
        el("draw_count", BL, (98, 236), (60, 30), txt=text("body", "ExtraBold", 24, NUMBER, "16", outline=1.5)),
        el("draw_label", BL, (98, 184), (90, 18), txt=text("display", "Bold", 13, GOLD_LABEL, "DRAW")),
        el("discard_token", BR, (-98, 252), (96, 112), sprite="Static/DiscardToken.png"),
        el("discard_count", BR, (-98, 236), (60, 30), txt=text("body", "ExtraBold", 24, NUMBER, "0", outline=1.5)),
        el("discard_label", BR, (-98, 184), (90, 18), txt=text("display", "Bold", 13, GOLD_LABEL, "DISCARD")),
        # commit (round 3: 10% larger, 236x76 -> 260x84)
        el("commit", BR, (-176, 122), (260, 84), sprite="Controls/Commit_Ready.png",
           states={s: f"Controls/Commit_{s}.png" for s in
                   ("Disabled", "Ready", "Hover", "Pressed", "Locked", "Resolving")}),
        el("commit_label", BR, (-176, 116), (150, 36), txt=text("display", "ExtraBold", 26, IVORY, "COMMIT")),
        # status + hints
        el("status_wash", TC, (0, -112), (460, 40), sprite="Static/TextWash.png"),
        el("status_text", TC, (0, -112), (440, 28),
           txt=text("display", "Bold", 17, IVORY, "TURN 1  ·  PLANNING")),
        el("inspect_wash", BC, (0, 12), (340, 28), pivot=(0.5, 0), sprite="Static/TextWash.png"),
        el("inspect_hint", BC, (0, 12), (320, 26), pivot=(0.5, 0),
           txt=text("body", "Medium", 14, "#D6C29A", "Middle-click a card: inspect")),
        # targeting ribbon (round 3: ivory parchment, 600x76 -> 440x46, ink text)
        el("targeting_banner", TC, (0, -196), (440, 46), sprite="Static/Banner.png",
           slice_border_px={"left": 44, "right": 44, "top": 0, "bottom": 0},
           txt=text("display", "Bold", 18, "#2A1E14", "CHOOSE A TARGET"),
           buttons={"size": [110, 30], "gap": 8, "text": text("display", "Bold", 14, "#2A1E14", "CANCEL")}),
        el("detail", TL, (196, -146), (360, 480), pivot=(0, 1), sprite="Cards/DetailFrame.png"),
        # terminal banner (round 3: 640x190 -> 560x166, raised to clear the enemy HUD row)
        el("terminal", MC, (0, 210), (560, 166), sprite="Static/TerminalBanner.png",
           title=text("display", "ExtraBold", 54, "#D9B56E", "VICTORY", defeat_color="#A8302A"),
           body=text("body", "Medium", 20, IVORY, "The abyss falls silent.")),
        el("vignette", MC, (0, 0), (1920, 1080), sprite="Static/Vignette.png", stretch=True, alpha=0.35),
    ]
    sockets = {
        "anchor": [0.5, 0], "pivot": [0.5, 0.5], "y": 440, "size": [118, 158],
        "slotCenterX": -127.5, "slotSpacing": 150, "sprite": "Controls/Socket.png",
        "rim": "Controls/SocketRim.png", "glow": "Controls/SocketGlow.png",
        "queuedCardScale": 0.5, "queuedCardOffset": [0, 6],
        # Round 3: the numeral tab is now parchment, so the rune is drawn in ink (like the card title).
        # Round 3 (runtime pass): invalid hover/drop is a warm desaturated red (#C8553D), away from the oxblood ATTACK
        # frame; focus tints carry alpha (#RRGGBBAA). history icon_alpha is the OLDER-entries alpha (newest is full).
        "rune": text("display", "ExtraBold", 15, "#2A1E14", "I", pos=[0, -63], box=[38, 16]),
        "state_tints": {"ARMED": "#D9B56E", "HOVER_VALID": "#E8C27A", "HOVER_INVALID": "#C8553D",
                        "QUEUED": "#A9864C", "RESOLVING": "#E09A3A", "VALID_GLOW": "#6E9A94"},
    }
    hand = {"anchor": [0.5, 0], "pos": [0, 200], "fanWidth": 850, "spacing": 185, "curve": 27, "maxRotation": 8,
            "card": [225, 300], "hoverLift": 70, "hoverScale": 1.075,
            "cardText": {
                "title": text("display", "ExtraBold", 21, "#2A1E14", "ATTACK", zone=[30, 14, 195, 40]),
                "effect": text("body", "Bold", 18, "#2A1E14", "Deal 1 damage.", zone=[26, 190, 199, 258],
                               wrapWidth=132),
                "label": text("display", "Bold", 13, "#EADCB8", "STRIKE", zone=[54, 270, 199, 288]),
                # Round 3: icon enlarged 118x118 -> ~150x150 (~75% of the window width), centre kept.
                "icon": {"centre": [112.5, 110], "size": [150, 150]},
                "glyph": {"centre": [37, 279], "size": [16, 16]},
            },
            # Round 3 (new): in-socket card mode -- no effect box/footer label, just an enlarged
            # icon and a title, painted on Cards/CardFrameSocket_<Category>.png.
            "socketCard": {
                # Round 4: taller ribbon (cards.SOCKET_RIBBON); `size` is the title auto-size ceiling.
                "title": text("display", "ExtraBold", 34, "#2A1E14", "ATTACK", zone=[26, 12, 199, 55]),
                "icon": {"centre": [112.5, 164], "size": [140, 140]},
                "glyph": {"centre": [37, 281], "size": [16, 16]},
            },
            "shadow": {"sprite": "Cards/CardShadow.png", "size": [245, 320]},
            "focusRim": {"sprite": "Cards/CardFocusRim.png", "size": [237, 312], "order": "above_frame",
                         "tints": {"hover": "#E8C27A", "selected": "#D9B56E", "valid": "#6E9A94E6",
                                   "invalid": "#C8553DF2"}}}
    enemy = {
        "root": [170, 80],
        "elements": [
            # Round 3: track 132x16 -> 136x20, fill 124x8 -> 128x12, hp text 13 -> 14.
            el("enemy_hp_track", MC, (6, 0), (136, 20), sprite="Enemy/EnemyHPTrack.png"),
            el("enemy_hp_fill", MC, (6, 0), (128, 12), sprite="Enemy/EnemyHPFill.png", image="Filled Horizontal"),
            el("enemy_hp_text", MC, (6, 0), (120, 16), txt=text("body", "ExtraBold", 14, NUMBER, "6 / 6",
                                                               outline=1.5)),
            # Round 1: 34 px icons on a 44 px socket were illegible at 1080; the revealed face is now parchment.
            el("intent_socket", MC, (-92, 6), (52, 52), sprite="Enemy/IntentSocket.png",
               unknown="Enemy/IntentUnknown.png", icon_size=[40, 40]),
            el("reveal_flash", MC, (-92, 6), (76, 76), sprite="Enemy/RevealFlash.png"),
            el("intent_label", MC, (6, 22), (150, 22), txt=text("display", "Bold", 15, "#E8C27A", "Attack")),
            el("enemy_name", MC, (6, -19), (160, 20), txt=text("display", "Bold", 14, "#D6C29A", "Abyss Warden")),
            # Round 3 (new token): a parchment disc behind the small ghosted icon per history entry.
            el("history", MC, (6, -38), (70, 18), glyph="Glyphs/GlyphEye.png", glyph_size=[16, 16],
               token="Enemy/HistoryToken.png", token_size=[22, 22], icon_size=[16, 16], icon_alpha=0.6,
               spacing=25),
            # Chips sit on the HP row, right of the track; a lone DODGE chip packs into this slot at runtime.
            el("enemy_chip_block", MC, (106, 0), (58, 28), sprite="Crest/ChipBlock.png"),
            # Round 3: painted antique-gold trigon, 28x18 -> 24x20, raised to clear the intent label.
            # Round 4: 24x20 -> 36x30 and lifted; painted gold is shown untinted (see style targetMarkerValid).
            el("target_marker", MC, (6, 52), (36, 30), sprite="Enemy/TargetMarker.png"),
        ],
        "mockAnchors": [
            {"name": "Abyss Warden", "screen": [1155, 500], "hp": [6, 6]},
            {"name": "Ash Hound", "screen": [1354, 560], "hp": [4, 4]},
            {"name": "Sentinel", "screen": [1555, 508], "hp": [7, 7]},
        ],
    }
    # Round 3 (new): per-category detail frame zones, in 360x480 display units, top-left origin.
    # `Cards/DetailFrame_<Category>.png` reuses the hand-card frame recipe scaled 1.6x; the old
    # `Cards/DetailFrame.png` (see the "detail" element above) stays as a category-agnostic fallback.
    detail = {
        "zones": {
            "title": [48, 22, 312, 64],
            "art": [32, 61, 328, 282],
            "icon": {"centre": [180, 179], "size": [200, 200]},
            "target": [30, 288, 330, 308],
            "effect": [30, 312, 330, 442],
            "footer_label": [100, 448, 300, 466],
            "footer_glyph": {"centre": [44, 458], "size": [24, 24]},
            "close": {"centre": [338, 8]},
        },
    }
    return {"version": VERSION, "reference": [1920, 1080], "canvasScaler": {"match": 0.5},
            "spriteRoot": "Assets/Art/UI/CombatHUDV4", "spriteScale": 2,
            "fonts": {"display": "Assets/Art/UI/CombatHUDV4/Fonts/AlegreyaSCDR-{weight}.ttf",
                      "body": "Assets/Art/UI/CombatHUDV4/Fonts/AlegreyaSansDR-{weight}.ttf"},
            "elements": elements, "sockets": sockets, "hand": hand, "enemy": enemy, "detail": detail}
