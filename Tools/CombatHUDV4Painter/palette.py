"""Combat HUD V4 palette (sRGB, float 0..1) and small colour helpers.

Base values come from the V4 art spec. Painters vary them locally (warm/cool
shifts, value steps) but never introduce pure black, flat white or neon.
"""
from __future__ import annotations

import numpy as np


def hex_rgb(value: str) -> np.ndarray:
    """'#RRGGBB' -> float32 array (3,) in 0..1."""
    value = value.lstrip("#")
    return np.array([int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


# Ink and neutrals
INK = hex_rgb("#1B1612")
SHADOW_WARM = hex_rgb("#1E140D")

# Charcoal-teal stone
CHAR_DEEP = hex_rgb("#141C20")
CHAR = hex_rgb("#1F2B30")
CHAR_LIGHT = hex_rgb("#2E3D42")
TEAL = hex_rgb("#3E6664")
TEAL_LIGHT = hex_rgb("#6E9A94")

# Bronze
BRONZE_DARK = hex_rgb("#4A361E")
BRONZE = hex_rgb("#7A5A32")
BRONZE_LIGHT = hex_rgb("#A9864C")
GOLD = hex_rgb("#D9B56E")

# Parchment / ivory
PARCH_SHADOW = hex_rgb("#B49B6E")
PARCH = hex_rgb("#D6C29A")
PARCH_LIGHT = hex_rgb("#EADCB8")
IVORY_TEXT = hex_rgb("#F3E9D2")

# Health
RED_DARK = hex_rgb("#6E1A16")
RED = hex_rgb("#A8302A")
RED_LIGHT = hex_rgb("#D0574A")
GHOST = hex_rgb("#E0A08A")
OXBLOOD = hex_rgb("#4A1A18")

# Amber / ember (limited use)
AMBER = hex_rgb("#E09A3A")
EMBER = hex_rgb("#F2B45A")
MUTED_GOLD = hex_rgb("#C49E57")

# Steel for icons/glyph blades
STEEL = hex_rgb("#9AA3A4")
STEEL_DARK = hex_rgb("#5E6668")

CATEGORY = {
    "Attack": hex_rgb("#7A2A24"),
    "Defence": hex_rgb("#4A5A6A"),
    "Mobility": hex_rgb("#3F6E62"),
    "Support": hex_rgb("#55703E"),
    "Special": hex_rgb("#5A4A66"),
}
CATEGORIES = ("Attack", "Defence", "Mobility", "Support", "Special")


def mix(a, b, t):
    """Linear blend; t may be a scalar or an array broadcastable to the colours."""
    a = np.asarray(a, np.float32)
    b = np.asarray(b, np.float32)
    t = np.asarray(t, np.float32)
    if t.ndim and t.shape[-1:] != (3,):
        t = t[..., None]
    return a * (1.0 - t) + b * t


def scale(colour, k: float) -> np.ndarray:
    """Multiply value (keeps hue)."""
    return np.clip(np.asarray(colour, np.float32) * k, 0.0, 1.0)


def desaturate(colour, amount: float) -> np.ndarray:
    colour = np.asarray(colour, np.float32)
    grey = colour @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    return mix(colour, np.stack([grey, grey, grey], -1), amount)


def luminance(rgb) -> np.ndarray:
    """Rec.709 relative luminance of sRGB-encoded values (same scale as input)."""
    rgb = np.asarray(rgb, np.float32)
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


# Warm/cool shift direction used for local hue variation (warm = +, cool = -)
WARM_SHIFT = np.array([1.0, 0.25, -0.85], np.float32)


def category_ramp(category: str) -> dict:
    """Low-saturation ramp for a card category, harmonised with charcoal and bronze."""
    c = CATEGORY[category]
    return {
        "deep": mix(CHAR_DEEP, c, 0.30),
        "body": mix(CHAR, c, 0.52),
        "body_light": mix(CHAR_LIGHT, c, 0.62),
        "accent_dark": scale(c, 0.62),
        "accent": c,
        "accent_light": mix(c, PARCH_LIGHT, 0.34),
        "accent_glint": mix(c, PARCH_LIGHT, 0.62),
        "wash_dark": mix(INK, c, 0.50),
        "wash": mix(CHAR, c, 0.80),
        "wash_light": mix(c, PARCH_LIGHT, 0.30),
    }
