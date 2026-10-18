using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using OldWebMap;
using Object=UnityEngine.Object;
using Vm=VRC.Udon.UdonBehaviour;

public static class OWFullFilmValidation
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v1");
    const string Scope="One actual Editor ClientSim/Udon performance; local received-anchor reconstruction, not real network/HMD/controller/human listening certification. Physical Windows output muted externally.";
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static Vm V(UdonSharpBehaviour b)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
    static float N(Vm b,string n)=>Convert.ToSingle(b.GetProgramVariable(n));
    static int I(Vm b,string n)=>Convert.ToInt32(b.GetProgramVariable(n));
    static bool B(Vm b,string n)=>Convert.ToBoolean(b.GetProgramVariable(n));
    static void Need(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Report(string name,object data){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(data,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    static double Clock()=>Utilities.IsValid(Networking.LocalPlayer)?Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;
    sealed class Context
    {
        public OWFullPresentation full;public OWV4Presentation legacy;public Vm vm,d,m,signal,overload,renewal,music,foley,op,cast;public Button button;
        public Context()
        {
            Need(EditorApplication.isPlaying,"Actual Play Mode required");full=F<OWFullPresentation>();Need(full!=null,"Full-film component missing");legacy=full.legacy;
            vm=V(full);d=V(full.director);m=V(legacy.montage);signal=V(full.signal);overload=V(full.overload);renewal=V(full.renewal);music=V(full.director.music);foley=V(F<OWV4Foley>());op=V(legacy.operations);cast=V(legacy.cast);
            var handler=legacy.menuRoot.GetComponentsInChildren<OWEpisodeButton>(true).First(h=>h.name=="Start performance");button=handler.GetComponent<Button>();
        }
        public AudioSource[] Sources()=>(AudioSource[])music.GetProgramVariable("sources");
    }
    static async Task Ready(Context x)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(timer.Elapsed.TotalSeconds<20&&(!Utilities.IsValid(Networking.LocalPlayer)||x.vm.GetProgramVariable("sampledState")==null))await Task.Delay(100);
        Need(Utilities.IsValid(Networking.LocalPlayer),"ClientSim LocalPlayer not ready");Need(!B(x.vm,"offlinePreview"),"Full-film runtime is still in offline preview");Need(Mathf.Abs(N(x.d,"duration")-256)<.001f,"Director duration not 256");
    }
    static void Click(Context x)
    {
        Need(x.button.IsActive()&&x.button.IsInteractable()&&EventSystem.current!=null,"Start/replay button unavailable");
        Need(ExecuteEvents.Execute(x.button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler),"Pointer click not dispatched");
    }
    static void Screenshot(string name)
    {
        var c=Camera.main;Need(c!=null,"Review camera missing");string dir=Tmp+"/runtime";Directory.CreateDirectory(dir);var previous=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(dir+"/"+name+".png",image.EncodeToPNG());}
        finally{c.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static async Task Cleanup(Context x,string name)
    {if(EditorApplication.isPlaying){x.d.SendCustomEvent("StopAtStart");await Task.Delay(200);}Report(name+"-cleanup.json",new{success=true,playing=EditorApplication.isPlaying,stoppedAtStart=true});}
    public static async Task<object> Interactions()
    {
        var x=new Context();var rows=new List<object>();
        try
        {
            await Ready(x);x.d.SendCustomEvent("StopAtStart");await Task.Delay(350);Need(I(x.vm,"sampledState")==0&&!x.full.continuation.activeInHierarchy&&x.legacy.menuRoot.activeInHierarchy,"Waiting state dirty");Screenshot("interaction-waiting");
            Click(x);await Task.Delay(700);Need(B(x.d,"_playing")&&!x.legacy.menuRoot.activeInHierarchy,"Real button did not start full-film");
            x.d.SendCustomEvent("Pause");await Task.Delay(200);Need(B(x.d,"_playing"),"Continuous performance accepted a pause");
            foreach(float target in new[]{20f,85f,118f,140f,150f,172f,190f,203f,220f,232f,240f,252f})
            {
                x.d.SetProgramVariable("_offset",0f);x.d.SetProgramVariable("_anchor",Clock()-target);x.d.SetProgramVariable("_playing",true);x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);x.d.SendCustomEvent("_onDeserialization");await Task.Delay(600);
                float time=N(x.vm,"sampledTime");Need(time>=target-.05f&&time<target+2f&&x.Sources().All(s=>s.isPlaying),"Late-anchor/audio reconstruction failed at "+target);
                Need(Mathf.Abs(time-N(x.signal,"sampledTime"))<.15f,"Signal clock diverged after anchor");rows.Add(new{target,time,scene=I(x.vm,"sampledScene"),playing=B(x.d,"_playing")});
            }
            x.d.SetProgramVariable("_offset",255.8f);x.d.SetProgramVariable("_anchor",Clock());x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);await Task.Delay(800);
            Need(!B(x.d,"_playing")&&I(x.vm,"sampledState")==2&&x.legacy.menuRoot.activeInHierarchy&&x.full.continuation.activeInHierarchy,"End/replay/exploration state failed");Screenshot("interaction-finished");
            var animators=x.full.renewal.actors.Where(a=>a!=null&&a.animator!=null&&a.animator.gameObject.activeInHierarchy).Select(a=>a.animator).ToArray();
            var phases=animators.Select(a=>a.GetFloat("V4PoseTime")).ToArray();await Task.Delay(450);
            Need(animators.Length>0&&animators.Where((a,i)=>Mathf.Abs(a.GetFloat("V4PoseTime")-phases[i])>.01f).Any(),"Work stopped moving after the music ended");
            Click(x);await Task.Delay(700);Need(B(x.d,"_playing")&&N(x.vm,"sampledTime")<2&&!x.full.continuation.activeInHierarchy,"Replay did not return to clean opening");Screenshot("interaction-replay");
            return Report("interaction.json",new{success=true,anchors=rows,pauseIgnored=true,endAndReplay=true,aftermathMotionContinues=true,scope=Scope});
        }
        catch(Exception e){Report("interaction-failure.json",new{success=false,error=e.ToString(),rows});throw;}
        finally{await Cleanup(x,"interaction");}
    }
    public static async Task<object> Live()
    {
        var x=new Context();var scenes=new HashSet<int>();var events=new HashSet<int>();var focus=new HashSet<int>();var oldKeys=new HashSet<int>();var newKeys=new HashSet<int>();var support=new HashSet<int>();var stages=new HashSet<int>();var failures=new HashSet<int>();var tool=new HashSet<int>();var subtitle=new HashSet<int>();var world=new HashSet<int>();var rows=new List<object>();
        float[] shots={2f,23.52f,34.56f,44.64f,66.8f,85.12f,87.28f,89.6f,105.6f,118.8f,129.2f,137.6f,146f,151f,159.5f,164.5f,170.5f,175f,180f,184f,192f,203.5f,211f,219f,225f,232f,240f,248f,252f};
        int nextShot=0,lastSecond=-1,maxHistory=0,maxSpread=0,foleyCursor=0,received=0,corrected=0,uploaded=0;float maxAnchor=0,maxClock=0;bool repair=false,transit=false;var timer=System.Diagnostics.Stopwatch.StartNew();var audioRms=new float[4];var scratch=new float[256];
        try
        {
            await Ready(x);x.d.SendCustomEvent("StopAtStart");await Task.Delay(350);Click(x);timer.Restart();
            while(timer.Elapsed.TotalSeconds<260)
            {
                await Task.Delay(45);Need(EditorApplication.isPlaying,"Play stopped during continuous run");float t=N(x.vm,"sampledTime");bool playing=B(x.d,"_playing");
                if(playing&&t>.12f&&t<256f)
                {
                    scenes.Add(I(x.vm,"sampledScene"));int key=I(x.vm,"sampledKey");if(key>=0)newKeys.Add(key);
                    maxClock=Mathf.Max(maxClock,Mathf.Abs(t-N(x.signal,"sampledTime")));Need(maxClock<.2f,"Full signal and presentation clocks diverged");
                    if(t<133.12f)
                    {
                        var starts=(float[])x.m.GetProgramVariable("starts");var ends=(float[])x.m.GetProgramVariable("ends");var roles=(int[])x.m.GetProgramVariable("roles");var cursors=x.m.GetProgramVariable("_cursor") as int[];var windows=(Transform[])x.m.GetProgramVariable("windows");
                        if(cursors!=null)for(int i=0;i<cursors.Length;i++){int at=cursors[i];if(at>=0&&at<starts.Length&&t>=starts[at]&&t<ends[at]&&windows[i].gameObject.activeInHierarchy){events.Add(at);if(roles[at]==1&&I(x.m,"focusPhase")==1)focus.Add(at);}}
                        int old=I(x.m,"sampledKey");if(old>=0)oldKeys.Add(old);if(I(x.m,"activeSupport")>0)support.Add(I(x.m,"sampledSupport"));maxHistory=Math.Max(maxHistory,I(x.m,"activeAmbient"));
                        received=Math.Max(received,I(x.op,"receivedPieces"));corrected=Math.Max(corrected,I(x.op,"correctedCount"));uploaded=Math.Max(uploaded,I(x.op,"uploadsCompleted"));
                    }
                    if(t>=133.12f&&t<186.88f){stages.Add(I(x.overload,"sampledStage"));int f=I(x.overload,"sampledFailure");if(f>=0)failures.Add(f);repair|=B(x.overload,"sampledRepairComplete");}
                    if(t>=197.12f){tool.Add(I(x.renewal,"sampledToolState"));subtitle.Add(I(x.renewal,"sampledSubtitleState"));world.Add(I(x.renewal,"sampledWorldState"));transit|=B(x.renewal,"sampledInTransit");}
                    var sources=x.Sources();var waves=t<133.12f?(Transform[])V(x.legacy.oscilloscope).GetProgramVariable("waves"):(Transform[])x.signal.GetProgramVariable("continuationWaves");
                    for(int ch=0;ch<4;ch++)
                    {
                        maxAnchor=Mathf.Max(maxAnchor,Vector3.Distance(sources[ch].transform.position,waves[ch].position));
                        sources[ch].GetOutputData(scratch,0);double mean=scratch.Average(v=>(double)v),sum=0;foreach(float v in scratch)sum+=(v-mean)*(v-mean);audioRms[ch]=Mathf.Max(audioRms[ch],(float)Math.Sqrt(sum/scratch.Length));
                    }
                    Need(maxAnchor<.001f,"Wave/source positions diverged");
                    if(t>1&&t<255.5f)
                    {
                        int a=sources.Min(s=>s.timeSamples),b=sources.Max(s=>s.timeSamples);if(b-a>2){await Task.Delay(25);a=sources.Min(s=>s.timeSamples);b=sources.Max(s=>s.timeSamples);}maxSpread=Math.Max(maxSpread,b-a);
                    }
                    foleyCursor=Math.Max(foleyCursor,I(x.foley,"_cursor"));
                    int second=Mathf.FloorToInt(t);
                    if(second!=lastSecond)
                    {
                        lastSecond=second;rows.Add(new{time=t,scene=I(x.vm,"sampledScene"),key,eventsObserved=events.Count,overload=I(x.overload,"sampledStage"),renewalActors=I(x.renewal,"sampledVisibleActors"),transit=B(x.renewal,"sampledInTransit"),audioSamples=sources.Select(s=>s.timeSamples).ToArray(),frameMs=Time.smoothDeltaTime*1000});
                        Report("live-progress.json",new{status="running",time=t,duration=256,scenes=scenes.Count,events=events.Count,keys=oldKeys.Count+newKeys.Count,wallSeconds=timer.Elapsed.TotalSeconds});
                    }
                    if(nextShot<shots.Length&&t>=shots[nextShot]){Screenshot("live_"+shots[nextShot].ToString("000.00",System.Globalization.CultureInfo.InvariantCulture));nextShot++;}
                }
                if(!playing&&I(x.vm,"sampledState")==2&&timer.Elapsed.TotalSeconds>255)break;
            }
            Need(I(x.vm,"sampledState")==2&&x.legacy.menuRoot.activeInHierarchy,"Natural full-film end missing");Screenshot("live_finished");
            Need(scenes.Count==24,"Not all 24 scenes observed: "+scenes.Count);Need(events.Count==580&&focus.Count==15,"Front-half actual montage coverage failed: "+events.Count+" focus="+focus.Count);
            Need(oldKeys.Count==8&&newKeys.Count==4,"Keyline coverage incomplete");Need(stages.Count==4&&failures.Count==4&&repair,"Overload/failure/repair coverage incomplete");
            Need(tool.Count>=4&&subtitle.Count>=3&&world.Count>=3&&transit,"Renewal outcomes or window transit missing");Need(received==16&&corrected==8&&uploaded>=8,"Retained real operations incomplete");Need(audioRms.All(v=>v>.0001f),"No real AC signal from one or more stems");Need(maxSpread<=2,"Confirmed inter-track drift: "+maxSpread);
            var result=new{success=true,durationSeconds=timer.Elapsed.TotalSeconds,scenes=scenes.OrderBy(v=>v).ToArray(),eventsObserved=events.Count,focusHolds=focus.Count,keylines=oldKeys.Count+newKeys.Count,supportSegments=support.Count,maxHistory,overloadStages=stages.OrderBy(v=>v).ToArray(),failureStates=failures.OrderBy(v=>v).ToArray(),repairComplete=repair,toolStates=tool.OrderBy(v=>v).ToArray(),subtitleStates=subtitle.OrderBy(v=>v).ToArray(),worldStates=world.OrderBy(v=>v).ToArray(),windowTransit=transit,received,corrected,uploaded,maxWaveSourceError=maxAnchor,maxModuleClockError=maxClock,maxConfirmedAudioSpreadSamples=maxSpread,peakSourceAcRms=audioRms,foleyCursor,runtimeScreenshots=nextShot,rows,scope=Scope};
            Report("live-progress.json",new{status="complete",success=true,time=256,scenes=scenes.Count,events=events.Count});return Report("live-validation.json",result);
        }
        catch(Exception e){Report("live-progress.json",new{status="failed",success=false,error=e.Message});Report("live-failure.json",new{success=false,error=e.ToString(),events=events.OrderBy(i=>i).ToArray(),scenes=scenes.OrderBy(i=>i).ToArray(),rows});throw;}
        finally{await Cleanup(x,"live");}
    }
}
