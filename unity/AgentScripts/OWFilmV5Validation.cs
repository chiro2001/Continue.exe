using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using TMPro;
using OldWebMap;
using Object=UnityEngine.Object;
using Vm=VRC.Udon.UdonBehaviour;

public static class OWFilmV5Validation
{
    const string Scene="Assets/Scenes/OWM_FullFilm_v5.unity";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5");
    static T F<T>() where T:Component=>Object.FindObjectsOfType<T>(true).First(x=>x.gameObject.scene==SceneManager.GetActiveScene());
    static Vm V(UdonSharpBehaviour b)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
    static float N(Vm b,string key)=>Convert.ToSingle(b.GetProgramVariable(key));
    static int I(Vm b,string key)=>Convert.ToInt32(b.GetProgramVariable(key));
    static bool B(Vm b,string key)=>Convert.ToBoolean(b.GetProgramVariable(key));
    static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name+".json",json);return json;}
    static float[] Vec(Vector3 v)=>new[]{v.x,v.y,v.z};
    static double Clock()=>Utilities.IsValid(Networking.LocalPlayer)?Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;
    static double Rms(float[] samples){double mean=samples.Average(x=>(double)x),sum=0;foreach(float x in samples)sum+=(x-mean)*(x-mean);return Math.Sqrt(sum/samples.Length);}
    static void Frame(float time)
    {
        var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;float captureAspect=camera.aspect;var rt=RenderTexture.GetTemporary(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
        bool hasInfo=GizmoUtility.TryGetGizmoInfo(typeof(Vm),out GizmoInfo info);bool icons=hasInfo&&info.iconEnabled;if(hasInfo)GizmoUtility.SetIconEnabled(typeof(Vm),false);
        try{camera.targetTexture=rt;camera.aspect=1600f/900;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory(Tmp+"/live");File.WriteAllBytes(Tmp+"/live/live-"+time.ToString("000.00",System.Globalization.CultureInfo.InvariantCulture)+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=previous;camera.aspect=captureAspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);if(hasInfo)GizmoUtility.SetIconEnabled(typeof(Vm),icons);}
    }
    public static object Static()
    {
        Need(!EditorApplication.isPlaying&&!EditorApplication.isCompiling&&SceneManager.GetActiveScene().path==Scene,"Stopped compiled full-film V4 required");var run=F<OWFilmV5Presentation>();var rows=new List<object>();float drift=0,anchor=0;
        try
        {
            Need(!V(F<OWFullPresentation>()).enabled&&!V(F<OWFullSignal>()).enabled,"V1 drivers still enabled");
            Need(run.failureStates.Length==4&&run.screenshotStates.Length>=5&&run.archiveTextures.Length>=64,"Full-film content not bound");
            foreach(string shader in new[]{"OldWebMap/V2/Archive Field","OldWebMap/V2/Source Surface","OldWebMap/V2/Depth Copper","OldWebMap/FilmV5/Story Ribbon","OldWebMap/FilmV5/Waiting Room Walls"})Need(!ShaderUtil.GetShaderMessages(Shader.Find(shader)).Any(m=>m.severity.ToString()=="Error"),"Shader errors: "+shader);
            foreach(float t in Enumerable.Range(0,198).Select(i=>i*1.28f+.04f).Concat(new[]{79.37f,81.91f,86.41f,88.31f,102.41f,104.95f,190.0f,217.6f,222.72f,239.36f}))
            {
                run.Preview(t);var sig=run.basis.signal;var osc=sig.oscilloscope;int regional=osc.regionalRenderers.Count(r=>r.enabled&&r.gameObject.activeInHierarchy);int cont=sig.continuationRenderers.Count(r=>r.enabled&&r.gameObject.activeInHierarchy);
                if(t>.08f&&t<133.12f)Need(regional==4&&cont==0,"Regional waveform leak at "+t+" active="+regional+" continuation="+cont);
                if(t>=133.12f&&t<run.director.duration-.8f)Need(regional==0&&cont==4,"Continuation waveform lifecycle at "+t);
                for(int ch=0;ch<4;ch++){var wave=t<133.12f?osc.waves[ch]:sig.continuationWaves[ch];anchor=Mathf.Max(anchor,Vector3.Distance(run.director.music.sources[ch].transform.position,wave.position));}
                if(t<77.44f||t>=102.4f)Need(!run.frontActors.Any(g=>g.activeInHierarchy),"Actor outside VRC at "+t);Need(!run.laterActors.Any(g=>g.activeInHierarchy),"Old narrative actors visible at "+t);
                Need((run.basis.keyRoot==null||!run.basis.keyRoot.activeInHierarchy)&&(run.basis.legacy.captionRoot==null||!run.basis.legacy.captionRoot.activeInHierarchy),"Explanatory caption still visible");
                if(t>=168.96f&&t<186.88f)Need(run.sampledFailure>=0&&run.sampledFailure<4,"Failure state absent");
                if(t>=197.12f)Need(run.sampledImage>=0&&run.editorStage.activeInHierarchy,"Real production stage absent");
                rows.Add(new{time=t,native=run.sampledNative,archive=run.sampledArchive,failure=run.sampledFailure,image=run.sampledImage,actors=run.visibleActors,regional,continuation=cont,scale=run.sampledWorldScale});
            }
            foreach(var span in new[]{new[]{79.36f,81.92f},new[]{86.40f,88.32f},new[]{102.40f,104.96f},new[]{217.6f,222.72f}})for(int ch=0;ch<4;ch++){var p=run.basis.signal.SourceWorldPosition(span[0],ch);for(float t=span[0];t<span[1];t+=1f/120f)drift=Mathf.Max(drift,Vector3.Distance(p,run.basis.signal.SourceWorldPosition(t,ch)));}
            Need(anchor<.001f&&drift<.0001f,"Physical voice anchor or travel drift: "+anchor+" / "+drift);
            run.Waiting();Need(run.basis.legacy.menuRoot.activeInHierarchy&&!run.editorStage.activeInHierarchy&&!run.overloadField.activeInHierarchy,"Opening objects leak before start");run.Finished();Need(run.basis.legacy.menuRoot.activeInHierarchy&&run.editorStage.activeInHierarchy,"Replay or aftermath absent");
            return Report("static-validation",new{success=true,maxWaveSourceAnchorError=anchor,maxTravelSourceDrift=drift,samples=rows.Count,rows,scope="Editor C# sampling, not a live Udon/HMD/multiplayer certification"});
        }
        catch(Exception e){Report("static-failure",new{success=false,error=e.ToString(),rows});throw;}
        finally{run.Waiting();run.offlinePreview=false;}
    }
    public static object Revision()
    {
        Need(!EditorApplication.isPlaying&&!EditorApplication.isCompiling&&SceneManager.GetActiveScene().path==Scene,"Stopped compiled full-film V4 required");
        var run=F<OWFilmV5Presentation>();var details=run.details;var sources=run.director.music.sources.Concat(run.audioFloor.drySources).ToArray();var curves=new List<object>();float maxDistance=0,minGain=1,maxHaloError=0,maxTravelPosition=0,maxTravelBlend=0,maxTravelVolume=0;
        try
        {
            Need(Mathf.Abs(run.director.duration-253.44f)<.001f&&Mathf.Abs(run.director.music.performanceEnd-run.director.duration)<.001f&&Mathf.Abs(run.basis.signal.duration-run.director.duration)<.001f,"V3 clocks disagree on trimmed ending");
            Need(run.director.music.sources.All(s=>s.clip.samples==12288000&&s.clip.frequency==48000),"Original complete PCM files were changed");
            foreach(var source in sources)
            {
                var sp=source.GetComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();Need(sp!=null,"Missing VRC spatial component");var so=new SerializedObject(sp);
                Need(source.rolloffMode==AudioRolloffMode.Linear&&source.minDistance==30&&source.maxDistance==150&&source.dopplerLevel==0,"Unexpected Unity distance curve");
                Need(so.FindProperty("Near").floatValue==30&&so.FindProperty("Far").floatValue==150&&so.FindProperty("Gain").floatValue==0&&so.FindProperty("UseAudioSourceVolumeCurve").boolValue,"VRC attenuation mismatch");
                curves.Add(new{source.name,source.minDistance,source.maxDistance,vrcNear=so.FindProperty("Near").floatValue,vrcFar=so.FindProperty("Far").floatValue,useUnityCurve=so.FindProperty("UseAudioSourceVolumeCurve").boolValue});
            }
            foreach(float t in Enumerable.Range(0,99).Select(i=>i*2.56f+.04f))
            {
                run.Preview(t);Need(!run.basis.signal.gridRenderer.enabled,"Ground grid returned");
                foreach(var source in run.director.music.sources)foreach(float x in new[]{-8f,0,8f})foreach(float z in new[]{-10.8f,-2.8f,5.2f})foreach(float y in new[]{.65f,1.5f})
                {float distance=Vector3.Distance(source.transform.position,new Vector3(x,y,z));maxDistance=Mathf.Max(maxDistance,distance);float gain=distance<=30?1:Mathf.Clamp01(1-(distance-30)/120);minGain=Mathf.Min(minGain,gain);}
                Transform expected=null;
                if(run.media.sampledChronology>=0||run.provenance.sampledMain>=0)expected=null;
                else if(details.provenanceRoot!=null&&details.provenanceRoot.activeInHierarchy)expected=details.provenanceMain.transform;
                else if(details.gamePlane!=null&&details.gamePlane.gameObject.activeInHierarchy)expected=details.gamePlane.transform;
                else if(run.nativeRoot.activeInHierarchy)expected=run.nativeMain.transform;
                else if(run.archiveFocus.gameObject.activeInHierarchy)expected=run.archiveMain;
                if(expected!=null){Need(details.focusHalos[0].gameObject.activeInHierarchy,"Focused source lacks its halo at "+t+" target="+expected.name);maxHaloError=Mathf.Max(maxHaloError,Vector3.Distance(details.focusHalos[0].position,expected.position+expected.forward*.035f));}
            }
            foreach(var span in new[]{new[]{79.36f,81.92f},new[]{86.4f,88.32f},new[]{102.4f,104.96f},new[]{217.6f,222.72f}})
            {
                run.Preview(span[0]);run.audioFloor.Sync(span[0],false);var p=sources.Select(s=>s.transform.position).ToArray();var blend=sources.Select(s=>s.spatialBlend).ToArray();var volumes=sources.Select(s=>s.volume).ToArray();
                for(float t=span[0];t<span[1];t+=1f/60){run.Preview(t);run.audioFloor.Sync(t,false);for(int i=0;i<8;i++){maxTravelPosition=Mathf.Max(maxTravelPosition,Vector3.Distance(p[i],sources[i].transform.position));maxTravelBlend=Mathf.Max(maxTravelBlend,Mathf.Abs(blend[i]-sources[i].spatialBlend));maxTravelVolume=Mathf.Max(maxTravelVolume,Mathf.Abs(volumes[i]-sources[i].volume));}}
            }
            Need(maxTravelPosition<.001f&&maxTravelBlend<.0001f&&maxTravelVolume<.0001f,"Audio changes with scene travel");Need(maxHaloError<.0001f,"Focus halo transform mismatch");Need(20*Mathf.Log10(minGain)>-.8f,"Audience walking causes excessive analytical attenuation");
            run.Preview(77);Need(!run.basis.legacy.cast.hub2017.gameObject.activeInHierarchy,"Hub appears before travel");run.Preview(79.37f);Need(run.basis.legacy.cast.hub2017.gameObject.activeInHierarchy,"Hub absent at travel start");
            run.Preview(205);var desk=run.workspace.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Desk_WalnutSlab").Select(r=>new{r.name,top=r.bounds.max.y}).ToArray();
            Need(desk.Any(r=>Mathf.Abs(r.top-.60f)<.025f),"No desk surface near 0.60m");
            run.Finished();Physics.SyncTransforms();Need(details.worldFloorColliders.Length==28&&details.worldFloorColliders.All(c=>c.enabled)&&details.originalFloorColliders.All(c=>!c.enabled),"Final collision handover failed");
            var hits=Physics.RaycastAll(new Vector3(0,2,-2.8f),Vector3.down,4).Where(h=>details.worldFloorColliders.Contains(h.collider)).OrderBy(h=>h.distance).ToArray();Need(hits.Length>0&&Mathf.Abs(hits[0].point.y+.05f)<.005f,"Final bridge does not align to the audience floor");
            var panel=details.menuGeometry.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Select(r=>r.bounds.min.y).ToArray();Need(panel.Length>0&&panel.Min()>.10f,"Replay panel enters floor");
            Need(details.penRoot!=null&&details.penRoot.activeInHierarchy&&details.penPickups.Length>0&&details.penPickups.All(p=>p.pickupable),"Aftermath QvPen missing");
            Need(details.worldFloorColliders.Count(c=>c is MeshCollider)==3,"Unexpected moving/final mesh collision set");
            Need(details.mangaNativeStates.Length==8&&details.flashStates.Length==6&&details.gameStates.Length==2&&details.stickerPlanes.Length==12&&run.provenance.plates.Length==14,"V3 art or provenance binding missing");
            Need(details.cursorSwarm.GetComponent<MeshFilter>().sharedMesh.vertexCount==512,"128 cursor batch missing");
            Need(run.basis.signal.oscilloscope.waves[0].GetComponent<MeshFilter>().sharedMesh.vertexCount==12288,"Round capsule PCM geometry missing");
            foreach(string name in new[]{"OldWebMap/V3/Window Interior","OldWebMap/V3/Returning Ribbon","OldWebMap/V3/EDGE TEAM Pixel Cylinder","OldWebMap/V3/Round Oscilloscope","OldWebMap/V3/Attached Focus Halo","OldWebMap/V3/Pixel Cursor Swarm"}){var shader=Shader.Find(name);Need(shader!=null&&!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"),"Shader error: "+name);}
            run.Waiting();Need(details.openingRoom.gameObject.activeInHierarchy&&details.openingWalls.Length==6&&details.penRoot.activeInHierarchy&&details.penRenderers.All(r=>!r.enabled)&&details.penColliders.All(c=>!c.enabled)&&details.penPickups.All(p=>!p.pickupable),"Waiting/pen visibility gate failed");
            return Report("v4-inherited-validation",new{success=true,duration=run.director.duration,originalClipSeconds=256,curves,maxAudienceSourceDistance=maxDistance,worstDistanceGainDb=20*Mathf.Log10(minGain),maxTravelPosition,maxTravelBlend,maxTravelVolume,maxHaloError,desk,finalColliders=28,replayLowestPixelGeometryY=panel.Min(),qvpenPickups=details.penPickups.Length,userOriginals=8,provenanceViews=14,scope="Stopped Editor sampling, imported meshes and actual serialized Unity/VRC settings. Analytic distance gain is not an HMD hearing test."});
        }
        catch(Exception e){Report("v4-inherited-failure",new{success=false,error=e.ToString()});throw;}
        finally{run.Waiting();run.offlinePreview=false;}
    }
    sealed class Context
    {
        public OWFilmV5Presentation run;public Vm vm,d,signal,floor,music,basis,details;public Button button;public int confirmedSpread,snapshotRetries,snapshotRechecks,maxRawSpread;
        public Context(){Need(EditorApplication.isPlaying,"Actual Play Mode required");run=F<OWFilmV5Presentation>();vm=V(run);d=V(run.director);signal=V(run.basis.signal);floor=V(run.audioFloor);music=V(run.director.music);basis=V(run.basis);details=V(run.details);button=run.basis.legacy.menuRoot.GetComponentsInChildren<OWEpisodeButton>(true).First(b=>b.name=="Start performance").GetComponent<Button>();}
        public AudioSource[] Sources()=>((AudioSource[])music.GetProgramVariable("sources")).Concat((AudioSource[])floor.GetProgramVariable("drySources")).ToArray();
    }
    static async Task Ready(Context x){var timer=System.Diagnostics.Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<20&&(!Utilities.IsValid(Networking.LocalPlayer)||x.vm.GetProgramVariable("sampledState")==null))await Task.Delay(100);Need(Utilities.IsValid(Networking.LocalPlayer)&&!B(x.vm,"offlinePreview"),"ClientSim/VM not ready");}
    static void Click(Context x){Need(x.button.IsActive()&&x.button.IsInteractable(),"Start/replay ray UI unavailable");Need(ExecuteEvents.Execute(x.button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler),"Pointer click dispatch failed");}
    static async Task Seek(Context x,float time){x.d.SetProgramVariable("_offset",0f);x.d.SetProgramVariable("_anchor",Clock()-time);x.d.SetProgramVariable("_playing",true);x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);x.d.SendCustomEvent("_onDeserialization");await Task.Delay(750);}
    static int[] StableSamples(AudioSource[] sources,out int retries)
    {
        // timeSamples is read on the main thread while the DSP thread advances.
        // Bracket all eight getters with the same source: accept only a snapshot
        // wholly inside one unchanged audio-buffer position, never the minimum
        // of inconsistent reads. Genuine inter-source skew still fails below.
        for(retries=0;retries<64;retries++)
        {
            int before=sources[0].timeSamples;var values=sources.Select(s=>s.timeSamples).ToArray();int after=sources[0].timeSamples;
            if(before==after&&values[0]==before)return values;
        }
        throw new Exception("Could not obtain one stable DSP sample snapshot");
    }
    static async Task<object> RuntimeRow(Context x)
    {
        var sources=x.Sources();var values=StableSamples(sources,out int retries);x.snapshotRetries+=retries;int rawSpread=values.Max()-values.Min();x.maxRawSpread=Math.Max(x.maxRawSpread,rawSpread);bool rechecked=rawSpread>2;
        if(rechecked)
        {
            // Per-source playback-position caches also update sequentially, even
            // while dspTime and source[0] remain unchanged. Diagnostics preserve
            // the raw mixed-buffer values. Wait one actual output buffer and
            // require the next snapshot to align; persistent skew still fails.
            AudioSettings.GetDSPBufferSize(out int length,out int count);await Task.Delay(Mathf.CeilToInt(length*1000f/AudioSettings.outputSampleRate)+3);
            values=StableSamples(sources,out retries);x.snapshotRetries+=retries;x.snapshotRechecks++;
        }
        int spread=values.Max()-values.Min();x.confirmedSpread=spread;Need(spread<=2,"Persistent eight-track sample spread after DSP-buffer recheck: "+spread);
        float t=N(x.vm,"sampledTime");var regional=(Renderer[])V(x.run.basis.signal.oscilloscope).GetProgramVariable("regionalRenderers");var continuation=(Renderer[])x.signal.GetProgramVariable("continuationRenderers");
        int a=regional.Count(r=>r.enabled&&r.gameObject.activeInHierarchy),b=continuation.Count(r=>r.enabled&&r.gameObject.activeInHierarchy);
        if(t>.08f&&t<133.12f)Need(a==4&&b==0,"Runtime target-region wave leak at "+t);if(t>=133.12f&&t<x.run.director.duration-.8f)Need(a==0&&b==4,"Runtime continuation waves at "+t);
        Need(Mathf.Abs(t-N(x.signal,"sampledTime"))<.16f,"Signal clock diverged");
        Need(!x.run.basis.signal.gridRenderer.enabled,"Runtime ground grid visible");
        Need(x.run.details.penPickups.Where(p=>p!=null).All(p=>!p.pickupable)&&x.run.details.penRenderers.Where(r=>r!=null).All(r=>!r.enabled),"QvPen visible/pickable during performance");
        Need(x.run.details.penCanvases.Length>0&&x.run.details.penCanvases.Where(c=>c!=null).All(c=>!c.enabled),"QvPen UI leaks during performance");
        if(t>=79.36f&&t<102.4f)
        {
            var names=V(x.run.basis.legacy.cast.nameplates);var ids=(int[])names.GetProgramVariable("playerIds");var labels=(TMP_Text[])names.GetProgramVariable("playerLabels");
            for(int i=0;i<ids.Length;i++)if(ids[i]>=0){var p=VRCPlayerApi.GetPlayerById(ids[i]);if(Utilities.IsValid(p))Need(labels[i].text==p.displayName,"Actual player name remains anonymous");}
        }
        var waves=t<133.12f?(Transform[])V(x.run.basis.signal.oscilloscope).GetProgramVariable("waves"):(Transform[])x.signal.GetProgramVariable("continuationWaves");float anchor=0;for(int ch=0;ch<4;ch++)anchor=Mathf.Max(anchor,Vector3.Distance(sources[ch].transform.position,waves[ch].position));Need(anchor<.001f,"Runtime wave/source anchor error: "+anchor);
        var front=(GameObject[])x.vm.GetProgramVariable("frontActors");var later=(GameObject[])x.vm.GetProgramVariable("laterActors");if(t<77.44f||t>=102.4f)Need(!front.Any(g=>g.activeInHierarchy),"Runtime actor outside VRC");Need(!later.Any(g=>g.activeInHierarchy),"Runtime old later actors active");
        var media=V(x.run.media);int group=I(media,"sampledGroup"),related=I(media,"activeRelated"),film=I(media,"sampledFilm");
        if(group>=0){Need(related>=2,"Runtime focus has fewer than two related sources");var planes=(Transform[])media.GetProgramVariable("relatedPlanes");var mats=(Material[])media.GetProgramVariable("relatedMaterials");for(int i=0;i<planes.Length;i++)if(planes[i].gameObject.activeInHierarchy)Need(mats[i].mainTexture!=null,"Runtime related source is a white placeholder");}
        if(film>=0)Need(x.run.media.filmPlane.gameObject.activeInHierarchy&&x.run.media.filmMaterial.mainTexture!=null,"Runtime silent footage absent");
        return new{time=t,scene=I(x.basis,"sampledScene"),native=I(x.vm,"sampledNative"),archive=I(x.vm,"sampledArchive"),failure=I(x.vm,"sampledFailure"),image=I(x.vm,"sampledImage"),ribbon=I(x.details,"sampledRibbonState"),related,history=I(media,"sampledHistory"),film,emotes=I(media,"activeEmotes"),floor=N(x.floor,"sampledFloor"),regional=a,continuation=b,anchor,spread,rawSpread,rechecked,samples=values};
    }
    public static async Task<object> Interactions()
    {
        var x=new Context();var rows=new List<object>();var samples=new float[1024];var far=new List<double>();
        try
        {
            await Ready(x);x.d.SendCustomEvent("StopAtStart");await Task.Delay(250);Click(x);await Task.Delay(800);Need(B(x.d,"_playing")&&!x.run.basis.legacy.menuRoot.activeInHierarchy,"Real ray UI did not start");x.d.SendCustomEvent("Pause");await Task.Delay(100);Need(B(x.d,"_playing"),"Continuous performance paused");
            foreach(float t in new[]{16f,35.84f,55.36f,80f,87f,103f,118f,137f,146f,161f,170f,175f,180f,184f,190f,203f,210f,219f,224f,239f,252f})
            {
                await Seek(x,t);Need(x.Sources().All(s=>s.isPlaying),"PCM source did not reconstruct at "+t);rows.Add(await RuntimeRow(x));
                if(t==190f)for(int i=0;i<12;i++){AudioListener.GetOutputData(samples,0);far.Add(Rms(samples));await Task.Delay(65);}
            }
            Need(far.Count>0&&far.Max()>.0003,"Final Unity listener signal is too low during retreat");
            await Seek(x,x.run.director.duration-.2f);await Task.Delay(650);Need(!B(x.d,"_playing")&&I(x.vm,"sampledState")==2&&x.run.basis.legacy.menuRoot.activeInHierarchy,"Natural end/replay UI failed");Need(x.run.details.penPickups.Where(p=>p!=null).All(p=>p.pickupable)&&x.run.details.worldFloorColliders.All(c=>c.enabled)&&x.run.details.originalFloorColliders.All(c=>!c.enabled),"Pen/floor not released after the end");Click(x);await Task.Delay(700);Need(B(x.d,"_playing")&&N(x.vm,"sampledTime")<2&&!x.run.editorStage.activeInHierarchy,"Replay left continuation objects active");Need(x.run.details.penPickups.Where(p=>p!=null).All(p=>!p.pickupable)&&x.run.details.worldFloorColliders.All(c=>!c.enabled),"Replay left pen or final floor interactive");
            return Report("interaction-validation",new{success=true,pauseIgnored=true,replay=true,lateAnchors=rows,retreatListenerAcRms=far,retreatPeak=far.Max(),scope="Actual single Editor ClientSim Udon, true pointer event; listener digital output precedes Windows mute. Not a real remote player/HMD/human hearing test."});
        }
        catch(Exception e){Report("interaction-failure",new{success=false,error=e.ToString(),rows,far});throw;}
        finally{x.d.SendCustomEvent("StopAtStart");await Task.Delay(150);}
    }
    public static async Task<object> ShowRuntime(float time)
    {
        var x=new Context();await Ready(x);await Seek(x,time);var row=await RuntimeRow(x);return Report("runtime-preview",new{success=true,time=N(x.vm,"sampledTime"),row});
    }
    public static async Task<object> Names()
    {
        var x=new Context();await Ready(x);
        try
        {
            await Seek(x,89);var vm=V(x.run.basis.legacy.cast.nameplates);var ids=(int[])vm.GetProgramVariable("playerIds");var labels=(TMP_Text[])vm.GetProgramVariable("playerLabels");var actors=(TMP_Text[])vm.GetProgramVariable("actorLabels");int count=0;
            for(int i=0;i<ids.Length;i++)if(ids[i]>=0){var player=VRCPlayerApi.GetPlayerById(ids[i]);if(!Utilities.IsValid(player))continue;Need(labels[i].text==player.displayName,"Actual displayName mismatch");count++;}
            Need(count>0&&I(x.details,"namedPlayers")>0,"Actual ClientSim player was not assigned a historical nameplate");Need(actors.Where(a=>a!=null).All(a=>a.text=="xxxx"),"An actor nameplate lost anonymity");
            return Report("player-names-validation",new{success=true,actualPlayersComparedToApi=count,anonymousActorLabels=actors.Count(a=>a!=null),actualNamesOmittedFromReport=true,scope="Actual single ClientSim roster and Udon label sampling; real remote roster/late join remains to be tested"});
        }
        finally{x.d.SendCustomEvent("StopAtStart");}
    }
    public static async Task<object> EndingLayout()
    {
        var x=new Context();await Ready(x);
        try
        {
            x.d.SetProgramVariable("_offset",x.run.director.duration);x.d.SetProgramVariable("_playing",false);x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);x.d.SendCustomEvent("_onDeserialization");await Task.Delay(600);
            Need(I(x.vm,"sampledState")==2&&x.run.details.creditsRoot.activeInHierarchy,"Actual Udon did not open the credits");
            var old=x.run.finalWorld.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CommunityPanel");Need(!old.gameObject.activeInHierarchy,"Superseded source board returned");
            var pages=x.run.details.creditsRoot.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Readable credits").ToArray();Need(pages.Length==2&&pages.All(r=>r.enabled&&r.sharedMaterial.mainTexture!=null),"Two actual credits pages absent");
            Need(Vector3.Distance(pages[0].transform.position,pages[1].transform.position)>2.5f,"Credits pages overlap");
            Need(x.run.details.penPickups.Where(p=>p!=null).All(p=>p.pickupable)&&x.run.details.worldFloorColliders.All(c=>c.enabled),"Final pen/floor gate failed after layout cleanup");
            Frame(x.run.director.duration);Click(x);await Task.Delay(500);
            Need(B(x.d,"_playing")&&!x.run.details.creditsRoot.activeInHierarchy&&x.run.details.penPickups.Where(p=>p!=null).All(p=>!p.pickupable),"Replay did not close final layout");
            return Report("ending-layout-validation",new{success=true,actualUdonCredits=2,supersededSourceBoardHidden=true,actualEndFrame=true,replayHidesCreditsAndPens=true,scope="Actual single ClientSim after the final static layout cleanup; the complete live test precedes only this geometry placement change"});
        }
        finally{x.d.SendCustomEvent("StopAtStart");}
    }
    public static async Task<object> FloorWalk()
    {
        var x=new Context();await Ready(x);var player=Networking.LocalPlayer;var saved=player.GetPosition();var rotation=player.GetRotation();var rows=new List<object>();
        try
        {
            x.d.SetProgramVariable("_offset",x.run.director.duration);x.d.SetProgramVariable("_playing",false);x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);x.d.SendCustomEvent("_onDeserialization");await Task.Delay(400);
            foreach(var point in new[]{new Vector3(0,2,-2.8f),new Vector3(2.5f,2,0),new Vector3(4.05f,2,0),new Vector3(-2.5f,2,0)})
            {
                var hits=Physics.RaycastAll(point,Vector3.down,5).Where(h=>x.run.details.worldFloorColliders.Contains(h.collider)).OrderBy(h=>h.distance).ToArray();Need(hits.Length>0,"No authored final surface at "+point);
                var start=point;start.y=1;player.TeleportTo(start,rotation);await Task.Delay(1000);var landed=player.GetPosition();Need(Mathf.Abs(landed.y-hits[0].point.y)<.07f,"ClientSim player did not settle on final surface at "+point+" y="+landed.y);
                rows.Add(new{point=Vec(point),surface=hits[0].collider.name,surfaceY=hits[0].point.y,playerFeet=Vec(landed)});
            }
            return Report("final-floor-walk",new{success=true,rows,scope="Actual single ClientSim gravity/collision settling on authored bridge, ring and seat surfaces; no HMD locomotion claim"});
        }
        finally{player.TeleportTo(saved,rotation);x.d.SendCustomEvent("StopAtStart");}
    }
    public static async Task<object> HoldRuntime(float time)
    {
        var x=new Context();await Ready(x);x.d.SetProgramVariable("_offset",time);x.d.SetProgramVariable("_anchor",Clock()+30);x.d.SetProgramVariable("_playing",true);x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);x.d.SendCustomEvent("_onDeserialization");await Task.Delay(300);
        Selection.activeGameObject=x.run.finalWorld.gameObject;var view=UnityEditor.SceneView.lastActiveSceneView;if(view!=null){view.LookAt(new Vector3(0,1.2f,2f),Quaternion.Euler(13f,-27f,0f),8f);view.Focus();view.Repaint();}
        var row=await RuntimeRow(x);return Report("runtime-held-"+time.ToString("000.00",System.Globalization.CultureInfo.InvariantCulture),new{success=true,time=N(x.vm,"sampledTime"),scope="Actual Unity Play Mode with a future test clock anchor holding a deterministic visual instant; no screenshot UI reconstructed",row});
    }
    public static async Task<object> AudioTiming()
    {
        var x=new Context();var rows=new List<object>();await Ready(x);AudioSettings.GetDSPBufferSize(out int bufferLength,out int buffers);
        try
        {
            foreach(float time in new[]{163f,190f,239f})
            {
                await Seek(x,time);var watch=System.Diagnostics.Stopwatch.StartNew();
                while(watch.Elapsed.TotalSeconds<3)
                {
                    var sources=x.Sources();double before=AudioSettings.dspTime;var values=sources.Select(s=>s.timeSamples).ToArray();double after=AudioSettings.dspTime;
                    rows.Add(new{time=N(x.vm,"sampledTime"),before,after,samples=values,spread=values.Max()-values.Min(),virtualized=sources.Select(s=>s.isVirtual).ToArray(),volumes=sources.Select(s=>s.volume).ToArray(),schedules=I(x.music,"scheduleCount"),commonDsp=x.floor.GetProgramVariable("lastCommonDsp")});
                    await Task.Delay(5);
                }
            }
            return Report("audio-timing-diagnostic",new{success=true,bufferLength,buffers,rate=AudioSettings.outputSampleRate,rows,scope="Raw sequential getter snapshots, including intermediate DSP updates; this diagnostic does not certify sample alignment"});
        }
        finally{x.d.SendCustomEvent("StopAtStart");}
    }
    public static async Task<object> Live()
    {
        var x=new Context();var rows=new List<object>();var scenes=new HashSet<int>();var natives=new HashSet<int>();var failures=new HashSet<int>();var images=new HashSet<int>();var peaks=new double[8];var scratch=new float[512];var far=new List<double>();int second=-1,maxSpread=0;var watch=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await Ready(x);x.d.SendCustomEvent("StopAtStart");await Task.Delay(250);Click(x);watch.Restart();
            while(watch.Elapsed.TotalSeconds<x.run.director.duration+10)
            {
                await Task.Delay(70);Need(EditorApplication.isPlaying,"Play stopped during live validation");float t=N(x.vm,"sampledTime");if(!B(x.d,"_playing")&&I(x.vm,"sampledState")==2)break;if(t<.12f||t>x.run.director.duration-.8f)continue;
                scenes.Add(I(x.basis,"sampledScene"));int n=I(x.vm,"sampledNative"),f=I(x.vm,"sampledFailure"),im=I(x.vm,"sampledImage");if(n>=0)natives.Add(n);if(f>=0)failures.Add(f);if(im>=0)images.Add(im);
                int now=Mathf.FloorToInt(t);if(now!=second)
                {
                    second=now;rows.Add(await RuntimeRow(x));var sources=x.Sources();maxSpread=Math.Max(maxSpread,x.confirmedSpread);for(int i=0;i<8;i++){sources[i].GetOutputData(scratch,0);peaks[i]=Math.Max(peaks[i],Rms(scratch));}
                    if(new[]{6,35,55,80,89,109,121,125,129,145,165,184,190,199,209,220,240,250}.Contains(now))Frame(t);
                    if(t>=189f&&t<194f){AudioListener.GetOutputData(scratch,0);far.Add(Rms(scratch));}
                    Report("live-progress",new{status="running",time=t,duration=x.run.director.duration,scenes=scenes.Count,nativeGroups=natives.Count,failureStates=failures.Count,wallSeconds=watch.Elapsed.TotalSeconds});
                }
            }
            Need(I(x.vm,"sampledState")==2&&x.run.basis.legacy.menuRoot.activeInHierarchy,"Full run did not reach replay state");Need(scenes.Count==24&&natives.Count==6&&failures.Count==4&&images.Count==x.run.screenshotStates.Length,"Incomplete actual state coverage");Need(peaks.All(p=>p>.00001)&&far.Count>0&&far.Max()>.0003,"PCM or retreat listener signal absent");Need(maxSpread<=2,"Eight source spread exceeds two samples");
            return Report("live-validation",new{success=true,wallSeconds=watch.Elapsed.TotalSeconds,scenes=scenes.OrderBy(v=>v).ToArray(),nativeGroups=natives.OrderBy(v=>v).ToArray(),failureStates=failures.OrderBy(v=>v).ToArray(),softwareStates=images.OrderBy(v=>v).ToArray(),peakSourceAcRms=peaks,retreatListenerAcRms=far,maxConfirmedEightSourceSpreadSamples=maxSpread,maxRawSequentialGetterSpreadSamples=x.maxRawSpread,stableSnapshotRetries=x.snapshotRetries,dspBufferRechecks=x.snapshotRechecks,rows,scope="Complete actual ClientSim/Udon single-client performance. Raw sequential getter mismatches are preserved and rechecked after one actual DSP buffer; persistent drift fails. Windows physical output muted. Network late join is emulated via received clock anchors; real multiplayer/HMD and human spatial hearing remain untested."});
        }
        catch(Exception e){Report("live-failure",new{success=false,error=e.ToString(),rows,far});throw;}
        finally{x.d.SendCustomEvent("StopAtStart");await Task.Delay(150);}
    }
}
