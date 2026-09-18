"""Deterministic original Trigonal Abyss blockout+; run with Blender --background --python."""
import bpy
import bmesh
import math
import random
import json
from pathlib import Path
from mathutils import Vector

random.seed(2317)
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Meshes/TrigonalAbyss/TrigonalAbyssStage.fbx'
SOURCE = Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.materials):
    bpy.data.materials.remove(data)

PALETTE = {
    'TravelerCloth': (.31, .57, .59, 1),
    'StoneDark': (.115, .15, .155, 1),
    'StoneWorn': (.28, .31, .30, 1),
    'StoneFloor': (.21, .255, .25, 1),
    'StoneBackground': (.105, .14, .155, 1),
    'MetalDark': (.08, .09, .10, 1),
    'ArcaneWarm': (.9, .32, .065, 1),
    'ArcaneCool': (.16, .68, .64, 1),
}
MATS = {}
for name, color in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = color
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = color
    p.inputs['Roughness'].default_value = .82
    if name.startswith('Arcane'):
        p.inputs['Emission Color'].default_value = color
        p.inputs['Emission Strength'].default_value = 2
    MATS[name] = m

GROUPS = {}
def u(v):
    return Vector((v[0], v[2], v[1]))

def finish(obj, group, mat='StoneDark', bevel=0):
    obj.name = group + '_part'
    obj.data.materials.append(MATS[mat])
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new('Readable stone bevel', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    GROUPS.setdefault(group, []).append(obj)
    return obj

def box(group, pos, size, mat='StoneDark', bevel=.06):
    bpy.ops.mesh.primitive_cube_add(size=1, location=u(pos))
    obj = bpy.context.object
    obj.scale = (size[0], size[2], size[1])
    return finish(obj, group, mat, bevel)

def cone(group, pos, bottom, top, height, mat='StoneDark', sides=8, bevel=.035):
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=bottom, radius2=top, depth=height, location=u(pos))
    return finish(bpy.context.object, group, mat, bevel)

def link(group, start, end, radius=.07, mat='MetalDark', sides=6):
    a, b = u(start), u(end)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length, location=(a+b)/2)
    ob = bpy.context.object
    ob.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return finish(ob, group, mat)

def polyprism(group, contour, depth, mat='StoneDark', bevel=.04):
    # Contour is Unity x/y; extrusion has real front and back faces.
    front, back = depth
    verts = [u((x,y,z)) for z in (front,back) for x,y in contour]
    n = len(contour)
    faces = [tuple(range(n-1,-1,-1)), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(group)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj = bpy.data.objects.new(group,mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    return finish(obj,group,mat,bevel)

# Ancient paving uses shared Voronoi boundaries: large irregular slabs, narrow joints.
box('ENV_FloorFoundation', (0,-.48,.5), (32,.6,20), bevel=.18)
def clip_cell(poly, nx, nz, distance):
    result = []
    for a,b in zip(poly, poly[1:] + poly[:1]):
        da = a[0]*nx+a[1]*nz-distance
        db = b[0]*nx+b[1]*nz-distance
        if da <= 0: result.append(a)
        if (da <= 0) != (db <= 0):
            t=da/(da-db)
            result.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
    return result

seeds=[]
for row in range(10):
    for col in range(11):
        seeds.append((-15+col*3+random.uniform(-.72,.72),
                      -8.6+row*1.94+random.uniform(-.48,.48), row))
for x,z,row in seeds:
    contour=[(-16,-9.45),(16,-9.45),(16,10),(-16,10)]
    for ox,oz,_ in seeds:
        if ox == x and oz == z: continue
        if (ox-x)**2+(oz-z)**2 > 65: continue
        contour=clip_cell(contour,ox-x,oz-z,(ox*ox+oz*oz-x*x-z*z)*.5)
        if not contour: break
    if len(contour)<3: continue
    cx=sum(p[0] for p in contour)/len(contour)
    cz=sum(p[1] for p in contour)/len(contour)
    # Narrow mortar joints and one restrained broken corner rather than noisy chips.
    contour=[(cx+(px-cx)*.985,cz+(pz-cz)*.985) for px,pz in contour]
    if len(contour)<7:
        prev,current,nxt=contour[-1],contour[0],contour[1]
        t=random.uniform(.06,.14)
        contour=[(current[0]+(prev[0]-current[0])*t,current[1]+(prev[1]-current[1])*t),
                 (current[0]+(nxt[0]-current[0])*t,current[1]+(nxt[1]-current[1])*t)]+contour[1:]
    n=len(contour)
    verts=[u((px,y,pz)) for y in (-.22,0) for px,pz in contour]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new('AncientSlab')
    mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('AncientSlab',mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    finish(obj,'ENV_FloorTiles_'+str(row//2),'StoneFloor',.025)
# Single shallow, broad dark metal ribbon: a restrained embedded trigonal mark.
points=[(-2.25,.008,3.25),(2.25,.008,3.25),(0,.008,.3)]
for a,b in zip(points,points[1:]+points[:1]):
    a,b=Vector(a),Vector(b)
    d=(b-a).normalized()
    side=Vector((-d.z,0,d.x))*.055
    corners=[a-side,b-side,b+side,a+side]
    verts=[u((v.x,y,v.z)) for y in (-.028,.008) for v in corners]
    faces=[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    mesh=bpy.data.meshes.new('InsetTrigonalRibbon');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('InsetTrigonalRibbon',mesh);bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    finish(obj,'ENV_FloorInlay','MetalDark')

def pillar(name,x,z,height,r=1.0):
    cone(name,(x,.24,z),r*1.4,r*1.4,.48,'StoneWorn')
    cone(name,(x,.65,z),r*1.24,r*1.15,.34)
    count = int((height-1.4)/1.55)
    for i in range(count):
        y = .9+(i+.5)*(height-1.8)/count
        cone(name,(x,y,z),r,r*.985,(height-1.8)/count-.045, 'StoneDark' if i%3 else 'StoneWorn')
    cone(name,(x,height-.63,z),r*1.13,r*1.32,.45,'StoneWorn')
    cone(name,(x,height-.22,z),r*1.4,r*1.4,.40)
    # Inset-facing flute ridges, few enough to preserve large masses.
    for dx in (-.47,.47):
        box(name,(x+dx,height*.49,z-r*.88),(.115,height*.7,.12),'StoneDark',.025)

for args in [('ENV_Pillar_Left',-13,2,12,1.2),('ENV_Pillar_Right',13,2,12,1.2),('ENV_Pillar_Vista',-11,10,14,1.15),('ENV_Pillar_Divider',-2,11,13,.95),('ENV_Pillar_BackRight',12,11,13,1.0)]:
    pillar(*args)

# A genuine pointed portal cut by constructing the wall around its void.
box('ENV_BackWall_LeftPier',(1.6,6,14),(6.7,12,2),bevel=.14)
box('ENV_BackWall_RightPier',(12.65,6,14),(7.3,12,2),bevel=.14)
polyprism('ENV_BackWall_Crown',[(4.9,12),(9,12),(9,5.5),(7,8.7),(4.9,5.5)],(13,15),bevel=.09)
for side in (-1,1):
    x=7+side*2.15
    for y in [i*.85+.42 for i in range(7)]:
        box('ENV_PortalStonework',(x,y,12.75),(.68,.79,1.3),'StoneWorn',.08)
    a=Vector((7+side*2.15,5.8,12.75)); b=Vector((7,9,12.75))
    for i in range(6):
        center = a.lerp(b,(i+.5)/6)
        ob=box('ENV_PortalStonework',center,(.69,(b-a).length/6-.035,1.3),'StoneWorn',.06)
        ob.rotation_euler.y = math.atan2(b.x-a.x,b.y-a.y)
box('ENV_PortalTunnel_Back',(7,3.1,20),(4.1,6.2,1.5),'StoneBackground',.08)
box('ENV_PortalTunnel_Floor',(7,-.15,17),(4.2,.3,8),'StoneDark')
for side in (-1,1):
    box('ENV_PortalTunnel_Sides',(7+side*2.4,3,17),(.7,6,6),'StoneDark')
for x in (1.2,12.8):
    for y in (2,4,6,8,10):
        box('ENV_WallCourses',(x,y,12.95),(3.2,.08,.11),'StoneBackground',.02)

# The open left side is a real vista of massive towers at distinct depths.
for name,x,z,h,r in [('A',-9,20,16,2.7),('B',-17,27,20,3.5),('C',-3.7,29,18,2.8)]:
    group='ENV_BackgroundTower_'+name
    cone(group,(x,h*.5,z),r*.84,r,h,'StoneBackground',8,.075)
    for y in (2,h*.43,h*.75,h-.8):
        cone(group,(x,y,z),r*1.10,r*1.10,.5,'StoneDark',8)
    cone(group,(x,h+.95,z),r*1.1,.18,2,'StoneBackground',8)
    for y in (4,7,10,13):
        if y>h-2:continue
        for dx in (-.9,.75):
            polyprism(group,[(x+dx-.18,y),(x+dx+.18,y),(x+dx+.18,y+.65),(x+dx,y+.95),(x+dx-.18,y+.65)],(z-r-.02,z-r+.13),'ArcaneWarm',.025)
box('ENV_DistantMass',(0,9,37),(65,25,5),'StoneBackground',.15)

# Tall broken foreground shoulders, with the central gameplay stage unobstructed.
for side in (-1,1):
    group='ENV_Foreground_'+('Left' if side<0 else 'Right')
    for i in range(4):
        cone(group,(side*(14.3+i*.5),.72+i*1.05,-4+i*.35),2.5-i*.29,2.28-i*.30,1.45,'StoneDark',7,.09)
    polyprism(group,[(side*13,4.2),(side*14.4,7.7),(side*16.5,6.5),(side*17.2,3)],(-3.8,-1.4),'StoneDark',.13)

# Individual closed chain links: toroidal geometry, no texture substitutes.
for index,(x1,x2,z) in enumerate([(-11,-2,11),(0,12,12.5)]):
    for i in range(35):
        t=i/34
        x=x1+(x2-x1)*t
        y=10.3-2.6*math.sin(math.pi*t)
        bpy.ops.mesh.primitive_torus_add(major_segments=10,minor_segments=4,location=u((x,y,z)),major_radius=.16,minor_radius=.035)
        ob=bpy.context.object
        ob.rotation_euler.x=math.pi/2
        ob.rotation_euler.y=(i%2)*math.pi/2
        ob.scale.y=1.28
        finish(ob,'ENV_Chains_'+str(index),'MetalDark')

for i,(x,y,z,mat) in enumerate([(-10,5.5,7.8,'ArcaneCool'),(-2,6.3,9.7,'ArcaneWarm'),(11.1,4.2,11.7,'ArcaneWarm')]):
    name='ENV_ArcaneFixture_'+str(i)
    cone(name,(x,y-.55,z),.55,.2,.28,'MetalDark',3)
    cone(name,(x,y+.50,z),.4,.05,.32,'MetalDark',3)
    cone(name,(x,y,z),.14,.24,.74,mat,6,0)
    for dx in (-.3,.3):
        link(name,(x+dx,y-.5,z),(x+dx*.7,y+.4,z),.045,'MetalDark')

# Original silhouette proxies: traveler, shield guardian, hound, floating sentinel.
g='CHR_Player'
cone(g,(-6,1.03,0),.82,.36,1.75,'TravelerCloth',7,.02)
cone(g,(-6,2.1,0),.43,.31,.60,'MetalDark',6)
cone(g,(-6,2.6,0),.38,.05,.50,'StoneWorn',5,.01)
for x in (-6.27,-5.75):
    box(g,(x,.13,-.15),(.28,.26,.5),'MetalDark')
link(g,(-5.65,1.8,0),(-5.1,1.18,-.05),.15,'StoneWorn')
link(g,(-5.1,.3,-.05),(-5.1,2.8,-.05),.047,'MetalDark')
cone(g,(-5.1,2.76,-.05),.12,0,.28,'ArcaneCool',4,0)
g='CHR_Enemy_01'
for x in (2.67,3.3):
    box(g,(x,.43,0),(.38,.86,.48),'MetalDark')
cone(g,(3,1.4,0),.5,.73,1.25,'StoneWorn',6)
cone(g,(3,2.32,0),.35,.22,.58,'StoneWorn',5)
polyprism(g,[(2.12,.75),(2.0,1.95),(2.55,2.15),(2.9,1.95),(2.75,.75),(2.42,.47)],(-.48,-.15),'StoneDark')
link(g,(3.68,.5,0),(3.68,2.8,0),.065,'MetalDark')
cone(g,(3.68,2.86,0),.22,0,.6,'StoneWorn',3)
g='CHR_Enemy_02'
ob=cone(g,(6,1.1,.6),.49,.40,1.65,'StoneWorn',6)
ob.rotation_euler.y=math.pi/2
for x in (5.5,6.6):
    for z in (.25,.95):
        link(g,(x,1,z),(x-.16,.16,z),.15,'MetalDark')
cone(g,(5.13,1.45,.6),.40,.22,.62,'StoneDark',5)
for z in (.32,.86):
    cone(g,(5.13,1.98,z),.14,0,.7,'StoneWorn',3)
link(g,(6.65,1.23,.6),(7.32,1.9,.6),.10,'StoneDark')
g='CHR_Enemy_03'
cone(g,(9,1.3,.2),.06,.7,1.8,'StoneDark',3)
cone(g,(9,2.55,.2),.7,0,.75,'StoneWorn',3)
cone(g,(9,2.02,-.26),.18,.18,.35,'ArcaneWarm',4)
for side in (-1,1):
    polyprism(g,[(9+side*.6,2.4),(9+side*1.0,2.0),(9+side*.82,1.1)],(.05,.4),'StoneWorn')

# Consolidate modular chunks, recalculate outward normals, create practical base pivots.
manifest={'coordinate_contract':'Unity x right, y up, z deeper. Blender x, z, y; Unity importer bakeAxisConversion enabled.', 'materials':list(MATS), 'groups':{}, 'export':{'axis_forward':'-Z','axis_up':'Y','scale':1,'bake_space_transform':True}}
for name,obs in GROUPS.items():
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs: ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.object.join()
    ob=bpy.context.object
    ob.name=name
    bm=bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data); bm.free()
    points=[ob.matrix_world @ v.co for v in ob.data.vertices]
    if name == 'CHR_Enemy_02':
        ground_offset = min(p.z for p in points)
        ob.location.z -= ground_offset
        bpy.context.view_layer.update()
        points=[ob.matrix_world @ v.co for v in ob.data.vertices]
    lo=[min(p[j] for p in points) for j in range(3)]
    hi=[max(p[j] for p in points) for j in range(3)]
    bpy.context.scene.cursor.location=((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2])
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    # Planar box-like UVs are adequate for texture-free prototype material families.
    uv=ob.data.uv_layers.new(name='PrototypeUV')
    for poly in ob.data.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a]))
        axes=[a for a in range(3) if a!=axis]
        for li in poly.loop_indices:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv=(co[axes[0]]*.25,co[axes[1]]*.25)
    ob.data.calc_loop_triangles()
    manifest['groups'][name]={'vertices':len(ob.data.vertices),'triangles':len(ob.data.loop_triangles),'unity_min':[lo[0],lo[2],lo[1]],'unity_max':[hi[0],hi[2],hi[1]]}
manifest['triangles']=sum(g['triangles'] for g in manifest['groups'].values())
manifest['mesh_objects']=len(GROUPS)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1
bpy.context.scene.cursor.location=(0,0,0)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'TrigonalAbyssStage.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT),object_types={'MESH'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',use_space_transform=True,bake_space_transform=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True,path_mode='AUTO')
(SOURCE/'stage_manifest.json').write_text(json.dumps(manifest,indent=2))
print('STAGE_EXPORT',json.dumps({'triangles':manifest['triangles'],'meshes':len(GROUPS),'fbx':str(OUT)}))



