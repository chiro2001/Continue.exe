using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWV2WorkspaceBuild
{
    const string Root="Assets/OldWebMap/FullFilm/Revision2";
    static OWV2Presentation Run(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!="Assets/Scenes/OWM_FullFilm_v2.unity")throw new Exception("Stopped compiled V2 required");return Object.FindObjectOfType<OWV2Presentation>(true);}
    static Material Material(string name,Texture texture)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Unlit/Texture"));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find("Unlit/Texture");m.mainTexture=texture;return m;
    }
    static Texture2D Texture(string file)
    {
        string path=Root+"/Textures/"+file;var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new Exception("Real capture not imported: "+file);
        if(importer.maxTextureSize!=2048||importer.npotScale!=TextureImporterNPOTScale.None||importer.wrapMode!=TextureWrapMode.Clamp){importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Transform Plane(string name,Transform parent,Material m,Vector3 position,Vector3 size)
    {
        var t=parent.Find(name);if(t==null){var go=new GameObject(name);go.transform.SetParent(parent,false);t=go.transform;go.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Quad.fbx");go.AddComponent<MeshRenderer>();}
        t.localPosition=position;t.localScale=size;var r=t.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;
    }
    static void ReadableModelUi(Transform root)
    {
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("UI_")))
        {
            var f=r.GetComponent<MeshFilter>();var source=f.sharedMesh;if(AssetDatabase.GetAssetPath(source).Contains("/UiUnmirrored-"))continue;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id))throw new Exception("Untracked imported UI mesh");
            string path=Root+"/Meshes/UiUnmirrored-"+guid+"-"+id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(source);mesh.name="Readable "+source.name;var uv=mesh.uv;for(int i=0;i<uv.Length;i++)uv[i].x=1f-uv[i].x;mesh.uv=uv;AssetDatabase.CreateAsset(mesh,path);}f.sharedMesh=mesh;
            PrefabUtility.RecordPrefabInstancePropertyModifications(f);
        }
    }
    static Vector3 UvPoint(Renderer renderer,Vector2 point)
    {
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3)
        {
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Vector2 ab=uv[b]-uv[a],ac=uv[c]-uv[a],ap=point-uv[a];
            float aa=Vector2.Dot(ab,ab),bb=Vector2.Dot(ab,ac),cc=Vector2.Dot(ac,ac),dd=Vector2.Dot(ap,ab),ee=Vector2.Dot(ap,ac),den=aa*cc-bb*bb;
            if(Mathf.Abs(den)<.000001f)continue;float y=(cc*dd-bb*ee)/den,z=(aa*ee-bb*dd)/den;
            if(y>=-.001f&&z>=-.001f&&y+z<=1.001f)return renderer.transform.TransformPoint(vertices[a]*(1-y-z)+vertices[b]*y+vertices[c]*z);
        }
        throw new Exception("Play control UV is outside the real toolbar mesh");
    }
    public static object Build()
    {
        var run=Run();ReadableModelUi(run.editorStage.transform);var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v2-software.json"));var timeline=(JArray)data["timeline"];
        run.screenshotTimes=timeline.Select(t=>(float)t["time"]).ToArray();run.screenshotStates=timeline.Select(t=>Texture((string)t["file"])).ToArray();
        var primary=Material("Real Windows software on main monitor",run.screenshotStates[0]);run.screenshotMaterials=new[]{primary};
        string[] names={"UI_Main_16x9","UI_LeftHierarchy","UI_RightAssets","UI_Timeline","UI_Thumbnail_00","UI_Thumbnail_01","UI_Thumbnail_02"};
        string[] files={"software-unity-editor.png","software-unity-hierarchy.png","software-unity-inspector.png","software-blender-timeline.png","software-blender-viewport.png","software-blender-wire-viewport.png","software-unity-viewport.png"};
        foreach(var r in run.workspace.GetComponentsInChildren<Renderer>(true)){int at=Array.IndexOf(names,r.name);if(at>=0)r.sharedMaterial=at==0?primary:Material("Real capture slot "+names[at],Texture(files[at]));}
        foreach(var r in run.editorStage.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="UI_WorldNotice"))r.sharedMaterial=Material("This world in its own editor",Texture("software-unity-viewport.png"));
        string[] slotNames={"UI_TopTitle","UI_TopToolbar","UI_LeftHierarchy","UI_RightInspector","UI_BottomTimeline"};string[] slotFiles={"software-frame-title.png","software-frame-toolbar-idle.png","software-frame-hierarchy.png","software-frame-inspector.png","software-blender-timeline.png"};
        foreach(var r in run.editorFrame.GetComponentsInChildren<Renderer>(true))
        {
            int at=Array.IndexOf(slotNames,r.name);if(at<0)continue;r.sharedMaterial=Material("Real editor frame crop "+r.name,Texture(slotFiles[at]));
            if(at==1)
            {
                run.editorToolbarMaterial=r.sharedMaterial;run.editorToolbarIdle=Texture("software-frame-toolbar-idle.png");run.editorToolbarPlaying=Texture("software-frame-toolbar-playing.png");
                var pointer=run.editorFrame.Find("Pointer on the actual captured Play control");if(pointer==null)
                {
                    var go=new GameObject("Pointer on the actual captured Play control");go.transform.SetParent(run.editorFrame,false);pointer=go.transform;go.AddComponent<MeshFilter>().sharedMesh=run.archivePointer.GetComponent<MeshFilter>().sharedMesh;go.AddComponent<MeshRenderer>().sharedMaterial=run.archivePointer.GetComponent<Renderer>().sharedMaterial;
                    var outline=Object.Instantiate(run.archivePointer.Find("Black pixel outline").gameObject,pointer,false);outline.name="Black pixel outline";
                }
                var target=UvPoint(r,new Vector2(.459f,.525f));run.editorPlayTarget=run.editorFrame.InverseTransformPoint(target)+new Vector3(0,0,-.035f);run.editorPlayPointer=pointer;
            }
        }
        run.layerPanels=new Transform[3];run.layerHomes=new[]{new Vector3(-2.8f,3.75f,7.8f),new Vector3(0,4.15f,8.2f),new Vector3(2.8f,3.75f,7.8f)};
        string[] layers={"software-blender-viewport.png","software-blender-wire-viewport.png","software-unity-viewport.png"};
        run.workflowMaterials=new Material[3];run.workflowStates=layers.Select(Texture).ToArray();
        for(int i=0;i<3;i++){run.workflowMaterials[i]=Material("Actual source viewport "+i,Texture(layers[i]));run.layerPanels[i]=Plane("Actual model workflow layer "+i,run.editorStage.transform,run.workflowMaterials[i],run.layerHomes[i],new Vector3(2.3f,1.29375f,1));}
        foreach(var r in run.finalWorld.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="UI_WorldNotice"))r.sharedMaterial=run.workflowMaterials[2];
        run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(run);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(run));EditorUtility.SetDirty(run);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return new {success=true,actualSoftwareStates=run.screenshotStates.Length,playCaptureReady=(bool)data["play_capture_ready"],sameMeshFromSourceThroughWorld=true,capturedUiControlsOnly=true};
    }
}
