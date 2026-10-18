#!/usr/bin/env python3
"""Build the local full-film review page from completed evidence; never open/play/upload it."""
from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import shutil
import subprocess
from collections import Counter
from pathlib import Path

from PIL import Image
import encode_fullfilm_movie as movie_tools
import package_fullfilm_sources as source_appendix

ROOT=Path(__file__).resolve().parents[1]
TMP=Path.home()/"tmp/vrc-old-web-map/fullfilm-v1"
V5=Path.home()/"tmp/vrc-old-web-map/episode123-v5"
FILENAME="fullfilm-v1-review.mp4"
SDK_NAME="OWM_FullFilm_v1.vrcw"
SCENE_PATH="Assets/Scenes/OWM_FullFilm_v1.unity"
REQUIRED=["integration","static-validation","live-validation","interaction","saved","persistence",
    "sdk-build","sdk-bundle-inspection","signal-validate","overload-validation","renewal-validation",
    "windows-output-muted","material-isolation","final-editor-state","full-decode","sdk-dynamic-materials"]
LABELS={"integration":"全片集成与事件配置","static-validation":"24 场面静态与旅行检查",
    "live-validation":"完整 Udon 连续运行","interaction":"开始、结束、重播与迟到恢复",
    "saved":"场景保存","persistence":"保存重开与 V5 场景文件保护","sdk-build":"本地 SDK 构建",
    "sdk-bundle-inspection":"SDK 包内容检查","signal-validate":"信号、声源、气氛与署名",
    "overload-validation":"过载与骤空场面","renewal-validation":"再兴、接力与继续",
    "encode-report":"参考片编码校验","audio-reference":"完整音乐与拟音参考",
    "movie-progress":"完整 6450 帧生成记录","windows-output-muted":"Windows 默认输出静音检查",
    "material-isolation":"可变旧材质隔离、属性等同与绑定检查",
    "final-editor-state":"最终停止、保存与编译状态","full-decode":"成片双流完整解码检查（无播放）",
    "sdk-dynamic-materials":"SDK 3.10.5 真实 Descriptor 与 13 项材质引用",
    "sdk-scene-transition":"SDK 输入/输出场景逐字节转换核验",
    "sdk-scene-diff":"前轮 SDK 诊断记录（不作本次转换证明）"}
COMPARE_TIMES=[.96,55.36,85.12,87.28,89.60,129.28]


def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def escape(value): return html.escape(str(value),quote=True)


def dynamic_materials_block(raw):
    """Locate the one exact Unity serialization block without decoding/re-emitting YAML.

    The narrow accepted grammar is the actual SDK 3.10.5 scene form: exactly
    thirteen consecutive external Material reference lines and then a new field.
    Anything else fails closed, including extra entries, whitespace or EOL edits.
    """
    keys=list(re.finditer(rb"(?m)^[ \t]*DynamicMaterials:[^\r\n]*(?:\r?\n|$)",raw))
    if len(keys)!=1: raise ValueError("场景必须恰有一个 DynamicMaterials 序列化列表")
    key=keys[0]
    if re.fullmatch(rb"  DynamicMaterials:\r?\n",key.group()) is None:
        raise ValueError("DynamicMaterials 列表头不是本次 SDK 3.10.5 的严格序列化形式")
    owners=list(re.finditer(rb"(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?\n",raw[:key.start()]))
    if not owners or owners[-1].group(1)!=b"114":
        raise ValueError("DynamicMaterials 不属于明确的 MonoBehaviour 场景记录")
    owner=owners[-1]
    if re.match(rb"MonoBehaviour:\r?\n",raw[owner.end():]) is None:
        raise ValueError("DynamicMaterials 所属记录不是 MonoBehaviour")
    start=key.end();end=start;lines=[];refs=[]
    pattern=rb"  - \{fileID: (2100000), guid: ([0-9a-fA-F]{32}), type: (2)\}(\r?\n)"
    for index in range(13):
        stop=raw.find(b"\n",end)
        if stop<0: raise ValueError("DynamicMaterials 引用行必须保留原始换行")
        line=raw[end:stop+1];match=re.fullmatch(pattern,line)
        if match is None: raise ValueError(f"DynamicMaterials 第 {index+1} 行不是严格的外部 Material 引用")
        lines.append(line);refs.append(dict(fileID=int(match[1]),guid=match[2].decode("ascii").lower(),type=int(match[3])))
        end=stop+1
    stop=raw.find(b"\n",end);following=raw[end:] if stop<0 else raw[end:stop+1]
    if re.fullmatch(rb"  [A-Za-z_][A-Za-z0-9_]*:[^\r\n]*(?:\r?\n|$)",following) is None:
        raise ValueError("DynamicMaterials 必须仅有 13 行引用，后面立即进入下一序列化字段")
    return dict(start=start,end=end,payload=raw[start:end],lines=lines,references=refs,
        descriptor_local_file_id=owner[2].decode("ascii"),header_offset=key.start())


def compare_dynamic_materials_bytes(before,after):
    """The only allowed unequal-byte case is a permutation of those exact 13 raw lines."""
    first=dynamic_materials_block(before);second=dynamic_materials_block(after)
    if first["descriptor_local_file_id"]!=second["descriptor_local_file_id"]:
        raise ValueError("SDK 改变了 DynamicMaterials 所属 Descriptor 引用")
    if Counter(first["lines"])!=Counter(second["lines"]):
        raise ValueError("SDK 场景包含材质引用、格式或换行变化；不只是这 13 行的顺序变化")
    rebuilt=before[:first["start"]]+second["payload"]+before[first["end"]:]
    if rebuilt!=after:
        raise ValueError("SDK 场景在唯一 DynamicMaterials 列表之外存在原始字节差异")
    return first,second


def verify_sdk_scene_transition(base,sdk,proof):
    input_hash=movie_tools.checked_sha(sdk.get("inputSceneSha256"),"sdk-build.inputSceneSha256")
    output_hash=movie_tools.checked_sha(sdk.get("outputSceneSha256"),"sdk-build.outputSceneSha256")
    sdk_hash=movie_tools.checked_sha(sdk.get("sha256"),"sdk-build.sha256")
    paths={role:Path("scene-snapshots")/f"sdk-{role}-{digest}.unity"
        for role,digest in (("input",input_hash),("output",output_hash))}
    snapshots={};raw={}
    for role,digest in (("input",input_hash),("output",output_hash)):
        source=base/paths[role]
        if not source.is_file(): raise ValueError("缺少 SDK 原始场景快照："+str(source))
        raw[role]=source.read_bytes();actual=hashlib.sha256(raw[role]).hexdigest()
        if actual!=digest: raise ValueError("SDK 原始快照真实 SHA256 与文件名/构建报告不一致："+role)
        snapshots[role]=dict(path=paths[role].as_posix(),sha256=actual,bytes=len(raw[role]))
    first,second=compare_dynamic_materials_bytes(raw["input"],raw["output"])
    if (proof.get("success") is not True
            or movie_tools.checked_sha(proof.get("sourceSceneSha256"),"sdk-dynamic-materials.sourceSceneSha256")!=output_hash):
        raise ValueError("真实材质检查报告未绑定本次 SDK 输出场景")
    if proof.get("descriptorType")!="VRC.SDK3.Components.VRCSceneDescriptor":
        raise ValueError("实际 Descriptor 类型必须是 VRC.SDK3.Components.VRCSceneDescriptor")
    if (proof.get("worldsVersion")!="3.10.5" or proof.get("baseVersion")!="3.10.5"):
        raise ValueError("本次已核验的转换仅适用于实际 Worlds/Base SDK 3.10.5")
    if proof.get("descriptorLocalFileId")!=second["descriptor_local_file_id"]:
        raise ValueError("实际 Descriptor localFileID 与原始快照不一致")
    materials=proof.get("materials")
    if not isinstance(materials,list) or len(materials)!=13 or proof.get("uniqueNames") is not True:
        raise ValueError("必须提供实际解析的 13 项唯一 GUID/name 材质结果")
    guids=[];names=[];asset_paths=[]
    for material in materials:
        if not isinstance(material,dict): raise ValueError("材质检查记录格式错误")
        guid=material.get("guid");name=material.get("name");path=material.get("path")
        if not isinstance(guid,str) or re.fullmatch(r"[0-9a-fA-F]{32}",guid) is None:
            raise ValueError("实际材质 GUID 无效")
        if not isinstance(name,str) or not name.strip(): raise ValueError("实际材质 name 不能为空")
        if not isinstance(path,str) or not path.startswith("Assets/"): raise ValueError("实际材质必须有持久 Assets 路径")
        guids.append(guid.lower());names.append(name);asset_paths.append(path)
    if len(set(guids))!=13 or len(set(names))!=13 or len(set(asset_paths))!=13:
        raise ValueError("13 项实际材质 GUID、name、path 必须各自唯一")
    ordered_input=[row["guid"] for row in first["references"]]
    ordered_output=[row["guid"] for row in second["references"]]
    if Counter(guids)!=Counter(ordered_input) or Counter(guids)!=Counter(ordered_output):
        raise ValueError("实际材质 GUID 集合与 SDK 输入/输出原始快照不一致")
    proof_path=base/"reports/sdk-dynamic-materials.json"
    if not proof_path.is_file() or read(proof_path)!=proof:
        raise ValueError("真实材质报告缺失或在核验过程中被改动")
    permutation=[ordered_input.index(guid) for guid in ordered_output]
    return dict(success=True,schema="oldwebmap.sdk.scene-transition.1",
        kind="byte-identical" if input_hash==output_hash else "dynamic-materials-order-only",
        inputSceneSha256=input_hash,outputSceneSha256=output_hash,sdkSha256=sdk_hash,
        targetSdkVersion="3.10.5",worldsVersion=proof["worldsVersion"],baseVersion=proof["baseVersion"],
        descriptorType=proof["descriptorType"],descriptorLocalFileId=proof["descriptorLocalFileId"],
        snapshots=snapshots,referenceCount=13,inputReferences=first["references"],outputReferences=second["references"],
        outputPermutationOfInput=permutation,changedOrderPositions=sum(i!=j for i,j in enumerate(permutation)),
        rawReferenceLineMultisetIdentical=True,allBytesOutsideOnlyListIdentical=True,
        replacementProducesExactOutputBytes=True,lineEndingsPreserved=True,
        inputListByteRange=[first["start"],first["end"]],outputListByteRange=[second["start"],second["end"]],
        unchangedOutsideListBytes=len(raw["input"])-len(first["payload"]),
        materials=materials,materialGuidsUnique=True,materialNamesUnique=True,
        materialProof=dict(path="reports/sdk-dynamic-materials.json",sha256=sha(proof_path)),
        validator=dict(path="tools/package_fullfilm_review.py",sha256=sha(Path(__file__).resolve()),
            method="Verify original snapshot SHA256; accept exactly one 13-reference DynamicMaterials list; compare exact raw-line multisets; replace only that list and require every resulting raw byte to equal SDK output, including all line endings"),
        scope="Exact raw snapshot transition and read-only Descriptor/material/version proof; original SDK input/output hashes are retained, not rewritten")


def write_sdk_transition(base,transition):
    path=base/"reports/sdk-scene-transition.json";path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(transition,ensure_ascii=False,indent=2)+"\n")
    return path


def report_passed(name,report):
    if name=="windows-output-muted":
        endpoints=report.get("endpoints")
        return (report.get("all_default_outputs_muted") is True and isinstance(endpoints,list)
            and bool(endpoints) and all(isinstance(row,dict) and row.get("muted") is True for row in endpoints)
            and {row.get("role") for row in endpoints}.issuperset({0,1,2}))
    return report.get("success") is True


def validate_reports(base):
    reports={};errors=[]
    for name in REQUIRED:
        path=base/"reports"/(name+".json")
        if not path.exists(): errors.append(name+"：缺少报告");continue
        report=read(path);reports[name]=report
        if not report_passed(name,report): errors.append(name+"：尚未通过")
    if errors: raise RuntimeError("完整验收证据尚未齐备："+"；".join(errors))
    saved,persistence=reports["saved"],reports["persistence"]
    if persistence.get("v5Preserved") is not True: raise RuntimeError("V5 字节保护尚未通过")
    saved_hash=movie_tools.checked_sha(saved.get("sha256"),"saved.sha256")
    v5_hash=movie_tools.checked_sha(saved.get("v5Sha256"),"saved.v5Sha256")
    if (saved_hash!=movie_tools.checked_sha(persistence.get("sceneSha256"),"persistence.sceneSha256")
            or v5_hash!=movie_tools.checked_sha(persistence.get("v5Sha256"),"persistence.v5Sha256")):
        raise RuntimeError("保存与重开报告引用不同的场景哈希")
    sdk=reports["sdk-build"];inspection=reports["sdk-bundle-inspection"];final=reports["final-editor-state"]
    if sdk.get("uploaded") is not False: raise RuntimeError("SDK 报告必须明确仅本地构建、未上传")
    if any(row.get("scene")!=SCENE_PATH for row in (saved,sdk,final)):
        raise RuntimeError("保存、SDK输入与最终编辑器状态须引用目标全片场景")
    movie_tools.checked_sha(sdk.get("inputSceneSha256"),"sdk-build.inputSceneSha256")
    if movie_tools.checked_sha(sdk.get("outputSceneSha256"),"sdk-build.outputSceneSha256")!=saved_hash:
        raise RuntimeError("SDK 实际输出场景与保存/重开/最终编辑器状态哈希不一致")
    sdk_hash=movie_tools.checked_sha(sdk.get("sha256"),"sdk-build.sha256")
    if movie_tools.checked_sha(inspection.get("sha256"),"sdk-bundle-inspection.sha256")!=sdk_hash:
        raise RuntimeError("SDK 包检查与构建报告指向不同工件")
    if (movie_tools.checked_sha(final.get("sceneSha256"),"final-editor-state.sceneSha256")!=saved_hash
            or movie_tools.checked_sha(final.get("v5Sha256"),"final-editor-state.v5Sha256")!=v5_hash):
        raise RuntimeError("最终编辑器状态与已保存场景哈希不一致")
    if (final.get("playStopped") is not True or final.get("sceneSaved") is not True
            or final.get("compiling") is not False or final.get("waiting") is not True
            or final.get("udonCompileErrors") is not False or final.get("shaderErrors")!=[]):
        raise RuntimeError("最终编辑器必须已停止、已保存、在等待态且无编译/着色器错误")
    if reports["material-isolation"].get("propertyEquivalence") is not True:
        raise RuntimeError("可变旧材质隔离报告须确认属性等同与绑定检查通过")
    decoded=reports["full-decode"]
    movie_tools.checked_sha(decoded.get("sha256"),"full-decode.sha256")
    if (type(decoded.get("returncode")) is not int or decoded["returncode"]!=0
            or "error" not in decoded or decoded["error"] not in (None,"") or decoded.get("played") is not False):
        raise RuntimeError("成片须完成无错误的双流解码到 null，且未播放")
    reports["sdk-scene-transition"]=verify_sdk_scene_transition(base,sdk,reports["sdk-dynamic-materials"])
    return reports


def probe_movie(path):
    result=json.loads(subprocess.check_output(["ffprobe","-v","error","-show_format","-show_streams","-of","json",str(path)]))
    video=[s for s in result["streams"] if s.get("codec_type")=="video"]
    audio=[s for s in result["streams"] if s.get("codec_type")=="audio"]
    if len(video)!=1 or len(audio)!=1: raise ValueError("参考片须包含一条视频与一条完整音频")
    v,a=video[0],audio[0]
    if (v.get("width"),v.get("height"),v.get("r_frame_rate"))!=(1600,900,"25/1"):
        raise ValueError("参考片必须为 1600×900 / 25 fps")
    frames=v.get("nb_frames")
    if frames in (None,"N/A"):
        count=json.loads(subprocess.check_output(["ffprobe","-v","error","-count_frames","-select_streams","v:0",
            "-show_entries","stream=nb_read_frames","-of","json",str(path)]))
        frames=count["streams"][0]["nb_read_frames"]
    if int(frames)!=6450 or abs(float(result["format"]["duration"])-258)>.04:
        raise ValueError("参考片必须为完整 6450 帧 / 258 秒")
    if int(a.get("sample_rate",0))!=48000 or int(a.get("channels",0))!=2:
        raise ValueError("参考片音频必须为 48 kHz 立体声")
    movie_tools.validate_audio_stream(a)
    return result


def frame_time(path,prefix):
    match=re.fullmatch(re.escape(prefix)+r"(\d+(?:\.\d+)?)",path.stem)
    return float(match[1]) if match else None


def frame_label(path):
    time=frame_time(path,"full_")
    if time is not None: return f"{time:.2f} 秒"
    return {"full_waiting":"等待连接","full_finished":"演后工作区与重播"}.get(path.stem,path.stem)


def copy_file(source,dest):
    dest.parent.mkdir(parents=True,exist_ok=True)
    if source.resolve()!=dest.resolve(): shutil.copy2(source,dest)


def image_card(url,label):
    return f'<a href="{escape(url)}"><img loading="lazy" src="{escape(url)}" alt="{escape(label)}"><span>{escape(label)}</span></a>'


def make_comparisons(frames,v5_root,out):
    old=list((v5_root/"frames").glob("v5_*.png"));pairs=[]
    for time in COMPARE_TIMES:
        full=next((p for p in frames if frame_time(p,"full_") is not None and abs(frame_time(p,"full_")-time)<.011),None)
        previous=next((p for p in old if frame_time(p,"v5_") is not None and abs(frame_time(p,"v5_")-time)<.011),None)
        if full is None or previous is None: continue
        with Image.open(previous) as p,Image.open(full) as q:
            a=p.convert("RGB").resize((800,450),Image.Resampling.LANCZOS)
            b=q.convert("RGB").resize((800,450),Image.Resampling.LANCZOS)
        sheet=Image.new("RGB",(1600,450));sheet.paste(a,(0,0));sheet.paste(b,(800,0))
        name=f"compare_{time:06.2f}.jpg";(out/"compare").mkdir(parents=True,exist_ok=True)
        sheet.save(out/"compare"/name,quality=88)
        pairs.append((time,name))
    return pairs


def render_page(presentation,reports,frames,runtime,pairs,report_names,audio,poster):
    scenes=presentation["scenes"];live=reports["live-validation"];integration=reports["integration"]
    transition=reports["sdk-scene-transition"]
    transition_text=("SDK 保存过程仅将唯一 DynamicMaterials 列表的 13 项材质引用重新排序；"
        "工具核验了原始引用行的多重集合，并只替换这唯一列表后逐字节比较，其他所有字节及换行完全一致。"
        if transition["kind"]=="dynamic-materials-order-only" else "SDK 输入与输出原始场景字节完全一致。")
    snapshot_links=" · ".join(f'<a href="{escape(item["path"])}" download>{label}原始快照</a>'
        for label,item in (("输入",transition["snapshots"]["input"]),("输出",transition["snapshots"]["output"])))
    chapters="".join(f'<button type="button" onclick="jump({float(s["start"])})"><time>{int(s["start"]//60):02d}:{int(s["start"]%60):02d}</time>{escape(s["title"])}</button>' for s in scenes)
    gallery="".join(image_card("stills/"+p.name,frame_label(p)) for p in frames)
    actual="".join(image_card("runtime/"+relative.as_posix(),relative.stem) for relative in runtime)
    comparison="".join(image_card("compare/"+name,f"{time:.2f} 秒 · 左 V5 / 右完整版本") for time,name in pairs)
    links="".join(f'<li><a href="reports/{escape(name)}.json">{escape(LABELS.get(name,name))}</a></li>' for name in report_names)
    counts=f'{len(live.get("scenes",[]))} / 24 场面、{live.get("eventsObserved","—")} / 580 个前半历史事件、{live.get("keylines","—")} 条关键句'
    css="""*{box-sizing:border-box}body{margin:0;background:#10171e;color:#e6edf3;font:16px/1.7 system-ui,sans-serif}main{max-width:1240px;margin:auto;padding:28px 24px 60px}h1{font-size:30px;font-weight:650;margin:0 0 8px}h2{font-size:22px;margin:32px 0 12px}p,li{color:#bdcbd7}a{color:#9cdae5}video{display:block;width:100%;background:#000;border:1px solid #304251;border-radius:6px}nav{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:7px;margin:18px 0}button{display:flex;gap:10px;text-align:left;border:1px solid #354654;background:#1b2b38;color:#e3edf4;padding:8px 10px;border-radius:4px;cursor:pointer;font:inherit;font-size:13px}button:hover{border-color:#88b7c9}time{color:#8cc0d1;font-variant-numeric:tabular-nums;white-space:nowrap}.lead{color:#9fb1bf;margin:0 0 20px}.notice{padding:14px 18px;background:#1b2a36;border-left:3px solid #83b6c5}.bounds{padding:14px 18px;background:#26252c;border-left:3px solid #c6ad92}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(290px,1fr));gap:16px}.grid.wide{grid-template-columns:1fr}.grid a{text-decoration:none}.grid img{display:block;width:100%;background:#060a0e}.grid span{display:block;font-size:13px;margin:4px 0;color:#a8bfcb}details{margin-top:28px}summary{font-size:21px;cursor:pointer}details>.grid,details>ul{margin-top:16px}.evidence{columns:2;column-gap:28px;padding-left:22px}.evidence li{break-inside:avoid;margin:6px 0}.downloads{display:flex;flex-wrap:wrap;gap:10px 22px;margin:16px 0}.downloads a{white-space:nowrap}small{color:#9eafbc}@media(max-width:650px){main{padding:18px 14px}.evidence{columns:1}h1{font-size:25px}}"""
    comparison_section=f'<details><summary>与 V5 的同刻对照（{len(pairs)} 组）</summary><div class="grid wide">{comparison}</div></details>' if pairs else ""
    return f"""<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Continue.exe · 256 秒完整版本验收</title><style>{css}</style></head><body><main>
<h1>Continue.exe · 完整版本</h1><p class="lead">原曲 256 秒 · 七段、24 场面 · 完整音乐与 2 秒演后画面 · FullFilm Revision1</p>
<video id="film" controls muted playsinline preload="metadata" src="{FILENAME}" poster="{escape(poster)}"></video>
<p><small>默认静音，手动播放。文件保留完整四轨音乐与 {len(presentation['foley_events'])} 个拟音事件；章节按钮只定位画面。</small></p>
<nav aria-label="24 场面章节">{chapters}</nav>
<p>从接入、繁荣和条件改变，走到过载、骤空、再兴与继续。保留 V5 的年代素材、荧光波形、tracker、星场、署名与人物近场；后半让失去回应、远处保存点、重新尝试、共同制作和接力成为可见的场面。</p>
<p>第二次高潮只让两条声部靠近；骤空时四轨整体退远，真实持续声仍在；再兴让音乐、工作区与新旧劳动角色一同回来。四次世界或窗口旅行中，真实音乐声源保持不动。结尾有持续音乐，空音符行没有被误当成静音。</p>
<div class="downloads"><a href="{SDK_NAME}" download="{SDK_NAME}">下载本地 SDK 包</a><a href="{FILENAME}" download="{FILENAME}">下载参考片</a><a href="review-audio.wav" download="review-audio.wav">下载完整立体声参考音频</a><a href="sources/index.html">素材与署名</a><a href="package.json">工件与哈希清单</a></div>
<p class="notice">现有报告记录：单 Editor ClientSim 的完整 Udon 连续运行覆盖 {escape(counts)}；另有开始/重播、迟到锚点、四次旅行、保存重开、V5 场景文件字节保护、可变材质隔离属性/绑定及本地 SDK 构建检查。SDK 包只在本地构建，没有上传。</p>
<details><summary>SDK 3.10.5 场景保存转换的精确核验</summary><p>{transition_text}实际 Descriptor 类型、13 个唯一 GUID/name 及 Base/Worlds 3.10.5 另有只读报告核对。保留真实输入和输出哈希，不将它们回填成相同值。</p><p><small>真实输入：<code style="overflow-wrap:anywhere">{transition['inputSceneSha256']}</code><br>最终保存输出：<code style="overflow-wrap:anywhere">{transition['outputSceneSha256']}</code></small></p><p>{snapshot_links} · <a href="reports/sdk-scene-transition.json">工具实算转换报告</a> · <a href="reports/sdk-dynamic-materials.json">实际材质、Descriptor 与版本报告</a></p></details>
<p class="bounds"><b>尚未实测：</b>真实 HMD 双眼观感、VR 控制器、真实多客户端联机、人耳试听与世界内实际响度。参考片来自固定视点，音频是固定听点的离线立体声近似，不能替代上述实机验收。电脑输出保持静音，未进行人耳试听。</p>
<p><small>参考混音峰值 {audio['mix']['peak_dbfs']:.2f} dBFS；拟音峰值低于音乐 {audio['foley_peak_below_music_peak_db']:.2f} dB。原分轨保持编配、相对增益和持续声，末 .16 秒淡出、后 2 秒为零 PCM；未做 HRTF 模拟。</small></p>
<details open><summary>完整版本定稿帧（{len(frames)} 张）</summary><div class="grid">{gallery}</div></details>
<details><summary>实际 Udon 运行截图（{len(runtime)} 张）</summary><div class="grid">{actual}</div></details>
{comparison_section}
<details><summary>检查报告与边界</summary><ul class="evidence">{links}</ul><p><small>只链接本次工件中实际存在的报告；报告的检查范围以各自 scope / provenance 为准。</small></p></details>
</main><script>const film=document.getElementById('film');film.defaultMuted=true;film.muted=true;function jump(t){{film.currentTime=t;film.scrollIntoView({{behavior:'smooth',block:'start'}});}}</script></body></html>
"""


def package(args):
    base=args.base;out=base/"review";reports=validate_reports(base)
    transition=reports["sdk-scene-transition"]
    write_sdk_transition(base,transition)
    presentation=read(args.presentation_data)
    if len(presentation.get("scenes",[]))!=24 or presentation.get("duration")!=256:
        raise ValueError("章节数据必须为 256 秒 / 24 场面")
    if len(reports["live-validation"].get("scenes",[]))!=24: raise ValueError("实时报告未覆盖 24 场面")
    movie=out/FILENAME;probe=probe_movie(movie);movie_hash=sha(movie)
    sdk=base/SDK_NAME
    if not sdk.exists() or sdk.stat().st_size<100000: raise ValueError("缺少完整本地 SDK 包："+str(sdk))
    sdk_hash=sha(sdk)
    if sdk_hash.lower()!=str(reports["sdk-build"].get("sha256","")).lower(): raise ValueError("本地 SDK 与构建报告哈希不一致")
    audio=read(out/"audio-reference.json");audio_path=out/"review-audio.wav"
    movie_tools.validate_audio(audio_path)
    if audio.get("success") is not True or audio.get("source_music_muted") is not False or audio.get("output_sha256")!=sha(audio_path):
        raise ValueError("完整音乐参考音频缺失或哈希不一致")
    if audio.get("review_seconds")!=258 or audio.get("foley_events")!=len(presentation["foley_events"]):
        raise ValueError("音频时长/拟音事件数与完整版本配置不一致")
    encoded_path=base/"reports/encode-report.json"
    if not encoded_path.exists(): raise ValueError("缺少实际编码检查报告 encode-report.json")
    encoded=read(encoded_path)
    if encoded.get("success") is not True or encoded.get("movieSha256")!=movie_hash or encoded.get("audioSha256")!=sha(audio_path):
        raise ValueError("参考片或输入音频与实际编码报告哈希不一致")
    if movie_tools.checked_sha(reports["full-decode"].get("sha256"),"full-decode.sha256")!=movie_hash:
        raise ValueError("完整解码报告与当前参考片哈希不一致")
    frames=sorted((base/"frames").glob("full_*.png"))
    runtime=[p for p in sorted((base/"runtime").rglob("*")) if p.suffix.lower() in (".png",".jpg",".jpeg")]
    if not frames or not runtime: raise ValueError("定稿帧与实际 Udon 截图均须存在")
    appendix=source_appendix.build(out/"sources")
    for folder in ["stills","runtime","reports","compare"]: (out/folder).mkdir(parents=True,exist_ok=True)
    report_names=[]
    for path in sorted((base/"reports").glob("*.json")):
        if path.name.startswith("cli-") or "failure" in path.stem: continue
        row=read(path)
        if path.stem in reports and row!=reports[path.stem]:
            raise RuntimeError("打包期间必需报告发生变化，请重新核验："+path.stem)
        if not report_passed(path.stem,row) and path.stem not in ("movie-progress",): continue
        copy_file(path,out/"reports"/path.name);report_names.append(path.stem)
    copy_file(out/"audio-reference.json",out/"reports/audio-reference.json");report_names.append("audio-reference")
    for path in frames: copy_file(path,out/"stills"/path.name)
    runtime_relative=[]
    for path in runtime:
        relative=path.relative_to(base/"runtime");runtime_relative.append(relative)
        copy_file(path,out/"runtime"/relative)
    pairs=make_comparisons(frames,args.v5_base,out)
    copy_file(sdk,out/SDK_NAME)
    snapshot_files=[]
    for item in transition["snapshots"].values():
        dest=out/item["path"];copy_file(base/item["path"],dest)
        if sha(dest)!=item["sha256"]: raise ValueError("交付的 SDK 原始快照哈希发生变化："+item["path"])
        snapshot_files.append(dest)
    copy_file(args.presentation_data,out/"fullfilm-presentation.json")
    (out/"ffprobe.json").write_text(json.dumps(probe,ensure_ascii=False,indent=2)+"\n")
    preferred=next((p for p in frames if p.name=="full_232.32.png"),None)
    if preferred is None: preferred=next((p for p in frames if frame_time(p,"full_") is not None),frames[0])
    report_names=sorted(set(report_names),key=lambda name:(REQUIRED.index(name) if name in REQUIRED else len(REQUIRED),name))
    page=render_page(presentation,reports,frames,runtime_relative,pairs,report_names,audio,"stills/"+preferred.name)
    if re.search(r"<video\b[^>]*\bautoplay\b",page,re.I) or re.search(r"\.play\s*\(",page):
        raise RuntimeError("验收页不能自动播放或由章节按钮开始播放")
    if not re.search(r"<video\b[^>]*\bmuted\b",page,re.I): raise RuntimeError("验收页视频必须默认静音")
    (out/"index.html").write_text(page,encoding="utf-8")
    important=[movie,out/SDK_NAME,audio_path,out/"index.html",out/"fullfilm-presentation.json",out/"ffprobe.json",
        out/"sources/index.html",out/"sources/source-manifest.json"]
    important += snapshot_files
    important += [out/"reports"/(name+".json") for name in report_names]
    meta=dict(complete=True,version="fullfilm-v1",scene="Assets/Scenes/OWM_FullFilm_v1.unity",
        duration_seconds=256,film_seconds=float(probe["format"]["duration"]),movie_frames=6450,
        movie_resolution=[1600,900],fps=25,scenes=len(presentation["scenes"]),
        foley_events=len(presentation["foley_events"]),stills=len(frames),runtime_screenshots=len(runtime),comparisons=len(pairs),
        sdk_bytes=sdk.stat().st_size,sdk_sha256=sdk_hash,sdk_uploaded=False,video_default_muted=True,video_autoplay=False,
        scene_sha256=reports["saved"]["sha256"],sdk_input_scene_sha256=reports["sdk-build"]["inputSceneSha256"],
        sdk_output_scene_sha256=reports["sdk-build"]["outputSceneSha256"],
        sdk_scene_transition=dict(kind=transition["kind"],report="reports/sdk-scene-transition.json",snapshots=transition["snapshots"]),
        hmd_tested=False,vr_controllers_tested=False,real_multiplayer_tested=False,human_listening_tested=False,
        mandatory_reports=REQUIRED+["sdk-scene-transition"],reports=report_names,
        sources=appendix["counts"],
        files=[dict(path=p.relative_to(out).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in important])
    (out/"package.json").write_text(json.dumps(meta,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps(dict(complete=True,index=str(out/"index.html"),film_seconds=meta["film_seconds"],
        stills=len(frames),runtime_screenshots=len(runtime),comparisons=len(pairs),reports=len(report_names),
        sdk_sha256=sdk_hash,video_default_muted=True,video_autoplay=False),ensure_ascii=False,indent=2))
    return meta


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base",type=Path,default=TMP)
    parser.add_argument("--v5-base",type=Path,default=V5)
    parser.add_argument("--presentation-data",type=Path,default=ROOT/"data/fullfilm-presentation.json")
    package(parser.parse_args())


if __name__=="__main__": main()
