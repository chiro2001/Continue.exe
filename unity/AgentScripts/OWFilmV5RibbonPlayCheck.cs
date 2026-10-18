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
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using VRC.SDKBase;
using Vm=VRC.Udon.UdonBehaviour;
using Object=UnityEngine.Object;

// Actual Udon VM lifecycle checks. No movie, HTML, audio export or SDK build.
public static class OWFilmV5RibbonPlayCheck
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5-ribbon-pass");
    static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    static Vm V(UdonSharpBehaviour proxy)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
    static float N(Vm vm,string key)=>Convert.ToSingle(vm.GetProgramVariable(key));
    static int I(Vm vm,string key)=>Convert.ToInt32(vm.GetProgramVariable(key));
    static double Clock()=>Utilities.IsValid(Networking.LocalPlayer)?Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;
    static string Report(string name,object value)
    {
        Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);
        File.WriteAllText(Tmp+"/reports/"+name+".json",json);return json;
    }
    static void Frame(string label,bool ribbon=false,float yaw=0)
    {
        var camera=Camera.main;Need(camera!=null,"ClientSim camera missing");
        var target=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect,fov=camera.fieldOfView;
        var rotation=camera.transform.rotation;var rt=RenderTexture.GetTemporary(1600,900,24);var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
        bool hasInfo=GizmoUtility.TryGetGizmoInfo(typeof(Vm),out GizmoInfo info);bool icons=hasInfo&&info.iconEnabled;
        if(hasInfo)GizmoUtility.SetIconEnabled(typeof(Vm),false);
        try
        {
            // Temporary review camera framing only; restore in this same call.
            if(ribbon){camera.transform.rotation=Quaternion.Euler(-18,yaw,0);camera.fieldOfView=65;}
            camera.targetTexture=rt;camera.aspect=1600f/900;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();Directory.CreateDirectory(Tmp+"/checks");
            File.WriteAllBytes(Tmp+"/checks/"+label+".png",image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=target;camera.aspect=aspect;camera.fieldOfView=fov;camera.transform.rotation=rotation;
            RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);
            if(hasInfo)GizmoUtility.SetIconEnabled(typeof(Vm),icons);
        }
    }
    static async Task Seek(Vm director,float time)
    {
        director.SetProgramVariable("_offset",0f);director.SetProgramVariable("_anchor",Clock()-time);
        director.SetProgramVariable("_playing",true);director.SetProgramVariable("_revision",I(director,"_revision")+1);
        director.SendCustomEvent("_onDeserialization");await Task.Delay(500);
    }
    static void Click(Button button)
    {
        Need(button.IsActive()&&button.IsInteractable(),"Start/replay button unavailable");
        Need(ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler),"Pointer click was not dispatched");
    }
    public static async Task<object> Run()
    {
        Need(EditorApplication.isPlaying&&SceneManager.GetActiveScene().path=="Assets/Scenes/OWM_FullFilm_v5.unity","Actual V5 Play Mode required");
        var run=Object.FindObjectsOfType<OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==SceneManager.GetActiveScene());
        var vm=V(run);var details=V(run.details);var provenance=V(run.provenance);var director=V(run.director);
        var rows=new List<object>();var errors=new List<string>();
        Application.LogCallback collect=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
        Application.logMessageReceived+=collect;
        try
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(timer.Elapsed.TotalSeconds<20&&(!Utilities.IsValid(Networking.LocalPlayer)||vm.GetProgramVariable("sampledState")==null))await Task.Delay(100);
            Need(Utilities.IsValid(Networking.LocalPlayer)&&!Convert.ToBoolean(vm.GetProgramVariable("offlinePreview")),"ClientSim/VM not ready");
            var button=run.basis.legacy.menuRoot.GetComponentsInChildren<OWEpisodeButton>(true).Single(b=>b.name=="Start performance").GetComponent<Button>();
            director.SendCustomEvent("StopAtStart");await Task.Delay(300);
            Need(I(vm,"sampledState")==0&&run.details.ribbonDock.gameObject.activeInHierarchy&&!run.details.textRibbon.gameObject.activeInHierarchy,"Waiting ribbon state failed");
            Frame("waiting");Click(button);await Task.Delay(650);Need(I(vm,"sampledState")==1,"Start ray UI does not start the live VM");
            foreach(float target in new[]{5.12f,20.5f,62.4f,76f,124f,126f,131f,160.5f,190f,213f,250.5f})
            {
                await Seek(director,target);float t=N(vm,"sampledTime"),scroll=N(details,"sampledRibbonScroll");
                Need(Mathf.Abs(scroll-run.details.FlowPixel(t))<.15f,"Live cumulative flow differs from the authored curve at "+target);
                Need(run.details.textRibbonMaterial.mainTexture==run.details.ribbonFlowAtlas,"Live chapter swapped the atlas");
                Need(!run.details.gamePlane.gameObject.activeInHierarchy&&!run.details.gameDetailPlane.gameObject.activeInHierarchy,"Removed game illustration is visible in Play Mode");
                if(target>20&&target<249)Need(I(details,"sampledRibbonHistory")>0,"Live history vanished on chapter change");
                bool permission=run.provenance.contentRoot.activeInHierarchy&&run.provenance.cards[1].gameObject.activeInHierarchy&&run.provenance.cardMaterials[1].mainTexture==run.provenance.plates[11];
                if(target==126)Need(permission,"Short Eku side display missing");
                if(target==124||target==131)Need(!permission,"Eku side display outlasts its time window");
                rows.Add(new{target,time=t,scroll,history=I(details,"sampledRibbonHistory"),cue=I(details,"sampledRibbonState"),permission,main=I(provenance,"sampledMain")});
                if(target==5.12f)Frame("right-entry",true,20);
                if(target==62.4f)Frame("grey-hold",true,-45);
                if(target==76f)Frame("detached-drop",true,-145);
                if(target==126)Frame("permission-side");
                if(target==213)Frame("production-without-game-translation");
            }
            await Seek(director,253.3f);await Task.Delay(600);
            Need(I(vm,"sampledState")==2&&run.details.ribbonDock.gameObject.activeInHierarchy&&!run.details.textRibbon.gameObject.activeInHierarchy,"Replay dock does not return");
            Need(button.IsActive()&&button.IsInteractable(),"Replay UI does not return");Click(button);await Task.Delay(650);
            Need(I(vm,"sampledState")==1&&N(details,"sampledRibbonScroll")<500&&I(details,"sampledRibbonHistory")==0,"Replay retains stale stream state");
            director.SendCustomEvent("StopAtStart");await Task.Delay(300);
            Need(errors.Count==0,"Runtime errors: "+string.Join(" | ",errors));
            return Report("play-check",new{success=true,rows,actualSingleClientUdon=true,startReplayRayEvent=true,lateJoinDeterministicSampling=true,
                actualSecondClientTested=false,hmdTested=false,errors,captures="Six internal stills; three review views temporarily turn the ClientSim camera and restore it in the same call. No recording or delivery page."});
        }
        catch(Exception e){Report("play-failure",new{success=false,error=e.ToString(),rows,errors});throw;}
        finally{Application.logMessageReceived-=collect;director.SendCustomEvent("StopAtStart");}
    }
}
