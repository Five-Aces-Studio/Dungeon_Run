"""Reproducible three-character snake production candidates for Blender 5.2.

Run through the connected Blender session; preserve all pre-existing scenes.
Shapes are authored section surfaces, not voxel blocks. Palette is a technical
UV color lookup, not a painted texture. Final acrylic treatment belongs to Unity.
"""
import bpy, math, json, os
from pathlib import Path
from mathutils import Vector
from math import sin, cos, pi

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docsBlender/EnemyRosterV1'
COLORS = [
    (0.055,.25,.115), (.085,.34,.16), (.12,.42,.20), (.20,.48,.24),
    (.34,.48,.23), (.48,.56,.30), (.56,.62,.35), (.68,.68,.40),
    (.09,.055,.035), (.22,.115,.065), (.34,.20,.11), (.44,.29,.16),
    (.18,.22,.22), (.32,.38,.37), (.48,.55,.51), (.66,.70,.62),
    (.35,.20,.045), (.61,.40,.08), (.80,.59,.19), (.94,.75,.35),
    (.15,.015,.015), (.38,.04,.025), (.56,.09,.045), (.74,.19,.08),
    (.015,.024,.015), (.72,.77,.12), (.96,.92,.34), (1.0,.97,.61),
    (.20,.045,.16), (.43,.15,.29), (.62,.42,.25), (.83,.68,.44),
]

def create_review_scene(name):
    scene_name = 'EnemyRosterV1_' + name
    if scene_name in bpy.data.scenes:
        raise RuntimeError('Review scene already exists; inspect it before rebuilding: ' + scene_name)
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    scene = bpy.data.scenes.new(scene_name)
    scene['EnemyRosterV1Owned'] = True
    scene.unit_settings.system = 'METRIC'
    scene.world = bpy.data.worlds.new(scene_name + '_World')
    bpy.context.window.scene = scene
    return scene

def palette(name, path):
    image = bpy.data.images.new(name + '_Palette', width=256, height=128)
    pixels=[]
    for y in range(128):
        for x in range(256): pixels.extend((*COLORS[(y//32)*8+x//32],1))
    image.pixels=pixels; image.filepath_raw=str(path); image.file_format='PNG'; image.save()
    mats=[]
    for label,metal in [('Organic',0),('Equipment',.5)]:
        m=bpy.data.materials.new('M_Enemy'+label);m.use_nodes=True
        p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.64;p.inputs['Metallic'].default_value=metal
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest'
        m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color']);mats.append(m)
    return mats

class Surface:
    def __init__(self,name,material=0):
        self.name=name;self.v=[];self.f=[];self.uv=[];self.weights=[];self.colors=[];self.material=material
    def vertex(self,p,uv=(.5,.5),weights=None):
        self.v.append(tuple(p));self.uv.append(uv);self.weights.append(weights or {'Chest':1});return len(self.v)-1
    def face(self,verts,color):self.f.append(verts);self.colors.append(color)
    def make(self,mats,rig):
        mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.v,[],self.f);mesh.update()
        ob=bpy.data.objects.new(rig.name.removeprefix('Rig_')+'_'+self.name+'_LOD0',mesh);bpy.context.collection.objects.link(ob);ob.data.materials.append(mats[self.material]);ob.parent=rig
        uv=mesh.uv_layers.new(name='UV0')
        for poly,col in zip(mesh.polygons,self.colors):
            for li in poly.loop_indices:
                u,v=self.uv[mesh.loops[li].vertex_index];uv.data[li].uv=((col%8+.12+.76*u)/8,(col//8+.12+.76*v)/4)
            poly.use_smooth=True
        names=set(k for d in self.weights for k in d)
        for n in names:
            g=ob.vertex_groups.new(name=n)
            for i,w in enumerate(self.weights):
                if n in w:g.add([i],w[n],'REPLACE')
        mod=ob.modifiers.new('Generic skin deformation','ARMATURE');mod.object=rig
        return ob

def catmull(values,steps):
    result=[]
    for i in range(len(values)-1):
        a=values[max(0,i-1)];b=values[i];c=values[i+1];d=values[min(len(values)-1,i+2)]
        for j in range(steps):
            t=j/steps
            row=tuple(.5*((2*b[k])+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-d[k])*t*t+(-a[k]+3*b[k]-3*c[k]+d[k])*t*t*t) for k in range(len(b)))
            result.append(Vector(row) if len(row)<=4 else row)
    result.append(Vector(values[-1]) if len(values[-1])<=4 else tuple(values[-1]));return result

def tube(s, sections, color=2, sides=24, steps=3, bone='Chest', weight_fn=None, front=None):
    # Sections carry x,y,z and two radii. Parallel frame uses world X unless vertical-crossing.
    rows=catmull(sections,steps);start=len(s.v);previous_axis=None
    for i,row in enumerate(rows):
        p=Vector(row[:3]);t=Vector(rows[min(i+1,len(rows)-1)][:3])-Vector(rows[max(i-1,0)][:3]);t.normalize()
        axis=previous_axis if previous_axis is not None else Vector((1,0,0));a=axis-t*axis.dot(t)
        if a.length<.15:a=Vector((0,1,0))-t*Vector((0,1,0)).dot(t)
        a.normalize();b=t.cross(a).normalized();previous_axis=a.copy()
        for j in range(sides):
            angle=2*pi*j/sides
            q=p+a*cos(angle)*row[3]+b*sin(angle)*row[4]
            s.vertex(q,(j/sides,i/(len(rows)-1)),weight_fn(i/(len(rows)-1),q) if weight_fn else {bone:1})
    for i in range(len(rows)-1):
        for j in range(sides):
            col=color(i,j/sides) if callable(color) else color
            s.face((start+i*sides+j,start+i*sides+(j+1)%sides,start+(i+1)*sides+(j+1)%sides,start+(i+1)*sides+j),col)
    s.face(tuple(start+j for j in reversed(range(sides))),color(0,0) if callable(color) else color)
    s.face(tuple(start+(len(rows)-1)*sides+j for j in range(sides)),color(0,0) if callable(color) else color)

def patch(s,coords,color,bone='Chest',depth=.025):
    # Beveled plate with a raised central face, useful for armor, brows and blades.
    center=sum((Vector(x) for x in coords),Vector())/len(coords)
    outer=[s.vertex(p,((p[0]-center.x)*.5+.5,(p[2]-center.z)*.5+.5),{bone:1}) for p in coords]
    inner=[s.vertex(center+(Vector(p)-center)*.84+Vector((0,-depth,0)),(.5,.5),{bone:1}) for p in coords]
    for i in range(len(coords)):s.face((outer[i],outer[(i+1)%len(coords)],inner[(i+1)%len(coords)],inner[i]),max(0,color-1))
    s.face(tuple(inner),color)
    # Back face closes equipment and keeps silhouettes correct from behind.
    s.face(tuple(reversed(outer)),max(0,color-1))

def eye(s,x,y,z,w,h,bone='Head',pupils=1,tilt=0):
    # Almond lens, rim, slit/pupil details: actual shaped geometry, not floating spheres.
    outline=[]
    for i in range(24):
        a=2*pi*i/24;xx=w*cos(a);zz=h*sin(a)*(abs(sin(a))**.25)
        outline.append((x+xx,y+.025*abs(cos(a)),z+zz+xx*tilt))
    patch(s,[(x+(p[0]-x)*1.14,p[1]+.004,z+(p[2]-z)*1.18) for p in outline],0,bone,.015)
    patch(s,outline,26,bone,.025)
    if pupils==1:
        patch(s,[(x-.016,y-.029,z),(x,y-.03,z+h*.83),(x+.016,y-.029,z),(x,y-.031,z-h*.83)],24,bone,.003)
    else:
        for dx,dz in [(-w*.25,h*.22),(w*.25,h*.22),(0,-h*.38)]:
            patch(s,[(x+dx+.018*cos(a*2*pi/10),y-.03,z+dz+.022*sin(a*2*pi/10)) for a in range(10)],24,bone,.003)

def ribbon(s,points,width,color,bone):
    # Flat leather straps/ribbons with actual thickness and shaped contour.
    pts=catmull(points,3)
    for i in range(len(pts)-1):
        p,q=pts[i],pts[i+1];d=q-p;side=Vector((d.z,0,-d.x)).normalized()*width*.5
        patch(s,[p-side,q-side,q+side,p+side],color,bone,.012)

def build_rig(name,tail,arms,heads):
    data=bpy.data.armatures.new('Skeleton_'+name);rig=bpy.data.objects.new('Rig_'+name,data);bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    def bone(n,a,b,parent=None,deform=True):
        e=data.edit_bones.new(n);e.head=a;e.tail=b;e.use_deform=deform
        if parent:e.parent=data.edit_bones[parent]
    bone('Root',(0,0,0),(0,0,.3));bone('Spine',(0,0,.65),(0,0,1.4),'Root');bone('Chest',(0,0,1.4),(0,0,2.12),'Spine')
    for suffix,(x,z,scale) in heads.items():
        neck='Neck'+suffix;head='Head'+suffix;jaw='Jaw'+suffix
        bone(neck,(x*.6,0,2),(x,0,z-.12),'Chest');bone(head,(x,0,z-.12),(x,0,z+.45),neck)
        bone(jaw,(x,0,z),(x,-.28,z-.04),head)
    for side,points in arms.items():
        shoulder,elbow,wrist=points
        bone('UpperArm_'+side,shoulder,elbow,'Chest');bone('Forearm_'+side,elbow,wrist,'UpperArm_'+side)
        hand=Vector(wrist)+Vector((0,-.08,-.18));bone('Hand_'+side,wrist,hand,'Forearm_'+side)
        bone('WeaponSocket_'+side,hand,hand+Vector((0,0,.14)),'Hand_'+side,True)
    for i in range(8):
        a=tail[min(i,len(tail)-2)][:3];b=tail[min(i+1,len(tail)-1)][:3]
        bone('Tail_%02d'%(i+1),a,b,'Root' if i==0 else 'Tail_%02d'%i)
    for n,p,parent in [('VFXSocket_Chest',(0,-.3,1.9),'Chest'),('VFXSocket_Head',(0,0,2.85),'Head'),('TargetAnchor',(0,0,3.15),'Root'),('VFX_Ground',(0,0,.04),'Root')]:bone(n,p,Vector(p)+Vector((0,0,.08)),parent,False)
    bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False);return rig

def weight_spine(t,p):
    w=max(0,min(1,(p.z-1.1)/.7));return {'Spine':1-w,'Chest':w}

def head_shape(s,details,x,z,scale,kind,suffix=''):
    b='Head'+suffix
    # Profile width and anterior depth create a tapered snake muzzle and brow plane.
    if kind=='Sentinel':rows=[(-.25,.17,.19),(-.10,.25,.25),(.10,.40,.22),(.28,.53,.20),(.44,.44,.19),(.59,.22,.17),(.63,.05,.06)]
    else:rows=[(-.29,.10,.19),(-.19,.14,.25),(.02,.23,.27),(.30,.29,.235),(.49,.23,.20),(.58,.09,.09)]
    tube(s,[(x, -.06 if h<0 else 0,z+h*scale,w*scale,d*scale) for h,w,d in rows],2,32,4,bone=b)
    # Angular raised brow/crown plates and cheek line define planes under painterly light.
    for sign in [-1,1]:
        patch(details,[(x+sign*.04*scale,-.236*scale,z+.37*scale),(x+sign*.255*scale,-.16*scale,z+.43*scale),(x+sign*.22*scale,-.205*scale,z+.36*scale)],3,b,.008)
        ribbon(details,[(x+sign*.22*scale,-.19*scale,z+.07*scale),(x+sign*.17*scale,-.272*scale,z-.04*scale),(x+sign*.09*scale,-.278*scale,z-.14*scale)],.012,0,b)
        patch(details,[(x+sign*.09*scale,-.291*scale,z-.12*scale),(x+sign*.065*scale,-.289*scale,z-.07*scale),(x+sign*.045*scale,-.295*scale,z-.13*scale)],24,b,.005)
    if kind=='Soldier':
        eye(details,x-.135,-.258,z+.29,.11,.041,b,tilt=-.33)
        eye(details,x-.117,-.289,z+.13,.102,.040,b,tilt=-.33)
        eye(details,x+.127,-.278,z+.21,.112,.046,b,tilt=.33)
    elif kind=='Sentinel':
        eye(details,x-.32,-.177,z+.34,.115,.045,b,tilt=-.35);eye(details,x+.32,-.177,z+.34,.115,.045,b,tilt=.35)
        eye(details,x,-.24,z+.16,.047,.137,b)
    else:eye(details,x,-.27*scale,z+.18*scale,.21*scale,.075*scale,b,3)
    tube(details,[(x,-.255*scale,z-.17*scale,.022,.016),(x,-.32*scale,z-.30*scale,.017,.012),(x-.018,-.35*scale,z-.35*scale,.003,.003)],22,8,2,'Jaw'+suffix)
    tube(details,[(x,-.32*scale,z-.30*scale,.015,.009),(x+.027,-.36*scale,z-.345*scale,.002,.002)],22,8,2,'Jaw'+suffix)

def make_character(name,kind,category,export=False,render=False):
    if export:
        raise RuntimeError('Production FBX export is gated until geometry and animation validation passes.')
    scene=create_review_scene(name)
    model_dir=ROOT/'Assets/Art/Characters/Enemies'/category/name/'Models';texture_dir=model_dir.parent/'Textures'
    model_dir.mkdir(parents=True,exist_ok=True);texture_dir.mkdir(parents=True,exist_ok=True)
    mats=palette(name,texture_dir/(name+'_Palette.png'))
    tail=[(0,0,.83,.29,.27),(-.17,.18,.51,.32,.27),(-.53,.48,.30,.33,.26),(-.36,.98,.26,.31,.24),(.32,1.08,.25,.28,.23),(.91,.70,.23,.24,.20),(1.00,.07,.19,.18,.16),(.55,-.42,.14,.105,.11),(-.13,-.46,.10,.016,.025)]
    if kind=='Chieftain':tail=[(x*1.15,y*1.1,z,rx*1.12,ry*1.12) for x,y,z,rx,ry in tail]
    arms={s: [(sign*.48,0,2.05),(sign*(.82 if kind=='Chieftain' else .73),-.03,1.71),(sign*(1.12 if kind=='Chieftain' else .81),-.25,1.62 if kind=='Chieftain' else 1.39)] for s,sign in [('L',1),('R',-1)]}
    heads={'':(0,2.50 if kind=='Sentinel' else 2.46,1)}
    if kind=='Chieftain':heads={'':(0,2.67,1.12),'_L':(.59,2.52,.81),'_R':(-.59,2.52,.81)}
    rig=build_rig(name,tail,arms,heads)
    body=Surface('Body');head=Surface('Head');limbs=Surface('Arms');armor=Surface('Armor',1);details=Surface('Details');weapons=[]
    tube(body,[(0,0,.55,.23,.23),(0,0,.83,.33,.29),(0,0,1.08,.30,.24),(0,0,1.42,.35,.26),(0,0,1.76,.48,.29),(0,0,1.97,.54,.27),(0,0,2.11,.41,.23),(0,0,2.18,.20,.19)],lambda i,u: 3 if .58<u<.9 else 2,32,4,weight_fn=weight_spine)
    def tw(t,p):
        f=t*7;idx=min(6,int(f));blend=f-idx;return {'Tail_%02d'%(idx+1):1-blend,'Tail_%02d'%(idx+2):blend}
    tube(body,tail,lambda i,u:5 if .60<u<.9 else (1 if .1<u<.4 else 2),32,5,weight_fn=tw)
    # Ventral segmentation on the upright lower body, with a clear abdominal rhythm.
    for i in range(9):
        z=.83+i*.105;w=.235+(z-.83)*.09;y=-.25-(z-1)*.045
        patch(details,[(-w,y,z),(-w*.9,y-.02,z+.065),(0,y-.043,z+.082),(w*.9,y-.02,z+.065),(w,y,z),(0,y-.032,z-.012)],5 if i%3 else 6,'Spine' if z<1.25 else 'Chest',.009)
    for suf,(x,z,size) in heads.items():
        tube(body,[(x*.60,.025,2.0,.18*size,.18*size),(x*.87,.02,2.25,.185*size,.17*size),(x,0,z,.19*size,.19*size)],2,24,5,'Neck'+suf)
        head_shape(head,details,x,z,size,kind,suf)
    for side,points in arms.items():
        sign=1 if side=='L' else -1;sh,el,wr=[Vector(v) for v in points]
        sections=[]
        for p,r in [(sh,.23),(sh.lerp(el,.35),.24),(sh.lerp(el,.75),.19),(el,.17),(el.lerp(wr,.32),.18),(el.lerp(wr,.78),.135),(wr,.105)]:sections.append((*p,r,r*.88))
        def aw(t,p,side=side):
            if t<.45:return {'UpperArm_'+side:1}
            if t>.67:return {'Forearm_'+side:1}
            f=(t-.45)/.22;return {'UpperArm_'+side:1-f,'Forearm_'+side:f}
        tube(limbs,sections,2,24,3,weight_fn=aw)
        palm=wr+Vector((0,-.055,-.13));tube(limbs,[(*wr,.105,.095),(*palm,.13,.085),(*(palm+Vector((0,-.015,-.1))),.10,.066)],3,16,3,'Hand_'+side)
        for j in range(4):
            finger=palm+Vector(((j-1.5)*.053,-.055,-.035))
            tube(limbs,[(*finger,.032,.029),(*(finger+Vector((0,-.055,-.07))),.030,.027),(*(finger+Vector((0,-.033,-.12))),.019,.02)],2,10,3,'Hand_'+side)
        thumb=palm+Vector((-sign*.09,0,.035));tube(limbs,[(*thumb,.046,.038),(*(thumb+Vector((-sign*.06,-.07,-.055))),.035,.03),(*(thumb+Vector((-sign*.025,-.10,-.095))),.025,.02)],3,12,3,'Hand_'+side)
        # Rigid forearm guard follows only the forearm bone, leaving elbow clearance.
        pa=el.lerp(wr,.40);pb=el.lerp(wr,.86);rad=.178
        tube(armor,[(*pa,rad,rad*.95),(*pa.lerp(pb,.12),rad+.012,rad*.98),(*pa.lerp(pb,.85),rad*.8,rad*.81),(*pb,rad*.78,rad*.78)],18 if kind=='Chieftain' else (14 if kind=='Sentinel' else 10),20,2,'Forearm_'+side)
    if kind=='Soldier':
        patch(armor,[(-.37,-.25,2.16),(-.64,-.20,2.18),(-.85,-.21,2.0),(-.83,-.28,1.86),(-.49,-.34,1.92)],10,'UpperArm_R',.06)
        ribbon(armor,[(-.35,-.25,2.14),(-.17,-.31,1.92),(.10,-.30,1.64),(.30,-.25,1.36)],.105,10,'Chest')
        ribbon(armor,[(.30,.25,1.36),(.05,.29,1.68),(-.35,.23,2.14)],.105,9,'Chest')
    elif kind=='Sentinel':
        patch(armor,[(-.38,-.26,2.05),(.38,-.26,2.05),(.42,-.29,1.68),(.29,-.28,1.40),(-.29,-.28,1.40),(-.42,-.29,1.68)],14,'Chest',.11)
        ribbon(armor,[(-.37,-.28,2.06),(0,-.38,2.095),(.37,-.28,2.06)],.03,15,'Chest')
        eye(armor,0,-.407,1.92,.086,.028,'Chest')
        patch(armor,[(-.035,-.425,1.88),(.035,-.425,1.88),(0,-.425,1.78)],28,'Chest',.008)
    else:
        tube(armor,[(0,0,1.14,.31,.263),(0,0,1.23,.335,.272),(0,0,1.34,.337,.268)],21,32,3,'Spine')
        ribbon(armor,[(.23,-.27,1.26),(.15,-.31,1.02),(.27,-.31,.68),(.07,-.35,.50)],.21,22,'Spine')
        ribbon(armor,[(-.25,-.18,2.08),(0,-.32,1.86),(.25,-.18,2.08)],.026,17,'Chest')
        patch(armor,[(-.15,-.34,1.92),(.15,-.34,1.92),(0,-.37,1.67)],18,'Chest',.025)
        patch(armor,[(-.07,-.372,1.87),(.07,-.372,1.87),(0,-.39,1.75)],24,'Chest',.005)
    # Longitudinal dorsal scale chevrons are modeled accents, not a noise texture.
    for i in range(11):
        z=.98+i*.091;y=.253 if z<1.7 else .277
        patch(details,[(-.10,y,z),(-.07,y,z+.063),(0,y+.028,z+.09),(.07,y,z+.063),(.10,y,z),(0,y+.026,z-.01)],1,'Spine' if z<1.3 else 'Chest',-.013)
    # Separate rigid weapons carry a socket weight so Unity can preserve/reparent them.
    for side in (['L','R'] if kind=='Chieftain' else ['R']):
        hand=Vector(arms[side][-1])+Vector((0,-.08,-.18));ws=Surface('Weapon_'+side,1);b='WeaponSocket_'+side;x,y,z=hand
        if kind=='Chieftain':
            tube(ws,[(x,y,z-.15,.038,.038),(x,y,z+.20,.038,.038)],9,16,3,b)
            patch(ws,[(x-.18,y,z+.14),(x+.18,y,z+.14),(x+.18,y,z+.20),(x-.18,y,z+.20)],18,b,.015)
            sign=1 if side=='L' else -1
            patch(ws,[(x-.07,y,z+.20),(x+.065,y,z+.20),(x+sign*.19,y,z+.71),(x+sign*.31,y,z+1.13),(x+sign*.22,y,z+1.26),(x+sign*.10,y,z+.79)],15,b,.029)
            tube(ws,[(x,y,z-.18,.055,.055),(x,y,z-.24,.01,.01)],18,12,2,b)
        else:
            tube(ws,[(x,y,.35,.034,.034),(x,y,2.78,.034,.034)],10,16,5,b)
            if kind=='Soldier':
                for i in range(15):
                    zz=1.09+i*.048;tube(ws,[(x,y,zz,.043,.043),(x,y,zz+.026,.044,.044)],31,12,1,b)
                patch(ws,[(x-.035,y,2.55),(x-.09,y,2.91),(x-.40,y,2.72),(x-.28,y,2.47),(x+.18,y,2.42),(x+.20,y,2.50)],14,b,.035)
            else:
                patch(ws,[(x-.045,y,2.65),(x+.045,y,2.65),(x+.04,y,2.98),(x-.14,y,3.24),(x-.20,y,3.65),(x-.34,y,3.40),(x-.31,y,3.06)],15,b,.035)
        weapons.append(ws)
    objects=[s.make(mats,rig) for s in [body,head,limbs,armor,details]+weapons]
    # Decimate only the independent LOD copies, preserving UVs and normalized groups.
    for source in list(objects):
        for lod,ratio in [(1,.5),(2,.25)]:
            ob=source.copy();ob.data=source.data.copy();ob.name=source.name.replace('_LOD0','_LOD%d'%lod);bpy.context.collection.objects.link(ob)
            bpy.context.view_layer.objects.active=ob;mod=ob.modifiers.new('LOD reduction','DECIMATE');mod.ratio=ratio
            ob.modifiers.move(len(ob.modifiers)-1,0)
            bpy.ops.object.modifier_apply(modifier=mod.name);ob.hide_render=True;ob.hide_set(True);objects.append(ob)
    animate(rig,kind)
    # Keep a readable source collection and select only the production rig and meshes for FBX.
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    for ob in objects:ob.hide_set(False);ob.select_set(True)
    bpy.context.view_layer.objects.active=rig
    fbx=model_dir/('CHR_'+name+'.fbx')
    for ob in objects:ob.hide_set(not ob.name.endswith('_LOD0'))
    rig.animation_data.action=bpy.data.actions.get(rig.name+'|Idle');bpy.context.scene.frame_set(1)
    setup_render(name)
    aim_camera((4.8,-6,3.8),(0,0,1.7))
    actions={a for a in bpy.data.actions if a.name.startswith(rig.name+'|')}
    bpy.data.libraries.write(str(OUT/(name+'.blend')), {scene}|actions, fake_user=True, path_remap='RELATIVE_ALL')
    stats={'name':name,'status':'BLOCKOUT_NOT_PRODUCTION_READY','bones':len(rig.data.bones),'materials':2,'palette':[256,128],'lods':{},'meshes':[],'clips':['Idle','Attack','HitReaction','Death']}
    for lod in range(3):
        obs=[x for x in objects if x.name.endswith('_LOD%d'%lod)];tri=0
        for ob in obs:ob.data.calc_loop_triangles();tri+=len(ob.data.loop_triangles)
        stats['lods'][str(lod)]={'triangles':tri,'renderers':len(obs)}
    for ob in objects:
        if not ob.name.endswith('_LOD0'):continue
        unweighted=sum(1 for v in ob.data.vertices if not v.groups);error=max([abs(sum(g.weight for g in v.groups)-1) for v in ob.data.vertices]or[0])
        stats['meshes'].append({'name':ob.name,'vertices':len(ob.data.vertices),'unweighted':unweighted,'weightErrorMax':error,'uv':len(ob.data.uv_layers)})
    (OUT/(name+'-stats.json')).write_text(json.dumps(stats,indent=2))
    for view,pos in ([('front',(0,-7,2.9)),('three-quarter',(4.8,-6.0,3.8)),('side',(7,-.2,3.1)),('back',(0,7,3.2))] if render else []):
        aim_camera(pos,(0,0,1.7));bpy.context.scene.render.filepath=str(OUT/(name+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
    return stats

def animate(rig,kind):
    rig.animation_data_create()
    for clip,length in [('Idle',60),('Attack',36),('HitReaction',24),('Death',48)]:
        action=bpy.data.actions.new(rig.name+'|'+clip);action.use_fake_user=True;rig.animation_data.action=action
        for frame in [1,length//3,2*length//3,length]:
            t=(frame-1)/(length-1);pulse=sin(t*pi);wave=sin(t*2*pi)
            for b in rig.pose.bones:
                b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0)
                if clip=='Idle':
                    if b.name=='Chest':b.rotation_euler.x=.025*wave
                    if b.name.startswith('Head'):b.rotation_euler.y=.04*wave
                    if b.name.startswith('Tail_'):b.rotation_euler.y=.018*wave
                elif clip=='Attack':
                    if b.name=='Chest':b.rotation_euler.z=.14*pulse;b.rotation_euler.x=.11*pulse
                    if b.name.startswith('UpperArm'):b.rotation_euler.x=-.60*pulse;b.rotation_euler.z=(.35 if b.name.endswith('R') else -.25)*pulse
                    if b.name.startswith('Forearm'):b.rotation_euler.x=.40*pulse
                    if b.name.startswith('Head'):b.rotation_euler.x=.16*pulse
                elif clip=='HitReaction':
                    if b.name=='Chest':b.rotation_euler.x=-.22*pulse
                    if b.name.startswith('Head'):b.rotation_euler.x=-.15*pulse
                else:
                    if b.name=='Spine':b.rotation_euler.x=.72*t
                    if b.name=='Chest':b.rotation_euler.x=.40*t
                    if b.name.startswith('Head'):b.rotation_euler.x=.35*t
                    if b.name.startswith('UpperArm'):b.rotation_euler.y=(.3 if b.name.endswith('L') else -.3)*t
                b.keyframe_insert('rotation_euler',frame=frame,group=b.name)
                b.keyframe_insert('location',frame=frame,group=b.name)
        action['Purpose']='Pose-validation candidate, not final authored combat animation'
    rig.animation_data.action=bpy.data.actions.get(rig.name+'|Idle');bpy.context.scene.frame_set(1)

def aim_camera(pos,target):
    cam=bpy.context.scene.camera;cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()

def setup_render(name):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.world.color=(.12,.12,.12)
    bpy.ops.object.camera_add();scene.camera=bpy.context.object;scene.camera.name='ReviewCamera';scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=4.6
    for n,pos,power,size,col in [('Key',(-3,-4,6),650,5,(1,.88,.72)),('Fill',(3,-2,3),400,4,(.65,.83,1)),('Rim',(1,4,5),850,3,(.70,1,.91))]:
        bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.name=n;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.data.color=col;l.rotation_euler=(Vector((0,0,1.5))-l.location).to_track_quat('-Z','Y').to_euler()
    scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.render.film_transparent=False

def show_review():
    """Display linked LOD0 candidates without moving the per-character sources."""
    scene=create_review_scene('Review')
    specs=[('SnakeSoldier',-3.2,'SOLDIER / BASIC'),('SnakeSentinel',0,'SENTINEL / ELITE'),('SnakeChieftain',3.2,'CHIEFTAIN / BOSS')]
    for name,x,label in specs:
        source=bpy.data.scenes['EnemyRosterV1_'+name]
        collection=bpy.data.collections.new(name+'_ReviewGeometry')
        for ob in source.objects:
            if ob.type=='ARMATURE' or (ob.type=='MESH' and ob.name.endswith('_LOD0')):
                collection.objects.link(ob)
        instance=bpy.data.objects.new(name+'_ReviewInstance',None)
        instance.instance_type='COLLECTION';instance.instance_collection=collection
        instance.location.x=x;scene.collection.objects.link(instance)
        curve=bpy.data.curves.new(name+'_Label','FONT');curve.body=label;curve.align_x='CENTER';curve.size=.16
        text=bpy.data.objects.new(name+'_Label',curve);scene.collection.objects.link(text)
        text.location=(x,-.8,-.15);text.rotation_euler=(pi/2,0,0)
    setup_render('Review')
    scene.render.resolution_x=1600;scene.render.resolution_y=850
    scene.camera.data.ortho_scale=11.0
    aim_camera((0,-16,6),(0,0,1.6))
    scene.frame_end=60
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.region_3d.view_camera_zoom=10
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active=None
    sources={bpy.data.scenes['EnemyRosterV1_'+name] for name,_,_ in specs}
    actions={a for a in bpy.data.actions if a.name.startswith('Rig_Snake')}
    bpy.data.libraries.write(str(OUT/'EnemyRosterV1_Review.blend'),{scene}|sources|actions,fake_user=True,path_remap='RELATIVE_ALL')
    return {'scene':scene.name,'source_file':str(OUT/'EnemyRosterV1_Review.blend')}

if __name__=='__main__':
    all_stats=[]
    for spec in [('SnakeSoldier','Soldier','Basic'),('SnakeSentinel','Sentinel','Elite'),('SnakeChieftain','Chieftain','Boss')]:all_stats.append(make_character(*spec))
    (OUT/'slice-stats.json').write_text(json.dumps(all_stats,indent=2))
    show_review()
    print('ENEMY_BLOCKOUT_REVIEW_READY_NOT_PRODUCTION')
