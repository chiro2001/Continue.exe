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

public static class OWV2Validation
{
    const string Scene="Assets/Scenes/OWM_FullFilm_v2.unity";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v2");
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
    public static object Static()
    {
        Need(!EditorApplication.isPlaying&&!EditorApplication.isCompiling&&SceneManager.GetActiveScene().path==Scene,"Stopped compiled V2 required");var run=F<OWV2Presentation>();var rows=new List<object>();float drift=0,anchor=0;
        try
        {
            Need(!V(F<OWFullPresentation>()).enabled&&!V(F<OWFullSignal>()).enabled,"V1 drivers still enabled");
            Need(run.failureStates.Length==4&&run.screenshotStates.Length>=5&&run.archiveTextures.Length>=64,"V2 content not bound");
            foreach(string shader in new[]{"OldWebMap/V2/Archive Field","OldWebMap/V2/Source Surface","OldWebMap/V2/Depth Copper"})Need(!ShaderUtil.GetShaderMessages(Shader.Find(shader)).Any(m=>m.severity.ToString()=="Error"),"Shader errors: "+shader);
            foreach(float t in Enumerable.Range(0,200).Select(i=>i*1.28f+.04f).Concat(new[]{79.37f,81.91f,86.41f,88.31f,102.41f,104.95f,190.0f,217.6f,222.72f,239.36f}))
            {
                run.Preview(t);var sig=run.basis.signal;var osc=sig.oscilloscope;int regional=osc.regionalRenderers.Count(r=>r.enabled&&r.gameObject.activeInHierarchy);int cont=sig.continuationRenderers.Count(r=>r.enabled&&r.gameObject.activeInHierarchy);
                if(t>.08f&&t<133.12f)Need(regional==4&&cont==0,"Regional waveform leak at "+t+" active="+regional+" continuation="+cont);
                if(t>=133.12f&&t<255.2f)Need(regional==0&&cont==4,"Continuation waveform lifecycle at "+t);
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
    sealed class Context
    {
        public OWV2Presentation run;public Vm vm,d,signal,floor,music,basis;public Button button;public int confirmedSpread,snapshotRetries,snapshotRechecks,maxRawSpread;
        public Context(){Need(EditorApplication.isPlaying,"Actual Play Mode required");run=F<OWV2Presentation>();vm=V(run);d=V(run.director);signal=V(run.basis.signal);floor=V(run.audioFloor);music=V(run.director.music);basis=V(run.basis);button=run.basis.legacy.menuRoot.GetComponentsInChildren<OWEpisodeButton>(true).First(b=>b.name=="Start performance").GetComponent<Button>();}
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
        if(t>.08f&&t<133.12f)Need(a==4&&b==0,"Runtime target-region wave leak at "+t);if(t>=133.12f&&t<255.2f)Need(a==0&&b==4,"Runtime continuation waves at "+t);
        Need(Mathf.Abs(t-N(x.signal,"sampledTime"))<.16f,"Signal clock diverged");
        var waves=t<133.12f?(Transform[])V(x.run.basis.signal.oscilloscope).GetProgramVariable("waves"):(Transform[])x.signal.GetProgramVariable("continuationWaves");float anchor=0;for(int ch=0;ch<4;ch++)anchor=Mathf.Max(anchor,Vector3.Distance(sources[ch].transform.position,waves[ch].position));Need(anchor<.001f,"Runtime wave/source anchor error: "+anchor);
        var front=(GameObject[])x.vm.GetProgramVariable("frontActors");var later=(GameObject[])x.vm.GetProgramVariable("laterActors");if(t<77.44f||t>=102.4f)Need(!front.Any(g=>g.activeInHierarchy),"Runtime actor outside VRC");Need(!later.Any(g=>g.activeInHierarchy),"Runtime old later actors active");
        return new{time=t,scene=I(x.basis,"sampledScene"),native=I(x.vm,"sampledNative"),archive=I(x.vm,"sampledArchive"),failure=I(x.vm,"sampledFailure"),image=I(x.vm,"sampledImage"),floor=N(x.floor,"sampledFloor"),regional=a,continuation=b,anchor,spread,rawSpread,rechecked,samples=values};
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
            await Seek(x,255.8f);await Task.Delay(650);Need(!B(x.d,"_playing")&&I(x.vm,"sampledState")==2&&x.run.basis.legacy.menuRoot.activeInHierarchy,"Natural end/replay UI failed");Click(x);await Task.Delay(700);Need(B(x.d,"_playing")&&N(x.vm,"sampledTime")<2&&!x.run.editorStage.activeInHierarchy,"Replay left continuation objects active");
            return Report("interaction-validation",new{success=true,pauseIgnored=true,replay=true,lateAnchors=rows,retreatListenerAcRms=far,retreatPeak=far.Max(),scope="Actual single Editor ClientSim Udon, true pointer event; listener digital output precedes Windows mute. Not a real remote player/HMD/human hearing test."});
        }
        catch(Exception e){Report("interaction-failure",new{success=false,error=e.ToString(),rows,far});throw;}
        finally{x.d.SendCustomEvent("StopAtStart");await Task.Delay(150);}
    }
    public static async Task<object> ShowRuntime(float time)
    {
        var x=new Context();await Ready(x);await Seek(x,time);var row=await RuntimeRow(x);return Report("runtime-preview",new{success=true,time=N(x.vm,"sampledTime"),row});
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
            while(watch.Elapsed.TotalSeconds<265)
            {
                await Task.Delay(70);Need(EditorApplication.isPlaying,"Play stopped during live validation");float t=N(x.vm,"sampledTime");if(!B(x.d,"_playing")&&I(x.vm,"sampledState")==2)break;if(t<.12f||t>255.2f)continue;
                scenes.Add(I(x.basis,"sampledScene"));int n=I(x.vm,"sampledNative"),f=I(x.vm,"sampledFailure"),im=I(x.vm,"sampledImage");if(n>=0)natives.Add(n);if(f>=0)failures.Add(f);if(im>=0)images.Add(im);
                int now=Mathf.FloorToInt(t);if(now!=second)
                {
                    second=now;rows.Add(await RuntimeRow(x));var sources=x.Sources();maxSpread=Math.Max(maxSpread,x.confirmedSpread);for(int i=0;i<8;i++){sources[i].GetOutputData(scratch,0);peaks[i]=Math.Max(peaks[i],Rms(scratch));}
                    if(t>=189f&&t<194f){AudioListener.GetOutputData(scratch,0);far.Add(Rms(scratch));}
                    Report("live-progress",new{status="running",time=t,duration=256,scenes=scenes.Count,nativeGroups=natives.Count,failureStates=failures.Count,wallSeconds=watch.Elapsed.TotalSeconds});
                }
            }
            Need(I(x.vm,"sampledState")==2&&x.run.basis.legacy.menuRoot.activeInHierarchy,"Full run did not reach replay state");Need(scenes.Count==24&&natives.Count==6&&failures.Count==4&&images.Count==x.run.screenshotStates.Length,"Incomplete actual state coverage");Need(peaks.All(p=>p>.00001)&&far.Count>0&&far.Max()>.0003,"PCM or retreat listener signal absent");Need(maxSpread<=2,"Eight source spread exceeds two samples");
            return Report("live-validation",new{success=true,wallSeconds=watch.Elapsed.TotalSeconds,scenes=scenes.OrderBy(v=>v).ToArray(),nativeGroups=natives.OrderBy(v=>v).ToArray(),failureStates=failures.OrderBy(v=>v).ToArray(),softwareStates=images.OrderBy(v=>v).ToArray(),peakSourceAcRms=peaks,retreatListenerAcRms=far,maxConfirmedEightSourceSpreadSamples=maxSpread,maxRawSequentialGetterSpreadSamples=x.maxRawSpread,stableSnapshotRetries=x.snapshotRetries,dspBufferRechecks=x.snapshotRechecks,rows,scope="Complete actual ClientSim/Udon single-client performance. Raw sequential getter mismatches are preserved and rechecked after one actual DSP buffer; persistent drift fails. Windows physical output muted. Network late join is emulated via received clock anchors; real multiplayer/HMD and human spatial hearing remain untested."});
        }
        catch(Exception e){Report("live-failure",new{success=false,error=e.ToString(),rows,far});throw;}
        finally{x.d.SendCustomEvent("StopAtStart");await Task.Delay(150);}
    }
}
