#!/usr/bin/env python3
"""Read-only proof of the exact V2 SDK scene transition; never rewrite Unity YAML."""
import hashlib
import json
from pathlib import Path
import re

TMP=Path.home()/"tmp/vrc-old-web-map/fullfilm-v2"


def main():
    read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
    sdk=read(TMP/"reports/sdk-build.json")
    proof=read(TMP/"reports/sdk-dynamic-materials.json")
    raw={}
    for role in ("input","output"):
        digest=sdk[role+"SceneSha256"]
        data=(TMP/"scene-snapshots"/("sdk-"+role+"-"+digest+".unity")).read_bytes()
        if hashlib.sha256(data).hexdigest()!=digest:raise ValueError("Changed SDK snapshot")
        raw[role]=data
    pattern=rb"(?m)^  DynamicMaterials:\r?\n(?:  - \{fileID: 2100000, guid: [0-9a-f]{32}, type: 2\}\r?\n)+"
    blocks={}
    for role,data in raw.items():
        matches=list(re.finditer(pattern,data))
        if len(matches)!=1:raise ValueError("Expected one strict external Material list")
        blocks[role]=matches[0]
    a,b=blocks["input"],blocks["output"]
    if raw["input"][:a.start()]+b.group()+raw["input"][a.end():]!=raw["output"]:
        raise ValueError("SDK changed bytes outside DynamicMaterials")
    refs=lambda block:re.findall(rb"guid: ([0-9a-f]{32})",block.group())
    before=[s.decode() for s in refs(a)];after=[s.decode() for s in refs(b)]
    actual={m["guid"]:m for m in proof["materials"]}
    if len(actual)!=len(after) or set(actual)!=set(after) or len(set(after))!=len(after):raise ValueError("Descriptor proof does not match output")
    if proof["sourceSceneSha256"]!=sdk["outputSceneSha256"]:raise ValueError("Proof is for a different saved scene")
    mapping=[]
    for guid in before:
        match=[m for key,m in actual.items() if key==guid or m["name"]=="Inherited-"+guid]
        if len(match)!=1:raise ValueError("Old reference not resolved to an original or its isolated V2 material: "+guid)
        mapping.append(dict(inputGuid=guid,outputGuid=match[0]["guid"],outputPath=match[0]["path"]))
    mapped={m["outputGuid"] for m in mapping}
    added=[m for key,m in actual.items() if key not in mapped]
    if {m["name"] for m in added}!={"Window physical stars","Window physical copper bars","Window recess contours"}:raise ValueError("Unexpected additional dynamic materials")
    result=dict(success=True,kind="SDK discovers V2 isolated materials and three opening depth materials; only DynamicMaterials changes",inputSceneSha256=sdk["inputSceneSha256"],outputSceneSha256=sdk["outputSceneSha256"],inputReferences=len(before),outputReferences=len(after),allBytesOutsideOnlyListIdentical=True,sourceReferenceMapping=mapping,addedMaterials=added,actualDescriptorMatchesOutput=True,originalSnapshotHashesPreserved=True,scope="Raw-byte replacement proof and actual SDK-output descriptor; no scene content rewritten")
    (TMP/"reports/sdk-scene-transition.json").write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps({k:v for k,v in result.items() if k not in ("sourceReferenceMapping","addedMaterials")},ensure_ascii=False))


if __name__=="__main__":main()
