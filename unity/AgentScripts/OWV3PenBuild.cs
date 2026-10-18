using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWV3PenBuild
{
    const string Target="Assets/QvPen";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v3");
    static string Source=>Tmp+"/intake/local-qvpen/Assets/QvPen";
    static string Report(string name,object data){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(data,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name+".json",s);return s;}
    static void Guard(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!="Assets/Scenes/OWM_FullFilm_v3.unity")throw new Exception("Stopped compiled V3 required");}
    public static object Import()
    {
        Guard();if(!Directory.Exists(Source))throw new Exception("Preserved source copy is absent");
        if(Directory.Exists(Target))return new{success=true,alreadyImported=true,version=File.ReadAllText(Target+"/version.txt").Trim()};
        var metas=Directory.GetFiles(Source,"*.meta",SearchOption.AllDirectories);var collisions=metas.Select(p=>new{source=p,guid=Regex.Match(File.ReadAllText(p),@"(?m)^guid: ([a-f0-9]{32})").Groups[1].Value}).Where(x=>x.guid.Length>0).Select(x=>new{x.source,x.guid,existing=AssetDatabase.GUIDToAssetPath(x.guid)}).Where(x=>!string.IsNullOrEmpty(x.existing)).ToArray();
        if(collisions.Length>0)throw new Exception(Report("qvpen-guid-collisions",new{success=false,collisions}));
        FileUtil.CopyFileOrDirectory(Source,Target);AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        return Report("qvpen-import",new{success=true,source=Source,target=Target,files=Directory.GetFiles(Source,"*",SearchOption.AllDirectories).Length,checkedGuids=metas.Length,collisions=collisions.Length,version=File.ReadAllText(Target+"/version.txt").Trim(),sourceFilesAndMetasCopiedUnchanged=true,compilePending=true});
    }
    static float[] Vec(Vector3 v)=>new[]{v.x,v.y,v.z};
    public static object Integrate()
    {
        Guard();var run=Object.FindObjectOfType<OWV3Presentation>(true);var d=run.details;
        const string prefabPath=Target+"/QvPen_WOOD_PenStand.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);if(prefab==null)throw new Exception("Imported wood stand prefab absent");
        if(d.penRoot!=null)throw new Exception("Pen already integrated: inspect and adjust this instance instead of replacing user writing");
        var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);root.name="QvPen - shared drawing after the performance";root.transform.SetParent(run.transform,false);root.transform.SetPositionAndRotation(new Vector3(.7f,0,-1.2f),Quaternion.Euler(0,180,0));root.transform.localScale=Vector3.one*.65f;
        var stand=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&(f.sharedMesh.name.IndexOf("PenStand",StringComparison.OrdinalIgnoreCase)>=0||f.name.IndexOf("PenStand",StringComparison.OrdinalIgnoreCase)>=0)).Select(f=>f.GetComponent<Renderer>()).Where(r=>r!=null).ToArray();
        if(stand.Length==0)throw new Exception("Cannot identify original wooden stand geometry");
        var bounds=stand[0].bounds;foreach(var r in stand.Skip(1))bounds.Encapsulate(r.bounds);
        run.Finished();Physics.SyncTransforms();var floor=Physics.RaycastAll(new Vector3(.7f,3,-1.2f),Vector3.down,8).Where(h=>d.worldFloorColliders.Contains(h.collider)).OrderBy(h=>h.distance).FirstOrDefault();
        if(floor.collider==null)throw new Exception("Final floor does not support the chosen pen stand position");
        root.transform.position+=Vector3.up*(floor.point.y-bounds.min.y);
        d.penRoot=root;d.penRenderers=root.GetComponentsInChildren<Renderer>(true);d.penColliders=root.GetComponentsInChildren<Collider>(true);d.penPickups=root.GetComponentsInChildren<VRC_Pickup>(true);
        if(d.penPickups.Length==0)throw new Exception("Original QvPen pickup hierarchy was lost");
        var udon=root.GetComponentsInChildren<UdonSharpBehaviour>(true);foreach(var b in udon)UdonSharpEditorUtility.CopyProxyToUdon(b);
        run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(d);UdonSharpEditorUtility.CopyProxyToUdon(run);EditorUtility.SetDirty(d);EditorUtility.SetDirty(run);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("qvpen-integration",new{success=true,version=File.ReadAllText(Target+"/version.txt").Trim(),prefab=prefabPath,guid=AssetDatabase.AssetPathToGUID(prefabPath),position=Vec(root.transform.position),scale=Vec(root.transform.localScale),floorY=floor.point.y,pickups=d.penPickups.Select(p=>new{p.name,position=Vec(p.transform.position)}).ToArray(),renderers=d.penRenderers.Length,colliders=d.penColliders.Length,behaviours=udon.Length,managerRootKeptActive=true,inkLifecycle="Original global ink managers stay active; rendering and collisions gated during performance"});
    }
    public static object AdjustStand()
    {
        Guard();var run=Object.FindObjectOfType<OWV3Presentation>(true);var d=run.details;var root=d.penRoot;if(root==null)throw new Exception("Integrate preserved prefab first");
        root.transform.SetPositionAndRotation(new Vector3(.7f,.52f,-1.2f),Quaternion.identity);
        var parts=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&(f.sharedMesh.name.IndexOf("PenStand",StringComparison.OrdinalIgnoreCase)>=0||f.name.IndexOf("PenStand",StringComparison.OrdinalIgnoreCase)>=0)).Select(f=>f.GetComponent<Renderer>()).Where(r=>r!=null).ToArray();var bounds=parts[0].bounds;foreach(var r in parts.Skip(1))bounds.Encapsulate(r.bounds);
        var child=root.transform.Find("Low shared drawing plinth");if(child==null){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Low shared drawing plinth";go.transform.SetParent(root.transform,false);Object.DestroyImmediate(go.GetComponent<Collider>());child=go.transform;}
        var plinth=child.GetComponent<Renderer>();const string path="Assets/OldWebMap/FullFilm/Revision3/Materials/Shared drawing plinth.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("lilToon"));AssetDatabase.CreateAsset(material,path);}material.color=new Color(.16f,.24f,.24f);plinth.sharedMaterial=material;
        float bottom=-.05f,top=bounds.min.y;child.SetPositionAndRotation(new Vector3(bounds.center.x,(bottom+top)*.5f,bounds.center.z),Quaternion.identity);child.localScale=new Vector3(Mathf.Max(.72f,bounds.size.x+.06f)/.65f,(top-bottom)/.65f,Mathf.Max(.34f,bounds.size.z+.05f)/.65f);
        d.penRenderers=root.GetComponentsInChildren<Renderer>(true);d.penCanvases=root.GetComponentsInChildren<Canvas>(true);run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(d);EditorUtility.SetDirty(d);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("qvpen-placement",new{success=true,rootPosition=Vec(root.transform.position),rackBounds=new{min=Vec(bounds.min),max=Vec(bounds.max)},plinthTop=top,penPickupHeight=.52f,preservedOriginalChildren=true});
    }
}
