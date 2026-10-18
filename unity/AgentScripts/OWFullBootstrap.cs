using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWFullBootstrap
{
    const string Source="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Target="Assets/Scenes/OWM_FullFilm_v1.unity";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v1");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static string Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static string PathOf(Transform t)=>t==null?null:(t.parent==null?t.name:PathOf(t.parent)+"/"+t.name);
    static string Report(string name,object data){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(data,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    public static object HoldImports(){if(!SessionState.GetBool("FullFilmImports",false)){AssetDatabase.DisallowAutoRefresh();SessionState.SetBool("FullFilmImports",true);}return "imports held";}
    public static object Import(){if(SessionState.GetBool("FullFilmImports",false)){AssetDatabase.AllowAutoRefresh();SessionState.SetBool("FullFilmImports",false);}AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);return "full-film batch imported";}
    public static object FrontCastTable()
    {
        var c=F<OWV4HubCast>();return Report("front-cast-table.json",new{times=c.keyTimes,positions=c.positions.Select(v=>new[]{v.x,v.y,v.z}).ToArray(),yaws=c.yaws,gestures=c.gestures,gaze=c.gazePositions.Select(v=>new[]{v.x,v.y,v.z}).ToArray()});
    }
    public static object Inspect()
    {
        var scene=SceneManager.GetActiveScene();var p=F<OWV4Presentation>();var c=F<OWV4HubCast>();var o=F<OWV4Oscilloscope>();
        return Report("preflight.json",new{scene=scene.path,scene.isDirty,playing=EditorApplication.isPlaying,roots=scene.GetRootGameObjects().Select(g=>g.name).ToArray(),performance=p==null?null:PathOf(p.performanceRoot.transform),world=c==null?null:PathOf(c.worldRoot),workshop=c==null?null:PathOf(c.workshop),osc=o==null?null:PathOf(o.transform),waves=o==null?null:o.waves.Select(PathOf).ToArray(),components=Object.FindObjectsOfType<UdonSharp.UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.StartsWith("OWV4")||b.GetType().Name.StartsWith("OWV5")).Select(b=>new{name=b.GetType().Name,path=PathOf(b.transform)}).ToArray()});
    }
    public static object Create()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stopped, compiled editor required");
        var s=SceneManager.GetActiveScene();if(s.isDirty)throw new Exception("Current scene has unsaved user edits: "+s.path);
        string hash=Hash(Source);Directory.CreateDirectory(Tmp+"/baseline");string backup=Tmp+"/baseline/v5-"+hash+".unity";if(!File.Exists(backup))File.Copy(Source,backup);
        if(!File.Exists(Target)&&!AssetDatabase.CopyAsset(Source,Target))throw new Exception("Scene copy failed");
        EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);
        if(GameObject.Find("FullFilm Continuation")==null)new GameObject("FullFilm Continuation");
        return Report("scene-created.json",new{source=Source,target=Target,v5Sha256=hash,backup});
    }
}
