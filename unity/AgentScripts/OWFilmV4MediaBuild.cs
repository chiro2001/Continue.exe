using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharpEditor;
using TMPro;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWFilmV4MediaBuild
{
    const string Root="Assets/OldWebMap/FullFilm/Revision4";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v4");
    static Transform Node(string name,Transform parent){var t=parent.Find(name);if(t!=null)return t;var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static Material Mat(string name,string shader){string path=Root+"/Materials/"+name+".mat";var sh=Shader.Find(shader);if(sh==null)throw new Exception(shader);var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(sh);AssetDatabase.CreateAsset(m,path);}else m.shader=sh;return m;}
    static Renderer Plane(string name,Transform parent,Material mat){var t=Node(name,parent);var f=t.GetComponent<MeshFilter>();if(f==null)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=Resources.GetBuiltinResource<Mesh>("Quad.fbx");var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;return r;}
    static Texture2D Tex(string file,int max=2048,bool point=false){string path=Root+"/Textures/"+Path.GetFileName(file);var imp=AssetImporter.GetAtPath(path)as TextureImporter;if(imp==null)throw new Exception("Missing media: "+path);imp.textureType=TextureImporterType.Default;imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=max;imp.wrapMode=TextureWrapMode.Clamp;imp.mipmapEnabled=true;imp.filterMode=point?FilterMode.Point:FilterMode.Bilinear;imp.alphaIsTransparency=point;imp.textureCompression=point?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
    public static object Build()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!="Assets/Scenes/OWM_FullFilm_v4.unity")throw new Exception("Stopped compiled V4 required");
        var run=Object.FindObjectOfType<OWFilmV4Presentation>(true);var m=run.media;var score=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v4-media-binding.json"));var frames=(JArray)score["history_frames"];var cues=(JArray)score["history_events"];var films=(JArray)score["films"];var emotes=(JArray)score["emotes"];
        m.historyFrames=frames.Select(f=>Tex((string)f["file"],2048)).ToArray();m.historyMainIndices=cues.Select(c=>(int)c["frame"]).ToArray();m.historyCompanionA=cues.Select(c=>(int)c["aside"][0]).ToArray();m.historyCompanionB=cues.Select(c=>(int)c["aside"][1]).ToArray();m.historyStarts=cues.Select(c=>(float)c["start"]).ToArray();m.historyEnds=cues.Select(c=>(float)c["end"]).ToArray();m.historyLabels=cues.Select(c=>(string)c["label"]).ToArray();
        m.historyRoot=Node("Actual VRChat historical UI and footage",run.basis.legacy.journey.worldFrame).gameObject;
        m.historyMainMaterial=Mat("Historical UI readable hold","OldWebMap/V2/Source Surface");m.historyAsideMaterial=Mat("Historical related UI flashbacks","OldWebMap/V2/Source Surface");
        m.historyMain=Plane("Historical main",m.historyRoot.transform,m.historyMainMaterial);m.historyAside=Plane("Historical related",m.historyRoot.transform,m.historyAsideMaterial);
        var textNode=Node("Historical interface caption",m.historyRoot.transform);var text=textNode.GetComponent<TextMeshPro>();if(text==null)text=textNode.gameObject.AddComponent<TextMeshPro>();text.GetType().GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(text,null);text.font=run.basis.keyText.font;text.fontSharedMaterial=text.font.material;text.fontSize=1.1f;text.alignment=TextAlignmentOptions.Center;text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Overflow;text.rectTransform.sizeDelta=new Vector2(4,.24f);text.transform.localPosition=new Vector3(-1.6f,1.36f,1.75f);m.historyDate=text;
        m.filmAtlases=films.Select(f=>Tex((string)f["file"],4096)).ToArray();m.filmStarts=films.Select(f=>(float)f["start"]).ToArray();m.filmEnds=films.Select(f=>(float)f["end"]).ToArray();m.filmFps=films.Select(f=>(float)f["fps"]).ToArray();m.filmCounts=films.Select(f=>(int)f["frames"]).ToArray();m.filmOnAside=films.Select(f=>(bool)f["aside"]).ToArray();
        m.filmMaterial=Mat("Actual silent footage ten fps","OldWebMap/FilmV4/Archive Filmstrip");m.filmMaterial.SetVector("_Grid",new Vector4(8,8,0,0));m.filmPlane=Plane("Silent actual gameplay frames",m.historyRoot.transform,m.filmMaterial);
        m.emotes=emotes.Select(e=>Tex((string)e["file"],512,true)).ToArray();m.emoteStarts=emotes.Select(e=>(float)e["start"]).ToArray();m.emoteEnds=emotes.Select(e=>(float)e["end"]).ToArray();m.emoteAspects=emotes.Select(e=>(float)e["aspect"]).ToArray();m.emotePlanes=new Transform[12];m.emoteMaterials=new Material[12];
        var root=Node("Historical community emoticons",run.transform);for(int i=0;i<12;i++){var mat=Mat("Historical emoticon "+i,"Unlit/Transparent");m.emoteMaterials[i]=mat;m.emotePlanes[i]=Plane("Historical emoticon "+i,root,mat).transform;}
        run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(m);EditorUtility.SetDirty(m);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(m));EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var result=new{success=true,historyFrames=frames.Count,readableEvents=cues.Count,silentFootageAtlases=films.Count,historicalEmotes=emotes.Count,newAudioSources=0,videoAudio="No VideoPlayer or extra sound source: source video is stripped of audio, actual frames are driven by the shared performance clock"};Directory.CreateDirectory(Tmp+"/reports");var json=JsonConvert.SerializeObject(result,Formatting.Indented);File.WriteAllText(Tmp+"/reports/media-build.json",json);return json;
    }
}
