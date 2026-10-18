#!/usr/bin/env python3
"""Offline checks for the renewal score and authored source handoff, without Unity/audio."""
from pathlib import Path
import json
import math
from PIL import Image
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

ROOT=Path(__file__).resolve().parents[1]
DATA=ROOT/"data/fullfilm-renewal.json"
TMP=Path.home()/"tmp/vrc-old-web-map/fullfilm-renewal"


def ease(x):
    x=min(1.,max(0.,x));return x*x*(3-2*x)


def actor_position(actor,t):
    keys=actor["keys"]
    for a,b in zip(keys,keys[1:]):
        if t<=b[0]:
            u=ease((t-a[0])/(b[0]-a[0]));return (a[1]+(b[1]-a[1])*u,a[2]+(b[2]-a[2])*u)
    return tuple(keys[-1][1:3])


def main():
    data=json.loads(DATA.read_text()); failures=[]; checks=0
    def check(ok,message):
        nonlocal checks
        checks+=1
        if not ok:failures.append(message)
    check(data["travel"]["start"]==217.60 and data["travel"]["end"]==222.72,"Travel boundaries")
    check(data["physical_parent"]=="FullFilm Continuation","Stable physical parent")
    check(len(data["actors"])==6,"Six independent roles")
    max_speed=0.;min_pair=100.;min_eye=100.;near_d=None
    for actor in data["actors"]:
        keys=actor["keys"]
        check(all(b[0]>a[0] for a,b in zip(keys,keys[1:])),actor["id"]+" strictly increasing times")
        check(all(abs(k[0]/.08-round(k[0]/.08))<1e-5 for k in keys),actor["id"]+" 80 ms grid")
        check(all(0<=k[4]<=11 for k in keys),actor["id"]+" supported Humanoid pose IDs")
        for a,b in zip(keys,keys[1:]):
            speed=1.5*math.dist(a[1:3],b[1:3])/(b[0]-a[0]);max_speed=max(max_speed,speed)
    check(max_speed<=1.3,"No new actor sprint")
    for n in range(865):
        t=186.88+n*.08
        visible=[a for a in data["actors"] if a["show"][0]<=t<=a["show"][1]]
        points=[actor_position(a,t) for a in visible]
        for i,p in enumerate(points):
            min_eye=min(min_eye,math.dist(p,(0,-2.8)))
            for q in points[i+1:]:min_pair=min(min_pair,math.dist(p,q))
    check(min_pair>=.6,"Actors keep separate bodies")
    check(min_eye>=1.8,"Actors stay outside the eye column")
    guide=next(a for a in data["actors"] if a["id"]=="guide")
    near_d=math.dist(actor_position(guide,222.72),(0,-2.8));check(1.8<=near_d<=2.4,"Near demonstration at 2.2 m")
    # Test the aperture against a broad stationary head range as its deep jambs
    # sweep through. This is an authored-volume check, not a physical headset test.
    minimum_aperture=100.
    for i in range(513):
        t=217.6+i*.01;u=ease((t-217.6)/5.12);scale=1+2.4*u;z=5.6-16*u
        if -4.15<=z<=-1.45:
            half_width=2.39*scale-.025*scale;bottom=1.65-1.38*scale;top=1.65+1.38*scale
            minimum_aperture=min(minimum_aperture,half_width)
            check(half_width>1.2 and bottom<.55 and top>2.15,"Portal frame clears stationary head range at "+str(round(t,2)))
    check(minimum_aperture>4.,"Window becomes an ample 3D opening before passage")
    texture_dir=ROOT/"art/export/fullfilm/renewal"
    textures=list(texture_dir.glob("*.png"));check(len(textures)==17,"Seventeen original/memory texture exports")
    for file in textures:
        with Image.open(file) as image:
            check(max(image.size)<=2048,file.name+" texture budget")
            if file.name.startswith("layer-"):check(image.mode=="RGBA",file.name+" physical layer alpha")
    parser=Parser(Language(tree_sitter_c_sharp.language()))
    source_paths=[ROOT/"unity/Assets/OldWebMap/Scripts/OWFullRenewal.cs",ROOT/"unity/AgentScripts/OWFullRenewalBuild.cs"]
    syntax=[]
    for path in source_paths:
        tree=parser.parse(path.read_bytes());errors=[]
        def visit(node):
            if node.type=="ERROR" or node.is_missing:errors.append({"type":node.type,"line":node.start_point.row+1,"column":node.start_point.column+1})
            for child in node.children:visit(child)
        visit(tree.root_node);syntax.append({"path":str(path.relative_to(ROOT)),"errors":errors})
        check(not errors,path.name+" C# syntax")
    runtime=source_paths[0].read_text()
    check("autoSample = false" in runtime,"Common scheduler owns sampling")
    check("Time.time" not in runtime and "Time.deltaTime" not in runtime and "Random." not in runtime,"Absolute-time sampling")
    check("AudioSource" not in runtime and "Networking." not in runtime,"No audio or player state mutation")
    check("CreatePrimitive" not in runtime and "Instantiate(" not in runtime,"Geometry is authored and persisted in the editor")
    check("那就一起做下去" not in runtime,"Parent owns final caption")
    result={"success":not failures,"checks":checks,"failures":failures,"max_actor_speed_mps":max_speed,"minimum_actor_pair_m":min_pair,"minimum_eye_distance_m":min_eye,"near_guide_distance_m":near_d,"minimum_portal_half_width_during_crossing_m":minimum_aperture,"textures":len(textures),"syntax":syntax,"audio_played":False,"scope":"Offline score, aperture and syntax checks. Unity/Udon compilation and integrated visual/headset validation remain with the parent."}
    TMP.mkdir(parents=True,exist_ok=True);(TMP/"offline-checks.json").write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps(result,ensure_ascii=False,indent=2))
    if failures:raise SystemExit(1)


if __name__=="__main__":main()
