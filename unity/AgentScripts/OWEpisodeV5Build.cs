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
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

// V5 integration on a copy of the saved V4 scene. Module builders (Atmosphere,
// Signal, Identity, Foley, Cast) own their objects; this file owns the scene
// copy, Udon programs, performed text, offline pose, capture, film and SDK build.
public static class OWEpisodeV5Build
{
    const string V4Path="Assets/Scenes/OWM_Episode123_v4.unity";
    const string ScenePath="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision5";
    const int MovieFrames=3376;
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v5");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    static VRC.Udon.UdonBehaviour Vm(UdonSharpBehaviour b)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
    static void Sync(UdonSharpBehaviour b){UdonSharpEditorUtility.CopyProxyToUdon(b);var vm=Vm(b);if(vm!=null)EditorUtility.SetDirty(vm);EditorUtility.SetDirty(b);}
    static void RequireV5(){if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped V5 scene required");}
    static string HashFile(string path){using(var sha=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static UdonSharpBehaviour[] V5Behaviours()=>Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.StartsWith("OWV5")).ToArray();
    static void Invoke(UdonSharpBehaviour b,string method,float time){var m=b.GetType().GetMethod(method,new[]{typeof(float)});if(m!=null)m.Invoke(b,new object[]{time});}

    public static object CreateScene()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first");
        var active=SceneManager.GetActiveScene();
        if(active.path==ScenePath)return new{scene=ScenePath,created=false,message="V5 already open"};
        if(active.path!=V4Path||active.isDirty)throw new InvalidOperationException("Saved, unmodified V4 scene must be active before copying: "+active.path+" dirty="+active.isDirty);
        Folder(Root+"/Programs");Folder(Root+"/Fonts");Folder(Root+"/Text");
        Directory.CreateDirectory(Tmp+"/baseline");string hash=HashFile(V4Path);string backup=Tmp+"/baseline/user-saved-v4-"+DateTime.Now.ToString("yyyyMMddHHmmss")+".unity";File.Copy(V4Path,backup,true);
        bool created=false;if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null){if(!AssetDatabase.CopyAsset(V4Path,ScenePath))throw new InvalidOperationException("V4→V5 copy failed");created=true;}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        return Report("create-scene.json",new{scene=ScenePath,created,v4Sha256=hash,backup});
    }

    public static object PrepareImport(){if(!SessionState.GetBool("OWEpisodeV5ImportsHeld",false)){AssetDatabase.DisallowAutoRefresh();SessionState.SetBool("OWEpisodeV5ImportsHeld",true);}return "auto refresh held for one V5 batch";}
    public static object ResumeImport()
    {
        if(SessionState.GetBool("OWEpisodeV5ImportsHeld",false)){AssetDatabase.AllowAutoRefresh();SessionState.SetBool("OWEpisodeV5ImportsHeld",false);}
        // Refresh first: folders copied on disk must be imported, never recreated with a suffix.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string module in new[]{"Atmosphere","Signal","Identity","Foley","Cast"})Folder(Root+"/"+module);
        return "V5 batch imported";
    }
    public static object RepairPrograms()
    {
        Folder(Root+"/Programs");var made=new List<string>();var programs=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Where(a=>a!=null).ToArray();
        foreach(string guid in AssetDatabase.FindAssets("OWV5 t:MonoScript",new[]{"Assets/OldWebMap/Scripts"}))
        {
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));if(script==null||!script.name.StartsWith("OWV5"))continue;
            if(programs.Any(a=>a.sourceCsScript==script))continue;
            var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;AssetDatabase.CreateAsset(program,Root+"/Programs/"+script.name+".asset");made.Add(script.name);
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new InvalidOperationException("Udon compilation failed");AssetDatabase.SaveAssets();
        return new{created=made,compiled=true};
    }

    static string Visible(string text)=>System.Text.RegularExpressions.Regex.Replace(text??"","<[^>]+>","");
    static TMP_FontAsset TextFont(IEnumerable<string> strings)
    {
        string path=Root+"/Fonts/V5PerformedText4096.asset";var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(font==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");if(source==null)throw new InvalidOperationException("NotoSansSC source missing");
            font=TMP_FontAsset.CreateFontAsset(source,72,9,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,false);font.name="V5PerformedText4096";font.isMultiAtlasTexturesEnabled=false;AssetDatabase.CreateAsset(font,path);
            if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
        }
        string all=new string(strings.SelectMany(s=>Visible(s)).Concat("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz，。！？、：；“”‘’（）《》【】…—·-=+ ").Where(c=>!char.IsWhiteSpace(c)).Distinct().ToArray());
        string required=new string(all.Where(c=>!font.HasCharacter(c)).ToArray());
        if(required.Length>0&&!font.TryAddCharacters(required,out string missing))
        {
            // A reloaded dynamic asset may refuse to reopen its face; rebuild once with every performed glyph.
            AssetDatabase.DeleteAsset(path);var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
            font=TMP_FontAsset.CreateFontAsset(source,72,9,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,false);font.name="V5PerformedText4096";font.isMultiAtlasTexturesEnabled=false;AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
            if(!font.TryAddCharacters(all,out missing))throw new InvalidOperationException("V5 glyphs missing: "+missing);
        }
        foreach(var atlas in font.atlasTextures)if(atlas!=null&&!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
        EditorUtility.SetDirty(font);return font;
    }
    static Material DanmakuMaterial(TMP_FontAsset font)
    {
        string path=Root+"/Fonts/V5DanmakuOutline.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(font.material){name="V5DanmakuOutline"};AssetDatabase.CreateAsset(mat,path);}
        mat.shader=font.material.shader;mat.SetTexture("_MainTex",font.atlasTexture);mat.SetFloat("_FaceDilate",.14f);mat.SetFloat("_OutlineWidth",.24f);mat.SetColor("_OutlineColor",new Color(0,0,0,.92f));mat.SetFloat("_OutlineSoftness",.05f);
        EditorUtility.SetDirty(mat);return mat;
    }

    public static object ApplyText()
    {
        RequireV5();var data=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-v5-text.json"));
        var keys=((JArray)data["keylines"]).ToArray();var comments=((JArray)data["comments"]).ToArray();
        var m=F<OWV4Montage>();var journey=F<OWV4Journey>();if(m==null||journey==null)throw new InvalidOperationException("V4 montage/journey missing in V5 scene");
        var font=TextFont(keys.Select(k=>(string)k["text"]).Concat(comments.Select(c=>(string)c["text"])));var danmaku=DanmakuMaterial(font);
        m.keyStarts=keys.Select(k=>(float)k["start"]).ToArray();m.keyEnds=keys.Select(k=>(float)k["end"]).ToArray();m.keyStrings=keys.Select(k=>(string)k["text"]).ToArray();m.keyRegions=keys.Select(k=>(int)k["region"]).ToArray();m.keyStyles=keys.Select(k=>(int)k["style"]).ToArray();
        m.commentTimes=comments.Select(c=>(float)c["time"]).ToArray();m.commentDurations=comments.Select(c=>(float)c["duration"]).ToArray();m.commentStrings=comments.Select(c=>(string)c["text"]).ToArray();m.commentRegions=comments.Select(c=>(int)c["region"]).ToArray();m.commentChannels=comments.Select(c=>(int)c["channel"]).ToArray();
        // Low reading band ≈16.6° below the reference eye, tilted to face it, clear of the actors' bodies.
        Vector3 eye=new Vector3(0,1.35f,-2.8f),local=new Vector3(0,.25f,.9f);float pitch=Mathf.Atan2(eye.y-local.y,local.z-eye.z)*Mathf.Rad2Deg;
        for(int r=0;r<4;r++)
        {
            m.keyRoots[r].localPosition=journey.RegionOffset(r)+local;m.keyRoots[r].localRotation=Quaternion.Euler(pitch,0,0);
            m.keyTexts[r].font=font;m.keyTexts[r].fontSharedMaterial=font.material;m.keyTexts[r].richText=false;EditorUtility.SetDirty(m.keyTexts[r]);
        }
        foreach(var t in m.commentTexts){t.font=font;t.fontSharedMaterial=danmaku;t.fontSize=1.75f;t.richText=true;t.enableWordWrapping=false;EditorUtility.SetDirty(t);}
        Sync(m);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("text-apply.json",new{keylines=m.keyStarts.Length,comments=m.commentTimes.Length,unique=m.commentStrings.Distinct().Count(),keylinePitch=pitch,font=AssetDatabase.GetAssetPath(font),glyphs=font.characterTable.Count});
    }

    // Run after BuildCracktro: the waiting window and its ray Canvas shrink together to ≈41° tall at 2.3 m.
    public static object ApplyLayout()
    {
        RequireV5();var p=F<OWV4Presentation>();const float k=.75f;
        if(p.startupObject==null||p.startupObject.name!="V5 Cracktro window")throw new InvalidOperationException("Build the V5 cracktro window first");
        p.menuRoot.transform.localScale=Vector3.one*.0036f*k;p.startupObject.localScale=Vector3.one*k;
        EditorUtility.SetDirty(p.menuRoot.transform);EditorUtility.SetDirty(p.startupObject);p.Waiting();p.PlaceMenu();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("layout.json",new{menuScale=p.menuRoot.transform.localScale.x,windowScale=k});
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
        foreach(var b in V5Behaviours())Invoke(b,seek?"Seek":"Sample",time);
        Canvas.ForceUpdateCanvases();foreach(var text in Object.FindObjectsOfType<TMP_Text>())if(text.havePropertiesChanged)text.ForceMeshUpdate(false,false);
    }
    static void PoseState(int state)
    {
        var p=F<OWV4Presentation>();if(state==0)p.Waiting();else p.Finished();F<OWV4Journey>().Sample(0);
        foreach(var b in V5Behaviours())Invoke(b,"Sample",state==0?0f:F<OWEpisodeDirector>().duration);
    }

    public static object FinalizeAndSave()
    {
        RequireV5();var p=F<OWV4Presentation>();
        foreach(var b in V5Behaviours()){var f=b.GetType().GetField("autoSample");if(f!=null)f.SetValue(b,true);}
        PoseState(0);
        foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.StartsWith("OWV4")||b.GetType().Name.StartsWith("OWV5")||b is OWEpisodeDirector||b is OWEpisodeMusic))Sync(b);
        var owner=GameObject.Find("OWEpisode123").transform;
        foreach(var t in owner.GetComponentsInChildren<Transform>(true))if(PrefabUtility.IsPartOfPrefabInstance(t)){PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Root})){string path=AssetDatabase.GUIDToAssetPath(guid);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null&&mat.name!=Path.GetFileNameWithoutExtension(path)){mat.name=Path.GetFileNameWithoutExtension(path);EditorUtility.SetDirty(mat);}}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        var m=F<OWV4Montage>();
        return Report("integration.json",new{scene=ScenePath,events=m.starts.Length,images=m.imageTextures.Length,keylines=m.keyStarts.Length,comments=m.commentTimes.Length,v5Modules=V5Behaviours().Select(b=>b.GetType().Name+"@"+b.gameObject.name).ToArray(),startup=p.startupObject==null?null:p.startupObject.name,waitingClean=!p.performanceRoot.activeSelf,menuVisible=p.menuRoot.activeInHierarchy});
    }

    static string Frame(Camera c,string path,bool menus=false,int width=1600,int height=900)
    {
        var p=F<OWV4Presentation>();bool shown=p.menuRoot.activeSelf;if(!menus)p.menuRoot.SetActive(false);Directory.CreateDirectory(Path.GetDirectoryName(path));var previous=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);var im=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;im.ReadPixels(new Rect(0,0,width,height),0,0);im.Apply();File.WriteAllBytes(path,path.EndsWith(".jpg")?im.EncodeToJPG(92):im.EncodeToPNG());}
        finally{c.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(im);p.menuRoot.SetActive(shown);}return path;
    }
    static readonly float[] CaptureTimes={.04f,.96f,3.2f,5.6f,11.6f,13.6f,17.2f,19.2f,23.52f,25.6f,28.16f,34.56f,40.96f,44.16f,46.5f,47.9f,50.8f,52.4f,55.36f,58.4f,60.6f,64.48f,66.4f,69.12f,74.88f,78.4f,80.2f,82.88f,85.12f,87.3f,89.6f,91.04f,93.2f,96.64f,103.6f,105.6f,109.44f,114f,118.8f,121.6f,125.44f,129.28f,131.84f};
    public static object CaptureAll(){return CaptureSet("frames",CaptureTimes);}
    public static object CaptureSome(string label,float[] times){return CaptureSet(label,times);}
    static object CaptureSet(string folder,float[] times)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped reference capture");var rows=new List<object>();
        try
        {
            PoseState(0);rows.Add(new{time=-1f,path=Frame(Camera.main,Tmp+"/"+folder+"/v5_waiting.png",true)});
            foreach(float t in times){Pose(t,true);rows.Add(new{time=t,path=Frame(Camera.main,Tmp+"/"+folder+"/v5_"+t.ToString("000.00",System.Globalization.CultureInfo.InvariantCulture)+".png")});}
            PoseState(2);rows.Add(new{time=134f,path=Frame(Camera.main,Tmp+"/"+folder+"/v5_finished.png",true)});
        }
        finally{PoseState(0);}return Report("capture-"+folder+".json",rows);
    }

    public static object TextAndSpaceChecks()
    {
        RequireV5();var p=F<OWV4Presentation>();var rows=new List<object>();var forbidden=new[]{"本项目","原创演示","风格复刻","审核","Agent","示意","来源图","原图拆层","注册机","序列号","keygen","serial","crack","patch","Generate"};
        try
        {
            foreach(float time in new[]{3.2f,25.6f,35.8f,47.8f,58.4f,66.9f,74.9f,85.12f,91.04f,105.6f,118.8f,129.28f})
            {
                Pose(time,true);var texts=Object.FindObjectsOfType<TMP_Text>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
                var bad=texts.Where(t=>forbidden.Any(s=>t.text.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0)).Select(t=>new{t.name,t.text}).ToArray();if(bad.Length>0)throw new InvalidOperationException("Performed text contains forbidden wording: "+JsonConvert.SerializeObject(bad));
                var osc=F<OWV4Oscilloscope>();float error=Enumerable.Range(0,4).Max(ch=>Vector3.Distance(osc.waves[ch].position,osc.music.sources[ch].transform.position));if(error>.0001f)throw new InvalidOperationException("Voice/wave mismatch at "+time);
                var m=F<OWV4Montage>();rows.Add(new{time,focus=m.activeFocus,periphery=m.activePeriphery,ambient=m.activeAmbient,comments=m.activeComments,key=m.sampledKey,texts=texts.Where(t=>!string.IsNullOrEmpty(t.text)).Select(t=>t.text).Distinct().Take(40).ToArray(),voiceError=error});
            }
            PoseState(0);bool waiting=p.menuRoot.activeInHierarchy&&!p.performanceRoot.activeInHierarchy&&p.startupObject!=null&&p.startupObject.gameObject.activeInHierarchy;
            PoseState(2);bool ended=p.menuRoot.activeInHierarchy&&!p.performanceRoot.activeInHierarchy&&p.startupObject.gameObject.activeInHierarchy;
            p.PlaceMenu();Vector3 viewport=Camera.main.WorldToViewportPoint(p.menuRoot.transform.position);bool inside=viewport.z>0&&viewport.x>0&&viewport.x<1&&viewport.y>0&&viewport.y<1;
            Vector3 menuPoint=p.menuRoot.transform.position;bool ray=Physics.Raycast(Camera.main.transform.position,(menuPoint-Camera.main.transform.position).normalized,out var hit,5,1<<17,QueryTriggerInteraction.Ignore)&&hit.collider.gameObject==p.menuRoot;
            var result=new{success=waiting&&ended&&inside&&ray,waiting,ended,menuInFront=inside,ray,rows};string json=Report("text-space.json",result);if(!result.success)throw new InvalidOperationException(json);return new{result.success,waiting,ended,ray,samples=rows.Count};
        }
        finally{PoseState(0);}
    }

    static Color32[] Pixels(Camera camera)
    {
        var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,675,24);var image=new Texture2D(1200,675,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,675),0,0);image.Apply();return image.GetPixels32();}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static bool Changed(Color32 a,Color32 b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))>24;
    public static object KeylineVisibilityChecks()
    {
        RequireV5();var p=F<OWV4Presentation>();var montage=F<OWV4Montage>();var rows=new List<object>();float minimum=1f;
        try
        {
            for(int key=0;key<montage.keyStarts.Length;key++)
            {
                foreach(float time in new[]{montage.keyStarts[key]+.6f,(montage.keyStarts[key]+montage.keyEnds[key])*.5f,montage.keyEnds[key]-.4f})
                {
                    Pose(time,true);var root=montage.keyRoots[montage.keyRegions[key]];var text=montage.keyTexts[montage.keyRegions[key]].GetComponent<Renderer>();
                    var renderers=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy).ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();int actual=0,potential=0;
                    try
                    {
                        var withText=Pixels(Camera.main);text.enabled=false;var withoutText=Pixels(Camera.main);text.enabled=true;
                        foreach(var r in renderers)if(!r.transform.IsChildOf(root)&&!r.transform.IsChildOf(p.overlayRoot.transform))r.enabled=false;
                        var isolated=Pixels(Camera.main);text.enabled=false;var plateOnly=Pixels(Camera.main);
                        for(int i=0;i<isolated.Length;i++)if(Changed(isolated[i],plateOnly[i])){potential++;if(Changed(withText[i],withoutText[i]))actual++;}
                    }
                    finally{for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];text.enabled=true;}
                    float ratio=potential==0?0:actual/(float)potential;minimum=Mathf.Min(minimum,ratio);rows.Add(new{key,time,text=montage.keyStrings[key],actual,potential,visibleRatio=ratio});
                    if(potential<100||ratio<.97f)throw new InvalidOperationException("Keyline glyphs occluded: "+JsonConvert.SerializeObject(rows.Last()));
                }
            }
            return Report("keyline-visibility.json",new{success=true,samples=rows.Count,minVisibleGlyphPixelRatio=minimum,method="Rendered glyph on/off difference, full scene vs isolated keyline; start/mid/end of each line",rows});
        }
        catch(Exception e){Report("keyline-visibility-failure.json",new{success=false,error=e.ToString(),rows});throw;}
        finally{PoseState(0);}
    }

    static bool _movie;static int _frame;static Camera _camera;static RenderTexture _movieRt;static Texture2D _movieTexture;static string _movieError;
    public static object StartMovie()
    {
        RequireV5();if(SessionState.GetBool("EpisodeV5Movie",false))throw new InvalidOperationException("Export already running");
        Directory.CreateDirectory(Tmp+"/movie-frames");_camera=Camera.main;_movieRt=new RenderTexture(1600,900,24);_movieRt.Create();_movieTexture=new Texture2D(1600,900,TextureFormat.RGB24,false);
        foreach(var panel in Object.FindObjectOfType<OWEpisodeDirector>().audiencePanels)panel.SetActive(false);
        _frame=0;_movieError=null;_movie=true;SessionState.SetBool("EpisodeV5Movie",true);SessionState.SetInt("EpisodeV5Frame",0);SessionState.SetString("EpisodeV5MovieError","");Pose(0,true);EditorApplication.update+=MovieTick;return new{started=true,frames=MovieFrames,fps=25,path=Tmp+"/movie-frames"};
    }
    static void MovieTick()
    {
        if(!_movie)return;
        try
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int batch=0;batch<7&&_frame<MovieFrames;batch++)
            {
                float t=Mathf.Min(_frame/25f,133.12f);Pose(t);
                var previous=_camera.targetTexture;var active=RenderTexture.active;
                try{_camera.targetTexture=_movieRt;_camera.Render();RenderTexture.active=_movieRt;_movieTexture.ReadPixels(new Rect(0,0,1600,900),0,0);_movieTexture.Apply();File.WriteAllBytes(Tmp+"/movie-frames/frame_"+_frame.ToString("00000")+".jpg",_movieTexture.EncodeToJPG(92));}
                finally{_camera.targetTexture=previous;RenderTexture.active=active;}
                _frame++;SessionState.SetInt("EpisodeV5Frame",_frame);if(watch.ElapsedMilliseconds>130)break;
            }
            if(_frame%25<7)Report("movie-progress.json",new{frame=_frame,total=MovieFrames,complete=false});
            if(_frame>=MovieFrames)EndMovie();
        }
        catch(Exception e){_movieError=e.ToString();EndMovie();}
    }
    static void EndMovie()
    {
        EditorApplication.update-=MovieTick;_movie=false;SessionState.SetBool("EpisodeV5Movie",false);SessionState.SetString("EpisodeV5MovieError",_movieError??"");
        if(_movieRt!=null){_movieRt.Release();Object.DestroyImmediate(_movieRt);}if(_movieTexture!=null)Object.DestroyImmediate(_movieTexture);
        PoseState(0);Report("movie-progress.json",new{frame=_frame,total=MovieFrames,complete=_frame==MovieFrames,error=_movieError});
    }
    public static object MovieStatus(){return new{running=SessionState.GetBool("EpisodeV5Movie",false),frame=SessionState.GetInt("EpisodeV5Frame",0),total=MovieFrames,error=SessionState.GetString("EpisodeV5MovieError","")};}

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
            AutoTick(false);string built=await api.Build();string path=Tmp+"/OWM_Episode123_v5.vrcw";File.Copy(built,path,true);
            return Report("sdk-build.json",new{success=true,path,bytes=new FileInfo(path).Length,uploaded=false,autoTickSuspendedDuringNativeBuild=true});
        }
        catch(Exception e){Report("sdk-build.json",new{success=false,error=e.ToString()});throw;}
        finally{AutoTick(true);}
    }
    public static object InspectBuiltBundle()
    {
        string path=Tmp+"/OWM_Episode123_v5.vrcw";var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new InvalidOperationException("SDK bundle load failed");
        try{string[] scenes=bundle.GetAllScenePaths();bool success=bundle.isStreamedSceneAssetBundle&&scenes.Length==1&&scenes[0].EndsWith("OWM_Episode123_v5.unity");var result=new{success,bytes=new FileInfo(path).Length,scenes,scope="AssetBundle load and scene table, not VRChat client launch"};if(!success)throw new InvalidOperationException(JsonConvert.SerializeObject(result));return Report("sdk-bundle-inspection.json",result);}finally{bundle.Unload(false);}
    }

    public static object ReopenCheck()
    {
        if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=ScenePath||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Saved, stopped V5 scene required");
        var copies=Directory.GetFiles(Tmp+"/baseline","user-saved-v4-*.unity");if(copies.Length==0)throw new InvalidOperationException("V4 backup missing");
        string v4Hash=HashFile(V4Path);bool v4Preserved=copies.Any(path=>HashFile(path)==v4Hash);string sceneHash=HashFile(ScenePath);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var p=F<OWV4Presentation>();var m=F<OWV4Montage>();var c=F<OWV4HubCast>();var o=F<OWV4Oscilloscope>();
        bool waiting=p.menuRoot.activeInHierarchy&&!p.performanceRoot.activeInHierarchy&&!p.nameplateRoot.activeInHierarchy&&p.startupObject!=null&&p.startupObject.gameObject.activeInHierarchy;
        bool content=m.imageTextures.Length==374&&m.starts.Length==580&&m.keyStarts.Length==8&&m.commentTimes.Length==m.commentStrings.Distinct().Count()&&o.music.sources.All(s=>s!=null&&s.clip!=null);
        var modules=V5Behaviours();bool programs=modules.Length>0&&modules.All(b=>Vm(b)!=null&&Vm(b).programSource!=null)&&Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.StartsWith("OWV4")).All(b=>Vm(b)!=null&&Vm(b).programSource!=null);
        bool anonymous=c.nameplates.actorLabels.Concat(c.nameplates.playerLabels).All(l=>l!=null&&l.text=="xxxx");
        bool unchanged=HashFile(ScenePath)==sceneHash;
        var result=new{success=waiting&&content&&programs&&anonymous&&unchanged&&v4Preserved,waiting,content,programs,anonymous,savedSceneUnchanged=unchanged,v4Preserved,v4Sha256=v4Hash,v5Sha256=sceneHash,modules=modules.Select(b=>b.GetType().Name).ToArray(),scene=ScenePath};
        string report=Report("persistence.json",result);if(!result.success)throw new InvalidOperationException(report);return result;
    }
}
