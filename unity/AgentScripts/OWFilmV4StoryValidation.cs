using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using TMPro;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWFilmV4StoryValidation
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v4");
    static void Need(bool b,string message){if(!b)throw new Exception(message);}
    static string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    public static object Check()
    {
        Need(!EditorApplication.isPlaying&&!EditorApplication.isCompiling&&SceneManager.GetActiveScene().path=="Assets/Scenes/OWM_FullFilm_v4.unity","Stopped compiled V4 required");var run=Object.FindObjectOfType<OWFilmV4Presentation>(true);var d=run.details;var m=run.media;var rows=new List<object>();
        try
        {
            run.Waiting();Need(d.openingRoom.gameObject.activeInHierarchy&&d.openingWalls.Length==6&&d.openingWalls.All(x=>x.gameObject.activeInHierarchy),"Waiting room is not physically closed");
            Need(d.openingRoom.parent==run.transform,"Waiting walls inherit a hidden journey root");Need(!run.editorStage.activeInHierarchy&&!m.relatedRoot.gameObject.activeInHierarchy&&!m.historyRoot.activeInHierarchy,"Future content visible before start");
            Need(d.sampledRibbonState==0&&d.textRibbonMaterial.GetFloat("_Cyclic")==0&&d.textRibbonMaterial.GetFloat("_Scroll")==d.ribbonPixels[0]*.5f,"Idle ribbon scrolls or loses full phrase");
            var fonts=Object.FindObjectsOfType<TMP_Text>(true).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()).Select(t=>t.font).Where(f=>f!=null).Distinct().ToArray();Need(fonts.Length>0&&fonts.All(f=>f.faceInfo.familyName.Contains(" SC")),"Non-SC glyph face remains on scene text");
            foreach(float t in new[]{0f,.64f,1.28f,14.08f,32f,44.8f,55.36f,66.56f,74.24f,80.64f,87.28f,109.44f,121.6f,125.44f,129.28f,145.92f,163.84f,175.36f,190.72f,199.68f,216.96f,217.6f,218.88f,224f,239.36f,247.04f,251.52f})
            {
                run.Preview(t);Need(!run.miniature.gameObject.activeInHierarchy,"Independent miniature exposed at "+t);
                if(t<217.6f)Need(!run.finalWorld.gameObject.activeInHierarchy,"World exposed before Play at "+t);
                if(t==217.6f)Need(Vector3.Distance(run.finalWorld.position,run.workspaceScreen.position)<.005f,"World does not originate inside actual monitor");
                if(t>=197.12f&&t<217.6f)Need(!run.editorFrame.gameObject.activeInHierarchy,"Detached future editor window leaked");
                if(t>=186.88f&&t<197.12f)Need(d.textRibbonMaterial.GetFloat("_Opacity")==0&&d.textRibbonMaterial.GetFloat("_Line")>0,"Quiet segment still has words");
                if(t>=120.32f&&t<133.12f)Need(m.provenanceHeading.gameObject.activeInHierarchy&&d.provenanceRoot.activeInHierarchy,"Avatar provenance subject invisible");
                if(m.sampledGroup>=0)Need(m.activeRelated>=2,"Fewer than two independent related sources at "+t);
                rows.Add(new{time=t,ribbon=d.sampledRibbonState,related=m.activeRelated,history=m.sampledHistory,film=m.sampledFilm,emotes=m.activeEmotes,world=run.finalWorld.gameObject.activeInHierarchy,worldPosition=run.finalWorld.position.ToString()});
            }
            Need(m.emotes.Length>=20&&m.historyFrames.Length>=20&&m.filmAtlases.Length>=4,"Historical media binding incomplete");
            Need(m.sources.Length==128&&m.sources.All(t=>t!=null),"Some related foreground sources are still unbound");
            foreach(string shaderName in new[]{"OldWebMap/FilmV4/Story Ribbon","OldWebMap/FilmV4/EDGE TEAM Wave Cylinder","OldWebMap/FilmV4/Continue Assembly","OldWebMap/FilmV4/Archive Field","OldWebMap/FilmV4/Archive Filmstrip"}){var sh=Shader.Find(shaderName);Need(sh!=null&&!ShaderUtil.GetShaderMessages(sh).Any(x=>x.severity.ToString()=="Error"),"Shader compile: "+shaderName);}
            run.Finished();Need(d.sampledRibbonState==13&&d.textRibbonMaterial.GetFloat("_Cyclic")==0&&run.basis.legacy.menuRoot.activeInHierarchy,"Finished ribbon or replay unavailable");Need(Mathf.Abs(d.penRoot.transform.position.x)>2.5f&&d.penPickups.All(p=>p.pickupable),"Pen rack not at usable side position");
            var baseline=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Tmp+"/reports/baseline.json"));foreach(var item in baseline["protectedScenes"])Need(Hash((string)item["path"])==(string)item["sha256"],"Protected scene changed: "+item["path"]);
            string json=JsonConvert.SerializeObject(new{success=true,rows,scFontAssets=fonts.Length,closedWaitingFaces=d.openingWalls.Length,historicalEmotes=m.emotes.Length,historicalFrames=m.historyFrames.Length,silentFootageAtlases=m.filmAtlases.Length,protectedScenesPreserved=true,scope="Actual Editor scene content and shaders; visual composition additionally requires captured pixels, not inferred from these values"},Formatting.Indented);Directory.CreateDirectory(Tmp+"/reports");File.WriteAllText(Tmp+"/reports/story-validation.json",json);return json;
        }
        finally{run.Waiting();run.offlinePreview=false;}
    }
}
