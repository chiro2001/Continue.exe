#!/usr/bin/env python3
"""Copy owned v2 sources and external images; import/settings stay in Pipeline."""
from pathlib import Path
import json,subprocess,sys
from PIL import Image
sys.path.insert(0,str(Path(__file__).resolve().parent))
import unity_remote as remote
ROOT=Path(__file__).resolve().parent.parent
TMP=Path.home()/'tmp/vrc-old-web-map/episode123-v2/image-import';TMP.mkdir(parents=True,exist_ok=True)
base='D:/Projects/VRChatProjects2/vrc-old-web-map';asset='Assets/OldWebMap/Episode123/Revision2/Images'
folders=[asset,'AgentScripts/data']
assert remote.run_pwsh('; '.join('New-Item -ItemType Directory -Force -Path '+remote.pwsh_literal(base+'/'+p)+' | Out-Null' for p in folders)).returncode==0
def copy(paths,target):
    if paths:subprocess.run(['scp','-q',*[str(p) for p in paths],'gpu5080:'+base+'/'+target+'/'],check=True)
if '--images-only' not in sys.argv:
    copy([ROOT/'unity/Assets/OldWebMap/Scripts'/f'{name}.cs' for name in ['OWEpisodeDirector','OWEpisodeButton','OWEpisodeMontage','OWEpisodeCaption']],'Assets/OldWebMap/Scripts')
    copy([ROOT/'unity/AgentScripts/OWEpisodeRevisionBuild.cs'],'AgentScripts')
copy([ROOT/'data'/f for f in ['episode123-v2-montage.json','episode123-v2-spectrum.json','episode123-historical-images.json']],'AgentScripts/data')
if '--scripts-only' not in sys.argv:
    rows=json.loads((ROOT/'data/episode123-v2-montage.json').read_text())['images'];paths=[]
    for row in rows:
        source=Path(row['path']);source=source if source.is_absolute() else ROOT/source
        output=TMP/(row['id']+'.png');Image.open(source).convert('RGBA').save(output);paths.append(output)
    output=TMP/'edge-grid.png';Image.open(ROOT/'art/export/episode123-v2/edge-grid.png').save(output);paths.append(output)
    copy(paths,asset)
print('Owned v2 batch copied; resume import and run Pipeline import/build checks.')
