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

public static class OWFilmV5FinishingBuild
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
    static string creditsBlocker;
    static bool ClearCreditsView(Vector3 centre,Quaternion rotation,float floor,Renderer[] blockers,Bounds[] canvasBounds)
    {
        var intersect=typeof(HandleUtility).GetMethod("IntersectRayMesh",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        if(intersect==null)throw new Exception("Editor mesh ray intersection API unavailable");
        foreach(float viewingDistance in new[]{2f,2.5f})foreach(float eye in new[]{.65f,1.15f,1.35f})
        {
            Vector3 viewer=centre-rotation*Vector3.forward*viewingDistance;viewer.y=floor+eye;
            for(int column=0;column<=34;column++)foreach(float y in new[]{-.60f,-.30f,0,.30f,.60f})
            {
                float x=Mathf.Lerp(-1.13f,1.13f,column/34f);
                Vector3 target=centre+rotation*new Vector3(x,y,-.04f),delta=target-viewer;
                var ray=new Ray(viewer,delta.normalized);
                foreach(var bounds in canvasBounds)if(bounds.IntersectRay(ray,out float distance)&&distance<delta.magnitude-.001f){creditsBlocker="Canvas at "+bounds.center;return false;}
                foreach(var blocker in blockers)if(blocker.bounds.IntersectRay(ray,out float distance)&&distance<delta.magnitude-.001f)
                {
                    var filter=blocker.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                    object[] args={ray,filter.sharedMesh,filter.transform.localToWorldMatrix,new RaycastHit()};
                    if((bool)intersect.Invoke(null,args)&&((RaycastHit)args[3]).distance<delta.magnitude-.001f){creditsBlocker=blocker.name;return false;}
                }
            }
        }
        return true;
    }
    public static object TimelineAndEac()
    {
        var run=Run();var m=run.media;var score=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-related-focus.json"));
        var textures=m.sources.ToList();var aspects=m.aspects.ToList();var labels=m.labels.ToList();
        int[] current=new int[3];string[] files={"eac-current-splash.png","eac-launch-error-20260924.png","eac-current-bear.png"};
        for(int i=0;i<files.Length;i++)
        {
            var tex=Tex(files[i],i==2?32:2048);int at=textures.FindIndex(t=>AssetDatabase.GetAssetPath(t)==AssetDatabase.GetAssetPath(tex));
            if(at<0){at=textures.Count;textures.Add(tex);aspects.Add(tex.width/(float)tex.height);labels.Add(i==1?"EAC / 2026-09-24":i==0?"VRChat / 2026":"Easy Anti-Cheat");}
            current[i]=at;
            if(i==2){var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
        }
        m.sources=textures.ToArray();m.aspects=aspects.ToArray();m.labels=labels.ToArray();
        var groups=(JArray)score["groups"];m.groupStarts=groups.Select(g=>(float)g["start"]).ToArray();m.groupEnds=groups.Select(g=>(float)g["end"]).ToArray();m.poolOffsets=new int[groups.Count];m.poolCounts=new int[groups.Count];var pool=new List<int>();
        for(int i=0;i<groups.Count;i++){m.poolOffsets[i]=pool.Count;var indices=(string)groups[i]["id"]=="conditions"?new[]{83,current[0],current[1]}:groups[i]["indices"].Select(x=>(int)x).ToArray();m.poolCounts[i]=indices.Length;pool.AddRange(indices);}m.poolIndices=pool.ToArray();
        var cues=((JArray)score["chronology_override_cues"]).ToList();
        cues.Add(JObject.FromObject(new{start=110.08f,end=112.64f,index=current[0],label="VRChat / 2026"}));
        cues.Add(JObject.FromObject(new{start=112.64f,end=120.32f,index=current[1],label="EAC / 2026-09-24"}));
        m.chronologyStarts=cues.Select(c=>(float)c["start"]).ToArray();m.chronologyEnds=cues.Select(c=>(float)c["end"]).ToArray();m.chronologyIndices=cues.Select(c=>(int)c["index"]).ToArray();m.chronologyLabels=cues.Select(c=>(string)c["label"]).ToArray();
        m.chronologyRoot=Node("Dated documents in their historical place",run.transform).gameObject;
        m.chronologyMaterial=Mat("Dated source main","OldWebMap/V2/Source Surface");m.chronologyImage=Plane("Original dated document",m.chronologyRoot.transform,m.chronologyMaterial,Vector3.zero,Vector3.one).GetComponent<Renderer>();
        var halo=Mat("Dated source attached emphasis","OldWebMap/V3/Attached Focus Halo");halo.SetFloat("_Intensity",.22f);Plane("Attached dated source backing",m.chronologyImage.transform,halo,Vector3.forward*.035f,new Vector3(1.08f,1.1f,1));
        m.chronologyLabel=Text("Document date",m.chronologyRoot.transform,"",Vector3.zero,new Vector2(5,.25f),1.05f,run.basis.keyText.font);m.chronologyLabelTransform=m.chronologyLabel.transform;
        m.currentEacBadge=Node("Current client EAC mark, a separate original resource",m.chronologyRoot.transform).gameObject;
        var icon=Mat("Current EAC icon","Unlit/Transparent");icon.mainTexture=m.sources[current[2]];
        Plane("Current setup icon",m.currentEacBadge.transform,icon,Vector3.zero,new Vector3(.7f,.7f,1));
        Text("EAC resource label",m.currentEacBadge.transform,"Easy Anti-Cheat",new Vector3(0,-.47f,0),new Vector2(2,.3f),1.15f,run.basis.keyText.font);
        m.currentEacBadge.transform.localPosition=new Vector3(3.0f,.25f,-.025f);
        for(int i=0;i<m.historyStarts.Length;i++)if(m.historyStarts[i]>=102.4f)m.historyEnds[i]=m.historyStarts[i];
        for(int i=0;i<m.filmStarts.Length;i++)if(m.filmStarts[i]>=102.4f)m.filmEnds[i]=m.filmStarts[i];
        var old=run.basis.legacy.montage;var legacyTextures=old.imageTextures.ToList();
        var sourceFiles=m.sources.Take(128).Select((t,i)=>new{file=Path.GetFileName(AssetDatabase.GetAssetPath(t)),i}).ToDictionary(x=>x.file,x=>x.i);
        Func<int,int> sourceOf=tex=>tex<0||tex>=legacyTextures.Count?-1:sourceFiles.TryGetValue(Path.GetFileName(AssetDatabase.GetAssetPath(legacyTextures[tex])),out var at)?at:-1;
        Func<int,int> legacyIndex=source=>{var tex=m.sources[source];int at=legacyTextures.IndexOf(tex);if(at<0){at=legacyTextures.Count;legacyTextures.Add(tex);}return at;};
        var changes=new List<object>();
        for(int i=0;i<old.images.Length;i++)
        {
            int source=sourceOf(old.images[i]);bool early=source==58&&old.starts[i]<78.08f;
            bool late=old.starts[i]>=102.4f&&new[]{35,56,57,66,73,9}.Contains(source);
            if(early||late){int replacement=early?51:83;old.images[i]=legacyIndex(replacement);old.resultImages[i]=-1;old.years[i]=early?"2005":"2022";changes.Add(new{kind="event",i,source,replacement,start=old.starts[i]});}
        }
        for(int i=0;i<old.supportImages.Length;i++)
        {
            int source=sourceOf(old.supportImages[i]);int row=i/4;if(row>=old.supportStarts.Length)break;
            if(source==58&&old.supportStarts[row]<78.08f||old.supportStarts[row]>=102.4f&&new[]{35,56,57,66,73,9}.Contains(source))
            {int replacement=source==58?51:83;old.supportImages[i]=legacyIndex(replacement);changes.Add(new{kind="support",i,source,replacement,start=old.supportStarts[row]});}
        }
        old.imageTextures=legacyTextures.ToArray();
        for(int region=0;region<old.ambientRegions.Length;region++)foreach(var renderer in old.ambientRegions[region].GetComponentsInChildren<Renderer>(true))
        {
            var mat=renderer.sharedMaterial;if(mat==null||mat.mainTexture==null)continue;
            if(!sourceFiles.TryGetValue(Path.GetFileName(AssetDatabase.GetAssetPath(mat.mainTexture)),out int source))continue;
            bool bad=region==0&&source==58||region==3&&new[]{35,56,57,66,73,9}.Contains(source);
            if(bad){int replacement=region==0?51:83;mat.mainTexture=m.sources[replacement];EditorUtility.SetDirty(mat);changes.Add(new{kind="ambient",name=renderer.name,source,replacement,region});}
        }
        run.Waiting();run.offlinePreview=false;foreach(var b in new UdonSharpBehaviour[]{run,m,old})Sync(b);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("timeline-eac-build",new{success=true,chronologyCues=cues.Count,conditionsSources=new[]{83,current[0],current[1]},legacyCorrections=changes,unrelatedLateVrcFootageRemoved=true});
    }
    public static object Credits()
    {
        var run=Run();var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-eac-credits.json"));var pages=(JArray)data["credits"]["pages"];
        run.Finished();
        var superseded=run.finalWorld.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CommunityPanel");superseded.gameObject.SetActive(false);
        Physics.SyncTransforms();var root=Node("Borrowed assets and sources",run.finalWorld);
        Vector3 chosen=new Vector3(run.finalWorld.position.x,1.2f,run.finalWorld.position.z);bool supported=true;
        root.position=chosen;root.rotation=Quaternion.identity;root.localScale=Vector3.one/run.finalWorld.lossyScale.x;
        var frame=Mat("Credits frame","Unlit/Color");frame.color=new Color(.045f,.075f,.105f,1);
        var trim=Mat("Credits pale edge","Unlit/Color");trim.color=new Color(.26f,.40f,.47f,1);
        var blockers=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==SceneManager.GetActiveScene()&&r.enabled&&r.gameObject.activeInHierarchy&&!r.transform.IsChildOf(root)&&r.sharedMaterials.Any(m=>m!=null&&m.renderQueue<2450)).ToArray();
        var canvasBounds=Object.FindObjectsOfType<UnityEngine.UI.Graphic>(true).Where(g=>g.gameObject.scene==SceneManager.GetActiveScene()&&g.IsActive()&&g.color.a>.01f&&g.canvas!=null&&g.canvas.enabled&&g.GetComponent<CanvasRenderer>()!=null&&g.canvasRenderer.GetInheritedAlpha()>.01f&&!g.transform.IsChildOf(root)).Select(g=>{
            var corners=new Vector3[4];g.rectTransform.GetWorldCorners(corners);var b=new Bounds(corners[0],Vector3.zero);foreach(var corner in corners)b.Encapsulate(corner);b.Expand(.025f);return b;}).ToArray();
        var placements=new List<object>();
        var rejections=new List<object>();
        for(int i=0;i<pages.Count;i++)
        {
            var panel=Node("Borrowed assets page "+(i+1),root);bool found=false;
            float sign=i==0?-1:1;
            foreach(float angle in new[]{125f,110f,140f,95f,80f,65f,50f,45f,35f,155f,170f,20f})
            {
                foreach(float radius in new[]{2.8f,3.0f,2.6f,3.4f,2.2f,3.8f,4.6f,5.0f,5.4f})
                {
                    foreach(float offset in new[]{0f,15f,-15f,30f,-30f,45f,-45f,60f,-60f,90f,-90f})
                    {
                    var rotation=Quaternion.Euler(0,sign*angle+offset,0);Vector3 centre=chosen+Quaternion.Euler(0,sign*angle,0)*Vector3.forward*radius;bool ok=true;float[] floorHeights=new float[2];
                    for(int side=0;side<2;side++)
                    {
                        Vector3 point=centre+rotation*new Vector3(side==0?-.76f:.76f,0,.06f);
                        var hits=Physics.RaycastAll(new Vector3(point.x,2,point.z),Vector3.down,5).Where(h=>run.details.worldFloorColliders.Contains(h.collider)).OrderBy(h=>h.distance).ToArray();
                        if(hits.Length==0||hits[0].collider.name.StartsWith("Seat"))ok=false;else floorHeights[side]=hits[0].point.y;
                    }
                    if(Mathf.Abs(floorHeights[0]-floorHeights[1])>.02f)ok=false;
                    if(!ok)rejections.Add(new{page=i+1,angle=sign*angle,radius,reason="floor",floorHeights});
                    if(ok)
                    {
                        float floor=(floorHeights[0]+floorHeights[1])*.5f;centre.y=floor+1.15f;
                        if(!ClearCreditsView(centre,rotation,floor,blockers,canvasBounds)){rejections.Add(new{page=i+1,angle=sign*angle,radius,reason=creditsBlocker});continue;}
                        panel.SetPositionAndRotation(centre,rotation);placements.Add(new{page=i+1,angle=sign*angle,facing=sign*angle+offset,radius,floor,position=centre.ToString(),clearReadViews=new[]{.65f,1.15f,1.35f},viewingDistances=new[]{2f,2.5f}});found=true;break;
                    }
                    }
                    if(found)break;
                }
                if(found)break;
            }
            if(!found){Report("credits-placement-rejections",new{success=false,rejections});throw new Exception("No supported arc position for credits page "+i+"; see credits-placement-rejections.json");}
            var mat=Mat("Credits page "+i,"Unlit/Texture");mat.mainTexture=Tex(Path.GetFileName((string)pages[i]["output_png"]),2048);
            Plane("Readable credits",panel,mat,new Vector3(0,0,-.035f),new Vector3(2.4f,1.35f,1));
            Plane("Frame",panel,frame,Vector3.zero,new Vector3(2.46f,1.41f,.045f),true);
            Plane("Lower warm edge",panel,trim,new Vector3(0,-.705f,-.038f),new Vector3(2.44f,.012f,.012f),true);
            for(int side=0;side<2;side++)Plane("Stand "+side,panel,trim,new Vector3(side==0?-.76f:.76f,-.93f,.06f),new Vector3(.025f,.44f,.035f),true);
        }
        run.details.creditsRoot=root.gameObject;run.Waiting();run.offlinePreview=false;Sync(run.details);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("credits-build",new{success=true,pages=pages.Count,separateObjects=true,position=chosen.ToString(),standingOnAuthoredFloor=supported,placements,readViewMeshAndCanvasChecks=true,supersededLegacySourceBoard=superseded.name,visible="after performance",includesQvPen=true});
    }
    public static object WaitingWalls()
    {
        var run=Run();var d=run.details;var wall=Mat("Consistent opaque waiting walls","OldWebMap/FilmV5/Waiting Room Walls");wall.color=new Color(.22f,.27f,.32f,1);
        foreach(var t in d.openingWalls)t.GetComponent<Renderer>().sharedMaterial=wall;
        d.openingWallHomes[5]=new Vector3(0,-.175f,.1f);d.openingWalls[5].localPosition=d.openingWallHomes[5];
        var light=Mat("Ceiling line luminance","Unlit/Color");light.color=new Color(.30f,.43f,.49f,1);
        for(int i=0;i<2;i++)Plane("Recessed ceiling light "+i,d.openingWalls[4],light,new Vector3(i==0?-.23f:.23f,-.54f,0),new Vector3(.21f,.12f,.006f),true);
        run.Waiting();run.offlinePreview=false;EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("waiting-walls",new{success=true,faces=6,samePaint=true,normalAndHeightShading=true,opaque=true});
    }

    public static object Canopy()
    {
        var run=Run();run.Finished();
        string path=Root+"/ModelAssets/Meshes/fullfilm_v5_branch_canopy.fbx";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new Exception("Prepared V5 canopy FBX missing");
        var old=run.finalWorld.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BranchCanopy");
        var existing=run.finalWorld.Find("Refined canopy");if(existing!=null)throw new Exception("Canopy is already integrated; inspect rather than duplicate it");
        var disabled=new List<string>();foreach(var r in old.GetComponentsInChildren<Renderer>(true)){r.enabled=false;disabled.Add(r.name);}
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,run.finalWorld);instance.name="Refined canopy";instance.transform.localPosition=run.finalWorld.InverseTransformPoint(old.position);
        instance.transform.localRotation=prefab.transform.localRotation;instance.transform.localScale=prefab.transform.localScale;
        var map=new Dictionary<Material,Material>();var shader=Shader.Find("lilToon");if(shader==null)shader=Shader.Find("Standard");
        var renderers=instance.GetComponentsInChildren<Renderer>(true);var originalColors=new List<Color>();var newMaterials=new List<Material>();
        foreach(var source in renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
        {
            var material=Mat("Canopy "+source.name,shader.name);material.color=source.color;
            if(material.HasProperty("_AsUnlit"))material.SetFloat("_AsUnlit",.72f);
            if(material.HasProperty("_LightMinLimit"))material.SetFloat("_LightMinLimit",.38f);
            if(material.HasProperty("_UseShadow"))material.SetFloat("_UseShadow",1);
            if(source.name.Contains("Timber")||source.name.Contains("EndGrain"))material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ModelAssets/Textures/archive-shelf-wood017-albedo.jpg");
            if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.12f);
            map[source]=material;newMaterials.Add(material);originalColors.Add(material.color);
        }
        foreach(var renderer in renderers){renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>map[m]).ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;}
        run.worldColorMaterials=run.worldColorMaterials.Concat(newMaterials).ToArray();run.worldOriginalColors=run.worldOriginalColors.Concat(originalColors).ToArray();
        Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        var result=new{success=true,source=path,oldPivot=old.position.ToString(),oldLocal=old.localPosition.ToString(),rootScale=instance.transform.localScale.ToString(),rootRotation=instance.transform.localRotation.eulerAngles.ToString(),worldBoundsCentre=bounds.center.ToString(),worldBoundsSize=bounds.size.ToString(),meshes=renderers.Length,materials=newMaterials.Count,shader=shader.name,disabledOld=disabled,collidersUntouched=true};
        run.Waiting();run.offlinePreview=false;Sync(run);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("canopy-integration",result);
    }

    public static object Curation()
    {
        var run=Run();var d=run.details;d.excludedArt=new[]{d.homepagePlane.gameObject,d.gamePlane.gameObject,d.gameDetailPlane.gameObject};
        d.commentWords=new[]{"嵌字好了","这一句再润色一下","辛苦了","校对中","还有种子","已补种","继续做种","谢谢分享","沙发","顶一下","楼主更新了","签名档更新了","时间轴+1","这句听清了","已校对","字幕已补","你好","去哪玩","来这边","同一个房间","需要登录","正在验证","打不开","连接中","Private","仅限自用","请先询问","保留署名","链接失效了","缓存还在","连接已断开","请勿转卖","能跑了","这里也能改","一起试试","再来一次"};
        run.Waiting();run.offlinePreview=false;Sync(d);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("curation",new{success=true,excluded="Contemporary mock homepage and final game localization pair",mangaRetained=true,flashNewSpan=new[]{202.24f,207.36f},gameTranslation="excluded following 2026-10-18 review",catStickers="six selected cards in the modern ending only",stillLifeAndConcept="modern creative context",comments="Era and subject specific groups; removed ambiguous repost prompts"});
    }

    public static object Emotes()
    {
        var run=Run();var m=run.media;var json=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-emotes.json"));var items=(JArray)json["items"];
        var textures=new List<Texture2D>();var starts=new List<float>();var ends=new List<float>();var aspects=new List<float>();
        foreach(var item in items)
        {
            string file=Path.GetFileName((string)item["file"]);var texture=Tex(file,512);var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            importer.textureCompression=TextureImporterCompression.Uncompressed;bool pixel=(string)item["timeline_group"]=="forum-phpbb-3.0.8"||(string)item["timeline_group"]=="tieba-middle-pack-listing-2014";
            importer.filterMode=pixel?FilterMode.Point:FilterMode.Bilinear;importer.mipmapEnabled=!pixel;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            foreach(var span in item["time_windows_seconds"]){textures.Add(texture);starts.Add((float)span[0]);ends.Add((float)span[1]);aspects.Add(texture.width/(float)texture.height);}
        }
        m.emotes=textures.ToArray();m.emoteStarts=starts.ToArray();m.emoteEnds=ends.ToArray();m.emoteAspects=aspects.ToArray();run.Waiting();run.offlinePreview=false;Sync(m);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("emotes-build",new{success=true,uniqueTextures=textures.Distinct().Count(),timedUses=textures.Count,groups=4,currentReusedInEnding=true,chronology="2010 forum / 2014 Tieba archive / 2020 mobile / 2026 current"});
    }

    public static object FinishMedia(){TimelineAndEac();WaitingWalls();Curation();Emotes();return Credits();}

}
