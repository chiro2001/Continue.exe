using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWEpisodeV4Build
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v4.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision4";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v4");
    static T F<T>() where T:UnityEngine.Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    static VRC.Udon.UdonBehaviour Vm(UdonSharpBehaviour b)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
    static float N(VRC.Udon.UdonBehaviour b,string name)=>Convert.ToSingle(b.GetProgramVariable(name));
    static void Sync(UdonSharpBehaviour b){UdonSharpEditorUtility.CopyProxyToUdon(b);var vm=Vm(b);if(vm!=null)EditorUtility.SetDirty(vm);}
    public static object PrepareImageImport(){Folder(Root+"/Images");return "V4 image directory prepared by Pipeline";}
    public static object ImportImages()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);int count=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Images"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=path.Contains("workflow-")?FilterMode.Point:FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.isReadable=false;importer.SaveAndReimport();count++;
        }
        return Report("image-import.json",new{textures=count,originalV3ImagesChanged=false});
    }
    public static object RepairPrograms()
    {
        Folder(Root+"/Programs");
        foreach(string name in new[]{"OWV4Journey","OWV4Presentation","OWV4HubCast","OWV4Nameplates","OWV4Montage","OWV4Oscilloscope","OWV4CameraFx","OWV4Operations","OWV4Foley"})
        {
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/OldWebMap/Scripts/"+name+".cs");if(script==null)throw new InvalidOperationException(name+" source missing");
            bool present=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Any(a=>a!=null&&a.sourceCsScript==script);
            if(!present){var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;AssetDatabase.CreateAsset(program,Root+"/Programs/"+name+".asset");}
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new InvalidOperationException("V4 Udon compilation failed");AssetDatabase.SaveAssets();return "V4 Udon programs compiled";
    }
    public static object ImportAnonymizedImages()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped owned V4 scene required");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var score=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-v4-score.json"));var images=(JArray)score["images"];var montage=F<OWV4Montage>();int count=0;
        for(int index=0;index<images.Count;index++)
        {
            string path=(string)images[index]["display_path"];if(string.IsNullOrEmpty(path))continue;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new InvalidOperationException("Anonymous display copy missing: "+path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.isReadable=false;importer.SaveAndReimport();
            var replacement=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var original=AssetDatabase.LoadAssetAtPath<Texture2D>((string)images[index]["path"]);montage.imageTextures[index]=replacement;
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(material!=null && material.HasProperty("_MainTex") && material.mainTexture==original){material.mainTexture=replacement;EditorUtility.SetDirty(material);}
            }
            count++;
        }
        Sync(montage);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return Report("anonymity-import.json",new{success=count==5,displayCopies=count,independentContentCount=montage.imageTextures.Length,sourceV3ImagesChanged=false});
    }
    static TMP_FontAsset MenuFont()
    {
        Folder(Root+"/Fonts");string path=Root+"/Fonts/ControlChinese2048.asset";var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(f==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
            f=TMP_FontAsset.CreateFontAsset(source,64,7,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);f.name="ControlChinese2048";f.isMultiAtlasTexturesEnabled=false;AssetDatabase.CreateAsset(f,path);
            if(!AssetDatabase.Contains(f.material))AssetDatabase.AddObjectToAsset(f.material,f);
            foreach(var t in f.atlasTextures)if(!AssetDatabase.Contains(t))AssetDatabase.AddObjectToAsset(t,f);
        }
        f.TryAddCharacters("连接尚未开始按下开始让第一扇窗口打开这一段先到这里想再看一次就从这里接通重新。。，",out string missing);
        if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Menu glyphs missing: "+missing);EditorUtility.SetDirty(f);return f;
    }
    public static object Bootstrap()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped V4 scene required");
        var owner=GameObject.Find("OWEpisode123").transform;var d=F<OWEpisodeDirector>();d.externalStageSampler=true;d.continuousPerformance=true;d.autoStart=false;d.montage=null;
        var performance=owner.Find("Revision4Performance");if(performance==null)performance=Child("Revision4Performance",owner).transform;performance.gameObject.SetActive(true);
        var world=performance.Find("Shared world frame");if(world==null)world=Child("Shared world frame",performance).transform;world.gameObject.SetActive(true);world.localPosition=Vector3.zero;
        var journey=F<OWV4Journey>();if(journey==null)journey=Child("V4 shared journey clock",owner).AddUdonSharpComponent<OWV4Journey>();journey.director=d;journey.worldFrame=world;
        var opening=owner.Find("OpeningRoom");if(opening!=null)opening.SetParent(world,true);
        journey.openingShells=d.stage.shells;journey.shellPositions=d.stage.shellPositions;journey.shellExits=d.stage.shellExits;
        foreach(var cue in d.stage.cueRoots)if(cue!=null)cue.gameObject.SetActive(false);
        foreach(var t in owner.GetComponentsInChildren<Transform>(true))if(t.name=="Sources and reconstruction"||t.name=="Preview completion"||t.name=="Audience playback desk")t.gameObject.SetActive(false);
        foreach(var m in owner.GetComponentsInChildren<OWV3Montage>(true)){Vm(m).enabled=false;m.gameObject.SetActive(false);}
        foreach(var o in owner.GetComponentsInChildren<OWV3Operations>(true)){Vm(o).enabled=false;o.gameObject.SetActive(false);}
        foreach(var o in owner.GetComponentsInChildren<OWV3Oscilloscope>(true)){Vm(o).enabled=false;o.transform.parent.gameObject.SetActive(false);}
        foreach(var o in owner.GetComponentsInChildren<OWV3HubCast>(true))Vm(o).enabled=false;
        var oldCaption=d.stage.captionText;if(oldCaption!=null)oldCaption.gameObject.SetActive(false);if(d.stage.endCard!=null)d.stage.endCard.SetActive(false);if(d.stage.chapterText!=null)d.stage.chapterText.gameObject.SetActive(false);if(d.stage.progressText!=null)d.stage.progressText.gameObject.SetActive(false);
        var menu=owner.GetComponentsInChildren<Canvas>(true).First(x=>x.name=="Ray UI - start and replay");menu.transform.SetParent(owner,false);menu.transform.localPosition=new Vector3(0,1.45f,-.7f);menu.transform.localRotation=Quaternion.identity;menu.transform.localScale=Vector3.one*.0036f;menu.gameObject.SetActive(true);
        var font=MenuFont();foreach(var text in menu.GetComponentsInChildren<TMP_Text>(true))text.font=font;
        var title=menu.transform.Find("Title").GetComponent<TextMeshProUGUI>();title.rectTransform.anchoredPosition=new Vector2(0,110);title.rectTransform.sizeDelta=new Vector2(510,80);title.fontSize=32;
        var body=menu.transform.Find("Invitation");if(body==null){body=new GameObject("Invitation",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI)).transform;body.SetParent(menu.transform,false);}
        body.gameObject.layer=17;var bodyText=body.GetComponent<TextMeshProUGUI>();bodyText.font=font;bodyText.fontSize=24;bodyText.alignment=TextAlignmentOptions.Center;bodyText.raycastTarget=false;bodyText.rectTransform.anchoredPosition=new Vector2(0,36);bodyText.rectTransform.sizeDelta=new Vector2(510,55);
        var button=menu.GetComponentsInChildren<Button>(true).First();button.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-70);button.interactable=true;var handler=button.GetComponent<OWEpisodeButton>();handler.director=d;handler.action=1;Sync(handler);
        var label=button.GetComponentInChildren<TMP_Text>(true);label.font=font;label.fontSize=31;
        var p=F<OWV4Presentation>();if(p==null)p=Child("V4 presentation lifecycle",owner).AddUdonSharpComponent<OWV4Presentation>();p.director=d;p.performanceRoot=performance.gameObject;p.menuRoot=menu.gameObject;p.menuTitle=title;p.menuBody=bodyText;p.playLabel=label;p.fallbackCamera=Camera.main.transform;
        if(p.startupObject==null)
        {
            Folder(Root+"/Materials");string path=Root+"/Materials/WaitingTerminal.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Unlit/Color"));mat.color=new Color(.025f,.045f,.055f);AssetDatabase.CreateAsset(mat,path);}
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="One waiting terminal";go.transform.SetParent(owner,false);go.transform.localPosition=new Vector3(0,1.45f,-.48f);go.transform.localScale=new Vector3(2.12f,1.38f,.12f);Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;p.startupObject=go.transform;
        }
        d.audiencePanels=new[]{menu.gameObject};var rev=owner.Find("Revision2");if(rev!=null)rev.gameObject.SetActive(false);
        var audio=owner.Find("V4 physical sound sources");if(audio==null)audio=Child("V4 physical sound sources",owner).transform;
        foreach(var source in d.music.sources)source.transform.SetParent(audio,true);
        var oldFoley=F<OWEpisodeFoley>();if(oldFoley!=null)
        {
            var f=F<OWV4Foley>();if(f==null)f=Child("V4 local action sounds",owner).AddUdonSharpComponent<OWV4Foley>();f.director=d;f.journey=journey;f.sources=oldFoley.sources;f.emitters=oldFoley.emitters;
            var indices=Enumerable.Range(0,oldFoley.eventTimes.Length).Select(i=>new{time=oldFoley.eventTimes[i]>=40.96f&&oldFoley.eventTimes[i]<51.2f?oldFoley.eventTimes[i]+20.48f:oldFoley.eventTimes[i]>=51.2f&&oldFoley.eventTimes[i]<61.44f?oldFoley.eventTimes[i]-10.24f:oldFoley.eventTimes[i],clip=oldFoley.eventClips[i],emitter=oldFoley.eventEmitters[i]}).OrderBy(x=>x.time).ToArray();f.eventTimes=indices.Select(x=>x.time).ToArray();f.eventClips=indices.Select(x=>x.clip).ToArray();f.eventEmitters=indices.Select(x=>x.emitter).ToArray();foreach(var source in f.sources)source.transform.SetParent(audio,true);Vm(oldFoley).enabled=false;Sync(f);
        }
        journey.Sample(0);Sync(journey);Sync(d);Sync(p);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return "V4 frame, lifecycle, physical start menu and sound systems prepared";
    }
    public static void Pose(float time,bool seek=false)
    {
        var p=F<OWV4Presentation>();p.Preview(time);var d=F<OWEpisodeDirector>();F<OWV4Journey>().Sample(time);d.music.SampleVisual(time);
        var cast=F<OWV4HubCast>();cast.Sample(time);
        string[] names={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
        foreach(var a in cast.actors.Concat(cast.alternateActors).Where(a=>a!=null&&a.animator!=null&&a.animator.gameObject.activeInHierarchy))
        {int at=a.animator.GetInteger("V4Pose");float phase=a.animator.GetFloat("V4PoseTime");a.animator.Play(names[Mathf.Clamp(at,0,11)],0,phase);a.animator.Update(0);}
        cast.Sample(time);cast.nameplates.Sample(time);
        var m=F<OWV4Montage>();if(seek)m.Seek(time);else m.Sample(time);var osc=F<OWV4Oscilloscope>();if(seek)osc.Seek(time);else osc.Sample(time);F<OWV4Operations>().Sample(time);F<OWV4CameraFx>().Sample(time);
        Canvas.ForceUpdateCanvases();foreach(var text in Object.FindObjectsOfType<TMP_Text>())if(text.havePropertiesChanged)text.ForceMeshUpdate(false,false);
    }
    public static object FinalizeAndSave()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped owned V4 scene required");
        var p=F<OWV4Presentation>();var cast=F<OWV4HubCast>();p.nameplateRoot=cast.nameplates.gameObject;p.overlayRoot=F<OWV4CameraFx>().gameObject;p.fallbackCamera=Camera.main.transform;cast.nameplates.headGap=.39f;
        cast.nameplates.anonymousLabel="xxxx";cast.nameplates.actorNicknames=Enumerable.Repeat("xxxx",cast.nameplates.actorPlates.Length).ToArray();
        var plates=cast.nameplates.actorPlates.Concat(cast.nameplates.playerPlates).ToArray();var labels=cast.nameplates.actorLabels.Concat(cast.nameplates.playerLabels).ToArray();
        // All historical display plates are anonymous; preserve glyph proportions.
        for(int i=0;i<plates.Length;i++)
        {
            var plate=plates[i];var label=labels[i];label.text="xxxx";label.enableAutoSizing=false;label.fontSize=1f;
            float width=Mathf.Clamp(label.GetPreferredValues("xxxx").x+.22f,.55f/.88f,.75f/.88f);
            var edge=plate.Find("Classic stepped nameplate outline");var fill=plate.Find("Dark nameplate fill");
            edge.localScale=new Vector3(width/1.32f,1,1);fill.localScale=new Vector3(.966f*width/1.32f,1,.80f);
            label.rectTransform.sizeDelta=new Vector2(width-.14f,label.rectTransform.sizeDelta.y);
        }
        p.journey=F<OWV4Journey>();p.cast=cast;p.montage=F<OWV4Montage>();p.oscilloscope=F<OWV4Oscilloscope>();p.operations=F<OWV4Operations>();p.cameraFx=F<OWV4CameraFx>();
        p.journey.autoSample=false;cast.autoSample=false;cast.nameplates.autoSample=false;p.montage.autoSample=false;p.oscilloscope.autoSample=false;p.operations.autoSample=false;p.cameraFx.autoSample=false;
        foreach(var group in p.operations.groupRoots)group.localScale=Vector3.one*1.28f;
        var hubFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/HubChinese.asset");
        if(hubFont!=null)
        {
            string copy="我的文档网上邻居网络连接兼容性检查安全设置辅助选项保存副本无法同时运行通过正在验证当前功能不可用已恢复连接此程序无法请选择其他请稍候更新返回后重新未正在保存…";
            string needed=new string(copy.Where(ch=>!char.IsWhiteSpace(ch)&&!hubFont.HasCharacter(ch)).Distinct().ToArray());
            if(needed.Length>0){hubFont.TryAddCharacters(needed,out string missing);if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("V4 public-copy font misses "+missing);}
            foreach(var atlas in hubFont.atlasTextures)if(atlas!=null&&!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,hubFont);EditorUtility.SetDirty(hubFont);
        }
        var owner=GameObject.Find("OWEpisode123").transform;
        foreach(var text in owner.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.name=="Working place title")text.text=text.transform.parent.localPosition.x<0?"我的文档":"网上邻居";
            if(text.name=="Gate meaning")
            {string[] words={"网络连接","兼容性检查","安全设置","辅助选项","保存副本"};int index=Array.IndexOf(cast.conditionFrames,text.transform.parent);if(index>=0)text.text=words[Mathf.Min(index,4)];}
            if(text.name=="Needs held together")text.gameObject.SetActive(false);
            if(text.name=="Useful tool remains running")text.text="FILE\nSAVE";
        }
        foreach(string name in new[]{"Revision3AudioFx","Revision3Montage","Revision3Operations"}){var old=owner.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}
        var m=F<OWV4Montage>();
        for(int region=0;region<4;region++)
        {
            // Keep the same angular reading size, ahead of the campfire logs.
            m.keyRoots[region].localPosition=F<OWV4Journey>().RegionOffset(region)+new Vector3(0,.556f,1.6f);
            m.keyPanels[region].transform.localScale=new Vector3(3.534f,.346f,1);m.keyTexts[region].fontSize=1.73f;m.keyTexts[region].rectTransform.sizeDelta=new Vector2(3.39f,.36f);
            m.typeRegions[region].localPosition=F<OWV4Journey>().RegionOffset(region)+new Vector3(0,5.0f,6.6f);
            m.typeWords[region*3].localPosition=Vector3.zero;m.typeWords[region*3].localScale=Vector3.one*.85f;
            m.typeWords[region*3+1].localPosition=new Vector3(3.4f,-.9f,.1f);m.typeWords[region*3+1].localScale=Vector3.one*.9f;
            m.typeWords[region*3+2].localPosition=new Vector3(-3.8f,-.9f,.1f);m.typeWords[region*3+2].localScale=Vector3.one*.9f;
            // Ambient and support layouts are persisted by ApplyPresentationLayout().
        }
        F<OWEpisodeDirector>().stage.actors=new OWActorRig[0];p.Waiting();F<OWV4Journey>().Sample(0);cast.nameplates.Sample(0);
        foreach(var b in owner.GetComponentsInChildren<UdonSharpBehaviour>(true))if(!b.GetType().Name.StartsWith("OWV3"))Sync(b);
        foreach(var t in owner.GetComponentsInChildren<Transform>(true))if(PrefabUtility.IsPartOfPrefabInstance(t)){PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root})){string path=AssetDatabase.GUIDToAssetPath(guid);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null&&mat.name!=Path.GetFileNameWithoutExtension(path)){mat.name=Path.GetFileNameWithoutExtension(path);EditorUtility.SetDirty(mat);}}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return Report("integration.json",new{scene=ScenePath,events=m.starts.Length,images=m.imageTextures.Length,focus=m.roles.Count(x=>x==1),comments=m.commentTimes.Length,ambient=m.ambientWindows.Length,actors=cast.actors.Length,nameplateSlots=cast.nameplates.playerPlates.Length,waitingClean=!p.performanceRoot.activeSelf,menuVisible=p.menuRoot.activeInHierarchy});
    }
    static string Frame(Camera c,string path,bool menus=false,int width=1600,int height=900)
    {
        var p=F<OWV4Presentation>();bool shown=p.menuRoot.activeSelf;if(!menus)p.menuRoot.SetActive(false);Directory.CreateDirectory(Path.GetDirectoryName(path));var previous=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);var im=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;im.ReadPixels(new Rect(0,0,width,height),0,0);im.Apply();File.WriteAllBytes(path,path.EndsWith(".jpg")?im.EncodeToJPG(92):im.EncodeToPNG());}
        finally{c.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(im);p.menuRoot.SetActive(shown);}return path;
    }
    public static object CaptureAll()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped reference capture");var p=F<OWV4Presentation>();var rows=new List<object>();
        try
        {
            p.Waiting();rows.Add(new{time=-1f,path=Frame(Camera.main,Tmp+"/frames/v4_waiting.png",true)});
            foreach(float t in new[]{.04f,.96f,3.2f,5.6f,11.6f,13.6f,17.2f,19.2f,23.52f,28.16f,34.56f,40.96f,44.16f,47.9f,50.8f,55.36f,56.2f,60.6f,64.48f,66.4f,69.12f,74.88f,80f,82.88f,85.12f,86.9f,88.64f,89.6f,91.04f,96.64f,103.2f,105.6f,109.44f,114f,118.8f,121.6f,125.44f,129.28f,131.84f})
            {Pose(t,true);rows.Add(new{time=t,path=Frame(Camera.main,Tmp+"/frames/v4_"+t.ToString("000.00",System.Globalization.CultureInfo.InvariantCulture)+".png")});}
            p.Finished();rows.Add(new{time=134f,path=Frame(Camera.main,Tmp+"/frames/v4_finished.png",true)});
        }
        finally{p.Waiting();F<OWV4Journey>().Sample(0);}return Report("capture.json",rows);
    }
    public static object TextAndSpaceChecks()
    {
        var p=F<OWV4Presentation>();var rows=new List<object>();var forbidden=new[]{"本项目","原创演示","风格复刻","审核","Agent","示意","来源图","原图拆层"};
        try
        {
            foreach(float time in new[]{3.2f,17.2f,35.8f,47.8f,66.9f,85.12f,89.6f,105.6f,118.8f,129.28f})
            {
                Pose(time,true);var texts=Object.FindObjectsOfType<TMP_Text>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
                var bad=texts.Where(t=>forbidden.Any(s=>t.text.Contains(s))).Select(t=>new{t.name,t.text}).ToArray();if(bad.Length>0)throw new InvalidOperationException("Performed agent/review text: "+JsonConvert.SerializeObject(bad));
                var m=F<OWV4Montage>();var c=F<OWV4HubCast>();var osc=F<OWV4Oscilloscope>();
                float error=Enumerable.Range(0,4).Max(ch=>Vector3.Distance(osc.waves[ch].position,osc.music.sources[ch].transform.position));if(error>.0001f)throw new InvalidOperationException("Voice/wave frame mismatch");
                float particleError=Enumerable.Range(0,4).Max(ch=>Vector3.Distance(osc.waves[ch].position,osc.particleAnchors[ch].position));if(particleError>.0001f)throw new InvalidOperationException("Voice-spark/wave world-frame mismatch");
                rows.Add(new{time,focus=m.activeFocus,periphery=m.activePeriphery,ambient=m.activeAmbient,comments=m.activeComments,era=c.visibleEra,texts=texts.Select(t=>new{t.name,t.text}).ToArray(),voiceError=error,particleError,frameZ=F<OWV4Journey>().worldFrame.position.z});
            }
            p.Waiting();bool waiting=p.menuRoot.activeInHierarchy&&!p.performanceRoot.activeInHierarchy&&!p.nameplateRoot.activeInHierarchy&&!p.overlayRoot.activeInHierarchy;
            p.Finished();bool ended=p.menuRoot.activeInHierarchy&&!p.performanceRoot.activeInHierarchy;
            Vector3 viewport=Camera.main.WorldToViewportPoint(p.menuRoot.transform.position);bool inside=viewport.z>0&&viewport.x>0&&viewport.x<1&&viewport.y>0&&viewport.y<1;
            Vector3 menuPoint=p.menuRoot.transform.position;bool ray=Physics.Raycast(Camera.main.transform.position,(menuPoint-Camera.main.transform.position).normalized,out var hit,5,1<<17,QueryTriggerInteraction.Ignore)&&hit.collider.gameObject==p.menuRoot;
            var result=new{success=waiting&&ended&&inside&&ray,waiting,ended,menuInFront=inside,ray,rows};string json=Report("text-space.json",result);if(!result.success)throw new InvalidOperationException(json);return new{result.success,waiting,ended,ray,samples=rows.Count};
        }
        finally{p.Waiting();F<OWV4Journey>().Sample(0);}
    }
    static Color32[] Pixels(Camera camera)
    {
        var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,675,24);var image=new Texture2D(1200,675,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,675),0,0);image.Apply();return image.GetPixels32();}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static int ChangedPixels(Color32[] a,Color32[] b)
    {int count=0;for(int i=0;i<a.Length;i++)if(Mathf.Max(Mathf.Abs(a[i].r-b[i].r),Mathf.Abs(a[i].g-b[i].g),Mathf.Abs(a[i].b-b[i].b))>24)count++;return count;}
    static bool ChangedPixel(Color32 a,Color32 b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))>24;
    public static object KeylineVisibilityChecks()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped owned V4 review required");
        var p=F<OWV4Presentation>();var montage=F<OWV4Montage>();var rows=new List<object>();float minimum=1f;
        try
        {
            for(int key=0;key<montage.keyStarts.Length;key++)
            {
                // Include the exact campfire angle that exposed the occlusion.
                float time=key==9?89.60f:(montage.keyStarts[key]+montage.keyEnds[key])*.5f;Pose(time,true);
                var root=montage.keyRoots[montage.keyRegions[key]];var text=montage.keyTexts[montage.keyRegions[key]].GetComponent<Renderer>();
                var renderers=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy).ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();
                int actual=0,potential=0;
                try
                {
                    var withText=Pixels(Camera.main);text.enabled=false;var withoutText=Pixels(Camera.main);text.enabled=true;
                    foreach(var r in renderers)if(!r.transform.IsChildOf(root)&&!r.transform.IsChildOf(p.overlayRoot.transform))r.enabled=false;
                    var isolated=Pixels(Camera.main);text.enabled=false;var plateOnly=Pixels(Camera.main);
                    for(int i=0;i<isolated.Length;i++)if(ChangedPixel(isolated[i],plateOnly[i])){potential++;if(ChangedPixel(withText[i],withoutText[i]))actual++;}
                }
                finally{for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];}
                float ratio=potential==0?0:actual/(float)potential;minimum=Mathf.Min(minimum,ratio);rows.Add(new{key,time,text=montage.keyStrings[key],actual,potential,visibleRatio=ratio});
                if(potential<100 || ratio<.97f)throw new InvalidOperationException("Keyline glyphs occluded in actual render: "+JsonConvert.SerializeObject(rows.Last()));
            }
            return Report("keyline-visibility.json",new{success=true,samples=rows.Count,minVisibleGlyphPixelRatio=minimum,method="Rendered glyph on/off difference with full scenery versus isolated keyline; threshold 24 RGB levels, minimum 97% visible",rows});
        }
        catch(Exception e){Report("keyline-visibility-failure.json",new{success=false,error=e.ToString(),rows});throw;}
        finally{p.Waiting();F<OWV4Journey>().Sample(0);}
    }
    public static object BeginReview(){Pose(.96f,true);return "Owned V4 presentation temporarily visible for module validation";}
    public static object ActivateForBuild()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped owned V4 scene required");
        var p=F<OWV4Presentation>();p.offlinePreview=true;p.performanceRoot.SetActive(true);if(p.nameplateRoot!=null)p.nameplateRoot.SetActive(true);F<OWV4Journey>().Sample(0);return "V4 hierarchy active for TMP/Animator asset construction";
    }
    public static object ResetOwnedReview()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped owned V4 review scene required");
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);return BeginReview();
    }
    static bool _movie;static int _frame;static Camera _camera;static RenderTexture _movieRt;static Texture2D _movieTexture;static string _movieError;
    public static object StartMovie()
    {
        if(EditorApplication.isPlaying || SessionState.GetBool("EpisodeV4Movie",false))throw new InvalidOperationException("Stopped editor, no existing export");
        Directory.CreateDirectory(Tmp+"/movie-frames");_camera=Camera.main;_movieRt=new RenderTexture(1600,900,24);_movieRt.Create();_movieTexture=new Texture2D(1600,900,TextureFormat.RGB24,false);
        foreach(var panel in Object.FindObjectOfType<OWEpisodeDirector>().audiencePanels)panel.SetActive(false);
        _frame=0;_movieError=null;_movie=true;SessionState.SetBool("EpisodeV4Movie",true);SessionState.SetInt("EpisodeV4Frame",0);SessionState.SetString("EpisodeV4MovieError","");Pose(0,true);EditorApplication.update+=MovieTick;return new{started=true,frames=3376,fps=25,path=Tmp+"/movie-frames"};
    }
    static void MovieTick()
    {
        if(!_movie)return;
        try
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int batch=0;batch<7 && _frame<3376;batch++)
            {
                float t=Mathf.Min(_frame/25f,133.12f);Pose(t);
                var previous=_camera.targetTexture;var active=RenderTexture.active;
                try{_camera.targetTexture=_movieRt;_camera.Render();RenderTexture.active=_movieRt;_movieTexture.ReadPixels(new Rect(0,0,1600,900),0,0);_movieTexture.Apply();File.WriteAllBytes(Tmp+"/movie-frames/frame_"+_frame.ToString("00000")+".jpg",_movieTexture.EncodeToJPG(92));}
                finally{_camera.targetTexture=previous;RenderTexture.active=active;}
                _frame++;SessionState.SetInt("EpisodeV4Frame",_frame);if(watch.ElapsedMilliseconds>130)break;
            }
            if(_frame%25<7)Report("movie-progress.json",new{frame=_frame,total=3376,complete=false});
            if(_frame>=3376)EndMovie();
        }
        catch(Exception e){_movieError=e.ToString();EndMovie();}
    }
    static void EndMovie()
    {
        EditorApplication.update-=MovieTick;_movie=false;SessionState.SetBool("EpisodeV4Movie",false);SessionState.SetString("EpisodeV4MovieError",_movieError??"");
        if(_movieRt!=null){_movieRt.Release();Object.DestroyImmediate(_movieRt);}
        if(_movieTexture!=null)Object.DestroyImmediate(_movieTexture);
        F<OWV4Presentation>().Waiting();F<OWV4Journey>().Sample(0);Report("movie-progress.json",new{frame=_frame,total=3376,complete=_frame==3376,error=_movieError});
    }
    public static object MovieStatus(){return new{running=SessionState.GetBool("EpisodeV4Movie",false),frame=SessionState.GetInt("EpisodeV4Frame",0),total=3376,error=SessionState.GetString("EpisodeV4MovieError","")};}
    static void AutoTick(bool enabled)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand",false)).FirstOrDefault(t=>t!=null);
        if(type==null)throw new InvalidOperationException("Pipeline auto-tick controller missing");
        type.GetMethod("SetAutoTick",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{enabled,16,false});
    }
    public static async Task<object> SdkBuildOnly()
    {
        FinalizeAndSave();var panel=EditorWindow.GetWindow<VRCSdkControlPanel>();panel.Show();await Task.Delay(500);
        if(!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var api))throw new InvalidOperationException("SDK builder unavailable");
        var builder=(VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder)api;builder.RegisterBuilder(panel);if(!builder.IsValidBuilder(out string message))throw new InvalidOperationException(message);builder.Initialize();
        try
        {
            AutoTick(false);string built=await api.Build();string path=Tmp+"/OWM_Episode123_v4.vrcw";File.Copy(built,path,true);
            return Report("sdk-build.json",new{success=true,path,bytes=new FileInfo(path).Length,uploaded=false,autoTickSuspendedDuringNativeBuild=true});
        }
        catch(Exception e){Report("sdk-build.json",new{success=false,error=e.ToString()});throw;}
        finally{AutoTick(true);}
    }
    public static object InspectBuiltBundle()
    {
        string path=Tmp+"/OWM_Episode123_v4.vrcw";var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new InvalidOperationException("SDK bundle load failed");
        try{string[] scenes=bundle.GetAllScenePaths();bool success=bundle.isStreamedSceneAssetBundle&&scenes.Length==1;var result=new{success,bytes=new FileInfo(path).Length,scenes,scope="AssetBundle load and scene table, not VRChat client launch"};if(!success)throw new InvalidOperationException(JsonConvert.SerializeObject(result));return Report("sdk-bundle-inspection.json",result);}finally{bundle.Unload(false);}
    }
    static string HashFile(string path)
    {
        using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    public static object ReopenCheck()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath || SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Saved, stopped owned V4 scene required for reopen verification");
        string baseline="Assets/Scenes/OWM_Episode123_v3.unity";
        var copies=Directory.GetFiles(Tmp+"/baseline","user-saved-v3-*.unity");if(copies.Length==0)throw new InvalidOperationException("Original V3 backup missing");
        string originalHash=HashFile(baseline);bool originalPreserved=copies.Any(path=>HashFile(path)==originalHash);if(!originalPreserved)throw new InvalidOperationException("V3 original differs from all pre-V4 backups");
        string sceneHash=HashFile(ScenePath);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var p=F<OWV4Presentation>();var m=F<OWV4Montage>();var c=F<OWV4HubCast>();var o=F<OWV4Oscilloscope>();
        bool waiting=p!=null && p.menuRoot.activeInHierarchy && !p.performanceRoot.activeInHierarchy && !p.nameplateRoot.activeInHierarchy && !p.overlayRoot.activeInHierarchy;
        bool references=p.journey!=null && p.cast==c && p.montage==m && p.oscilloscope==o && p.operations!=null && p.cameraFx!=null && p.fallbackCamera!=null;
        bool content=m.imageTextures.Length==374 && m.starts.Length==580 && m.roles.Count(role=>role==1)==15 && c.nameplates.playerPlates.Length==80 && o.regionalWaves.Length==16 && o.music.sources.All(source=>source!=null && source.clip!=null);
        bool support=m.usePresentationLayout && m.supportPlanes!=null && m.supportPlanes.Length==4 && m.supportStarts.Length==22;
        bool anonymous=c.nameplates.anonymousLabel=="xxxx" && c.nameplates.actorLabels.Concat(c.nameplates.playerLabels).All(label=>label!=null && label.text=="xxxx");
        bool programs=GameObject.Find("OWEpisode123").GetComponentsInChildren<UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.StartsWith("OWV4")).All(b=>Vm(b)!=null && Vm(b).programSource!=null);
        bool unchanged=HashFile(ScenePath)==sceneHash;var result=new{success=waiting&&references&&content&&programs&&support&&anonymous&&unchanged&&originalPreserved,waiting,references,content,programs,support,anonymous,savedSceneUnchanged=unchanged,v3OriginalPreserved=originalPreserved,v3Sha256=originalHash,v4Sha256=sceneHash,orbitRadius=o.orbitRadius,fusedCenter=new[]{o.fusedCenter.x,o.fusedCenter.y,o.fusedCenter.z},orbitCenter=new[]{o.orbitCenter.x,o.orbitCenter.y,o.orbitCenter.z},scene=ScenePath};
        string report=Report("persistence.json",result);if(!result.success)throw new InvalidOperationException(report);return result;
    }

}
