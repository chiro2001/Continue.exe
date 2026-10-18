using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UdonSharp;
using UdonSharpEditor;
using TMPro;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWFilmV4Build
{
    public const string Root="Assets/OldWebMap/FullFilm/Revision4";
    public const string Scene="Assets/Scenes/OWM_FullFilm_v4.unity";
    const string Source="Assets/Scenes/OWM_FullFilm_v3.unity";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v4");
    static T F<T>()where T:Component=>Object.FindObjectsOfType<T>(true).FirstOrDefault(x=>x.gameObject.scene==SceneManager.GetActiveScene());
    static OWFilmV4Presentation Run(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=Scene)throw new Exception("Stopped compiled full-film V4 required");return F<OWFilmV4Presentation>();}
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    static string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static object Report(string file,object value){Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+file+".json",json);return json;}
    static Transform Node(string name,Transform parent){var old=parent==null?SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name==name)?.transform:parent.Find(name);if(old!=null)return old;var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static void Style(Renderer r){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static void Sync(UdonSharpBehaviour b){UdonSharpEditorUtility.CopyProxyToUdon(b);EditorUtility.SetDirty(b);var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)EditorUtility.SetDirty(vm);}
    static void Copy(object from,object to){foreach(var field in from.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance)){var target=to.GetType().GetField(field.Name);if(target!=null&&target.FieldType.IsAssignableFrom(field.FieldType))target.SetValue(to,field.GetValue(from));}}
    static Material Mat(string name,string shader){string path=Root+"/Materials/"+name+".mat";var sh=Shader.Find(shader);if(sh==null)throw new Exception("Missing shader "+shader);var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(sh);AssetDatabase.CreateAsset(m,path);}else m.shader=sh;m.enableInstancing=true;return m;}
    static Transform Plane(string name,Transform parent,Material material,Vector3 p,Vector3 size,bool solid=false){var t=Node(name,parent);var f=t.GetComponent<MeshFilter>();if(f==null)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=Resources.GetBuiltinResource<Mesh>(solid?"Cube.fbx":"Quad.fbx");var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;Style(r);t.localPosition=p;t.localScale=size;return t;}
    static TMP_Text Text(string name,Transform parent,string value,Vector3 p,Vector2 size,float fontSize,TMP_FontAsset font){var t=Node(name,parent);var x=t.GetComponent<TextMeshPro>();if(x==null)x=t.gameObject.AddComponent<TextMeshPro>();x.GetType().GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(x,null);x.font=font;x.fontSharedMaterial=font.material;x.text=value;x.fontSize=fontSize;x.alignment=TextAlignmentOptions.Center;x.enableWordWrapping=false;x.overflowMode=TextOverflowModes.Overflow;x.rectTransform.sizeDelta=size;x.transform.localPosition=p;return x;}
    static Texture2D Tex(string file,int max=2048,bool data=false){string path=Root+"/Textures/"+file;var imp=AssetImporter.GetAtPath(path)as TextureImporter;if(imp==null)throw new Exception("Missing texture "+path);imp.textureType=TextureImporterType.Default;imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=max;imp.wrapMode=TextureWrapMode.Clamp;imp.mipmapEnabled=!data;imp.sRGBTexture=!data;imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.alphaIsTransparency=false;imp.filterMode=FilterMode.Bilinear;imp.textureCompression=data?TextureImporterCompression.Uncompressed:TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
    public static object Folders(){foreach(string dir in new[]{Root,Root+"/Materials",Root+"/Textures",Root+"/Fonts",Root+"/Meshes",Root+"/Programs"})Folder(dir);return new{success=true,root=Root};}
    public static object Programs(){Folders();var existing=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Where(p=>p!=null).ToArray();var made=new List<string>();foreach(string guid in AssetDatabase.FindAssets("OWFilmV4 t:MonoScript",new[]{"Assets/OldWebMap/Scripts"})){var script=AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));if(script==null||existing.Any(p=>p.sourceCsScript==script))continue;var asset=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();asset.sourceCsScript=script;AssetDatabase.CreateAsset(asset,Root+"/Programs/"+script.name+".asset");made.Add(script.name);}UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new Exception("Udon compile failed");AssetDatabase.SaveAssets();return new{success=true,made};}
    public static object Create()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().isDirty)throw new Exception("Preserve user edits: clean stopped source required");
        if(File.Exists(Scene))throw new Exception("V4 exists: do not replace it");string sourceHash=Hash(Source);Folders();if(!AssetDatabase.CopyAsset(Source,Scene))throw new Exception("V3 copy failed");EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        var old=F<OWV3Presentation>();if(old==null)throw new Exception("V3 driver missing");var root=Node("Full-film V4 Performance",null);
        var run=root.gameObject.AddUdonSharpComponent<OWFilmV4Presentation>();var basis=root.gameObject.AddUdonSharpComponent<OWFilmV4BasePresentation>();var signal=Node("V4 Signal",root).gameObject.AddUdonSharpComponent<OWFilmV4Signal>();var floor=Node("V4 Audio",root).gameObject.AddUdonSharpComponent<OWFilmV4AudioFloor>();var details=root.gameObject.AddUdonSharpComponent<OWFilmV4Details>();var media=root.gameObject.AddUdonSharpComponent<OWFilmV4Media>();
        Copy(old,run);Copy(old.basis,basis);Copy(old.basis.signal,signal);Copy(old.audioFloor,floor);Copy(old.details,details);run.basis=basis;basis.signal=signal;basis.drivenExternally=true;run.audioFloor=floor;run.details=details;details.run=run;run.media=media;media.run=run;
        foreach(var b in new UdonSharpBehaviour[]{old,old.basis,old.basis.signal,old.audioFloor,old.details}){b.enabled=false;var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)vm.enabled=false;}old.offlinePreview=true;
        IsolateMaterials();run.Waiting();run.offlinePreview=false;foreach(var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))Sync(b);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("integration",new{success=true,source=Source,sourceHash,sourcePreserved=Hash(Source)==sourceHash,scene=Scene});
    }
    static void IsolateMaterials()
    {
        var behaviours=Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.gameObject.scene==SceneManager.GetActiveScene()).ToArray();var map=new Dictionary<Material,Material>();
        var mats=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).ToList();
        foreach(var b in behaviours)foreach(var f in b.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public)){if(f.FieldType==typeof(Material)){var m=(Material)f.GetValue(b);if(m!=null)mats.Add(m);}else if(f.FieldType==typeof(Material[])){var a=(Material[])f.GetValue(b);if(a!=null)mats.AddRange(a.Where(m=>m!=null));}}
        foreach(var m in mats.Distinct()){if(AssetDatabase.GetAssetPath(m).StartsWith(Root+"/"))continue;string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m));if(string.IsNullOrEmpty(guid))continue;string path=Root+"/Materials/Inherited-"+guid+".mat";var copy=AssetDatabase.LoadAssetAtPath<Material>(path);if(copy==null){copy=new Material(m){name="V4 "+m.name};AssetDatabase.CreateAsset(copy,path);}map[m]=copy;}
        foreach(var b in behaviours)foreach(var f in b.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public)){if(f.FieldType==typeof(Material)){var m=(Material)f.GetValue(b);if(m!=null&&map.TryGetValue(m,out var c))f.SetValue(b,c);}else if(f.FieldType==typeof(Material[])){var a=(Material[])f.GetValue(b);if(a!=null)f.SetValue(b,a.Select(m=>m!=null&&map.TryGetValue(m,out var c)?c:m).ToArray());}}
        foreach(var r in Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()))r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&map.TryGetValue(m,out var c)?c:m).ToArray();
        foreach(var t in Object.FindObjectsOfType<TMP_Text>(true).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()))if(t.fontSharedMaterial!=null&&map.TryGetValue(t.fontSharedMaterial,out var c))t.fontSharedMaterial=c;
    }
    public static object Typography()
    {
        var run=Run();string path=Root+"/Fonts/NotoSansCJKsc SDF.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(asset==null){var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/NotoSansCJKsc-Regular.otf");if(source==null)throw new Exception("SC source font missing");asset=TMP_FontAsset.CreateFontAsset(source,64,7,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,true);asset.name="Noto Sans CJK SC Film V4";AssetDatabase.CreateAsset(asset,path);AssetDatabase.AddObjectToAsset(asset.material,asset);foreach(var atlas in asset.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,asset);}
        var texts=Object.FindObjectsOfType<TMP_Text>(true).Where(x=>x.gameObject.scene==SceneManager.GetActiveScene()).ToArray();
        string chars=string.Concat(texts.Select(x=>x.text))+File.ReadAllText("AgentScripts/data/fullfilm-v4-design.json")+"盗模授权未经许可复制上传模型由谁决定社区提案尚非已实施规则还可以写点什么一首旧曲一起听继续开始重播";
        string missing;asset.TryAddCharacters(new string(chars.Where(c=>!char.IsSurrogate(c)).Distinct().ToArray()),out missing);
        foreach(var text in texts){text.GetType().GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(text,null);text.font=asset;text.fontSharedMaterial=asset.material;EditorUtility.SetDirty(text);}EditorUtility.SetDirty(asset);EditorUtility.SetDirty(asset.material);foreach(var atlas in asset.atlasTextures)EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();return Report("typography",new{success=true,face=asset.faceInfo.familyName,source=AssetDatabase.GetAssetPath(asset.sourceFontFile),texts=texts.Length,unavailableCharacters=missing,atlasCount=asset.atlasTextureCount});
    }
    static Vector3 UvPoint(Renderer renderer,Vector2 point)
    {
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Vector2 ab=uv[b]-uv[a],ac=uv[c]-uv[a],ap=point-uv[a];float aa=Vector2.Dot(ab,ab),bb=Vector2.Dot(ab,ac),cc=Vector2.Dot(ac,ac),dd=Vector2.Dot(ap,ab),ee=Vector2.Dot(ap,ac),den=aa*cc-bb*bb;if(Mathf.Abs(den)<.000001f)continue;float y=(cc*dd-bb*ee)/den,z=(aa*ee-bb*dd)/den;if(y>=-.001f&&z>=-.001f&&y+z<=1.001f)return renderer.transform.TransformPoint(vertices[a]*(1-y-z)+vertices[b]*y+vertices[c]*z);}
        throw new Exception("Actual Play-control UV outside monitor mesh");
    }
    public static object Core()
    {
        var run=Run();var d=run.details;var design=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v4-design.json"));
        d.textRibbonMaterial=Mat("Story ribbon main","OldWebMap/FilmV4/Story Ribbon");d.textRibbon.GetComponent<Renderer>().sharedMaterial=d.textRibbonMaterial;
        var ribbons=(JArray)design["ribbons"];d.ribbonStates=new Texture2D[ribbons.Count];d.ribbonPixels=new float[ribbons.Count];d.ribbonStarts=new float[ribbons.Count];d.ribbonEnds=new float[ribbons.Count];d.ribbonSpeeds=new float[ribbons.Count];
        for(int i=0;i<ribbons.Count;i++){d.ribbonStates[i]=Tex(Path.GetFileName((string)ribbons[i]["file"]),4096,true);d.ribbonPixels[i]=(float)ribbons[i]["pixels"];d.ribbonStarts[i]=(float)ribbons[i]["start"];d.ribbonEnds[i]=(float)ribbons[i]["end"];d.ribbonSpeeds[i]=(float)ribbons[i]["speed_px_s"];}
        d.ribbonBranches=new Transform[2];d.ribbonBranchMaterials=new Material[2];for(int i=0;i<2;i++){var mat=Mat("Story ribbon converging "+i,"OldWebMap/FilmV4/Story Ribbon");mat.SetFloat("_Branch",i*2.1f);var t=Node("Converging story ribbon "+i,run.transform);var f=t.GetComponent<MeshFilter>();if(f==null)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=d.textRibbon.GetComponent<MeshFilter>().sharedMesh;var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;Style(r);d.ribbonBranches[i]=t;d.ribbonBranchMaterials[i]=mat;}
        d.pixelCylinderMaterial.shader=Shader.Find("OldWebMap/FilmV4/EDGE TEAM Wave Cylinder");d.pixelCylinderMaterial.SetFloat("_WaveAmp",.24f);d.pixelCylinderMaterial.SetFloat("_WaveCount",2);
        d.openingRoom=Node("Independent enclosed waiting room",run.transform);d.openingWalls=new Transform[6];d.openingWallHomes=new[]{new Vector3(0,3,-10.8f),new Vector3(0,3,10),new Vector3(-10,3,-.4f),new Vector3(10,3,-.4f),new Vector3(0,6.2f,-.4f),new Vector3(0,-.10f,-.4f)};
        var dark=Mat("Enclosed waiting walls","Unlit/Color");dark.color=new Color(.011f,.018f,.033f);Vector3[] sizes={new Vector3(20,6.4f,.15f),new Vector3(20,6.4f,.15f),new Vector3(.15f,6.4f,21),new Vector3(.15f,6.4f,21),new Vector3(20,.15f,21),new Vector3(20,.15f,21)};
        for(int i=0;i<6;i++)d.openingWalls[i]=Plane("Closed room face "+i,d.openingRoom,dark,d.openingWallHomes[i],sizes[i],true);
        if(d.rearWall!=null)d.rearWall.gameObject.SetActive(false);
        var backing=run.finalWordRoot.transform.Find("Quiet continue background");if(backing!=null){d.wordBackground=Mat("Continue assembled light","OldWebMap/FilmV4/Continue Assembly");d.wordBackground.renderQueue=2990;backing.GetComponent<Renderer>().sharedMaterial=d.wordBackground;backing.localScale=new Vector3(2.65f,.85f,1);}
        d.wordEchoes=new TMP_Text[2];d.wordEchoTransforms=new Transform[2];for(int i=0;i<2;i++){d.wordEchoes[i]=Text("Continue assembly echo "+i,run.finalWordRoot.transform,"继续",Vector3.zero,new Vector2(3,1),run.finalWord.fontSize,run.basis.keyText.font);d.wordEchoTransforms[i]=d.wordEchoes[i].transform;}
        run.Preview(216.96f);var screen=run.workspace.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="UI_Main_16x9");var emitter=Node("Actual monitor screen origin",run.workspace);emitter.position=screen.transform.TransformPoint(screen.GetComponent<MeshFilter>().sharedMesh.bounds.center);run.workspaceScreen=emitter;
        var control=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v4-workspace-control.json"));var play=Node("Actual captured monitor Play button",run.workspace);play.position=UvPoint(screen,new Vector2((float)control["play_control_uv"][0],(float)control["play_control_uv"][1]));run.workspacePlayControl=play;run.editorPlayPointer.SetParent(run.transform,true);if(run.screenshotTimes.Length>4)run.screenshotTimes[4]=217.6f;Vector3 screenOrigin=emitter.position;
        for(int i=0;i<run.nativeStates.Length;i++){string[] groups={"readme","manga","torrent","forum","subtitles","access"};run.nativeStates[i]=Tex("native-"+groups[i/4]+"-"+(i%4).ToString("00")+"-full.png");run.nativeCompanions[i]=Tex("native-"+groups[i/4]+"-"+(i%4).ToString("00")+"-secondary.png");}
        run.fieldMaterial.shader=Shader.Find("OldWebMap/FilmV4/Archive Field");run.fieldMaterial.mainTexture=Tex("archive-cover-atlas.png",4096);
        // Increase readable foreground without changing the fixed audio coordinate frame.
        run.archiveFile.gameObject.SetActive(false);
        run.Waiting();run.offlinePreview=false;foreach(var b in new UdonSharpBehaviour[]{run,d,run.basis,run.basis.signal})Sync(b);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("core-build",new{success=true,ribbons=ribbons.Count,closedRoomFaces=d.openingWalls.Length,edgeWaveAmplitude=.24f,screenOrigin=screenOrigin.ToString(),archivePaddingRemoved=true});
    }
    public static object Related()
    {
        var run=Run();var m=run.media;var score=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v4-related-focus.json"));var groups=(JArray)score["groups"];
        m.sources=score["sources"].Select(s=>Tex(Path.GetFileName((string)s["unity_path"]),1024)).ToArray();m.aspects=run.archiveAspects;m.labels=score["sources"].Select(s=>(string)s["label"]).ToArray();if(m.sources.Any(t=>t==null))throw new Exception("Related foreground source binding has null entries");m.groupStarts=groups.Select(g=>(float)g["start"]).ToArray();m.groupEnds=groups.Select(g=>(float)g["end"]).ToArray();m.poolOffsets=new int[groups.Count];m.poolCounts=new int[groups.Count];var indices=new List<int>();for(int i=0;i<groups.Count;i++){m.poolOffsets[i]=indices.Count;var a=groups[i]["indices"].Select(x=>(int)x).ToArray();m.poolCounts[i]=a.Length;indices.AddRange(a);}m.poolIndices=indices.ToArray();
        m.relatedRoot=Node("Related sources within the attention area",run.transform);m.relatedPlanes=new Transform[6];m.relatedMaterials=new Material[6];m.relatedLabels=new TMP_Text[6];m.relatedLabelTransforms=new Transform[6];m.sampledRelated=Enumerable.Repeat(-1,6).ToArray();
        for(int i=0;i<6;i++){m.relatedMaterials[i]=Mat("Related source "+i,"OldWebMap/V2/Source Surface");m.relatedPlanes[i]=Plane("Independent related source "+i,m.relatedRoot,m.relatedMaterials[i],Vector3.zero,Vector3.one);m.relatedLabels[i]=Text("Source caption "+i,m.relatedRoot,"",Vector3.zero,new Vector2(3,.2f),.84f,run.basis.keyText.font);m.relatedLabelTransforms[i]=m.relatedLabels[i].transform;}
        m.provenanceHeading=Node("Avatar provenance visible subject",run.details.provenanceRoot.transform);m.provenanceHeading.localPosition=new Vector3(-.5f,4.05f,5.10f);
        m.provenanceHeadingMaterial=Mat("Avatar provenance high contrast","OldWebMap/V2/Source Surface");m.provenanceHeadingMaterial.renderQueue=2990;m.provenanceHeadingMaterial.SetColor("_Tint",new Color(.006f,.012f,.020f,1));Plane("Subject backing",m.provenanceHeading,m.provenanceHeadingMaterial,Vector3.forward*.025f,new Vector3(5.2f,.9f,1));
        m.provenanceTitle=Text("Subject",m.provenanceHeading,"盗模",new Vector3(0,.16f,0),new Vector2(5,.5f),3.5f,run.basis.keyText.font);m.provenanceSubtitle=Text("Subject context",m.provenanceHeading,"未经许可复制、上传",new Vector3(0,-.25f,0),new Vector2(5,.3f),1.38f,run.basis.keyText.font);
        m.playPulseMaterial=Mat("Screen crossing light","OldWebMap/V3/Attached Focus Halo");m.playPulse=Plane("Play expands from actual monitor",run.transform,m.playPulseMaterial,Vector3.zero,Vector3.one);
        run.Waiting();run.offlinePreview=false;Sync(m);Sync(run);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("related-build",new{success=true,groups=groups.Count,independentPlanes=m.relatedPlanes.Length,sourceBindings=m.sources.Length,provenanceSubject=true});
    }
    public static object PenSide()
    {
        var run=Run();run.Finished();Physics.SyncTransforms();var d=run.details;var root=d.penRoot.transform;var old=root.position;Vector3[] candidates={new Vector3(3.4f,.52f,.8f),new Vector3(3.0f,.52f,-.4f),new Vector3(-3.2f,.52f,.8f)};Vector3 chosen=old;bool found=false;
        foreach(var p in candidates){var hits=Physics.RaycastAll(p+Vector3.up*2,Vector3.down,5).Where(h=>d.worldFloorColliders.Contains(h.collider)).OrderBy(h=>h.distance).ToArray();if(hits.Length==0)continue;chosen=new Vector3(p.x,.52f,p.z);found=true;break;}
        if(!found)throw new Exception("No supported side position for the original QvPen rack");root.position=chosen;run.Waiting();run.offlinePreview=false;Sync(d);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("qvpen-placement",new{success=true,old=old.ToString(),position=chosen.ToString(),originalPrefabPreserved=true,side=true});
    }
    public static object Credits()
    {
        var run=Run();var texture=Tex("production-sources.png",2048);var material=Mat("World sources and music attribution","Unlit/Texture");material.mainTexture=texture;
        var boards=run.finalWorld.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="UI_WorldNotice").ToArray();if(boards.Length==0)throw new Exception("Existing source board missing");foreach(var board in boards)board.sharedMaterial=material;EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("credits",new{success=true,boards=boards.Length,texture=AssetDatabase.GetAssetPath(texture),visibleFrom=217.6f,caption="Source names and the credited original video author, without a software download link"});
    }
    public static object Save()
    {
        var run=Run();run.Waiting();run.offlinePreview=false;
        foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.gameObject.scene==SceneManager.GetActiveScene()))Sync(b);
        foreach(var b in new UdonSharpBehaviour[]{F<OWFullPresentation>(),F<OWFullSignal>(),F<OWV2Presentation>(),F<OWV2BasePresentation>(),F<OWV2Signal>(),F<OWV2AudioFloor>(),F<OWV3Presentation>(),F<OWV3BasePresentation>(),F<OWV3Signal>(),F<OWV3AudioFloor>(),F<OWV3Details>()})if(b!=null){b.enabled=false;var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)vm.enabled=false;}
        foreach(var t in Object.FindObjectsOfType<Transform>(true).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&PrefabUtility.IsPartOfPrefabInstance(t))){PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return Report("saved",new{success=true,scene=Scene,sha256=Hash(Scene),v3Sha256=Hash(Source),v2Sha256=Hash("Assets/Scenes/OWM_FullFilm_v2.unity"),v1Sha256=Hash("Assets/Scenes/OWM_FullFilm_v1.unity"),v5Sha256=Hash("Assets/Scenes/OWM_Episode123_v5.unity"),playing=EditorApplication.isPlaying});
    }
}
