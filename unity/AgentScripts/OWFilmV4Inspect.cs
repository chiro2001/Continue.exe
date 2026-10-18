using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class OWFilmV4Inspect
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v4");
    static string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    public static object Baseline()
    {
        Directory.CreateDirectory(Tmp+"/baseline");Directory.CreateDirectory(Tmp+"/reports");
        string[] files={"Assets/Scenes/OWM_FullFilm_v1.unity","Assets/Scenes/OWM_FullFilm_v2.unity","Assets/Scenes/OWM_FullFilm_v3.unity","Assets/Scenes/OWM_Episode123_v5.unity"};
        var rows=files.Where(File.Exists).Select(p=>{string hash=Hash(p);File.Copy(p,Tmp+"/baseline/"+Path.GetFileNameWithoutExtension(p)+"-"+hash+".unity",true);return new{path=p,sha256=hash};}).ToArray();
        var scene=SceneManager.GetActiveScene();
        var report=new{success=true,playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,scene=scene.path,dirty=scene.isDirty,scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>{var s=SceneManager.GetSceneAt(i);return new{s.path,s.isDirty,s.isLoaded};}).ToArray(),protectedScenes=rows};
        string json=JsonConvert.SerializeObject(report,Formatting.Indented);File.WriteAllText(Tmp+"/reports/baseline.json",json);return json;
    }
    public static object State()
    {
        var scene=SceneManager.GetActiveScene();return new{scene=scene.path,scene.isDirty,playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,drivers=Object.FindObjectsOfType<UdonSharp.UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.Contains("Presentation")).Select(b=>new{type=b.GetType().Name,b.enabled,b.gameObject.activeInHierarchy}).ToArray()};
    }
    public static object Monitor()
    {
        var run=Object.FindObjectOfType<OldWebMap.OWFilmV4Presentation>(true);run.Preview(216.96f);
        try{return JsonConvert.SerializeObject(new{time=run.sampledTime,editor=run.editorStage.activeInHierarchy,workspace=new{run.workspace.name,position=run.workspace.position.ToString(),scale=run.workspace.localScale.ToString(),lossyScale=run.workspace.lossyScale.ToString(),parent=run.workspace.parent.name},screens=run.workspace.GetComponentsInChildren<MeshFilter>(true).Where(x=>x.name.Contains("UI_Main")).Select(f=>new{f.name,vertices=f.sharedMesh.vertexCount,meshCentre=f.sharedMesh.bounds.center.ToString(),position=f.transform.position.ToString(),lossyScale=f.transform.lossyScale.ToString(),geometricCentre=f.transform.TransformPoint(f.sharedMesh.bounds.center).ToString(),parents=f.transform.parent.name,rendererEnabled=f.GetComponent<Renderer>().enabled,active=f.gameObject.activeInHierarchy}).ToArray()},Formatting.Indented);}
        finally{run.Waiting();run.offlinePreview=false;}
    }
    public static object Related(float time)
    {
        var run=Object.FindObjectOfType<OldWebMap.OWFilmV4Presentation>(true);run.Preview(time);
        try{return JsonConvert.SerializeObject(run.media.relatedPlanes.Select((t,i)=>new{i,index=run.media.sampledRelated[i],material=AssetDatabase.GetAssetPath(t.GetComponent<Renderer>().sharedMaterial),texture=AssetDatabase.GetAssetPath(run.media.relatedMaterials[i].mainTexture),rect=run.media.relatedMaterials[i].GetVector("_Rect").ToString(),blend=run.media.relatedMaterials[i].GetFloat("_Blend"),uv=t.GetComponent<MeshFilter>().sharedMesh.uv.Select(u=>u.ToString()).ToArray()}).ToArray(),Formatting.Indented);}
        finally{run.Waiting();run.offlinePreview=false;}
    }
}
