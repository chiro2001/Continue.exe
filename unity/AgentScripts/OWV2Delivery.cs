using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWV2Delivery
{
    const string Scene="Assets/Scenes/OWM_FullFilm_v2.unity";
    const int Width=1600,Height=900,Fps=25,Total=6450;
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v2");
    static OWV2Presentation Run()=>Object.FindObjectsOfType<OWV2Presentation>(true).First(x=>x.gameObject.scene==SceneManager.GetActiveScene());
    static string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static void Guard(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=Scene)throw new Exception("Stopped compiled V2 required");}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name+".json",json);return json;}
    static int frame;static bool running;static Camera camera;static RenderTexture rt;static Texture2D image;static OWV2Presentation run;
    static readonly string[] Poses={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
    public static object StartMovie()
    {
        Guard();if(SessionState.GetBool("OWV2MovieRunning",false))throw new Exception("Movie already running");
        Directory.CreateDirectory(Tmp+"/movie-frames");run=Run();camera=Camera.main;rt=new RenderTexture(Width,Height,24);rt.Create();image=new Texture2D(Width,Height,TextureFormat.RGB24,false);frame=0;running=true;
        SessionState.SetBool("OWV2MovieRunning",true);SessionState.SetInt("OWV2MovieFrame",0);EditorApplication.update+=Tick;
        return new{success=true,frames=Total,fps=Fps,width=Width,height=Height,performanceSeconds=256,aftermathSeconds=2,audioPlayed=false};
    }
    static void Tick()
    {
        if(!running)return;
        try
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<4&&frame<Total;i++)
            {
                float t=frame/(float)Fps;if(t>=256)run.Finished();else run.Preview(t);
                if(t>=77.44f&&t<102.4f)
                {
                    foreach(var actor in run.frontActors)
                    {
                        var rig=actor.GetComponent<OWActorRig>();if(rig==null||rig.animator==null||!actor.activeInHierarchy||rig.animator.runtimeAnimatorController==null)continue;var a=rig.animator;a.Play(Poses[Mathf.Clamp(a.GetInteger("V4Pose"),0,11)],0,a.GetFloat("V4PoseTime"));a.Update(0);
                    }
                    run.Preview(t);
                }
                if(run.channelParticles!=null)foreach(var ps in run.channelParticles)if(ps!=null&&ps.gameObject.activeInHierarchy)ps.Simulate(Mathf.Repeat(t,4f),true,true,true);
                Canvas.ForceUpdateCanvases();foreach(var text in Object.FindObjectsOfType<TMPro.TMP_Text>())if(text.havePropertiesChanged)text.ForceMeshUpdate(false,false);
                var previous=camera.targetTexture;var active=RenderTexture.active;
                try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();File.WriteAllBytes(Tmp+"/movie-frames/frame_"+frame.ToString("00000")+".jpg",image.EncodeToJPG(91));}
                finally{camera.targetTexture=previous;RenderTexture.active=active;}
                frame++;SessionState.SetInt("OWV2MovieFrame",frame);if(watch.ElapsedMilliseconds>100)break;
            }
            if(frame%25<4)Report("movie-progress",new{frame,total=Total,complete=false,error=(string)null});if(frame>=Total)End(null);
        }
        catch(Exception e){End(e.ToString());}
    }
    static void End(string error)
    {
        running=false;EditorApplication.update-=Tick;SessionState.SetBool("OWV2MovieRunning",false);if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}if(image!=null)Object.DestroyImmediate(image);if(run!=null){run.Waiting();run.offlinePreview=false;}
        Report("movie-progress",new{frame,total=Total,complete=frame==Total&&error==null,error});
    }
    public static object MovieStatus()=>new{running=SessionState.GetBool("OWV2MovieRunning",false),frame=SessionState.GetInt("OWV2MovieFrame",0),total=Total};
    public static object CancelMovie(){End("Cancelled by authoring tool");return new{stopped=true};}
    static void AutoTick(bool enabled)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand",false)).First(t=>t!=null);
        type.GetMethod("SetAutoTick",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{enabled,16,false});
    }
    public static async Task<object> SdkBuild()
    {
        Guard();if(SceneManager.GetActiveScene().isDirty)throw new Exception("Save V2 before SDK build");string input=Hash(Scene);Directory.CreateDirectory(Tmp+"/scene-snapshots");File.Copy(Scene,Tmp+"/scene-snapshots/sdk-input-"+input+".unity",true);
        var panel=EditorWindow.GetWindow<VRCSdkControlPanel>();panel.Show();await Task.Delay(500);if(!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var api))throw new Exception("SDK builder unavailable");var builder=(VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder)api;builder.RegisterBuilder(panel);if(!builder.IsValidBuilder(out string why))throw new Exception(why);builder.Initialize();
        try{AutoTick(false);string built=await api.Build();string path=Tmp+"/OWM_FullFilm_v2.vrcw";File.Copy(built,path,true);string output=Hash(Scene);File.Copy(Scene,Tmp+"/scene-snapshots/sdk-output-"+output+".unity",true);return Report("sdk-build",new{success=true,path,bytes=new FileInfo(path).Length,sha256=Hash(path),scene=Scene,inputSceneSha256=input,outputSceneSha256=output,uploaded=false});}
        catch(Exception e){Report("sdk-build",new{success=false,error=e.ToString()});throw;}
        finally{AutoTick(true);}
    }
    public static object InspectBundle()
    {
        Guard();string path=Tmp+"/OWM_FullFilm_v2.vrcw";var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new Exception("Bundle unreadable");try{var scenes=bundle.GetAllScenePaths();if(!bundle.isStreamedSceneAssetBundle||scenes.Length!=1||!scenes[0].EndsWith("OWM_FullFilm_v2.unity"))throw new Exception("Wrong world bundle");return Report("sdk-bundle-inspection",new{success=true,scenes,sha256=Hash(path),scope="Local bundle and scene table; not an uploaded VRChat client build test"});}finally{bundle.Unload(false);}
    }
    public static object Reopen()
    {
        Guard();if(SceneManager.GetActiveScene().isDirty)throw new Exception("Save before reopening");string before=Hash(Scene);UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scene,UnityEditor.SceneManagement.OpenSceneMode.Single);var x=Run();bool waiting=x.basis.legacy.menuRoot.activeInHierarchy&&!x.editorStage.activeInHierarchy&&!x.overloadField.activeInHierarchy&&!x.offlinePreview;
        if(!waiting||Hash(Scene)!=before)throw new Exception("Saved V2 waiting state failed");return Report("reopen",new{success=true,scene=Scene,sha256=before,waiting,stopped=!EditorApplication.isPlaying,compiled=!EditorApplication.isCompiling,v1Sha256=Hash("Assets/Scenes/OWM_FullFilm_v1.unity"),v5Sha256=Hash("Assets/Scenes/OWM_Episode123_v5.unity")});
    }
    public static object FinalState()
    {
        Guard();var scene=SceneManager.GetActiveScene();var x=Run();
        bool waiting=x.basis.legacy.menuRoot.activeInHierarchy&&!x.editorStage.activeInHierarchy&&!x.overloadField.activeInHierarchy&&!x.offlinePreview;
        bool udonErrors=UdonSharpProgramAsset.AnyUdonSharpScriptHasError();
        var shaders=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==scene).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.shader).Where(s=>s!=null).Distinct().ToArray();
        var errors=shaders.SelectMany(s=>ShaderUtil.GetShaderMessages(s).Where(m=>m.severity.ToString()=="Error").Select(m=>s.name+": "+m.message)).ToArray();
        bool success=!scene.isDirty&&waiting&&!udonErrors&&errors.Length==0&&x.screenshotStates.Length==11;
        string report=Report("final-editor-state",new{success,scene=scene.path,sceneSha256=Hash(Scene),playStopped=!EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,sceneSaved=!scene.isDirty,waiting,udonCompileErrors=udonErrors,shaderErrors=errors,shaders=shaders.Length,actualSoftwareStates=x.screenshotStates.Length,v1Sha256=Hash("Assets/Scenes/OWM_FullFilm_v1.unity"),v5Sha256=Hash("Assets/Scenes/OWM_Episode123_v5.unity"),scope="Stopped saved V2 Editor and referenced shaders; no device/network certification"});
        if(!success)throw new Exception(report);return report;
    }
    public static object DynamicMaterialsProof()
    {
        Guard();var descriptor=Object.FindObjectOfType<VRC.SDKBase.VRC_SceneDescriptor>();if(descriptor==null)throw new Exception("Scene descriptor missing");
        var field=descriptor.GetType().GetField("DynamicMaterials",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
        var materials=((System.Collections.Generic.IEnumerable<Material>)field.GetValue(descriptor)).ToArray();
        var rows=materials.Select(m=>new{guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)),name=m.name,path=AssetDatabase.GetAssetPath(m)}).ToArray();
        var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        string worldsVersion=packages.First(p=>p.name=="com.vrchat.worlds").version,baseVersion=packages.First(p=>p.name=="com.vrchat.base").version;
        bool success=rows.Length>0&&rows.All(m=>!string.IsNullOrEmpty(m.guid)&&m.path.StartsWith("Assets/"))&&rows.Select(m=>m.guid).Distinct().Count()==rows.Length;
        string report=Report("sdk-dynamic-materials",new{success,sourceSceneSha256=Hash(Scene),descriptorType=descriptor.GetType().FullName,descriptorLocalFileId=GlobalObjectId.GetGlobalObjectIdSlow(descriptor).targetObjectId.ToString(),worldsVersion,baseVersion,materials=rows,scope="Read-only actual V2 SDK-output descriptor; material count is derived from this build"});
        if(!success)throw new Exception(report);return report;
    }
}
