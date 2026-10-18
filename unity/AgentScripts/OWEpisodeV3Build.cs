using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using TMPro;
using Object=UnityEngine.Object;

public static class OWEpisodeV3Build
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v3.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision3";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v3");
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    public static object Prepare()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling)throw new InvalidOperationException("Stopped editor required");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("User scene has unsaved edits; preserve before proceeding");
        string current=SceneManager.GetActiveScene().path;
        if(current!="Assets/Scenes/OWM_Episode123_v2.unity" && current!=ScenePath)throw new InvalidOperationException("Unexpected scene "+current);
        Directory.CreateDirectory(Tmp+"/baseline");
        if(current!=ScenePath)
        {
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            File.Copy(current,Tmp+"/baseline/user-saved-v2-"+stamp+".unity",false);
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)throw new InvalidOperationException("V3 exists; do not overwrite");
            if(!AssetDatabase.CopyAsset(current,ScenePath))throw new InvalidOperationException("Clone failed");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        foreach(string p in new[]{Root,Root+"/Images",Root+"/Materials",Root+"/Meshes",Root+"/Data",Root+"/Programs"})Folder(p);
        return Report("baseline.json",new{scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,originalPreserved=true,clonedFrom=current,consoleBaselineCursor=3542});
    }
    public static object ImportRoom()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor required");
        string source=@"D:\Projects\VRChatProjects\GiantSleepWorld\Assets\Room01";
        var files=new List<string>();
        foreach(string dir in new[]{"Scene/model","materials","texture"})
        {
            string p=Path.Combine(source,dir);if(!Directory.Exists(p))continue;
            files.AddRange(Directory.GetFiles(p,"*",SearchOption.AllDirectories));
        }
        if(!files.Any(p=>p.EndsWith("Room01.fbx",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Room model missing");
        foreach(string f in files.Where(f=>f.EndsWith(".meta")))
        {
            var match=Regex.Match(File.ReadAllText(f),@"guid:\s*([0-9a-f]{32})");if(!match.Success)continue;
            string existing=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            if(!string.IsNullOrEmpty(existing) && !existing.StartsWith("Assets/Room01/"))throw new InvalidOperationException("External GUID conflict: "+existing);
        }
        AssetDatabase.DisallowAutoRefresh();
        try{
            foreach(string f in files)
            {
                string relative=f.Substring(source.Length+1);string to=Path.Combine("Assets/Room01",relative);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                if(!File.Exists(to))File.Copy(f,to,false);
            }
        }finally{AssetDatabase.AllowAutoRefresh();}
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Room01"}))
        {
            string p=AssetDatabase.GUIDToAssetPath(guid);var t=AssetImporter.GetAtPath(p) as TextureImporter;if(t==null)continue;
            t.maxTextureSize=1024;t.textureCompression=TextureImporterCompression.CompressedHQ;t.isReadable=false;t.SaveAndReimport();
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Room01/Scene/model/Room01.fbx");
        return Report("room-import.json",new{files=files.Count,model=model!=null,meshes=model==null?null:model.GetComponentsInChildren<MeshFilter>(true).Select(m=>new{name=m.name,vertices=m.sharedMesh.vertexCount,materials=m.GetComponent<Renderer>().sharedMaterials.Select(x=>x==null?null:AssetDatabase.GetAssetPath(x)).ToArray()}).ToArray()});
    }
    public static object RepairPrograms()
    {
        Folder(Root+"/Programs");
        var programs=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Where(a=>a!=null).ToArray();
        foreach(string name in new[]{"OWV3Oscilloscope","OWV3CameraFx","OWV3HubCast","OWV3Montage","OWV3Operations"})
        {
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/OldWebMap/Scripts/"+name+".cs");
            if(script==null)throw new InvalidOperationException("Missing script "+name);
            if(programs.Any(p=>p.sourceCsScript==script))continue;
            var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;AssetDatabase.CreateAsset(program,Root+"/Programs/"+name+".asset");
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new InvalidOperationException("V3 Udon compile failed; inspect diagnostics");
        AssetDatabase.SaveAssets();return "V3 programs compiled";
    }
    public static object FinalizeAndSave()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped owned V3 scene required");
        var d=Object.FindObjectOfType<OWEpisodeDirector>();var s=d.stage;var m=Object.FindObjectOfType<OWV3Montage>();var osc=Object.FindObjectOfType<OWV3Oscilloscope>();var cast=Object.FindObjectOfType<OWV3HubCast>();
        if(m==null || osc==null || cast==null)throw new InvalidOperationException("Build all three V3 modules first");
        var owner=GameObject.Find("OWEpisode123").transform;
        var moving=owner.Find("MovingScenery");var room=moving==null?null:moving.Find("OpeningRoom");if(room!=null)room.SetParent(owner,true);if(moving!=null)moving.gameObject.SetActive(false);
        var old=d.montage!=null?d.montage:owner.GetComponentInChildren<OWEpisodeMontage>(true);
        if(old!=null)
        {
            foreach(var t in old.slotRoots)if(t!=null)t.gameObject.SetActive(false);
            foreach(var t in old.starLayers)if(t!=null)t.gameObject.SetActive(false);
            foreach(var text in new[]{old.focusWord,old.imageLabel,old.greeting})if(text!=null)text.gameObject.SetActive(false);
            if(old.logoRoot!=null)old.logoRoot.gameObject.SetActive(false);
            var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(old);if(vm!=null)vm.enabled=false;
            Object.DestroyImmediate(old.gameObject);
        }
        d.montage=null;d.continuousPerformance=true;
        var revision2=owner.Find("Revision2");if(revision2!=null)foreach(var t in revision2.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("Voice label"))t.gameObject.SetActive(false);
        // Dedicated cast owns actors; legacy stage retains its stable support,
        // captions and playback state without also writing their transforms.
        s.actors=new OWActorRig[0];
        if(s.chapterText!=null)s.chapterText.gameObject.SetActive(false);
        if(m.sceneCaption!=s.captionText)
        {
            if(m.sceneCaption!=null)m.sceneCaption.gameObject.SetActive(false);
            m.sceneCaption=s.captionText;s.captionText.font=m.sceneTitle.font;
            string path=Root+"/Materials/V3CaptionOverlay.mat";var overlay=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(overlay==null){overlay=new Material(s.captionText.font.material);AssetDatabase.CreateAsset(overlay,path);}
            else EditorUtility.CopySerialized(s.captionText.font.material,overlay);
            overlay.shader=Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");overlay.renderQueue=5000;s.captionText.fontSharedMaterial=overlay;EditorUtility.SetDirty(overlay);
        }
        s.captions=s.starts.Select(t=>m.sceneCaptions[Mathf.Clamp(Mathf.FloorToInt(t/10.24f+.001f),0,12)]).ToArray();
        // Keep the fused signal high enough to frame the content rather than
        // obscure its main readable areas. The climax uses its orbit center.
        osc.fusedCenter=new Vector3(0,4.1f,8.2f);osc.orbitCenter=new Vector3(0,1.65f,-2.8f);m.matrixCenter=new Vector3(0,3.6f,9.8f);m.matrixHubCenter=new Vector3(-4.1f,4.1f,10.5f);m.matrixSize=.78f;
        if(s.endCard!=null){var t=s.endCard.GetComponentInChildren<TMPro.TMP_Text>(true);if(t!=null)t.text="前三段 · 第三轮演出结束\n射线点击左侧面板，可以重播。";}
        var sources=owner.Find("Sources and reconstruction");if(sources!=null)
        {var text=sources.GetComponentInChildren<TMP_Text>(true);if(text!=null)text.text="前三段 V3 · 来源与制作\n\n真实界面 / 文档 / 网页存档\nGUIdebook · Aegisub Docs\nVRChat 官方 · Steam / Medium\nGeekPark / Sina · Wayback\n\n144 个原创操作状态\nRoom01：家具、书与台灯\n\n音乐：FC14 四声部\nRhino of Torment（资料记载）\n视觉：EDGE 模板风格 / 原生 3D\n\n355 项内容的性质与来源分别登记。";}
        CloneActorMaterials();Pose(0,true);
        foreach(var b in owner.GetComponentsInChildren<UdonSharpBehaviour>(true)){UdonSharpEditorUtility.CopyProxyToUdon(b);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(b));}
        foreach(var t in owner.GetComponentsInChildren<Transform>(true))if(PrefabUtility.IsPartOfPrefabInstance(t))
        {PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var component in t.GetComponents<Component>())if(component!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {string path=AssetDatabase.GUIDToAssetPath(guid);var material=AssetDatabase.LoadAssetAtPath<Material>(path);string expected=Path.GetFileNameWithoutExtension(path);if(material!=null&&material.name!=expected){material.name=expected;EditorUtility.SetDirty(material);}}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return Report("integration.json",new{scene=ScenePath,events=m.starts.Length,images=m.imageTextures.Length,slots=m.windowRoots.Length,actorSlots=cast.actors.Length,alternateBodies=cast.alternateActors.Count(a=>a!=null),particles=osc.particles.Length,oldMontageRetired=d.montage==null,legacyActorWrites=s.actors.Length,continuous=d.continuousPerformance,captionOverlay=s.captionText.fontSharedMaterial.shader.name});
    }
    public static void Pose(float time,bool seek=false)
    {
        var d=Object.FindObjectOfType<OWEpisodeDirector>();d.stage.Sample(time);d.music.SampleVisual(time);
        var cast=Object.FindObjectOfType<OWV3HubCast>();cast.Sample(time);
        string[] names={"V3Idle","V3Type","V3Carry","V3Present","V3Point","V3Inspect","V3Walk","V3Wave","V3Calibrate","V3Talk","V3Adjust","V3Shelve"};
        foreach(var a in cast.actors.Concat(cast.alternateActors).Where(a=>a!=null && a.gameObject.activeInHierarchy && a.animator!=null && a.animator.gameObject.activeInHierarchy))
        {int pose=a.animator.GetInteger("V3Pose");float phase=a.animator.GetFloat("V3PoseTime");a.animator.Play(names[Mathf.Clamp(pose,0,11)],0,phase);a.animator.Update(0);}
        cast.Sample(time);
        var m=Object.FindObjectOfType<OWV3Montage>();if(seek)m.Seek(time);else m.Sample(time);
        var osc=Object.FindObjectOfType<OWV3Oscilloscope>();if(seek)osc.Seek(time);else osc.Sample(time);
        Object.FindObjectOfType<OWV3CameraFx>().Sample(time);
        var operations=Object.FindObjectOfType<OWV3Operations>();if(operations!=null)operations.Sample(time);
        var follow=Object.FindObjectOfType<OWEpisodeCaption>();if(follow!=null){follow.transform.position=Camera.main.transform.position+Camera.main.transform.rotation*follow.headOffset;follow.transform.rotation=Camera.main.transform.rotation;}
        Canvas.ForceUpdateCanvases();
        foreach(var text in Object.FindObjectsOfType<TMP_Text>())if(text.havePropertiesChanged)text.ForceMeshUpdate(false,false);
    }
    static string Frame(Camera camera,string path,int width=1600,int height=900)
    {
        var panels=Object.FindObjectOfType<OWEpisodeDirector>().audiencePanels;var shown=panels.Select(p=>p.activeSelf).ToArray();foreach(var p in panels)p.SetActive(false);
        Directory.CreateDirectory(Path.GetDirectoryName(path));var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,path.EndsWith(".jpg")?image.EncodeToJPG(92):image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);for(int i=0;i<panels.Length;i++)panels[i].SetActive(shown[i]);}return path;
    }
    public static object CaptureAll()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor reference capture");
        var rows=new List<object>();float[] times={.96f,5.6f,11.6f,13.44f,13.6f,17.2f,19.2f,23.52f,31.84f,34.56f,43.52f,44.16f,44.64f,47.36f,50.8f,53.2f,55.36f,57.9f,60.6f,64.48f,74.88f,82.88f,85.12f,88.64f,91.04f,96.64f,104.96f,105.6f,112.64f,115.2f,120.32f,125.44f,129.28f,131.84f};
        try{foreach(float time in times){Pose(time,true);rows.Add(new{time,path=Frame(Camera.main,Tmp+"/frames/v3_"+time.ToString("000.00",System.Globalization.CultureInfo.InvariantCulture)+".png"),windows=Object.FindObjectOfType<OWV3Montage>().activeWindows,era=Object.FindObjectOfType<OWV3HubCast>().visibleEra});}}
        finally{Pose(0,true);}
        return Report("capture-report.json",rows);
    }
    public static object ViewpointChecks()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor only");var c=Camera.main;var pos=c.transform.position;var rot=c.transform.rotation;var rows=new List<object>();
        try{
            foreach(float height in new[]{.59f,1.35f,1.9f})foreach(float time in new[]{23.52f,43.52f,85.12f,115.2f})
            {
                c.transform.position=new Vector3(0,height,pos.z);c.transform.rotation=Quaternion.identity;Pose(time,true);
                var caption=Object.FindObjectOfType<OWEpisodeDirector>().stage.captionText;
                var panel=Object.FindObjectsOfType<Canvas>(true).First(x=>x.name=="Ray UI - start and replay");var target=panel.transform.position;bool hit=Physics.Raycast(c.transform.position,(target-c.transform.position).normalized,out var h,5,1<<17,QueryTriggerInteraction.Ignore)&&h.collider.gameObject==panel.gameObject;
                rows.Add(new{height,time,captionShader=caption.fontSharedMaterial.shader.name,rayHit=hit,path=Frame(c,Tmp+"/views/view_"+height.ToString("0.00")+"_"+time.ToString("000.00")+".jpg",1280,720)});
                if(!hit)throw new InvalidOperationException("Ray UI is obstructed at eye height "+height);
            }
        }finally{c.transform.SetPositionAndRotation(pos,rot);Pose(0,true);}
        return Report("viewpoints.json",new{success=true,headHeights=3,checks=rows});
    }
    static bool _movie;static int _frame;static Camera _camera;static RenderTexture _movieRt;static Texture2D _movieTexture;static string _movieError;
    public static object StartMovie()
    {
        if(EditorApplication.isPlaying || SessionState.GetBool("EpisodeV3Movie",false))throw new InvalidOperationException("Stopped editor, no existing export");
        Directory.CreateDirectory(Tmp+"/movie-frames");_camera=Camera.main;_movieRt=new RenderTexture(1600,900,24);_movieRt.Create();_movieTexture=new Texture2D(1600,900,TextureFormat.RGB24,false);
        foreach(var panel in Object.FindObjectOfType<OWEpisodeDirector>().audiencePanels)panel.SetActive(false);
        _frame=0;_movieError=null;_movie=true;SessionState.SetBool("EpisodeV3Movie",true);SessionState.SetInt("EpisodeV3Frame",0);SessionState.SetString("EpisodeV3MovieError","");Pose(0,true);EditorApplication.update+=MovieTick;return new{started=true,frames=3376,fps=25,path=Tmp+"/movie-frames"};
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
                _frame++;SessionState.SetInt("EpisodeV3Frame",_frame);if(watch.ElapsedMilliseconds>130)break;
            }
            if(_frame%25<7)Report("movie-progress.json",new{frame=_frame,total=3376,complete=false});
            if(_frame>=3376)EndMovie();
        }
        catch(Exception e){_movieError=e.ToString();EndMovie();}
    }
    static void EndMovie()
    {
        EditorApplication.update-=MovieTick;_movie=false;SessionState.SetBool("EpisodeV3Movie",false);SessionState.SetString("EpisodeV3MovieError",_movieError??"");
        if(_movieRt!=null){_movieRt.Release();Object.DestroyImmediate(_movieRt);}
        if(_movieTexture!=null)Object.DestroyImmediate(_movieTexture);
        Pose(0,true);foreach(var panel in Object.FindObjectOfType<OWEpisodeDirector>().audiencePanels)panel.SetActive(true);Report("movie-progress.json",new{frame=_frame,total=3376,complete=_frame==3376,error=_movieError});
    }
    public static object MovieStatus(){return new{running=SessionState.GetBool("EpisodeV3Movie",false),frame=SessionState.GetInt("EpisodeV3Frame",0),total=3376,error=SessionState.GetString("EpisodeV3MovieError","")};}
    public static object RepairRoomNormals()
    {
        var importer=AssetImporter.GetAtPath("Assets/Room01/Scene/model/Room01.fbx") as ModelImporter;
        if(importer==null)throw new InvalidOperationException("Room model not imported");
        importer.importNormals=ModelImporterNormals.Calculate;importer.normalCalculationMode=ModelImporterNormalCalculationMode.AngleWeighted;importer.normalSmoothingAngle=60;importer.SaveAndReimport();return "Room01 normals calculated by Unity importer";
    }
    public static object ParticleClockCheck()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor only");var osc=Object.FindObjectOfType<OWV3Oscilloscope>();
        osc.Seek(0);for(int f=1;f<=2592;f++)osc.Sample(f/30f+.000001f);
        var continuousTimes=osc.particles.Select(p=>p.time).ToArray();var continuousCounts=osc.particles.Select(p=>p.particleCount).ToArray();
        osc.Seek(86.4f);var absoluteTimes=osc.particles.Select(p=>p.time).ToArray();var absoluteCounts=osc.particles.Select(p=>p.particleCount).ToArray();
        var result=new{target=86.4f,continuousTimes,continuousCounts,absoluteTimes,absoluteCounts,maxContinuousClockError=continuousTimes.Max(t=>Mathf.Abs(t-86.4f)),fixedDeltaTime=Time.fixedDeltaTime};osc.Seek(0);return Report("particle-clock.json",result);
    }
    static string RigState()
    {
        return JsonConvert.SerializeObject(Object.FindObjectsOfType<OWActorRig>(true).OrderBy(a=>a.actorSlot).Select(a=>new{a.actorSlot,a.modelHeight,scale=new[]{a.visualRoot.localScale.x,a.visualRoot.localScale.y,a.visualRoot.localScale.z},position=new[]{a.visualRoot.localPosition.x,a.visualRoot.localPosition.y,a.visualRoot.localPosition.z},controller=AssetDatabase.GetAssetPath(a.animator.runtimeAnimatorController),materials=a.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.sharedMaterials).Select(AssetDatabase.GetAssetPath).ToArray()}).ToArray());
    }
    public static object ReopenCheck()
    {
        FinalizeAndSave();string before=RigState();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);string after=RigState();
        bool same=before==after;var operations=Object.FindObjectOfType<OWV3Operations>();var cast=Object.FindObjectOfType<OWV3HubCast>();
        bool baseline=Directory.GetFiles(Tmp+"/baseline","user-saved-v2-*.unity").Any(p=>File.ReadAllBytes(p).SequenceEqual(File.ReadAllBytes("Assets/Scenes/OWM_Episode123_v2.unity")));
        var result=new{success=same&&baseline&&operations!=null&&cast!=null,rigOverridesPreserved=same,originalV2FileUnchanged=baseline,actorCount=Object.FindObjectsOfType<OWActorRig>(true).Length,snapshot=JsonConvert.DeserializeObject(before),operationsPresent=operations!=null};
        string json=Report("reopen-persistence.json",result);if(!result.success)throw new InvalidOperationException(json);Pose(0,true);return json;
    }
    public static object DiagnoseOperationText()
    {
        var op=Object.FindObjectOfType<OWV3Operations>();op.Sample(44.64f);var rows=new List<object>();
        foreach(var t in new[]{op.sourceLine,op.translationLine,op.correctionLine,op.readmeBody})
        {t.ForceMeshUpdate(true,true);rows.Add(new{t.name,t.text,t.fontSize,width=t.rectTransform.rect.width,height=t.rectTransform.rect.height,characters=t.textInfo.characterCount,visible=t.textInfo.characterInfo.Take(t.textInfo.characterCount).Count(c=>c.isVisible),lineHeight=t.font.faceInfo.lineHeight,pointSize=t.font.faceInfo.pointSize,queue=t.fontSharedMaterial.renderQueue,z=t.transform.position.z,vertices=t.mesh.vertexCount});}
        op.Sample(0);return Report("operation-text-diagnostics.json",rows);
    }
    static Color32[] RenderPixels(string name)
    {
        string path=Frame(Camera.main,Tmp+"/shader-checks/"+name+".png",640,360);var image=new Texture2D(2,2);ImageConversion.LoadImage(image,File.ReadAllBytes(path));var pixels=image.GetPixels32();Object.DestroyImmediate(image);return pixels;
    }
    static float CornerDifference(Color32[] a,Color32[] b)
    {long sum=0;int n=0;for(int y=295;y<350;y++)for(int x=5;x<95;x++){int i=y*640+x;sum+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);n+=3;}return sum/(float)n;}
    public static object ShaderChecks()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor only");var variants=new ShaderVariantCollection();var shaderRows=new List<object>();
        foreach(string name in new[]{"Triggered Oscilloscope","Signal Particles","Local Camera Signal","Layered Window","Metal Point Matrix","Operation Crop"})
        {
            var shader=Shader.Find("OldWebMap/V3/"+name);if(shader==null||!shader.isSupported)throw new InvalidOperationException(name+" unsupported");
            foreach(string keyword in new[]{"STEREO_INSTANCING_ON","STEREO_MULTIVIEW_ON"})variants.Add(new ShaderVariantCollection.ShaderVariant(shader,UnityEngine.Rendering.PassType.Normal,new[]{keyword}));
            var errors=ShaderUtil.GetShaderMessages(shader).Where(e=>e.severity.ToString()=="Error").Select(e=>e.message).ToArray();if(errors.Length>0)throw new InvalidOperationException(string.Join(";",errors));shaderRows.Add(new{name,errors});
        }
        variants.WarmUp();Object.DestroyImmediate(variants);float mirror=Shader.GetGlobalFloat("_VRChatMirrorMode"),camera=Shader.GetGlobalFloat("_VRChatCameraMode");var fx=Object.FindObjectOfType<OWV3CameraFx>();
        try
        {
            Shader.SetGlobalFloat("_VRChatMirrorMode",0);Shader.SetGlobalFloat("_VRChatCameraMode",0);Pose(85.12f,true);
            var cast=Object.FindObjectOfType<OWV3HubCast>();if(cast.mirror2017!=null)cast.mirror2017.SetActive(false);if(cast.mirror2019!=null)cast.mirror2019.SetActive(false);
            var main=RenderPixels("primary-view");
            fx.overlayRenderer.enabled=false;var plain=RenderPixels("effect-disabled");fx.overlayRenderer.enabled=true;
            Shader.SetGlobalFloat("_VRChatMirrorMode",1);float mirrorBefore=Shader.GetGlobalFloat("_VRChatMirrorMode");var mir=RenderPixels("mirror-flag-excluded");float mirrorAfter=Shader.GetGlobalFloat("_VRChatMirrorMode");Shader.SetGlobalFloat("_VRChatMirrorMode",0);Shader.SetGlobalFloat("_VRChatCameraMode",1);var cam=RenderPixels("camera-flag-excluded");
            float mainDiff=CornerDifference(main,plain),mirrorDiff=CornerDifference(mir,plain),cameraDiff=CornerDifference(cam,plain);bool success=mainDiff>.1f&&mirrorDiff<.1f&&cameraDiff<.1f;
            var result=new{success,mainEffectDifference=mainDiff,mirrorExclusionDifference=mirrorDiff,cameraExclusionDifference=cameraDiff,mirrorBefore,mirrorAfter,stereoKeywordVariantsWarmed=true,shaders=shaderRows,scope="Desktop render comparison with explicit VRChat camera flags; no physical headset test"};string report=Report("shader-checks.json",result);if(!success)throw new InvalidOperationException(report);return report;
        }
        finally{Shader.SetGlobalFloat("_VRChatMirrorMode",mirror);Shader.SetGlobalFloat("_VRChatCameraMode",camera);Pose(0,true);}
    }
    static void CloneActorMaterials()
    {
        var map=new Dictionary<Material,Material>();var rows=new List<object>();
        foreach(var actor in Object.FindObjectsOfType<OWActorRig>(true))foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var original=mats[i];if(original==null)continue;string source=AssetDatabase.GetAssetPath(original);
                if(source.StartsWith(Root+"/Materials/Cast_"))continue;
                if(!map.TryGetValue(original,out var mat))
                {
                    string key=AssetDatabase.AssetPathToGUID(source)+"_"+Regex.Replace(original.name,@"[^a-zA-Z0-9_]","_");string path=Root+"/Materials/Cast_"+key+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null){mat=new Material(original);AssetDatabase.CreateAsset(mat,path);}else EditorUtility.CopySerialized(original,mat);
                    // Exposure changes belong to this performance, preserving the
                    // user's imported avatar materials and its texture references.
                    foreach(string prop in new[]{"_UseEmission","_UseEmission2nd"})if(mat.HasProperty(prop))mat.SetFloat(prop,0);
                    if(mat.HasProperty("_LightMaxLimit"))mat.SetFloat("_LightMaxLimit",1);
                    if(mat.HasProperty("_LightMinLimit"))mat.SetFloat("_LightMinLimit",.10f);
                    if(mat.HasProperty("_VertexLightStrength"))mat.SetFloat("_VertexLightStrength",.15f);
                    map[original]=mat;EditorUtility.SetDirty(mat);rows.Add(new{source,performanceMaterial=path,shader=mat.shader.name,texture=AssetDatabase.GetAssetPath(mat.mainTexture)});
                }
                mats[i]=mat;
            }
            renderer.sharedMaterials=mats;
        }
        if(rows.Count>0 || !File.Exists(Tmp+"/reports/performance-materials.json"))Report("performance-materials.json",new{originalMaterialsModified=false,cloned=rows});
    }
    static VRC.Udon.UdonBehaviour Vm(UdonSharpBehaviour b){return UdonSharpEditorUtility.GetBackingUdonBehaviour(b);}
    static float N(VRC.Udon.UdonBehaviour b,string name){return Convert.ToSingle(b.GetProgramVariable(name));}
    public static async Task<object> InteractionChecks()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required");
        var d=Object.FindObjectOfType<OWEpisodeDirector>();var vm=Vm(d);var montage=Vm(Object.FindObjectOfType<OWV3Montage>());var osc=Vm(Object.FindObjectOfType<OWV3Oscilloscope>());var cast=Vm(Object.FindObjectOfType<OWV3HubCast>());
        vm.SendCustomEvent("StopAtStart");await Task.Delay(200);
        var h=Object.FindObjectsOfType<OWEpisodeButton>(true).First(x=>x.name=="Start performance");var button=h.GetComponent<UnityEngine.UI.Button>();
        bool persistent=button.onClick.GetPersistentTarget(0)==Vm(h)&&button.onClick.GetPersistentMethodName(0)=="SendCustomEvent";
        UnityEngine.EventSystems.ExecuteEvents.Execute(h.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left},UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        await Task.Delay(700);bool click=Convert.ToBoolean(vm.GetProgramVariable("_playing"))&&N(vm,"_elapsed")>.1f;
        bool hidden=d.audiencePanels.All(p=>!p.activeInHierarchy);float before=N(vm,"_elapsed");vm.SendCustomEvent("Pause");await Task.Delay(400);bool pauseIgnored=N(vm,"_elapsed")>before+.15f&&d.music.sources.All(s=>s.isPlaying);
        double clock=0;bool join=true;float received=0;var joins=new List<object>();var operations=Vm(Object.FindObjectOfType<OWV3Operations>());
        foreach(float target in new[]{17.2f,44.64f,58.4f,86.4f,128.2f})
        {
            clock=VRC.SDKBase.Utilities.IsValid(VRC.SDKBase.Networking.LocalPlayer)?VRC.SDKBase.Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;
            vm.SetProgramVariable("_offset",0f);vm.SetProgramVariable("_anchor",clock-target);vm.SetProgramVariable("_playing",true);vm.SetProgramVariable("_revision",Convert.ToInt32(vm.GetProgramVariable("_revision"))+1);vm.SendCustomEvent("_onDeserialization");
            await Task.Delay(500);received=N(vm,"_elapsed");int group=Convert.ToInt32(operations.GetProgramVariable("sampledGroup"));int expected=target<20.48f?0:target<51.2f?1:target<61.44f?2:-1;
            bool ok=received>=target&&received<target+1.6f&&Mathf.Abs(N(montage,"sampledTime")-received)<.15f&&Mathf.Abs(N(osc,"sampledTime")-received)<.15f&&Mathf.Abs(N(cast,"sampledTime")-received)<.15f&&group==expected&&d.music.sources.All(s=>s.isPlaying);
            join&=ok;joins.Add(new{target,received,group,expected,ok,readme=N(operations,"readmeCharacters"),corrected=N(operations,"correctedCount"),pieces=N(operations,"receivedPieces")});
        }
        clock=VRC.SDKBase.Utilities.IsValid(VRC.SDKBase.Networking.LocalPlayer)?VRC.SDKBase.Networking.GetServerTimeInSeconds():Time.realtimeSinceStartup;
        vm.SetProgramVariable("_offset",133.0f);vm.SetProgramVariable("_anchor",clock);vm.SetProgramVariable("_revision",Convert.ToInt32(vm.GetProgramVariable("_revision"))+1);await Task.Delay(600);
        bool end=!Convert.ToBoolean(vm.GetProgramVariable("_playing"))&&d.audiencePanels.All(p=>p.activeInHierarchy)&&d.music.sources.All(s=>!s.isPlaying);
        vm.SendCustomEvent("StopAtStart");var result=new{success=persistent&&click&&hidden&&pauseIgnored&&join&&end,persistent,click,hidden,pauseIgnored,receivedAnchorRebuilt=join,joins,receivedTime=received,end,scope="Real Udon/ClientSim pointerClick and received anchor local reconstruction; not actual network transfer between clients"};
        string json=Report("interaction.json",result);if(!result.success)throw new InvalidOperationException(json);return json;
    }
    public static async Task<object> LiveValidation()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required");
        var d=Object.FindObjectOfType<OWEpisodeDirector>();var vm=Vm(d);var m=Object.FindObjectOfType<OWV3Montage>();var mvm=Vm(m);var osc=Object.FindObjectOfType<OWV3Oscilloscope>();var ovm=Vm(osc);var cast=Object.FindObjectOfType<OWV3HubCast>();var cvm=Vm(cast);
        var active=new HashSet<int>();var scenes=new HashSet<int>();var rows=new List<object>();var transients=new List<object>();float maxClock=0,maxFrameMs=0;int maxRaw=0,maxConfirmed=0,maxParticles=0;float[] rms=new float[4];var buffer=new float[1024];
        vm.SendCustomEvent("StopAtStart");await Task.Delay(100);vm.SendCustomEvent("PlayFromConnect");var watch=System.Diagnostics.Stopwatch.StartNew();int lastSecond=-1;
        try
        {
            while(watch.Elapsed.TotalSeconds<136)
            {
                await Task.Delay(65);float t=N(vm,"_elapsed");scenes.Add(Convert.ToInt32(mvm.GetProgramVariable("sampledScene")));
                int[] cursors=(int[])mvm.GetProgramVariable("_cursor");if(cursors!=null)foreach(int c in cursors)if(c>=0&&c<m.starts.Length&&t>=m.starts[c]&&t<m.ends[c])active.Add(c);
                if(t>.4f&&t<132.7f)
                {
                    if(d.music.sources.Any(s=>!s.isPlaying))throw new InvalidOperationException("Audio stopped at "+t);
                    var samples=d.music.sources.Select(s=>s.timeSamples).ToArray();int raw=samples.Max()-samples.Min();maxRaw=Math.Max(maxRaw,raw);int confirmed=raw;
                    if(raw>960){await Task.Delay(65);var resample=d.music.sources.Select(s=>s.timeSamples).ToArray();confirmed=resample.Max()-resample.Min();transients.Add(new{time=t,raw,confirmed});}
                    maxConfirmed=Math.Max(maxConfirmed,confirmed);if(confirmed>1024)throw new InvalidOperationException("Persistent stem drift "+confirmed+" at "+t);
                    maxClock=Mathf.Max(maxClock,Mathf.Abs(d.music.sources[0].time-t));
                    var waves=(Transform[])ovm.GetProgramVariable("waves");for(int ch=0;ch<4;ch++)if(Vector3.Distance(waves[ch].position,d.music.sources[ch].transform.position)>.0001f)throw new InvalidOperationException("Wave/source anchor mismatch at "+t);
                    for(int ch=0;ch<4;ch++){d.music.sources[ch].GetOutputData(buffer,0);double s=0;foreach(float v in buffer)s+=v*v;rms[ch]=Mathf.Max(rms[ch],(float)Math.Sqrt(s/buffer.Length));}
                }
                maxParticles=Math.Max(maxParticles,osc.particles.Sum(p=>p.particleCount));maxFrameMs=Mathf.Max(maxFrameMs,Time.smoothDeltaTime*1000);
                int second=Mathf.FloorToInt(t);
                if(second!=lastSecond){lastSecond=second;rows.Add(new{time=t,scene=Convert.ToInt32(mvm.GetProgramVariable("sampledScene")),windows=N(mvm,"activeWindows"),comments=N(mvm,"activeComments"),era=N(cvm,"visibleEra"),open=N(ovm,"sampledOpen"),particles=osc.particles.Select(p=>p.particleCount).ToArray(),audioSamples=d.music.sources.Select(s=>s.timeSamples).ToArray(),frameMilliseconds=Time.smoothDeltaTime*1000});Report("live-progress.json",new{time=t,eventsObserved=active.Count,scenesObserved=scenes.Count});
                    if(new[]{2,13,23,34,43,56,64,75,84,89,97,106,116,129}.Contains(second))Frame(Camera.main,Tmp+"/runtime/live_"+second.ToString("000")+".jpg",1280,720);
                }
            }
            bool success=scenes.Count==13&&active.Count>=m.starts.Length*.88&&rms.All(x=>x>.0001f)&&!Convert.ToBoolean(vm.GetProgramVariable("_playing"));
            var result=new{success,eventsObserved=active.Count,eventsTotal=m.starts.Length,sceneCount=scenes.Count,peakRms=rms,maxRawSpread=maxRaw,maxConfirmedSpread=maxConfirmed,maxClockError=maxClock,maxFrameMs,maxParticles,transients,rows,scope="One real Editor ClientSim full-duration Udon run; not HMD or multiclient network"};string json=Report("live-validation.json",result);if(!success)throw new InvalidOperationException("Live validation failed, see report");return new{success,eventsObserved=active.Count,eventsTotal=m.starts.Length,scenes=scenes.Count,maxConfirmed,maxClock,maxParticles};
        }
        catch(Exception e){Report("live-failure.json",new{error=e.ToString(),time=N(vm,"_elapsed"),rows});throw;}
        finally{vm.SendCustomEvent("StopAtStart");}
    }
    public static async Task<object> SdkBuildOnly()
    {
        FinalizeAndSave();var panel=EditorWindow.GetWindow<VRCSdkControlPanel>();panel.Show();await Task.Delay(500);
        if(!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var api))throw new InvalidOperationException("SDK builder unavailable");
        var builder=(VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder)api;builder.RegisterBuilder(panel);if(!builder.IsValidBuilder(out string message))throw new InvalidOperationException(message);builder.Initialize();
        try{string built=await api.Build();string path=Tmp+"/OWM_Episode123_v3.vrcw";File.Copy(built,path,true);return Report("sdk-build.json",new{success=true,path,bytes=new FileInfo(path).Length,uploaded=false});}
        catch(Exception e){Report("sdk-build.json",new{success=false,error=e.ToString(),errors=e.GetType().GetField("Errors")?.GetValue(e)});throw;}
    }
    public static object InspectBuiltBundle()
    {
        if(EditorApplication.isPlaying || SessionState.GetBool("EpisodeV3Movie",false))throw new InvalidOperationException("Stopped editor, completed export required");
        string path=Tmp+"/OWM_Episode123_v3.vrcw";var bundle=AssetBundle.LoadFromFile(path);
        if(bundle==null)throw new InvalidOperationException("SDK world bundle could not be loaded");
        try
        {
            string[] scenes=bundle.GetAllScenePaths();bool success=bundle.isStreamedSceneAssetBundle && scenes.Length==1;
            var result=new{success,bytes=new FileInfo(path).Length,streamedSceneBundle=bundle.isStreamedSceneAssetBundle,scenes,scope="Unity AssetBundle load and scene table only; no VRChat client launch"};
            string report=Report("sdk-bundle-inspection.json",result);if(!success)throw new InvalidOperationException(report);return report;
        }
        finally{bundle.Unload(false);}
    }
}
