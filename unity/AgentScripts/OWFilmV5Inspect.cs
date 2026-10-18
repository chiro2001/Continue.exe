using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class OWFilmV5Inspect
{
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5");
    static string P(Transform t){return t.parent==null?t.name:P(t.parent)+"/"+t.name;}
    static string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    public static object Baseline(string label="baseline")
    {
        Directory.CreateDirectory(Tmp+"/reports");Directory.CreateDirectory(Tmp+"/baseline");
        var scene=SceneManager.GetActiveScene();
        var protectedFiles=new[]{"Assets/Scenes/OWM_FullFilm_v1.unity","Assets/Scenes/OWM_FullFilm_v2.unity","Assets/Scenes/OWM_FullFilm_v3.unity","Assets/Scenes/OWM_FullFilm_v4.unity","Assets/Scenes/OWM_Episode123_v5.unity"}.Where(File.Exists).Select(path=>{
            string hash=Hash(path);string copy=Tmp+"/baseline/"+Path.GetFileNameWithoutExtension(path)+"-"+hash+".unity";if(!File.Exists(copy))File.Copy(path,copy);
            return new{path,sha256=hash,copy};}).ToArray();
        var shelves=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==scene&&P(r.transform).ToLowerInvariant().Contains("archive shelf")).Select(r=>new{
            path=P(r.transform),r.enabled,active=r.gameObject.activeInHierarchy,slots=r.sharedMaterials.Length,
            materials=r.sharedMaterials.Select((m,i)=>new{slot=i,name=m==null?"":m.name,path=m==null?"":AssetDatabase.GetAssetPath(m),shader=m==null||m.shader==null?"":m.shader.name,
                mainTexture=m==null||m.mainTexture==null?"":AssetDatabase.GetAssetPath(m.mainTexture),scale=m==null?"":m.mainTextureScale.ToString(),offset=m==null?"":m.mainTextureOffset.ToString()}).ToArray()}).ToArray();
        var cameras=Object.FindObjectsOfType<Camera>(true).Where(c=>c.gameObject.scene==scene).Select(c=>new{path=P(c.transform),c.enabled,c.stereoTargetEye,position=c.transform.position.ToString(),c.fieldOfView}).ToArray();
        var report=new{success=true,label,scene=scene.path,scene.isDirty,playing=EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,
            scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>{var s=SceneManager.GetSceneAt(i);return new{s.path,s.isDirty,s.isLoaded};}).ToArray(),protectedFiles,shelves,cameras,
            v5SceneExists=File.Exists("Assets/Scenes/OWM_FullFilm_v5.unity"),
            drivers=Object.FindObjectsOfType<UdonSharp.UdonSharpBehaviour>(true).Where(b=>b.gameObject.scene==scene&&b.GetType().Name.Contains("Presentation")).Select(b=>new{type=b.GetType().Name,b.enabled,b.gameObject.activeInHierarchy}).ToArray()};
        string json=JsonConvert.SerializeObject(report,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+label+".json",json);return json;
    }
    public static object RoomMaterials()
    {
        var rows=Object.FindObjectsOfType<MeshRenderer>(true).Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()&&r.GetComponent<MeshFilter>()!=null&&AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh).StartsWith("Assets/Room01/")).Select(r=>new{path=P(r.transform),mesh=r.GetComponent<MeshFilter>().sharedMesh.name,materials=r.sharedMaterials.Select((m,i)=>new{slot=i,name=m==null?"":m.name,path=m==null?"":AssetDatabase.GetAssetPath(m),texture=m==null||m.mainTexture==null?"":AssetDatabase.GetAssetPath(m.mainTexture)}).ToArray()}).ToArray();
        string json=JsonConvert.SerializeObject(rows,Formatting.Indented);File.WriteAllText(Tmp+"/reports/room01-materials.json",json);return new{success=true,count=rows.Length,books=rows.Where(r=>r.mesh=="Books"||r.mesh=="Book").ToArray()};
    }

    public static object AudioSources()
    {
        var run=Object.FindObjectsOfType<OldWebMap.OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==SceneManager.GetActiveScene());
        var rows=run.director.music.sources.Select((s,i)=>{string path=AssetDatabase.GetAssetPath(s.clip);var importer=(AudioImporter)AssetImporter.GetAtPath(path);return new{channel=i+1,asset_path=path,sha256=Hash(path),samples=s.clip.samples,frequency=s.clip.frequency,channels=s.clip.channels,compression=importer.defaultSampleSettings.compressionFormat.ToString(),drySameClip=run.audioFloor.drySources[i].clip==s.clip};}).ToArray();
        string json=JsonConvert.SerializeObject(new{success=true,scene=SceneManager.GetActiveScene().path,rows},Formatting.Indented);File.WriteAllText(Tmp+"/reports/music-source-proof.json",json);return json;
    }

    public static object SourceIntegrity()
    {
        var expected=Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-protected-sources.json"));
        var original=expected.Select(row=>{string path=(string)row["path"],actual=File.Exists(path)?Hash(path):"";return new{path,expected=(string)row["sha256"],actual,unchanged=actual==(string)row["sha256"]};}).ToArray();
        var manifest=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-integration-resources.json"));
        var imported=manifest["resources"].Select(row=>{string path=(string)row["destination"],actual=File.Exists(path)?Hash(path):"";return new{path,expected=(string)row["sha256"],actual,matching=actual==(string)row["sha256"]};}).ToArray();
        string modelPath="Assets/OldWebMap/FullFilm/Revision5/ModelAssets/Meshes/fullfilm_v5_branch_canopy.fbx";
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);var meshes=model.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Where(m=>m!=null).ToArray();
        long triangles=meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3));
        bool uv=meshes.All(m=>m.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0));
        var modelImport=new{path=modelPath,sha256=Hash(modelPath),meshes=meshes.Length,triangles,allMeshesHaveUv=uv,materials=model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().Count()};
        bool success=original.Length>0&&imported.Length>0&&original.All(r=>r.unchanged)&&imported.All(r=>r.matching)&&meshes.Length==75&&triangles==16128&&uv;
        string json=JsonConvert.SerializeObject(new{success,original,imported,modelImport,scope="Read-only hashes of pre-existing source files and the final V5 source import batch, plus actual Unity model import geometry counts"},Formatting.Indented);
        File.WriteAllText(Tmp+"/reports/source-integrity.json",json);if(!success)throw new Exception(json);return new{success,protectedSources=original.Length,matchingV5Imports=imported.Length};
    }

    public static object EndingLayout()
    {
        var run=Object.FindObjectsOfType<OldWebMap.OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==SceneManager.GetActiveScene());
        if(EditorApplication.isPlaying)throw new Exception("Stopped Editor required");
        try
        {
            run.Finished();
            var nodes=run.finalWorld.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"Gateway","Gateway_CommunitySign","Refined canopy","BridgeWalkway"}.Contains(t.name)).Select(t=>new{t.name,position=t.position.ToString(),local=t.localPosition.ToString(),rotation=t.rotation.eulerAngles.ToString(),scale=t.lossyScale.ToString(),meshes=t.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).Select(r=>new{r.name,centre=r.bounds.center.ToString(),size=r.bounds.size.ToString()}).ToArray()}).ToArray();
            string json=JsonConvert.SerializeObject(new{success=true,nodes},Formatting.Indented);File.WriteAllText(Tmp+"/reports/ending-layout.json",json);return json;
        }
        finally{run.Waiting();run.offlinePreview=false;}
    }

    public static object CreditsOcclusion()
    {
        var run=Object.FindObjectsOfType<OldWebMap.OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==SceneManager.GetActiveScene());
        if(EditorApplication.isPlaying)throw new Exception("Stopped Editor required");
        try
        {
            run.Finished();var page=run.details.creditsRoot.transform.Find("Borrowed assets page 2");
            var rows=new System.Collections.Generic.List<object>();Vector3 viewer=page.position-page.forward*2.9f;
            var method=typeof(HandleUtility).GetMethod("IntersectRayMesh",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            for(int column=15;column<=26;column++)
            {
                float x=Mathf.Lerp(-1.43f,1.43f,column/42f);Vector3 target=page.TransformPoint(new Vector3(x,0,-.04f));var delta=target-viewer;var ray=new Ray(viewer,delta.normalized);
                foreach(var r in Object.FindObjectsOfType<Renderer>().Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()&&r.enabled&&!r.transform.IsChildOf(run.details.creditsRoot.transform)))
                {
                    if(!r.bounds.IntersectRay(ray,out float distance)||distance>delta.magnitude)continue;var filter=r.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                    object[] args={ray,filter.sharedMesh,filter.transform.localToWorldMatrix,new RaycastHit()};bool hit=(bool)method.Invoke(null,args);var result=(RaycastHit)args[3];
                    rows.Add(new{column,x,name=P(r.transform),targetDistance=delta.magnitude,boundsDistance=distance,meshHit=hit,meshDistance=result.distance,meshPoint=result.point.ToString(),vertices=filter.sharedMesh.vertexCount,boundsCentre=r.bounds.center.ToString(),boundsSize=r.bounds.size.ToString()});
                }
            }
            string json=JsonConvert.SerializeObject(new{success=true,viewer=viewer.ToString(),page=page.position.ToString(),rotation=page.rotation.eulerAngles.ToString(),rows},Formatting.Indented);File.WriteAllText(Tmp+"/reports/credits-occlusion-probe.json",json);return json;
        }
        finally{run.Waiting();run.offlinePreview=false;}
    }

    public static object GroundSupport()
    {
        var run=Object.FindObjectsOfType<OldWebMap.OWFilmV5Presentation>(true).Single(r=>r.gameObject.scene==SceneManager.GetActiveScene());run.Waiting();run.Finished();Physics.SyncTransforms();
        try
        {
            var colliders=run.details.worldFloorColliders.Select(c=>new{path=P(c.transform),c.enabled,active=c.gameObject.activeInHierarchy,c.gameObject.layer,centre=c.bounds.center.ToString(),size=c.bounds.size.ToString()}).ToArray();
            var rows=new System.Collections.Generic.List<object>();
            foreach(float x in new[]{-4f,-2f,0,2,4})foreach(float z in new[]{-2.8f,0,2,4})
            {var hits=Physics.RaycastAll(new Vector3(x,3,z),Vector3.down,10,~0,QueryTriggerInteraction.Collide).Where(h=>run.details.worldFloorColliders.Contains(h.collider)).OrderBy(h=>h.distance).Select(h=>new{h.collider.name,y=h.point.y,point=h.point.ToString()}).ToArray();rows.Add(new{x,z,hits});}
            var report=new{success=true,colliders,rows};string json=JsonConvert.SerializeObject(report,Formatting.Indented);File.WriteAllText(Tmp+"/reports/ground-support.json",json);return json;
        }
        finally{run.Waiting();run.offlinePreview=false;}
    }

}
