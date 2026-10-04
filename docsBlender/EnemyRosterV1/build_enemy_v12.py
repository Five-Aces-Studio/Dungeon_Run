"""Enemy Vertical Slice V1.2 builder: Snake Soldier, Snake Sentinel, Snake Chieftain.

Run only through the connected Blender MCP session:

    spec = importlib.util.spec_from_file_location('enemy_v12', <this file>)
    v12 = importlib.util.module_from_spec(spec); spec.loader.exec_module(v12)
    v12.build_character('SnakeSoldier')

Nothing runs on import. Each build owns one scene, `EnemyRosterV12_<Name>`, and never touches the
V1/V1.1 scenes, checkpoints or the user's original scene. Rebuilding an owned V1.2 scene is explicit
(`rebuild=True`) and removes only data blocks that scene created.

Art direction (matches the acrylic Trigonal Abyss world, see docs/ART_DIRECTION.md):
- faceted low-poly forms (flat shading, 16-sided torso/tail) with large readable masses;
- designs follow the official printable cards (pp. 7, 9, 11): Soldier = four eyes, rope-wrapped war
  axe, left leather pauldron and chest strap; Sentinel = cobra hood, forehead eye, glaive, grey
  breastplate with eye glyph; Chieftain = three hooded heads, twin scimitars, gold eye amulet, red belt;
- desaturated olive/ochre palette, painted per pixel at bake time (see `bake_color`), never per face.

Rig standard (shared by the whole snake roster): Root, COG, Spine, Chest, Neck/Head/Jaw (+_L/_R for
extra heads), Clavicle_L/R, UpperArm_L/R, Forearm_L/R, Hand_L/R, Tail_01..Tail_08, non-deforming
MainHand (right) / OffHand (left) weapon sockets, VFX_WeaponTip_Main/_Off, TargetAnchor, HUDAnchor,
DamageTextAnchor, VFXSocket_Chest, VFXSocket_Head, VFX_Ground.

Shoulder fix (diagnosed on V1.1, see docs/ENEMY_V12_REPORT.md): the humerus pivot sits inside the
deltoid mass, not on the torso skin; the socket is a rounded loop; torso, socket and arm weights are
one continuous spatial field; the clavicle carries the deltoid/trapezius and takes part in every raise.
Blender validation must keep Preserve Volume off (Unity linear skinning is authoritative).
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector, Matrix, Quaternion

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'docsBlender/EnemyRosterV1/V12'
VERSION = 'V12'
pi, sin, cos = math.pi, math.sin, math.cos
CATEGORY = {'SnakeSoldier': 'Basic', 'SnakeSentinel': 'Elite', 'SnakeChieftain': 'Boss'}
KIND = {'SnakeSoldier': 'Soldier', 'SnakeSentinel': 'Sentinel', 'SnakeChieftain': 'Chieftain'}
N_BODY = 16


# ----------------------------------------------------------------------------------------- math

def clamp(x, a=0.0, b=1.0):
    return max(a, min(b, x))


def smooth(x):
    x = clamp(x)
    return x * x * (3 - 2 * x)


def weights(*pairs):
    result = {}
    for name, amount in pairs:
        if amount > 1e-5:
            result[name] = result.get(name, 0.0) + amount
    total = sum(result.values()) or 1.0
    return {k: v / total for k, v in result.items()}


def blend(a, b, t):
    """Blend two weight dicts: (1-t)*a + t*b."""
    out = {k: v * (1 - t) for k, v in a.items()}
    for k, v in b.items():
        out[k] = out.get(k, 0.0) + v * t
    return weights(*out.items())


def catmull(values, steps):
    result = []
    for i in range(len(values) - 1):
        a, b, c, d = values[max(0, i - 1)], values[i], values[i + 1], values[min(len(values) - 1, i + 2)]
        for j in range(steps):
            t = j / steps
            result.append(tuple(.5 * ((2 * b[k]) + (-a[k] + c[k]) * t + (2 * a[k] - 5 * b[k] + 4 * c[k] - d[k]) * t * t
                                       + (-a[k] + 3 * b[k] - 3 * c[k] + d[k]) * t * t * t) for k in range(len(b))))
    result.append(tuple(values[-1]))
    return result


def superellipse(a, e):
    c, s = cos(a), sin(a)
    return math.copysign(abs(c) ** (2 / e), c), math.copysign(abs(s) ** (2 / e), s)


def perpendicular_frame(t, hint=Vector((0, 1, 0))):
    u = hint - t * hint.dot(t)
    if u.length < .1:
        u = Vector((1, 0, 0)) - t * t.x
    u.normalize()
    return u, t.cross(u).normalized()


# -------------------------------------------------------------------------------------- palette
# sRGB; calibrated against the lit Lab world (cloak s.28 v.56, floor v.27). Saturation stays moderate
# so the acrylic shader, torch light and palette pass can do the painting.
SHARED = {
    'eye': (0.93, 0.84, 0.30), 'pupil': (0.10, 0.09, 0.05), 'mouth': (0.20, 0.08, 0.07),
    'tongue': (0.66, 0.20, 0.17), 'nostril': (0.16, 0.16, 0.10),
    'leather': (0.43, 0.27, 0.17), 'leather_dark': (0.28, 0.17, 0.11), 'rope': (0.72, 0.64, 0.48),
    'wood': (0.40, 0.27, 0.17), 'steel': (0.47, 0.51, 0.53), 'steel_dark': (0.27, 0.30, 0.32),
    'gold': (0.78, 0.60, 0.26), 'gold_dark': (0.52, 0.36, 0.16), 'red': (0.55, 0.16, 0.13),
    'red_dark': (0.36, 0.10, 0.09), 'glyph': (0.40, 0.28, 0.46), 'buckle': (0.66, 0.66, 0.60),
}
SKIN = {
    # top, side, belly, band (dorsal saddle), brow
    'Soldier': dict(top=(0.26, 0.34, 0.21), side=(0.38, 0.46, 0.29), belly=(0.63, 0.60, 0.43),
                    band=(0.18, 0.23, 0.15), brow=(0.21, 0.27, 0.17), band_period=.62),
    'Sentinel': dict(top=(0.22, 0.32, 0.25), side=(0.34, 0.44, 0.35), belly=(0.60, 0.61, 0.47),
                     band=(0.14, 0.21, 0.18), brow=(0.17, 0.24, 0.20), band_period=.48),
    'Chieftain': dict(top=(0.21, 0.29, 0.18), side=(0.32, 0.42, 0.25), belly=(0.64, 0.56, 0.39),
                      band=(0.22, 0.18, 0.12), brow=(0.15, 0.21, 0.13), band_period=.55),
}


# ------------------------------------------------------------------------------------ mesh part

class Part:
    """Indexed mesh with per-vertex skin weights and bake attributes, per-face color keys."""

    def __init__(self, name, family):
        self.name, self.family = name, family
        self.v, self.w, self.f, self.fc = [], [], [], []
        self.attr = {'Skin': [], 'Belly': [], 'Dorsal': [], 'Arc': []}

    def vertex(self, p, w=None, skin=0.0, belly=0.0, dorsal=0.0, arc=0.0):
        self.v.append(tuple(p))
        self.w.append(w or {'Chest': 1.0})
        for key, value in (('Skin', skin), ('Belly', belly), ('Dorsal', dorsal), ('Arc', arc)):
            self.attr[key].append(value)
        return len(self.v) - 1

    def face(self, verts, color):
        self.f.append(tuple(verts))
        self.fc.append(color)

    def loft(self, rings, color, cap_start=True, cap_end=True):
        for a, b in zip(rings, rings[1:]):
            n = len(a)
            for j in range(n):
                self.face((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j]), color)
        if cap_start:
            self.face(tuple(reversed(rings[0])), color)
        if cap_end:
            self.face(tuple(rings[-1]), color)

    def make(self, rig, materials, colors):
        mesh = bpy.data.meshes.new(rig.name.removeprefix('Rig_') + '_' + self.name)
        mesh.from_pydata(self.v, [], self.f)
        mesh.validate()
        mesh.update()
        ob = bpy.data.objects.new(rig.name.removeprefix('Rig_') + '_' + self.name + '_LOD0', mesh)
        bpy.context.collection.objects.link(ob)
        ob.parent = rig
        mesh.materials.append(materials[self.family])
        organic = self.family == 'Organic'
        for poly in mesh.polygons:
            poly.use_smooth = organic
        if organic:
            # Faceted planes survive (sharp above 40 degrees) without flat-shading triangulation noise.
            mesh.set_sharp_from_angle(angle=math.radians(40))
        mesh.uv_layers.new(name='UV0')
        col = mesh.color_attributes.new('PartColor', 'FLOAT_COLOR', 'CORNER')
        lin = {k: tuple(srgb_to_linear(c) for c in rgb) + (1.0,) for k, rgb in colors.items()}
        for poly, key in zip(mesh.polygons, self.fc):
            for li in poly.loop_indices:
                col.data[li].color = lin[key]
        for key, values in self.attr.items():
            layer = mesh.attributes.new(key, 'FLOAT', 'POINT')
            layer.data.foreach_set('value', values)
        for name in sorted({k for d in self.w for k in d}):
            group = ob.vertex_groups.new(name=name)
            for i, d in enumerate(self.w):
                if name in d:
                    group.add([i], d[name], 'REPLACE')
        mod = ob.modifiers.new('Skin (linear, Unity-compatible)', 'ARMATURE')
        mod.object = rig
        mod.use_deform_preserve_volume = False
        return ob


def srgb_to_linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


# ------------------------------------------------------------------------------ kind parameters

def params(kind):
    """Proportions in metres before the Unity VisualScale. Front is -Y, left is +X, up is +Z."""
    tail = [(0, 0, .83, .31, .29), (-.17, .18, .51, .33, .30), (-.53, .48, .31, .33, .29),
            (-.36, .98, .29, .31, .27), (.32, 1.08, .26, .28, .24), (.91, .70, .22, .23, .20),
            (1.00, .07, .17, .17, .15), (.55, -.42, .11, .10, .09), (-.10, -.50, .05, .035, .035)]
    torso = [  # z, half-width, front depth, back depth, superellipse exponent
        (.98, .31, .27, .27, 2.2), (1.16, .30, .25, .26, 2.3), (1.36, .33, .26, .27, 2.5),
        (1.56, .40, .28, .28, 2.7), (1.74, .47, .30, .29, 2.9), (1.88, .52, .31, .29, 3.0),
        (2.00, .54, .29, .28, 3.0), (2.12, .49, .26, .26, 2.8), (2.21, .36, .23, .23, 2.4),
        (2.29, .25, .20, .20, 2.2), (2.37, .21, .19, .19, 2.0)]
    p = dict(kind=kind, tail=tail, torso=torso, hole=(4, 7), neck_top=2.46,
             pivot=(.43, .01, 1.97), elbow=(.72, -.05, 1.62), wrist=(.86, -.26, 1.37), arm_r=1.0,
             heads={'': (0, 2.66, 1.34)}, hood=False)
    if kind == 'Sentinel':
        p.update(neck_top=2.50, heads={'': (0, 2.74, 1.30)}, hood=True)
        p['torso'] = [(z, w * .97, f, b, e) for z, w, f, b, e in torso]
    if kind == 'Chieftain':
        p['tail'] = [(x * 1.14, y * 1.10, z * 1.08, rx * 1.12, ry * 1.10) for x, y, z, rx, ry in tail]
        p['torso'] = [(z, w * 1.13, f * 1.10, b * 1.10, e) for z, w, f, b, e in torso]
        p.update(neck_top=2.54, pivot=(.49, .01, 1.97), elbow=(.83, -.05, 1.61), wrist=(1.02, -.24, 1.38),
                 arm_r=1.12, heads={'': (0, 2.98, 1.36), '_L': (.78, 2.80, 1.0), '_R': (-.78, 2.80, 1.0)},
                 hood=True)
    return p


def arm_points(p, side):
    s = 1 if side == 'L' else -1
    return [Vector((s * v[0], v[1], v[2])) for v in (p['pivot'], p['elbow'], p['wrist'])]


def grip_point(p, side):
    s = 1 if side == 'L' else -1
    wrist = arm_points(p, side)[2]
    return wrist + Vector((s * .03, -.09, -.11))


# ---------------------------------------------------------------------------------------- rig

def create_rig(name, p):
    data = bpy.data.armatures.new('Skeleton_' + name + VERSION)
    rig = bpy.data.objects.new('Rig_' + name + VERSION, data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')

    def bone(n, a, b, parent=None, deform=True):
        item = data.edit_bones.new(n)
        item.head, item.tail = Vector(a), Vector(b)
        item.use_deform = deform
        if parent:
            item.parent = data.edit_bones[parent]
        return item

    bone('Root', (0, 0, 0), (0, 0, .25), deform=False)
    bone('COG', (0, 0, .67), (0, 0, 1.05), 'Root')
    bone('Spine', (0, 0, 1.05), (0, 0, 1.63), 'COG')
    bone('Chest', (0, 0, 1.63), (0, 0, 2.13), 'Spine')
    for suffix, (x, z, size) in p['heads'].items():
        neck_base = (x * .45, .03, 2.14) if suffix else (0, .015, 2.14)
        bone('Neck' + suffix, neck_base, (x, .02, z - .12 * size), 'Chest')
        bone('Head' + suffix, (x, .02, z - .12 * size), (x, .02, z + .30 * size), 'Neck' + suffix)
        bone('Jaw' + suffix, (x, .06 * size, z - .10 * size), (x, -.36 * size, z - .15 * size), 'Head' + suffix)
    for side in ('L', 'R'):
        pivot, elbow, wrist = arm_points(p, side)
        s = 1 if side == 'L' else -1
        grip = grip_point(p, side)
        # Sternoclavicular head, humeral tail: the clavicle lever carries the shoulder mass.
        bone('Clavicle_' + side, (s * .10, -.02, 2.10), pivot, 'Chest')
        bone('UpperArm_' + side, pivot, elbow, 'Clavicle_' + side)
        bone('Forearm_' + side, elbow, wrist, 'UpperArm_' + side)
        bone('Hand_' + side, wrist, grip, 'Forearm_' + side)
        socket = 'OffHand' if side == 'L' else 'MainHand'
        tip = 'VFX_WeaponTip_Off' if side == 'L' else 'VFX_WeaponTip_Main'
        bone(socket, grip, grip + Vector((0, 0, .15)), 'Hand_' + side, False)
        bone(tip, grip + Vector((0, 0, 1.2)), grip + Vector((0, 0, 1.28)), socket, False)
    tail = catmull(p['tail'], 1)
    for i in range(8):
        bone('Tail_%02d' % (i + 1), tail[i][:3], tail[i + 1][:3], 'COG' if i == 0 else 'Tail_%02d' % i)
    top = max(z + .30 * s for _, (x, z, s) in p['heads'].items())
    for label, point, parent in [
            ('TargetAnchor', (0, 0, top * .66), 'Root'), ('HUDAnchor', (0, 0, top + .35), 'Root'),
            ('DamageTextAnchor', (0, -.15, top - .05), 'Chest'), ('VFXSocket_Chest', (0, -.33, 1.90), 'Chest'),
            ('VFXSocket_Head', (0, -.20, p['heads'][''][1] + .05), 'Head'), ('VFX_Ground', (0, 0, .035), 'Root')]:
        bone(label, point, Vector(point) + Vector((0, 0, .08)), parent, False)
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.select_set(False)
    rig.show_in_front = True
    rig['Milestone'] = 'V1.2 production-reference candidate'
    return rig


# ------------------------------------------------------------------------------- skin weights

def axial_weights(z):
    stops = [(0.65, 'COG'), (1.20, 'Spine'), (1.90, 'Chest'), (2.40, 'Neck')]
    if z <= stops[0][0]:
        return {'COG': 1.0}
    for (low, a), (high, b) in zip(stops, stops[1:]):
        if z <= high:
            t = smooth((z - low) / (high - low))
            return weights((a, 1 - t), (b, t))
    return {'Neck': 1.0}


def shoulder_field(q, p):
    """Continuous torso weights: axial chain, clavicle over deltoid/trapezius, humerus near socket."""
    q = Vector(q)
    side = 'L' if q.x >= 0 else 'R'
    pivot, elbow, _ = arm_points(p, side)
    d = (elbow - pivot).normalized()
    lateral = smooth((abs(q.x) - .13 * p['arm_r']) / (.30 * p['arm_r']))
    band = smooth((q.z - (pivot.z - .40)) / .24) * (1 - smooth((q.z - (pivot.z + .27)) / .14))
    region = lateral * band
    along = (q - pivot).dot(d)
    upper = region * smooth((along + .01) / .20) * .92
    clav = region * (1 - upper) * .78
    axial = axial_weights(q.z)
    # Neck influence must not drag the lateral trapezius with head turns.
    neck = axial.pop('Neck', 0.0)
    keep = neck * (1 - smooth((abs(q.x) - .12) / .16))
    axial['Neck'] = keep
    axial['Chest'] = axial.get('Chest', 0.0) + neck - keep
    rest = max(0.0, 1 - upper - clav)
    pairs = [(k, v * rest) for k, v in axial.items()]
    pairs += [('UpperArm_' + side, upper), ('Clavicle_' + side, clav)]
    return weights(*pairs)


def arm_field(q, p, side):
    q = Vector(q)
    pivot, elbow, wrist = arm_points(p, side)
    d = (elbow - pivot).normalized()
    fdir = (wrist - elbow).normalized()
    upper_len, fore_len = (elbow - pivot).length, (wrist - elbow).length
    along = (q - pivot).dot(d)
    result = shoulder_field(q, p)
    full = smooth((along - .05) / .22)
    result = blend(result, {'UpperArm_' + side: 1.0}, full)
    fore = smooth(((q - elbow).dot(fdir) + .09) / .20) if along > upper_len - .2 else 0.0
    result = blend(result, {'Forearm_' + side: 1.0}, fore)
    hand = smooth(((q - elbow).dot(fdir) - (fore_len - .07)) / .12) * .65
    return blend(result, {'Hand_' + side: 1.0}, hand)


def tail_weights(t):
    """t: 0 at the tail tip, 1 at the abdomen join."""
    f = clamp((1 - t) * 8, 0, 7.999)
    i = int(f)
    b = smooth(f - i)
    return weights(('Tail_%02d' % min(8, i + 1), 1 - b), ('Tail_%02d' % min(8, i + 2), b))


# ----------------------------------------------------------------------------------- the body

def build_body(part, p):
    n = N_BODY
    tail_rows = list(reversed(catmull(p['tail'], 5)))
    rows = []
    for r in tail_rows:
        rows.append(dict(p=Vector(r[:3]), rx=r[3], ryf=r[4], ryb=r[4], e=2.2, torso=False))
    for z, w, f, b, e in p['torso']:
        rows.append(dict(p=Vector((0, 0, z)), rx=w, ryf=f, ryb=b, e=e, torso=True))
    rows.append(dict(p=Vector((0, .01, p['neck_top'])), rx=.19, ryf=.18, ryb=.18, e=2.0, torso=True))
    first_torso = len(tail_rows)
    lo, hi = first_torso + p['hole'][0], first_torso + p['hole'][1]
    holes = {'L': (lo, hi, [(-2 + k) % n for k in range(4)]),
             'R': (lo, hi, [(6 + k) % n for k in range(4)])}

    arc, previous_axis, rings = 0.0, Vector((1, 0, 0)), []
    for i, row in enumerate(rows):
        c = row['p']
        if i:
            arc += (c - rows[i - 1]['p']).length
        t = (Vector(rows[min(i + 1, len(rows) - 1)]['p']) - Vector(rows[max(0, i - 1)]['p'])).normalized()
        axis = previous_axis - t * previous_axis.dot(t)
        if axis.length < .05:
            axis = Vector((0, 1, 0)) - t * t.y
        axis.normalize()
        if i >= len(tail_rows) - 12:
            # Distribute the roll toward the torso frame (+X) over the last tail rows; no pinch at the join.
            target = Vector((1, 0, 0)) - t * t.x
            if target.length > .05:
                target.normalize()
                angle = math.atan2(t.dot(axis.cross(target)), axis.dot(target))
                axis = Quaternion(t, angle / max(1, len(tail_rows) - i)) @ axis
        if row['torso']:
            axis = Vector((1, 0, 0))
        across = t.cross(axis).normalized()
        previous_axis = axis.copy()
        # Ventral side: down on the floor coil, front (-Y) once the body rises.
        rise = smooth((c.z - .30) / .55) if not row['torso'] else 1.0
        ventral = (Vector((0, 0, -1)).lerp(Vector((0, -1, 0)), rise)).normalized()
        tail_t = i / max(1, len(tail_rows) - 1)
        ring = []
        for j in range(n):
            a = 2 * pi * j / n
            cx, sy = superellipse(a, row['e'])
            ry = row['ryb'] if sy > 0 else row['ryf']
            q = c + axis * cx * row['rx'] + across * sy * ry
            radial = (q - c).normalized()
            vd = radial.dot(ventral)
            belly = smooth((vd - .30) / .45)
            dorsal = smooth((-vd - .15) / .55)
            if not row['torso'] and i < len(tail_rows) - 1:
                w = tail_weights(tail_t)
                if i >= len(tail_rows) - 4:
                    w = blend(w, {'COG': 1.0}, smooth((i - (len(tail_rows) - 4)) / 3))
            else:
                w = shoulder_field(q, p)
            ring.append(part.vertex(q, w, 1.0, belly, dorsal, arc))
        rings.append(ring)

    for i in range(len(rows) - 1):
        for j in range(n):
            if any(a <= i < b and j in cols for a, b, cols in holes.values()):
                continue
            part.face((rings[i][j], rings[i][(j + 1) % n], rings[i + 1][(j + 1) % n], rings[i + 1][j]), 'skin')
    part.face(tuple(reversed(rings[0])), 'skin')
    part.face(tuple(rings[-1]), 'skin')

    for side, (a, b, cols) in holes.items():
        boundary = [rings[a][cols[0]]] + [rings[a][c] for c in cols[1:]] + [rings[a][(cols[-1] + 1) % n]]
        boundary += [rings[k][(cols[-1] + 1) % n] for k in range(a + 1, b)]
        boundary += [rings[b][c] for c in [(cols[-1] + 1) % n] + list(reversed(cols))]
        boundary += [rings[k][cols[0]] for k in range(b - 1, a, -1)]
        boundary = list(dict.fromkeys(boundary))
        build_arm(part, p, side, boundary, arc)
    return rings


def build_arm(part, p, side, boundary, arc):
    pivot, elbow, wrist = arm_points(p, side)
    d = (elbow - pivot).normalized()
    fdir = (wrist - elbow).normalized()
    k = p['arm_r']
    pts = [Vector(part.v[i]) for i in boundary]
    center = sum(pts, Vector()) / len(pts)
    u, v = perpendicular_frame(d, Vector((0, 1, 0)))
    # Round the shared socket loop: even angular spacing on an ellipse around the socket center,
    # lying on the plane perpendicular to the humerus. Keeps its winding, removes the rectangle corners.
    proj = [((q - center).dot(u), (q - center).dot(v)) for q in pts]
    area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(proj, proj[1:] + proj[:1]))
    orient = 1 if area > 0 else -1
    m = len(boundary)
    fit = sum(complex(x, y) * complex(cos(-orient * 2 * pi * j / m), sin(-orient * 2 * pi * j / m))
              for j, (x, y) in enumerate(proj))
    offset = math.atan2(fit.imag, fit.real)
    angles = [offset + orient * 2 * pi * j / m for j in range(m)]
    socket_c = pivot + d * ((center - pivot).dot(d))
    for idx, a in zip(boundary, angles):
        q = socket_c + u * cos(a) * .215 * k + v * sin(a) * .20 * k
        part.v[idx] = tuple(q)
        part.w[idx] = arm_field(q, p, side) if False else shoulder_field(q, p)
    t0 = (socket_c - pivot).dot(d)
    upper_len, fore_len = (elbow - pivot).length, (wrist - elbow).length
    up_bias = (Vector((0, 0, 1)) - d * d.z).normalized()
    stations = [  # (point, radius, deltoid lift)
        (pivot + d * (t0 + .06), .225, .030), (pivot + d * (t0 + .14), .222, .022),
        (pivot + d * (t0 + .23), .200, .010), (pivot + d * (upper_len * .72), .172, 0),
        (elbow, .150, 0), (elbow + fdir * fore_len * .22, .160, 0), (elbow + fdir * fore_len * .55, .145, 0),
        (elbow + fdir * fore_len * .85, .118, 0), (wrist, .100, 0)]
    previous = boundary
    frame_u = u
    for s_i, (c, r, lift) in enumerate(stations):
        nxt = stations[min(s_i + 1, len(stations) - 1)][0]
        prv = stations[max(s_i - 1, 0)][0]
        tangent = (nxt - prv).normalized() if s_i else d
        fu = (frame_u - tangent * frame_u.dot(tangent)).normalized()
        fv = tangent.cross(fu).normalized()
        frame_u = fu
        c = c + up_bias * lift
        ring = []
        for a in angles:
            q = c + fu * cos(a) * r * k * .94 + fv * sin(a) * r * k
            # Faint belly tone on the inner forearm/biceps only.
            inner = smooth(((q - c).normalized().dot(Vector((0, 0, -1))) - .2) / .6) * .6
            ring.append(part.vertex(q, arm_field(q, p, side), 1.0, inner, 1 - inner, 0.0))
        for j in range(m):
            part.face((previous[j], previous[(j + 1) % m], ring[(j + 1) % m], ring[j]), 'skin')
        previous = ring
    part.face(tuple(reversed(previous)), 'skin')


# ------------------------------------------------------------------------------------- heads

HEAD_PROFILES = {
    # y (forward is -), half width, top height, bottom height, center z offset  (unit head size)
    # Viper wedge: widest at the jaw hinge (back), tapering to a narrow snout; not a round frog skull.
    'Soldier': [(.16, .13, .10, .10, -.02), (.08, .215, .15, .14, 0), (-.02, .222, .155, .13, .01),
                (-.14, .19, .135, .11, 0), (-.26, .15, .105, .085, -.015), (-.36, .11, .078, .06, -.03),
                (-.44, .07, .05, .04, -.045), (-.49, .035, .03, .025, -.05)],
    'Sentinel': [(.15, .15, .11, .10, -.02), (.07, .235, .165, .14, 0), (-.04, .24, .165, .13, .01),
                 (-.16, .20, .14, .11, 0), (-.28, .155, .105, .085, -.015), (-.37, .11, .075, .06, -.03),
                 (-.44, .06, .045, .035, -.045)],
    'Chieftain': [(.15, .15, .11, .10, -.02), (.07, .235, .17, .14, 0), (-.04, .245, .17, .13, .01),
                  (-.16, .205, .14, .11, 0), (-.28, .16, .105, .085, -.015), (-.37, .11, .075, .06, -.03),
                  (-.44, .06, .045, .035, -.045)],
}


def head_point(prof, y, a, size, x0, z0, off=0.0):
    """Point on the head surface at section y (interpolated) and angle a (0=+X, pi/2=top)."""
    ys = [s[0] for s in prof]
    y = clamp(y, min(ys), max(ys))
    for s0, s1 in zip(prof, prof[1:]):
        if s1[0] <= y <= s0[0]:
            t = (s0[0] - y) / max(1e-6, s0[0] - s1[0])
            sec = [s0[k] + (s1[k] - s0[k]) * t for k in range(5)]
            break
    cx, sz = superellipse(a, 2.7)
    h = sec[2] if sz > 0 else sec[3]
    return Vector((x0 + cx * (sec[1] + off) * size, y * size, z0 + (sec[4] + sz * (h + off)) * size))


def plate(part, outline, normal, depth, color, w, side_color=None, **attrs):
    """Raised plate: outline on a surface, top face offset along the normal, beveled sides."""
    c = sum(outline, Vector()) / len(outline)
    base = [part.vertex(q, w, **attrs) for q in outline]
    top = [part.vertex(c + (q - c) * .86 + normal * depth, w, **attrs) for q in outline]
    for i in range(len(outline)):
        part.face((base[i], base[(i + 1) % len(base)], top[(i + 1) % len(top)], top[i]), side_color or color)
    part.face(tuple(top), color)
    part.face(tuple(reversed(base)), side_color or color)


def build_head(body, jaw_part, p, suffix, x0, z0, size):
    kind = p['kind']
    prof = HEAD_PROFILES[kind]
    bone = 'Head' + suffix
    wh = {bone: 1.0}
    rings = []
    for y, w, ht, hb, cz in catmull(prof, 2):
        ring = []
        for j in range(12):
            a = 2 * pi * j / 12
            q = head_point(prof, y, a, size, x0, z0)
            under = smooth((-(sin(a)) - .35) / .5)
            ring.append(body.vertex(q, wh, 1.0, under, 1 - under, 0))
        rings.append(ring)
    body.loft(rings, 'skin')
    # Lower jaw: a wedge under the front half, hinged at the Jaw bone.
    jw = {'Jaw' + suffix: 1.0}
    jrings = []
    for y, w, h, cz in [(.02, .17, .06, -.12), (-.12, .165, .06, -.125), (-.24, .125, .05, -.11),
                        (-.34, .085, .038, -.09), (-.41, .045, .024, -.08)]:
        ring = []
        for j in range(10):
            a = 2 * pi * j / 10
            cx, sz = superellipse(a, 2.6)
            q = Vector((x0 + cx * w * size, y * size, z0 + (cz + sz * h) * size))
            ring.append(body.vertex(q, jw, 1.0, .9, 0, 0))
        jrings.append(ring)
    body.loft(jrings, 'skin')
    # Mouth seam: a dark inset band along the upper lip line reads as a closed mouth.
    for s in (-1, 1):
        line = [head_point(prof, y, s * -.22 if s > 0 else pi + .22, size, x0, z0, .004) for y in (-.10, -.22, -.34, -.41)]
        for q0, q1 in zip(line, line[1:]):
            n = Vector((s, 0, -.3)).normalized()
            plate(body, [q0 + Vector((0, 0, .012)), q1 + Vector((0, 0, .010)), q1 - Vector((0, 0, .010)),
                         q0 - Vector((0, 0, .012))], n, .004, 'mouth', wh)
    # Nostrils.
    for s in (-1, 1):
        c = head_point(prof, -.43, pi / 2 - s * .55, size, x0, z0, .003)
        n = (c - Vector((x0, c.y, z0))).normalized()
        plate(body, [c + Vector((s * .018, 0, 0)) * size, c + Vector((0, -.012, .010)) * size,
                     c - Vector((s * .018, 0, 0)) * size, c + Vector((0, .012, -.006)) * size], n, -.006, 'nostril', wh)
    # Eyes (per card) with slit pupils, plus angular brow ridges for menace.
    # Eyes on the sides of the skull, forward of the jaw hinge (snake), not on the crown (frog).
    eyes = {'Soldier': [(-.17, .36, .085, .042), (-.07, .70, .075, .038)],
            'Sentinel': [(-.15, .42, .095, .046)],
            'Chieftain': [(-.15, .42, .09 if not suffix else .10, .044 if not suffix else .048)]}[kind]
    for s in (-1, 1):
        for y, a, ew, eh in eyes:
            ang = a if s > 0 else pi - a
            c = head_point(prof, y, ang, size, x0, z0, .002)
            n = (c - head_point(prof, y, ang, size, x0, z0, -.05)).normalized()
            t1 = Vector((0, -1, 0))
            t2 = n.cross(t1).normalized()
            outline = [c + (t1 * cos(k * pi / 6) * ew + t2 * sin(k * pi / 6) * eh * (abs(sin(k * pi / 6)) ** .3)) * size
                       for k in range(12)]
            plate(body, outline, n, .012 * size, 'eye', wh, 'brow')
            slit = [c + n * .014 * size + (t1 * cos(k * pi / 2) * ew * .16 + t2 * sin(k * pi / 2) * eh * .85) * size
                    for k in range(4)]
            plate(body, slit, n, .003 * size, 'pupil', wh)
            # Brow ridge: a heavy wedge just above and forward of the eye.
            above = ang + (.30 if s > 0 else -.30)
            bc = head_point(prof, y - .01, above, size, x0, z0, .0)
            bn = (bc - head_point(prof, y - .01, above, size, x0, z0, -.05)).normalized()
            bt = t1
            b2 = bn.cross(bt).normalized()
            brow = [bc + (bt * ew * 1.1 + b2 * eh * .1) * size, bc + (-bt * ew * .95 + b2 * eh * .7) * size,
                    bc + (-bt * ew * .85 + b2 * eh * .1) * size, bc + (bt * ew * .9 - b2 * eh * .35) * size]
            plate(body, brow, bn, .018 * size, 'brow', wh)
    if kind in ('Sentinel', 'Chieftain'):
        # Third eye on the forehead, vertical.
        # Flush with the brow slope (surface normal), so it reads as an eye, not a raised ring.
        c = head_point(prof, -.13, pi / 2, size, x0, z0, .001)
        n = (c - head_point(prof, -.13, pi / 2, size, x0, z0, -.05)).normalized()
        n = (n + Vector((0, -.6, 0))).normalized()
        t1 = Vector((1, 0, 0))
        t2 = n.cross(t1).normalized()
        ew, eh = .032, .07
        outline = [c + (t1 * cos(k * pi / 6) * ew + t2 * sin(k * pi / 6) * eh) * size for k in range(12)]
        plate(body, outline, n, .006 * size, 'eye', wh, 'brow')
        plate(body, [c + n * .008 * size + (t1 * cos(k * pi / 2) * ew * .3 + t2 * sin(k * pi / 2) * eh * .7) * size
                     for k in range(4)], n, .003 * size, 'pupil', wh)
    # Forked tongue flick from the jaw tip (card identity), on the Jaw bone.
    tip = Vector((x0, -.44 * size, z0 - .09 * size))
    for s in (-1, 1):
        a = tip
        b = tip + Vector((s * .03, -.07, -.10)) * size
        u = Vector((1, 0, 0))
        plate(body, [a - u * .012 * size, a + u * .012 * size, b + u * .004 * size, b - u * .004 * size],
              Vector((0, -1, .2)).normalized(), .01 * size, 'tongue', jw)
    if p['hood'] and not suffix:
        # The Chieftain's hood stays narrower so the two side necks keep clear silhouettes.
        build_hood(body, suffix, x0, z0, size * (.80 if kind == 'Chieftain' else 1.0))


def build_hood(part, suffix, x0, z0, size):
    """Cobra hood: a thick shell flaring from the neck behind the head; edges curl forward."""
    rows = []
    for zt, hw in [(-1.05, .16), (-.88, .36), (-.70, .46), (-.52, .44), (-.36, .32), (-.24, .18)]:
        row = []
        for k in range(9):
            t = -1 + 2 * k / 8
            x = t * hw
            y = .08 - .16 * (t * t) * (hw / .40)
            row.append(Vector((x0 + x * size, y * size, z0 + zt * size)))
        rows.append(row)
    thickness = .05 * size
    outer, inner = [], []
    for zi, row in enumerate(rows):
        lift = smooth((zi) / (len(rows) - 1))
        w = blend({'Neck' + suffix: 1.0}, {'Head' + suffix: 1.0}, lift)
        outer.append([part.vertex(q + Vector((0, thickness, 0)), w, 1.0, 0, 1, 0) for q in row])
        inner.append([part.vertex(q, w, 1.0, 1, 0, 0) for q in row])
    for zi in range(len(rows) - 1):
        for k in range(8):
            part.face((outer[zi][k + 1], outer[zi][k], outer[zi + 1][k], outer[zi + 1][k + 1]), 'skin')
            part.face((inner[zi][k], inner[zi][k + 1], inner[zi + 1][k + 1], inner[zi + 1][k]), 'skin')
    for zi in range(len(rows) - 1):
        for k in (0, 8):
            a, b = (outer, inner) if k == 0 else (inner, outer)
            part.face((a[zi][k], b[zi][k], b[zi + 1][k], a[zi + 1][k]), 'skin')
    for k in range(8):
        part.face((outer[0][k], outer[0][k + 1], inner[0][k + 1], inner[0][k]), 'skin')
        part.face((inner[-1][k], inner[-1][k + 1], outer[-1][k + 1], outer[-1][k]), 'skin')


def build_extra_neck(part, p, suffix, x0, z0, size):
    rings = []
    # Heads sit at y ~0; the necks arc slightly backward so raised arms pass in front of them.
    pts = [(x0 * .30, .08, 2.10, .21, .19), (x0 * .58, .10, 2.32, .18, .17), (x0 * .86, .07, 2.55, .16, .15),
           (x0, .03, z0 - .12 * size, .15, .15)]
    for i, (x, y, z, rx, ry) in enumerate(catmull(pts, 3)):
        c = Vector((x, y, z))
        t = i / (3 * (len(pts) - 1))
        w = blend({'Chest': 1.0}, {'Neck' + suffix: 1.0}, smooth(t * 1.6))
        ring = []
        for j in range(10):
            a = 2 * pi * j / 10
            q = c + Vector((cos(a) * rx, sin(a) * ry, 0))
            ring.append(part.vertex(q, w, 1.0, smooth((-sin(a) - .3) / .5), 0, 0))
        rings.append(ring)
    part.loft(rings, 'skin')


# -------------------------------------------------------------------------------------- hands

def build_hands(part, p):
    for side in ('L', 'R'):
        wrist = arm_points(p, side)[2]
        g = grip_point(p, side)
        bone = 'Hand_' + side
        s = 1 if side == 'L' else -1
        w = {bone: 1.0}
        palm = g + Vector((0, .075, .005))
        sections = [(wrist, .092, .078), (wrist.lerp(palm, .5), .108, .08), (palm, .11, .074),
                    (palm + Vector((0, 0, -.07)), .08, .054)]
        rings = []
        for c, rx, ry in sections:
            rings.append([part.vertex(c + Vector((cos(a) * rx, sin(a) * ry, 0)), w, 1.0, 0, 1, 0)
                          for a in [2 * pi * j / 10 for j in range(10)]])
        part.loft(rings, 'skin')
        # Four curled digits wrapping the vertical handle, then a thumb over the top.
        for i in range(4):
            z = g.z + .075 - i * .048
            ring_list = []
            for k in range(6):
                a = math.radians(40 + k * 46)
                c = Vector((g.x + cos(a) * .068, g.y + sin(a) * .068, z - .008 * k / 5))
                r = .027 if k < 4 else .021
                ring_list.append([part.vertex(c + Vector((0, 0, cos(b) * r)) + Vector((cos(a), sin(a), 0)) * sin(b) * r,
                                              w, 1.0, 0, 1, 0) for b in [2 * pi * j / 6 for j in range(6)]])
            part.loft(ring_list, 'skin')
        thumb = [g + Vector((-s * .08, .04, .11)), g + Vector((-s * .085, -.022, .07)),
                 g + Vector((-s * .05, -.07, .03)), g + Vector((s * .01, -.075, .012))]
        ring_list = []
        for k, c in enumerate(thumb):
            r = .036 - .005 * k
            ring_list.append([part.vertex(c + Vector((cos(a) * r, 0, sin(a) * r)), w, 1.0, 0, 1, 0)
                              for a in [2 * pi * j / 6 for j in range(6)]])
        part.loft(ring_list, 'skin')


# ------------------------------------------------------------------------------------ costume

def torso_point(p, a, z, off=0.0):
    rows = p['torso']
    zs = [r[0] for r in rows]
    z = clamp(z, zs[0], zs[-1])
    for r0, r1 in zip(rows, rows[1:]):
        if r0[0] <= z <= r1[0]:
            t = (z - r0[0]) / max(1e-6, r1[0] - r0[0])
            r = [r0[k] + (r1[k] - r0[k]) * t for k in range(5)]
            break
    cx, sy = superellipse(a, r[4])
    ry = r[3] if sy > 0 else r[2]
    return Vector((cx * (r[1] + off), sy * (ry + off), z))


def conform_shell(part, p, a0, a1, z0, z1, off, thickness, color, rim_color=None, na=10, nz=6, bone=None,
                  closed=False):
    """Armor shell following the torso surface between two angles and heights."""
    grid_o, grid_i = [], []
    for iz in range(nz + 1):
        z = z0 + (z1 - z0) * iz / nz
        ro, ri = [], []
        for ia in range(na if closed else na + 1):
            a = a0 + (a1 - a0) * ia / na
            qi = torso_point(p, a, z, off)
            qo = torso_point(p, a, z, off + thickness)
            w = {bone: 1.0} if bone else shoulder_field(qi, p)
            ri.append(part.vertex(qi, w))
            ro.append(part.vertex(qo, w))
        grid_o.append(ro)
        grid_i.append(ri)
    cols = len(grid_o[0])
    nxt = (lambda ia: (ia + 1) % cols) if closed else (lambda ia: ia + 1)
    for iz in range(nz):
        for ia in range(na):
            part.face((grid_o[iz][ia], grid_o[iz][nxt(ia)], grid_o[iz + 1][nxt(ia)], grid_o[iz + 1][ia]), color)
            part.face((grid_i[iz][nxt(ia)], grid_i[iz][ia], grid_i[iz + 1][ia], grid_i[iz + 1][nxt(ia)]), rim_color or color)
    rc = rim_color or color
    if not closed:
        for iz in range(nz):
            for ia in (0, na):
                a, b = (grid_i, grid_o) if ia == 0 else (grid_o, grid_i)
                part.face((a[iz][ia], b[iz][ia], b[iz + 1][ia], a[iz + 1][ia]), rc)
    for ia in range(na):
        part.face((grid_i[0][ia], grid_i[0][nxt(ia)], grid_o[0][nxt(ia)], grid_o[0][ia]), rc)
        part.face((grid_o[nz][ia], grid_o[nz][nxt(ia)], grid_i[nz][nxt(ia)], grid_i[nz][ia]), rc)


def strap(part, p, path, width, thickness, color, weight=None):
    """Flat strap lying on the torso: path given as (angle, z) samples."""
    rows = []
    pts = [torso_point(p, a, z, .012) for a, z in path]
    for i, c in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(0, i - 1)]).normalized()
        n = Vector((c.x, c.y, 0)).normalized()
        side = n.cross(d).normalized() * width * .5
        w = weight or shoulder_field(c, p)
        rows.append([part.vertex(c - side, w), part.vertex(c + side, w),
                     part.vertex(c + side + n * thickness, w), part.vertex(c - side + n * thickness, w)])
    part.loft(rows, color)


def bracer(part, p, side, color, band_color, flare=1.0):
    pivot, elbow, wrist = arm_points(p, side)
    fdir = (wrist - elbow).normalized()
    fore_len = (wrist - elbow).length
    u, v = perpendicular_frame(fdir, Vector((0, 0, 1)))
    k = p['arm_r']
    rings = []
    for t, r, col in [(.30, .188, color), (.42, .192, color), (.80, .160 * flare, color), (.93, .158 * flare, color)]:
        c = elbow + fdir * fore_len * t
        rings.append([part.vertex(c + (u * cos(a) + v * sin(a)) * r * k,
                                  arm_field(c + (u * cos(a) + v * sin(a)) * .1, p, side)) for a in
                      [2 * pi * j / 10 for j in range(10)]])
    part.loft(rings, color)
    for t in (.50, .70):
        c = elbow + fdir * fore_len * t
        band = [[part.vertex(c + fdir * dz + (u * cos(a) + v * sin(a)) * (.172 * k + .012),
                             arm_field(c, p, side)) for a in [2 * pi * j / 10 for j in range(10)]]
                for dz in (-.025, .025)]
        part.loft(band, band_color)


def pauldron(part, p, side, color, rim):
    """Layered leather plates over the deltoid, weighted like the skin beneath."""
    pivot, elbow, _ = arm_points(p, side)
    d = (elbow - pivot).normalized()
    u, v = perpendicular_frame(d, Vector((0, 1, 0)))
    up = (Vector((0, 0, 1)) - d * d.z).normalized()
    k = p['arm_r']
    for layer, (t0, t1, r0, grow) in enumerate([(-.02, .20, .285, 0), (.12, .30, .265, .02)]):
        rows_o, rows_i = [], []
        for it in range(5):
            t = t0 + (t1 - t0) * it / 4
            c = pivot + d * t + up * (.06 - .02 * layer)
            ro, ri = [], []
            for ia in range(9):
                # Cover the top 240 degrees of the arm (outer and upper faces).
                a = math.radians(-30 + 240 * ia / 8)
                dirv = (up * sin(a) + (d.cross(up)).normalized() * cos(a) * (1 if side == 'L' else -1))
                dirv = (dirv - d * dirv.dot(d)).normalized()
                r = (r0 + grow) * k * (1 - .15 * (it / 4))
                q = c + dirv * r
                w = arm_field(q, p, side)
                ri.append(part.vertex(q, w))
                ro.append(part.vertex(q + dirv * .035, w))
            rows_o.append(ro)
            rows_i.append(ri)
        for it in range(4):
            for ia in range(8):
                part.face((rows_o[it][ia], rows_o[it + 1][ia], rows_o[it + 1][ia + 1], rows_o[it][ia + 1]), color)
                part.face((rows_i[it][ia + 1], rows_i[it + 1][ia + 1], rows_i[it + 1][ia], rows_i[it][ia]), rim)
        for it in range(4):
            for ia in (0, 8):
                a, b = (rows_i, rows_o) if ia == 0 else (rows_o, rows_i)
                part.face((a[it][ia], a[it + 1][ia], b[it + 1][ia], b[it][ia]), rim)
        for ia in range(8):
            part.face((rows_i[0][ia], rows_i[0][ia + 1], rows_o[0][ia + 1], rows_o[0][ia]), rim)
            part.face((rows_o[4][ia], rows_o[4][ia + 1], rows_i[4][ia + 1], rows_i[4][ia]), rim)


def costume(armor, detail, p):
    kind = p['kind']
    FRONT = -pi / 2
    if kind == 'Soldier':
        pauldron(armor, p, 'L', 'leather', 'leather_dark')
        for side in ('L', 'R'):
            bracer(armor, p, side, 'leather', 'leather_dark')
        # Baldric: left shoulder across the chest to the right hip, and back up behind.
        # Over the top of the left shoulder (above the arm socket), down the front to the right hip.
        path = [(math.radians(a), z) for a, z in [(60, 2.02), (30, 2.15), (0, 2.19), (-30, 2.15), (-60, 2.02),
                                                   (-85, 1.80), (-115, 1.55), (-150, 1.34), (-175, 1.20)]]
        strap(armor, p, path, .13, .03, 'leather_dark')
        strap(armor, p, [(math.radians(a), z) for a, z in [(175, 1.20), (140, 1.40), (110, 1.62), (85, 1.84),
                                                            (60, 2.02)]], .13, .03, 'leather_dark')
        c = torso_point(p, math.radians(-78), 1.80, .05)
        n = Vector((c.x, c.y, 0)).normalized()
        t = Vector((0, 0, 1))
        s = n.cross(t).normalized()
        plate(armor, [c + (s * .06 + t * .06), c + (-s * .06 + t * .06), c + (-s * .06 - t * .06), c + (s * .06 - t * .06)],
              n, .02, 'buckle', shoulder_field(c, p))
    elif kind == 'Sentinel':
        conform_shell(armor, p, math.radians(-138), math.radians(-42), 1.42, 2.10, .012, .05, 'steel', 'steel_dark',
                      na=10, nz=6)
        for side in ('L', 'R'):
            bracer(armor, p, side, 'steel', 'steel_dark', 1.05)
        for s in (-1, 1):
            strap(armor, p, [(math.radians(90 - s * 35), 2.06), (math.radians(90 - s * 40), 1.75),
                             (math.radians(90 - s * 45), 1.48)], .10, .025, 'leather_dark')
        # Purple eye glyph on the breastplate.
        c = torso_point(p, FRONT, 1.80, .07)
        n = Vector((0, -1, 0))
        outline = [c + Vector((cos(k * pi / 6) * .13, 0, sin(k * pi / 6) * .06 * abs(sin(k * pi / 6)) ** .2)) for k in range(12)]
        plate(armor, outline, n, .01, 'glyph', shoulder_field(c, p))
        plate(armor, [c + Vector((cos(k * pi / 4) * .025, -.012, sin(k * pi / 4) * .05)) for k in range(8)], n, .004,
              'eye', shoulder_field(c, p))
    else:
        for side in ('L', 'R'):
            bracer(armor, p, side, 'gold_dark', 'gold', 1.08)
        conform_shell(armor, p, -pi, pi, 1.10, 1.30, .01, .045, 'red', 'red_dark', na=16, nz=2, bone='Spine', closed=True)
        c = torso_point(p, FRONT, 1.20, .06)
        plate(armor, [c + Vector((x, 0, z)) for x, z in [(-.09, .075), (.09, .075), (.09, -.075), (-.09, -.075)]],
              Vector((0, -1, 0)), .025, 'buckle', {'Spine': 1.0})
        # Hanging sash end on the right hip.
        top = torso_point(p, math.radians(-120), 1.12, .06)
        rows = []
        for i, (dx, dz, wd) in enumerate([(0, 0, .16), (.02, -.22, .15), (.05, -.44, .13), (.07, -.62, .12)]):
            c2 = top + Vector((-dx, -.02, dz))
            w = {'Spine': 1.0} if i < 2 else {'COG': 1.0}
            rows.append([armor.vertex(c2 + Vector((-wd / 2, 0, 0)), w), armor.vertex(c2 + Vector((wd / 2, 0, 0)), w),
                         armor.vertex(c2 + Vector((wd / 2, .03, 0)), w), armor.vertex(c2 + Vector((-wd / 2, .03, 0)), w)])
        armor.loft(rows, 'red')
        # Gold eye amulet on a V chain.
        strap(armor, p, [(math.radians(-55), 2.16), (math.radians(-72), 2.02), (FRONT, 1.92)], .03, .015, 'gold')
        strap(armor, p, [(FRONT, 1.92), (math.radians(-108), 2.02), (math.radians(-125), 2.16)], .03, .015, 'gold')
        c = torso_point(p, FRONT, 1.80, .03)
        n = Vector((0, -1, 0))
        tri = [c + Vector((-.13, 0, .10)), c + Vector((.13, 0, .10)), c + Vector((0, 0, -.13))]
        plate(armor, tri, n, .035, 'gold', shoulder_field(c, p), 'gold_dark')
        plate(armor, [c + Vector((cos(k * pi / 4) * .045, -.036, .02 + sin(k * pi / 4) * .025)) for k in range(8)],
              n, .006, 'eye', shoulder_field(c, p))


# ------------------------------------------------------------------------------------ weapons

def blade(part, outline, origin, u, v, thickness, color, edge_color=None, w=None):
    """Diamond-section blade from a 2D outline (u, v): thick spine, sharpened edge ring."""
    n = u.cross(v).normalized()
    pts = [origin + u * a + v * b for a, b in outline]
    c = sum(pts, Vector()) / len(pts)
    edge = [part.vertex(q, w) for q in pts]
    front = [part.vertex(c + (q - c) * .78 + n * thickness * .5, w) for q in pts]
    back = [part.vertex(c + (q - c) * .78 - n * thickness * .5, w) for q in pts]
    m = len(pts)
    for i in range(m):
        j = (i + 1) % m
        part.face((edge[i], edge[j], front[j], front[i]), edge_color or color)
        part.face((edge[j], edge[i], back[i], back[j]), edge_color or color)
    part.face(tuple(front), color)
    part.face(tuple(reversed(back)), color)


def rod(part, a, b, r, color, sides=8, w=None):
    d = (b - a).normalized()
    u, v = perpendicular_frame(d)
    rings = [[part.vertex(c + (u * cos(t) + v * sin(t)) * r, w) for t in [2 * pi * j / sides for j in range(sides)]]
             for c in (a, b)]
    part.loft(rings, color)


def build_weapon(p, side):
    kind = p['kind']
    g = grip_point(p, side)
    s = 1 if side == 'L' else -1
    out = Vector((s, 0, 0))
    up = Vector((0, 0, 1))
    part = Part('Weapon_' + ('Off' if side == 'L' else 'Main'), 'Equipment')
    if kind == 'Soldier':
        rod(part, g + up * -.62, g + up * 1.42, .036, 'wood')
        for k in range(6):  # rope wrap
            z = -.20 + k * .08
            rod(part, g + up * z, g + up * (z + .05), .046, 'rope')
        rod(part, g + up * -.70, g + up * -.62, .05, 'steel_dark')
        head = g + up * 1.18
        rod(part, head + up * -.05, head + up * .20, .05, 'steel_dark')
        blade(part, [(.02, .16), (.16, .24), (.44, .40), (.52, .12), (.50, -.12), (.40, -.36),
                     (.18, -.16), (.02, -.08)], head, out, up, .07, 'steel', 'steel')
        blade(part, [(-.02, .06), (-.20, .02), (-.02, -.05)], head, out, up, .05, 'steel_dark')
    elif kind == 'Sentinel':
        rod(part, g + up * -1.10, g + up * 1.85, .033, 'wood')
        for z in (-1.10, -.14, .14, 1.70):
            rod(part, g + up * z, g + up * (z + .07), .045, 'steel_dark')
        base = g + up * 1.80
        blade(part, [(-.05, 0), (.08, .03), (.15, .40), (.06, .82), (-.14, 1.24), (-.28, 1.36), (-.22, 1.04),
                     (-.19, .66), (-.28, .50), (-.10, .40), (-.08, .12)], base, out, up, .07, 'steel', 'steel')
    else:
        rod(part, g + up * -.16, g + up * .16, .038, 'leather_dark')
        rod(part, g + up * -.22, g + up * -.16, .055, 'gold')
        guard = g + up * .19
        blade(part, [(-.20, -.03), (.20, -.03), (.22, .03), (-.22, .03)], guard, out, up, .06, 'gold', 'gold_dark')
        blade(part, [(-.08, .02), (.09, .02), (.19, .52), (.32, 1.04), (.52, 1.45), (.39, 1.61), (.21, 1.30),
                     (.05, .80), (-.05, .38)], guard, out, up, .06, 'steel', 'steel')
    return part


# --------------------------------------------------------------------------------- animation

def set_pose(rig, pose):
    """pose: {bone: [(axis, degrees), ...]} armature-space rotations in rest frame; '@COG': (x,y,z)."""
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = Quaternion()
        pb.location = (0, 0, 0)
    if '@COG' in pose and '@Tail_01' not in pose:
        # Keep the floor coil planted: the tail root cancels the COG translation.
        pose = dict(pose)
        pose['@Tail_01'] = tuple(-v for v in pose['@COG'])
    for name, rots in pose.items():
        if name.startswith('@'):
            pb = rig.pose.bones.get(name[1:])
            if pb:
                basis = pb.bone.matrix_local.to_quaternion()
                pb.location = basis.inverted() @ Vector(rots)
            continue
        if name.startswith('>'):
            continue
        pb = rig.pose.bones.get(name)
        if pb is None:
            continue
        basis = pb.bone.matrix_local.to_quaternion()
        q = Quaternion()
        for axis, deg in rots:
            q = Quaternion(Vector(axis), math.radians(deg)) @ q
        pb.rotation_quaternion = basis.inverted() @ q @ basis
    aims = [(k[1:], v) for k, v in pose.items() if k.startswith('>')]
    if aims:
        bpy.context.view_layer.update()
    for hand, (direction, blade_out) in aims:
        # Orient the held weapon: socket axis -> direction, weapon outward edge -> blade_out.
        side = hand[-1]
        sock = rig.pose.bones['OffHand' if side == 'L' else 'MainHand']
        pb = rig.pose.bones[hand]
        sm = sock.matrix.to_3x3()
        cur_y = sm.col[1].normalized()
        cur_out = (sm.col[0] * (1 if side == 'L' else -1)).normalized()
        want_y = Vector(direction).normalized()
        want_out = Vector(blade_out) - want_y * Vector(blade_out).dot(want_y)
        if want_out.length < 1e-4:
            want_out = cur_out - want_y * cur_out.dot(want_y)
        want_out.normalize()
        cur = Matrix((cur_out, cur_y, cur_out.cross(cur_y))).transposed()
        want = Matrix((want_out, want_y, want_out.cross(want_y))).transposed()
        rot = (want @ cur.inverted()).to_4x4()
        m = rot @ pb.matrix
        m.translation = pb.head.copy()
        pb.matrix = m
        bpy.context.view_layer.update()


def merge(*poses):
    out = {}
    for pose in poses:
        for k, v in pose.items():
            if k.startswith('>'):
                out[k] = v
            elif k.startswith('@'):
                a = out.get(k, (0, 0, 0))
                out[k] = tuple(x + y for x, y in zip(a, v))
            else:
                out[k] = list(out.get(k, [])) + list(v)
    return out


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)


def arm(side, abduct=0, flex=0, elbow=0, twist=0, clav=None, hand=()):
    """Arm pose with automatic clavicle share (25% of elevation, capped 28 degrees)."""
    s = 1 if side == 'L' else -1
    elevation = max(abs(abduct), abs(flex))
    c = min(28, .25 * elevation) if clav is None else clav
    pose = {'Clavicle_' + side: [(Y, -s * c * (abs(abduct) / max(1e-6, elevation)) if abduct > 0 else 0),
                                 (X, -c * (abs(flex) / max(1e-6, elevation)) if flex > 0 else 0)],
            'UpperArm_' + side: [(Y, -s * abduct), (X, -flex), (Z, s * twist)],
            'Forearm_' + side: [(X, -elbow)]}
    if hand:
        pose['Hand_' + side] = list(hand)
    return pose


def tail_wave(amp, phase, axis=Z, falloff=1.0):
    return {'Tail_%02d' % (i + 1): [(axis, amp * sin(phase + i * .75) * (1 + .15 * i * falloff))] for i in range(8)}


def stance(kind):
    if kind == 'Soldier':
        # Axe carried diagonally across the body toward the left shoulder, as on the card.
        return merge(arm('R', abduct=-10, flex=20, elbow=58),
                     arm('L', abduct=8, flex=14, elbow=38),
                     {'>Hand_R': ((.42, -.30, .86), (-.55, -.65, .30)),
                      'Spine': [(X, 4)], 'Chest': [(X, 3), (Z, -4)], 'Neck': [(X, -6)], 'Head': [(X, 8)]})
    if kind == 'Sentinel':
        # Glaive upright at the side, off hand guarding the chest.
        return merge(arm('R', abduct=6, flex=20, elbow=50),
                     arm('L', abduct=-4, flex=30, elbow=95),
                     {'>Hand_R': ((-.06, -.05, 1), (-.55, -.85, 0)),
                      'Spine': [(X, 3)], 'Neck': [(X, -8)], 'Head': [(X, 10)]})
    # Chieftain: both scimitars held low and outward, blades flaring like the card.
    return merge(arm('R', abduct=16, flex=24, elbow=45), arm('L', abduct=16, flex=24, elbow=45),
                 {'>Hand_R': ((-.22, -.12, 1), (-1, 0, .1)), '>Hand_L': ((.22, -.12, 1), (1, 0, .1))},
                 {'Spine': [(X, 4)], 'Chest': [(X, 2)], 'Neck': [(X, -6)], 'Head': [(X, 8)],
                  'Neck_L': [(Y, -10)], 'Neck_R': [(Y, 10)], 'Head_L': [(Z, -18)], 'Head_R': [(Z, 18)]})


def attack_keys(kind):
    """Anticipation (f14), strike (f20), impact hold (f24), follow-through (f32), recovery (f48)."""
    if kind == 'Soldier':
        wind = merge(arm('R', abduct=20, flex=150, elbow=70), {'>Hand_R': ((.05, .55, .83), (0, -.6, .8))},
                     arm('L', abduct=25, flex=40, elbow=50),
                     {'Spine': [(X, -6), (Z, 10)], 'Chest': [(X, -10), (Z, 14)], 'Head': [(X, -6), (Z, -8)],
                      '@COG': (0, .06, -.10)}, tail_wave(9, 1.2))
        hit = merge(arm('R', abduct=6, flex=48, elbow=10), {'>Hand_R': ((.05, -.85, -.52), (0, -.5, -.85))},
                    arm('L', abduct=30, flex=10, elbow=30),
                    {'Spine': [(X, 12), (Z, -12)], 'Chest': [(X, 14), (Z, -16)], 'Head': [(X, 8), (Z, 8)],
                     '@COG': (0, -.18, -.04)}, tail_wave(-12, 1.2))
    elif kind == 'Sentinel':
        wind = merge(arm('R', abduct=30, flex=-25, elbow=85), {'>Hand_R': ((-.2, .55, .81), (-1, 0, 0))},
                     arm('L', abduct=10, flex=55, elbow=75),
                     {'Spine': [(X, -5), (Z, 16)], 'Chest': [(Z, 14)], 'Head': [(Z, -12)], '@COG': (0, .10, -.06)},
                     tail_wave(8, 2.0))
        hit = merge(arm('R', abduct=10, flex=85, elbow=6), {'>Hand_R': ((.05, -1, .12), (-1, 0, 0))},
                    arm('L', abduct=10, flex=70, elbow=40),
                    {'Spine': [(X, 14), (Z, -10)], 'Chest': [(X, 10), (Z, -14)], 'Head': [(X, 6), (Z, 8)],
                     '@COG': (0, -.30, -.03)}, tail_wave(-10, 2.0))
    else:
        wind = merge(arm('R', abduct=55, flex=80, elbow=60), arm('L', abduct=55, flex=80, elbow=60),
                     {'>Hand_R': ((-.35, .35, .87), (-1, 0, 0)), '>Hand_L': ((.35, .35, .87), (1, 0, 0)),
                      'Neck_L': [(Y, -12), (X, -8)], 'Neck_R': [(Y, 12), (X, -8)]},
                     {'Spine': [(X, -8)], 'Chest': [(X, -8)], 'Neck': [(X, -12)], 'Head': [(X, -8)],
                      '@COG': (0, .08, -.10)}, tail_wave(7, .6))
        hit = merge(arm('R', abduct=-12, flex=70, elbow=12), arm('L', abduct=-12, flex=70, elbow=12),
                    {'>Hand_R': ((.35, -.9, -.25), (0, 0, -1)), '>Hand_L': ((-.35, -.9, -.25), (0, 0, -1))},
                    {'Spine': [(X, 14)], 'Chest': [(X, 12)], 'Neck': [(X, 10)], 'Head': [(X, 6)],
                     'Neck_L': [(Y, 6)], 'Neck_R': [(Y, -6)], '@COG': (0, -.22, -.04)}, tail_wave(-9, .6))
    return wind, hit


def create_actions(rig, kind):
    base = stance(kind)
    rig.animation_data_create()
    actions = []

    def key(action_pose_list, name, length):
        action = bpy.data.actions.new(rig.name + '|' + name)
        action.use_fake_user = True
        rig.animation_data.action = action
        previous = {}
        for frame, pose in action_pose_list:
            set_pose(rig, pose)
            for pb in rig.pose.bones:
                q = pb.rotation_quaternion.copy()
                if pb.name in previous and previous[pb.name].dot(q) < 0:
                    q.negate()
                    pb.rotation_quaternion = q
                previous[pb.name] = q
                pb.keyframe_insert('rotation_quaternion', frame=frame, group=pb.name)
                pb.keyframe_insert('location', frame=frame, group=pb.name)
        action['Purpose'] = 'V1.2 validation clip (rig/weights/grip/silhouette), not final choreography'
        actions.append(action)
        return action

    # Idle: seamless 72-frame loop; breathing, head sway, tail wave travelling toward the tip.
    idle = []
    for f in (1, 19, 37, 55, 73):
        ph = 2 * pi * (f - 1) / 72
        idle.append((f, merge(base, {'Spine': [(X, 1.2 * sin(ph))], 'Chest': [(X, 1.5 * sin(ph + .5))],
                                     'Head': [(Z, 3 * sin(ph + 1.0)), (X, 1.5 * sin(ph))], '@COG': (0, 0, .015 * sin(ph))},
                              tail_wave(3.0, ph), {'Clavicle_L': [(Y, -1.5 * sin(ph + .5))],
                                                   'Clavicle_R': [(Y, 1.5 * sin(ph + .5))]})))
    key(idle, 'Idle', 73)
    wind, hit = attack_keys(kind)
    follow = merge(base, {'Spine': [(X, 5)], 'Chest': [(X, 4)]})
    key([(1, base), (6, merge(base, {'@COG': (0, .02, -.03)})), (14, merge(base, wind)),
         (17, merge(base, wind, {'Chest': [(X, 2)]})), (20, merge(base, hit)), (24, merge(base, hit)),
         (32, merge(follow, {'@COG': (0, -.06, 0)})), (40, merge(base, {'Spine': [(X, -2)]})), (48, base)], 'Attack', 48)
    recoil = {'Spine': [(X, -10)], 'Chest': [(X, -12)], 'Neck': [(X, -10)], 'Head': [(X, -14), (Z, 10)],
              '@COG': (0, .14, 0)}
    key([(1, base), (4, merge(base, recoil, tail_wave(6, 0))), (10, merge(base, {'Spine': [(X, 3)], 'Head': [(X, 4)]})),
         (18, merge(base, {'Spine': [(X, -2)]})), (30, base)], 'HitReaction', 30)
    slump = merge({'Spine': [(X, 40), (Z, 8)], 'Chest': [(X, 30)], 'Neck': [(X, 30)], 'Head': [(X, 25), (Z, 20)],
                   'Neck_L': [(X, 35), (Y, -20)], 'Neck_R': [(X, 35), (Y, 20)], '@COG': (0, -.18, -.28)},
                  arm('R', abduct=25, flex=-10, elbow=20), arm('L', abduct=30, flex=-5, elbow=25), tail_wave(10, 1.5))
    key([(1, base), (6, merge(base, recoil)), (20, merge(base, {'Spine': [(X, 18)], '@COG': (0, -.1, -.15)})),
         (40, slump), (60, slump)], 'Death', 60)
    for action in actions:
        for fc in action_fcurves(action):
            for kp in fc.keyframe_points:
                kp.interpolation = 'BEZIER'
                kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
            fc.update()
    rig.animation_data.action = actions[0]
    bpy.context.scene.frame_set(1)
    return actions


def action_fcurves(action):
    """Blender 4.4+ layered actions keep curves in channelbags; older ones expose action.fcurves."""
    if hasattr(action, 'fcurves') and len(getattr(action, 'fcurves', [])):
        return list(action.fcurves)
    curves = []
    for layer in getattr(action, 'layers', []):
        for strip in layer.strips:
            for bag in getattr(strip, 'channelbags', []):
                curves.extend(bag.fcurves)
    return curves


# ------------------------------------------------------------------------------ scene/building

def owned_scene_name(name):
    return 'EnemyRoster' + VERSION + '_' + name


def remove_owned(name):
    scene = bpy.data.scenes.get(owned_scene_name(name))
    if scene is None:
        return
    if not scene.get('EnemyRosterV12Owned'):
        raise RuntimeError('Scene exists but is not owned by the V1.2 builder: ' + scene.name)
    if bpy.context.window.scene == scene:
        other = next(s for s in bpy.data.scenes if s != scene)
        bpy.context.window.scene = other
    objects = list(scene.objects)
    meshes = {o.data for o in objects if o.type == 'MESH'}
    arms = {o.data for o in objects if o.type == 'ARMATURE'}
    cams = {o.data for o in objects if o.type == 'CAMERA'}
    lights = {o.data for o in objects if o.type == 'LIGHT'}
    for o in objects:
        bpy.data.objects.remove(o, do_unlink=True)
    for datablocks, coll in ((meshes, bpy.data.meshes), (arms, bpy.data.armatures), (cams, bpy.data.cameras),
                             (lights, bpy.data.lights)):
        for d in datablocks:
            if d.users == 0:
                coll.remove(d)
    for a in [a for a in bpy.data.actions if a.name.startswith('Rig_' + name + VERSION + '|')]:
        bpy.data.actions.remove(a)
    for m in [m for m in bpy.data.materials if m.get('EnemyRosterV12Owner') == name]:
        bpy.data.materials.remove(m)
    world = scene.world
    bpy.data.scenes.remove(scene)
    if world and world.users == 0:
        bpy.data.worlds.remove(world)


def create_scene(name, rebuild):
    if rebuild:
        remove_owned(name)
    scene_name = owned_scene_name(name)
    if scene_name in bpy.data.scenes:
        raise RuntimeError('V1.2 candidate exists; pass rebuild=True to replace the owned scene: ' + scene_name)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    scene = bpy.data.scenes.new(scene_name)
    scene['EnemyRosterV12Owned'] = True
    scene.unit_settings.system = 'METRIC'
    scene.world = bpy.data.worlds.new(scene_name + '_World')
    scene.world.color = (.05, .05, .055)
    bpy.context.window.scene = scene
    return scene


def make_materials(name):
    kind = KIND[name]
    mats = {}
    for family in ('Organic', 'Equipment'):
        m = bpy.data.materials.new('M_' + name + '_' + family)
        m['EnemyRosterV12Owner'] = name
        m.use_nodes = True
        paint_nodes(m, kind, family)
        mats[family] = m
    return mats


def paint_nodes(material, kind, family):
    """Painted color field, evaluated per pixel (bind pose): skin = dorsal/side/belly masses with
    noise-broken boundaries, dorsal saddle bands, faint belly scutes; parts = flat PartColor. Both get
    broad value breakup, vertical brush streaks, painted cavity shading (AO) and a warm top light.
    The same tree drives the Blender preview (BSDF) and the texture bake (emission)."""
    nt = material.node_tree
    nt.nodes.clear()
    N, L = nt.nodes, nt.links
    skin = SKIN[kind]

    def node(kind_, **props):
        n = N.new(kind_)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def value(v):
        n = node('ShaderNodeValue')
        n.outputs[0].default_value = v
        return n.outputs[0]

    def rgb(c):
        n = node('ShaderNodeRGB')
        n.outputs[0].default_value = tuple(srgb_to_linear(x) for x in c) + (1.0,)
        return n.outputs[0]

    def attr(name):
        return node('ShaderNodeAttribute', attribute_type='GEOMETRY', attribute_name=name).outputs['Fac']

    def math_(op, a, b=None, clamp_=False):
        n = node('ShaderNodeMath', operation=op, use_clamp=clamp_)
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                n.inputs[i].default_value = v
            else:
                L.new(v, n.inputs[i])
        return n.outputs[0]

    def ramp(v, lo, hi):
        n = node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP')
        L.new(v, n.inputs['Value'])
        n.inputs['From Min'].default_value, n.inputs['From Max'].default_value = lo, hi
        return n.outputs['Result']

    def mix(fac, a, b, blend='MIX'):
        n = node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        fin = n.inputs['Factor']
        if isinstance(fac, (int, float)):
            fin.default_value = fac
        else:
            L.new(fac, fin)
        ins = [i for i in n.inputs if i.type == 'RGBA']
        L.new(a, ins[0])
        L.new(b, ins[1])
        return [o for o in n.outputs if o.type == 'RGBA'][0]

    def noise(vec, scale, detail=2.0, rough=.5):
        n = node('ShaderNodeTexNoise', noise_dimensions='3D')
        L.new(vec, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough
        return n.outputs['Fac']

    coord = node('ShaderNodeTexCoord').outputs['Object']
    streak_map = node('ShaderNodeMapping')
    streak_map.inputs['Scale'].default_value = (1.0, 1.0, .18)
    L.new(coord, streak_map.inputs['Vector'])
    n_broad = noise(coord, 1.6, 2.0, .55)
    n_edge = noise(coord, 6.5, 1.0, .5)
    n_streak = noise(streak_map.outputs['Vector'], 4.5, 1.0, .4)

    part = node('ShaderNodeVertexColor', layer_name='PartColor').outputs['Color']
    color = part
    if family == 'Organic':
        dorsal, belly, arc, is_skin = attr('Dorsal'), attr('Belly'), attr('Arc'), attr('Skin')
        c = mix(dorsal, rgb(skin['side']), rgb(skin['top']))
        belly_edge = ramp(math_('ADD', belly, math_('MULTIPLY', math_('SUBTRACT', n_edge, .5), .34)), .60, .72)
        c = mix(belly_edge, c, rgb(skin['belly']))
        phase = math_('ADD', math_('MULTIPLY', arc, 2 * pi / skin['band_period']),
                      math_('MULTIPLY', math_('SUBTRACT', n_broad, .5), 3.2))
        band = math_('MULTIPLY', ramp(math_('SINE', phase), .30, .62), ramp(dorsal, .25, .70))
        band = math_('MULTIPLY', band, ramp(arc, .30, .60))  # body and tail only (heads/arms carry arc 0)
        c = mix(math_('MULTIPLY', band, .85), c, rgb(skin['band']))
        scute = math_('MULTIPLY', ramp(math_('SINE', math_('MULTIPLY', arc, 2 * pi / .17)), .70, .95), belly_edge)
        darker = mix(1.0, c, rgb((.80, .80, .80)), 'MULTIPLY')
        c = mix(math_('MULTIPLY', scute, .75), c, darker)
        color = mix(is_skin, part, c)
    # Broad value breakup and vertical brush streaks (low frequency only).
    breakup = math_('ADD', math_('ADD', math_('MULTIPLY', math_('SUBTRACT', n_broad, .5), .20),
                                        math_('MULTIPLY', math_('SUBTRACT', n_streak, .5), .12)), 1.0)
    comb = node('ShaderNodeCombineColor')
    for i in range(3):
        L.new(breakup, comb.inputs[i])
    color = mix(1.0, color, comb.outputs[0], 'MULTIPLY')
    # Painted cavity shading and a warm top light (values stay in the mid range for the acrylic pass).
    ao = node('ShaderNodeAmbientOcclusion', samples=16, only_local=False)
    ao.inputs['Distance'].default_value = .28
    ao_fac = math_('ADD', math_('MULTIPLY', ao.outputs['AO'], .30), .70)
    comb2 = node('ShaderNodeCombineColor')
    for i in range(3):
        L.new(ao_fac, comb2.inputs[i])
    color = mix(1.0, color, comb2.outputs[0], 'MULTIPLY')
    sep = node('ShaderNodeSeparateXYZ')
    L.new(coord, sep.inputs[0])
    lift = math_('ADD', math_('MULTIPLY', ramp(sep.outputs['Z'], .0, 3.4), .10), .95)
    comb3 = node('ShaderNodeCombineColor')
    L.new(lift, comb3.inputs[0])
    L.new(lift, comb3.inputs[1])
    L.new(math_('SUBTRACT', lift, .02), comb3.inputs[2])
    color = mix(1.0, color, comb3.outputs[0], 'MULTIPLY')
    bsdf = node('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = .8 if family == 'Organic' else .55
    bsdf.inputs['Metallic'].default_value = 0 if family == 'Organic' else .25
    L.new(color, bsdf.inputs['Base Color'])
    emit = node('ShaderNodeEmission', name='BakeEmission')
    L.new(color, emit.inputs['Color'])
    out = node('ShaderNodeOutputMaterial')
    L.new(bsdf.outputs[0], out.inputs['Surface'])
    material['PaintedColorSocket'] = 'BakeEmission'


def review_setup(scene):
    cam_data = bpy.data.cameras.new('ReviewCamera')
    cam = bpy.data.objects.new('ReviewCamera', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (4.8, -6, 3.8)
    cam.rotation_euler = (Vector((0, 0, 1.7)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    for label, loc, energy, color in [('Key', (4, -5, 7), 900, (1, .86, .7)), ('Fill', (-6, -3, 4), 260, (.6, .75, 1)),
                                      ('Rim', (-2, 6, 6), 500, (.8, .9, 1))]:
        data = bpy.data.lights.new(label, 'AREA')
        data.energy, data.color, data.size = energy, color, 4
        light = bpy.data.objects.new(label, data)
        scene.collection.objects.link(light)
        light.location = loc
        light.rotation_euler = (Vector((0, 0, 1.6)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()


def colors_for(kind):
    c = dict(SHARED)
    c['skin'] = SKIN[kind]['side']
    c['brow'] = SKIN[kind]['brow']
    return c


def build_character(name, rebuild=False):
    if name not in KIND:
        raise ValueError('Only the three approved representatives may be built: ' + name)
    kind = KIND[name]
    p = params(kind)
    scene = create_scene(name, rebuild)
    mats = make_materials(name)
    rig = create_rig(name, p)
    body = Part('Body', 'Organic')
    armor = Part('Armor', 'Equipment')
    build_body(body, p)
    for suffix, (x, z, size) in p['heads'].items():
        if suffix:
            build_extra_neck(body, p, suffix, x, z, size)
        build_head(body, body, p, suffix, x, z, size)
    build_hands(body, p)
    costume(armor, body, p)
    colors = colors_for(kind)
    objects = [body.make(rig, mats, colors), armor.make(rig, mats, colors)]
    for side in (('L', 'R') if kind == 'Chieftain' else ('R',)):
        objects.append(make_rigid_weapon(build_weapon(p, side), rig, mats, colors, side, p))
    for ob in objects:
        clean_mesh(ob)
    actions = create_actions(rig, kind)
    scene.frame_end = 73
    review_setup(scene)
    stats = measure(rig, objects)
    stats.update({'name': name, 'kind': kind, 'actions': [a.name for a in actions]})
    return {'scene': scene.name, 'rig': rig.name, 'objects': [o.name for o in objects], 'stats': stats}


def make_rigid_weapon(part, rig, mats, colors, side, p):
    ob = part.make(rig, mats, colors)
    for modifier in list(ob.modifiers):
        ob.modifiers.remove(modifier)
    ob.vertex_groups.clear()
    g = grip_point(p, side)
    for vertex in ob.data.vertices:
        vertex.co -= g
    ob.parent = rig
    ob.parent_type = 'BONE'
    ob.parent_bone = 'OffHand' if side == 'L' else 'MainHand'
    bpy.context.view_layer.update()
    ob.matrix_world = Matrix.Translation(g)
    ob['GripPivot'] = 'Origin at the handle center; rigid bone child, never skinned'
    return ob


def clean_mesh(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    for modifier in ob.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.use_deform_preserve_volume = False


def measure(rig, objects):
    result = {'bones': len(rig.data.bones), 'deform_bones': sum(b.use_deform for b in rig.data.bones), 'meshes': []}
    for ob in objects:
        ob.data.calc_loop_triangles()
        skinned = any(m.type == 'ARMATURE' for m in ob.modifiers)
        result['meshes'].append({
            'name': ob.name, 'triangles': len(ob.data.loop_triangles), 'vertices': len(ob.data.vertices),
            'unweighted': sum(not v.groups for v in ob.data.vertices) if skinned else 0,
            'max_influences': max((len(v.groups) for v in ob.data.vertices), default=0) if skinned else 0,
            'rigid': not skinned})
    return result


# ------------------------------------------------------------------------ bake / LOD / export

def _lod0(scene):
    return sorted([o for o in scene.objects if o.type == 'MESH' and o.name.endswith('_LOD0')], key=lambda o: o.name)


def texture_path(name):
    return ROOT / 'Assets/Art/Characters/Enemies' / CATEGORY[name] / name / 'Textures' / (name + '_Palette.png')


def atlas_cells(names):
    """Square atlas cells (x0, y0, size): body .70, armor and weapons .30 in the right column."""
    cells, column = {}, [(.70, .70), (.70, .40), (.70, .10)]
    for n in sorted(names, key=lambda n: ('Body' not in n, 'Armor' not in n, n)):
        if 'Body' in n:
            cells[n] = (0.0, .30, .70)
        else:
            x, y = column.pop(0)
            cells[n] = (x, y, .30)
    return cells


def bake_color(name, resolution=1024, island_margin=.024):
    """Unwrap all LOD0 meshes into one atlas, bake the painted field (bind pose, emission + AO) and
    fill every unbaked texel with the nearest island color (no black/foreign color under any mip).

    island_margin .024 of 1024 px leaves >= 24 px gutters; the nearest-color fill then gives each island
    >= 12 px of its own color, clean through mip 3 (8 px footprint) at combat distance."""
    import numpy as np
    scene = bpy.data.scenes[owned_scene_name(name)]
    bpy.context.window.scene = scene
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    sources = _lod0(scene)
    saved_action = rig.animation_data.action
    rig.animation_data.action = None
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    for o in scene.objects:
        o.select_set(False)
    for o in sources:
        o.hide_set(False)
        o.select_set(True)
        # A freshly created layer is required: smart_project silently skips the builder's placeholder UV0.
        for stale in [u for u in o.data.uv_layers if u.name == 'BakeUV']:
            o.data.uv_layers.remove(stale)
        layer = o.data.uv_layers.new(name='BakeUV')
        o.data.uv_layers.active = layer
        layer.active_render = True
    # Unwrap object by object into its fresh BakeUV layer, then place each object in a fixed square cell
    # of the atlas (uniform scale, so texel density is not distorted). The operators pack_islands and
    # multi-object smart_project silently collapse or skip UVs from a script context, so neither is used.
    cells = atlas_cells([o.name for o in sources])
    for o in sources:
        for other in scene.objects:
            other.select_set(other == o)
        bpy.context.view_layer.objects.active = o
        size = cells[o.name][2]
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        # Island margin is relative to the cell: >= 18 px of gutter at 1024 in every cell.
        bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=min(.08, 18 / (size * resolution)),
                                 scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        x0, y0, size = cells[o.name]
        inset = 6 / resolution
        for d in o.data.uv_layers['BakeUV'].data:
            d.uv = (x0 + inset + d.uv.x * (size - 2 * inset), y0 + inset + d.uv.y * (size - 2 * inset))
    for o in sources:
        uv = o.data.uv_layers['BakeUV'].data
        lo = min(min(d.uv.x, d.uv.y) for d in uv)
        hi = max(max(d.uv.x, d.uv.y) for d in uv)
        area = sum(abs(sum(uv[l[i]].uv.x * uv[l[(i + 1) % len(l)]].uv.y - uv[l[(i + 1) % len(l)]].uv.x * uv[l[i]].uv.y
                               for i in range(len(l)))) / 2 for l in [list(p.loop_indices) for p in o.data.polygons])
        if area < 1e-4 or lo < -1e-4 or hi > 1 + 1e-4:
            raise RuntimeError('UV unwrap failed for %s: area %.6f range %.3f..%.3f' % (o.name, area, lo, hi))
    image = bpy.data.images.get(name + '_BakeV12')
    if image:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(name + '_BakeV12', resolution, resolution, alpha=True, float_buffer=False)
    image.generated_color = (0, 0, 0, 0)  # alpha 0 = not baked; the bake writes opaque texels
    for o in scene.objects:
        o.select_set(o in sources)
    bpy.context.view_layer.objects.active = sources[0]
    temp_nodes = []
    mats = {m for o in sources for m in o.data.materials}
    for m in mats:
        nt = m.node_tree
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = image
        nt.nodes.active = tex
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
        emit = nt.nodes['BakeEmission']
        previous = out.inputs['Surface'].links[0].from_socket
        nt.links.new(emit.outputs[0], out.inputs['Surface'])
        temp_nodes.append((m, tex, previous))
    saved = (scene.render.engine, scene.cycles.samples, scene.render.bake.margin, scene.render.bake.use_clear)
    try:
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 32
        scene.render.bake.margin = 0
        scene.render.bake.use_clear = False  # keep alpha 0 outside islands for the coverage mask
        scene.render.bake.use_selected_to_active = False
        bpy.ops.object.bake(type='EMIT')
    finally:
        for m, tex, previous in temp_nodes:
            out = next(n for n in m.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
            m.node_tree.links.new(previous, out.inputs['Surface'])
            m.node_tree.nodes.remove(tex)
        scene.render.engine, scene.cycles.samples, scene.render.bake.margin, scene.render.bake.use_clear = saved
        rig.data.pose_position = 'POSE'
        rig.animation_data.action = saved_action
    px = np.array(image.pixels[:], dtype=np.float32).reshape(resolution, resolution, 4)
    covered = px[..., 3] > .5
    coverage = float(covered.mean())
    rgb = px[..., :3] * covered[..., None]
    weight = covered.astype(np.float32)
    filled = covered.copy()
    iterations = 0
    while not filled.all() and iterations < 2048:
        # Average of already-filled 4-neighbours: a Voronoi-like nearest-island fill.
        acc = np.zeros_like(rgb)
        cnt = np.zeros_like(weight)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            acc += np.roll(np.roll(rgb * weight[..., None], dy, 0), dx, 1)
            cnt += np.roll(np.roll(weight, dy, 0), dx, 1)
        grow = (~filled) & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        weight[grow] = 1.0
        filled |= grow
        iterations += 1
    out = np.concatenate([rgb, np.ones_like(weight)[..., None]], axis=2)
    image.pixels.foreach_set(out.ravel())
    path = texture_path(name)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.filepath_raw = str(path)
    image.file_format = 'PNG'
    image.save()
    for o in sources:
        o.data.uv_layers.remove(o.data.uv_layers['UV0'])
        o.data.uv_layers['BakeUV'].name = 'UV0'
        o['V12ColorBaked'] = True
    return {'path': str(path), 'coverage': round(coverage, 4), 'fill_iterations': iterations,
            'island_margin_px': round(island_margin * resolution, 1)}


def build_lods(name, ratios=((1, .52), (2, .27))):
    scene = bpy.data.scenes[owned_scene_name(name)]
    bpy.context.window.scene = scene
    for o in [o for o in scene.objects if o.type == 'MESH' and not o.name.endswith('_LOD0')]:
        bpy.data.objects.remove(o, do_unlink=True)
    made = []
    for source in _lod0(scene):
        for lod, ratio in ratios:
            ob = source.copy()
            ob.data = source.data.copy()
            ob.name = source.name.replace('_LOD0', '_LOD%d' % lod)
            scene.collection.objects.link(ob)
            ob.hide_set(False)
            bpy.context.view_layer.objects.active = ob
            r = ratio
            if 'Weapon' in source.name:
                r = .8 if lod == 1 else .55  # weapons are already lean; keep their silhouettes
            dec = ob.modifiers.new('LOD reduction', 'DECIMATE')
            dec.ratio = r
            dec.use_collapse_triangulate = True
            ob.modifiers.move(len(ob.modifiers) - 1, 0)
            bpy.ops.object.modifier_apply(modifier=dec.name)
            ob.hide_render = True
            ob.hide_set(True)
            made.append(ob.name)
    return made


def lod_triangles(name):
    scene = bpy.data.scenes[owned_scene_name(name)]
    result = {}
    for lod in range(3):
        total = 0
        for o in scene.objects:
            if o.type == 'MESH' and o.name.endswith('_LOD%d' % lod):
                o.data.calc_loop_triangles()
                total += len(o.data.loop_triangles)
        result['LOD%d' % lod] = total
    return result


def export_fbx(name):
    scene = bpy.data.scenes[owned_scene_name(name)]
    prev = bpy.context.window.scene
    bpy.context.window.scene = scene
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    if len(rig.animation_data.nla_tracks):
        raise RuntimeError('Unexpected NLA tracks on ' + rig.name)
    meshes = [o for o in scene.objects if o.type == 'MESH']
    vis = {o: (o.hide_get(), o.hide_render) for o in meshes}
    action = rig.animation_data.action
    path = ROOT / 'Assets/Art/Characters/Enemies' / CATEGORY[name] / name / 'Models' / ('CHR_' + name + '.fbx')
    path.parent.mkdir(parents=True, exist_ok=True)
    tracks = []
    try:
        rig.animation_data.action = None
        for clip in ('Idle', 'Attack', 'HitReaction', 'Death'):
            act = bpy.data.actions[rig.name + '|' + clip]
            track = rig.animation_data.nla_tracks.new()
            track.name = 'V12Export_' + clip
            track.strips.new(clip, int(act.frame_range[0]), act)
            tracks.append(track)
        for o in scene.objects:
            o.select_set(False)
        rig.select_set(True)
        for o in meshes:
            o.hide_set(False)
            o.hide_render = False
            o.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=True,
                                 bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                                 bake_anim_simplify_factor=0, path_mode='RELATIVE', use_mesh_modifiers=True,
                                 use_armature_deform_only=False, mesh_smooth_type='OFF', use_tspace=False)
        return str(path)
    finally:
        for t in tracks:
            rig.animation_data.nla_tracks.remove(t)
        rig.animation_data.action = action
        for o, (h, r) in vis.items():
            o.hide_set(h)
            o.hide_render = r
        bpy.context.window.scene = prev


def save_source(name):
    scene = bpy.data.scenes[owned_scene_name(name)]
    folder = SRC / name
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / (name + '_V12.blend')
    actions = {a for a in bpy.data.actions if a.name.startswith('Rig_' + name + VERSION + '|')}
    bpy.data.libraries.write(str(path), {scene} | actions, fake_user=True, path_remap='RELATIVE_ALL')
    return str(path)
