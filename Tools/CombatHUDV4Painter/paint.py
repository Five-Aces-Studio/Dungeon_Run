"""Painted materials for the V4 HUD painter.

Everything works on a premultiplied float ``Layer`` and a per-sprite ``Ctx`` that
owns the deterministic random generator. Materials are small functions so each
look (bronze bevel, stone inset, parchment, health band, ink, wear, shadow) can
be tuned in one place.

Light comes from the top-left (``LIGHT``). Brush grain is sampled from the world
stroke texture ``T_DungeonRun_PaintStrokes.png`` (R dab value, G dab hue id,
B bristle, A blotch), which ties the UI brushwork to the acrylic world.
"""
from __future__ import annotations

import hashlib
import math
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

import palette as P
import shapes as S

SEED = 4242
AA = 1.0
LIGHT = np.array([-0.62, -0.78], np.float32)  # unit vector pointing toward the light
STROKE_TEXTURE_REL = Path("Assets/Art/Rendering/AcrylicV3/T_DungeonRun_PaintStrokes.png")
FONT_DIR_REL = Path("Assets/Art/UI/CombatHUDV4/Fonts")


# ------------------------------------------------------------------ process


def opt_out_of_power_throttling() -> bool:
    """Best effort: ask Windows not to move this CPU-bound process to efficiency cores (EcoQoS).

    Hybrid CPUs otherwise demote a background terminal process after a couple of seconds,
    which makes painting about 3x slower. It changes speed only, never results.
    """
    import sys
    if sys.platform != "win32":
        return False
    try:
        import ctypes
        from ctypes import wintypes

        class _State(ctypes.Structure):
            _fields_ = [("Version", ctypes.c_ulong), ("ControlMask", ctypes.c_ulong), ("StateMask", ctypes.c_ulong)]

        k32 = ctypes.WinDLL("kernel32", use_last_error=True)
        k32.GetCurrentProcess.restype = wintypes.HANDLE
        k32.SetProcessInformation.argtypes = [wintypes.HANDLE, ctypes.c_int, ctypes.c_void_p, wintypes.DWORD]
        k32.SetProcessInformation.restype = wintypes.BOOL
        state = _State(1, 1, 0)  # version 1, control EXECUTION_SPEED, state 0 = never throttle
        return bool(k32.SetProcessInformation(k32.GetCurrentProcess(), 4, ctypes.byref(state),
                                              ctypes.sizeof(state)))
    except Exception:  # pragma: no cover - purely an optimisation
        return False


# ------------------------------------------------------------------ determinism

def stable_hash(name: str) -> int:
    """Process-independent 64-bit hash (never Python's salted ``hash``)."""
    return int.from_bytes(hashlib.sha256(name.encode("utf-8")).digest()[:8], "little")


def rng_for(name: str) -> np.random.Generator:
    return np.random.default_rng([SEED, stable_hash(name)])


# --------------------------------------------------------------------- helpers

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def coverage(sd, aa: float = AA):
    """Analytic antialiased coverage of a signed distance field."""
    return np.clip(0.5 - sd / aa, 0.0, 1.0).astype(np.float32)


def normals(sd):
    gy, gx = np.gradient(sd)
    n = np.hypot(gx, gy) + 1e-6
    return (gx / n).astype(np.float32), (gy / n).astype(np.float32)


def facing_light(sd):
    """+1 where the outward normal points to the light (top-left edges), -1 opposite."""
    nx, ny = normals(sd)
    return nx * LIGHT[0] + ny * LIGHT[1]


def cel(value, thresholds, soft: float = 0.012):
    """Quantise a continuous value into steps 0..len(thresholds)."""
    lvl = np.zeros_like(value, dtype=np.float32)
    for t in thresholds:
        lvl += smoothstep(t - soft, t + soft, value)
    return lvl


def ramp(level, colours):
    """Map a float level (0..n-1) onto a list of colours."""
    cols = np.asarray(colours, np.float32)
    xs = np.arange(len(cols), dtype=np.float32)
    level = np.asarray(level, np.float32)
    return np.stack([np.interp(level, xs, cols[:, c]).astype(np.float32) for c in range(3)], -1)


def warm_cool(col, amount):
    """Shift colours warm (amount > 0) or cool (amount < 0); amount may be an array."""
    amount = np.asarray(amount, np.float32)
    if amount.ndim:
        amount = amount[..., None]
    return np.clip(col * (1.0 + amount * P.WARM_SHIFT), 0.0, 1.0)


def blur(a, sigma):
    return ndimage.gaussian_filter(a, sigma) if sigma > 0 else a


# ----------------------------------------------------------------------- layer

class Layer:
    """Premultiplied RGBA float canvas."""

    def __init__(self, w: int, h: int):
        self.w, self.h = w, h
        self.c = np.zeros((h, w, 3), np.float32)
        self.a = np.zeros((h, w), np.float32)

    def paint(self, colour, alpha):
        """Source-over with a colour (3,) or field (h, w, 3) and coverage (h, w).

        Only the bounding box of non-zero coverage is touched (same result, much faster).
        """
        alpha = np.clip(np.asarray(alpha, np.float32), 0.0, 1.0)
        if alpha.ndim == 0:
            alpha = np.full((self.h, self.w), float(alpha), np.float32)
        colour = np.asarray(colour, np.float32)
        rows = np.flatnonzero(alpha.max(axis=1) > 0.0)
        if rows.size == 0:
            return
        cols = np.flatnonzero(alpha[rows[0]:rows[-1] + 1].max(axis=0) > 0.0)
        y0, y1, x0, x1 = rows[0], rows[-1] + 1, cols[0], cols[-1] + 1
        a = alpha[y0:y1, x0:x1]
        col = colour[y0:y1, x0:x1] if colour.ndim == 3 else colour
        a3 = a[..., None]
        self.c[y0:y1, x0:x1] = col * a3 + self.c[y0:y1, x0:x1] * (1.0 - a3)
        self.a[y0:y1, x0:x1] = a + self.a[y0:y1, x0:x1] * (1.0 - a)

    def paint_under(self, colour, alpha):
        """Destination-over: paints behind what is already there."""
        alpha = np.clip(np.asarray(alpha, np.float32), 0.0, 1.0)
        colour = np.asarray(colour, np.float32)
        inv = (1.0 - self.a)[..., None]
        self.c = self.c + colour * alpha[..., None] * inv
        self.a = self.a + alpha * (1.0 - self.a)

    def multiply(self, factor, mask=None):
        """Scale existing colour (keeps alpha). factor: scalar, (3,) or (h, w[, 3])."""
        f = np.asarray(factor, np.float32)
        if f.ndim == 2:
            f = f[..., None]
        if mask is not None:
            f = 1.0 + (f - 1.0) * mask[..., None]
        self.c = self.c * f

    def straight(self):
        return self.c / np.maximum(self.a, 1e-6)[..., None]

    def replace_rgb(self, rgb, mask):
        m = np.clip(mask, 0.0, 1.0)[..., None]
        self.c = self.c * (1.0 - m) + np.asarray(rgb, np.float32) * self.a[..., None] * m

    def composite(self, other: "Layer", x: int = 0, y: int = 0, opacity: float = 1.0):
        """Place another layer on top at integer offset (x, y)."""
        x0, y0 = max(x, 0), max(y, 0)
        x1, y1 = min(x + other.w, self.w), min(y + other.h, self.h)
        if x1 <= x0 or y1 <= y0:
            return
        oc = other.c[y0 - y:y1 - y, x0 - x:x1 - x] * opacity
        oa = other.a[y0 - y:y1 - y, x0 - x:x1 - x] * opacity
        self.c[y0:y1, x0:x1] = oc + self.c[y0:y1, x0:x1] * (1.0 - oa[..., None])
        self.a[y0:y1, x0:x1] = oa + self.a[y0:y1, x0:x1] * (1.0 - oa)

    def downsample(self, k: int) -> "Layer":
        """Box-filter downsample by an integer factor (premultiplied, so edges stay clean)."""
        out = Layer(self.w // k, self.h // k)
        out.c = self.c.reshape(out.h, k, out.w, k, 3).mean(axis=(1, 3))
        out.a = self.a.reshape(out.h, k, out.w, k).mean(axis=(1, 3))
        return out

    def to_image(self) -> Image.Image:
        a = np.clip(self.a, 0.0, 1.0)
        rgb = np.clip(self.straight(), 0.0, 1.0)
        solid = a > (0.5 / 255.0)
        if solid.any() and (~solid).any():
            # colour bleed into transparent pixels so bilinear filtering never shows dark fringes
            idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
            rgb = np.where(solid[..., None], rgb, rgb[idx[0], idx[1]])
        arr = np.dstack([rgb, a[..., None]])
        return Image.fromarray(np.round(arr * 255.0).astype(np.uint8))

    @staticmethod
    def from_image(img: Image.Image) -> "Layer":
        arr = np.asarray(img.convert("RGBA"), np.float32) / 255.0
        lay = Layer(arr.shape[1], arr.shape[0])
        lay.a = arr[..., 3].copy()
        lay.c = arr[..., :3] * lay.a[..., None]
        return lay


# ------------------------------------------------------------------------ ctx

class Grain:
    """World stroke texture sampled for one sprite (channels 0..1, sampled lazily on first use)."""

    def __init__(self, tex: np.ndarray, u: np.ndarray, v: np.ndarray):
        self._tex, self._u, self._v = tex, u, v
        self._cache: dict = {}

    def _channel(self, k: int) -> np.ndarray:
        if k not in self._cache:
            self._cache[k] = ndimage.map_coordinates(self._tex[..., k], [self._v, self._u], order=1,
                                                     mode="grid-wrap").astype(np.float32)
        return self._cache[k]

    r = property(lambda self: self._channel(0))
    g = property(lambda self: self._channel(1))
    b = property(lambda self: self._channel(2))
    a = property(lambda self: self._channel(3))


_STROKES: dict = {}


def stroke_texture(repo_root: Path) -> np.ndarray:
    key = str(repo_root)
    if key not in _STROKES:
        img = Image.open(Path(repo_root) / STROKE_TEXTURE_REL).convert("RGBA")
        _STROKES[key] = np.asarray(img, np.float32) / 255.0
    return _STROKES[key]


def font_path(repo_root: Path, role: str, weight: str) -> Path:
    family = "AlegreyaSCDR" if role == "display" else "AlegreyaSansDR"
    return Path(repo_root) / FONT_DIR_REL / f"{family}-{weight}.ttf"


class Ctx:
    """Per-sprite painting context: size, grid and deterministic generator."""

    def __init__(self, name: str, w: int, h: int, repo_root: Path):
        self.name, self.w, self.h = name, w, h
        self.repo_root = Path(repo_root)
        self.rng = rng_for(name)
        self.X, self.Y = S.grid(w, h)

    # -- noise
    def noise(self, cell: float, octaves: int = 2, gain: float = 0.5):
        return fbm(self.h, self.w, self.rng, cell, octaves, gain)

    # -- brush grain
    def grain(self, texel: float = 2.2, angle: float = 18.0, stretch: float = 1.0, base_angle: float = 0.0) -> Grain:
        tex = stroke_texture(self.repo_root)
        th = math.radians(base_angle + self.rng.uniform(-angle, angle))
        flip = self.rng.random() < 0.5
        ox, oy = self.rng.uniform(0.0, 512.0, 2)
        X = (self.w - self.X) if flip else self.X
        Y = self.Y
        ca, sa = math.cos(th), math.sin(th)
        u = (X * ca - Y * sa) / (texel * stretch) + ox
        v = (X * sa + Y * ca) / texel + oy
        return Grain(tex, u, v)

    def base_grain(self) -> Grain:
        """Shared fine grain for metal parts of this sprite (sampled once)."""
        if not hasattr(self, "_base_grain"):
            self._base_grain = self.grain(texel=1.7)
        return self._base_grain


def _bspline_matrix(n: int, cells: int, cell: float) -> np.ndarray:
    """Cubic B-spline interpolation weights (n samples x cells control points)."""
    x = (np.arange(n, dtype=np.float64) + 0.5) / cell + 1.5
    i = np.floor(x).astype(np.int64)
    f = x - i
    w = np.stack([(1 - f) ** 3 / 6.0, (3 * f ** 3 - 6 * f ** 2 + 4) / 6.0,
                  (-3 * f ** 3 + 3 * f ** 2 + 3 * f + 1) / 6.0, f ** 3 / 6.0], 1)
    m = np.zeros((n, cells), np.float64)
    rows = np.arange(n)
    for kk in range(4):
        m[rows, np.clip(i - 1 + kk, 0, cells - 1)] += w[:, kk]
    return m


def value_noise(h: int, w: int, rng: np.random.Generator, cell: float):
    """Smooth value noise in about -1..1 (separable cubic B-spline of a random lattice)."""
    cell = max(float(cell), 2.0)
    gh = int(math.ceil(h / cell)) + 4
    gw = int(math.ceil(w / cell)) + 4
    g = rng.uniform(-1.0, 1.0, (gh, gw))
    out = _bspline_matrix(h, gh, cell) @ g @ _bspline_matrix(w, gw, cell).T
    return out.astype(np.float32)


def fbm(h, w, rng, cell, octaves=2, gain=0.5):
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        total += amp * value_noise(h, w, rng, cell / (2 ** o))
        norm += amp
        amp *= gain
    total /= norm
    m = float(np.abs(total).max()) or 1.0
    return total / m


# ------------------------------------------------------------------- materials

@dataclass(frozen=True)
class MetalStyle:
    ramp: tuple
    thresholds: tuple = (0.30, 0.66, 0.88)
    tilt: float = 1.15
    grain: float = 0.16
    hue: float = 0.035
    lowfreq: float = 0.06
    soft: float = 0.55
    sweep: float = 0.10


BRONZE = MetalStyle((P.BRONZE_DARK, P.BRONZE, P.BRONZE_LIGHT, P.GOLD), grain=0.2, hue=0.05)
BRONZE_DIM = MetalStyle((P.scale(P.BRONZE_DARK, 0.82), P.scale(P.BRONZE, 0.80), P.scale(P.BRONZE_LIGHT, 0.80),
                         P.scale(P.GOLD, 0.78)))
BRONZE_BRIGHT = MetalStyle((P.BRONZE, P.BRONZE_LIGHT, P.mix(P.BRONZE_LIGHT, P.GOLD, 0.5),
                            P.mix(P.GOLD, P.PARCH_LIGHT, 0.35)))
BRONZE_DESAT = MetalStyle(tuple(P.desaturate(P.scale(c, 0.78), 0.55) for c in
                                (P.BRONZE_DARK, P.BRONZE, P.BRONZE_LIGHT, P.GOLD)))
# Round 3: the highest-priority interactive element (the empty socket) needs to out-rank static
# HUD chrome, so static rims (Floor plaque, ACTIONS ring) are pulled ~15% dimmer than the base BRONZE.
BRONZE_STATIC_DIM = MetalStyle(tuple(P.scale(c, 0.85) for c in (P.BRONZE_DARK, P.BRONZE, P.BRONZE_LIGHT, P.GOLD)),
                               grain=0.2, hue=0.05)
# Bronze rim brightened ~1.35x for the empty action socket (was reading as the dimmest bronze on screen).
BRONZE_SOCKET = MetalStyle(tuple(P.scale(c, 1.35) for c in (P.BRONZE_DARK, P.BRONZE, P.BRONZE_LIGHT, P.GOLD)),
                           grain=0.2, hue=0.05)
# Antique-gold trigon, e.g. the painted target marker.
GOLD_TRIGON = MetalStyle((P.scale(P.BRONZE_DARK, 0.95), P.mix(P.AMBER, P.BRONZE, 0.35), P.mix(P.AMBER, P.GOLD, 0.55),
                          P.mix(P.GOLD, P.PARCH_LIGHT, 0.35)))


def metal(layer: Layer, ctx: Ctx, sd_outer, sd_hole=None, *, rim_out=6.0, rim_in=5.0, style: MetalStyle = BRONZE,
          mask=None, grain: Grain | None = None):
    """Raised cel-shaded metal between an outer silhouette and an optional hole.

    Returns (lam, region): the lighting term (for wear placement) and the coverage.
    """
    nx, ny = normals(sd_outer)
    d_out = np.maximum(-sd_outer, 0.0)
    region = coverage(sd_outer)
    if sd_hole is not None:
        # raised bar between the silhouette and the hole: outer bevel, flat top, inner bevel (cel regions)
        hx, hy = normals(sd_hole)
        d_in = np.maximum(sd_hole, 0.0)
        # bevel widths are absolute (px), scaled down only where the bar is thinner than both bevels
        width = d_out + d_in
        k = np.clip(width / max(rim_out + rim_in, 1e-3), 0.0, 1.0) * 0.82 + 0.18 * (width > 0)
        b_out, b_in = rim_out * k, rim_in * k
        s_out = 1.0 - smoothstep(b_out * 0.72, b_out, d_out)
        s_in = 1.0 - smoothstep(b_in * 0.72, b_in, d_in)
        lam = s_out * (nx * LIGHT[0] + ny * LIGHT[1]) - s_in * (hx * LIGHT[0] + hy * LIGHT[1])
        region = region * coverage(-sd_hole)
    else:
        # solid boss: bevel of width rim_out, then a flat top
        s_out = 1.0 - smoothstep(rim_out * 0.7, rim_out * 1.0, d_out)
        lam = s_out * (nx * LIGHT[0] + ny * LIGHT[1])
    if mask is not None:
        region = region * mask
    if grain is None:
        grain = ctx.base_grain()
    lf = ctx.noise(cell=46, octaves=2)
    # global top-left light across the whole part so flat tops are not uniform
    sweep = (0.5 - ctx.X / ctx.w) * 0.6 + (0.5 - ctx.Y / ctx.h) * 0.8
    v = (0.5 + 0.5 * style.tilt * lam + style.grain * (grain.r - 0.5) * 1.25 + style.lowfreq * lf
         + style.sweep * sweep)
    lvl = blur(cel(v, style.thresholds), style.soft)
    col = ramp(lvl, style.ramp)
    col = col * (1.0 + style.grain * 0.45 * (grain.r - 0.5) * 2.0)[..., None]
    col = warm_cool(col, style.hue * (grain.g - 0.5) * 2.0 + 0.05 * lf)
    layer.paint(col, region)
    return lam.astype(np.float32), region


def stone(layer: Layer, ctx: Ctx, sd, *, ramp_cols=(P.CHAR_DEEP, P.CHAR, P.CHAR_LIGHT), vignette=18.0,
          grain_strength=0.2, recess=0.55, catch=0.28, clean=None, texel=2.4, mask=None, warm=0.06,
          mottle=0.35, grain: Grain | None = None, hue=0.07, sweep=0.14):
    """Charcoal-teal stone/enamel inset: painted vignette, recess shadow, world brush grain."""
    d = -sd
    t = smoothstep(0.0, vignette, d)
    lf = ctx.noise(cell=max(ctx.w, ctx.h) * 0.33, octaves=2)
    keep = 1.0 if clean is None else (1.0 - clean)
    col = P.mix(ramp_cols[0], ramp_cols[1], t)
    col = P.mix(col, ramp_cols[2], np.clip(lf, 0.0, 1.0) * mottle * t * keep)
    g = grain if grain is not None else ctx.grain(texel)
    col = col * (1.0 + grain_strength * (g.r - 0.5) * 2.0 * keep)[..., None]
    col = warm_cool(col, hue * (g.g - 0.5) * 2.0 * keep + warm * lf)
    if sweep:
        # painted light from the top-left across the whole face (kept, gently, inside text zones)
        sw = (0.5 - ctx.X / ctx.w) * 0.6 + (0.5 - ctx.Y / ctx.h) * 0.8
        col = col * (1.0 + sweep * sw)[..., None]
    if recess:
        f = facing_light(sd)
        sh = (1.0 - smoothstep(0.0, 9.0, d)) * np.clip(f * 1.25, 0.0, 1.0) * recess
        col = col * (1.0 - sh)[..., None]
        c = (1.0 - smoothstep(0.0, 2.6, d)) * np.clip(-f, 0.0, 1.0) * catch
        col = P.mix(col, P.TEAL_LIGHT, c)
    region = coverage(sd) if mask is None else coverage(sd) * mask
    layer.paint(col, region)
    return region


def parchment(layer: Layer, ctx: Ctx, sd, *, clean=None, edge=8.0, grain_strength=0.05, burn=0.45, mask=None,
              base=P.PARCH, light=P.PARCH_LIGHT, shadow=P.PARCH_SHADOW):
    """Warm parchment with painted, slightly burnt edges; grain is zero inside clean zones."""
    d = -sd
    keep = 1.0 if clean is None else (1.0 - clean)
    lf = ctx.noise(cell=70, octaves=3)
    col = P.mix(base, light, np.clip(lf, 0.0, 1.0) * 0.45 * keep)
    col = P.mix(col, shadow, np.clip(-lf, 0.0, 1.0) * 0.30 * keep)
    edge_n = 1.0 + 0.4 * ctx.noise(cell=18, octaves=2)
    e = 1.0 - smoothstep(0.0, edge * edge_n, d)
    col = P.mix(col, shadow, e * 0.9)
    col = P.mix(col, P.BRONZE, (1.0 - smoothstep(0.0, edge * 0.32 * edge_n, d)) * burn)
    g = ctx.grain(texel=2.0)
    col = col * (1.0 + grain_strength * (g.r - 0.5) * 2.0 * keep)[..., None]
    col = warm_cool(col, 0.03 * (g.g - 0.5) * 2.0 * keep)
    region = coverage(sd) if mask is None else coverage(sd) * mask
    layer.paint(col, region)
    return region


def health_band(layer: Layer, ctx: Ctx, sd, y0, y1, *, cols=(P.RED_DARK, P.RED, P.RED_LIGHT), grain_strength=0.12,
                streak=0.55, flat=False):
    """Painted health band: light top band, base, dark bottom, grain and a highlight streak."""
    t = (ctx.Y - y0) / max(y1 - y0, 1e-6)
    g = ctx.grain(texel=1.8, angle=6.0)
    lf = ctx.noise(cell=60, octaves=2)
    if flat:
        v = 0.62 - 0.25 * t + 0.02 * lf
        lvl = blur(cel(v, (0.40, 0.62)), 0.5)
    else:
        v = 1.0 - t + 0.05 * lf + 0.08 * (g.r - 0.5)
        lvl = blur(cel(v, (0.30, 0.68)), 0.6)
    col = ramp(lvl, cols)
    col = col * (1.0 + grain_strength * (g.r - 0.5) * 2.0)[..., None]
    col = warm_cool(col, 0.04 * (g.g - 0.5) * 2.0)
    region = coverage(sd)
    layer.paint(col, region)
    if streak > 0:
        ys = y0 + (y1 - y0) * 0.24
        xs0, xs1 = ctx.w * 0.05, ctx.w * 0.58
        width = max((y1 - y0) * 0.09, 1.2)
        along = smoothstep(xs0, xs0 + ctx.w * 0.08, ctx.X) * (1.0 - smoothstep(xs1 - ctx.w * 0.2, xs1, ctx.X))
        broken = np.clip(0.6 + 0.8 * ctx.noise(cell=24, octaves=2), 0.0, 1.0)
        line = np.clip(0.5 - (np.abs(ctx.Y - ys) - width * 0.5), 0.0, 1.0)
        layer.paint(P.mix(cols[2], P.PARCH_LIGHT, 0.55), line * along * broken * streak * region)
    return region


# --------------------------------------------------------------------------- ink

def ink_contour(layer: Layer, ctx: Ctx, sd, *, w_min=2.2, w_max=3.4, colour=P.INK, alpha=1.0, light_bias=0.45,
                mask=None):
    """Outer ink band with living width: thinner on lit edges, thicker in shadow, noisy along the contour."""
    nx, ny = normals(sd)
    nz = ctx.noise(cell=26, octaves=2) * 0.5 + 0.5
    shadow_side = np.clip(-(nx * LIGHT[0] + ny * LIGHT[1]) * 0.5 + 0.5, 0.0, 1.0)
    t = np.clip((1.0 - light_bias) * nz + light_bias * shadow_side, 0.0, 1.0)
    w = w_min + (w_max - w_min) * t
    a = coverage(sd) * np.clip(0.5 + (sd + w) / AA, 0.0, 1.0) * alpha
    if mask is not None:
        a = a * mask
    layer.paint(colour, a)


def ink_line(layer: Layer, ctx: Ctx, sd, *, at=0.0, width=1.4, colour=P.INK, alpha=1.0, vary=0.35, mask=None):
    """Thin ink line along the iso-contour ``sd == at`` (secondary inner lines)."""
    w = width * (1.0 + vary * ctx.noise(cell=22, octaves=2))
    a = np.clip(0.5 - (np.abs(sd - at) - w * 0.5) / AA, 0.0, 1.0) * alpha
    if mask is not None:
        a = a * mask
    layer.paint(colour, a)


def engraved_line(layer: Layer, ctx: Ctx, sd, *, at=0.0, width=1.6, depth=0.35, mask=None):
    """Engraving: a dark groove with a light catch offset toward the lower right."""
    groove = np.clip(0.5 - (np.abs(sd - at) - width * 0.5) / AA, 0.0, 1.0)
    shifted = ndimage.shift(groove, (1.2, 1.0), order=1)
    catch = np.clip(shifted - groove, 0.0, 1.0)
    if mask is not None:
        groove, catch = groove * mask, catch * mask
    layer.multiply(1.0 - depth * groove)
    layer.paint(P.TEAL_LIGHT, catch * 0.35)


# -------------------------------------------------------------------- stamping

def stamp_ellipse(layer: Layer, cx, cy, rx, ry, angle, colour, alpha, clip=None, soft=0.9):
    r = int(math.ceil(max(rx, ry) + 2))
    x0, x1 = max(int(cx) - r, 0), min(int(cx) + r + 1, layer.w)
    y0, y1 = max(int(cy) - r, 0), min(int(cy) + r + 1, layer.h)
    if x1 <= x0 or y1 <= y0:
        return
    xs = np.arange(x0, x1, dtype=np.float32) + 0.5
    ys = np.arange(y0, y1, dtype=np.float32) + 0.5
    Xl, Yl = np.meshgrid(xs, ys)
    dx, dy = Xl - cx, Yl - cy
    ca, sa = math.cos(angle), math.sin(angle)
    u = (dx * ca + dy * sa) / rx
    v = (-dx * sa + dy * ca) / ry
    sdl = (np.hypot(u, v) - 1.0) * min(rx, ry)
    a = np.clip(0.5 - sdl / soft, 0.0, 1.0) * alpha
    if clip is not None:
        a = a * clip[y0:y1, x0:x1]
    col = np.asarray(colour, np.float32)
    sub_c = layer.c[y0:y1, x0:x1]
    sub_a = layer.a[y0:y1, x0:x1]
    layer.c[y0:y1, x0:x1] = col * a[..., None] + sub_c * (1.0 - a[..., None])
    layer.a[y0:y1, x0:x1] = a + sub_a * (1.0 - a)


def worn_dabs(layer: Layer, ctx: Ctx, lam, region, sd, *, count=12, colour=P.GOLD, size=(1.1, 2.4),
              alpha=(0.5, 0.85), min_lam=0.3, grain: Grain | None = None, elong=2.6, spacing=10.0):
    """Light-gold worn highlights along lit raised edges (masked by the blotch channel)."""
    cand = (region > 0.97) & (lam > min_lam)
    ys, xs = np.nonzero(cand)
    if len(ys) == 0:
        return
    nx, ny = normals(sd)
    order = ctx.rng.permutation(len(ys))
    placed: list = []
    for i in order[: count * 12]:
        y, x = int(ys[i]), int(xs[i])
        if grain is not None and grain.a[y, x] < 0.5:
            continue
        if any((x - px) ** 2 + (y - py) ** 2 < spacing ** 2 for px, py in placed):
            continue
        ang = math.atan2(float(ny[y, x]), float(nx[y, x])) + math.pi / 2
        r = float(ctx.rng.uniform(*size))
        stamp_ellipse(layer, x + 0.5, y + 0.5, r * elong, r, ang, colour, float(ctx.rng.uniform(*alpha)), clip=region)
        placed.append((x, y))
        if len(placed) >= count:
            break


def chips(layer: Layer, ctx: Ctx, sd, *, count=6, colour=P.INK, size=(0.9, 1.8), alpha=(0.7, 0.95),
          band=(0.8, 3.2), spacing=16.0, mask=None):
    """Small dark nicks on the outer contour, favouring the shadow side."""
    d = -sd
    f = facing_light(sd)
    cand = (d > band[0]) & (d < band[1]) & (f < 0.35)
    if mask is not None:
        cand &= mask > 0.5
    ys, xs = np.nonzero(cand)
    if len(ys) == 0:
        return
    nx, ny = normals(sd)
    region = coverage(sd)
    order = ctx.rng.permutation(len(ys))
    placed: list = []
    for i in order[: count * 20]:
        y, x = int(ys[i]), int(xs[i])
        if any((x - px) ** 2 + (y - py) ** 2 < spacing ** 2 for px, py in placed):
            continue
        ang = math.atan2(float(ny[y, x]), float(nx[y, x])) + math.pi / 2
        r = float(ctx.rng.uniform(*size))
        stamp_ellipse(layer, x + 0.5, y + 0.5, r * 1.9, r, ang, colour, float(ctx.rng.uniform(*alpha)), clip=region)
        placed.append((x, y))
        if len(placed) >= count:
            break


# ------------------------------------------------------------ shadows / glows

def halo(layer: Layer, sd, *, radius=5.0, alpha=0.35, colour=P.SHADOW_WARM, offset=(0.0, 1.5)):
    """Soft painted contact halo just outside a silhouette (painted underneath)."""
    m = coverage(sd)
    if offset != (0.0, 0.0):
        m = ndimage.shift(m, (offset[1], offset[0]), order=1)
    m = blur(m, radius * 0.5)
    layer.paint_under(colour, np.clip(m * alpha, 0.0, 1.0))


def contact_shadow(ctx: Ctx, sd, *, blur_px=10.0, alpha=0.5, offset=(0.0, 8.0), bottom_weight=0.45):
    """Soft warm-dark contact shadow as a (colour, alpha) pair: darker toward the bottom, brushy edge."""
    m = coverage(sd)
    m = ndimage.shift(m, (offset[1], offset[0]), order=1)
    m = blur(m, blur_px)
    ys = ctx.Y / ctx.h
    m = m * (1.0 - bottom_weight + bottom_weight * smoothstep(0.1, 0.95, ys))
    m = m * (1.0 + 0.12 * ctx.noise(cell=60, octaves=2))
    return np.clip(m * alpha / max(float(m.max()), 1e-6), 0.0, alpha)


# ----------------------------------------------------------------- text zones

def zone_px(zone_1080, k=2):
    x0, y0, x1, y1 = zone_1080
    return (int(round(x0 * k)), int(round(y0 * k)), int(round(x1 * k)), int(round(y1 * k)))


def zone_mask(ctx: Ctx, zones_px, feather=0.0):
    """1 inside the text-safe rectangles, optionally feathered outward by ``feather`` px."""
    m = np.zeros((ctx.h, ctx.w), np.float32)
    for x0, y0, x1, y1 in zones_px:
        if feather <= 0:
            m[y0:y1, x0:x1] = 1.0
        else:
            sd = S.sd_rect(ctx.X, ctx.Y, x0, y0, x1, y1)
            m = np.maximum(m, 1.0 - smoothstep(0.0, feather, sd))
    return m


def flatten_zones(layer: Layer, ctx: Ctx, zones_px, *, feather=5.0, max_std=2.0):
    """Replace each text zone with a fitted gentle linear gradient (luminance std <= max_std/255)."""
    rgb = layer.straight()
    for x0, y0, x1, y1 in zones_px:
        zone = rgb[y0:y1, x0:x1].reshape(-1, 3).astype(np.float64)
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float64)
        cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
        sx, sy = max(x1 - x0, 1), max(y1 - y0, 1)
        A = np.stack([np.ones(zone.shape[0]), ((xx - cx) / sx).ravel(), ((yy - cy) / sy).ravel()], 1)
        coef, *_ = np.linalg.lstsq(A, zone, rcond=None)
        # evaluate on the zone plus the feather ring
        fx0, fy0 = max(int(x0 - feather - 1), 0), max(int(y0 - feather - 1), 0)
        fx1, fy1 = min(int(x1 + feather + 1), ctx.w), min(int(y1 + feather + 1), ctx.h)
        gy, gx = np.mgrid[fy0:fy1, fx0:fx1].astype(np.float64)
        B = np.stack([np.ones(gx.size), ((gx - cx) / sx).ravel(), ((gy - cy) / sy).ravel()], 1)
        plane = (B @ coef).reshape(fy1 - fy0, fx1 - fx0, 3)
        inner = plane[y0 - fy0:y1 - fy0, x0 - fx0:x1 - fx0]
        lum = P.luminance(inner) * 255.0
        std = float(lum.std())
        if std > max_std:
            mean = inner.reshape(-1, 3).mean(0)
            plane = mean + (plane - mean) * (max_std / std)
        full = np.zeros((ctx.h, ctx.w, 3), np.float32)
        full[fy0:fy1, fx0:fx1] = plane
        sd = S.sd_rect(ctx.X, ctx.Y, x0, y0, x1, y1)
        m = 1.0 - smoothstep(0.0, max(feather, 1e-3), sd)
        m[y0:y1, x0:x1] = 1.0
        layer.replace_rgb(full, m)
        rgb = layer.straight()


# ------------------------------------------------------------ font glyph masks

def text_sd(ctx: Ctx, text: str, role: str, weight: str, size_px: float, cx: float, cy: float, ss: int = 4):
    """Signed distance of rendered text (display/body font), centred at (cx, cy)."""
    W, H = ctx.w * ss, ctx.h * ss
    img = Image.new("L", (W, H), 0)
    font = ImageFont.truetype(str(font_path(ctx.repo_root, role, weight)), int(round(size_px * ss)))
    ImageDraw.Draw(img).text((cx * ss, cy * ss), text, font=font, fill=255, anchor="mm")
    m = np.asarray(img, np.float32) / 255.0 >= 0.5
    inside = ndimage.distance_transform_edt(m)
    outside = ndimage.distance_transform_edt(~m)
    sd = (outside - inside).astype(np.float32) / ss
    # sample back at pixel centres
    sd = sd.reshape(ctx.h, ss, ctx.w, ss).mean(axis=(1, 3))
    return sd
