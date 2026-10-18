using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWV3Inspect
{
    static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
    public static object Initial()
    {
        var scene=SceneManager.GetActiveScene();var run=Object.FindObjectOfType<OWV2Presentation>(true);
        var sources=run==null?new AudioSource[0]:run.director.music.sources;
        var data=new{scene=scene.path,dirty=scene.isDirty,playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,
            menu=run==null?null:run.basis.legacy.menuRoot.GetComponentsInChildren<Transform>(true).Select(t=>new{t.name,position=V(t.position),scale=V(t.lossyScale),components=t.GetComponents<Component>().Select(c=>c==null?"Missing":c.GetType().FullName).ToArray()}).ToArray(),
            startup=run==null?null:run.basis.legacy.startupObject.GetComponentsInChildren<Renderer>(true).Select(r=>new{r.name,position=V(r.transform.position),scale=V(r.transform.lossyScale),center=V(r.bounds.center),size=V(r.bounds.size),material=r.sharedMaterial==null?null:AssetDatabase.GetAssetPath(r.sharedMaterial)}).ToArray(),
            sources=sources.Select(s=>new{s.name,position=V(s.transform.position),s.spatialBlend,s.minDistance,s.maxDistance,s.rolloffMode,s.volume,components=s.GetComponents<Component>().Select(c=>new{type=c.GetType().FullName,serialized=EditorJsonUtility.ToJson(c)}).ToArray()}).ToArray()};
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v3/reports");Directory.CreateDirectory(folder);string json=JsonConvert.SerializeObject(data,Formatting.Indented);File.WriteAllText(folder+"/initial-editor.json",json);return json;
    }
    public static object Floors()
    {
        var scene=SceneManager.GetActiveScene();
        return JsonConvert.SerializeObject(Object.FindObjectsOfType<Collider>(true).Where(c=>c.gameObject.scene==scene&&!c.isTrigger&&c.bounds.size.x>4&&c.bounds.size.y<1).Select(c=>new{c.name,position=V(c.transform.position),size=V(c.bounds.size),parent=c.transform.parent==null?"":c.transform.parent.name}).ToArray(),Formatting.Indented);
    }
}
