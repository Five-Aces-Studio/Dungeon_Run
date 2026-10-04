"""V1.2 reversible captures and scoped export; execute in Blender through MCP.

Reuses the V1.1 implementation without changing its globals or source file.
Arm labels are absolute elevation from downward, not rotation deltas from the
approximately 42-degree outward bind pose. ``overhead`` means practical 150
degrees, not a forced 180-degree stress pose. Renders are evidence, not approval.
"""
from pathlib import Path
import json
import math
import bpy
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Captures/EnemyRosterV12'
VERSION = 'V12'
ATTACK_FRAMES = (9, 24, 34, 48)
ARM_ELEVATIONS = {'arm45': 45.0, 'arm90': 90.0, 'overhead': 150.0}
CLIPS = ('Idle', 'Attack', 'HitReaction', 'Death')


def _helpers():
    source = Path(__file__).with_name('validate_enemy_slice_v11.py')
    namespace = {'__file__': str(source), '__name__': 'v12_private_helpers'}
    exec(compile(source.read_text(encoding='utf-8'), str(source), 'exec'), namespace)
    namespace['OUT'] = OUT
    original_find = namespace['find_source']
    namespace['find_source'] = lambda name, version=VERSION: original_find(name, version)
    return namespace


def find_source(name):
    scene, rig = _helpers()['find_source'](name)
    if rig.name != 'Rig_' + name + VERSION:
        raise ValueError('Unexpected source rig: ' + rig.name)
    return scene, rig


def _linear_skinning(scene, rig):
    incompatible = [ob.name for ob in scene.objects for modifier in ob.modifiers
                    if modifier.type == 'ARMATURE' and modifier.object == rig
                    and modifier.use_deform_preserve_volume]
    if incompatible:
        raise RuntimeError('Unity comparison requires Preserve Volume disabled: '
                           + ', '.join(incompatible))
    if rig.animation_data is None:
        raise RuntimeError('Source rig needs animation data')
    if len(rig.animation_data.nla_tracks):
        raise RuntimeError('Review existing NLA tracks before isolated pose evaluation')


def _align_direction(bone, target):
    """Rotate in armature space, preserving the joint position and axial roll."""
    current = (bone.tail - bone.head).normalized()
    rotation = current.rotation_difference(target.normalized())
    matrix = rotation.to_matrix().to_4x4() @ bone.matrix
    matrix.translation = bone.head.copy()
    bone.matrix = matrix
    bpy.context.view_layer.update()


def arm_pose(rig, pose, helpers=None):
    """Share frontal elevation with the clavicle, then aim each upper arm exactly.

    Targets use source armature coordinates (+Z up, -Y forward). Bind direction
    comes from the actual shoulder-to-elbow bone vector, never a guessed delta.
    Clavicles take 25% of the frontal elevation change, capped at 30 degrees;
    the upper arm solves the remaining rotation after the parent has evaluated.
    """
    helpers = helpers or _helpers()
    helpers['reset_pose'](rig)
    measurements = []
    for side in ('L', 'R'):
        upper = rig.pose.bones['UpperArm_' + side]
        clavicle = rig.pose.bones['Clavicle_' + side]
        rest = upper.bone.tail_local - upper.bone.head_local
        sign = 1.0 if rest.x > 0 else -1.0
        rest_angle = math.degrees(math.atan2(abs(rest.x), -rest.z))
        if pose == 'forward':
            target = Vector((0, -1, 0))
            # Small forward clavicle contribution; the subsequent solve accounts
            # for its evaluated effect rather than adding two assumed angles.
            helpers['rotate'](rig, clavicle.name, (1, 0, 0), -12)
        else:
            elevation = ARM_ELEVATIONS[pose]
            radians = math.radians(elevation)
            target = Vector((sign * math.sin(radians), 0, -math.cos(radians)))
            shared = max(-30.0, min(30.0, (elevation - rest_angle) * .25))
            helpers['rotate'](rig, clavicle.name, (0, 1, 0), -sign * shared)
        bpy.context.view_layer.update()
        _align_direction(upper, target)
        actual = (upper.tail - upper.head).normalized()
        error = math.degrees(actual.angle(target.normalized()))
        if error > .25:
            raise RuntimeError('Arm target not reached: %s %.3f degrees' % (side, error))
        measurements.append({'side': side, 'bind_elevation_degrees': rest_angle,
                             'target_direction': list(target),
                             'actual_direction': list(actual),
                             'direction_error_degrees': error})
    return measurements


def structural_report(name):
    helpers = _helpers()
    report = helpers['structural_report'](name, VERSION)
    scene, rig = find_source(name)
    report['preserve_volume_meshes'] = [ob.name for ob in scene.objects
        for modifier in ob.modifiers if modifier.type == 'ARMATURE'
        and modifier.object == rig and modifier.use_deform_preserve_volume]
    report['source_rig'] = rig.name
    return report


def capture(name, pose='Idle', view='front-quarter', batch='current', lod=0,
            frame=None, silhouette=False):
    """Return a rendered PNG path plus adjacent JSON metadata; refuse overwrites.

    Poses: neutral, arm45, arm90, forward, overhead, V1.1 torso/head/jaw/tail
    stress poses, Idle, Attack, Hit (alias HitReaction), Death. Explicit clip
    frames are supported; Attack defaults to frame 24 and Death to its final
    frame. Use ATTACK_FRAMES for windup/strike/follow-through/recovery captures.
    Source action, frame, pose matrices, visibility, camera and renderer restore
    via the V1.1 helper even when a render or pose evaluation fails.
    """
    helpers = _helpers()
    scene, rig = find_source(name)
    _linear_skinning(scene, rig)
    if scene.camera is None:
        raise RuntimeError('Source scene needs its review camera')
    if view not in helpers['VIEWS'] or lod not in (0, 1, 2):
        raise ValueError('Unsupported view or LOD')
    pose = 'HitReaction' if pose == 'Hit' else pose
    metadata = {'name': name, 'version': VERSION, 'pose': pose, 'view': view,
                'lod': lod, 'silhouette': silhouette, 'linear_skinning': True}
    selected_action = None
    if pose in CLIPS:
        selected_action = bpy.data.actions.get(rig.name + '|' + pose)
        if selected_action is None:
            raise ValueError('Missing action ' + rig.name + '|' + pose)
        start, end = selected_action.frame_range
        if frame is None:
            frame = start if pose == 'Idle' else end if pose == 'Death' else (
                24 if pose == 'Attack' else round((start + end) / 2))
        if not start <= frame <= end or float(frame) != int(frame):
            raise ValueError('Capture frame must be an integer within the clip range')
        frame = int(frame)
        label = pose + '-f%03d' % frame
        metadata['frame'] = frame
        metadata['action'] = selected_action.name
    else:
        if frame is not None:
            raise ValueError('Frame is only valid for an animation clip')
        if pose not in ('neutral', 'forward') and pose not in ARM_ELEVATIONS \
                and pose not in helpers['EXTREMES']:
            raise ValueError('Unknown pose: ' + pose)
        label = pose
    original_extreme = helpers['extreme_pose']

    def apply_pose(source_rig, unused_label):
        if selected_action is not None:
            helpers['reset_pose'](source_rig)
            source_rig.animation_data.action = selected_action
            scene.frame_set(frame)
            bpy.context.view_layer.update()
        elif pose == 'neutral':
            helpers['reset_pose'](source_rig)
        elif pose == 'forward' or pose in ARM_ELEVATIONS:
            metadata['arms'] = arm_pose(source_rig, pose, helpers)
        else:
            original_extreme(source_rig, pose)

    helpers['EXTREMES'] = tuple(helpers['EXTREMES']) + (label,)
    helpers['extreme_pose'] = apply_pose
    path = helpers['capture'](name, VERSION, view, label, lod, silhouette, batch)
    Path(path).with_suffix('.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    return path


def export_candidate(name, category):
    """Export only this V1.2 rig, its meshes and four scoped validation clips."""
    scene, rig = find_source(name)
    _linear_skinning(scene, rig)
    for clip in CLIPS:
        if bpy.data.actions.get(rig.name + '|' + clip) is None:
            raise ValueError('Missing scoped clip: ' + clip)
    return _helpers()['export_candidate'](name, category)
