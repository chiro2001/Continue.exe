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

public static class OWFilmV5Build
{
    public const string Root="Assets/OldWebMap/FullFilm/Revision5";
    public const string Scene="Assets/Scenes/OWM_FullFilm_v5.unity";
    const string Source="Assets/Scenes/OWM_FullFilm_v4.unity";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5");
    static T F<T>()where T:Component=>Object.FindObjectsOfType<T>(true).FirstOrDefault(x=>x.gameObject.scene==SceneManager.GetActiveScene());
    static OWFilmV5Presentation Run(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=Scene)throw new Exception("Stopped compiled full-film V5 required");return F<OWFilmV5Presentation>();}
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
    public static object Programs(){Folders();var existing=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Where(p=>p!=null).ToArray();var made=new List<string>();foreach(string guid in AssetDatabase.FindAssets("OWFilmV5 t:MonoScript",new[]{"Assets/OldWebMap/Scripts"})){var script=AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));if(script==null||existing.Any(p=>p.sourceCsScript==script))continue;var asset=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();asset.sourceCsScript=script;AssetDatabase.CreateAsset(asset,Root+"/Programs/"+script.name+".asset");made.Add(script.name);}UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new Exception("Udon compile failed");AssetDatabase.SaveAssets();return new{success=true,made};}
    public static object Create()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().isDirty)throw new Exception("Preserve user edits: clean stopped source required");
        if(File.Exists(Scene))throw new Exception("V5 already exists: do not replace it");
        string sourceHash=Hash(Source);Folders();if(!AssetDatabase.CopyAsset(Source,Scene))throw new Exception("V4 copy failed");
        EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
        var old=F<OWFilmV4Presentation>();if(old==null)throw new Exception("Full-film V4 source driver missing");
        var root=Node("Full-film V5 Performance",null);
        var run=root.gameObject.AddUdonSharpComponent<OWFilmV5Presentation>();
        var basis=root.gameObject.AddUdonSharpComponent<OWFilmV5BasePresentation>();
        var signal=Node("V5 Signal",root).gameObject.AddUdonSharpComponent<OWFilmV5Signal>();
        var floor=Node("V5 Audio",root).gameObject.AddUdonSharpComponent<OWFilmV5AudioFloor>();
        var details=root.gameObject.AddUdonSharpComponent<OWFilmV5Details>();
        var media=root.gameObject.AddUdonSharpComponent<OWFilmV5Media>();
        var provenance=root.gameObject.AddUdonSharpComponent<OWFilmV5Provenance>();
        Copy(old,run);Copy(old.basis,basis);Copy(old.basis.signal,signal);Copy(old.audioFloor,floor);Copy(old.details,details);Copy(old.media,media);
        run.basis=basis;basis.signal=signal;basis.drivenExternally=true;run.audioFloor=floor;run.details=details;details.run=run;run.media=media;media.run=run;run.provenance=provenance;provenance.run=run;
        foreach(var b in new UdonSharpBehaviour[]{old,old.basis,old.basis.signal,old.audioFloor,old.details,old.media}){b.enabled=false;var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)vm.enabled=false;}
        old.offlinePreview=true;
        IsolateMaterials();run.Waiting();run.offlinePreview=false;
        foreach(var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))Sync(b);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("integration",new{success=true,source=Source,sourceHash,sourcePreserved=Hash(Source)==sourceHash,scene=Scene});
    }
    static void IsolateMaterials()
    {
        var scene=SceneManager.GetActiveScene();
        var behaviours=Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.gameObject.scene==scene).ToArray();
        var map=new Dictionary<Material,Material>();
        var mats=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==scene).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).ToList();
        foreach(var b in behaviours)foreach(var f in b.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))
        {if(f.FieldType==typeof(Material)){var m=(Material)f.GetValue(b);if(m!=null)mats.Add(m);}else if(f.FieldType==typeof(Material[])){var a=(Material[])f.GetValue(b);if(a!=null)mats.AddRange(a.Where(m=>m!=null));}}
        foreach(var m in mats.Distinct())
        {
            if(AssetDatabase.GetAssetPath(m).StartsWith(Root+"/"))continue;
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long fileId))continue;
            string path=Root+"/Materials/Inherited-"+guid+"-"+fileId+".mat";
            var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(copy==null){copy=new Material(m){name="V5 "+m.name};AssetDatabase.CreateAsset(copy,path);}
            map[m]=copy;
        }
        foreach(var b in behaviours)foreach(var f in b.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))
        {if(f.FieldType==typeof(Material)){var m=(Material)f.GetValue(b);if(m!=null&&map.TryGetValue(m,out var c))f.SetValue(b,c);}else if(f.FieldType==typeof(Material[])){var a=(Material[])f.GetValue(b);if(a!=null)f.SetValue(b,a.Select(m=>m!=null&&map.TryGetValue(m,out var c)?c:m).ToArray());}}
        foreach(var r in Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==scene))r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null&&map.TryGetValue(m,out var c)?c:m).ToArray();
        foreach(var t in Object.FindObjectsOfType<TMP_Text>(true).Where(t=>t.gameObject.scene==scene))if(t.fontSharedMaterial!=null&&map.TryGetValue(t.fontSharedMaterial,out var c))t.fontSharedMaterial=c;
    }
    public static object Typography()
    {
        var run=Run();string path=Root+"/Fonts/NotoSansCJKsc SDF.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(asset==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/OldWebMap/FullFilm/Revision4/Fonts/NotoSansCJKsc-Regular.otf");
            if(source==null)throw new Exception("Simplified Chinese source font missing");
            asset=TMP_FontAsset.CreateFontAsset(source,64,7,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,true);
            asset.name="Noto Sans CJK SC Film V5";AssetDatabase.CreateAsset(asset,path);AssetDatabase.AddObjectToAsset(asset.material,asset);
            foreach(var atlas in asset.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,asset);
        }
        var texts=Object.FindObjectsOfType<TMP_Text>(true).Where(x=>x.gameObject.scene==SceneManager.GetActiveScene()).ToArray();
        string chars=string.Concat(texts.Select(x=>x.text))+string.Concat(run.details.commentWords??new string[0])+File.ReadAllText("AgentScripts/data/fullfilm-v5-story.json")+"创作仍将继续借物表音乐制作工具模型字体画笔历史影像来源";
        // The strip separator is rasterized by the strip authoring tool and is
        // never displayed by TMP; do not request that decorative glyph here.
        string missing;asset.TryAddCharacters(new string(chars.Where(c=>!char.IsSurrogate(c)&&c!='✦').Distinct().ToArray()),out missing);
        foreach(var text in texts){text.GetType().GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(text,null);text.font=asset;text.fontSharedMaterial=asset.material;EditorUtility.SetDirty(text);}
        EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();return Report("typography",new{success=true,face=asset.faceInfo.familyName,texts=texts.Length,missing});
    }
    static Mesh RibbonMesh()
    {
        string path=Root+"/Meshes/Word ribbon 2048 segments.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
        const int n=2048;var v=new Vector3[(n+1)*2];var uv=new Vector2[v.Length];var tris=new int[n*6];
        for(int i=0;i<=n;i++){float u=i/(float)n;v[i*2]=new Vector3(u-.5f,-.5f,0);v[i*2+1]=new Vector3(u-.5f,.5f,0);uv[i*2]=new Vector2(u,0);uv[i*2+1]=new Vector2(u,1);if(i<n){int a=i*2,t=i*6;tris[t]=a;tris[t+1]=a+1;tris[t+2]=a+2;tris[t+3]=a+2;tris[t+4]=a+1;tris[t+5]=a+3;}}
        mesh=new Mesh{name="V5 independent word nodes"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=tris;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*28);AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    public static object Story()
    {
        var run=Run();var d=run.details;var story=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-story.json"));var score=(JArray)story["ribbons"];
        // Keep the later cumulative glyph module when regenerating story plates.
        // OWFilmV5RibbonPass.Build owns its atlas, mesh, curve and dock bindings.
        if(d.ribbonFlowAtlas==null){d.textRibbonMaterial=Mat("Story ribbon main","OldWebMap/FilmV5/Story Ribbon");d.textRibbon.GetComponent<Renderer>().sharedMaterial=d.textRibbonMaterial;d.textRibbon.GetComponent<MeshFilter>().sharedMesh=RibbonMesh();}
        d.ribbonStates=new Texture2D[score.Count];d.ribbonPixels=new float[score.Count];d.ribbonStarts=new float[score.Count];d.ribbonEnds=new float[score.Count];d.ribbonSpeeds=new float[score.Count];
        for(int i=0;i<score.Count;i++){d.ribbonStates[i]=Tex(Path.GetFileName((string)score[i]["file"]),4096,true);d.ribbonPixels[i]=(float)score[i]["pixels"];d.ribbonStarts[i]=(float)score[i]["start"];d.ribbonEnds[i]=(float)score[i]["end"];d.ribbonSpeeds[i]=(float)score[i]["speed_px_s"];}
        for(int i=0;i<d.ribbonBranches.Length;i++)
        {d.ribbonBranchMaterials[i]=Mat("Story ribbon branch "+i,"OldWebMap/FilmV5/Story Ribbon");d.ribbonBranchMaterials[i].SetFloat("_Branch",i*2.1f);d.ribbonBranches[i].GetComponent<Renderer>().sharedMaterial=d.ribbonBranchMaterials[i];d.ribbonBranches[i].GetComponent<MeshFilter>().sharedMesh=RibbonMesh();}
        var p=run.provenance;p.contentRoot=Node("Source and permission within overload",run.transform).gameObject;
        var sources=(JArray)story["provenance"];p.plates=new Texture2D[sources.Count];p.details=new Texture2D[sources.Count];p.aspects=new float[sources.Count];p.detailAspects=new float[sources.Count];
        for(int i=0;i<sources.Count;i++){p.plates[i]=Tex(Path.GetFileName((string)sources[i]["file"]));p.details[i]=Tex(Path.GetFileName((string)sources[i]["detail_file"]));p.aspects[i]=(float)sources[i]["aspect"];p.detailAspects[i]=(float)sources[i]["detail_aspect"];}
        p.heroPlates=(Texture2D[])p.plates.Clone();p.heroAspects=(float[])p.aspects.Clone();
        for(int i=0;i<sources.Count;i++)if(sources[i]["hero_file"]!=null){p.heroPlates[i]=Tex(Path.GetFileName((string)sources[i]["hero_file"]));p.heroAspects[i]=(float)sources[i]["hero_aspect"];}
        p.mainSequence=new[]{6,4,-1,7,5,-1,9,8,-1,10,9,-1,4,-1};p.companionA=new[]{1,6,2,3,0,4,10,9,8,0,12,6,2,4};p.companionB=new[]{4,1,0,5,3,7,3,10,1,12,13,2,6,0};
        p.cards=new Transform[4];p.cardRenderers=new Renderer[4];p.cardMaterials=new Material[4];p.haloMaterials=new Material[4];
        for(int i=0;i<4;i++)
        {
            var mat=Mat("Redacted source "+i,"OldWebMap/V2/Source Surface");p.cardMaterials[i]=mat;
            p.cards[i]=Plane("Held source and attached emphasis "+i,p.contentRoot.transform,mat,Vector3.zero,Vector3.one);p.cardRenderers[i]=p.cards[i].GetComponent<Renderer>();
            var halo=Mat("Redacted source attached halo "+i,"OldWebMap/V3/Attached Focus Halo");p.haloMaterials[i]=halo;
            Plane("Attached backing",p.cards[i],halo,Vector3.forward*.035f,new Vector3(1.1f,1.13f,1));
        }
        var oldRoom=d.openingRoom;if(oldRoom!=null)oldRoom.gameObject.SetActive(false);
        d.openingRoom=Node("Closed waiting room V5",run.transform);d.openingWalls=new Transform[6];
        d.openingWallHomes=new[]{new Vector3(0,2.7f,-8.8f),new Vector3(0,2.7f,9),new Vector3(-7.5f,2.7f,.1f),new Vector3(7.5f,2.7f,.1f),new Vector3(0,5.5f,.1f),new Vector3(0,-.10f,.1f)};
        Vector3[] sizes={new Vector3(15.4f,5.8f,.3f),new Vector3(15.4f,5.8f,.3f),new Vector3(.3f,5.8f,18.1f),new Vector3(.3f,5.8f,18.1f),new Vector3(15.4f,.3f,18.1f),new Vector3(15.4f,.3f,18.1f)};
        var wall=Mat("Consistent opaque waiting walls","Unlit/Color");wall.color=new Color(.145f,.185f,.225f,1);wall.renderQueue=2000;
        for(int i=0;i<6;i++)d.openingWalls[i]=Plane("Solid room wall "+i,d.openingRoom,wall,d.openingWallHomes[i],sizes[i],true);
        var trim=Mat("Subtle waiting room seams","Unlit/Color");trim.color=new Color(.25f,.34f,.39f,1);
        for(int i=0;i<4;i++)Plane("Wall ceiling joint "+i,d.openingWalls[i],trim,new Vector3(0,.47f,-.51f),new Vector3(.99f,.006f,.012f),true);
        run.finalWord.text="创作仍将继续";run.finalWord.fontSize=3.2f;run.finalWord.rectTransform.sizeDelta=new Vector2(5.2f,.85f);
        for(int i=0;i<d.wordEchoes.Length;i++){d.wordEchoes[i].text="创作仍将继续";d.wordEchoes[i].fontSize=run.finalWord.fontSize;d.wordEchoes[i].rectTransform.sizeDelta=new Vector2(5.2f,.85f);}
        var backing=run.finalWordRoot.transform.Find("Quiet continue background");if(backing!=null)backing.localScale=new Vector3(4.8f,.85f,1);
        run.Waiting();run.offlinePreview=false;foreach(var b in new UdonSharpBehaviour[]{run,d,p})Sync(b);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("story-build",new{success=true,ribbons=score.Count,redactedPlates=sources.Count,heldCues=p.mainSequence.Count(x=>x>=0),closedFaces=d.openingWalls.Length,phrase=run.finalWord.text});
    }
    static string Relative(Transform node,Transform root){return node==root?"":Relative(node.parent,root)+"/"+node.GetSiblingIndex()+":"+node.name;}
    static void RestoreActorBasis(OWFilmV5Presentation run)
    {
        var target=SceneManager.GetActiveScene();if(SceneManager.GetSceneByPath(Source).isLoaded)throw new Exception("Source V4 is already open; preserve that scene's state");
        var source=EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
        try
        {
            var baseline=Object.FindObjectsOfType<OWFilmV4Presentation>(true).Single(r=>r.gameObject.scene==source);
            for(int i=0;i<run.frontActors.Length&&i<baseline.frontActors.Length;i++)
            {
                var from=baseline.frontActors[i].GetComponent<OWActorRig>();var to=run.frontActors[i].GetComponent<OWActorRig>();if(from==null||to==null||from.animator==null||to.animator==null)continue;
                var root=from.animator.transform;var map=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>Relative(t,root),t=>t);
                foreach(var t in to.animator.GetComponentsInChildren<Transform>(true))if(map.TryGetValue(Relative(t,to.animator.transform),out var pose))
                {t.localPosition=pose.localPosition;t.localRotation=pose.localRotation;t.localScale=pose.localScale;}
            }
        }
        finally{EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(target);}
    }
    public static object Save()
    {
        var run=Run();run.Waiting();RestoreActorBasis(run);run.Waiting();run.offlinePreview=false;
        foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.gameObject.scene==SceneManager.GetActiveScene()))Sync(b);
        foreach(var t in Object.FindObjectsOfType<Transform>(true).Where(t=>t.gameObject.scene==SceneManager.GetActiveScene()&&PrefabUtility.IsPartOfPrefabInstance(t)))
        {PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return Report("saved",new{success=true,scene=Scene,sha256=Hash(Scene),v4Sha256=Hash(Source),playing=EditorApplication.isPlaying});
    }
    public static object Initial(){Programs();Story();Typography();return Save();}

    public static object RebuildStory(){Story();Typography();return Save();}

}
