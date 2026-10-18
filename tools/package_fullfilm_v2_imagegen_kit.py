#!/usr/bin/env python3
"""Package the eight approved manual prompts and their existing reference image."""
import hashlib
import html
import json
from pathlib import Path
import re
import shutil
import zipfile

ROOT=Path(__file__).resolve().parents[1]
OUT=Path.home()/"tmp/vrc-old-web-map/fullfilm-v2/imagegen-kit"
REF=ROOT/"art/models/fullfilm-v2/fullfilm_v2_scene_block_preview.png"


def main():
    document=(ROOT/"art/prompts/fullfilm-v2-chatgpt-prompts.md").read_text()
    manifest=json.loads((ROOT/"data/fullfilm-v2-prompt-assets.json").read_text())
    for folder in (OUT,OUT/"prompts",OUT/"reference"):
        folder.mkdir(parents=True,exist_ok=True)
    reference=OUT/"reference/11-scene-block-model.png"
    shutil.copy2(REF,reference)
    entries=[]
    for item in manifest['items']:
        number=item['filename'].split('-')[1]
        match=re.search(r'^## '+number+r'\. (.*?)(?=^## |\Z)',document,re.M|re.S)
        if not match:raise ValueError('Missing prompt '+number)
        prompts=re.findall(r'~~~text\n(.*?)\n~~~',match[0],re.S)
        if len(prompts)!=1:raise ValueError('Expected one copyable prompt '+number)
        path=OUT/'prompts'/(item['id']+'.txt')
        path.write_text(prompts[0].strip()+'\n')
        upload='无需上传参考图'
        if number=='05':upload='上传你本轮生成的 v2-04-comic-translation-pair.png（先完成04）'
        if number=='11':upload='上传 reference/11-scene-block-model.png'
        entries.append(dict(number=number,title=item['prompt_section'].split('. ',1)[1],prompt='prompts/'+path.name,output=item['filename'],upload=upload,text=prompts[0]))
    lines=['# 手动生图配套包','', '共8张。编号沿用现有素材清单04—11；先做04，再上传它制作05。其余可独立生成。', '', 'prompt正文见prompts/里的TXT。唯一预先提供的参考图是reference/11-scene-block-model.png；05的参考图须由你先生成04。', '', '生成结果保存到：`C:\\Users\\Chiro\\tmp\\vrc-old-web-map\\fullfilm-v2\\user-images\\`。按各项输出文件名保存，下载原图。', '', '| 编号 | 内容与prompt | 上传图片 | 输出文件名 |','| --- | --- | --- | --- |']
    for e in entries:lines.append(f"| {e['number']} | [{e['title']}]({e['prompt']}) | {e['upload']} | `{e['output']}` |")
    (OUT/'README.md').write_text('\n'.join(lines)+'\n')
    shutil.copy2(ROOT/'art/prompts/fullfilm-v2-chatgpt-prompts.md',OUT/'all-prompts.md')
    esc=html.escape
    cards=[]
    for e in entries:
        illustration='<a href="reference/11-scene-block-model.png" download><img src="reference/11-scene-block-model.png" alt="11号需要上传的实际模型参考图"></a>' if e['number']=='11' else ''
        cards.append(f'<section id="p{e["number"]}"><h2>{e["number"]} · {esc(e["title"])}</h2><p>参考图：{esc(e["upload"])}</p>{illustration}<p>保存为：<code>{esc(e["output"])}</code></p><p><a href="{e["prompt"]}">打开纯文本prompt</a> · <button onclick="copyPrompt(this)">复制prompt</button></p><textarea readonly>{esc(e["text"])}</textarea></section>')
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>V2 手动生图 · Prompt与参考图</title><style>body{max-width:1050px;margin:auto;padding:24px;background:#151b24;color:#e6ebf2;font:16px/1.6 system-ui,sans-serif}a{color:#a9d4ef}section{margin:35px 0;padding:20px;background:#202b39;border-radius:8px}img{width:100%;max-width:700px;display:block}textarea{width:100%;height:340px;box-sizing:border-box;padding:14px;background:#111821;color:#e6ebf2;border:1px solid #667b90;line-height:1.7;font-size:15px}code{overflow-wrap:anywhere}button{padding:7px 12px;cursor:pointer}</style><h1>8张手动生图素材</h1><p>先做04，再把04成图上传制作05。11上传配套模型图；其余不需要参考图。每项复制一条prompt，下载结果按指定文件名保存。</p><p>保存目录：<code>C:\\Users\\Chiro\\tmp\\vrc-old-web-map\\fullfilm-v2\\user-images\\</code></p>'''+''.join(cards)+'''<script>function copyPrompt(button){const area=button.closest('section').querySelector('textarea');area.focus();area.select();const ok=document.execCommand('copy');button.textContent=ok?'已复制':'请按 Ctrl+C 复制';}</script></html>'''
    (OUT/'index.html').write_text(page)
    result=dict(count=len(entries),entries=[{k:v for k,v in e.items() if k!='text'} for e in entries],reference_source=str(REF),reference_sha256=hashlib.sha256(reference.read_bytes()).hexdigest())
    (OUT/'manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    target=OUT.with_suffix('.zip')
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as archive:
        for file in sorted(OUT.rglob('*')):
            if file.is_file():archive.write(file,file.relative_to(OUT.parent))
    print(json.dumps(dict(success=True,count=len(entries),index=str(OUT/'index.html'),zip=str(target),reference_identical=reference.read_bytes()==REF.read_bytes()),ensure_ascii=False))


if __name__=='__main__':main()
