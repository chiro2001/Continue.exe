"""Offline Blender geometry review only. Does not save source or operate Unity."""
import bpy, json, sys
from pathlib import Path
from mathutils import Vector
out=Path(sys.argv[sys.argv.index('--')+1])
views=[]
for stem,shots in [('fullfilm_v3_workbench',[
    ('eye-070',(0,1.55,.7/2.4),(0,-.06,.43)),
    ('eye-110',(0,1.55,1.1/2.4),(0,-.06,.43)),
    ('eye-150',(0,1.55,1.5/2.4),(0,-.06,.40))]),
    ('fullfilm_v3_scene_block',[
    ('walkway-eye',(0,4.05,1.10),(0,-1.6,1.20)),
    ('archive-side',(-5,4,2.9),(-.8,-.5,1.25))])]:
    bpy.ops.wm.open_mainfile(filepath=str(out/(stem+'.blend')))
    scene=bpy.context.scene
    scene.render.resolution_x=1280;scene.render.resolution_y=800
    cam=scene.camera;cam.data.type='PERSP';cam.data.lens=32
    for label,pos,target in shots:
        cam.location=pos
        cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/(stem+'_'+label+'.png'))
        bpy.ops.render.render(write_still=True)
        views.append({'file':Path(scene.render.filepath).name,'camera_blender_m':list(pos),'target_blender_m':list(target),
            'note':'Unbound neutral UI slots; review geometry/occlusion only, not final software screenshot legibility'})
(out/'offline-review-views.json').write_text(json.dumps(views,ensure_ascii=False,indent=2),encoding='utf8')
