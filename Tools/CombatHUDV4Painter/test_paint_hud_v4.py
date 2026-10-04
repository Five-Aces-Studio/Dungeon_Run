"""unittest suite for the Combat HUD V4 painter.

Run from the repo root:
    python -m unittest discover -s Tools/CombatHUDV4Painter -p "test_*.py" -v

The suite paints the full set twice into two temporary output roots (never into the
project) and checks determinism, the exact sprite list and sizes, silhouettes,
text-safe zones, glyph/icon margins, the layout JSON and the path guard.
"""
from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import paint_hud_v4 as painter  # noqa: E402
import registry  # noqa: E402

CATS = ("Attack", "Defence", "Mobility", "Support", "Special")
ICONS = ("Attack", "Defence", "Dodge", "Heal", "Piercing", "Combo", "Counterattack", "Charge", "Miss")
GLYPHS = ("Sword", "Shield", "Boot", "Heart", "Star", "Pierce", "Clock", "Burst", "Eye", "Question")

# The art spec's sprite list (reference size -> PNG is 2x), kept independent of the registry.
EXPECTED = {}
EXPECTED.update({f"Cards/CardFrame_{c}.png": (450, 600) for c in CATS})
# Round 3 (new): socket card frames, same size/categories as the hand frame, 1:1 swappable.
EXPECTED.update({f"Cards/CardFrameSocket_{c}.png": (450, 600) for c in CATS})
# Round 3 (new): per-category detail frames (the hand-card recipe scaled 1.6x).
EXPECTED.update({f"Cards/DetailFrame_{c}.png": (720, 960) for c in CATS})
EXPECTED.update({f"Cards/DetailArt_{c}.png": (600, 292) for c in CATS})
EXPECTED.update({
    "Cards/CardShadow.png": (490, 640), "Cards/CardFocusRim.png": (474, 624), "Cards/DetailFrame.png": (720, 960),
    "Controls/Socket.png": (236, 316), "Controls/SocketRim.png": (236, 316), "Controls/SocketGlow.png": (236, 316),
    # Round 3: small button repainted as a parchment chip, 172x34 -> 110x30 (220x60 sprite).
    "Controls/SmallButton.png": (220, 60), "Controls/CloseButton.png": (64, 64),
    "Crest/PlayerHPTrack.png": (500, 60), "Crest/PlayerHPFill.png": (464, 32), "Crest/PlayerHPGhost.png": (464, 32),
    # Round 3: status chip redesigned, 52x26 -> 58x28 (116x56 sprite).
    "Crest/ChipBlock.png": (116, 56), "Crest/ChipDodge.png": (116, 56),
    # Round 3: enemy HP track 132x16 -> 136x20, fill 124x8 -> 128x12.
    "Enemy/EnemyHPTrack.png": (272, 40), "Enemy/EnemyHPFill.png": (256, 24), "Enemy/IntentSocket.png": (88, 88),
    "Enemy/IntentUnknown.png": (88, 88), "Enemy/RevealFlash.png": (128, 128),
    # Round 3: target marker 28x18 -> 24x20 (48x40 sprite).
    "Enemy/TargetMarker.png": (72, 60),  # round 4: 24x20 -> 36x30 display
    # Round 3 (new): observed-history token, 22x22 display (44x44 sprite).
    "Enemy/HistoryToken.png": (44, 44),
    "Static/FloorPlaque.png": (460, 128), "Static/ActionMedallion.png": (264, 264), "Static/PipOn.png": (32, 32),
    "Static/PipOff.png": (32, 32), "Static/DrawToken.png": (192, 224), "Static/DiscardToken.png": (192, 224),
    # Round 3: targeting ribbon 600x76 -> 440x46 (880x92 sprite).
    "Static/Banner.png": (880, 92),
    # Round 3: terminal banner 640x190 -> 560x166 (1120x332 sprite).
    "Static/TerminalBanner.png": (1120, 332), "Static/Vignette.png": (256, 256),
    "Static/TextWash.png": (512, 96),
})
EXPECTED.update({f"Controls/Commit_{s}.png": (520, 168)  # Round 3: 10% larger (236x76 -> 260x84).
                 for s in ("Disabled", "Ready", "Hover", "Pressed", "Locked", "Resolving")})
EXPECTED.update({f"Glyphs/Glyph{g}.png": (48, 48) for g in GLYPHS})
EXPECTED.update({f"Icons/{i}Icon.png": (1254, 1254) for i in ICONS})
EXPECTED.update({f"Icons/Small/{i}Icon.png": (96, 96) for i in ICONS})


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rgba(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), np.float64)


class PainterTest(unittest.TestCase):
    tmp_a = tmp_b = None

    @classmethod
    def setUpClass(cls):
        cls._td_a = tempfile.TemporaryDirectory(prefix="hudv4_a_")
        cls._td_b = tempfile.TemporaryDirectory(prefix="hudv4_b_")
        cls.tmp_a, cls.tmp_b = Path(cls._td_a.name), Path(cls._td_b.name)
        cls.files_a = painter.paint_all(out_root=cls.tmp_a, verbose=False)
        cls.files_b = painter.paint_all(out_root=cls.tmp_b, verbose=False)

    @classmethod
    def tearDownClass(cls):
        cls._td_a.cleanup()
        cls._td_b.cleanup()

    def sprite_path(self, rel: str, root: Path | None = None) -> Path:
        return (root or self.tmp_a) / registry.SPRITE_ROOT_REL / rel

    # ------------------------------------------------------------ determinism
    def test_deterministic_byte_identical(self):
        rel_a = sorted(p.relative_to(self.tmp_a).as_posix() for p in self.files_a)
        rel_b = sorted(p.relative_to(self.tmp_b).as_posix() for p in self.files_b)
        self.assertEqual(rel_a, rel_b)
        for rel in rel_a:
            with self.subTest(rel=rel):
                self.assertEqual(sha(self.tmp_a / rel), sha(self.tmp_b / rel))

    # ---------------------------------------------------------- list and size
    def test_registry_matches_spec_list(self):
        self.assertEqual({s.rel: tuple(s.size) for s in registry.SPRITES}, EXPECTED)

    def test_every_sprite_exists_with_exact_size(self):
        for rel, size in EXPECTED.items():
            with self.subTest(rel=rel):
                p = self.sprite_path(rel)
                self.assertTrue(p.is_file(), rel)
                with Image.open(p) as im:
                    self.assertEqual(im.size, size)
                    self.assertEqual(im.mode, "RGBA")

    # ------------------------------------------------------------ silhouettes
    def test_corners_centres_and_alpha_limits(self):
        for sp in registry.SPRITES:
            with self.subTest(rel=sp.rel):
                a = rgba(self.sprite_path(sp.rel))[..., 3]
                h, w = a.shape
                if sp.corners_clear:
                    for y, x in ((0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)):
                        self.assertEqual(a[y, x], 0, f"corner {(x, y)}")
                for (x, y) in sp.opaque:
                    self.assertEqual(a[int(y * 2), int(x * 2)], 255, f"opaque probe {(x, y)}")
                for (x, y) in sp.clear:
                    self.assertEqual(a[int(y * 2), int(x * 2)], 0, f"clear probe {(x, y)}")
                if sp.max_alpha is not None:
                    self.assertLessEqual(a.max(), sp.max_alpha * 255 + 1)

    def test_vignette_is_full_bleed(self):
        a = rgba(self.sprite_path("Static/Vignette.png"))[..., 3]
        self.assertEqual(a[128, 128], 0)
        self.assertGreater(a[0, 0], 0.6 * 255)

    # -------------------------------------------------------------- text zones
    def test_text_safe_zones_are_clean_and_opaque(self):
        for sp in registry.SPRITES:
            for z in sp.zones:
                with self.subTest(rel=sp.rel, zone=z):
                    arr = rgba(self.sprite_path(sp.rel))
                    x0, y0, x1, y1 = (int(round(v * 2)) for v in z)
                    crop = arr[y0:y1, x0:x1]
                    self.assertGreater(crop.size, 0)
                    lum = crop[..., 0] * 0.2126 + crop[..., 1] * 0.7152 + crop[..., 2] * 0.0722
                    self.assertLess(float(lum.std()), 3.0)
                    self.assertEqual(float(crop[..., 3].mean()), 255.0)

    # ---------------------------------------------------------- round 3 additions
    def test_socket_card_frames_text_zone_clean(self):
        """The new Cards/CardFrameSocket_<Category>.png keeps only the title text zone."""
        for cat in CATS:
            with self.subTest(cat=cat):
                sp = next(s for s in registry.SPRITES if s.rel == f"Cards/CardFrameSocket_{cat}.png")
                self.assertEqual(len(sp.zones), 1)
                arr = rgba(self.sprite_path(sp.rel))
                x0, y0, x1, y1 = (int(round(v * 2)) for v in sp.zones[0])
                crop = arr[y0:y1, x0:x1]
                lum = crop[..., 0] * 0.2126 + crop[..., 1] * 0.7152 + crop[..., 2] * 0.0722
                self.assertLess(float(lum.std()), 3.0)
                self.assertEqual(float(crop[..., 3].mean()), 255.0)

    def test_detail_frame_category_text_zones_clean(self):
        """The new Cards/DetailFrame_<Category>.png keeps title/target/effect/footer_label clean."""
        for cat in CATS:
            with self.subTest(cat=cat):
                sp = next(s for s in registry.SPRITES if s.rel == f"Cards/DetailFrame_{cat}.png")
                self.assertEqual(len(sp.zones), 4)
                arr = rgba(self.sprite_path(sp.rel))
                for z in sp.zones:
                    x0, y0, x1, y1 = (int(round(v * 2)) for v in z)
                    crop = arr[y0:y1, x0:x1]
                    self.assertGreater(crop.size, 0)
                    lum = crop[..., 0] * 0.2126 + crop[..., 1] * 0.7152 + crop[..., 2] * 0.0722
                    self.assertLess(float(lum.std()), 3.0)
                    self.assertEqual(float(crop[..., 3].mean()), 255.0)

    def test_socket_card_and_detail_frame_deterministic(self):
        """Extra explicit determinism check for the round-3 additions (also covered generically
        by test_deterministic_byte_identical, which iterates every registered sprite)."""
        for rel in [f"Cards/CardFrameSocket_{c}.png" for c in CATS] + [f"Cards/DetailFrame_{c}.png" for c in CATS]:
            with self.subTest(rel=rel):
                self.assertEqual(sha(self.sprite_path(rel, self.tmp_a)), sha(self.sprite_path(rel, self.tmp_b)))

    def test_layout_has_round3_keys(self):
        p = self.tmp_a / painter.LAYOUT_REL
        data = json.loads(p.read_text(encoding="utf-8"))
        self.assertIn("socketCard", data["hand"])
        for key in ("title", "icon", "glyph"):
            self.assertIn(key, data["hand"]["socketCard"])
        self.assertIn("detail", data)
        for key in ("title", "art", "icon", "target", "effect", "footer_label", "footer_glyph", "close"):
            self.assertIn(key, data["detail"]["zones"])
        self.assertIn("buttons", data["elements"][[e["id"] for e in data["elements"]].index("targeting_banner")])
        hist = data["enemy"]["elements"][[e["id"] for e in data["enemy"]["elements"]].index("history")]
        self.assertIn("token", hist)
        self.assertIn("token_size", hist)

    # ---------------------------------------------------------- glyphs, icons
    def test_glyphs_and_icons_nonempty_with_margin(self):
        for sp in registry.SPRITES:
            if not sp.fit_margin:
                continue
            with self.subTest(rel=sp.rel):
                a = rgba(self.sprite_path(sp.rel))[..., 3]
                ys, xs = np.nonzero(a > 0)
                self.assertGreater(len(ys), a.size * 0.05, "nearly empty")
                m = sp.fit_margin
                self.assertGreaterEqual(xs.min(), m)
                self.assertGreaterEqual(ys.min(), m)
                self.assertLessEqual(xs.max(), a.shape[1] - 1 - m)
                self.assertLessEqual(ys.max(), a.shape[0] - 1 - m)

    def test_no_pure_black_or_flat_white_masses(self):
        for sp in registry.SPRITES:
            with self.subTest(rel=sp.rel):
                arr = rgba(self.sprite_path(sp.rel))
                solid = arr[..., 3] > 200
                if not solid.any():
                    continue
                rgb = arr[..., :3][solid]
                black = np.all(rgb < 6, axis=1).mean()
                white = np.all(rgb > 250, axis=1).mean()
                self.assertLess(black, 0.01)
                self.assertLess(white, 0.01)

    # -------------------------------------------------------------- layout
    def test_layout_json_references_existing_sprites(self):
        p = self.tmp_a / painter.LAYOUT_REL
        data = json.loads(p.read_text(encoding="utf-8"))
        self.assertEqual(data["reference"], [1920, 1080])
        ids = {e["id"] for e in data["elements"]}
        for need in ("portrait", "player_hp_track", "floor_plaque", "actions_medallion", "draw_token",
                     "discard_token", "commit", "status_text", "targeting_banner", "detail", "terminal"):
            self.assertIn(need, ids)
        refs = [e["sprite"] for e in data["elements"] if "sprite" in e]
        refs += [e["sprite"] for e in data["enemy"]["elements"] if "sprite" in e]
        refs += [data["sockets"]["sprite"], data["sockets"]["rim"], data["sockets"]["glow"]]
        for r in refs:
            with self.subTest(sprite=r):
                self.assertIn(r, EXPECTED)

    # ------------------------------------------------------------ path guard
    def test_path_guard(self):
        root = self.tmp_a
        ok = ["Assets/Art/UI/CombatHUDV4/Cards/x.png", "Assets/Settings/CombatHUDV4/x.json",
              "Captures/CombatHUDV4/mock/r1/x.png"]
        for rel in ok:
            self.assertEqual(painter.guard(rel, root), (root / rel).resolve())
        bad = ["Assets/Art/UI/CombatHUDV3/AttackIcon.png", "Assets/Art/UI/CombatHUDV4/Fonts/x.ttf",
               "Assets/Art/UI/CombatHUDV4/../CombatHUDV3/x.png", "Assets/Scenes/x.unity", "Tools/x.png",
               "Assets/Art/Rendering/AcrylicV3/T_DungeonRun_PaintStrokes.png", str(Path(root).parent / "x.png")]
        for rel in bad:
            with self.subTest(path=rel):
                with self.assertRaises(painter.PathGuardError):
                    painter.guard(rel, root)
        with self.assertRaises(painter.PathGuardError):
            painter.save_png(painter.PT.Layer(4, 4), "Assets/Art/UI/CombatHUDV3/x.png", root)


if __name__ == "__main__":
    unittest.main()
