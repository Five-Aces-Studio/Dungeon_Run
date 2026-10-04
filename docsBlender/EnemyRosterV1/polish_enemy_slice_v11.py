"""Non-destructive Enemy V1.1 surface/rig construction for the interactive session.

Load with importlib, then build_character('SnakeSoldier', 'Soldier', 'Basic').
No automatic execution, FBX export, scene replacement or background process.
Each build has its own collision-guarded scene. Existing V1 sources stay intact.
Technical color swatches are not a painted texture; Unity supplies acrylic shading.
"""
import importlib.util
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector, Matrix, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docsBlender/EnemyRosterV1/V11'
_spec = importlib.util.spec_from_file_location('enemy_v1_surface_helpers', Path(__file__).with_name('build_enemy_slice.py'))
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)
Surface = base.Surface
pi, sin, cos = math.pi, math.sin, math.cos

# Keep black facial accents in their own color family, rather than shading into red.
_original_patch = base.patch
def family_safe_patch(surface, coords, color, bone='Chest', depth=.025):
    first = len(surface.colors)
    _original_patch(surface, coords, color, bone, depth)
    if color == 24:
        for i in range(first, len(surface.colors)):
            surface.colors[i] = 24
base.patch = family_safe_patch


def clamp(x, a=0.0, b=1.0):
    return max(a, min(b, x))


def smooth(x):
    x = clamp(x)
    return x*x*(3-2*x)


def weights(*pairs):
    result = {}
    for name, amount in pairs:
        if amount > 0.00001:
            result[name] = result.get(name, 0) + amount
    total = sum(result.values())
    return {name: amount / total for name, amount in result.items()}


def axial_weights(z):
    stops = [(0.65, 'COG'), (1.20, 'Spine'), (1.90, 'Chest'), (2.38, 'Neck')]
    if z <= stops[0][0]:
        return {'COG': 1.0}
    for (low, a), (high, b) in zip(stops, stops[1:]):
        if z <= high:
            t = smooth((z-low)/(high-low))
            return weights((a, 1-t), (b, t))
    return {'Neck': 1.0}


def create_scene(name):
    scene_name = 'EnemyRosterV11_' + name
    rig_name = 'Rig_' + name + 'V11'
    if scene_name in bpy.data.scenes or rig_name in bpy.data.objects:
        raise RuntimeError('V1.1 candidate already exists; inspect rather than overwrite: ' + scene_name)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    scene = bpy.data.scenes.new(scene_name)
    scene['EnemyRosterV11Owned'] = True
    scene.unit_settings.system = 'METRIC'
    scene.world = bpy.data.worlds.new(scene_name + '_World')
    bpy.context.window.scene = scene
    return scene


def make_materials(name):
    folder = OUT / name
    folder.mkdir(parents=True, exist_ok=True)
    atlas = folder / (name + '_Palette.png')
    mats = base.palette(name + 'V11', atlas)
    for material, family in zip(mats, ['Organic', 'Equipment']):
        material.name = 'M_' + name + '_' + family
        shader = material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value = .72 if family == 'Organic' else .48
        shader.inputs['Metallic'].default_value = 0 if family == 'Organic' else .35
    return mats, atlas


def create_rig(name, tail, arms, heads, grips):
    data = bpy.data.armatures.new('Skeleton_' + name + 'V11')
    rig = bpy.data.objects.new('Rig_' + name + 'V11', data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')

    def bone(n, a, b, parent=None, deform=True):
        item = data.edit_bones.new(n)
        item.head, item.tail = a, b
        item.use_deform = deform
        if parent:
            item.parent = data.edit_bones[parent]
        return item

    bone('Root', (0, 0, 0), (0, 0, .25), deform=False)
    bone('COG', (0, 0, .67), (0, 0, 1.05), 'Root')
    bone('Spine', (0, 0, 1.05), (0, 0, 1.63), 'COG')
    bone('Chest', (0, 0, 1.63), (0, 0, 2.13), 'Spine')
    for suffix, (x, z, size) in heads.items():
        bone('Neck' + suffix, (x*.5, .015, 2.12), (x, 0, z-.04), 'Chest')
        bone('Head' + suffix, (x, 0, z-.04), (x, 0, z+.48*size), 'Neck'+suffix)
        bone('Jaw' + suffix, (x, .055, z-.065*size), (x, -.25*size, z-.10*size), 'Head'+suffix)
    for side, (shoulder, elbow, wrist) in arms.items():
        bone('Clavicle_'+side, (0, 0, 2.09), shoulder, 'Chest')
        bone('UpperArm_'+side, shoulder, elbow, 'Clavicle_'+side)
        bone('Forearm_'+side, elbow, wrist, 'UpperArm_'+side)
        bone('Hand_'+side, wrist, grips[side], 'Forearm_'+side)
        bone('WeaponSocket_'+side, grips[side], Vector(grips[side])+Vector((0, 0, .15)), 'Hand_'+side, False)
        bone('VFX_WeaponTip_'+side, Vector(grips[side])+Vector((0, 0, 1.10)), Vector(grips[side])+Vector((0, 0, 1.18)), 'WeaponSocket_'+side, False)
    for i in range(8):
        bone('Tail_%02d' % (i+1), tail[i][:3], tail[i+1][:3], 'COG' if i == 0 else 'Tail_%02d' % i)
    for label, point, parent in [
            ('TargetAnchor', (0, 0, 3.3), 'Root'),
            ('HUDAnchor', (0, 0, 3.45), 'Root'),
            ('DamageTextAnchor', (0, -.15, 2.45), 'Chest'),
            ('VFXSocket_Chest', (0, -.31, 1.90), 'Chest'),
            ('VFXSocket_Head', (0, 0, 2.95), 'Head'),
            ('VFX_Ground', (0, 0, .035), 'Root')]:
        bone(label, point, Vector(point)+Vector((0, 0, .08)), parent, False)
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.select_set(False)
    rig.show_in_front = True
    rig['Milestone'] = 'V1.1 visual/deformation validation candidate'
    return rig


def bridge(surface, a, b, color):
    assert len(a) == len(b)
    for j in range(len(a)):
        surface.face((a[j], a[(j+1) % len(a)], b[(j+1) % len(b)], b[j]), color)


def body_and_arms(surface, tail, arms, heads, kind):
    """One continuous quad surface: tail tip -> abdomen -> neck, branched arms.

    Shoulder faces are removed and their ordered boundary is bridged to arm loops.
    This is actual shared topology, not overlapping capsules or voxel remeshing.
    """
    n = 32
    # Extend the tail's final rising tangent into the abdomen without doubled surfaces.
    tail_rows = list(reversed(base.catmull(tail, 5)))
    spine_profile = [
        (0, 0, .98, .31, .265), (0, 0, 1.18, .305, .25),
        (0, 0, 1.40, .34, .26), (0, 0, 1.62, .405, .275),
        (0, 0, 1.78, .46, .275), (0, 0, 1.84, .475, .268),
        (0, 0, 1.93, .495, .26), (0, 0, 2.02, .50, .245),
        (0, 0, 2.11, .435, .23), (0, 0, 2.19, .31, .205),
        (0, 0, 2.27, .225, .18), (0, 0, 2.36, .18, .16),
        (0, 0, heads[''][1]-.005, .16, .155)]
    rows = tail_rows + spine_profile
    start_shoulder = len(tail_rows) + 5
    end_shoulder = len(tail_rows) + 8
    holes = [(start_shoulder, end_shoulder, -4, 4, 'L'),
             (start_shoulder, end_shoulder, 12, 20, 'R')]
    rings = []
    previous_axis = Vector((1, 0, 0))
    for i, row in enumerate(rows):
        p = Vector(row[:3])
        tangent = Vector(rows[min(i+1, len(rows)-1)][:3]) - Vector(rows[max(0, i-1)][:3])
        tangent.normalize()
        # Parallel transport prevents Frenet flips on nearly straight sections.
        axis = previous_axis - tangent*previous_axis.dot(tangent)
        if axis.length < .05:
            axis = Vector((0, 1, 0)) - tangent*tangent.y
        axis.normalize()
        # Distribute the roll toward the torso frame; an abrupt X-axis reset pinches the join.
        if i >= len(tail_rows)-12:
            target = Vector((1, 0, 0)) - tangent*tangent.x
            if target.length > .05:
                target.normalize()
                angle = math.atan2(tangent.dot(axis.cross(target)), axis.dot(target))
                remaining = max(1, len(tail_rows)-i)
                axis = Quaternion(tangent, angle/remaining) @ axis
        across = tangent.cross(axis).normalized()
        previous_axis = axis.copy()
        ring = []
        for j in range(n):
            a = 2*pi*j/n
            # Broad chest front/back planes, not a paper-thin cylinder.
            q = p + axis*cos(a)*row[3] + across*sin(a)*row[4]
            if i < len(tail_rows)-1:
                t = 1-i/(len(tail_rows)-1)
                f = clamp(t*8, 0, 7.999)
                index = int(f)
                blend = smooth(f-index)
                wa = weights(('Tail_%02d' % (index+1), 1-blend), ('Tail_%02d' % min(8, index+2), blend))
                if i >= len(tail_rows)-4:
                    blend = smooth((i-(len(tail_rows)-4))/3)
                    wa = weights(*[(key, value*(1-blend)) for key, value in wa.items()], ('COG', blend))
            else:
                wa = axial_weights(q.z)
            ring.append(surface.vertex(q, (j/n, i/(len(rows)-1)), wa))
        rings.append(ring)
    for i in range(len(rows)-1):
        for j in range(n):
            in_hole = any(low <= i < high and any(j == k % n for k in range(left, right)) for low, high, left, right, side in holes)
            if in_hole:
                continue
            # Whole ventral band with family-safe tonal values; no palette overflow.
            color = 5 if 20 <= j <= 27 else (1 if 4 <= j <= 11 else 2)
            surface.face((rings[i][j], rings[i][(j+1) % n], rings[i+1][(j+1) % n], rings[i+1][j]), color)
    surface.face(tuple(reversed(rings[0])), 2)
    surface.face(tuple(rings[-1]), 2)
    for low, high, left, right, side in holes:
        boundary = [rings[low][j % n] for j in range(left, right)]
        boundary += [rings[i][right % n] for i in range(low, high)]
        boundary += [rings[high][j % n] for j in range(right, left, -1)]
        boundary += [rings[i][left % n] for i in range(high, low, -1)]
        shoulder, elbow, wrist = [Vector(point) for point in arms[side]]
        sign = 1 if side == 'L' else -1
        center = sum((Vector(surface.v[v]) for v in boundary), Vector()) / len(boundary)
        # Start at the welded body opening, then flow into deltoid and biceps.
        # Move monotonically out of the socket; rising to the shoulder pivot first folded the loft back.
        path = [center+(elbow-center).normalized()*.055, center.lerp(elbow,.20),
                center.lerp(elbow, .36), shoulder.lerp(elbow, .54),
                shoulder.lerp(elbow, .82), elbow, elbow.lerp(wrist, .16),
                elbow.lerp(wrist, .42), elbow.lerp(wrist, .76), wrist]
        radii = [.194, .225, .224, .198, .161, .150, .155, .151, .118, .096]
        tang = (elbow-shoulder).normalized()
        u = Vector((0, 1, 0))
        v = tang.cross(u).normalized()
        # Follow the actual boundary angular order, not a guessed circular order.
        projected = [((Vector(surface.v[index])-center).dot(u), (Vector(surface.v[index])-center).dot(v)) for index in boundary]
        area = sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(projected,projected[1:]+projected[:1]))
        orientation = 1 if area > 0 else -1
        # A rectangular socket has repeated polar angles. Reusing those angles folds the arm rings.
        fit = sum(complex(x,y)*complex(cos(-orientation*2*pi*j/len(boundary)),sin(-orientation*2*pi*j/len(boundary))) for j,(x,y) in enumerate(projected))
        offset = math.atan2(fit.imag,fit.real)
        angles = [offset+orientation*2*pi*j/len(boundary) for j in range(len(boundary))]
        previous = boundary
        for index in boundary:
            surface.weights[index] = weights(('Chest',.65),('Clavicle_'+side,.35))
        for k, (p, radius) in enumerate(zip(path, radii)):
            tangent = (path[min(k+1, len(path)-1)]-path[max(0, k-1)]).normalized()
            u = Vector((0, 1, 0))-tangent*tangent.y
            u.normalize()
            v = tangent.cross(u).normalized()
            if k < 2:
                t = smooth((k+1)/3)
                wa = weights(('Chest', 1-t), ('Clavicle_'+side, t*.35), ('UpperArm_'+side, t*.65))
            elif k <= 3:
                wa = {'UpperArm_'+side: 1.0}
            elif k <= 6:
                t = smooth((k-3)/4)
                wa = weights(('UpperArm_'+side, 1-t), ('Forearm_'+side, t))
            else:
                t = smooth((k-7)/2)*.6
                wa = weights(('Forearm_'+side, 1-t), ('Hand_'+side, t))
            ring = [surface.vertex(p+u*cos(a)*radius*.9+v*sin(a)*radius, (j/len(angles), k/10), wa) for j, a in enumerate(angles)]
            bridge(surface, previous, ring, 2)
            previous = ring
        surface.face(tuple(reversed(previous)), 2)
    return rings


def strip(surface, points, width, color, bone='Chest', thickness=.018):
    """Continuous leather/cloth ribbon with a closed back and shared edge loops."""
    centers = base.catmull(points, 4)
    rows = []
    for i, center in enumerate(centers):
        direction = centers[min(i+1, len(centers)-1)]-centers[max(0, i-1)]
        edge = Vector((direction.z, 0, -direction.x)).normalized()*width*.5
        wa = axial_weights(center.z) if bone == 'BODY' else {bone: 1.0}
        row = [surface.vertex(center-edge, (0, i/len(centers)), wa),
               surface.vertex(center+edge, (1, i/len(centers)), wa),
               surface.vertex(center+edge+Vector((0, thickness, 0)), (1, i/len(centers)), wa),
               surface.vertex(center-edge+Vector((0, thickness, 0)), (0, i/len(centers)), wa)]
        if rows:
            bridge(surface, rows[-1], row, color)
        rows.append(row)
    surface.face(tuple(reversed(rows[0])), color)
    surface.face(tuple(rows[-1]), color)


def head(surface, detail, jaw_surface, x, z, size, kind, suffix=''):
    bone = 'Head'+suffix
    # Flattened anterior planes preserve readable eyes and avoid a ball-headed toy.
    if kind == 'Sentinel':
        profile = [(-.045, .145, .29), (.05, .235, .29), (.20, .41, .24),
                   (.36, .51, .195), (.49, .40, .19), (.59, .21, .135), (.635, .035, .035)]
    else:
        profile = [(-.055, .115, .31), (.03, .18, .31), (.16, .245, .275),
                   (.34, .285, .235), (.48, .235, .20), (.56, .12, .115), (.585, .025, .025)]
    rings = []
    for height, width, depth in base.catmull(profile, 3):
        ring = []
        for j in range(24):
            a = 2*pi*j/24
            # Soft-square cross-section creates broad brow/cheek/muzzle planes.
            xx = math.copysign(abs(cos(a))**.78, cos(a))*width
            yy = math.copysign(abs(sin(a))**.68, sin(a))*depth
            if yy > 0:
                yy *= .74
            p = (x+xx*size, yy*size, z+height*size)
            ring.append(surface.vertex(p, (j/24, (height+.08)/.7), {bone: 1.0}))
        if rings:
            bridge(surface, rings[-1], ring, 2)
        rings.append(ring)
    surface.face(tuple(reversed(rings[0])), 1)
    surface.face(tuple(rings[-1]), 2)
    # A separate closed mandibular surface rotates at its actual rear hinge.
    base.tube(jaw_surface, [(x,.11*size,z-.07*size,.09*size,.055*size),
                            (x,-.015*size,z-.115*size,.145*size,.071*size),
                            (x,-.20*size,z-.15*size,.122*size,.064*size),
                            (x,-.32*size,z-.125*size,.077*size,.035*size)],
              5, 20, 3, 'Jaw'+suffix)
    base.patch(detail, [(x-.112*size,-.299*size,z-.067*size),
                        (x+.112*size,-.299*size,z-.067*size),
                        (x+.077*size,-.332*size,z-.108*size),
                        (x-.077*size,-.332*size,z-.108*size)], 24, bone, .002)
    # Nostril accents remain above the mouth and below the eyes.
    for sign in [-1, 1]:
        base.patch(detail, [(x+sign*.085*size,-.313*size,z+.005*size),
                            (x+sign*.057*size,-.325*size,z+.044*size),
                            (x+sign*.046*size,-.325*size,z+.004*size)], 24, bone, .003)
    if kind == 'Soldier':
        base.eye(detail, x-.145, -.280, z+.32, .108, .041, bone, tilt=-.30)
        base.eye(detail, x-.115, -.322, z+.16, .099, .040, bone, tilt=-.30)
        base.eye(detail, x+.139, -.300, z+.235, .112, .045, bone, tilt=.32)
    elif kind == 'Sentinel':
        base.eye(detail, x-.325, -.220, z+.345, .117, .046, bone, tilt=-.36)
        base.eye(detail, x+.325, -.220, z+.345, .117, .046, bone, tilt=.36)
        base.eye(detail, x, -.278, z+.195, .047, .136, bone)
    else:
        base.eye(detail, x, -.300*size, z+.21*size, .214*size, .075*size, bone, 3)
    # Broad upper brow overhang, kept above the lens rather than over its center.
    for sign in [-1, 1]:
        base.patch(detail, [(x+sign*.03*size,-.242*size,z+.445*size),
                            (x+sign*.235*size,-.205*size,z+.485*size),
                            (x+sign*.245*size,-.227*size,z+.43*size),
                            (x+sign*.07*size,-.26*size,z+.402*size)], 3, bone, .006)
    base.tube(jaw_surface, [(x,-.32*size,z-.12*size,.018,.012),
                           (x,-.395*size,z-.245*size,.014,.009),
                           (x-.024*size,-.41*size,z-.295*size,.002,.002)],
              22, 8, 2, 'Jaw'+suffix)
    base.tube(jaw_surface, [(x,-.39*size,z-.24*size,.011,.009),
                           (x+.027*size,-.417*size,z-.294*size,.002,.002)],
              22, 8, 2, 'Jaw'+suffix)


def grip_hands(surface, arms, grips):
    for side, points in arms.items():
        wrist = Vector(points[-1])
        g = Vector(grips[side])
        bone = 'Hand_'+side
        palm = g+Vector((0, .075, .005))
        base.tube(surface, [(*wrist,.098,.083),
                            (*wrist.lerp(palm,.5),.112,.082),
                            (*palm,.115,.077),
                            (*(palm+Vector((0,0,-.075))),.082,.055)], 2, 16, 3, bone)
        # Four horizontal curled digits surround the vertical handle with .003m clearance.
        # They are intentionally a mitten-like stylized grip, not high-detail human fingers.
        for i in range(4):
            z = g.z+.078-i*.049
            sections = []
            for j in range(8):
                a = math.radians(38+j*37)
                radius = .071
                finger = .028 if j < 5 else .022
                sections.append((g.x+cos(a)*radius, g.y+sin(a)*radius, z-.009*j/7, finger, finger*.92))
            base.tube(surface, sections, 3 if i == 0 else 2, 8, 2, bone)
        sign = 1 if side == 'L' else -1
        base.tube(surface, [(g.x-sign*.082,g.y+.042,g.z+.11,.039,.034),
                            (g.x-sign*.087,g.y-.022,g.z+.07,.038,.030),
                            (g.x-sign*.052,g.y-.071,g.z+.03,.034,.026),
                            (g.x+sign*.012,g.y-.077,g.z+.011,.020,.019)],
                  3, 10, 3, bone)


def costume(armor, detail, arms, kind):
    for side, points in arms.items():
        shoulder, elbow, wrist = [Vector(p) for p in points]
        a, b = elbow.lerp(wrist,.32), elbow.lerp(wrist,.85)
        color = 18 if kind == 'Chieftain' else (14 if kind == 'Sentinel' else 10)
        base.tube(armor, [(*a,.178,.16), (*a.lerp(b,.15),.184,.166),
                         (*a.lerp(b,.85),.145,.132), (*b,.144,.13)], color, 16, 2, 'Forearm_'+side)
    if kind == 'Soldier':
        # Image-right shoulder = anatomical left; unlike the preliminary wrong-side plate.
        base.tube(armor, [(.42,0,2.11,.245,.22),(.55,0,2.09,.275,.225),
                         (.68,-.005,1.965,.245,.21)], 10, 20, 3, 'UpperArm_L')
        strip(armor, [(.37,-.217,2.14),(.22,-.302,1.94),(-.06,-.3,1.67),(-.29,-.235,1.32)], .115, 10, 'BODY')
        strip(armor, [(-.29,.235,1.32),(-.05,.285,1.68),(.22,.273,1.96),(.37,.18,2.14)], .115, 9, 'BODY', -.018)
        base.patch(armor, [(.18,-.326,1.91),(.27,-.302,2.005),(.32,-.291,1.965),(.23,-.338,1.868)], 14, 'Chest', .006)
    elif kind == 'Sentinel':
        base.patch(armor, [(-.375,-.235,2.115),(.375,-.235,2.115),
                           (.425,-.25,1.80),(.305,-.295,1.55),(0,-.31,1.43),
                           (-.305,-.295,1.55),(-.425,-.25,1.80)], 14, 'Chest', .115)
        # Two large backing straps make the breastplate credible from rear/side.
        for sign in [-1,1]:
            strip(armor, [(sign*.285,.22,2.09),(sign*.255,.296,1.85),(sign*.225,.28,1.56)], .11, 9, 'Chest', -.018)
        base.patch(armor, [(-.36,-.264,2.118),(0,-.378,2.135),(.36,-.264,2.118),
                           (.345,-.282,2.077),(0,-.399,2.088),(-.345,-.282,2.077)], 15, 'Chest', .007)
        # Dark purple glyph, not a second yellow facial eye.
        base.patch(detail, [(-.122,-.397,1.91),(0,-.427,1.963),(.122,-.397,1.91),
                            (0,-.43,1.85)], 28, 'Chest', .006)
        base.patch(detail, [(-.019,-.439,1.942),(.019,-.439,1.942),(.014,-.447,1.835),
                            (0,-.44,1.785),(-.014,-.447,1.835)], 24, 'Chest', .003)
    else:
        base.tube(armor, [(0,0,1.09,.322,.267),(0,0,1.16,.326,.27),
                         (0,0,1.27,.335,.278),(0,0,1.31,.324,.27)], 21, 32, 2, 'Spine')
        strip(armor, [(.20,-.284,1.235),(.145,-.307,1.07),(.25,-.32,.82),
                      (.22,-.37,.60),(.065,-.355,.43)], .235, 22, 'Spine')
        base.tube(armor, [(0,0,1.285,.337,.282),(0,0,1.345,.345,.283)], 9, 32, 1, 'Spine')
        strip(armor, [(-.28,-.18,2.11),(0,-.338,1.90),(.28,-.18,2.11)], .028, 17, 'Chest')
        base.patch(armor, [(-.15,-.36,1.93),(.15,-.36,1.93),(0,-.39,1.67)], 18, 'Chest', .025)
        base.patch(armor, [(-.071,-.397,1.87),(.071,-.397,1.87),(0,-.414,1.75)], 24, 'Chest', .003)


def weapon_surface(kind, side, grip):
    s = Surface('Weapon_'+side, 1)
    x,y,z = grip
    bone = 'WeaponSocket_'+side
    if kind == 'Chieftain':
        base.tube(s, [(x,y,z-.17,.036,.036),(x,y,z+.19,.036,.036)], 9, 12, 2, bone)
        base.patch(s, [(x-.17,y,z+.17),(x+.17,y,z+.17),(x+.18,y,z+.23),
                       (x,y-.015,z+.245),(x-.18,y,z+.23)], 18, bone, .025)
        sign = 1 if side == 'L' else -1
        base.patch(s, [(x-.063,y,z+.23),(x+.068,y,z+.23),
                       (x+sign*.19,y,z+.74),(x+sign*.35,y,z+1.12),
                       (x+sign*.23,y,z+1.31),(x+sign*.12,y,z+.83)], 15, bone, .042)
        base.tube(s, [(x,y,z-.175,.054,.054),(x,y,z-.225,.046,.046),
                      (x,y,z-.25,.018,.018)], 18, 12, 2, bone)
    else:
        base.tube(s, [(x,y,.29,.034,.034),(x,y,2.82,.034,.034)], 10, 12, 3, bone)
        # Low-frequency grip rings and ferrules read from gameplay distance.
        for zz in [z-.12,z+.12,2.53,2.72]:
            base.tube(s, [(x,y,zz-.025,.043,.043),(x,y,zz+.025,.043,.043)], 31 if zz<2 else 13, 12, 1, bone)
        if kind == 'Soldier':
            base.patch(s, [(x-.034,y,2.56),(x-.07,y,2.94),(x-.44,y,2.77),
                           (x-.32,y,2.50),(x-.12,y,2.60),(x+.19,y,2.48),(x+.25,y,2.535)], 14, bone, .052)
        else:
            base.patch(s, [(x-.05,y,2.65),(x+.053,y,2.65),(x+.053,y,3.02),
                           (x-.145,y,3.25),(x-.21,y,3.63),(x-.355,y,3.38),
                           (x-.31,y,3.04)], 15, bone, .047)
    return s


def clean_mesh(ob):
    # Remove only exact duplicate vertices and fix component winding; never voxel remesh.
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    for modifier in ob.modifiers:
        if modifier.type == 'ARMATURE':
            # Match the linear skinning used by the exported Unity candidates.
            modifier.use_deform_preserve_volume = False


def make_rigid_weapon(surface, mats, rig, grip, side):
    ob = surface.make(mats, rig)
    for modifier in list(ob.modifiers):
        ob.modifiers.remove(modifier)
    ob.vertex_groups.clear()
    for vertex in ob.data.vertices:
        vertex.co -= Vector(grip)
    ob.parent = rig
    ob.parent_type = 'BONE'
    ob.parent_bone = 'WeaponSocket_'+side
    bpy.context.view_layer.update()
    ob.matrix_world = Matrix.Translation(Vector(grip))
    ob['GripPivot'] = 'Origin at handle center; rigid bone parent, never skinned'
    clean_mesh(ob)
    return ob


def build_lods(objects):
    result = list(objects)
    for source in objects:
        for lod, ratio in [(1,.58),(2,.30)]:
            # Small finger/eye surfaces receive gentler decimation to retain identity.
            if 'Hands' in source.name or 'Face' in source.name or 'Jaw' in source.name:
                ratio = .72 if lod == 1 else .47
            ob = source.copy()
            ob.data = source.data.copy()
            ob.name = source.name.replace('_LOD0', '_LOD%d'%lod)
            bpy.context.collection.objects.link(ob)
            bpy.context.view_layer.objects.active = ob
            reducer = ob.modifiers.new('Silhouette-aware LOD candidate', 'DECIMATE')
            reducer.ratio = ratio
            reducer.use_collapse_triangulate = True
            ob.modifiers.move(len(ob.modifiers)-1, 0)
            bpy.ops.object.modifier_apply(modifier=reducer.name)
            ob.hide_render = True
            ob.hide_set(True)
            result.append(ob)
    return result


def create_actions(rig, kind):
    """Four short rig validation clips, not final authored combat choreography."""
    rig.animation_data_create()
    actions = []
    for label, length in [('Idle',72),('Attack',48),('HitReaction',30),('Death',60)]:
        action = bpy.data.actions.new(rig.name+'|'+label)
        action.use_fake_user = True
        rig.animation_data.action = action
        keyframes = [1, max(2,length//5), length*2//5, length*3//5, length*4//5, length]
        for frame in keyframes:
            t = (frame-1)/(length-1)
            wave = sin(2*pi*t)
            pulse = sin(pi*t)
            # One anticipation/strike/recovery progression; no jittery constant motion.
            strike = -.22*sin(pi*t/.35) if t < .35 else .70*sin(pi*(t-.35)/.65)
            for b in rig.pose.bones:
                b.rotation_mode = 'XYZ'
                b.rotation_euler = (0,0,0)
                b.location = (0,0,0)
                if label == 'Idle':
                    if b.name == 'Chest': b.rotation_euler.x = .018*wave
                    if b.name.startswith('Head'): b.rotation_euler.y = .028*wave
                    if b.name.startswith('Tail_'): b.rotation_euler.y = .013*sin(2*pi*t+int(b.name[-2:])*.4)
                    if b.name.startswith('Forearm'): b.rotation_euler.x = .012*wave
                elif label == 'Attack':
                    if b.name == 'Chest': b.rotation_euler.z = strike*.32; b.rotation_euler.x = strike*.20
                    if b.name.startswith('UpperArm'): b.rotation_euler.x = -strike*(1.05 if kind=='Chieftain' else .85)
                    if b.name.startswith('Forearm'): b.rotation_euler.x = strike*.46
                    if b.name.startswith('Head'): b.rotation_euler.x = strike*.12
                    if b.name.startswith('Tail_'): b.rotation_euler.y = -strike*.05
                    if b.name.startswith('Jaw'): b.rotation_euler.x = .16*pulse
                elif label == 'HitReaction':
                    if b.name == 'Chest': b.rotation_euler.x = -.21*pulse
                    if b.name.startswith('Head'): b.rotation_euler.x = -.16*pulse
                    if b.name.startswith('Tail_'): b.rotation_euler.y = .04*pulse
                else:
                    ease = smooth(t)
                    if b.name == 'COG': b.location.z = -.18*ease
                    if b.name == 'Spine': b.rotation_euler.x = .58*ease
                    if b.name == 'Chest': b.rotation_euler.x = .35*ease
                    if b.name.startswith('Head'): b.rotation_euler.x = .30*ease
                    if b.name.startswith('UpperArm'): b.rotation_euler.z = (.17 if b.name.endswith('L') else -.17)*ease
                    if b.name.startswith('Tail_'): b.rotation_euler.y = .07*sin(int(b.name[-2:])*.7)*ease
                b.keyframe_insert('rotation_euler', frame=frame, group=b.name)
                b.keyframe_insert('location', frame=frame, group=b.name)
        action['Purpose'] = 'V1.1 visual pose validation, not production choreography'
        actions.append(action)
    rig.animation_data.action = actions[0]
    bpy.context.scene.frame_set(1)
    return actions


def measure(rig, objects):
    result = {'status':'VISUAL_VALIDATION_REQUIRED', 'bones':len(rig.data.bones),
              'materials':2, 'palette':[256,128], 'lods':{}, 'meshes':[]}
    for lod in range(3):
        group = [ob for ob in objects if ob.name.endswith('_LOD%d'%lod)]
        for ob in group: ob.data.calc_loop_triangles()
        result['lods'][lod] = {'triangles':sum(len(ob.data.loop_triangles) for ob in group),
                               'skinned_meshes':sum(any(m.type=='ARMATURE' for m in ob.modifiers) for ob in group),
                               'rigid_meshes':sum(ob.parent_type=='BONE' for ob in group)}
    for ob in objects:
        if not ob.name.endswith('_LOD0'): continue
        skinned = any(m.type=='ARMATURE' for m in ob.modifiers)
        result['meshes'].append({'name':ob.name, 'vertices':len(ob.data.vertices),
                                'unweighted':sum(not v.groups for v in ob.data.vertices) if skinned else 0,
                                'max_weight_error':max([abs(sum(g.weight for g in v.groups)-1) for v in ob.data.vertices] or [0]) if skinned else 0,
                                'rigid':not skinned})
    return result


def build_character(name, kind, category):
    if (name,kind,category) not in [('SnakeSoldier','Soldier','Basic'),
                                    ('SnakeSentinel','Sentinel','Elite'),
                                    ('SnakeChieftain','Chieftain','Boss')]:
        raise ValueError('Only the three approved representatives may be built')
    scene = create_scene(name)
    mats, atlas = make_materials(name)
    tail = [(0,0,.83,.29,.265),(-.17,.18,.51,.31,.255),(-.53,.48,.29,.32,.25),
            (-.36,.98,.255,.30,.235),(.32,1.08,.24,.27,.22),(.91,.70,.225,.23,.19),
            (1.00,.07,.185,.175,.15),(.55,-.42,.135,.10,.105),(-.13,-.46,.085,.012,.018)]
    if kind == 'Chieftain':
        tail = [(x*1.14,y*1.1,z,rx*1.10,ry*1.08) for x,y,z,rx,ry in tail]
    arms = {side:[(sign*.485,0,2.075), (sign*(.85 if kind=='Chieftain' else .77),-.035,1.755),
                  (sign*(1.14 if kind=='Chieftain' else .895),-.24,1.565 if kind=='Chieftain' else 1.475)]
            for side,sign in [('L',1),('R',-1)]}
    grips = {side:tuple(Vector(points[-1])+Vector(((.035 if side=='L' else -.035),-.09,-.10))) for side,points in arms.items()}
    heads = {'':(0,2.46 if kind!='Sentinel' else 2.50,1.0)}
    if kind == 'Chieftain':
        heads = {'':(0,2.68,1.10),'_L':(.60,2.54,.81),'_R':(-.60,2.54,.81)}
    rig = create_rig(name, tail, arms, heads, grips)
    body = Surface('Body')
    face = Surface('Face')
    jaws = Surface('Jaw')
    hands = Surface('Hands')
    armor = Surface('Armor',1)
    body_and_arms(body, tail, arms, heads, kind)
    for suffix,(x,z,size) in heads.items():
        if suffix:
            # Chief lateral necks overlap broadly inside trapezius mass; not floating stalks.
            base.tube(body, [(x*.44,.045,2.035,.22,.19),(x*.72,.025,2.23,.19,.17),
                             (x*.94,.012,2.42,.157,.148),(x,0,z+.015,.15,.145)],
                      2,20,4,weight_fn=lambda t,p,suffix=suffix: weights(('Chest',1-smooth(t*2)),('Neck'+suffix,smooth(t*2))))
        head(body,face,jaws,x,z,size,kind,suffix)
    grip_hands(hands, arms, grips)
    costume(armor,face,arms,kind)
    objects = [surface.make(mats,rig) for surface in [body,face,jaws,hands,armor]]
    for ob in objects: clean_mesh(ob)
    for side in (['L','R'] if kind=='Chieftain' else ['R']):
        objects.append(make_rigid_weapon(weapon_surface(kind,side,grips[side]), mats,rig,grips[side],side))
    objects = build_lods(objects)
    actions = create_actions(rig,kind)
    scene.frame_end = 72
    base.setup_render(name+'V11')
    base.aim_camera((4.8,-6,3.8),(0,0,1.7))
    stats = measure(rig,objects)
    stats.update({'name':name,'kind':kind,'grips':grips, 'actions':[a.name for a in actions]})
    folder = OUT/name
    source_path = folder/(name+'_V11.blend')
    if source_path.exists():
        raise RuntimeError('Refusing to overwrite a V1.1 source checkpoint: '+str(source_path))
    bpy.data.libraries.write(str(source_path), {scene}|set(actions), fake_user=True, path_remap='RELATIVE_ALL')
    (folder/(name+'_V11-stats.json')).write_text(json.dumps(stats,indent=2))
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    return {'scene':scene,'rig':rig,'objects':objects,'actions':actions,
            'stats':stats,'source_path':str(source_path),'atlas_path':str(atlas)}
