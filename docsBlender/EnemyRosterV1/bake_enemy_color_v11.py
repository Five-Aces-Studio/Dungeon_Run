"""Bake broad authored surface-color fields to a portable atlas in live Blender.

This is a material bake, not a hand-painted illustration or a new renderer.
Only V1.1-owned meshes/materials are changed; the V1 checkpoint is untouched.
"""
from pathlib import Path
import math
import bpy

ROOT = Path(__file__).resolve().parents[2]

def bake(name, category, resolution=1024):
    scene = bpy.data.scenes['EnemyRosterV11_' + name]
    previous_scene = bpy.context.window.scene
    bpy.context.window.scene = scene
    rig = next(o for o in scene.objects if o.type == 'ARMATURE')
    sources = sorted([o for o in scene.objects if o.type == 'MESH' and o.name.endswith('_LOD0')], key=lambda o:o.name)
    if any(o.get('V11ColorBaked') for o in sources):
        raise RuntimeError('Color pass already applied; use the saved pre-bake checkpoint for revision')
    if any(len(o.data.materials) != 1 for o in sources):
        raise RuntimeError('Color bake requires exactly one existing material family per modular mesh')
    image = bpy.data.images.new(name + '_ColorV11', width=resolution, height=resolution, alpha=False)
    directory = ROOT / 'Assets/Art/Characters/Enemies' / category / name / 'Textures'
    directory.mkdir(parents=True, exist_ok=True)
    # Preserve the importer filename contract, but this now contains real unwrapped surface-color data.
    path = directory / (name + '_Palette.png')
    image.filepath_raw = str(path)
    image.file_format = 'PNG'
    columns = math.ceil(math.sqrt(len(sources)))
    rows = math.ceil(len(sources) / columns)
    saved_engine, samples = scene.render.engine, scene.cycles.samples
    saved_bake = (scene.render.bake.use_clear, scene.render.bake.margin, scene.render.bake.use_selected_to_active)
    visibility = {o:(o.hide_get(),o.hide_render) for o in scene.objects if o.type == 'MESH'}
    original_materials = {}
    temporary_materials = []
    original_action = rig.animation_data.action
    original_pose_position = rig.data.pose_position
    try:
        rig.data.pose_position = 'REST'
        rig.animation_data.action = None
        bpy.context.view_layer.update()
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 1
        scene.render.bake.margin = 8
        scene.render.bake.use_selected_to_active = False
        for index, ob in enumerate(sources):
            ob.hide_set(False)
            mesh = ob.data
            original_materials[ob] = list(mesh.materials)
            old_uv = mesh.uv_layers.active
            old_uv.name = 'PaletteSourceUV'
            color = mesh.color_attributes.new(name='BroadColorV11', type='FLOAT_COLOR', domain='CORNER')
            pixels_by_image = {}
            textures = {}
            for slot, mat in enumerate(mesh.materials):
                textures[slot] = next(n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image)
                im = textures[slot]
                if im.name not in pixels_by_image:
                    pixels_by_image[im.name] = list(im.pixels)
            for poly in mesh.polygons:
                source_image = textures[poly.material_index]
                pixels = pixels_by_image[source_image.name]
                w, h = source_image.size
                for li in poly.loop_indices:
                    uv = old_uv.data[li].uv
                    ix = min(w-1, max(0, int(uv.x*w)))
                    iy = min(h-1, max(0, int(uv.y*h)))
                    c = pixels[4*(iy*w+ix):4*(iy*w+ix)+3]
                    # PNG image pixels are encoded sRGB; color attributes/emission require scene-linear values.
                    c = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in c]
                    vertex = mesh.vertices[mesh.loops[li].vertex_index]
                    p = ob.matrix_world @ vertex.co
                    field = .87 + .09*math.sin(p.x*2.4+p.z*1.1) + .055*math.cos(p.y*2.1-p.z*1.7)
                    field += .07*min(1, max(0, p.z/3.5))
                    if c[1] > c[0]*1.25 and c[1] > c[2]*1.2:
                        cool = .5 + .5*math.sin(p.x*1.7+p.y*1.3-p.z*.8)
                        c = [c[0]*(.94-.08*cool), c[1]*(.94-.06*cool), c[2]*(1.02+.13*cool)]
                    color.data[li].color = (*[min(1,max(0,value*field)) for value in c],1)
            uv = mesh.uv_layers.new(name='ColorUV')
            mesh.uv_layers.active = uv
            uv.active_render = True
            bpy.ops.object.select_all(action='DESELECT')
            ob.select_set(True)
            bpy.context.view_layer.objects.active = ob
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.035)
            bpy.ops.object.mode_set(mode='OBJECT')
            # Edit-mode UV operations invalidate RNA handles to mesh custom-data layers.
            mesh = ob.data
            uv = mesh.uv_layers['ColorUV']
            old_uv = mesh.uv_layers['PaletteSourceUV']
            for loop in uv.data:
                loop.uv = ((index % columns + .025 + loop.uv.x*.95)/columns,
                           (index // columns + .025 + loop.uv.y*.95)/rows)
            bake_mat = bpy.data.materials.new(name + '_TransientColorBake')
            bake_mat.use_nodes = True
            nodes = bake_mat.node_tree.nodes
            nodes.clear()
            output = nodes.new('ShaderNodeOutputMaterial')
            emission = nodes.new('ShaderNodeEmission')
            attribute = nodes.new('ShaderNodeVertexColor')
            attribute.layer_name = 'BroadColorV11'
            bake_mat.node_tree.links.new(attribute.outputs['Color'],emission.inputs['Color'])
            bake_mat.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
            target = nodes.new('ShaderNodeTexImage')
            target.image = image
            nodes.active = target
            temporary_materials.append(bake_mat)
            mesh.materials.clear()
            mesh.materials.append(bake_mat)
            for poly in mesh.polygons:poly.material_index=0
            scene.render.bake.use_clear = index == 0
            bpy.ops.object.bake(type='EMIT')
            mesh.materials.clear()
            for mat in original_materials[ob]:mesh.materials.append(mat)
            mesh.uv_layers.remove(old_uv)
            uv.name = 'UV0'
            ob['V11ColorBaked'] = True
        image.save()
        for mat in {m for mats in original_materials.values() for m in mats}:
            for node in mat.node_tree.nodes:
                if node.type == 'TEX_IMAGE':
                    node.image = image
                    node.interpolation = 'Linear'
        # Regenerate only new V11 LOD mesh data so the new UV seams transfer coherently.
        for source in sources:
            for lod, ratio in ((1,.5),(2,.25)):
                name_lod = source.name.removesuffix('_LOD0') + '_LOD' + str(lod)
                destination = bpy.data.objects.get(name_lod)
                if destination is None:
                    raise RuntimeError('Missing owned V11 LOD: ' + name_lod)
                temporary = source.copy()
                temporary.data = source.data.copy()
                scene.collection.objects.link(temporary)
                temporary.hide_set(False)
                bpy.context.view_layer.objects.active = temporary
                reduction = temporary.modifiers.new('ColorPassLOD', 'DECIMATE')
                reduction.ratio = ratio
                temporary.modifiers.move(len(temporary.modifiers)-1,0)
                bpy.ops.object.modifier_apply(modifier=reduction.name)
                destination.data = temporary.data
                bpy.data.objects.remove(temporary,do_unlink=True)
        return {'image':str(path),'resolution':resolution,'method':'Broad deterministic surface-color field baked through Blender emission; not hand-painted art'}
    finally:
        if bpy.context.object and bpy.context.object.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        for ob, mats in original_materials.items():
            if any(m in temporary_materials for m in ob.data.materials):
                ob.data.materials.clear()
                for mat in mats:ob.data.materials.append(mat)
        for mat in temporary_materials:
            bpy.data.materials.remove(mat)
        rig.data.pose_position = original_pose_position
        rig.animation_data.action = original_action
        scene.render.engine, scene.cycles.samples = saved_engine, samples
        scene.render.bake.use_clear, scene.render.bake.margin, scene.render.bake.use_selected_to_active = saved_bake
        for ob,state in visibility.items():
            ob.hide_set(state[0]);ob.hide_render=state[1]
        bpy.context.window.scene = previous_scene
