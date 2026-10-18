#!/usr/bin/env python3
"""Blender 4.2 background authoring. Static source meshes only; no Unity API.

blender --factory-startup --background --python build_fullfilm_v2_blender.py -- --output DIR
Blender metres, Z up, +Y faces viewer. FBX/glTF convert to Y up.
"""
import argparse
import json
import math
import os
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


def args():
    p = argparse.ArgumentParser()
    p.add_argument('--output', required=True)
    p.add_argument('--render', action='store_true')
    return p.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])


OPT = args()
OUT = Path(OPT.output)
OUT.mkdir(parents=True, exist_ok=True)
MANIFEST = {'schema': 2, 'units': 'metres', 'blender': bpy.app.version_string,
            'axes': {'blender': 'X right, Z up, +Y front', 'gltf_unity': 'X right, Y up, -Z front'},
            'assets': [], 'ui_slots': []}
MAT = {}


def clean():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    MAT.clear()


def material(name, rgb, metallic=0, roughness=.45):
    if name in MAT:
        return MAT[name]
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb, 1)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = roughness
    MAT[name] = m
    return m


def palette():
    material('M_GraphitePowderCoat', (.055, .065, .08), .55, .33)
    material('M_AnodisedWarmSilver', (.45, .48, .49), .75, .30)
    material('M_ChampagneEdge', (.55, .35, .17), .6, .32)
    material('M_Walnut', (.22, .115, .065), 0, .48)
    material('M_Endgrain', (.32, .19, .10), 0, .6)
    material('M_Rubber', (.025, .03, .035), 0, .8)
    material('M_KeysIvory', (.70, .70, .64), 0, .42)
    material('M_KeysSlate', (.16, .19, .21), 0, .38)
    material('M_JadeAccent', (.11, .42, .38), .2, .35)
    material('M_UI_MainScreenshot_SLOT', (.07, .09, .12), 0, .7)
    material('M_UI_LeftHierarchy_SLOT', (.09, .115, .14), 0, .7)
    material('M_UI_RightAssets_SLOT', (.12, .14, .17), 0, .7)
    material('M_UI_Timeline_SLOT', (.085, .11, .13), 0, .7)
    material('M_UI_Thumbnail_SLOT', (.18, .23, .23), 0, .7)
    material('M_Sandstone', (.42, .46, .46), 0, .8)
    material('M_CreamConcrete', (.68, .66, .57), 0, .74)
    material('M_Terracotta', (.49, .22, .13), 0, .62)
    material('M_CanopyLeaf', (.10, .25, .19), 0, .72)
    material('M_CanopySage', (.25, .37, .27), 0, .72)
    material('M_PathInset', (.41, .61, .58), .12, .5)


def node(name, location=(0, 0, 0), parent=None):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    o.location = location
    o.empty_display_type = 'PLAIN_AXES'
    o.empty_display_size = .13
    return o


def finish(o, name, loc, mat, parent, bevel=0):
    o.name = name
    o.location = loc
    o.parent = parent
    if mat:
        o.data.materials.append(MAT[mat])
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        b = o.modifiers.new('Machined edge bevel', 'BEVEL')
        b.width = bevel
        b.segments = 2
        bpy.ops.object.modifier_apply(modifier=b.name)
    # Weighted corner normals keep planar panels clean under nearby lighting.
    if o.type == 'MESH':
        for f in o.data.polygons:
            f.use_smooth = False
        o['static_source_mesh'] = True
    o.select_set(False)
    return o


def box(name, loc, size, mat, parent=None, bevel=.004):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.object
    o.scale = size
    return finish(o, name, loc, mat, parent, bevel)


def cylinder(name, loc, radius, depth, mat, parent=None, axis='Z', vertices=16):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    o = bpy.context.object
    if axis == 'Y':
        o.rotation_euler[0] = math.pi / 2
    if axis == 'X':
        o.rotation_euler[1] = math.pi / 2
    return finish(o, name, loc, mat, parent, .0015 if radius > .007 else 0)


def mesh(name, verts, faces, mat, parent=None, location=(0, 0, 0)):
    d = bpy.data.meshes.new(name + '_Mesh')
    d.from_pydata(verts, [], faces)
    d.update()
    o = bpy.data.objects.new(name, d)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    o.location = location
    if mat:
        d.materials.append(MAT[mat])
    return o


def face(name, parent, width, height, loc, mat, source_aspect=None):
    # XZ face, normal +Y. No rotation or mirrored UV required in Unity.
    o = mesh(name, [(-width/2, 0, -height/2), (-width/2, 0, height/2),
                    (width/2, 0, height/2), (width/2, 0, -height/2)], [(0, 1, 2, 3)], mat, parent, loc)
    uv = o.data.uv_layers.new(name='ScreenshotUV')
    for loop, coord in zip(uv.data, [(0, 0), (0, 1), (1, 1), (1, 0)]):
        loop.uv = coord
    o['uv_policy'] = 'Full image 0..1, bottom-left origin, front normal +Y'
    o['screenshot_material_slot'] = mat
    MANIFEST['ui_slots'].append({'object': name, 'material': mat, 'size_m': [width, height],
                                 'aspect': source_aspect or width/height, 'uv': [0, 0, 1, 1],
                                 'front_blender': [0, 1, 0], 'screenshot_status': 'unbound_real_capture_required'})
    return o


def join(parts, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    parts[0].name = name
    return parts[0]


def rod(name, start, end, r, mat, parent=None, vertices=12):
    a, b = Vector(start), Vector(end)
    o = cylinder(name, (a+b)/2, r, (b-a).length, mat, parent, vertices=vertices)
    o.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return o


def cable(name, points, radius, mat, parent):
    c = bpy.data.curves.new(name, 'CURVE')
    c.dimensions = '3D'
    c.resolution_u = 2
    c.bevel_depth = radius
    c.bevel_resolution = 1
    sp = c.splines.new('POLY')
    sp.points.add(len(points)-1)
    for p, xyz in zip(sp.points, points):
        p.co = (*xyz, 1)
    o = bpy.data.objects.new(name, c)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    c.materials.append(MAT[mat])
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target='MESH')
    o.select_set(False)
    return o


def panel(name, parent, loc, w, h, slot, framed=True):
    p = node(name, loc, parent)
    if framed:
        parts = [box('Rail', (x, -.013, 0), (.018, .044, h+.032), 'M_AnodisedWarmSilver', p, .003) for x in (-w/2-.009, w/2+.009)]
        parts += [box('Rail', (0, -.013, z), (w+.032, .044, .018), 'M_AnodisedWarmSilver', p, .003) for z in (-h/2-.009, h/2+.009)]
        join(parts, name + '_Perimeter')
        box(name+'_Backplate', (0, -.033, 0), (w+.012, .016, h+.012), 'M_GraphitePowderCoat', p)
    face('UI_'+name, p, w, h, (0, .011, 0), slot)
    return p


def build_workbench():
    clean(); palette()
    root = node('FFV2_Workbench', (0, 0, 0))
    root['purpose'] = 'One physical authoring desk; two detachable editor side layers'
    top = node('DeskStructure', parent=root)
    # Distinct slab edge, inset fasteners, foot rails and cable tray.
    box('Desk_WalnutSlab', (0, .05, .767), (2.42, .93, .056), 'M_Walnut', top, .014)
    box('Desk_DeskMat', (0, .21, .798), (1.56, .65, .006), 'M_Rubber', top, .007)
    metal = []
    for x in (-.94, .94):
        metal.append(box('TrestleLeg', (x, -.17, .39), (.064, .065, .72), 'M_GraphitePowderCoat', top))
        metal.append(box('TrestleFoot', (x, .01, .045), (.09, .74, .055), 'M_GraphitePowderCoat', top, .009))
        metal.append(box('UnderSlabRail', (x, .01, .72), (.06, .75, .06), 'M_GraphitePowderCoat', top))
        for y in (-.28, .30):
            metal.append(cylinder('RubberFoot', (x, y, .014), .042, .018, 'M_Rubber', top))
    metal.append(box('RearStretcher', (0, -.19, .33), (1.9, .045, .09), 'M_GraphitePowderCoat', top))
    join(metal, 'Desk_SteelTrestleAndFeet')
    box('CableTray_LowerFold', (0, -.25, .688), (1.52, .18, .026), 'M_GraphitePowderCoat', top)
    box('CableTray_RearLip', (0, -.335, .71), (1.52, .018, .065), 'M_GraphitePowderCoat', top)
    slits = []
    for i in range(14):
        slits.append(box('CableTraySlit', (-.64+i*.098, -.242, .703), (.062, .085, .004), 'M_Rubber', top, .001))
    join(slits, 'CableTray_VentInserts')
    monitor = node('DisplayRig_Main', (0, -.10, 1.36), root)
    monitor['pivot_role'] = 'Screen centre; enlargement and screen-to-world alignment anchor'
    box('Monitor_RearShell', (0, -.042, 0), (1.365, .071, .813), 'M_GraphitePowderCoat', monitor, .018)
    rim = []
    for x in (-.661, .661):
        rim.append(box('MonitorBezel', (x, .008, 0), (.042, .043, .79), 'M_GraphitePowderCoat', monitor, .007))
    for z in (-.383, .383):
        rim.append(box('MonitorBezel', (0, .008, z), (1.325, .043, .046), 'M_GraphitePowderCoat', monitor, .007))
    join(rim, 'Monitor_FrontBezel')
    face('UI_Main_16x9', monitor, 1.28, .72, (0, .031, 0), 'M_UI_MainScreenshot_SLOT', 16/9)
    box('Monitor_ChinSilverLine', (0, .031, -.385), (1.18, .006, .008), 'M_AnodisedWarmSilver', monitor, .001)
    cylinder('Monitor_PowerButton', (.608, .037, -.383), .006, .005, 'M_JadeAccent', monitor, 'Y')
    vents = []
    for i in range(25):
        vents.append(box('RearVent', (-.48+i*.04, -.080, .24), (.020, .006, .070), 'M_Rubber', monitor, .001))
    join(vents, 'Monitor_RearVentBank')
    box('Monitor_VesaPlate', (0, -.091, 0), (.19, .025, .19), 'M_AnodisedWarmSilver', monitor, .008)
    screws = [cylinder('M4Fastener', (x, -.109, z), .009, .007, 'M_GraphitePowderCoat', monitor, 'Y', 8) for x in (-.065,.065) for z in (-.065,.065)]
    join(screws, 'Monitor_VesaFasteners')
    port = []
    for i in range(4):
        port.append(box('RearInterface', (-.43+i*.085, -.083, -.22), (.05, .009, .022), 'M_Rubber', monitor, .002))
        port.append(box('PortMetalLip', (-.43+i*.085, -.085, -.202), (.052, .005, .004), 'M_AnodisedWarmSilver', monitor, .001))
    join(port, 'Monitor_HDMI_USB_InterfaceBank')
    stand = node('MonitorStand', (0, -.12, .80), root)
    box('MonitorStand_WeightedFoot', (0, -.055, .022), (.43, .30, .043), 'M_AnodisedWarmSilver', stand, .012)
    box('MonitorStand_Column', (0, -.12, .22), (.075, .075, .40), 'M_AnodisedWarmSilver', stand, .009)
    cylinder('MonitorStand_Hinge', (0, -.06, .52), .048, .24, 'M_GraphitePowderCoat', stand, 'X')
    cable('Monitor_CableToTray', [(0,-.18,1.13),(.04,-.26,1.06),(.07,-.28,.88),(.15,-.28,.71),(.45,-.27,.71)], .007, 'M_Rubber', root)
    # Low profile keyboard: cap borders, individual wells and a tactile row silhouette.
    kb = node('Keyboard', (0, .36, .812), root)
    box('Keyboard_AnodisedCase', (0, 0, .014), (.78, .265, .028), 'M_AnodisedWarmSilver', kb, .011)
    box('Keyboard_InsetWell', (0, 0, .031), (.75, .235, .009), 'M_Rubber', kb, .006)
    ivory, dark = [], []
    for row in range(4):
        for col in range(14):
            x = -.343+col*.052+(row%2)*.006
            y = -.086+row*.052
            dst = dark if col in (0,13) or row==0 else ivory
            dst.append(box('Keycap', (x,y,.043), (.045,.045,.018), 'M_KeysSlate' if dst is dark else 'M_KeysIvory', kb, .004))
    for x,w in [(-.31,.065),(-.227,.065),(-.144,.065),(.02,.238),(.188,.065),(.27,.065),(.34,.045)]:
        ivory.append(box('ModifierKey', (x,.122,.043), (w,.042,.018), 'M_KeysIvory', kb,.004))
    join(ivory, 'Keyboard_IvoryKeycaps'); join(dark, 'Keyboard_SlateKeycaps')
    for x in (-.34,.34):
        box('Keyboard_RubberPad', (x,-.07,-.003), (.045,.10,.01), 'M_Rubber', kb)
    cable('Keyboard_Cable', [(-.35,.22,.82),(-.49,.15,.81),(-.52,-.08,.81),(-.45,-.29,.73)], .004, 'M_Rubber', root)
    tablet = node('PenTablet', (.76,.32,.806), root)
    box('PenTablet_Body', (0,0,.012), (.40,.29,.024), 'M_GraphitePowderCoat',tablet,.009)
    box('PenTablet_ActiveArea', (0,0,.026), (.34,.23,.003), 'M_KeysSlate',tablet,.003)
    pen = node('Stylus_PivotNib', (.75,.46,.85), root)
    pen.rotation_euler = (0, math.radians(73), math.radians(-21))
    cylinder('Stylus_Barrel', (0,0,.072), .007,.134,'M_GraphitePowderCoat',pen)
    cylinder('Stylus_Grip', (0,0,.035),.008,.034,'M_Rubber',pen)
    cylinder('Stylus_Nib', (0,0,.004),.0025,.012,'M_AnodisedWarmSilver',pen)
    cylinder('Stylus_EndCap',(0,0,.143),.006,.008,'M_ChampagneEdge',pen)
    # Exactly two detached side layers. Smaller inset strip belongs to the central rig.
    left = panel('LeftHierarchy', root, (-1.015,-.055,1.40), .44,.70,'M_UI_LeftHierarchy_SLOT')
    left.rotation_euler[2] = math.radians(-9)
    right = panel('RightAssets',root,(1.045,-.055,1.40),.46,.70,'M_UI_RightAssets_SLOT')
    right.rotation_euler[2] = math.radians(9)
    panel('Timeline',monitor,(0,.05,-.49),1.20,.14,'M_UI_Timeline_SLOT')
    for x in (-.945,.945):
        rod('SidePanel_Arm',(x,-.12,.80),(x,-.12,1.24),.018,'M_GraphitePowderCoat',root)
        rod('SidePanel_Elbow',(x,-.12,1.24),(x,-.07,1.40),.015,'M_AnodisedWarmSilver',root)
    # Three actual extruded thumbnail cards, independently addressable on the right side.
    for i in range(3):
        p=node('ThumbnailCard_%02d'%i,(1.048,.005,1.60-i*.195),root)
        box('Thumbnail_Backing',(0,-.008,0),(.27,.012,.152),'M_GraphitePowderCoat',p,.002)
        face('UI_Thumbnail_%02d'%i,p,.256,.144,(0,.002,0),'M_UI_Thumbnail_SLOT',16/9)
    export('fullfilm_v2_workbench', root, (3.4,4.0,2.8), (0,0,1.0))


def arc(name, parent, radius, thickness, start, end, z, depth, mat, segments=28):
    # Flat horizontal ring wedge, extruded vertically.
    v=[]
    for h in (z,z+depth):
        for r in (radius-thickness/2,radius+thickness/2):
            for i in range(segments+1):
                a=math.radians(start+(end-start)*i/segments)
                v.append((math.sin(a)*r, math.cos(a)*r,h))
    n=segments+1; f=[]
    for i in range(segments):
        f.extend([(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
                  (i,2*n+i,2*n+i+1,i+1),(n+i,n+i+1,3*n+i+1,3*n+i)])
    f += [(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)]
    return mesh(name,v,f,mat,parent)


def arch(parent, name='Gateway', w=3.55, h=3.4, depth=.32, loc=(0,-1.7,0)):
    p=node(name,loc,parent)
    r=w/2; spring=h-r
    # Ring arch has genuine open volume; reveal inner faces from near and oblique views.
    verts=[]; segments=28
    for y in (-depth/2,depth/2):
        for rr in (r-.16,r+.16):
            for i in range(segments+1):
                a=math.pi*i/segments
                verts.append((math.cos(a)*rr,y,spring+math.sin(a)*rr))
    n=segments+1; faces=[]
    for i in range(segments):
        faces += [(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
                  (i,2*n+i,2*n+i+1,i+1),(n+i,n+i+1,3*n+i+1,3*n+i)]
    faces += [(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)]
    mesh(name+'_ArchRib',verts,faces,'M_CreamConcrete',p)
    for x in (-r,r):
        box(name+'_Jamb',(x,0,spring/2),(.32,depth,spring),'M_CreamConcrete',p,.015)
        box(name+'_StoneShoe',(x,0,.11),(.46,depth+.14,.22),'M_Sandstone',p,.012)
        box(name+'_InsetJadeStrip',(x,depth/2+.004,spring/2),(.048,.012,spring*.83),'M_JadeAccent',p,.003)
    return p


def build_scene():
    clean(); palette()
    root=node('FFV2_SceneBlock')
    root['purpose']='One reusable authored world mesh; thumbnail and expanded world MUST instance this asset'
    root['nominal_size']='7.8m wide x 7.8m deep x 4.0m high'
    ground=node('PlazaFoundation',parent=root)
    cylinder('Plaza_LowerStoneDisk',(0,0,-.15),3.9,.25,'M_Sandstone',ground,vertices=64)
    cylinder('Plaza_UpperDeck',(0,0,-.025),3.72,.08,'M_Walnut',ground,vertices=64)
    # Radial joints and inset bands establish scale without dense surface texture.
    arc('Plaza_InsetOuterBand',ground,3.54,.045,0,360,.019,.006,'M_ChampagneEdge',64)
    arc('Plaza_InnerStoneBorder',ground,1.82,.18,0,360,.022,.035,'M_CreamConcrete',64)
    planks=[]
    for i in range(-10,11):
        y=i*.30; half=math.sqrt(max(0,3.62**2-y*y))
        planks.append(box('DeckJoint',(0,y,.022),(half*2,.012,.005),'M_Rubber',ground,.001))
    join(planks,'Plaza_DeckPlankJoints')
    # Curved bench: open entry and a second exit remain, encouraging depth through the model.
    seating=node('RingSeating',parent=root)
    for j,(a,b) in enumerate([(43,91),(94,142),(218,266),(269,317)]):
        arc('Bench_%02d_StoneBase'%j,seating,2.56,.56,a,b,.05,.31,'M_Sandstone',12)
        arc('Bench_%02d_SeatSlats'%j,seating,2.56,.64,a,b,.36,.075,'M_Walnut',12)
        arc('Bench_%02d_BackRail'%j,seating,2.83,.07,a,b,.77,.09,'M_Endgrain',12)
        for ang in (a+4,(a+b)/2,b-4):
            x,y=math.sin(math.radians(ang))*2.83,math.cos(math.radians(ang))*2.83
            box('BenchBackSupport',(x,y,.59),(.045,.045,.42),'M_GraphitePowderCoat',seating)
    gateway=arch(root)
    # Walkway crosses the actual plaza; its handrail and piers make the miniature readable.
    bridge=node('BridgeWalkway',(0,.15,0),root)
    box('Bridge_LongitudinalBeam_L',(-.65,0,.14),(.13,6.35,.19),'M_Endgrain',bridge,.008)
    box('Bridge_LongitudinalBeam_R',(.65,0,.14),(.13,6.35,.19),'M_Endgrain',bridge,.008)
    boards=[]
    for i in range(26):
        boards.append(box('BridgeBoard',(0,-3.03+i*.242,.269),(1.46,.23,.085),'M_Walnut',bridge,.004))
    join(boards,'Bridge_26DeckBoards')
    for x in (-.71,.71):
        for y in (-2.58,1.50,2.58):
            box('Bridge_Post',(x,y,.69),(.065,.065,.93),'M_GraphitePowderCoat',bridge)
        for y0,y1 in [(-3.10,-2.18),(1.10,3.10)]:
            rod('Bridge_Handrail',(x,y0,1.12),(x,y1,1.12),.031,'M_AnodisedWarmSilver',bridge)
    # Sculpted branching mast and folded umbrella canopy recall a Hub tree without copying one.
    tree=node('BranchCanopy',(.95,-.35,0),root)
    rod('Canopy_Trunk',(0,0,.05),(-.16,.02,2.38),.12,'M_Endgrain',tree,12)
    for i in range(6):
        a=i*math.tau/6+.2
        end=(math.cos(a)*1.47,math.sin(a)*1.47,2.72+(i%2)*.20)
        rod('CanopyBranch_%02d'%i,(-.13,0,1.79),end,.052,'M_Endgrain',tree,10)
        a2=a+math.tau/6
        p0=(-.10,0,3.0)
        p1=(math.cos(a)*1.75,math.sin(a)*1.75,2.62)
        p2=(math.cos(a2)*1.75,math.sin(a2)*1.75,2.62)
        mid=((p1[0]+p2[0])*.50,(p1[1]+p2[1])*.50,2.85)
        verts=[p0,p1,mid,p2,(p0[0],p0[1],p0[2]-.045),(p1[0],p1[1],p1[2]-.045),
               (mid[0],mid[1],mid[2]-.045),(p2[0],p2[1],p2[2]-.045)]
        mesh('Canopy_FoldedLeaf_%02d'%i,verts,[(0,1,2),(0,2,3),(4,6,5),(4,7,6),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],
             'M_CanopyLeaf' if i%2==0 else 'M_CanopySage',tree)
    cylinder('Canopy_Planter',(.95,-.35,.16),.41,.32,'M_Terracotta',root,vertices=16)
    # A small semantic content panel in the same world, not an unrelated portal.
    notice=node('CommunityPanel',(-2.06,-1.18,0),root)
    notice.rotation_euler[2]=math.radians(-18)
    for x in (-.43,.43):
        box('Panel_Post',(x,0,.84),(.055,.07,1.68),'M_Endgrain',notice)
    panel('WorldNotice',notice,(0,.035,1.35),.92,.52,'M_UI_Thumbnail_SLOT')
    for i,(x,y) in enumerate([(-.99,2.9),(.99,2.9),(-1.02,-2.88),(1.02,-2.88)]):
        light=node('PathLight_%02d'%i,(x,y,0),root)
        cylinder('PathLight_Stem',(0,0,.31),.045,.62,'M_GraphitePowderCoat',light)
        cylinder('PathLight_Diffuser',(0,0,.56),.075,.11,'M_KeysIvory',light)
        cylinder('PathLight_Cap',(0,0,.63),.10,.035,'M_ChampagneEdge',light)
    export('fullfilm_v2_scene_block',root,(8.7,11.8,8.5),(0,0,1.0))


def build_hub_kit():
    clean(); palette()
    root=node('FFV2_HubDetailKit')
    root['purpose']='Optional selective structural detail kit; no historical reconstruction claim'
    arch(root,'HubEntrance',2.3,2.85,.32,(0,0,0))
    # Distinct doorway threshold, rounded edge and dark inset top slot.
    box('HubEntrance_Threshold',(0,.08,.045),(2.63,.74,.09),'M_Sandstone',root,.015)
    sign=node('HubCategorySign',(0,0,3.12),root)
    box('HubSign_Body',(0,0,0),(2.38,.18,.34),'M_Endgrain',sign,.012)
    face('UI_HubSign',sign,2.18,.25,(0,.095,0),'M_UI_Thumbnail_SLOT')
    bench=node('HubConversationBench',(3.1,0,0),root)
    for x in (-.69,.69):
        box('HubBench_StoneSupport',(x,0,.21),(.24,.60,.42),'M_Sandstone',bench,.015)
    for i in range(4):
        box('HubBench_TopSlat',(0,-.22+i*.145,.46),(1.94,.13,.075),'M_Walnut',bench,.009)
    for x in (-.83,.83):
        rod('HubBenchBackSupport',(x,-.28,.40),(x,-.35,1.04),.027,'M_GraphitePowderCoat',bench)
    for z in (.77,.95):
        box('HubBench_BackSlat',(0,-.33,z),(1.94,.065,.13),'M_Endgrain',bench,.008)
    # Timber post shoe and knee-brace module usable on the existing gallery posts.
    p=node('HubTimberPostDetail',(-2.35,0,0),root)
    box('HubPost_StoneShoe',(0,0,.14),(.61,.61,.28),'M_Sandstone',p,.012)
    box('HubPost_Timber',(0,0,1.66),(.32,.32,3.0),'M_Walnut',p,.012)
    box('HubPost_Capital',(0,0,3.15),(.70,.56,.16),'M_Endgrain',p,.008)
    rod('HubPost_KneeBrace_L',(0,0,2.53),(-.70,0,3.12),.058,'M_Endgrain',p,8)
    rod('HubPost_KneeBrace_R',(0,0,2.53),(.70,0,3.12),.058,'M_Endgrain',p,8)
    straps=[]
    for z in (.37,2.98):
        straps.append(box('PostMetalStrap',(0,0,z),(.35,.35,.065),'M_GraphitePowderCoat',p,.004))
    join(straps,'HubPost_MetalStraps')
    export('fullfilm_v2_hub_detail_kit',root,(7,11,6),(0,0,1.45))


def preview(camera_pos,target,stem):
    scene=bpy.context.scene
    scene.render.engine='BLENDER_EEVEE_NEXT'
    scene.render.resolution_x=1600; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    scene.world.color=(.07,.075,.09)
    world=bpy.data.worlds.new('PreviewWorld'); world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.10,.12,.16,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.45
    scene.world=world
    bpy.ops.object.camera_add(location=camera_pos)
    cam=bpy.context.object;cam.name='SOURCE_PREVIEW_CAMERA'
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=3.55 if 'workbench' in stem else 10.1
    scene.camera=cam
    for name,loc,energy,size in [('Key',(3,4,8),1500,5),('Fill',(-4,2,5),900,4),('Rim',(0,-5,6),1200,4)]:
        bpy.ops.object.light_add(type='AREA',location=loc)
        o=bpy.context.object;o.name='SOURCE_PREVIEW_'+name;o.data.energy=energy;o.data.shape='DISK';o.data.size=size
        o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_distance=5 if 'workbench' in stem else 11
                area.spaces.active.region_3d.view_location=target
                area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
                area.spaces.active.shading.type='MATERIAL'
    return scene


def export(stem,root,camera_pos,target):
    bpy.context.view_layer.update()
    objects=[root]+list(root.children_recursive)
    rows=[]
    for o in objects:
        row={'name':o.name,'parent':o.parent.name if o.parent else None,
             'type':o.type,'pivot_local_blender_m':[round(v,6) for v in o.location],
             'dimensions_m':[round(v,6) for v in o.dimensions],
             'rotation_euler_deg':[round(math.degrees(v),4) for v in o.rotation_euler]}
        if o.type=='MESH':
            if not o.name.startswith('UI_'):
                bm=bmesh.new();bm.from_mesh(o.data)
                bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
                bm.to_mesh(o.data);bm.free();o.data.update()
            o.data.calc_loop_triangles()
            row.update(vertices=len(o.data.vertices),triangles=len(o.data.loop_triangles),
                       uv_layers=[u.name for u in o.data.uv_layers],materials=[m.name for m in o.data.materials])
            # Every model has at least one authored UV set, including ordinary solid surfaces.
            if not o.data.uv_layers:
                uv=o.data.uv_layers.new(name='SurfaceUV')
                for f in o.data.polygons:
                    normal=f.normal;axis=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=axis]
                    for li in f.loop_indices:
                        co=o.data.vertices[o.data.loops[li].vertex_index].co
                        uv.data[li].uv=(co[axes[0]],co[axes[1]])
                row['uv_layers']=['SurfaceUV']
        rows.append(row)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(OUT/(stem+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},
                             add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',
                             apply_unit_scale=True,global_scale=1.0,mesh_smooth_type='FACE',path_mode='AUTO')
    bpy.ops.export_scene.gltf(filepath=str(OUT/(stem+'.glb')),export_format='GLB',use_selection=True,
                              export_yup=True,export_animations=False,export_cameras=False,export_lights=False)
    scene=preview(camera_pos,target,stem)
    scene['asset_contract']='Static source model. Preview camera/lights excluded from FBX/GLB. No authored animation.'
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(stem+'.blend')))
    if OPT.render:
        scene.render.filepath=str(OUT/(stem+'_preview.png'))
        bpy.ops.render.render(write_still=True)
    item={'id':stem,'root':root.name,'files':[stem+x for x in ('.blend','.fbx','.glb')],
          'mesh_count':sum(o.type=='MESH' for o in objects),'triangles':sum(r.get('triangles',0) for r in rows),
          'vertices':sum(r.get('vertices',0) for r in rows),'objects':rows}
    MANIFEST['assets'].append(item)
    (OUT/(stem+'_objects.json')).write_text(json.dumps(item,ensure_ascii=False,indent=2),encoding='utf-8')


build_workbench()
build_scene()
build_hub_kit()
(OUT/'fullfilm-v2-model-assets.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2),encoding='utf-8')
print('FFV2_STATIC_MODEL_BUILD_COMPLETE '+json.dumps([{k:a[k] for k in ('id','mesh_count','triangles')} for a in MANIFEST['assets']]))
