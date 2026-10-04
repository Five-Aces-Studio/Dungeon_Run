"""Interactive-only, reversible source capture and validation helpers for V1.1."""
from pathlib import Path
import json
import math
import bpy
import bmesh
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Captures/EnemyRosterV11'
VIEWS = {
    'front': (0, -7, 2.9), 'front-quarter': (4.8, -6, 3.8),
    'side': (7, -.2, 3.1), 'rear-quarter': (4.8, 6, 3.8), 'back': (0, 7, 3.2),
}
EXTREMES = ('arms-forward', 'arms-overhead', 'body-twist', 'head-turn', 'jaw-open',
            'tail-left', 'tail-right', 'tail-compression', 'tail-straight', 'tail-s-curve')

def find_source(name, version='V11'):
    scene = bpy.data.scenes['EnemyRoster' + version + '_' + name]
    rigs = [o for o in scene.objects if o.type == 'ARMATURE']
    if len(rigs) != 1:
        raise ValueError('Expected exactly one source rig in ' + scene.name)
    return scene, rigs[0]

def reset_pose(rig):
    rig.animation_data.action = None
    for b in rig.pose.bones:
        b.rotation_mode = 'QUATERNION'
        b.rotation_quaternion = Quaternion()
        b.location = (0, 0, 0)
        b.scale = (1, 1, 1)
    bpy.context.view_layer.update()

def rotate(rig, name, axis, degrees):
    bone = rig.pose.bones.get(name)
    if bone is None:
        return
    basis = bone.bone.matrix_local.to_quaternion()
    bone.rotation_mode = 'QUATERNION'
    bone.rotation_quaternion = basis.inverted() @ Quaternion(Vector(axis), math.radians(degrees)) @ basis

def extreme_pose(rig, pose):
    reset_pose(rig)
    if pose.startswith('arms-'):
        for side in ('L', 'R'):
            if pose == 'arms-forward':
                rotate(rig, 'Clavicle_' + side, (1, 0, 0), -10)
                rotate(rig, 'UpperArm_' + side, (1, 0, 0), -75)
            else:
                # Abduct about the frontal-plane axis; X rotation tested backward hyperextension instead.
                sign = -1 if side == 'L' else 1
                rotate(rig, 'Clavicle_' + side, (0, 1, 0), sign*25)
                rotate(rig, 'UpperArm_' + side, (0, 1, 0), sign*115)
    elif pose == 'body-twist':
        rotate(rig, 'Spine', (0, 0, 1), 25)
        rotate(rig, 'Chest', (0, 0, 1), 30)
    elif pose == 'head-turn':
        for b in rig.pose.bones:
            if b.name.startswith('Head'):
                rotate(rig, b.name, (0, 0, 1), 65)
    elif pose == 'jaw-open':
        for b in rig.pose.bones:
            if b.name.startswith('Jaw'):
                rotate(rig, b.name, (1, 0, 0), 30)
    elif pose in ('tail-left', 'tail-right', 'tail-s-curve', 'tail-compression'):
        for i in range(1, 9):
            angle = 13 if pose == 'tail-left' else -13
            axis = (0, 0, 1)
            if pose == 'tail-s-curve':
                angle = 18 if i < 4 else -18
            if pose == 'tail-compression':
                angle = 17 if i % 2 else -17
                axis = (1, 0, 0)
            rotate(rig, 'Tail_%02d' % i, axis, angle)
    elif pose == 'tail-straight':
        for i in range(1, 9):
            b = rig.pose.bones.get('Tail_%02d' % i)
            if not b:
                continue
            bpy.context.view_layer.update()
            current = (b.tail - b.head).normalized()
            q = current.rotation_difference(Vector((0, 1, 0)))
            matrix = q.to_matrix().to_4x4() @ b.matrix
            matrix.translation = b.head
            b.matrix = matrix
    else:
        raise ValueError('Unknown extreme pose: ' + pose)
    bpy.context.view_layer.update()

def structural_report(name, version='V11'):
    scene, rig = find_source(name, version)
    results = []
    for ob in scene.objects:
        if ob.type != 'MESH':
            continue
        mesh = ob.data
        mesh.calc_loop_triangles()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        skin = any(m.type == 'ARMATURE' for m in ob.modifiers)
        weight_errors = sum(1 for v in mesh.vertices if skin and
                            (not v.groups or abs(sum(g.weight for g in v.groups) - 1) > 1e-4))
        results.append({
            'mesh': ob.name, 'vertices': len(mesh.vertices), 'triangles': len(mesh.loop_triangles),
            'boundary_edges': sum(1 for e in bm.edges if e.is_boundary),
            'nonmanifold_edges': sum(1 for e in bm.edges if not e.is_manifold),
            'zero_area_faces': sum(1 for f in bm.faces if f.calc_area() < 1e-10),
            'invalid_weights': weight_errors, 'max_influences': max((len(v.groups) for v in mesh.vertices), default=0),
            'missing_bones': [g.name for g in ob.vertex_groups if g.name not in rig.data.bones] if skin else [],
            'rigid_weapon': ob.parent_type == 'BONE', 'uv_layers': [u.name for u in mesh.uv_layers],
        })
        bm.free()
    return {'name': name, 'version': version, 'bones': len(rig.data.bones), 'meshes': results,
            'scope': 'Structure only; visual deformation and intersection review required.'}

def capture(name, version='V11', view='front-quarter', pose='Idle', lod=0, silhouette=False, batch='current'):
    """Render one exact view and restore all source states, including actions and visibility."""
    scene, rig = find_source(name, version)
    previous_scene = bpy.context.window.scene
    previous_action = rig.animation_data.action
    previous_frame = scene.frame_current
    transforms = {b.name: (b.rotation_mode, b.matrix_basis.copy()) for b in rig.pose.bones}
    bpy.context.window.scene = scene
    if scene.camera is None:
        raise RuntimeError('Source scene needs its review camera')
    camera = scene.camera
    cam_matrix = camera.matrix_world.copy()
    camera_type, ortho = camera.data.type, camera.data.ortho_scale
    render = scene.render
    saved = (render.engine, render.resolution_x, render.resolution_y, render.resolution_percentage,
             render.filepath, scene.cycles.samples, scene.view_layers[0].material_override)
    visibility = {o: (o.hide_get(), o.hide_render) for o in scene.objects if o.type == 'MESH'}
    folder = OUT / batch / version
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / f'{name}-{view}-{pose}-LOD{lod}{"-silhouette" if silhouette else ""}.png'
    if path.exists():
        bpy.context.window.scene = previous_scene
        raise FileExistsError(str(path))
    black = None
    try:
        for ob in visibility:
            hidden = not ob.name.endswith('_LOD' + str(lod))
            ob.hide_set(hidden)
            ob.hide_render = hidden
        if pose in EXTREMES:
            extreme_pose(rig, pose)
        else:
            action = bpy.data.actions.get(rig.name + '|' + pose)
            if action is None:
                raise ValueError('Missing action ' + rig.name + '|' + pose)
            rig.animation_data.action = action
            scene.frame_set(1 if pose == 'Idle' else int(action.frame_range[1] * (.85 if pose == 'Death' else .4)))
        camera.data.type = 'ORTHO'
        camera.data.ortho_scale = 4.6 if not pose.startswith('tail-') else 6.8
        camera.location = VIEWS[view]
        camera.rotation_euler = (Vector((0, 0, 1.7)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        render.engine = 'CYCLES'
        scene.cycles.samples = 12
        render.resolution_x, render.resolution_y, render.resolution_percentage = 900, 1000, 100
        render.filepath = str(path)
        if silhouette:
            black = bpy.data.materials.new('V11_TransientSilhouette')
            black.use_nodes = True
            nodes = black.node_tree.nodes
            nodes.clear()
            output = nodes.new('ShaderNodeOutputMaterial')
            emission = nodes.new('ShaderNodeEmission')
            emission.inputs['Color'].default_value = (0, 0, 0, 1)
            black.node_tree.links.new(emission.outputs[0], output.inputs['Surface'])
            scene.view_layers[0].material_override = black
        bpy.ops.render.render(write_still=True)
        return str(path)
    finally:
        rig.animation_data.action = previous_action
        scene.frame_set(previous_frame)
        for b in rig.pose.bones:
            b.rotation_mode, b.matrix_basis = transforms[b.name]
        for ob, state in visibility.items():
            ob.hide_set(state[0])
            ob.hide_render = state[1]
        camera.matrix_world = cam_matrix
        camera.data.type, camera.data.ortho_scale = camera_type, ortho
        (render.engine, render.resolution_x, render.resolution_y, render.resolution_percentage,
         render.filepath, scene.cycles.samples, scene.view_layers[0].material_override) = saved
        if black:
            bpy.data.materials.remove(black)
        bpy.context.window.scene = previous_scene

def export_candidate(name, category):
    """Explicit technical-review export; never implies visual approval."""
    scene, rig = find_source(name)
    previous_scene = bpy.context.window.scene
    bpy.context.window.scene = scene
    if len(rig.animation_data.nla_tracks):
        bpy.context.window.scene = previous_scene
        raise RuntimeError('Existing NLA tracks must be reviewed before export')
    selected = list(bpy.context.selected_objects)
    active = bpy.context.view_layer.objects.active
    action = rig.animation_data.action
    frame = scene.frame_current
    visibility = {o: (o.hide_get(), o.hide_render) for o in scene.objects if o.type == 'MESH'}
    tracks = []
    path = ROOT / 'Assets/Art/Characters/Enemies' / category / name / 'Models' / ('CHR_' + name + '.fbx')
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        rig.animation_data.action = None
        for clip in ('Idle', 'Attack', 'HitReaction', 'Death'):
            clip_action = bpy.data.actions[rig.name + '|' + clip]
            track = rig.animation_data.nla_tracks.new()
            track.name = 'V11Export_' + clip
            track.strips.new(clip, int(clip_action.frame_range[0]), clip_action)
            tracks.append(track)
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        for ob in visibility:
            ob.hide_set(False)
            ob.hide_render = False
            ob.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'ARMATURE', 'MESH'},
            axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=True,
            bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_simplify_factor=0,
            path_mode='RELATIVE', use_mesh_modifiers=True, use_armature_deform_only=False)
        return str(path)
    finally:
        for track in tracks:
            rig.animation_data.nla_tracks.remove(track)
        rig.animation_data.action = action
        scene.frame_set(frame)
        bpy.ops.object.select_all(action='DESELECT')
        for ob, state in visibility.items():
            ob.hide_set(state[0])
            ob.hide_render = state[1]
        for ob in selected:
            ob.select_set(True)
        bpy.context.view_layer.objects.active = active
        bpy.context.window.scene = previous_scene
