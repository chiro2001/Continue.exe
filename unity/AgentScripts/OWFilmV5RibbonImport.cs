using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

// Import only this pass's staged, nonserialized sources through Pipeline.
public static class OWFilmV5RibbonImport
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5-ribbon-pass");
    public static object Import()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||scene.path!="Assets/Scenes/OWM_FullFilm_v5.unity"||scene.isDirty)
            throw new Exception("Preserve user edits: a clean, stopped full-film V5 is required before import");
        string stage=Tmp+"/stage";var rows=new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var source in Directory.GetFiles(stage,"*",SearchOption.AllDirectories).OrderBy(s=>s))
            {
                string target=source.Substring(stage.Length+1).Replace('\\','/');
                bool owned=target.StartsWith("Assets/OldWebMap/Scripts/OWFilmV5")&&target.EndsWith(".cs")
                    ||target.StartsWith("Assets/OldWebMap/Shaders/OWFilmV5")&&target.EndsWith(".shader")
                    ||target.StartsWith("Assets/OldWebMap/FullFilm/Revision5/Textures/ribbon-")&&target.EndsWith(".png");
                if(!owned)throw new Exception("Unexpected staged source: "+target);
                File.Copy(source,target,true);rows.Add(target);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        Directory.CreateDirectory(Tmp+"/reports");File.WriteAllText(Tmp+"/reports/import.json",JsonConvert.SerializeObject(new{success=true,rows},Formatting.Indented));
        AssetDatabase.Refresh();return new{success=true,files=rows.Count};
    }
}
