using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using OldWebMap;

public static class OWEpisodeV4Prepare
{
    const string Scene="Assets/Scenes/OWM_Episode123_v4.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision4";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v4");
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    public static object Prepare()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Stopped compiled editor required");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve user unsaved scene changes first");
        string current=SceneManager.GetActiveScene().path;
        if(current!="Assets/Scenes/OWM_Episode123_v3.unity"&&current!=Scene)throw new InvalidOperationException(current);
        Directory.CreateDirectory(Tmp+"/baseline");Directory.CreateDirectory(Tmp+"/reports");
        if(current!=Scene)
        {
            File.Copy(current,Tmp+"/baseline/user-saved-v3-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",false);
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene)!=null)throw new InvalidOperationException("V4 already exists; do not overwrite");
            if(!AssetDatabase.CopyAsset(current,Scene))throw new InvalidOperationException("V4 clone failed");
            EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        }
        foreach(string p in new[]{Root,Root+"/Data",Root+"/Materials",Root+"/Meshes",Root+"/Programs"})Folder(p);
        string atlas=Root+"/Data/waveform_rgba.png";
        if(AssetDatabase.LoadAssetAtPath<Texture2D>(atlas)==null && !AssetDatabase.CopyAsset("Assets/OldWebMap/Episode123/Revision3/Data/waveform_rgba.png",atlas))throw new InvalidOperationException("Atlas copy failed");
        var d=UnityEngine.Object.FindObjectOfType<OWEpisodeDirector>();
        var result=new{scene=SceneManager.GetActiveScene().path,originalPreserved=true,panels=d.audiencePanels.Select(p=>new{name=p.name,active=p.activeSelf,parent=p.transform.parent==null?null:p.transform.parent.name,position=p.transform.position.ToString(),rotation=p.transform.eulerAngles.ToString()}).ToArray(),rootChildren=GameObject.Find("OWEpisode123").transform.Cast<Transform>().Select(t=>new{t.name,active=t.gameObject.activeSelf}).ToArray()};
        string json=JsonConvert.SerializeObject(result,Formatting.Indented);File.WriteAllText(Tmp+"/reports/baseline.json",json);return json;
    }
}
