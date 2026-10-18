#!/usr/bin/env python3
"""Render a 258 s stereo review file without playing any audio.

All four original FC14 stems remain present for 256 s. Source motion mirrors
OWFullSignal; the final 2 s are zero PCM. This is an offline fixed-listener
equal-power approximation, not HRTF or a recorded VRChat output.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
import soundfile as sf

import prepare_fullfilm_signal as signal
import render_episode123_v5_audio as v5

ROOT = Path(__file__).resolve().parents[1]
TMP = Path.home() / "tmp/vrc-old-web-map"
RATE, SHOW, REVIEW = 48000, 256.0, 258.0
GAIN, END_FADE, MODEL_RATE = .8, .16, 240
BLOCK = RATE * 4
LISTENER = np.array([0.0, 1.35, -2.8])


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def db(value): return float(20*np.log10(max(float(value),1e-12)))


def ease(value):
    value=np.clip(value,0,1)
    return value*value*(3-2*value)


def stats(bus, active=None):
    peak=0.0; squares=0.0; count=0; active_squares=0.0; active_count=0
    for start in range(0,len(bus),BLOCK):
        block=bus[start:start+BLOCK].astype(np.float64)
        peak=max(peak,float(np.abs(block).max()))
        squares+=float(np.sum(block*block)); count+=block.size
        if active is not None:
            chosen=block[active[start:start+len(block)]]
            active_squares+=float(np.sum(chosen*chosen));active_count+=chosen.size
    result=dict(peak=peak,peak_dbfs=db(peak),rms_full_dbfs=db(math.sqrt(squares/max(count,1))))
    if active_count: result["rms_active_dbfs"]=db(math.sqrt(active_squares/active_count))
    return result


def trajectory_grid(data):
    boundaries=[j[k] for j in data["journeys"] for k in ("start","end")]
    return np.unique(np.r_[np.arange(round(SHOW*MODEL_RATE)+1)/MODEL_RATE,boundaries])


def sampled_gains(ch,data,grid):
    positions=np.array([signal.source_world_position(float(t),ch,data) for t in grid])
    blend=np.array([signal.source_spatial_blend(float(t),ch,data) for t in grid])
    relative=positions-LISTENER
    distances=np.linalg.norm(relative,axis=1)
    pan=np.sin(np.arctan2(relative[:,0],relative[:,2]))
    angle=(pan+1)*np.pi/4
    centered=(1-blend)/math.sqrt(2)
    gains=np.stack([centered+blend*np.cos(angle),centered+blend*np.sin(angle)],axis=1)
    # V5's first-half review balance is retained. The later retreat changes one
    # common field gain, so no individual stem is normalised or pushed forward.
    reference=np.linalg.norm(np.asarray(data["source_motion"]["fused_center"])-LISTENER)
    far=np.linalg.norm(np.array([0,1.95,16.2])-LISTENER)
    retreat=ease((grid-186.88)/2.56)*(1-ease((grid-194.56)/12.80))
    field_attenuation=1-(1-min(1,reference/far))*retreat
    gains*=field_attenuation[:,None]
    row=dict(channel=ch+1,pan_range=[float(pan.min()),float(pan.max())],
        distance_range=[float(distances.min()),float(distances.max())],
        common_field_attenuation_min=float(field_attenuation.min()),
        landmarks=[dict(time=t,position=list(signal.source_world_position(t,ch,data)),
            spatial_blend=signal.source_spatial_blend(t,ch,data))
            for t in [0,81.92,84.48,86.4,88.32,92.16,133.12,143.36,148.48,153.6,156.16,
                186.88,189.44,194.56,197.12,207.36,215.04,217.6,222.72,235.52,250.88,253.44,256]])
    return gains,row


def check_model(data,grid):
    rows=[]
    for j in data["journeys"]:
        points=np.linspace(j["start"],j["end"],129)
        worst=max(math.dist(signal.source_world_position(j["start"],ch,data),signal.source_world_position(float(t),ch,data))
            for ch in range(4) for t in points)
        rows.append(dict(start=j["start"],end=j["end"],maximum_source_motion=worst))
    if any(r["maximum_source_motion"]!=0 for r in rows): raise ValueError("Sources move during a journey")
    # Interpolation is only for the continuous spatial gains; original PCM is
    # neither resampled nor time-stretched. Include exact journey endpoints.
    queries=np.linspace(0,SHOW,1003)
    max_position_error=0.0
    for ch in range(4):
        at=np.array([signal.source_world_position(float(t),ch,data) for t in grid])
        interp=np.stack([np.interp(queries,grid,at[:,axis]) for axis in range(3)],axis=1)
        exact=np.array([signal.source_world_position(float(t),ch,data) for t in queries])
        max_position_error=max(max_position_error,float(np.linalg.norm(interp-exact,axis=1).max()))
    if max_position_error>.001: raise ValueError(f"Spatial interpolation error too large: {max_position_error}")
    return dict(success=True,grid_hz=MODEL_RATE,journeys=rows,max_position_interpolation_error=max_position_error)


def emitter_position(index,time,foley,presentation,override):
    if index<len(foley["emitters"]):
        emitter=foley["emitters"][index]
        return v5.emitter_position(emitter,time,override),emitter["id"]
    at=index-len(foley["emitters"])
    if not 0<=at<len(presentation["new_emitters"]): raise ValueError(f"Unknown emitter {index}")
    return np.asarray(presentation["new_emitters"][at],dtype=float),f"fullfilm-emitter-{index}"


def render(args):
    presentation=json.loads(args.presentation_data.read_text())
    foley_data=json.loads(args.foley_data.read_text())
    data=json.loads(args.signal_data.read_text())
    if float(presentation["duration"])!=SHOW or float(data["duration"])!=SHOW: raise ValueError("Expected 256 s full-film metadata")
    count,total=round(SHOW*RATE),round(REVIEW*RATE)
    grid=trajectory_grid(data)
    model_check=check_model(data,grid)
    music=np.zeros((total,2),np.float32)
    sources=[]; trajectories=[]
    for ch in range(4):
        path=args.stems_dir/f"FC14_Mono_{ch+1:02d}.wav"
        gains,trajectory=sampled_gains(ch,data,grid); trajectories.append(trajectory)
        with sf.SoundFile(path) as stem:
            if stem.samplerate!=RATE or stem.channels!=1 or len(stem)!=count:
                raise ValueError(f"Expected unchanged complete 256 s / {RATE} Hz mono stem: {path}")
            sources.append(dict(channel=ch+1,path=str(path),sha256=sha(path),samples=len(stem),sample_rate=RATE))
            for start in range(0,count,BLOCK):
                length=min(BLOCK,count-start); pcm=stem.read(length,dtype="float32")
                if not np.isfinite(pcm).all(): raise ValueError(f"Nonfinite source {path}")
                time=np.arange(start,start+length,dtype=np.float64)/RATE
                gain=np.stack([np.interp(time,grid,gains[:,side]) for side in range(2)],axis=1)
                fade=1-ease((time-SHOW+END_FADE)/END_FADE)
                music[start:start+length]+=pcm[:,None]*gain*(GAIN*fade[:,None])

    override={}
    if args.emitter_report.exists():
        report=json.loads(args.emitter_report.read_text())
        keyboard=report.get("emitterPhysical",{}).get("keyboard")
        if keyboard is not None: override["keyboard"]=keyboard
    clips=[]; clip_sources=[]
    for clip in foley_data["clips"]:
        path=args.foley_dir/clip["file"]; pcm,rate=sf.read(path,dtype="float32")
        if rate!=RATE or pcm.ndim!=1 or not np.isfinite(pcm).all(): raise ValueError(f"Invalid Foley: {path}")
        clips.append(pcm);clip_sources.append(dict(id=clip["id"],path=str(path),sha256=sha(path),volume=clip["volume"]))
    foley=np.zeros_like(music);active=np.zeros(total,dtype=bool);events=[]
    for event in presentation["foley_events"]:
        time=float(event["time"]); ci=int(event["clip"]); ei=int(event["emitter"])
        if not 0<=time<SHOW or not 0<=ci<len(clips): raise ValueError(f"Invalid Foley event {event}")
        pos,name=emitter_position(ei,time,foley_data,presentation,override)
        gains,pan,distance,attenuation=v5.spatialise(pos,args.foley_reference_distance)
        start=round(time*RATE);pcm=clips[ci][:min(len(clips[ci]),count-start)]
        scaled=pcm[:,None]*gains[None,:]*float(foley_data["clips"][ci]["volume"])
        fade_time=np.arange(start,start+len(pcm),dtype=np.float64)/RATE
        scaled*=((1-ease((fade_time-SHOW+END_FADE)/END_FADE))[:,None])
        foley[start:start+len(pcm)]+=scaled;active[start:start+len(pcm)]=True
        local=music[start:min(count,start+max(1,round(min(len(pcm)/RATE,.6)*RATE)))]
        peak=float(np.abs(scaled).max());local_peak=float(np.abs(local).max())
        events.append(dict(time=time,clip=foley_data["clips"][ci]["id"],group=foley_data["clips"][ci]["group"],
            emitter=name,emitter_index=ei,position=pos.tolist(),pan=pan,distance=distance,attenuation=attenuation,
            peak=peak,peak_dbfs=db(peak),music_local_peak_dbfs=db(local_peak),
            margin_to_local_music_db=db(local_peak)-db(peak),length=len(pcm)/RATE))
    music_stats=stats(music[:count]);foley_stats=stats(foley[:count],active[:count])
    margin=music_stats["peak_dbfs"]-foley_stats["peak_dbfs"]
    mix=music+foley;mix_stats=stats(mix)
    if mix_stats["peak"]>=1 or not np.isfinite(mix_stats["peak"]):
        raise ValueError(f"Reference would clip; no automatic normalisation applied: {mix_stats['peak']}")
    if margin<14: raise ValueError(f"Foley peak margin below required 14 dB: {margin:.2f}")
    if np.any(mix[count:]): raise ValueError("Review's final two seconds must be zero PCM")
    args.output_dir.mkdir(parents=True,exist_ok=True)
    output=args.output_dir/"review-audio.wav"
    sf.write(output,mix,RATE,subtype="PCM_16")
    info=sf.info(output)
    if info.frames!=total or info.channels!=2 or info.samplerate!=RATE or info.subtype!="PCM_16": raise ValueError("Output format mismatch")
    with sf.SoundFile(output) as check:
        check.seek(count)
        if np.any(check.read(dtype="int16")): raise ValueError("Encoded final tail is not zero")
    path_sources=[args.presentation_data,args.foley_data,args.signal_data,ROOT/"tools/prepare_fullfilm_signal.py",
        ROOT/"unity/Assets/OldWebMap/Scripts/OWFullSignal.cs",Path(__file__).resolve()]
    report=dict(success=True,kind="Offline fixed-listener equal-power stereo approximation; not HRTF, not captured VRChat audio, no human listening performed",
        audio_played=False,source_music_muted=False,output=str(output),output_sha256=sha(output),
        sample_rate=RATE,channels=2,subtype="PCM_16",samples=total,show_seconds=SHOW,review_seconds=REVIEW,
        final_zero_pcm_seconds=2.0,music_gain=GAIN,music_gain_is_common_to_all_stems=True,end_fade_seconds=END_FADE,
        listener_position=LISTENER.tolist(),listener_forward=[0,0,1],model_check=model_check,
        position_function="prepare_fullfilm_signal.source_world_position",spatial_blend_function="prepare_fullfilm_signal.source_spatial_blend",
        source_motion=data["source_motion"],sources=sources,clip_sources=clip_sources,
        inputs=[dict(path=str(path),sha256=sha(path)) for path in path_sources],
        music=music_stats,foley=foley_stats,mix=mix_stats,foley_events=len(events),
        new_foley_events=sum(e["time"]>=133.12 for e in events),
        foley_peak_below_music_peak_db=margin,required_margin_db=14.0,margin_ok=margin>=14,
        foley_reference_distance=args.foley_reference_distance,emitter_override=override,
        worst_events=sorted(events,key=lambda e:e["margin_to_local_music_db"])[:10],
        assumptions=[
            "Original complete mono PCM is read at 48 kHz without resampling, retiming, per-track normalisation, note-on gating, or sound playback.",
            "First-half equal-power pan and centered/spatialBlend interpolation preserve the V5 review model; source positions use the saved V4 builder centers, not the older class field defaults.",
            "The retreat adds one common distance envelope for all four tracks: reference fused-field distance / far-field distance, mixed by the runtime retreat smoothstep. It preserves the relative stem balance and is an offline approximation of distance.",
            "Spatial gains are sampled at 240 Hz, including exact journey endpoints, and linearly interpolated onto original audio samples. Rear positions fold into stereo; no HRTF, elevation, occlusion, Doppler, VRC +10 dB gain or actual rolloff curve is simulated.",
            "Foley uses the original V5 clips and volumes; old frame emitters are evaluated at event onset, the actual keyboard override is retained, and emitters 8..11 use full-film physical coordinates.",
            "Only the final 0.16 s fade is applied to the entire music/Foley presentation. The subsequent two seconds are exact zero PCM.",
        ],trajectories=trajectories,events=events)
    (args.output_dir/"audio-reference.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps({key:report[key] for key in ["success","output","review_seconds","sample_rate","channels","subtype","foley_events","foley_peak_below_music_peak_db","music","foley","mix","model_check"]},ensure_ascii=False,indent=2))
    return report


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stems-dir",type=Path,default=TMP/"episode123/audio")
    parser.add_argument("--signal-data",type=Path,default=ROOT/"data/fullfilm-v1-signal.json")
    parser.add_argument("--presentation-data",type=Path,default=ROOT/"data/fullfilm-presentation.json")
    parser.add_argument("--foley-data",type=Path,default=ROOT/"data/episode123-v5-foley.json")
    parser.add_argument("--foley-dir",type=Path,default=ROOT/"art/export/episode123-v5/foley")
    parser.add_argument("--emitter-report",type=Path,default=TMP/"episode123-v5/reports/foley-validate.json")
    parser.add_argument("--foley-reference-distance",type=float,default=6.0)
    parser.add_argument("--output-dir",type=Path,default=TMP/"fullfilm-v1/review")
    render(parser.parse_args())


if __name__=="__main__": main()
