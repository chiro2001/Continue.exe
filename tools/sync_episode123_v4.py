#!/usr/bin/env python3
"""Copy owned V4 source as one batch; Pipeline imports and builds assets."""
from pathlib import Path
import subprocess,sys,json
ROOT=Path(__file__).resolve().parent.parent
BASE='gpu5080:D:/Projects/VRChatProjects2/vrc-old-web-map/'
def copy(files,destination):
    if files:subprocess.run(['scp','-q',*[str(p) for p in files],BASE+destination+'/'],check=True)
def main():
    if '--images-only' not in sys.argv and '--anonymous-only' not in sys.argv:
        copy(list((ROOT/'unity/AgentScripts').glob('OWEpisodeV4*.cs')),'AgentScripts')
        copy([p for p in (ROOT/'data').glob('episode123-v4-*.json') if p.name not in ['episode123-v4-production.json']],'AgentScripts/data')
        copy(list((ROOT/'unity/Assets/OldWebMap/Shaders').glob('OWV4*.shader')),'Assets/OldWebMap/Shaders')
        copy(list((ROOT/'unity/Assets/OldWebMap/Scripts').glob('OWV4*.cs'))+[ROOT/'unity/Assets/OldWebMap/Scripts/OWEpisodeDirector.cs'],'Assets/OldWebMap/Scripts')
    if '--scripts-only' not in sys.argv:
        data=json.loads((ROOT/'data/episode123-v4-score.json').read_text());files=[]
        from PIL import Image
        out=Path.home()/'tmp/vrc-old-web-map/episode123-v4/image-import';out.mkdir(exist_ok=True,parents=True)
        for r in data['images']:
            if '--anonymous-only' in sys.argv and not r.get('display_path'):continue
            path=r.get('display_path',r['path'])
            if '/Revision4/Images/' not in path:continue
            dest=out/Path(path).name;im=Image.open(r.get('display_source_path',r['source_path'])).convert('RGB');im.thumbnail((1600,1600));im.save(dest);files.append(dest)
        for at in range(0,len(files),60):copy(files[at:at+60],'Assets/OldWebMap/Episode123/Revision4/Images')
    print('V4 text/source batch copied; Pipeline import/validation still required',flush=True)
if __name__=='__main__':main()
