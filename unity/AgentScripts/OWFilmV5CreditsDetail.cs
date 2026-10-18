using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using OldWebMap;
using Object=UnityEngine.Object;

// Update the existing credits bitmap without rebuilding or moving its boards.
public static class OWFilmV5CreditsDetail
{
    const string Scene="Assets/Scenes/OWM_FullFilm_v5.unity";
    const string Texture="Assets/OldWebMap/FullFilm/Revision5/Textures/credits-01-tools-and-world.png";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5-credits-detail");
    static string Hash(string path)
    {
        using(var h=System.Security.Cryptography.SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();
    }
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    public static object Import()
    {
        var scene=SceneManager.GetActiveScene();
        Need(scene.path==Scene&&!scene.isDirty&&!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Preserve user edits: clean stopped V5 required");
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-eac-credits.json"));var page=data["credits"]["pages"][0];
        string stage=Tmp+"/credits-01-tools-and-world.png",expected=(string)page["output_sha256"];
        Need(Hash(stage)==expected,"Staged credits do not match the manifest");
        string before=Hash(Scene);Directory.CreateDirectory(Tmp+"/before");
        if(!File.Exists(Tmp+"/before/credits-01-tools-and-world.png"))File.Copy(Texture,Tmp+"/before/credits-01-tools-and-world.png");
        if(!File.Exists(Tmp+"/before/OWM_FullFilm_v5.unity"))File.Copy(Scene,Tmp+"/before/OWM_FullFilm_v5.unity");
        File.Copy(stage,Texture,true);AssetDatabase.ImportAsset(Texture,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Texture);Need(tex!=null&&tex.width==2048&&tex.height==1152,"Credit texture size changed");
        var run=Object.FindObjectsOfType<OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==scene);
        var boards=run.details.creditsRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Readable credits").ToArray();
        Need(boards.Length==2&&boards.Count(r=>r.sharedMaterial.mainTexture==tex)==1,"Existing credits board binding failed");
        var row=((JArray)page["rows"]).Single(r=>(string)r["source"]=="https://booth.pm/ja/items/8379856");
        Need(!scene.isDirty&&Hash(Scene)==before,"Bitmap import unexpectedly changed the scene");
        var result=new{success=true,work=(string)row["work"],author=(string)row["author"],url=(string)row["source"],
            texture=Texture,sha256=Hash(Texture),rows=((JArray)page["rows"]).Count,boardCount=boards.Length,
            existingTextureBindingVerified=true,sceneUnchanged=true,sceneSha256=before,playing=EditorApplication.isPlaying,dirty=scene.isDirty};
        Directory.CreateDirectory(Tmp+"/reports");File.WriteAllText(Tmp+"/reports/credits-update.json",JsonConvert.SerializeObject(result,Formatting.Indented));
        return result;
    }
}
