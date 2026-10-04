"""V1.2 review renders through the Blender MCP session (evidence only, never approval).

render(name, pose='Idle', frame=None, view='front-quarter', out='iter') renders the V1.2 source scene
with linear skinning. Poses: rest, any clip name with an optional frame, or an arm test pose
('arm45', 'arm90', 'forward', 'overhead') solved with the shared clavicle share of the V1.2 validator.
All pose/action/camera state is restored afterwards.
"""
import math
from pathlib import Path

import bpy
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Captures/EnemyRosterV12'
VIEWS = {'front': (0, -8, 2.6), 'front-quarter': (5.2, -6.6, 3.4), 'side': (8, -.2, 2.8),
         'rear-quarter': (5.2, 6.6, 3.6), 'three-quarter-left': (-5.2, -6.6, 3.4)}
ARM = {'arm45': 45.0, 'arm90': 90.0, 'overhead': 150.0}


def _scene(name):
    scene = bpy.data.scenes['EnemyRosterV12_' + name]
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    return scene, rig


def _reset(rig):
    rig.animation_data.action = None
    for b in rig.pose.bones:
        b.rotation_mode = 'QUATERNION'
        b.rotation_quaternion = Quaternion()
        b.location = (0, 0, 0)
    bpy.context.view_layer.update()


def _rotate(rig, bone, axis, degrees):
    pb = rig.pose.bones[bone]
    basis = pb.bone.matrix_local.to_quaternion()
    pb.rotation_quaternion = basis.inverted() @ Quaternion(Vector(axis), math.radians(degrees)) @ basis


def _aim(pb, target):
    current = (pb.tail - pb.head).normalized()
    rot = current.rotation_difference(target.normalized())
    m = rot.to_matrix().to_4x4() @ pb.matrix
    m.translation = pb.head.copy()
    pb.matrix = m
    bpy.context.view_layer.update()


def arm_pose(rig, pose):
    """Absolute elevation from straight down; clavicle takes 25% of the change (cap 30 degrees)."""
    _reset(rig)
    for side in ('L', 'R'):
        upper = rig.pose.bones['UpperArm_' + side]
        rest = upper.bone.tail_local - upper.bone.head_local
        sign = 1.0 if rest.x > 0 else -1.0
        rest_angle = math.degrees(math.atan2(abs(rest.x), -rest.z))
        if pose == 'forward':
            _rotate(rig, 'Clavicle_' + side, (1, 0, 0), -12)
            target = Vector((sign * .12, -1, -.05))
        else:
            elevation = ARM[pose]
            share = max(-30.0, min(30.0, (elevation - rest_angle) * .25))
            _rotate(rig, 'Clavicle_' + side, (0, 1, 0), -sign * share)
            r = math.radians(elevation)
            target = Vector((sign * math.sin(r), 0, -math.cos(r)))
        bpy.context.view_layer.update()
        _aim(upper, target)


def render(name, pose='Idle', frame=None, view='front-quarter', out='iter', lod=0, samples=16, res=(900, 1000),
           ortho=4.8, target_z=1.6):
    scene, rig = _scene(name)
    prev_scene = bpy.context.window.scene
    bpy.context.window.scene = scene
    action = rig.animation_data.action
    transforms = {b.name: (b.rotation_mode, b.matrix_basis.copy()) for b in rig.pose.bones}
    cam = scene.camera
    saved = (cam.matrix_world.copy(), cam.data.type, cam.data.ortho_scale, scene.render.engine,
             scene.render.resolution_x, scene.render.resolution_y, scene.render.filepath, scene.frame_current)
    vis = {o: (o.hide_get(), o.hide_render) for o in scene.objects if o.type == 'MESH'}
    folder = OUT / out
    folder.mkdir(parents=True, exist_ok=True)
    label = pose + ('' if frame is None else '-f%03d' % frame)
    path = folder / f'{name}-{view}-{label}-LOD{lod}.png'
    try:
        for o in vis:
            hidden = not o.name.endswith('_LOD%d' % lod)
            o.hide_set(hidden)
            o.hide_render = hidden
        if pose == 'rest':
            _reset(rig)
        elif pose in ARM or pose == 'forward':
            arm_pose(rig, pose)
        else:
            act = bpy.data.actions['Rig_' + name + 'V12|' + pose]
            rig.animation_data.action = act
            start, end = act.frame_range
            scene.frame_set(int(frame if frame is not None else start))
        bpy.context.view_layer.update()
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
        cam.location = VIEWS[view]
        cam.rotation_euler = (Vector((0, 0, target_z)) - Vector(VIEWS[view])).to_track_quat('-Z', 'Y').to_euler()
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = samples
        scene.cycles.use_denoising = True
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.resolution_percentage = 100
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        return str(path)
    finally:
        rig.animation_data.action = action
        for b in rig.pose.bones:
            b.rotation_mode, b.matrix_basis = transforms[b.name]
        (cam.matrix_world, cam.data.type, cam.data.ortho_scale, scene.render.engine, scene.render.resolution_x,
         scene.render.resolution_y, scene.render.filepath, frame_current) = saved
        scene.frame_set(frame_current)
        for o, s in vis.items():
            o.hide_set(s[0])
            o.hide_render = s[1]
        bpy.context.window.scene = prev_scene
