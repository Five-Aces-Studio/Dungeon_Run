"""The complete V4 sprite list: output path, 2x pixel size, painter and test metadata.

Zones and probe points are in 1080-reference units relative to the sprite's
top-left corner (the PNG is 2x). Tests read the same metadata, so the list here is
the single source of truth for names, sizes and text-safe zones.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Callable

import cards
import glyphs
import hud
import icons
import palette as P

SPRITE_ROOT_REL = "Assets/Art/UI/CombatHUDV4"


@dataclass(frozen=True)
class Sprite:
    rel: str                     # path relative to Assets/Art/UI/CombatHUDV4
    size: tuple                  # (w, h) output pixels
    group: str
    fn: Callable
    zones: tuple = ()            # text-safe zones (reference units)
    opaque: tuple = ()           # points that must be alpha 255 (reference units)
    clear: tuple = ()            # points that must be alpha 0 (reference units), besides corners
    corners_clear: bool = True
    max_alpha: float | None = None
    fit_margin: int = 0          # >0: the painted content must keep this pixel margin to the canvas edge
    notes: str = ""
    extra: dict = field(default_factory=dict)


def _z(zs):
    return tuple(tuple(z) for z in zs)


def build() -> list:
    s: list = []
    # ---------------------------------------------------------------- cards
    for cat in P.CATEGORIES:
        s.append(Sprite(f"Cards/CardFrame_{cat}.png", (450, 600), "cards",
                        lambda ctx, c=cat: cards.paint_card_frame(ctx, c),
                        zones=_z(cards.CARD_ZONES.values()), opaque=((112, 112), (112, 222), (60, 150))))
    # Round 3 (new): socket variant of the hand frame, same names/categories/size, 1:1 swappable.
    for cat in P.CATEGORIES:
        s.append(Sprite(f"Cards/CardFrameSocket_{cat}.png", (450, 600), "cards",
                        lambda ctx, c=cat: cards.paint_card_frame_socket(ctx, c),
                        zones=_z([cards.SOCKET_TITLE_ZONE]), opaque=((112, 112), (112, 222), (60, 150))))
    s.append(Sprite("Cards/CardShadow.png", (490, 640), "cards", cards.paint_card_shadow, max_alpha=0.5))
    s.append(Sprite("Cards/CardFocusRim.png", (474, 624), "cards", cards.paint_card_focus_rim,
                    clear=((118, 156),)))
    s.append(Sprite("Cards/DetailFrame.png", (720, 960), "cards", cards.paint_detail_frame,
                    zones=_z(cards.DETAIL_ZONES.values()), opaque=((180, 140), (180, 330))))
    # Round 3 (new): per-category detail frame, same hand-card recipe scaled 1.6x. The old
    # category-agnostic DetailFrame.png above is kept as a fallback.
    for cat in P.CATEGORIES:
        s.append(Sprite(f"Cards/DetailFrame_{cat}.png", (720, 960), "cards",
                        lambda ctx, c=cat: cards.paint_detail_frame_category(ctx, c),
                        zones=_z(cards.DETAIL_CAT_ZONES.values()), opaque=((180, 140), (180, 330))))
    for cat in P.CATEGORIES:
        s.append(Sprite(f"Cards/DetailArt_{cat}.png", (600, 292), "cards",
                        lambda ctx, c=cat: cards.paint_detail_art(ctx, c), opaque=((150, 90),)))
    # ------------------------------------------------------------- controls
    s.append(Sprite("Controls/Socket.png", (236, 316), "controls", hud.paint_socket,
                    zones=_z(hud.ZONES["Controls/Socket.png"]), opaque=((59, 79),)))
    s.append(Sprite("Controls/SocketRim.png", (236, 316), "controls", hud.paint_socket_rim, clear=((59, 79),)))
    s.append(Sprite("Controls/SocketGlow.png", (236, 316), "controls", hud.paint_socket_glow, max_alpha=0.45))
    for st in hud.COMMIT_STATES:
        # Round 3: 10% larger (236x76 -> 260x84, sprite 520x168).
        s.append(Sprite(f"Controls/Commit_{st}.png", (520, 168), "controls",
                        lambda ctx, t=st: hud.paint_commit(ctx, t),
                        zones=_z(hud.ZONES["Controls/Commit"]), opaque=((130, 44),)))
    # Round 3: repainted as a small bronze-rimmed parchment chip, about 110x30 (sprite 220x60).
    s.append(Sprite("Controls/SmallButton.png", (220, 60), "controls", hud.paint_small_button,
                    zones=_z(hud.ZONES["Controls/SmallButton.png"]), opaque=((55, 15),)))
    s.append(Sprite("Controls/CloseButton.png", (64, 64), "controls", hud.paint_close_button, opaque=((16, 16),)))
    # ---------------------------------------------------------------- crest
    s.append(Sprite("Crest/PlayerHPTrack.png", (500, 60), "crest", hud.paint_player_hp_track,
                    opaque=((4, 15), (126, 15))))
    s.append(Sprite("Crest/PlayerHPFill.png", (464, 32), "crest", hud.paint_player_hp_fill, opaque=((116, 8),)))
    s.append(Sprite("Crest/PlayerHPGhost.png", (464, 32), "crest", hud.paint_player_hp_ghost, opaque=((116, 8),)))
    # Round 3: status chip redesigned as a tag (ivory disc + a dark pointed-end number tag),
    # display ~58x28 (sprite 116x56).
    s.append(Sprite("Crest/ChipBlock.png", (116, 56), "crest", lambda ctx: hud.paint_chip(ctx, "Shield"),
                    zones=_z(hud.ZONES["Crest/Chip"]), opaque=((14, 14), (41, 14))))
    s.append(Sprite("Crest/ChipDodge.png", (116, 56), "crest", lambda ctx: hud.paint_chip(ctx, "Boot"),
                    zones=_z(hud.ZONES["Crest/Chip"]), opaque=((14, 14), (41, 14))))
    # ---------------------------------------------------------------- enemy
    # Round 3: track 132x16 -> 136x20, fill 124x8 -> 128x12.
    s.append(Sprite("Enemy/EnemyHPTrack.png", (272, 40), "enemy", hud.paint_enemy_hp_track, opaque=((68, 10),)))
    s.append(Sprite("Enemy/EnemyHPFill.png", (256, 24), "enemy", hud.paint_enemy_hp_fill, opaque=((64, 6),)))
    s.append(Sprite("Enemy/IntentSocket.png", (88, 88), "enemy", hud.paint_intent_socket, opaque=((22, 22),)))
    s.append(Sprite("Enemy/IntentUnknown.png", (88, 88), "enemy", hud.paint_intent_unknown, opaque=((22, 22),)))
    s.append(Sprite("Enemy/RevealFlash.png", (128, 128), "enemy", hud.paint_reveal_flash, max_alpha=0.6))
    # Round 3: painted antique-gold down-pointing trigon, display 24x20 (sprite 48x40).
    s.append(Sprite("Enemy/TargetMarker.png", (72, 60), "enemy", hud.paint_target_marker, opaque=((18, 18),)))
    # Round 3 (new): observed-history token, display 22x22 (sprite 44x44).
    s.append(Sprite("Enemy/HistoryToken.png", (44, 44), "enemy", hud.paint_history_token, opaque=((11, 11),)))
    # --------------------------------------------------------------- static
    s.append(Sprite("Static/FloorPlaque.png", (460, 128), "static", hud.paint_floor_plaque,
                    zones=_z(hud.ZONES["Static/FloorPlaque.png"]), opaque=((115, 30),)))
    s.append(Sprite("Static/ActionMedallion.png", (264, 264), "static", hud.paint_action_medallion,
                    zones=_z(hud.ZONES["Static/ActionMedallion.png"]), opaque=((66, 66),)))
    s.append(Sprite("Static/PipOn.png", (32, 32), "static", lambda ctx: hud.paint_pip(ctx, True), opaque=((8, 8),)))
    s.append(Sprite("Static/PipOff.png", (32, 32), "static", lambda ctx: hud.paint_pip(ctx, False),
                    opaque=((8, 8),)))
    s.append(Sprite("Static/DrawToken.png", (192, 224), "static", lambda ctx: hud.paint_token(ctx, "Draw"),
                    zones=_z(hud.ZONES["Static/Token"]), opaque=((48, 74), (48, 30))))
    s.append(Sprite("Static/DiscardToken.png", (192, 224), "static", lambda ctx: hud.paint_token(ctx, "Discard"),
                    zones=_z(hud.ZONES["Static/Token"]), opaque=((48, 74), (48, 40))))
    # Round 3: ivory parchment ribbon, target display 440x46 (sprite 880x92).
    s.append(Sprite("Static/Banner.png", (880, 92), "static", hud.paint_banner,
                    zones=_z(hud.ZONES["Static/Banner.png"]), opaque=((220, 23),),
                    extra={"slice_border_px": {"left": 44, "right": 44, "top": 0, "bottom": 0}}))
    # Round 3: display 640x190 -> 560x166 (sprite 1120x332).
    s.append(Sprite("Static/TerminalBanner.png", (1120, 332), "static", hud.paint_terminal_banner,
                    zones=_z(hud.ZONES["Static/TerminalBanner.png"]), opaque=((280, 88),)))
    s.append(Sprite("Static/Vignette.png", (256, 256), "static", hud.paint_vignette, corners_clear=False,
                    clear=((64, 64),), max_alpha=0.7))
    s.append(Sprite("Static/TextWash.png", (512, 96), "static", hud.paint_text_wash, max_alpha=0.55))
    # --------------------------------------------------------------- glyphs
    for name in glyphs.NAMES:
        s.append(Sprite(f"Glyphs/Glyph{name}.png", (48, 48), "glyphs",
                        lambda ctx, n=name: glyphs.paint_glyph(ctx, n), fit_margin=2))
    # ---------------------------------------------------------------- icons
    for name in icons.ALL_NAMES:
        s.append(Sprite(f"Icons/{name}Icon.png", (icons.SIZE, icons.SIZE), "icons",
                        lambda ctx, n=name: icons.paint_icon(ctx, n), fit_margin=2))
    for name in icons.ALL_NAMES:
        s.append(Sprite(f"Icons/Small/{name}Icon.png", (96, 96), "icons",
                        lambda ctx, n=name: icons.paint_small_icon(ctx, n), fit_margin=2))
    return s


SPRITES = build()
GROUPS = ("cards", "controls", "crest", "enemy", "static", "glyphs", "icons")
