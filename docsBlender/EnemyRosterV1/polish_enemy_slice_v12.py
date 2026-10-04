"""V1.2 candidates: continuous shoulder weights and rounded socket support.

Run only through the connected Blender MCP session. V1.1 remains immutable.
The existing rig names, hierarchy, weapon grips and material families are retained.
"""
from pathlib import Path

_legacy_path = Path(__file__).with_name('polish_enemy_slice_v11.py')
_legacy_source = _legacy_path.read_text(encoding='utf-8')
# Isolate the revision namespace while reusing the established tail, hands and jaw.
_legacy_source = _legacy_source.replace('V11', 'V12').replace('V1.1', 'V1.2')
_attack_line = "if b.name.startswith('UpperArm'): b.rotation_euler.x = -strike*(1.05 if kind=='Chieftain' else .85)"
assert _legacy_source.count(_attack_line) == 1
_legacy_source = _legacy_source.replace(_attack_line,
    "if b.name.startswith('Clavicle_'): b.rotation_euler.x = -strike*.18\n"
    "                    if b.name.startswith('UpperArm'): b.rotation_euler.x = -strike*(.87 if kind=='Chieftain' else .67)")
exec(compile(_legacy_source, str(_legacy_path), 'exec'), globals())

_original_create_rig = create_rig
_original_axial_weights = axial_weights


def create_rig(name, tail, arms, heads, grips):
    # Center the humerus inside the rounded socket, rather than at its outer rim.
    for side, sign in [('L', 1), ('R', -1)]:
        arms[side][0] = (sign*.425, 0, 1.975)
    return _original_create_rig(name, tail, arms, heads, grips)


def shoulder_weights(p, arms):
    """One spatial field across torso, socket and arm; no per-ring weight jumps."""
    p = Vector(p)
    side = 'L' if p.x >= 0 else 'R'
    shoulder, elbow, wrist = [Vector(v) for v in arms[side]]
    direction = (elbow-shoulder).normalized()
    upper_length = (elbow-shoulder).length
    along = (p-shoulder).dot(direction)
    # Confine humeral motion laterally; axial chest/neck remain independent.
    lateral = smooth((abs(p.x)-.19)/.40)
    shoulder_band = smooth((p.z-1.54)/.24) * (1-smooth((p.z-2.22)/.18))
    influence = lateral * shoulder_band
    upper = smooth((along+.07)/.27) * influence
    clavicle = (1-upper) * influence * .78
    result = _original_axial_weights(p.z)
    # Neck influence must not pull the lateral shoulder/trapezius along with head turns.
    neck = result.pop('Neck', 0)
    neck_keep = neck * (1-smooth((abs(p.x)-.12)/.17))
    result['Neck'] = neck_keep
    result['Chest'] = result.get('Chest', 0) + neck-neck_keep
    axial = max(0, 1-upper-clavicle)
    pairs = [(key, value*axial) for key,value in result.items()]
    pairs += [('UpperArm_'+side, upper), ('Clavicle_'+side, clavicle)]
    return weights(*pairs)


def arm_weights(p, side, arms):
    shoulder, elbow, wrist = [Vector(v) for v in arms[side]]
    p = Vector(p)
    direction = (elbow-shoulder).normalized()
    along = (p-shoulder).dot(direction)
    upper_length = (elbow-shoulder).length
    # Preserve the same field at the seam, then converge continuously to the humerus.
    proximal = shoulder_weights(p, arms)
    full_upper = smooth((along-.07)/.23)
    result = {k:v*(1-full_upper) for k,v in proximal.items()}
    result['UpperArm_'+side] = result.get('UpperArm_'+side,0)+full_upper
    forearm = smooth((along-(upper_length-.13))/.26)
    result = {k:v*(1-forearm) for k,v in result.items()}
    result['Forearm_'+side] = forearm
    fore_dir = (wrist-elbow).normalized()
    hand = smooth(((p-elbow).dot(fore_dir)-(wrist-elbow).length+.10)/.16)*.6
    result = {k:v*(1-hand) for k,v in result.items()}
    result['Hand_'+side] = hand
    return weights(*result.items())


def body_and_arms(surface, tail, arms, heads, kind):
    n = 32
    tail_rows = list(reversed(base.catmull(tail, 5)))
    spine_profile = [
        (0,0,.98,.31,.265), (0,0,1.18,.305,.25), (0,0,1.40,.34,.26),
        (0,0,1.62,.405,.275), (0,0,1.78,.46,.275), (0,0,1.84,.475,.268),
        (0,0,1.93,.495,.26), (0,0,2.02,.50,.245), (0,0,2.11,.435,.23),
        (0,0,2.19,.31,.205), (0,0,2.27,.225,.18), (0,0,2.36,.18,.16),
        (0,0,heads[''][1]-.005,.16,.155)]
    rows = tail_rows + spine_profile
    low, high = len(tail_rows)+5, len(tail_rows)+8
    holes = [(low,high,-4,4,'L'), (low,high,12,20,'R')]
    rings, previous_axis = [], Vector((1,0,0))
    for i,row in enumerate(rows):
        p = Vector(row[:3])
        tangent = (Vector(rows[min(i+1,len(rows)-1)][:3])-Vector(rows[max(i-1,0)][:3])).normalized()
        axis = previous_axis-tangent*previous_axis.dot(tangent)
        if axis.length < .05:
            axis = Vector((0,1,0))-tangent*tangent.y
        axis.normalize()
        if i >= len(tail_rows)-12:
            target = Vector((1,0,0))-tangent*tangent.x
            if target.length > .05:
                target.normalize()
                angle = math.atan2(tangent.dot(axis.cross(target)),axis.dot(target))
                axis = Quaternion(tangent,angle/max(1,len(tail_rows)-i)) @ axis
        across = tangent.cross(axis).normalized()
        previous_axis = axis.copy()
        ring = []
        for j in range(n):
            a = 2*pi*j/n
            q = p+axis*cos(a)*row[3]+across*sin(a)*row[4]
            if i < len(tail_rows)-1:
                f = clamp((1-i/(len(tail_rows)-1))*8,0,7.999)
                index, blend = int(f), smooth(f-int(f))
                wa = weights(('Tail_%02d'%(index+1),1-blend),('Tail_%02d'%min(8,index+2),blend))
                if i >= len(tail_rows)-4:
                    blend = smooth((i-len(tail_rows)+4)/3)
                    wa = weights(*[(k,v*(1-blend)) for k,v in wa.items()],('COG',blend))
            else:
                wa = shoulder_weights(q,arms)
            ring.append(surface.vertex(q,(j/n,i/(len(rows)-1)),wa))
        rings.append(ring)
    for i in range(len(rows)-1):
        for j in range(n):
            if any(lo <= i < hi and j in [k%n for k in range(left,right)] for lo,hi,left,right,_ in holes):
                continue
            color = 5 if 20 <= j <= 27 else (1 if 4 <= j <= 11 else 2)
            surface.face((rings[i][j],rings[i][(j+1)%n],rings[i+1][(j+1)%n],rings[i+1][j]),color)
    surface.face(tuple(reversed(rings[0])),2)
    surface.face(tuple(rings[-1]),2)
    for lo,hi,left,right,side in holes:
        boundary = [rings[lo][j%n] for j in range(left,right)]
        boundary += [rings[i][right%n] for i in range(lo,hi)]
        boundary += [rings[hi][j%n] for j in range(right,left,-1)]
        boundary += [rings[i][left%n] for i in range(hi,lo,-1)]
        shoulder,elbow,wrist = [Vector(v) for v in arms[side]]
        sign = 1 if side == 'L' else -1
        center = Vector((sign*.425,0,1.975))
        # Round the existing shared boundary rather than laying circular rings onto a rectangle.
        projected = [(Vector(surface.v[k]).y,Vector(surface.v[k]).z-center.z) for k in boundary]
        area = sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(projected,projected[1:]+projected[:1]))
        orientation = 1 if area > 0 else -1
        fit = sum(complex(x,y)*complex(cos(-orientation*2*pi*j/len(boundary)),sin(-orientation*2*pi*j/len(boundary))) for j,(x,y) in enumerate(projected))
        offset = math.atan2(fit.imag,fit.real)
        angles = [offset+orientation*2*pi*j/len(boundary) for j in range(len(boundary))]
        for index,a in zip(boundary,angles):
            y,z = cos(a)*.185,center.z+sin(a)*.155
            x = sign*.49*math.sqrt(max(.1,1-(y/.255)**2))
            q = Vector((x,y,z))
            surface.v[index] = tuple(q)
            surface.weights[index] = shoulder_weights(q,arms)
        direction = (elbow-shoulder).normalized()
        path = [shoulder+direction*d for d in (.035,.070,.110,.155,.205)]
        path += [shoulder.lerp(elbow,t) for t in (.68,.84,1.0)]
        path += [elbow.lerp(wrist,t) for t in (.17,.38,.68,1.0)]
        radii = [.188,.208,.222,.224,.214,.193,.164,.150,.155,.151,.125,.096]
        # Match circular arm-loop ordering to the rounded shared boundary.
        u = Vector((0,1,0))
        v = direction.cross(u).normalized()
        projection = [((Vector(surface.v[k])-center).dot(u),(Vector(surface.v[k])-center).dot(v)) for k in boundary]
        area = sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(projection,projection[1:]+projection[:1]))
        order = 1 if area > 0 else -1
        fit = sum(complex(x,y)*complex(cos(-order*2*pi*j/len(boundary)),sin(-order*2*pi*j/len(boundary))) for j,(x,y) in enumerate(projection))
        offset = math.atan2(fit.imag,fit.real)
        angles = [offset+order*2*pi*j/len(boundary) for j in range(len(boundary))]
        previous = boundary
        for k,(p,radius) in enumerate(zip(path,radii)):
            tangent = (path[min(k+1,len(path)-1)]-path[max(k-1,0)]).normalized()
            u = (Vector((0,1,0))-tangent*tangent.y).normalized()
            v = tangent.cross(u).normalized()
            ring = []
            for j,a in enumerate(angles):
                q = p+u*cos(a)*radius*.9+v*sin(a)*radius
                ring.append(surface.vertex(q,(j/len(angles),k/len(path)),arm_weights(q,side,arms)))
            bridge(surface,previous,ring,2)
            previous = ring
        surface.face(tuple(reversed(previous)),2)
    return rings
