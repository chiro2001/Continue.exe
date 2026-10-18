using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;

public static class OWFilmV5Bootstrap
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5");
    static bool Allowed(string path)=>path.StartsWith("Assets/OldWebMap/FullFilm/Revision5/")||path.StartsWith("Assets/OldWebMap/Scripts/OWFilmV5")&&path.EndsWith(".cs")||path.StartsWith("Assets/OldWebMap/Shaders/OWFilmV5")&&path.EndsWith(".shader");
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int p=path.LastIndexOf('/');Folder(path.Substring(0,p));AssetDatabase.CreateFolder(path.Substring(0,p),path.Substring(p+1));}
    static string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    public static object Import()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stopped compiled editor required");
        string staging=Tmp+"/stage";var rows=new List<object>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(string src in Directory.GetFiles(staging,"*",SearchOption.AllDirectories).OrderBy(x=>x))
            {
                string dest=src.Substring(staging.Length+1).Replace('\\','/');
                if(!Allowed(dest)||dest.EndsWith(".meta")||dest.EndsWith(".mat")||dest.EndsWith(".unity")||dest.EndsWith(".asset"))throw new Exception("Only owned V5 sources may be staged: "+dest);
                Folder(Path.GetDirectoryName(dest).Replace('\\','/'));
                bool changed=!File.Exists(dest)||Hash(dest)!=Hash(src);if(changed)File.Copy(src,dest,true);
                rows.Add(new{path=dest,changed,sha256=Hash(src)});
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        Directory.CreateDirectory(Tmp+"/reports");var json=JsonConvert.SerializeObject(new{success=true,rows},Formatting.Indented);File.WriteAllText(Tmp+"/reports/source-import.json",json);
        AssetDatabase.Refresh();return new{success=true,files=rows.Count,report=Tmp+"/reports/source-import.json"};
    }
}
