#!/usr/bin/env python3
"""Build a private local source/credit appendix from existing project files only.

No network, audio playback, Unity operations, or publication. Historical records,
original UI, and derivative previews are counted separately. Existing anonymous
display copies take precedence and are never replaced by unredacted originals.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import html
import json
import re
import shutil
from pathlib import Path
from urllib.parse import urlparse

from PIL import Image,ImageOps

ROOT=Path(__file__).resolve().parents[1]
TMP=Path.home()/"tmp/vrc-old-web-map"
DEFAULT_OUT=TMP/"fullfilm-v1/review/sources"
TITLES={
    # Transcribed from the existing local anonymous display images, not inferred
    # from an event description or fetched from the live websites.
    "vrc-anti-ripping-2024":"Developer Update - 25 July 2024",
    "vrc-ai-2026":"Regarding AI Usage on VRChat",
}
KINDS={
    "original-operation-reconstruction":"本项目原创操作复原（非同期原始截图）",
    "retrospective-article-image-date-needs-caption-check":"回顾文章配图；具体年代依现有图注记录",
    "museum-software-version-screenshot":"博物馆收录的软件版本界面截图",
    "official-documentation-screenshot":"官方文档截图",
    "official-steam-announcement-image":"Steam 官方公告配图",
    "official-2019-announcement-image":"2019 官方公告配图",
    "current-project-self-capture":"当前项目自采截图",
    "archival-client-screenshot":"归档客户端界面截图",
    "archived-product-ui-image":"归档产品界面图",
    "archived-webpage-render":"归档网页呈现",
    "original-explanatory-interface":"本项目原创说明界面（一般体验，非具体历史事件复刻）",
}


def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def esc(value): return html.escape(str(value),quote=True)
def resolve(value):
    path=Path(value).expanduser()
    if path.is_absolute(): return path
    return ROOT/("unity/"+value if value.startswith("Assets/") else value)


def select_image(record):
    display=[record.get(key) for key in ("display_source_path","display_path") if record.get(key)]
    for value in display:
        path=resolve(value)
        if path.is_file(): return path,"existing-display-copy"
    if display:
        return None,"指定的匿名/展示副本本地缺失；未回退未处理原图"
    for key in ("source_path","local_path"):
        if record.get(key):
            path=resolve(record[key])
            if path.is_file(): return path,"existing-source-image"
    return None,"本地源图缺失，仅保留原始记录"


def thumbnail(record,out):
    source,status=select_image(record)
    result=dict(source_available=source is not None,selection=status)
    if source is None: return result
    result.update(selected_path=str(source),selected_sha256=sha(source))
    if record.get("preview_mode")=="index-only":
        result["selection"]="数据图集仅列索引，不把通道数据冒充成片截图"
        return result
    name=result["selected_sha256"][:24]+".jpg"
    dest=out/"thumbs"/name
    try:
        with Image.open(source) as image:
            image=ImageOps.exif_transpose(image)
            if image.mode in ("RGBA","LA") or "transparency" in image.info:
                rgba=image.convert("RGBA");background=Image.new("RGBA",rgba.size,(15,23,32,255))
                image=Image.alpha_composite(background,rgba).convert("RGB")
            else: image=image.convert("RGB")
            result["source_size"]=list(image.size)
            image.thumbnail((480,300),Image.Resampling.LANCZOS)
            dest.parent.mkdir(parents=True,exist_ok=True)
            if not dest.exists(): image.save(dest,quality=82,optimize=True)
        result.update(thumbnail="thumbs/"+name,thumbnail_sha256=sha(dest))
    except (OSError,ValueError) as error:
        result["selection"]="本地文件存在但无法读取为图片；仅列记录"
        result["image_error"]=str(error)
    return result


def merge_sources(score,anchors):
    records=[];by_id={}
    for row in score["images"]:
        item=dict(row);item["collections"]=["episode123-v4-score"]
        records.append(item);by_id[item["id"]]=item
    if len(by_id)!=len(records): raise ValueError("基础素材表存在重复ID，不能静默增加计数")
    reused=[];new=[]
    for row in anchors["assets"]:
        if row["id"] in by_id:
            target=by_id[row["id"]];target["collections"].append("fullfilm-historical-anchors")
            target["anchor_record"]=dict(row);reused.append(row["id"])
            # A later explicit display copy is safe to use without discarding the
            # richer original chronology fields or changing the source identity.
            for key in ("display_source_path","display_path","display_sha256","display_processing"):
                if row.get(key): target[key]=row[key]
        else:
            target=dict(row);target["collections"]=["fullfilm-historical-anchors"]
            records.append(target);by_id[target["id"]]=target;new.append(target["id"])
        if not target.get("title") and target["id"] in TITLES:
            target["title"]=TITLES[target["id"]]
            target["title_basis"]="从现有本地匿名展示图的可见页面标题逐字读取"
    for item in records:
        if not item.get("title"):
            item["title"]="未提供标题 · "+item["id"]
            item["title_basis"]="记录缺项，未编造标题"
    return records,reused,new


def original_ui_entries():
    entries=[]
    overload_path=ROOT/"data/fullfilm-overload.json"
    if overload_path.exists():
        data=read(overload_path)
        for asset in data.get("assets",[]):
            name=asset["file"]
            entries.append(dict(id="fullfilm-overload-"+Path(name).stem,
                title="过载中央界面图集" if "focus" in name else "过载外围界面图集",
                kind=asset.get("kind","original-explanatory-interface"),
                source_path="art/export/fullfilm/overload/"+name,
                source_url="data/fullfilm-overload.json",category="原创 UI · 过载",
                date_basis=data.get("failure_interface_note","本项目原创界面"),
                atlas_cells=int(asset.get("columns",1))*int(asset.get("rows",1)),
                authorship="本项目原创界面制作；不对应某次已发生事件的截图"))
    renewal=ROOT/"art/export/fullfilm/renewal"
    names={"tool":"层间 / 一个整理图层的小工具","subtitles":"让这句话，变得读得懂",
        "world":"星桥 / 我们做的一个小世界","share":"把问题说清楚，等一个回应",
        "newcomer":"我的第一个版本 v0.1","layer":"原创远景、桥面与前景图层"}
    for path in sorted(renewal.glob("*.png")):
        stem=path.stem;group=stem.split("-")[0]
        memory=stem=="memory-v5-archive"
        title="V5 128秒保存场面的既有渲染回认" if memory else names.get(group,path.name)
        if "-" in stem and not memory: title+=" · "+stem.rsplit("-",1)[-1]
        entries.append(dict(id="fullfilm-renewal-"+stem,title=title,
            kind="existing-rendered-scene-recall" if memory else "original-explanatory-interface",
            source_path=str(path.relative_to(ROOT)),source_url="data/fullfilm-renewal.json",
            category="既有演出帧回认" if memory else "原创 UI · 再兴",
            authorship="既有V5渲染帧复用，不增加历史素材条数" if memory else "本项目原创工作界面与图层",
            date_basis="既有演出渲染，不是历史界面原件" if memory else "原创界面，不指定为历史同期截图"))
    for ident,title,path,preview in [
        ("v5-edge-logo","EDGE 风格等待窗像素标识","art/export/episode123-v5/identity/cracktro-logo.png",None),
        ("v5-window-scroller","等待窗滚动字图集","art/export/episode123-v5/identity/window-scroller.png","index-only"),
        ("v5-greetz","V5 匿名劳动角色署名图集","art/export/episode123-v5/identity/greetz-atlas.png","index-only"),
        ("v5-tracker","真实 FC14 谱面的原创排版图集","art/export/episode123-v5/signal/tracker-atlas.png","index-only"),
        ("fullfilm-greetz","全片劳动角色、掉名与接续署名图集",str(TMP/"fullfilm-v1/signal/fullfilm-greetz-atlas.png"),"index-only"),
        ("fullfilm-credit-panel","演后音乐、档案与素材署名板","art/reference/fullfilm-anchors/fullfilm-credits-display.png",None),
    ]:
        entries.append(dict(id=ident,title=title,kind="original-explanatory-interface",source_path=path,
            category="原创 UI · 身份与信号",source_url="spec/53-fullfilm-signal.md",preview_mode=preview,
            authorship="本项目原创排版、场景视觉与着色器；音乐与史料署名另列",
            date_basis="本次制作，不是某年代的软件原始界面"))
    return entries


def local_reference(value,out):
    if not value or urlparse(value).scheme: return None
    path=resolve(value)
    try: relative=path.resolve().relative_to(ROOT)
    except ValueError: return None
    if not path.is_file() or path.suffix.lower() not in (".md",".json"): return None
    name="__".join(relative.parts);dest=out/"notes"/name
    dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
    return "notes/"+name


def link(value,label=None):
    if not value: return ""
    if urlparse(value).scheme not in ("http","https"): return esc(value)
    return f'<a href="{esc(value)}" target="_blank" rel="noreferrer noopener">{esc(label or value)}</a>'


def card(record):
    preview=record["preview"];thumb=preview.get("thumbnail")
    visual=f'<a href="{esc(thumb)}"><img loading="lazy" src="{esc(thumb)}" alt="{esc(record["title"])}"></a>' if thumb else f'<div class="missing">{esc(preview["selection"])}</div>'
    nature=KINDS.get(record.get("kind"),record.get("kind","未标注"))
    dates=[]
    for field,label in [("display_year","呈现年代"),("content_or_event_date","内容/事件日期"),
        ("source_date","来源日期"),("source_publication_date","来源发表日期"),
        ("capture_or_archive_date","截图/存档日期"),("source_page_date","来源页面日期")]:
        if record.get(field): dates.append(f'<dt>{label}</dt><dd>{esc(record[field])}</dd>')
    if not dates: dates.append('<dt>年代</dt><dd>原记录未标定；未补写</dd>')
    source=record.get("source_url") or record.get("source_page_url")
    if record.get("local_source_link"): source_html=f'<a href="{esc(record["local_source_link"])}">{esc(source)}</a>'
    else: source_html=link(source) if source else "未提供外部链接（当前项目自采或原创）"
    notes=[]
    for field,label in [("post_title","原页标题"),("real_context","来源语境"),("date_basis","日期依据"),("chronology_note","年代说明"),
        ("display_processing","展示处理"),("authorship","制作/署名"),("rights","原记录权利说明"),
        ("title_basis","标题依据"),("supports","支持范围"),("limits","限制")]:
        if record.get(field):
            value=record[field];value="；".join(map(str,value)) if isinstance(value,list) else str(value)
            notes.append(f'<p><b>{label}：</b>{esc(value)}</p>')
    if record.get("chronology_evidence"): notes.append('<p><b>年代证据：</b>'+link(record["chronology_evidence"])+"</p>")
    if record.get("atlas_cells"): notes.append(f'<p>图集内 {record["atlas_cells"]} 格状态预览，仍只计一个图集文件。</p>')
    search=" ".join(str(record.get(k,"")) for k in ("title","id","display_year","source_date","category","kind"))
    return f'<article class="source" data-search="{esc(search.lower())}">{visual}<div class="body"><h3>{esc(record["title"])}</h3><p class="kind">{esc(nature)}</p><dl>{"".join(dates)}</dl><p class="url">{source_html}</p><details><summary>依据、展示处理与记录</summary>{"".join(notes)}<p class="id">ID：{esc(record["id"])}<br>图像：{esc(preview.get("selected_path",record.get("source_path","缺失")))}<br>SHA256：{esc(preview.get("selected_sha256","本地文件不可用"))}</p></details></div></article>'


def music_facts(source):
    required=["256.0 秒","4 个通道","Rhino of Torment","Cytax","I.C.E. 2","曲名待进一步查档确认"]
    if not all(text in source for text in required): raise ValueError("audio/SOURCE.md 音乐事实已改变，需人工更新附录摘录")
    return dict(format="Future Composer 1.4（FC14，Amiga）",duration_seconds=256,
        channels=4,row_ms=80,orders=100,rows_per_order=32,note_on_events=2953,
        composer_credit="Rhino of Torment（既有反向工程报告署名）",
        origin_credit="移植项目注记原曲出自 Cytax 的 I.C.E. 2 磁盘杂志；曲名待进一步查档确认",
        required_credit="取自 2008 年 EDGE Keil 注册机内嵌模块",
        program_distribution="程序本体不随片提供；本附录不包含注册机程序下载或运行入口",
        module_sha256="2e901c5869a1bfa486baa0d521af03a691e416d7641ec2f243b04f736b91b0b6",
        evidence="audio/SOURCE.md",evidence_sha256=sha(ROOT/"audio/SOURCE.md"),
        sustained_note="空行可能持续前音；note-on 数量不等于正在发声的声部数。")


def build(out=DEFAULT_OUT,score_path=ROOT/"data/episode123-v4-score.json",anchors_path=ROOT/"data/fullfilm-historical-anchors.json"):
    score=read(score_path);anchors=read(anchors_path)
    records,reused,new=merge_sources(score,anchors);originals=original_ui_entries()
    out.mkdir(parents=True,exist_ok=True)
    for row in records+originals:
        row["preview"]=thumbnail(row,out)
        row["local_source_link"]=local_reference(row.get("source_url"),out)
    authored=[r for r in records if r.get("kind")=="original-operation-reconstruction"]
    historical=[r for r in records if r not in authored]
    anchor_records=[r for r in records if "fullfilm-historical-anchors" in r["collections"]]
    facts=music_facts((ROOT/"audio/SOURCE.md").read_text())
    counts=dict(base_score_records=len(score["images"]),base_original_operation_records=len(authored),
        historical_anchor_records=len(anchors["assets"]),reused_anchor_ids=reused,new_anchor_ids=new,
        unique_score_and_anchor_records=len(records),additional_original_ui_index_entries=len(originals),
        source_images_available=sum(r["preview"]["source_available"] for r in records),
        missing_source_images=[r["id"] for r in records if not r["preview"]["source_available"]],
        anonymous_display_copies=sum(r["preview"]["selection"]=="existing-display-copy" for r in records),
        thumbnails_not_counted_as_materials=True,atlas_cells_not_counted_as_source_records=True)
    manifest=dict(schema="oldwebmap.fullfilm.sources.1",success=True,kind="Local private source/credit appendix; no network or playback",
        inputs=[dict(path=str(p),sha256=sha(p)) for p in [score_path,anchors_path,ROOT/"audio/SOURCE.md"]],
        counts=counts,records=records,additional_original_ui=originals,music=facts,
        claims=dict(network_used=False,audio_played=False,published=False,downloadable_programs=False,
            crop_variants_not_added_to_source_count=True,unknown_titles_or_dates_not_invented=True))
    (out/"source-manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n")
    groups=[("anchors","后半真实史料与前半回认",anchor_records),
        ("historical","史料、软件界面与项目自采索引",historical),
        ("authored","基础素材中的原创操作复原",authored),
        ("original-ui","后半与信号层原创 UI 索引",originals)]
    sections="".join(f'<details class="group" {"open" if key=="anchors" else ""} id="{key}"><summary>{esc(title)} · {len(items)} 条</summary><div class="grid">{"".join(card(r) for r in items)}</div></details>' for key,title,items in groups)
    css="""*{box-sizing:border-box}body{margin:0;background:#101820;color:#e2eaf0;font:15px/1.65 system-ui,sans-serif}main{max-width:1400px;margin:auto;padding:28px 24px 60px}h1{font-size:29px;margin-bottom:8px}h2{font-size:22px}h3{font-size:17px;margin:0 0 8px}a{color:#a1d9e5;overflow-wrap:anywhere}p{color:#bdcbd5}nav{display:flex;gap:18px;flex-wrap:wrap}.note,.music{padding:16px 20px;background:#1c2b37;border-left:3px solid #8eb8c9}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px;margin-top:16px}.source{border:1px solid #334653;border-radius:5px;overflow:hidden;background:#18242e}.source img{display:block;width:100%;height:210px;object-fit:contain;background:#0a1117}.body{padding:15px}.kind{font-size:13px;color:#a6c2d1}.url{font-size:12px}.missing{height:160px;display:grid;place-content:center;padding:20px;color:#cbb99a;background:#1b2028}dl{font-size:13px;margin:10px 0}dt{font-weight:600;color:#b2c8d4}dd{margin:0 0 7px}.source details{font-size:12px}.id{font-family:monospace;overflow-wrap:anywhere;color:#9fb4c2}.group{margin-top:30px}.group>summary{cursor:pointer;font-size:22px}input{width:100%;padding:12px;border:1px solid #4a6576;background:#162530;color:#eef4f7;border-radius:5px;font:inherit;margin:24px 0 8px}.hidden{display:none!important}.music li{margin:7px 0}small{color:#9eb2c0}"""
    page=f"""<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>世界地图 · 素材与署名附录</title><style>{css}</style></head><body><main>
<nav><a href="../index.html">返回完整版本验收页</a><a href="source-manifest.json">机器可读来源清单</a><a href="#music">音乐事实与署名</a><a href="#authored">原创操作复原</a><a href="#original-ui">后半原创 UI</a></nav>
<h1>素材与署名</h1><p>私有验收用本地追溯附录。基础素材 {counts['base_score_records']} 条，其中 {counts['base_original_operation_records']} 条为本项目原创操作复原。后半 {counts['historical_anchor_records']} 条史料中，{len(reused)} 条复用基础素材、{len(new)} 条新增；合并后 {len(records)} 个独立素材 ID。</p>
<p class="note">同一素材的匿名展示副本、缩略图、图集格子和多次登场不增加素材计数。年代、来源发表日与截图/存档时间按原记录分别列出；原记录不足时明确留空。优先使用既有匿名展示图，指定展示图缺失时不回退未处理原图。本地源图可见 {counts['source_images_available']} / {len(records)}。</p>
<section class="music" id="music"><h2>音乐事实与署名</h2><ul><li>格式：{esc(facts['format'])}；完整一轮 256 秒，4 条通道，100×32 行，每行80 ms。</li><li>作曲署名：{esc(facts['composer_credit'])}。</li><li>{esc(facts['origin_credit'])}。</li><li>注明：{esc(facts['required_credit'])}。视觉语汇致意 EDGE 与 demoscene。</li><li>{esc(facts['sustained_note'])}</li><li>{esc(facts['program_distribution'])}。</li></ul><p><small>事实摘录自现有 audio/SOURCE.md；文档 SHA256：{facts['evidence_sha256']}。模块 SHA256：{facts['module_sha256']}。本工具没有新增联网核验、播放或发布。</small></p></section>
<input id="search" type="search" placeholder="按标题、年代、来源类型或 ID 搜索" aria-label="筛选素材"><p id="result" aria-live="polite"></p>
{sections}
</main><script>const search=document.getElementById('search');const cards=[...document.querySelectorAll('.source')];search.addEventListener('input',()=>{{const q=search.value.trim().toLowerCase();for(const card of cards)card.classList.toggle('hidden',!card.dataset.search.includes(q));if(q)for(const group of document.querySelectorAll('.group'))group.open=true;document.getElementById('result').textContent=q?'当前显示 '+cards.filter(c=>!c.classList.contains('hidden')).length+' 个索引卡片（同一素材可在回认分组再次列示，不增加独立计数）':'';}});</script></body></html>"""
    (out/"index.html").write_text(page,encoding="utf-8")
    print(json.dumps(dict(success=True,index=str(out/"index.html"),manifest=str(out/"source-manifest.json"),counts=counts),ensure_ascii=False,indent=2))
    return manifest


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,default=DEFAULT_OUT)
    parser.add_argument("--score",type=Path,default=ROOT/"data/episode123-v4-score.json")
    parser.add_argument("--anchors",type=Path,default=ROOT/"data/fullfilm-historical-anchors.json")
    args=parser.parse_args();build(args.output,args.score,args.anchors)


if __name__=="__main__": main()
