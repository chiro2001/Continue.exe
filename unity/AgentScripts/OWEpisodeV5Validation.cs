using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using OldWebMap;
using Object=UnityEngine.Object;
using Stopwatch=System.Diagnostics.Stopwatch;
using UdonVm=VRC.Udon.UdonBehaviour;

// Self-contained Pipeline run_script entry. Enter the integrated V5 scene's
// ClientSim Play Mode first. LiveValidation requires timeout_ms >= 180000.
// Never calls another temporary builder, proxy Sample(), or preview helper.
public static class OWEpisodeV5Validation
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Scope="One actual Editor ClientSim/Udon run and desktop-camera RenderTexture evidence. Received anchors are local VM reconstruction, not real network packets. No HMD, stereo-eye or multiclient certification.";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v5");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static UdonVm Vm(UdonSharpBehaviour b)
    {if(b==null)throw new InvalidOperationException("Required V4 behaviour missing");var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm==null)throw new InvalidOperationException("Backing Udon VM missing for "+b.GetType().Name);return vm;}
    static object Value(UdonVm vm,string name)
    {var value=vm.GetProgramVariable(name);if(value==null)throw new InvalidOperationException("Udon variable missing/uninitialized: "+vm.name+"."+name);return value;}
    static float N(UdonVm vm,string name)=>Convert.ToSingle(Value(vm,name));
    static int I(UdonVm vm,string name)=>Convert.ToInt32(Value(vm,name));
    static bool B(UdonVm vm,string name)=>Convert.ToBoolean(Value(vm,name));
    static T A<T>(UdonVm vm,string name) where T:class
    {var value=Value(vm,name) as T;if(value==null)throw new InvalidOperationException("Udon variable has unexpected type: "+name+" expected "+typeof(T));return value;}
    static float[] Vec(Vector3 value)=>new[]{value.x,value.y,value.z};
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static string Report(string name,object value)
    {Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);string path=Tmp+"/reports/"+name;File.WriteAllText(path,json);return path;}
    static double Clock()=>Utilities.IsValid(Networking.LocalPlayer)?Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;
    static float Ease(float value){value=Mathf.Clamp01(value);return value*value*(3-2*value);}
    static float JourneyZ(float time)=>-(24*Ease((time-79.36f)/2.56f)+28*Ease((time-86.4f)/1.92f)+28*Ease((time-102.4f)/2.56f));
    static int OperationGroup(float time)=>time>=10.24f&&time<20.48f?0:time>=40.96f&&time<51.2f?2:time>=61.44f&&time<71.68f?1:-1;
    static int Era(float time)=>time<79.36f||time>=102.4f?0:time<86.4f?2017:2019;
    static int ActiveRenderers(GameObject root)=>root==null?0:root.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled&&r.gameObject.activeInHierarchy);

    sealed class Context
    {
        public OWEpisodeDirector director;public OWV4Presentation presentation;public OWV4Journey journey;public OWV4Montage montage;public OWV4HubCast cast;public OWV4Oscilloscope osc;public OWV4Operations operations;public OWV4CameraFx fx;public OWV4Nameplates plates;public OWV4Foley foley;
        public UdonVm d,p,j,m,c,o,op,f,np,music,foleyVm;public Camera camera;public Button button;public OWEpisodeButton handler;public UdonVm buttonVm;
        public float[] eventStarts,eventEnds;public int[] eventRoles;public float[] eventEnters,eventExits;public Vector3[] eventTo;public Transform[] windows;public float[] keyStarts;
        public readonly float[] output=new float[1024];
        public Context()
        {
            Require(EditorApplication.isPlaying,"Enter V5 ClientSim Play Mode before invoking validation; this script does not trigger a domain reload");
            Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==ScenePath,"Validation is scoped to the integrated V5 scene");
            presentation=F<OWV4Presentation>();Require(presentation!=null,"V4 presentation missing");director=presentation.director;journey=presentation.journey;montage=presentation.montage;cast=presentation.cast;osc=presentation.oscilloscope;operations=presentation.operations;fx=presentation.cameraFx;plates=cast==null?null:cast.nameplates;
            d=Vm(director);p=Vm(presentation);j=Vm(journey);m=Vm(montage);c=Vm(cast);o=Vm(osc);op=Vm(operations);f=Vm(fx);np=Vm(plates);music=Vm(director.music);foley=F<OWV4Foley>();foleyVm=foley==null?null:Vm(foley);
            camera=Camera.main;if(camera==null && presentation.fallbackCamera!=null)camera=presentation.fallbackCamera.GetComponent<Camera>();Require(camera!=null,"A real main/review Camera is required for render evidence");
            Require(presentation.performanceRoot!=null&&presentation.menuRoot!=null&&presentation.nameplateRoot!=null&&presentation.overlayRoot!=null&&presentation.startupObject!=null,"V4 presentation roots incomplete");
            handler=presentation.menuRoot.GetComponentsInChildren<OWEpisodeButton>(true).FirstOrDefault(h=>h.name=="Start performance")??presentation.menuRoot.GetComponentsInChildren<OWEpisodeButton>(true).FirstOrDefault();Require(handler!=null,"Real Start performance handler missing");button=handler.GetComponent<Button>();buttonVm=Vm(handler);Require(button!=null,"Real UI Button missing on Start performance");
        }
        public void CacheScore()
        {
            eventStarts=A<float[]>(m,"starts");eventEnds=A<float[]>(m,"ends");eventRoles=A<int[]>(m,"roles");eventEnters=A<float[]>(m,"enters");eventExits=A<float[]>(m,"exits");eventTo=A<Vector3[]>(m,"to");windows=A<Transform[]>(m,"windows");keyStarts=A<float[]>(m,"keyStarts");
        }
        public AudioSource[] Sources()=>A<AudioSource[]>(music,"sources");
    }
    static async Task Ready(Context x)
    {
        var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<5 && (!Utilities.IsValid(Networking.LocalPlayer)||x.d.GetProgramVariable("_playing")==null||x.p.GetProgramVariable("sampledState")==null))await Task.Delay(65);
        Require(Utilities.IsValid(Networking.LocalPlayer),"Actual ClientSim LocalPlayer was not ready; do not substitute preview-player labels");
        Require(!B(x.p,"offlinePreview"),"Presentation VM still has offlinePreview=true; live execution would be bypassed");
        Require(B(x.d,"continuousPerformance")&&B(x.d,"externalStageSampler"),"Continuous V4 director/external stage integration not enabled");
        Require(I(x.buttonVm,"action")==1,"Start/replay VM must invoke Restart (action 1)");
        Require(x.Sources().Length==4&&x.Sources().All(s=>s!=null&&s.clip!=null),"Four real audio clips required");
    }
    static async Task RunningReady(Context x)
    {
        // Performed roots start inactive. Do not assume their VM private state is
        // initialized until the actual UI has opened and Presentation sampled them.
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed.TotalSeconds<3 && (!x.presentation.performanceRoot.activeInHierarchy || x.m.GetProgramVariable("_cursor")==null || N(x.p,"sampledTime")<.12f))await Task.Delay(20);
        Require(x.presentation.performanceRoot.activeInHierarchy&&x.m.GetProgramVariable("_cursor")!=null,"Presentation did not initialize the performed VM after the actual UI click");
        foreach(var vm in new[]{x.j,x.c,x.m,x.o,x.op,x.f,x.np})Require(!B(vm,"autoSample"),"Presentation is not the only clock owner: "+vm.name+".autoSample=true");
        x.CacheScore();
    }
    static void Click(Context x)
    {
        Require(EventSystem.current!=null,"The actual UI EventSystem is missing");Require(x.button.gameObject.activeInHierarchy&&x.button.IsActive()&&x.button.interactable&&x.button.IsInteractable(),"UI button is inactive or not interactable");
        bool dispatched=ExecuteEvents.Execute(x.button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,clickCount=1},ExecuteEvents.pointerClickHandler);Require(dispatched,"Actual pointerClick was not handled by the UI Button");
    }
    static bool PersistentButton(Context x)
    {
        for(int i=0;i<x.button.onClick.GetPersistentEventCount();i++)if(x.button.onClick.GetPersistentTarget(i)==x.buttonVm && x.button.onClick.GetPersistentMethodName(i)=="SendCustomEvent")return true;return false;
    }
    static object Modules(Context x,bool assert)
    {
        float master=N(x.p,"sampledTime");float journey=N(x.j,"sampledTime"),cast=N(x.c,"sampledTime"),montage=N(x.m,"sampledTime"),wave=N(x.o,"sampledTime"),operations=N(x.op,"sampledTime"),fx=N(x.f,"sampledTime"),nameplates=N(x.np,"lastSampledTime");
        float maxDelta=new[]{Mathf.Abs(master-journey),Mathf.Abs(master-cast),Mathf.Abs(master-montage),Mathf.Abs(master-wave),Mathf.Abs(master-operations),Mathf.Abs(master-fx),Mathf.Abs(master-nameplates)}.Max();
        float zExpected=JourneyZ(journey),zActual=x.journey.worldFrame.localPosition.z,zError=Mathf.Abs(zActual-zExpected);
        float particleAnchorError=0;
        if(assert)
        {
            Require(maxDelta<.15f,"A V4 module did not follow the presentation clock: max "+maxDelta+" at "+master);
            Require(zError<.002f,"Shared world-frame z disagrees with the independent journey formula at "+journey+": "+zActual+" / "+zExpected);
            Require(I(x.op,"sampledGroup")==OperationGroup(operations),"Operations VM is in the wrong new chronology group at "+operations);
            Require(I(x.c,"visibleEra")==Era(cast),"Hub VM era disagrees with its actual sample time");
            Require(I(x.np,"sampledEra")==Era(N(x.np,"lastSampledTime")),"Nameplate VM era is stale");
            var waves=A<Transform[]>(x.o,"waves");var sources=x.Sources();Require(waves.Length==4,"Active regional waveform VM array is not four voices");
            for(int ch=0;ch<4;ch++)Require(Vector3.Distance(waves[ch].position,sources[ch].transform.position)<.0005f,"Real source and active regional waveform differ for channel "+ch+" at "+master);
            var anchors=A<Transform[]>(x.o,"particleAnchors");Require(anchors.Length>=4,"Four world-space particle anchors are required");
            for(int ch=0;ch<4;ch++){particleAnchorError=Mathf.Max(particleAnchorError,Vector3.Distance(anchors[ch].position,waves[ch].position));Require(Vector3.Distance(anchors[ch].position,waves[ch].position)<.0005f,"Particle anchor and active waveform differ in world space for channel "+ch+" at "+master);}

        }
        float v5Delta=0;foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(false).Where(b=>b.GetType().Name.StartsWith("OWV5")&&b.gameObject.activeInHierarchy)){var vm=Vm(b);var value=vm.GetProgramVariable("sampledTime");if(value!=null)v5Delta=Mathf.Max(v5Delta,Mathf.Abs(Convert.ToSingle(value)-master));}
        if(assert)Require(v5Delta<.15f,"A V5 module did not follow the director clock: "+v5Delta+" at "+master);
        return new{master,journey,cast,montage,wave,operations,fx,nameplates,maxDelta,v5Delta,zExpected,zActual,zError,particleAnchorError,operationGroup=I(x.op,"sampledGroup"),era=I(x.c,"visibleEra")};
    }
    static object Waiting(Context x,int state)
    {
        Require(!B(x.d,"_playing")&&!B(x.p,"sampledPlaying")&&I(x.p,"sampledState")==state,"Director/presentation did not reach waiting or natural-end VM state");
        Require(x.presentation.menuRoot.activeInHierarchy&&!x.presentation.performanceRoot.activeInHierarchy&&!x.presentation.nameplateRoot.activeInHierarchy&&!x.presentation.overlayRoot.activeInHierarchy,"Waiting/end leaked performed world, nameplates or camera overlay");
        Require(x.presentation.startupObject.gameObject.activeInHierarchy&&ActiveRenderers(x.presentation.startupObject.gameObject)>=1,"Waiting must show the V5 cracktro startup window");
        // Native V4 keylines are children of performanceRoot. Legacy caption
        // references are optional; if present, they must also be hidden/cleared.
        if(x.presentation.captionRoot!=null)Require(!x.presentation.captionRoot.activeInHierarchy,"Waiting/end leaked the optional legacy caption root");
        if(x.presentation.caption!=null)Require(string.IsNullOrEmpty(x.presentation.caption.text),"Waiting/end still contains the optional legacy caption");
        Require(ActiveRenderers(x.presentation.performanceRoot)==0&&ActiveRenderers(x.presentation.nameplateRoot)==0&&ActiveRenderers(x.presentation.overlayRoot)==0,"A hidden performance root still renders in the hierarchy");
        Require(x.Sources().All(s=>!s.isPlaying),"Real music sources did not stop in waiting/end");
        return new{state,elapsed=N(x.d,"_elapsed"),menu=x.presentation.menuRoot.activeInHierarchy,performedRenderers=ActiveRenderers(x.presentation.performanceRoot),terminalRenderers=ActiveRenderers(x.presentation.startupObject.gameObject),caption=x.presentation.caption==null?null:x.presentation.caption.text};
    }
    static object Menu(Context x)
    {
        Canvas.ForceUpdateCanvases();var rect=x.button.GetComponent<RectTransform>();var center=rect.TransformPoint(rect.rect.center);var viewport=x.camera.WorldToViewportPoint(center);
        bool inView=viewport.z>x.camera.nearClipPlane&&viewport.x>0&&viewport.x<1&&viewport.y>0&&viewport.y<1;
        Vector3 delta=center-x.camera.transform.position;RaycastHit hit;Physics.SyncTransforms();bool ray=Physics.Raycast(x.camera.transform.position,delta.normalized,out hit,delta.magnitude+.12f,1<<17,QueryTriggerInteraction.Ignore);
        bool rightCollider=ray&&(hit.collider.gameObject==x.presentation.menuRoot || hit.collider.transform.IsChildOf(x.presentation.menuRoot.transform));
        Require(x.presentation.menuRoot.activeInHierarchy&&x.button.interactable&&x.button.IsInteractable()&&inView&&rightCollider,"Actual menu/button is not visible/interactable/layer-17 ray reachable");
        int visibleGlyphs=0;foreach(var text in x.presentation.menuRoot.GetComponentsInChildren<TMP_Text>(false)){text.ForceMeshUpdate(false,false);if(text.textInfo.characterInfo!=null)visibleGlyphs+=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Count(c=>c.isVisible);}
        Require(visibleGlyphs>0,"Actual start/replay menu has no visible TMP mesh");
        return new{viewport=Vec(viewport),inView,ray=rightCollider,collider=ray?hit.collider.name:"",interactable=x.button.interactable,visibleGlyphs,camera=x.camera.name,menuTitle=x.presentation.menuTitle.text,playLabel=x.presentation.playLabel.text};
    }
    sealed class FrameInfo
    {
        public string path,sha256,camera;public int width,height,brightPixels,roiPixels;public double mean,stddev;public float[] menuViewport;
    }
    static FrameInfo Frame(Context x,string filename,bool menuEvidence=false,int width=1600,int height=900)
    {
        Directory.CreateDirectory(Tmp+"/runtime");string path=Tmp+"/runtime/"+filename;var camera=x.camera;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply(false,false);var png=image.EncodeToPNG();Require(png!=null&&png.Length>1024,"Actual camera produced no useful screenshot");File.WriteAllBytes(path,png);
            int x0=0,y0=0,x1=width-1,y1=height-1;float[] viewport=null;
            if(menuEvidence)
            {
                var rect=x.presentation.menuRoot.GetComponent<RectTransform>();var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(camera.WorldToViewportPoint).ToArray();
                x0=Mathf.Clamp(Mathf.FloorToInt(points.Min(p=>p.x)*width),0,width-1);x1=Mathf.Clamp(Mathf.CeilToInt(points.Max(p=>p.x)*width),0,width-1);y0=Mathf.Clamp(Mathf.FloorToInt(points.Min(p=>p.y)*height),0,height-1);y1=Mathf.Clamp(Mathf.CeilToInt(points.Max(p=>p.y)*height),0,height-1);viewport=Vec(camera.WorldToViewportPoint(rect.TransformPoint(rect.rect.center)));
            }
            var pixels=image.GetPixels32();double sum=0,squared=0;int count=0,bright=0;
            for(int y=y0;y<=y1;y+=2)for(int px=x0;px<=x1;px+=2){var color=pixels[y*width+px];double l=(color.r*.2126+color.g*.7152+color.b*.0722)/255;sum+=l;squared+=l*l;count++;if(l>.55)bright++;}
            double mean=count>0?sum/count:0,stddev=Math.Sqrt(Math.Max(0,count>0?squared/count-mean*mean:0));
            if(menuEvidence)Require(count>100&&bright>30&&stddev>.015,"Rendered menu ROI is blank/flat despite hierarchy flags: "+filename);
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant();
            return new FrameInfo{path=path,sha256=hash,camera=camera.name,width=width,height=height,brightPixels=bright,roiPixels=count,mean=mean,stddev=stddev,menuViewport=viewport};
        }
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static object Nameplate(Context x,bool assert)
    {
        var local=Networking.LocalPlayer;Require(Utilities.IsValid(local),"ClientSim LocalPlayer disappeared");int era=I(x.np,"sampledEra");var ids=A<int[]>(x.np,"playerIds");int slot=Array.IndexOf(ids,local.playerId);bool registered=slot>=0;
        bool visible=registered&&x.plates.playerPlates[slot].gameObject.activeInHierarchy;string label=registered?x.plates.playerLabels[slot].text:"";
        if(assert){Require(!B(x.np,"previewMode"),"Preview players leaked into live nameplates");Require(registered&&label=="xxxx"&&visible,"Actual local player is missing its anonymous era plate");Require(x.plates.actorLabels.All(text=>text.text=="xxxx")&&x.plates.playerLabels.All(text=>text.text=="xxxx"),"A real or fictional display name leaked into a plate");}
        return new{era,localId=local.playerId,anonymous=true,slot,label,registered,visible,activePlayers=I(x.np,"activePlayerCount"),visiblePlayers=I(x.np,"visiblePlayerCount"),ids=ids.ToArray()};
    }
    static async Task Cleanup(Context x,string entry)
    {
        try
        {
            if(EditorApplication.isPlaying&&x.d!=null){x.d.SendCustomEvent("StopAtStart");await Task.Delay(260);Report(entry+"-cleanup.json",new{playing=B(x.d,"_playing"),elapsed=N(x.d,"_elapsed"),presentationState=I(x.p,"sampledState"),menu=x.presentation.menuRoot.activeInHierarchy,performance=x.presentation.performanceRoot.activeInHierarchy});}
        }
        catch(Exception e){Report(entry+"-cleanup.json",new{cleanupError=e.ToString()});}
    }
    public static async Task<object> InteractionChecks()
    {
        var x=new Context();var rows=new List<object>();var shots=new List<FrameInfo>();string phase="ready";
        try
        {
            await Ready(x);Require(PersistentButton(x),"Real UI button is not persistently wired to backing Udon SendCustomEvent");
            x.d.SendCustomEvent("StopAtStart");await Task.Delay(420);phase="waiting";rows.Add(new{step=phase,state=Waiting(x,0),menu=Menu(x)});shots.Add(Frame(x,"interaction_waiting.png",true));
            phase="start_pointer_click";Click(x);await Task.Delay(850);await RunningReady(x);
            Require(B(x.d,"_playing")&&N(x.d,"_elapsed")>.25f&&x.presentation.performanceRoot.activeInHierarchy&&!x.presentation.menuRoot.activeInHierarchy&&!x.presentation.startupObject.gameObject.activeInHierarchy,"Real UI click did not open the performance and hide the menu");
            rows.Add(new{step=phase,time=N(x.d,"_elapsed"),modules=Modules(x,true)});shots.Add(Frame(x,"interaction_started.png"));
            phase="pause_ignored";float before=N(x.d,"_elapsed");x.d.SendCustomEvent("Pause");await Task.Delay(420);Require(B(x.d,"_playing")&&N(x.d,"_elapsed")>before+.2f&&x.Sources().All(s=>s.isPlaying),"Pause interrupted a continuous performance");rows.Add(new{step=phase,before,after=N(x.d,"_elapsed")});
            foreach(float target in new[]{17.2f,44.64f,66.8f,85.12f,89.6f,118.8f,129.2f})
            {
                phase="received_anchor_"+target.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
                x.d.SetProgramVariable("_offset",0f);x.d.SetProgramVariable("_anchor",Clock()-target);x.d.SetProgramVariable("_playing",true);x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);x.d.SendCustomEvent("_onDeserialization");await Task.Delay(520);
                float received=N(x.d,"_elapsed");Require(received>=target-.03f&&received<target+1.8f&&B(x.d,"_playing")&&x.Sources().All(s=>s.isPlaying),"Received-anchor VM reconstruction/audio failed at "+target);
                object names=Era(N(x.c,"sampledTime"))!=0?Nameplate(x,true):null;
                rows.Add(new{step=phase,target,received,modules=Modules(x,true),nameplate=names,readme=I(x.op,"readmeCharacters"),pieces=I(x.op,"receivedPieces"),corrected=I(x.op,"correctedCount"),sourceSamples=x.Sources().Select(s=>s.timeSamples).ToArray()});
            }
            phase="natural_end";x.d.SetProgramVariable("_offset",133f);x.d.SetProgramVariable("_anchor",Clock());x.d.SetProgramVariable("_revision",I(x.d,"_revision")+1);await Task.Delay(750);
            rows.Add(new{step=phase,state=Waiting(x,2),menu=Menu(x)});shots.Add(Frame(x,"interaction_finished.png",true));
            phase="replay_pointer_click";Click(x);await Task.Delay(850);await RunningReady(x);float replay=N(x.d,"_elapsed");Require(B(x.d,"_playing")&&replay>.15f&&replay<1.5f&&x.presentation.performanceRoot.activeInHierarchy&&!x.presentation.menuRoot.activeInHierarchy,"Ended real UI did not restart the performance from zero");
            rows.Add(new{step=phase,replay,modules=Modules(x,true)});shots.Add(Frame(x,"interaction_replay.png"));
            phase="restore_waiting";x.d.SendCustomEvent("StopAtStart");await Task.Delay(300);rows.Add(new{step=phase,state=Waiting(x,0),menu=Menu(x)});
            string report=Report("interaction.json",new{success=true,persistentUiBackingVerified=true,rows,shots,scope=Scope});return new{success=true,receivedAnchors=7,screenshots=shots.Count,report,scope=Scope};
        }
        catch(Exception e){string path=Report("interaction-failure.json",new{phase,error=e.ToString(),rows,shots,scope=Scope});throw new InvalidOperationException("V4 interaction failed at "+phase+"; "+path+"; "+e.Message,e);}
        finally{await Cleanup(x,"interaction");}
    }
    sealed class LiveState
    {
        public readonly HashSet<int> events=new HashSet<int>(),focus=new HashSet<int>(),focusHolds=new HashSet<int>(),keys=new HashSet<int>(),eras=new HashSet<int>(),groups=new HashSet<int>(),conditionPhases=new HashSet<int>(),supportedFocus=new HashSet<int>(),supportSegments=new HashSet<int>();
        public readonly List<object> rows=new List<object>(),transients=new List<object>(),nameplateRows=new List<object>();public readonly List<FrameInfo> shots=new List<FrameInfo>();public readonly List<float> frameTimes=new List<float>();
        public readonly float[] rms=new float[4],gateMin={float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity},gateMax={float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity};public readonly bool[] lockedSeen=new bool[3],openSeen=new bool[3];
        public int maxRaw,maxConfirmed,maxHistory,earlyHistory,lateHistory,maxActualHistory,maxComments,maxFocus,maxPeriphery,maxSupport,maxParticles,maxPieces,maxCorrected,maxUploads,maxDelivered,foleyCursor;public float maxSceneAudioError,maxDspAudioError,maxJourneyError,maxModuleError,maxAnchorError,maxParticleAnchorError,maxFocusDrift,listenerRms;public bool localPlate2017,localPlate2019,oldHubGeometry,newHubGeometry,archivedCopy;public int oldHubFrustum,newHubFrustum;
    }
    static async Task AudioProbe(Context x,LiveState s)
    {
        var sources=x.Sources();Require(sources.All(source=>source.isPlaying),"A real stem stopped during the performance at "+N(x.d,"_elapsed"));
        var first=sources.Select(source=>source.timeSamples).ToArray();int raw=first.Max()-first.Min(),confirmed=raw;s.maxRaw=Math.Max(s.maxRaw,raw);
        if(raw>960)
        {
            float before=N(x.d,"_elapsed");double dspBefore=AudioSettings.dspTime;await Task.Delay(65);Require(EditorApplication.isPlaying,"Play Mode stopped during the audio confirmation read");
            var second=sources.Select(source=>source.timeSamples).ToArray();confirmed=second.Max()-second.Min();s.transients.Add(new{time=before,raw,first,confirmed,second,dspBefore,dspAfter=AudioSettings.dspTime});
        }
        s.maxConfirmed=Math.Max(s.maxConfirmed,confirmed);Require(confirmed<=1024,"Persistent inter-stem sample spread exceeds 1024 after the 65ms confirmation: "+confirmed+" at "+N(x.d,"_elapsed"));
        float now=N(x.p,"sampledTime");s.maxSceneAudioError=Mathf.Max(s.maxSceneAudioError,Mathf.Abs(sources[0].time-now));
        float scheduledAt=N(x.music,"lastScheduleDsp"),scheduledPosition=N(x.music,"lastScheduledTime");double expected=scheduledPosition+Math.Max(0,AudioSettings.dspTime-scheduledAt);
        if(scheduledAt>0)for(int ch=0;ch<4;ch++)s.maxDspAudioError=Mathf.Max(s.maxDspAudioError,Mathf.Abs(sources[ch].time-(float)expected));
        for(int ch=0;ch<4;ch++)
        {
            sources[ch].GetOutputData(x.output,0);double sum=0;for(int i=0;i<x.output.Length;i++)sum+=x.output[i]*x.output[i];s.rms[ch]=Mathf.Max(s.rms[ch],(float)Math.Sqrt(sum/x.output.Length));
        }
        AudioListener.GetOutputData(x.output,0);double listener=0;for(int i=0;i<x.output.Length;i++)listener+=x.output[i]*x.output[i];s.listenerRms=Mathf.Max(s.listenerRms,(float)Math.Sqrt(listener/x.output.Length));
    }
    static float GateAngle(int gate,float time)
    {
        float angle=gate==0?90*(1-Ease((time-104.96f)/(109-104.96f)))+90*Ease((time-115.2f)/(117.76f-115.2f)):
            gate==1?90*(1-Ease((time-109)/(112.64f-109)))+90*Ease((time-118.4f)/(120.32f-118.4f)):
            90*(1-Ease((time-112.64f)/(117.76f-112.64f)))+95*Ease((time-120.32f)/(122.88f-120.32f));return Mathf.Clamp(angle,0,95);
    }
    static bool GateLocked(int gate,float time)=>gate==0?time>=107.52f&&time<117.76f:gate==1?time>=112.64f&&time<120.32f:time>=117.76f&&time<122.88f;
    static int FrustumRenderers(Camera camera,Transform root)
    {
        if(root==null||!root.gameObject.activeInHierarchy)return 0;var planes=GeometryUtility.CalculateFrustumPlanes(camera);return root.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled&&r.gameObject.activeInHierarchy&&(camera.cullingMask&(1<<r.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(planes,r.bounds));
    }
    static void Observe(Context x,LiveState s,bool nativeChecks)
    {
        float t=N(x.m,"sampledTime");var cursors=x.m.GetProgramVariable("_cursor") as int[];
        if(cursors==null){Require(N(x.p,"sampledTime")<1,"V4 montage VM never initialized its actual event cursors");return;}
        Require(cursors.Length==x.windows.Length,"Actual montage cursor/slot dimensions differ");
        for(int slot=0;slot<cursors.Length;slot++)
        {
            int index=cursors[slot];if(index<0 || index>=x.eventStarts.Length || t<x.eventStarts[index] || t>=x.eventEnds[index] || !x.windows[slot].gameObject.activeInHierarchy)continue;
            s.events.Add(index);
            if(x.eventRoles[index]==1)
            {
                s.focus.Add(index);
                if(t>=x.eventStarts[index]+x.eventEnters[index]+.05f&&t<x.eventEnds[index]-x.eventExits[index]-.05f)
                {
                    s.focusHolds.Add(index);if(t>=x.eventStarts[index]+x.eventEnters[index]+.12f&&t<x.eventEnds[index]-x.eventExits[index]-.12f){Require(I(x.m,"activeSupport")>=2,"A held central focus lacks its auxiliary images");s.supportedFocus.Add(index);}float error=Vector3.Distance(x.windows[slot].localPosition,x.eventTo[index]);s.maxFocusDrift=Mathf.Max(s.maxFocusDrift,error);Require(error<.05f,"A live central focus did not hold near its authored anchor");
                }
            }
        }
        int support=I(x.m,"activeSupport");Require(support>=0&&support<=4,"Central support pool exceeded four planes");s.maxSupport=Math.Max(s.maxSupport,support);if(support>0)s.supportSegments.Add(I(x.m,"sampledSupport"));
        int key=I(x.m,"sampledKey");if(key>=0)s.keys.Add(key);
        int history=I(x.m,"activeAmbient");Require(history>=0&&history<=48,"Live VM exceeded the 48-history-window budget");s.maxHistory=Math.Max(s.maxHistory,history);if(t<30.72f)s.earlyHistory=Math.Max(s.earlyHistory,history);if(t>115.65f)s.lateHistory=Math.Max(s.lateHistory,history);
        s.maxFocus=Math.Max(s.maxFocus,I(x.m,"activeFocus"));s.maxPeriphery=Math.Max(s.maxPeriphery,I(x.m,"activePeriphery"));s.maxComments=Math.Max(s.maxComments,I(x.m,"activeComments"));
        int era=I(x.c,"visibleEra");if(era!=0)s.eras.Add(era);int group=I(x.op,"sampledGroup");if(group>=0)s.groups.Add(group);s.conditionPhases.Add(I(x.c,"conditionsPhase"));
        s.maxPieces=Math.Max(s.maxPieces,I(x.op,"receivedPieces"));s.maxCorrected=Math.Max(s.maxCorrected,I(x.op,"correctedCount"));s.maxUploads=Math.Max(s.maxUploads,I(x.op,"uploadsStarted"));s.maxDelivered=Math.Max(s.maxDelivered,I(x.op,"uploadsCompleted"));
        var waves=A<Transform[]>(x.o,"waves");var sources=x.Sources();for(int ch=0;ch<4;ch++){float error=Vector3.Distance(waves[ch].position,sources[ch].transform.position);s.maxAnchorError=Mathf.Max(s.maxAnchorError,error);Require(error<.0005f,"Active regional waveform and real source are not colocated");}
        var particleAnchors=A<Transform[]>(x.o,"particleAnchors");Require(particleAnchors.Length>=4,"Four particle anchors missing");
        for(int ch=0;ch<4;ch++){float error=Vector3.Distance(particleAnchors[ch].position,waves[ch].position);s.maxParticleAnchorError=Mathf.Max(s.maxParticleAnchorError,error);Require(error<.0005f,"Live particle anchor retained a local-coordinate value as world position");}
        float jt=N(x.j,"sampledTime"),worldError=Mathf.Abs(x.journey.worldFrame.localPosition.z-JourneyZ(jt));s.maxJourneyError=Mathf.Max(s.maxJourneyError,worldError);Require(worldError<.002f,"Actual world frame stopped following the journey formula");
        float master=N(x.p,"sampledTime");float moduleError=new[]{Mathf.Abs(master-N(x.j,"sampledTime")),Mathf.Abs(master-N(x.c,"sampledTime")),Mathf.Abs(master-N(x.m,"sampledTime")),Mathf.Abs(master-N(x.o,"sampledTime")),Mathf.Abs(master-N(x.op,"sampledTime")),Mathf.Abs(master-N(x.f,"sampledTime")),Mathf.Abs(master-N(x.np,"lastSampledTime"))}.Max();s.maxModuleError=Mathf.Max(s.maxModuleError,moduleError);Require(moduleError<.15f,"Presentation module clocks diverged");
        Require(group==OperationGroup(N(x.op,"sampledTime")),"Live operation group uses an old chronology");
        var angles=A<float[]>(x.c,"sampledGateAngles");var locked=A<bool[]>(x.c,"sampledGateLocked");Require(angles.Length>=3&&locked.Length>=3,"Live cast lacks the three condition barriers");float castTime=N(x.c,"sampledTime");
        for(int gate=0;gate<3;gate++)
        {
            Require(Mathf.Abs(angles[gate]-GateAngle(gate,castTime))<.03f&&locked[gate]==GateLocked(gate,castTime),"Live condition barrier state differs from its sampled action at "+castTime);
            if(castTime>=104.96f){s.gateMin[gate]=Mathf.Min(s.gateMin[gate],angles[gate]);s.gateMax[gate]=Mathf.Max(s.gateMax[gate],angles[gate]);s.lockedSeen[gate]|=locked[gate];s.openSeen[gate]|=!locked[gate];}
        }
        if(x.foleyVm!=null)s.foleyCursor=Math.Max(s.foleyCursor,I(x.foleyVm,"_cursor"));
        s.maxParticles=Math.Max(s.maxParticles,A<ParticleSystem[]>(x.o,"particles").Where(p=>p!=null).Sum(p=>p.particleCount));s.frameTimes.Add(Time.smoothDeltaTime*1000);
        if(!nativeChecks)return;
        Modules(x,true);int actualSupport=x.montage.supportPlanes.Count(w=>w!=null&&w.gameObject.activeInHierarchy);Require(actualSupport==support,"Hierarchy and VM support counts disagree");int actualHistory=x.montage.ambientWindows.Count(w=>w!=null&&w.gameObject.activeInHierarchy);Require(actualHistory==history&&actualHistory<=48,"Hierarchy and VM history budgets disagree");s.maxActualHistory=Math.Max(s.maxActualHistory,actualHistory);
        if(key>=0)
        {
            var keyRegions=A<int[]>(x.m,"keyRegions");var keyStrings=A<string[]>(x.m,"keyStrings");int region=keyRegions[key];var text=x.montage.keyTexts[region];Require(x.montage.keyRoots[region].gameObject.activeInHierarchy&&text.text==keyStrings[key],"VM keyline changed but its actual text/root did not");text.ForceMeshUpdate(false,false);Require(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(info=>info.isVisible),"Live keyline has zero visible glyphs");
        }
        if(era!=0)
        {
            object plate=Nameplate(x,true);if(era==2017)s.localPlate2017=true;else if(era==2019)s.localPlate2019=true;if(s.nameplateRows.Count<40)s.nameplateRows.Add(new{time=castTime,plate});
            int old=FrustumRenderers(x.camera,x.cast.hub2017),modern=FrustumRenderers(x.camera,x.cast.hub2019);s.oldHubFrustum=Math.Max(s.oldHubFrustum,old);s.newHubFrustum=Math.Max(s.newHubFrustum,modern);if(era==2017&&old>0)s.oldHubGeometry=true;if(era==2019&&modern>0)s.newHubGeometry=true;
        }
        if(castTime>=100)
        {
            Require(x.cast.conditionsRoot.gameObject.activeInHierarchy,"Condition barriers exist in VM but their actual scenery is hidden");
            for(int gate=0;gate<3;gate++)
            {
                Require(x.cast.barrierLockedSigns[gate].activeInHierarchy==locked[gate]&&x.cast.barrierOpenSigns[gate].activeInHierarchy==!locked[gate],"Actual barrier lock/open signs disagree with the VM");
                float actual=Mathf.Abs(Mathf.DeltaAngle(0,x.cast.barrierLeftLeaves[gate].localEulerAngles.y));Require(Mathf.Abs(actual-angles[gate])<.05f,"Actual barrier mesh did not rotate with the VM angle");
            }
        }
        if(castTime>=128){Require(x.cast.archivedCopy.activeInHierarchy,"Maintenance VM phase reached save but the archived copy is hidden");s.archivedCopy=true;}
    }
    static float Percentile(List<float> values,float fraction)
    {if(values.Count==0)return 0;var sorted=values.OrderBy(v=>v).ToArray();return sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Length-1)*fraction),0,sorted.Length-1)];}
    public static async Task<object> LiveValidation()
    {
        var x=new Context();var s=new LiveState();var targets=new[]{2f,13.6f,23.52f,34.56f,44.64f,50.8f,55.36f,66.8f,74.88f,85.12f,89.6f,105.6f,118.8f,129.2f};var phase="ready";Stopwatch watch=null;int nextShot=0,lastSecond=-1;double nextProgress=0;
        AudioSettings.GetDSPBufferSize(out int bufferLength,out int bufferCount);
        try
        {
            await Ready(x);x.d.SendCustomEvent("StopAtStart");await Task.Delay(350);Waiting(x,0);Menu(x);Require(PersistentButton(x),"Live start is not connected to real backing Udon");phase="complete_performance";Click(x);watch=Stopwatch.StartNew();await RunningReady(x);
            while(watch.Elapsed.TotalSeconds<136)
            {
                await Task.Delay(65);Require(EditorApplication.isPlaying,"ClientSim Play Mode stopped before the 136-second observation completed");float t=N(x.p,"sampledTime");bool playing=B(x.d,"_playing");
                if(playing&&t>=.15f&&t<133.12f)
                {
                    int second=Mathf.FloorToInt(t);bool native=second!=lastSecond;Observe(x,s,native);
                    if(t>.5f&&t<132.7f){await AudioProbe(x,s);t=N(x.p,"sampledTime");if(B(x.d,"_playing")&&t<133.12f)Observe(x,s,false);}
                    if(second!=lastSecond)
                    {
                        lastSecond=second;s.rows.Add(new{time=t,wallSeconds=watch.Elapsed.TotalSeconds,focus=I(x.m,"activeFocus"),periphery=I(x.m,"activePeriphery"),history=I(x.m,"activeAmbient"),support=I(x.m,"activeSupport"),comments=I(x.m,"activeComments"),key=I(x.m,"sampledKey"),era=I(x.c,"visibleEra"),operationGroup=I(x.op,"sampledGroup"),conditionsPhase=I(x.c,"conditionsPhase"),gateAngles=A<float[]>(x.c,"sampledGateAngles").ToArray(),gateLocked=A<bool[]>(x.c,"sampledGateLocked").ToArray(),worldFrame=Vec(x.journey.worldFrame.localPosition),open=N(x.o,"sampledOpen"),particles=A<ParticleSystem[]>(x.o,"particles").Select(p=>p==null?0:p.particleCount).ToArray(),audioSamples=x.Sources().Select(source=>source.timeSamples).ToArray(),audioDsp=AudioSettings.dspTime,scheduleCount=I(x.music,"scheduleCount"),frameMilliseconds=Time.smoothDeltaTime*1000});
                    }
                    if(nextShot<targets.Length&&t>=targets[nextShot])
                    {
                        Require(t-targets[nextShot]<1.5f,"Runtime screenshot target was skipped by a long stall: "+targets[nextShot]);s.shots.Add(Frame(x,"live_"+(nextShot+1).ToString("00")+"_"+targets[nextShot].ToString("000.00",System.Globalization.CultureInfo.InvariantCulture)+".png"));nextShot++;
                    }
                }
                if(watch.Elapsed.TotalSeconds>=nextProgress)
                {
                    nextProgress=watch.Elapsed.TotalSeconds+1;string json=JsonConvert.SerializeObject(new{status="running",time=N(x.p,"sampledTime"),wallSeconds=watch.Elapsed.TotalSeconds,playing=B(x.d,"_playing"),eventsObserved=s.events.Count,eventsTotal=x.eventStarts.Length,focusObserved=s.focus.Count,keylinesObserved=s.keys.Count,historyMax=s.maxHistory,confirmedSpread=s.maxConfirmed,screenshots=s.shots.Count},Formatting.Indented);Directory.CreateDirectory(Tmp+"/reports");File.WriteAllText(Tmp+"/reports/live-progress.json",json);SessionState.SetString("EpisodeV4LiveProgress",json);
                }
            }
            phase="natural_end_ui";object ended=Waiting(x,2),menu=Menu(x);var endFrame=Frame(x,"live_finished.png",true);
            int expectedFocus=x.eventRoles.Count(role=>role==1);var missing=Enumerable.Range(0,x.eventStarts.Length).Where(i=>!s.events.Contains(i)).ToArray();
            Require(missing.Length==0,"Not every actual VM/hierarchy event was observed: "+s.events.Count+"/"+x.eventStarts.Length+"; missing "+string.Join(",",missing.Take(20)));
            Require(s.focus.Count==expectedFocus&&s.focusHolds.Count==expectedFocus,"Not every central focus had a live observed hold");Require(s.keys.Count==x.keyStarts.Length,"Not every keyline change was observed in the actual VM");
            Require(s.rms.All(r=>r>.0001f)&&s.listenerRms>.0001f,"The full run did not produce real output from all four stems and the listener");
            Require(s.eras.Contains(2017)&&s.eras.Contains(2019)&&s.oldHubGeometry&&s.newHubGeometry,"Both Hub eras were not observed as active geometry in the camera frustum");Require(s.localPlate2017&&s.localPlate2019,"Actual LocalPlayer did not retain its anonymous plate in both eras");
            Require(s.groups.Contains(0)&&s.groups.Contains(1)&&s.groups.Contains(2)&&s.maxPieces==16&&s.maxCorrected==8&&s.maxUploads>=8&&s.maxDelivered>=8,"The real operations did not reach README/BT/subtitle completion");
            Require(s.maxHistory<=48&&s.maxActualHistory<=48&&s.maxHistory>=32&&s.maxActualHistory>=32&&s.lateHistory>s.earlyHistory,"History windows did not grow within their spacing and 48-window cap");
            Require(s.supportedFocus.Count==expectedFocus&&s.supportSegments.Count==A<float[]>(x.m,"supportStarts").Length,"Central auxiliary images were not observed across every authored scene");
            Require(s.lockedSeen.All(v=>v)&&s.openSeen.All(v=>v)&&Enumerable.Range(0,3).All(i=>s.gateMax[i]-s.gateMin[i]>30)&&s.archivedCopy,"Barrier closure/recovery or maintenance save was not observed");Require(s.shots.Count==14,"Exactly fourteen runtime stage screenshots are required");
            var result=new{success=true,durationSeconds=watch.Elapsed.TotalSeconds,eventsObserved=s.events.Count,eventsTotal=x.eventStarts.Length,missingEvents=missing,focusObserved=s.focus.Count,focusHolds=s.focusHolds.Count,supportedFocus=s.supportedFocus.Count,supportSegmentsObserved=s.supportSegments.Count,maxSupport=s.maxSupport,keylinesObserved=s.keys.Count,keylinesTotal=x.keyStarts.Length,eras=s.eras.OrderBy(e=>e).ToArray(),operationGroups=s.groups.OrderBy(g=>g).ToArray(),peakSourceRms=s.rms,peakListenerRms=s.listenerRms,maxRawSpread=s.maxRaw,maxConfirmedSpread=s.maxConfirmed,dspBufferLength=bufferLength,dspBufferCount=bufferCount,maxSceneAudioClockError=s.maxSceneAudioError,maxDspScheduleClockError=s.maxDspAudioError,maxJourneyPositionError=s.maxJourneyError,maxModuleTimeDelta=s.maxModuleError,maxWaveSourceError=s.maxAnchorError,maxParticleAnchorError=s.maxParticleAnchorError,maxFocusAnchorDrift=s.maxFocusDrift,history=new{early=s.earlyHistory,late=s.lateHistory,vmMax=s.maxHistory,actualMax=s.maxActualHistory},s.maxComments,s.maxFocus,s.maxPeriphery,s.maxParticles,s.maxPieces,s.maxCorrected,s.maxUploads,s.maxDelivered,gateMinimum=s.gateMin,gateMaximum=s.gateMax,s.lockedSeen,s.openSeen,s.archivedCopy,s.foleyCursor,performance=new{medianMilliseconds=Percentile(s.frameTimes,.5f),p95Milliseconds=Percentile(s.frameTimes,.95f),maxMilliseconds=s.frameTimes.Count==0?0:s.frameTimes.Max(),includesExplicitScreenshotCost=true},s.transients,s.nameplateRows,s.rows,s.shots,ended,menu,endFrame,scope=Scope};
            string report=Report("live-validation.json",result);Report("live-progress.json",new{status="complete",success=true,time=N(x.p,"sampledTime"),eventsObserved=s.events.Count,eventsTotal=x.eventStarts.Length,keylines=s.keys.Count,screenshots=s.shots.Count,report});return new{success=true,eventsObserved=s.events.Count,eventsTotal=x.eventStarts.Length,focus=s.focus.Count,keylines=s.keys.Count,maxConfirmedSpread=s.maxConfirmed,maxHistory=s.maxHistory,runtimeScreenshots=s.shots.Count,report,scope=Scope};
        }
        catch(Exception e)
        {
            string report=Report("live-failure.json",new{phase,error=e.ToString(),wallSeconds=watch==null?0:watch.Elapsed.TotalSeconds,eventsObserved=s.events.Count,eventsTotal=x.eventStarts==null?0:x.eventStarts.Length,observedEventIndices=s.events.OrderBy(i=>i).ToArray(),missingEventIndices=x.eventStarts==null?new int[0]:Enumerable.Range(0,x.eventStarts.Length).Where(i=>!s.events.Contains(i)).ToArray(),focus=s.focus.OrderBy(i=>i).ToArray(),keys=s.keys.OrderBy(i=>i).ToArray(),s.transients,s.rows,s.shots,scope=Scope});Report("live-progress.json",new{status="failed",phase,error=e.Message,report});throw new InvalidOperationException("V4 live validation failed at "+phase+"; "+report+"; "+e.Message,e);
        }
        finally{await Cleanup(x,"live");}
    }
}
