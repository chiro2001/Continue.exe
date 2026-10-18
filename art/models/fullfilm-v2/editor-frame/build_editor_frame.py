import bpy,bmesh,json,math
from mathutils import Vector
from pathlib import Path
OUT=Path(r'C:/Users/Chiro/tmp/vrc-old-web-map/fullfilm-v2/editor-frame');OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
MAT={};SLOTS=[]
def mat(name,c,metal=.0,rough=.55):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*c,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;MAT[name]=m
mat('M_EF_OuterGraphite',(.075,.080,.088),.45,.34)
mat('M_EF_RevealDark',(.025,.030,.038),.25,.5)
mat('M_EF_RailEdge',(.16,.17,.185),.5,.33)
mat('M_EF_BackShell',(.043,.047,.055),.35,.46)
for name,c in [('Title',(.10,.105,.115)),('Toolbar',(.11,.115,.125)),('Hierarchy',(.085,.09,.10)),('Inspector',(.095,.10,.11)),('Timeline',(.08,.085,.095))]:mat('M_EF_UI_'+name+'_SLOT',c,0,.72)
def node(n,p=None,loc=(0,0,0)):
 o=bpy.data.objects.new(n,None);bpy.context.collection.objects.link(o);o.parent=p;o.location=loc;o.empty_display_size=.12;return o
root=node('FFV2_EditorFrame');root['units']='metres';root['viewport_opening']='3.2m x 1.8m; no centre mesh, ratio16:9';root['purpose']='Static physical editor-window frame with real screenshot UV slots'
frame=node('Frame_Perimeter',root)
def finish(o,n,p,loc,material,bevel=.004):
 o.name=n;o.parent=p;o.location=loc;bpy.context.view_layer.objects.active=o
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if material:o.data.materials.append(MAT[material])
 if bevel:
  b=o.modifiers.new('Fine structural edge','BEVEL');b.width=bevel;b.segments=2;bpy.ops.object.modifier_apply(modifier=b.name)
 o.select_set(False);return o
def box(n,p,loc,size,material,bevel=.004):
 bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1);o=bpy.context.object;o.scale=size;return finish(o,n,p,loc,material,bevel)
def cyl(n,p,loc,r,depth,material):
 bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=r,depth=depth);o=bpy.context.object;o.rotation_euler[0]=math.pi/2;return finish(o,n,p,loc,material,0)
def ui(n,p,loc,w,h,material):
 d=bpy.data.meshes.new(n+'_Mesh');d.from_pydata([(-w/2,0,-h/2),(-w/2,0,h/2),(w/2,0,h/2),(w/2,0,-h/2)],[],[(0,1,2,3)]);d.update();o=bpy.data.objects.new(n,d);bpy.context.collection.objects.link(o);o.parent=p;o.location=loc;d.materials.append(MAT[material]);uv=d.uv_layers.new(name='ScreenshotUV')
 for l,v in zip(uv.data,[(0,0),(0,1),(1,1),(1,0)]):l.uv=v
 SLOTS.append({'object':n,'parent':p.name,'size_m':[w,h],'pivot_local_blender_m':list(loc),'material':material,'uv_rect':[0,0,1,1],'uv_layer':'ScreenshotUV','normal_blender':[0,1,0],'screenshot_status':'unbound_real_capture_required'})
 o['source_policy']='Only bind a real editor screenshot strip, do not generate imitation software text or controls';return o
# Outer chassis remains structurally distinct from content panels.
for side in (-1,1):
 box('Frame_OuterVertical_'+('L' if side<0 else 'R'),frame,(side*1.97,0,0),(.06,.22,2.60),'M_EF_OuterGraphite',.009)
 box('Frame_RearVerticalFlange_'+('L' if side<0 else 'R'),frame,(side*1.935,-.105,0),(.11,.055,2.55),'M_EF_BackShell',.006)
for side in (-1,1):
 box('Frame_OuterHorizontal_'+('Bottom' if side<0 else 'Top'),frame,(0,0,side*1.27),(3.88,.22,.06),'M_EF_OuterGraphite',.009)
 box('Frame_RearHorizontalFlange_'+('Bottom' if side<0 else 'Top'),frame,(0,-.105,side*1.235),(3.83,.055,.11),'M_EF_BackShell',.006)
# Central reveal has real inside walls and an exact 3.2x1.8 opening.
reveal=node('Viewport_InnerReveal',root)
for side in (-1,1):
 box('Reveal_Vertical_'+('L' if side<0 else 'R'),reveal,(side*1.62,-.012,0),(.04,.18,1.88),'M_EF_RevealDark',.003)
 box('Reveal_Horizontal_'+('Bottom' if side<0 else 'Top'),reveal,(0,-.012,side*.92),(3.20,.18,.04),'M_EF_RevealDark',.003)
 box('Reveal_FrontLipVertical_'+('L' if side<0 else 'R'),reveal,(side*1.635,.083,0),(.028,.020,1.87),'M_EF_RailEdge',.003)
 box('Reveal_FrontLipHorizontal_'+('Bottom' if side<0 else 'Top'),reveal,(0,.083,side*.935),(3.25,.020,.025),'M_EF_RailEdge',.003)
# Screenshot-backed toolbar/title bars. No modelled fake buttons.
top=node('TopToolbar_Rig',root,(0,0,1.135));top['pivot_role']='top panel centre, independently separable'
box('Toolbar_SteppedHousing',top,(0,-.016,0),(3.86,.165,.25),'M_EF_OuterGraphite',.006)
box('Toolbar_RecessedBed',top,(0,.070,-.030),(3.22,.020,.17),'M_EF_RevealDark',.002)
ui('UI_TopToolbar',top,(0,.082,-.030),3.18,.15,'M_EF_UI_Toolbar_SLOT')
ui('UI_TopTitle',top,(0,.070,.105),3.80,.058,'M_EF_UI_Title_SLOT')
# Two editor side panels have their own thin multi-step shells.
for sign,name,slot in [(-1,'LeftHierarchy','Hierarchy'),(1,'RightInspector','Inspector')]:
 p=node(name+'_Rig',root,(sign*1.80,0,0));p['pivot_role']='side layer centre; move root for separation'
 box(name+'_RearShell',p,(0,-.025,0),(.30,.14,2.27),'M_EF_BackShell',.005)
 box(name+'_InsetPanel',p,(0,.053,0),(.281,.026,2.20),'M_EF_RevealDark',.003)
 for s in (-1,1):
  box(name+'_EdgeRail_'+str(s),p,(s*.147,.069,0),(.015,.026,2.25),'M_EF_RailEdge',.002)
  box(name+'_EndCap_'+str(s),p,(0,.069,s*1.117),(.295,.026,.016),'M_EF_RailEdge',.002)
 ui('UI_'+name,p,(0,.069,0),.265,2.16,'M_EF_UI_'+slot+'_SLOT')
 # Small real housing seam, not an imaginary on-screen control.
 box(name+'_OuterSeam',p,(sign*.153,-.013,0),(.007,.07,1.97),'M_EF_RevealDark',.001)
# Timeline cassette with three physical track channels behind a unified real screenshot strip.
bottom=node('BottomTimeline_Rig',root,(0,0,-1.105));bottom['pivot_role']='timeline cassette centre'
box('Timeline_BackCassette',bottom,(0,-.022,0),(3.86,.17,.30),'M_EF_BackShell',.006)
box('Timeline_RecessedBed',bottom,(0,.065,0),(3.23,.026,.276),'M_EF_RevealDark',.003)
for z in (-.142,-.045,.045,.142):
 box('Timeline_TrackChannelRail',bottom,(0,.072,z),(3.25,.025,.009),'M_EF_RailEdge',.001)
ui('UI_BottomTimeline',bottom,(0,.088,0),3.18,.267,'M_EF_UI_Timeline_SLOT')
# Structural fixings and rear connector recesses add plausible depth when travelling through.
fix=node('Frame_AssemblyDetails',root)
for x in (-1.97,1.97):
 for z in (-1.21,0,1.21):
  cyl('Frame_CountersunkFastener',fix,(x,.113,z),.009,.006,'M_EF_RevealDark')
for x in (-.20,-.09,.02,.13):
 box('Frame_RearConnectorPocket',fix,(x,-.145,-1.105),(.068,.018,.025),'M_EF_RevealDark',.002)
# Anchor nodes make the 16:9 viewport measurable; they contain no meshes.
anchor=node('Viewport_16x9_Anchor',root);anchor['opening_size_m']=[3.2,1.8];anchor['front_plane_blender_y']=.09
for n,loc in [('TopLeft',(-1.6,.09,.9)),('TopRight',(1.6,.09,.9)),('BottomLeft',(-1.6,.09,-.9)),('BottomRight',(1.6,.09,-.9))]:node('Viewport_'+n,anchor,loc)
objects=[root]+list(root.children_recursive)
rows=[]
for o in objects:
 if o.type=='MESH':
  if not o.name.startswith('UI_'):
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
  if not o.data.uv_layers:
   uv=o.data.uv_layers.new(name='SurfaceUV')
   for f in o.data.polygons:
    axis=max(range(3),key=lambda i:abs(f.normal[i]));axes=[i for i in range(3) if i!=axis]
    for li in f.loop_indices:
     co=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(co[axes[0]],co[axes[1]])
  o.data.calc_loop_triangles()
 bpy.context.view_layer.update()
 row={'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'pivot_local_blender_m':[round(v,6) for v in o.location],'size_m':[round(v,6) for v in o.dimensions]}
 if o.type=='MESH':row.update(triangles=len(o.data.loop_triangles),vertices=len(o.data.vertices),uv_layers=[u.name for u in o.data.uv_layers],materials=[m.name for m in o.data.materials])
 rows.append(row)
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
stem='fullfilm_v2_editor_frame'
bpy.ops.export_scene.fbx(filepath=str(OUT/(stem+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,mesh_smooth_type='FACE')
bpy.ops.export_scene.gltf(filepath=str(OUT/(stem+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_animations=False,export_cameras=False,export_lights=False)
report={'schema':'fullfilm-v2-editor-frame.1','blender':bpy.app.version_string,'units':'metres','root':root.name,'blender_axes':'Z up, +Y front','fbx_export_axes':'-Z forward, Y up','dimensions_m':[4,2.6,.27],
 'opening':{'width_m':3.2,'height_m':1.8,'aspect':16/9,'centre_blender_m':[0,0,0],'bounds_blender_xz_m':[-1.6,-.9,1.6,.9],'geometry':'No centre mesh or transparent centre plane','anchor':'Viewport_16x9_Anchor'},'ui_slots':SLOTS,'objects':rows,
 'mesh_count':sum(o.type=='MESH' for o in objects),'triangles':sum(r.get('triangles',0) for r in rows),'animation_data_count':0,'real_screenshot_status':'unbound; only blank neutral PBR material slots supplied'}
# Saved preview uses a dedicated camera/lights, excluded from both model exports.
sc=bpy.context.scene;sc.render.engine='BLENDER_EEVEE_NEXT';sc.render.resolution_x=1600;sc.render.resolution_y=1100;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG'
world=bpy.data.worlds.new('FramePreviewWorld');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.065,.085,1);world.node_tree.nodes['Background'].inputs[1].default_value=.5;sc.world=world
bpy.ops.object.camera_add(location=(4.3,8.7,3.9));cam=bpy.context.object;cam.name='SOURCE_PREVIEW_CAMERA';cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=4.8;sc.camera=cam
for n,loc,en,size in [('Key',(1,4,6),1300,5),('Fill',(-4,3,1),900,4),('Rim',(2,-3,3),1500,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name='SOURCE_PREVIEW_'+n;o.data.energy=en;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   sp=area.spaces.active;sp.region_3d.view_distance=6;sp.region_3d.view_location=(0,0,0);sp.region_3d.view_rotation=cam.rotation_euler.to_quaternion();sp.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(stem+'.blend')))
sc.render.filepath=str(OUT/(stem+'_preview.png'));bpy.ops.render.render(write_still=True)
(OUT/'fullfilm-v2-editor-frame.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('EDITOR_FRAME_COMPLETE',report['mesh_count'],report['triangles'])
