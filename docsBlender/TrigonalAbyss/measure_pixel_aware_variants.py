"""Read-only Blender mesh probes; writes JSON only under Captures/PixelContentV1.

Run Blender --background --python this_file. Never saves imported .blend files.
"""
import json
import math
from pathlib import Path
import bpy
from mathutils import Euler, Vector

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[1]
OUTPUT = ROOT / 'Captures/PixelContentV1/blender_mesh_probes.json'
SOURCES = [('Original', SOURCE / 'TrigonalAbyssStage.blend')]
SOURCES += [(name, SOURCE / 'PixelAware' / (name + '.blend')) for name in
            ('Chain15', 'Chain20', 'Chain30', 'ChainSparse20', 'Staff20', 'Staff30', 'FloorSimple')]


def unity(point):
    return [point.x, point.z, point.y]


def unique(points):
    result = []
    for point in points:
        if not any((point - old).length < 1e-6 for old in result):
            result.append(point)
    return result


result = {'coordinate_contract': 'Unity world x/y/z from Blender world x/z/y; canonical identity stage placement.',
          'note': 'Project points through the live camera; wire ring dimensions are orientation-dependent, not universal diameter.',
          'sources': {}}
for label, path in SOURCES:
    bpy.ops.wm.open_mainfile(filepath=str(path))
    record = {'source_blend': str(path), 'chain_cross_sections': [], 'staff_sections': []}
    for chain_index, (x1, x2, depth) in enumerate(((-11, -2, 11), (0, 12, 12.5))):
        obj = bpy.data.objects.get('ENV_Chains_' + str(chain_index))
        if obj is None:
            continue
        count = 23 if label == 'ChainSparse20' else 35
        major = .255 if label == 'ChainSparse20' else .16
        if len(obj.data.vertices) != count * 40:
            raise RuntimeError('Unexpected chain topology; refusing assumed vertex ordering: ' + label)
        # Include both alternating orientations and both sides of midpoint.
        for link_index in sorted(set((0, 1, count // 2, count // 2 + 1))):
            t = link_index / (count - 1)
            center = Vector((x1 + (x2 - x1) * t, depth, 10.3 - 2.6 * math.sin(math.pi * t)))
            rotation = Euler((math.pi / 2, (link_index % 2) * math.pi / 2, 0), 'XYZ').to_matrix()
            for major_index in (0, 2):
                start = link_index * 40 + major_index * 4
                points = [obj.matrix_world @ obj.data.vertices[start + k].co for k in range(4)]
                angle = major_index * 2 * math.pi / 10
                expected = center + rotation @ Vector((major * math.cos(angle), major * math.sin(angle) * 1.28, 0))
                actual = sum(points, Vector()) / 4
                if (actual - expected).length > .0001:
                    raise RuntimeError('Torus vertex-order validation failed: %s link %d section %d error %g' %
                                       (label, link_index, major_index, (actual - expected).length))
                # Opposite points must share a midpoint, excluding accidental adjacent-ring slices.
                if ((points[0] + points[2]) * .5 - actual).length > .0001 or \
                   ((points[1] + points[3]) * .5 - actual).length > .0001:
                    raise RuntimeError('Non-opposite cross-section endpoints: ' + label)
                record['chain_cross_sections'].append({
                    'object': obj.name, 'link_index': link_index, 'major_index': major_index,
                    'center_unity': unity(actual), 'vertices_unity': [unity(p) for p in points],
                    'opposite_pair_widths_world': [(points[0] - points[2]).length, (points[1] - points[3]).length],
                    'expected_center_error': (actual - expected).length})
    player = bpy.data.objects.get('CHR_Player')
    if player is not None:
        vertices = [player.matrix_world @ v.co for v in player.data.vertices]
        for label_section, height, radius_limit in [('shaft_bottom', .3, .13), ('tip_terminal', 2.90, .13)]:
            points = unique([p for p in vertices if abs(p.z - height) < .0001
                             and abs(p.x + 5.1) < radius_limit and abs(p.y + .05) < radius_limit])
            if not points:
                raise RuntimeError('Missing player cross-section: ' + label_section)
            record['staff_sections'].append({'section': label_section, 'vertices_unity': [unity(p) for p in points],
                'world_x_extent': max(p.x for p in points) - min(p.x for p in points),
                'world_z_extent': max(p.y for p in points) - min(p.y for p in points)})
    result['sources'][label] = record
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
OUTPUT.write_text(json.dumps(result, indent=2))
print('PIXEL_MESH_PROBES', str(OUTPUT))
