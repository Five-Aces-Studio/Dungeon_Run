"""Non-destructive pixel-aware variants. Run Blender --background --python this_file.

Reuses the canonical generator in memory, stopping before its save/export block.
Fail-fast replacement guards deliberately require review when canonical source changes.
"""
import json
from pathlib import Path
import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[1]
ORIGINAL = SOURCE / 'TrigonalAbyssStage.blend'
OUT = ROOT / 'Assets/Meshes/TrigonalAbyss/PixelAware'
BLENDS = SOURCE / 'PixelAware'
OUT.mkdir(parents=True, exist_ok=True)
BLENDS.mkdir(parents=True, exist_ok=True)

# Original pivots and material ordering are the mesh-swap coordinate contract.
bpy.ops.wm.open_mainfile(filepath=str(ORIGINAL))
original = {o.name: {'origin': tuple(o.location),
                    'materials': [m.name for m in o.data.materials]}
            for o in bpy.context.scene.objects if o.type == 'MESH'}
source = (SOURCE / 'build_stage.py').read_text()
stop = "bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'TrigonalAbyssStage.blend'))"
if source.count(stop) != 1:
    raise RuntimeError('Canonical export boundary changed; review generator integration.')
source = source.split(stop)[0]


def replace(text, old, new):
    if text.count(old) != 1:
        raise RuntimeError('Canonical source changed: ' + old)
    return text.replace(old, new)


variants = {
    'Chain15': {'wire': .124, 'groups': ['ENV_Chains_0', 'ENV_Chains_1']},
    'Chain20': {'wire': .165, 'groups': ['ENV_Chains_0', 'ENV_Chains_1']},
    'Chain30': {'wire': .248, 'groups': ['ENV_Chains_0', 'ENV_Chains_1']},
    'ChainSparse20': {'wire': .165, 'links': 23, 'ring': .255,
                      'groups': ['ENV_Chains_0', 'ENV_Chains_1']},
    'Staff20': {'shaft': .121, 'terminal': .121, 'groups': ['CHR_Player']},
    'Staff30': {'shaft': .181, 'terminal': .181, 'groups': ['CHR_Player']},
    'FloorSimple': {'groups': ['ENV_FloorTiles_' + str(i) for i in range(5)]},
}
report = {'canonical_source': 'build_stage.py', 'original_blend': str(ORIGINAL), 'variants': {}}
for name, config in variants.items():
    code = source
    if 'wire' in config:
        code = replace(code, 'major_radius=.16,minor_radius=.035',
                       'major_radius=%r,minor_radius=%r' % (config.get('ring', .16), config['wire'] / 2))
        if 'links' in config:
            code = replace(code, 'for i in range(35):\n        t=i/34',
                           'for i in range(23):\n        t=i/22')
    if 'shaft' in config:
        code = replace(code, "link(g,(-5.1,.3,-.05),(-5.1,2.8,-.05),.047,'MetalDark')",
                       "link(g,(-5.1,.3,-.05),(-5.1,2.8,-.05),%r,'MetalDark')" % (config['shaft']/2))
        code = replace(code, "cone(g,(-5.1,2.76,-.05),.12,0,.28,'ArcaneCool',4,0)",
                       "cone(g,(-5.1,2.76,-.05),.12,%r,.28,'ArcaneCool',4,0)" % (config['terminal']/2))
    if name == 'FloorSimple':
        code = replace(code, 'for row in range(10):\n    for col in range(11):',
                       'for row in range(5):\n    for col in range(7):')
        code = replace(code, '-15+col*3+random.uniform(-.72,.72)', '-15+col*5+random.uniform(-.72,.72)')
        code = replace(code, '-8.6+row*1.94+random.uniform(-.48,.48)', '-8.6+row*4.5+random.uniform(-.48,.48)')
        code = replace(code, 'if (ox-x)**2+(oz-z)**2 > 65: continue', '# Larger slabs: clip against every seed.')
        code = replace(code, 'if len(contour)<7:', 'if False:  # No subpixel corner chips in this variant.')
        code = replace(code, "finish(obj,'ENV_FloorTiles_'+str(row//2),'StoneFloor',.025)",
                       "finish(obj,'ENV_FloorTiles_'+str(row),'StoneFloor',0)")
    namespace = {'__file__': str(SOURCE / 'build_stage.py'), '__name__': '__pixel_variant__'}
    exec(compile(code, str(SOURCE / 'build_stage.py'), 'exec'), namespace)
    keep = set(config['groups'])
    for ob in list(bpy.context.scene.objects):
        if ob.name not in keep:
            bpy.data.objects.remove(ob, do_unlink=True)
    details = {}
    for ob in list(bpy.context.scene.objects):
        baseline = original[ob.name]
        # Rebase vertices without moving any world-space geometry.
        desired = Vector(baseline['origin'])
        shift = ob.location - desired
        for vertex in ob.data.vertices:
            vertex.co += shift
        ob.location = desired
        slots = [m.name for m in ob.data.materials]
        targets = baseline['materials']
        polygon_slots = [targets.index(slots[p.material_index]) for p in ob.data.polygons]
        ob.data.materials.clear()
        for material in targets:
            ob.data.materials.append(bpy.data.materials[material])
        for polygon, slot in zip(ob.data.polygons, polygon_slots):
            polygon.material_index = slot
        ob.data.name = ob.name
        ob.data.calc_loop_triangles()
        details[ob.name] = {'triangles': len(ob.data.loop_triangles), 'vertices': len(ob.data.vertices),
                            'materials': targets, 'origin_blender': list(ob.location)}
    blend = BLENDS / (name + '.blend')
    fbx = OUT / (name + '.fbx')
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.export_scene.fbx(filepath=str(fbx), object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                            global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                            use_space_transform=True, bake_space_transform=True, bake_anim=False,
                            add_leaf_bones=False, use_mesh_modifiers=True, path_mode='AUTO')
    probes = []
    if 'wire' in config:
        probes = [{'feature': 'chain_wire_nominal_diameter', 'width': config['wire'],
                   'unity_center': [x, 8, z],
                   'note': 'Circular wire nominal diameter; actual projected facets depend on link orientation.'}
                  for x, z in [(-6.5, 11), (6, 12.5)]]
    if 'shaft' in config:
        player = bpy.data.objects['CHR_Player']
        ring = [player.matrix_world @ v.co for v in player.data.vertices
                if abs((player.matrix_world @ v.co).z - .3) < .0001
                and abs((player.matrix_world @ v.co).x + 5.1) < config['shaft']]
        probes = [{'feature': 'staff_shaft', 'width': config['shaft'],
                   'unity_center': [-5.1, 1.55, -.05],
                   'mesh_world_x_extent': max(v.x for v in ring) - min(v.x for v in ring),
                   'mesh_world_z_extent': max(v.y for v in ring) - min(v.y for v in ring)},
                  {'feature': 'staff_tip_terminal', 'width': config['terminal'],
                   'unity_center': [-5.1, 2.90, -.05],
                   'note': 'Four-sided terminal nominal diameter; orientation-dependent projected width.'}]
    if name == 'FloorSimple':
        probes = [{'feature': 'floor_bevel', 'width': 0, 'unity_center': [0, 0, 0]},
                  {'feature': 'floor_joint', 'slab_count': 35, 'contour_scale': .985,
                   'note': 'Variable Voronoi joint width; no fixed world or pixel-width claim.'}]
    report['variants'][name] = {'parameters': config, 'objects': details, 'probes': probes,
                                'fbx': str(fbx), 'blend': str(blend)}
    print('PIXEL_CONTENT_VARIANT', name, json.dumps(details))
(BLENDS / 'variant_manifest.json').write_text(json.dumps(report, indent=2))
