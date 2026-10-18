#!/usr/bin/env python3
"""Read-only SDK scene-difference proof from preserved input/output snapshots."""
from pathlib import Path
import difflib
import hashlib
import json
import re

TMP=Path.home()/'tmp/vrc-old-web-map/fullfilm-v5'

def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def property_block(blob,key):
    matches=list(re.finditer(rb'(?m)^  '+key.encode()+rb':[^\r\n]*(?:\r?\n|$)',blob))
    if len(matches)!=1:raise ValueError('Expected one descriptor property '+key)
    start=matches[0].start();end=matches[0].end()
    for line in blob[end:].splitlines(keepends=True):
        if line.startswith((b'    ',b'  - ')):end+=len(line)
        else:break
    return start,end,blob[start:end]

def main():
    sdk=read(TMP/'reports/sdk-build.json');proof=read(TMP/'reports/sdk-dynamic-materials.json');raw={};blocks={}
    for role in ('input','output'):
        digest=sdk[role+'SceneSha256'];blob=(TMP/'scene-snapshots'/f'sdk-{role}-{digest}.unity').read_bytes()
        if hashlib.sha256(blob).hexdigest()!=digest:raise ValueError('Snapshot hash mismatch')
        raw[role]=blob;blocks[role]={}
        for key in ('DynamicMaterials','DynamicPrefabs'):
            if re.search(rb'(?m)^  '+key.encode()+rb':',blob):blocks[role][key]=property_block(blob,key)
    reduced={}
    for role,blob in raw.items():
        for key,(a,b,block) in sorted(blocks[role].items(),key=lambda kv:kv[1][0],reverse=True):blob=blob[:a]+b'  '+key.encode()+b': <SDK discovery list>\n'+blob[b:]
        reduced[role]=blob
    same=reduced['input']==reduced['output']
    if not same:
        diff=''.join(difflib.unified_diff(reduced['input'].decode('utf-8-sig').splitlines(True),reduced['output'].decode('utf-8-sig').splitlines(True),fromfile='SDK input',tofile='SDK output'))
        (TMP/'reports/sdk-unexpected-scene-change.diff').write_text(diff)
        raise ValueError('SDK changed bytes beyond discovered material/prefab lists; inspect sdk-unexpected-scene-change.diff')
    refs=lambda role,key:[g.decode() for g in re.findall(rb'guid: ([0-9a-f]{32})',blocks[role].get(key,(0,0,b''))[2])]
    output_refs=refs('output','DynamicMaterials');actual=[r['guid'] for r in proof['materials']]
    if len(output_refs)!=len(actual) or set(output_refs)!=set(actual) or proof['sourceSceneSha256']!=sdk['outputSceneSha256']:raise ValueError('Actual descriptor does not match SDK-output snapshot')
    changed=[key for key in ('DynamicMaterials','DynamicPrefabs') if blocks['input'].get(key,(0,0,b''))[2]!=blocks['output'].get(key,(0,0,b''))[2]]
    result={'success':True,'kind':'SDK actual descriptor discovery lists only; every byte outside these lists unchanged','inputSceneSha256':sdk['inputSceneSha256'],'outputSceneSha256':sdk['outputSceneSha256'],'allBytesOutsideOnlyListsIdentical':same,'changedProperties':changed,'inputMaterialReferences':len(refs('input','DynamicMaterials')),'outputMaterialReferences':len(output_refs),'actualDescriptorMatchesOutput':True,'inputSnapshotPreserved':True,'outputSnapshotPreserved':True,'sceneRewrittenByVerifier':False}
    (TMP/'reports/sdk-scene-transition.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps(result,ensure_ascii=False))

if __name__=='__main__':main()
