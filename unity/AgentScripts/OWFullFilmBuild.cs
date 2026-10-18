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

public static class OWFullFilmBuild
{
    const string ScenePath="Assets/Scenes/OWM_FullFilm_v1.unity";
    const string V5="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Root="Assets/OldWebMap/FullFilm/Revision1";
    const int Frames=6450;
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v1");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static string Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static void Guard(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Stopped compiled full-film scene required");}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string j=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,j);return j;}
    static void Sync(UdonSharpBehaviour b){UdonSharpEditorUtility.CopyProxyToUdon(b);var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)EditorUtility.SetDirty(vm);EditorUtility.SetDirty(b);}
    static JObject Data()=>JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-presentation.json"));
    public static object HoldImports(){if(!SessionState.GetBool("FullFilmImports",false)){AssetDatabase.DisallowAutoRefresh();SessionState.SetBool("FullFilmImports",true);}return "imports held";}
    public static object Import()
    {
        if(SessionState.GetBool("FullFilmImports",false)){AssetDatabase.AllowAutoRefresh();SessionState.SetBool("FullFilmImports",false);}
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);return "full-film text and external files imported";
    }
    public static object Programs()
    {
        Folder(Root+"/Programs");var made=new List<string>();
        var existing=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Where(p=>p!=null).ToArray();
        foreach(string g in AssetDatabase.FindAssets("OWFull t:MonoScript",new[]{"Assets/OldWebMap/Scripts"}))
        {
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));if(script==null||!script.name.StartsWith("OWFull")||existing.Any(p=>p.sourceCsScript==script))continue;
            var p=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();p.sourceCsScript=script;AssetDatabase.CreateAsset(p,Root+"/Programs/"+script.name+".asset");made.Add(script.name);
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new Exception("Udon compile failed");AssetDatabase.SaveAssets();return new{success=true,made};
    }
    static TMP_FontAsset FontFor(string text)
    {
        Folder(Root+"/Text");string path=Root+"/Text/FullFilmText.asset";var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(font==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
            font=TMP_FontAsset.CreateFontAsset(source,72,9,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,false);font.name="FullFilmText";AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
        }
        string all=new string(text.Concat("xxxx0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz，。！？、：；…—— ·_✓").Where(c=>!char.IsWhiteSpace(c)).Distinct().ToArray());
        string missing=new string(all.Where(c=>!font.HasCharacter(c)).ToArray());
        if(missing.Length>0&&!font.TryAddCharacters(missing,out string failed))
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
            font=TMP_FontAsset.CreateFontAsset(source,72,9,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,false);font.name="FullFilmTextRecovered";
            AssetDatabase.CreateAsset(font,AssetDatabase.GenerateUniqueAssetPath(Root+"/Text/FullFilmTextRecovered.asset"));AssetDatabase.AddObjectToAsset(font.material,font);
            if(!font.TryAddCharacters(all,out failed))throw new Exception("Full-film glyphs missing: "+failed);
        }
        foreach(var a in font.atlasTextures)if(a!=null&&!AssetDatabase.Contains(a))AssetDatabase.AddObjectToAsset(a,font);EditorUtility.SetDirty(font);return font;
    }
    static Material CopyMaterial(Material source,string name)
    {
        Folder(Root+"/Tuning");string path=Root+"/Tuning/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(source){name=name};AssetDatabase.CreateAsset(mat,path);}return mat;
    }
    static void SetupText(OWFullPresentation run,JObject data)
    {
        var p=run.legacy;var m=p.montage;var keys=(JArray)data["keylines"];var comments=(JArray)data["comments"];
        var font=FontFor(string.Join("",keys.Select(k=>(string)k["text"]).Concat(comments.Select(k=>(string)k["text"])).Concat(data["history_anchors"]==null?new string[0]:data["history_anchors"].Select(h=>(string)h["caption"]))));
        Transform textRoot=run.transform.Find("Continuation performed text");if(textRoot==null){textRoot=new GameObject("Continuation performed text").transform;textRoot.SetParent(run.transform,false);}
        Transform key=textRoot.Find("Continuation keyline");
        if(key==null){key=Object.Instantiate(m.keyRoots[0],textRoot);key.name="Continuation keyline";}
        key.localPosition=new Vector3(0,.25f,.9f);key.localRotation=Quaternion.Euler(16.56f,0,0);key.localScale=m.keyRoots[0].localScale;
        run.keyRoot=key.gameObject;run.keyText=key.GetComponentInChildren<TMP_Text>(true);run.keyText.font=font;run.keyText.fontSharedMaterial=font.material;run.keyText.richText=false;
        run.keyPlate=key.GetComponentsInChildren<Renderer>(true).First(r=>r.GetComponent<TMP_Text>()==null);run.keyDark=m.blackPanel;run.keyLight=m.whitePanel;
        run.keyStarts=keys.Select(k=>(float)k["start"]).ToArray();run.keyEnds=keys.Select(k=>(float)k["end"]).ToArray();run.keyStrings=keys.Select(k=>(string)k["text"]).ToArray();run.keyStyles=keys.Select(k=>(int)k["style"]).ToArray();
        Material dm=CopyMaterial(font.material,"FullFilmDanmaku");dm.SetTexture("_MainTex",font.atlasTexture);dm.SetFloat("_FaceDilate",.10f);dm.SetFloat("_OutlineWidth",.22f);dm.SetColor("_OutlineColor",new Color(0,0,0,.9f));
        var roots=new List<Transform>();var texts=new List<TMP_Text>();
        for(int i=0;i<16;i++)
        {
            Transform item=textRoot.Find("Comment "+i);
            if(item==null){item=Object.Instantiate(m.commentRoots[0].transform,textRoot);item.name="Comment "+i;}
            var t=item.GetComponentInChildren<TMP_Text>(true);t.font=font;t.fontSharedMaterial=dm;t.fontSize=1.65f;t.text="";t.color=new Color(.85f,.92f,1,.78f);t.richText=false;t.enableWordWrapping=false;
            item.localRotation=Quaternion.identity;roots.Add(item);texts.Add(t);
        }
        run.commentRoots=roots.ToArray();run.commentTexts=texts.ToArray();run.commentTimes=comments.Select(c=>(float)c["time"]).ToArray();run.commentDurations=comments.Select(c=>(float)c["duration"]).ToArray();run.commentStrings=comments.Select(c=>(string)c["text"]).ToArray();
        run.sceneStarts=data["scene_starts"].Select(t=>(float)t).ToArray();
    }
    static void SetupHistory(OWFullPresentation run,JObject data)
    {
        var entries=data["history_anchors"] as JArray;if(entries==null)return;
        Folder(Root+"/History/Materials");var panels=new List<GameObject>();
        Transform container=run.continuation.transform.Find("Historical echoes");
        if(container==null){container=new GameObject("Historical echoes").transform;container.SetParent(run.continuation.transform,false);}
        for(int i=0;i<entries.Count;i++)
        {
            var row=entries[i];string name="Historical echo "+i;var old=container.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(name); // Initialize TMP while active before parenting under the waiting root.
            string path=(string)row["path"];var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(tex==null)throw new Exception("Historical image missing: "+path);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(path.StartsWith(Root+"/History/")&&importer!=null){importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();}
            string mp=Root+"/History/Materials/Echo "+i+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(mat==null){mat=new Material(Shader.Find("Unlit/Texture"));AssetDatabase.CreateAsset(mat,mp);}mat.mainTexture=tex;
            float width=(float)row["width"],height=width*tex.height/tex.width;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Original title and image";quad.transform.SetParent(root.transform,false);quad.transform.localScale=new Vector3(width,height,1);quad.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(quad.GetComponent<Collider>());
            var label=new GameObject("Year").AddComponent<TextMeshPro>();label.font=run.keyText.font;label.fontSharedMaterial=run.keyText.font.material;label.text=(string)row["caption"];label.fontSize=.62f;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,.3f);label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,-height*.5f-.22f,-.015f);label.color=new Color(.85f,.92f,1f);
            root.transform.SetParent(container,false);root.transform.localPosition=new Vector3((float)row["position"][0],(float)row["position"][1],(float)row["position"][2]);root.SetActive(false);panels.Add(root);
        }
        run.historyPanels=panels.ToArray();run.historyStarts=entries.Select(e=>(float)e["start"]).ToArray();run.historyEnds=entries.Select(e=>(float)e["end"]).ToArray();
    }
    static void SetupFoley(JObject data)
    {
        var f=F<OWV4Foley>();var events=(JArray)data["foley_events"];var extra=(JArray)data["new_emitters"];
        Transform parent=f.transform.Find("FullFilm physical emitters");if(parent==null){parent=new GameObject("FullFilm physical emitters").transform;parent.SetParent(f.transform,false);}
        var emitters=f.emitters.Take(8).ToList();
        for(int i=0;i<extra.Count;i++)
        {
            Transform item=parent.Find("Workstation "+i);if(item==null){item=new GameObject("Workstation "+i).transform;item.SetParent(parent,false);}
            item.position=new Vector3((float)extra[i][0],(float)extra[i][1],(float)extra[i][2]);emitters.Add(item);
        }
        f.emitters=emitters.ToArray();f.eventTimes=events.Select(e=>(float)e["time"]).ToArray();f.eventClips=events.Select(e=>(int)e["clip"]).ToArray();f.eventEmitters=events.Select(e=>(int)e["emitter"]).ToArray();Sync(f);
    }
    public static object ExtendNoteRows()
    {
        Guard();var source=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-v5-tracker.json"));
        var notes=source["channels"].Select(ch=>ch["events"].Select(e=>(int)e[0]*.08f).ToArray()).ToArray();
        var tracker=F<OWV5Tracker>();tracker.noteTimes0=notes[0];tracker.noteTimes1=notes[1];tracker.noteTimes2=notes[2];tracker.noteTimes3=notes[3];
        var music=F<OWEpisodeMusic>();music.notes0=notes[0];music.notes1=notes[1];music.notes2=notes[2];music.notes3=notes[3];Sync(tracker);Sync(music);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("full-note-rows.json",new{success=true,counts=notes.Select(n=>n.Length).ToArray(),lastTimes=notes.Select(n=>n.Last()).ToArray(),source="Complete V5 3200-row event table; front-only noteOnTimes replaced"});
    }
    public static object Build()
    {
        Guard();var p=F<OWV4Presentation>();var data=Data();var go=GameObject.Find("FullFilm Playback");if(go==null)go=new GameObject("FullFilm Playback");
        var run=go.GetComponent<OWFullPresentation>();if(run==null)run=go.AddUdonSharpComponent<OWFullPresentation>();
        run.director=p.director;run.legacy=p;run.signal=F<OWFullSignal>();run.overload=F<OWFullOverload>();run.renewal=F<OWFullRenewal>();
        if(run.signal==null||run.overload==null||run.renewal==null)throw new Exception("Build signal, overload and renewal modules before integration");
        // The signal driver participates in the first half, before continuation scenery is enabled.
        run.signal.transform.SetParent(run.transform,true);
        run.continuation=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="FullFilm Continuation");run.referenceEye=Camera.main.transform;
        run.legacyContents=new[]{p.cast.worldRoot.gameObject,p.montage.gameObject,p.operations.gameObject};
        run.transitWalls=p.cast.hub2017.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();run.transitWallEnabled=run.transitWalls.Select(r=>r.enabled).ToArray();
        p.director.duration=256;p.director.music.performanceEnd=256;p.director.music.endFadeSeconds=.16f;p.director.autoStart=false;p.director.continuousPerformance=true;p.director.externalStageSampler=true;
        foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true))
        {var field=b.GetType().GetField("autoSample");if(field!=null&&field.FieldType==typeof(bool))field.SetValue(b,false);}
        p.offlinePreview=true;p.cameraFx.effectMaterial=CopyMaterial(p.cameraFx.effectMaterial,"FullFilmPrimaryViewEffect");p.cameraFx.overlayRenderer.sharedMaterial=p.cameraFx.effectMaterial;
        // Darken only copied opening-shell surfaces. V5 and later Hub materials stay intact.
        var tuned=new Dictionary<Material,Material>();int n=0;
        foreach(var shell in p.journey.openingShells.Where(s=>s!=null))foreach(var renderer in shell.GetComponentsInChildren<Renderer>(true))
        {
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var original=mats[i];if(original==null)continue;
                if(!tuned.TryGetValue(original,out var mat))
                {
                    string ap=AssetDatabase.GetAssetPath(original);
                    if(ap.StartsWith(Root+"/Tuning/Opening")){mat=original;}
                    else{mat=CopyMaterial(original,"Opening surface "+n++);if(mat.HasProperty("_Color"))mat.SetColor("_Color",original.GetColor("_Color")*.42f);}
                    tuned[original]=mat;
                }
                mats[i]=mat;
            }
            renderer.sharedMaterials=mats;
        }
        SetupText(run,data);SetupHistory(run,data);SetupFoley(data);ExtendNoteRows();run.Waiting();run.offlinePreview=false;
        foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true))if(b.GetType().Name.StartsWith("OW"))Sync(b);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("integration.json",new{success=true,scene=ScenePath,duration=256,keylines=run.keyStarts.Length+8,comments=run.commentTimes.Length+75,foley=F<OWV4Foley>().eventTimes.Length,modules=new[]{run.overload.GetType().Name,run.renewal.GetType().Name,run.signal.GetType().Name},transitRenderers=run.transitWalls.Length,openingMaterials=tuned.Count});
    }
    public static void Pose(float time,bool reset=false)
    {
        var run=F<OWFullPresentation>();run.Preview(time);
        string[] names={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
        foreach(var actor in Object.FindObjectsOfType<OWActorRig>(true))
        {
            var a=actor.animator;if(a==null||!a.gameObject.activeInHierarchy||!a.enabled||a.runtimeAnimatorController==null)continue;
            int at=a.GetInteger("V4Pose");a.Play(names[Mathf.Clamp(at,0,11)],0,a.GetFloat("V4PoseTime"));a.Update(0);
        }
        run.Preview(time);Canvas.ForceUpdateCanvases();foreach(var t in Object.FindObjectsOfType<TMP_Text>())if(t.havePropertiesChanged)t.ForceMeshUpdate(false,false);
    }
    public static object Save()
    {
        Guard();var run=F<OWFullPresentation>();run.Waiting();run.offlinePreview=false;
        foreach(var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true))if(b.GetType().Name.StartsWith("OW"))Sync(b);
        foreach(var t in Object.FindObjectsOfType<Transform>(true))if(PrefabUtility.IsPartOfPrefabInstance(t))
        {PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);foreach(var c in t.GetComponents<Component>())if(c!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return Report("saved.json",new{success=true,scene=ScenePath,sha256=Hash(ScenePath),v5Sha256=Hash(V5),duration=run.director.duration,waiting=!run.continuation.activeSelf&&run.legacy.menuRoot.activeInHierarchy});
    }
    public static object StaticChecks()
    {
        Guard();var run=F<OWFullPresentation>();var rows=new List<object>();var observed=new HashSet<int>();var forbidden=new[]{"Agent","审核","本项目","原创演示","示意图","序列号","keygen","Generate"};float drift=0,anchor=0;
        try
        {
            foreach(float time in Enumerable.Range(0,200).Select(i=>i*1.28f+.04f).Concat(new[]{87.28f,133.12f,150f,189.60f,203f,220f,234f,240f,252f,255.96f}))
            {
                Pose(time);observed.Add(run.sampledScene);
                var sources=run.director.music.sources;
                for(int ch=0;ch<4;ch++)anchor=Mathf.Max(anchor,Vector3.Distance(sources[ch].transform.position,run.signal.SourceWorldPosition(time,ch)));
                foreach(var text in Object.FindObjectsOfType<TMP_Text>())if(text.gameObject.activeInHierarchy&&forbidden.Any(s=>text.text.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0))throw new Exception("Unexpected performed text: "+text.text);
                if(time>=133.12f&&run.legacyContents.Any(g=>g.activeInHierarchy))throw new Exception("Legacy scenery leaked into continuation at "+time);
                if(time>=133.12f)rows.Add(new{time,scene=run.sampledScene,key=run.sampledKey,comments=run.activeComments,renderers=run.continuation.GetComponentsInChildren<Renderer>().Count(r=>r.enabled)});
            }
            foreach(var span in new[]{new[]{79.36f,81.92f},new[]{86.4f,88.32f},new[]{102.4f,104.96f},new[]{217.6f,222.72f}})
            for(int ch=0;ch<4;ch++)
            {
                Vector3 baseline=run.signal.SourceWorldPosition(span[0],ch);
                for(float t=span[0];t<span[1];t+=1f/240f)drift=Mathf.Max(drift,Vector3.Distance(baseline,run.signal.SourceWorldPosition(t,ch)));
            }
            if(observed.Count!=24||anchor>.001f||drift>.0001f)throw new Exception("Full-film invariants failed: scenes="+observed.Count+" anchor="+anchor+" drift="+drift);
            return Report("static-validation.json",new{success=true,scenes=observed.OrderBy(i=>i).ToArray(),maxSourceAnchorError=anchor,maxTravelSourceDrift=drift,rows,scope="Saved-scene C# sampling, not live Udon/HMD/multiplayer"});
        }
        catch(Exception e){Report("static-failure.json",new{success=false,error=e.ToString(),rows});throw;}
        finally{run.Waiting();run.offlinePreview=false;}
    }
    static void Frame(Camera camera,string path,int width=1600,int height=900)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,path.EndsWith(".jpg")?image.EncodeToJPG(92):image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
    }
    static readonly float[] CaptureTimes={.96f,5.6f,23.52f,34.56f,44.16f,55.36f,66.4f,74.88f,85.12f,87.28f,89.6f,105.6f,118.8f,129.28f,134.4f,137.6f,141.44f,145.92f,150.4f,155.2f,160.64f,164.48f,170.24f,174.72f,179.2f,183.68f,188.16f,192f,195.84f,199.68f,203.52f,209.28f,213.76f,216.32f,218.88f,221.44f,224f,227.84f,232.32f,235.52f,239.36f,243.2f,248.32f,252.16f,255.36f};
    public static object CaptureAll()=>CaptureSome("frames",CaptureTimes);
    static Color32[] Pixels(Camera c)
    {
        var old=c.targetTexture;var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,675,24);var texture=new Texture2D(1200,675,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,675),0,0);texture.Apply();return texture.GetPixels32();}
        finally{c.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);}
    }
    static bool Changed(Color32 a,Color32 b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))>24;
    public static object KeylineChecks()
    {
        Guard();var run=F<OWFullPresentation>();var m=run.legacy.montage;var rows=new List<object>();float minimum=1;
        try
        {
            for(int key=0;key<m.keyStarts.Length+run.keyStarts.Length;key++)
            {
                bool front=key<m.keyStarts.Length;int index=front?key:key-m.keyStarts.Length;
                float start=front?m.keyStarts[index]:run.keyStarts[index],end=front?m.keyEnds[index]:run.keyEnds[index];
                foreach(float time in new[]{start+.4f,(start+end)*.5f,end-.3f})
                {
                    Pose(time);Transform root=front?m.keyRoots[m.keyRegions[index]]:run.keyRoot.transform;
                    var text=(front?m.keyTexts[m.keyRegions[index]]:run.keyText).GetComponent<Renderer>();
                    var renderers=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy).ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();int actual=0,potential=0;
                    try
                    {
                        var full=Pixels(Camera.main);text.enabled=false;var without=Pixels(Camera.main);text.enabled=true;
                        foreach(var r in renderers)if(!r.transform.IsChildOf(root)&&!r.transform.IsChildOf(run.legacy.overlayRoot.transform))r.enabled=false;
                        var only=Pixels(Camera.main);text.enabled=false;var plate=Pixels(Camera.main);
                        for(int i=0;i<only.Length;i++)if(Changed(only[i],plate[i])){potential++;if(Changed(full[i],without[i]))actual++;}
                    }
                    finally{for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=enabled[i];text.enabled=true;}
                    float ratio=potential==0?0:actual/(float)potential;minimum=Mathf.Min(minimum,ratio);rows.Add(new{key,time,actual,potential,visibleRatio=ratio});
                    if(potential<100||ratio<.97f)throw new Exception("Keyline is obscured at "+time+": "+ratio);
                }
            }
            return Report("keyline-visibility.json",new{success=true,samples=rows.Count,minVisibleGlyphPixelRatio=minimum,rows,scope="Fixed reference eye render difference; not HMD validation"});
        }
        catch(Exception e){Report("keyline-failure.json",new{success=false,error=e.ToString(),rows});throw;}
        finally{run.Waiting();run.offlinePreview=false;}
    }
    public static object CaptureSome(string label,float[] times)
    {
        Guard();var run=F<OWFullPresentation>();var rows=new List<object>();
        try
        {
            run.Waiting();Frame(Camera.main,Tmp+"/"+label+"/full_waiting.png");
            foreach(float t in times){Pose(t,true);string path=Tmp+"/"+label+"/full_"+t.ToString("000.00",System.Globalization.CultureInfo.InvariantCulture)+".png";Frame(Camera.main,path);rows.Add(new{time=t,path});}
            run.Finished();Frame(Camera.main,Tmp+"/"+label+"/full_finished.png");
            return Report("capture-"+label+".json",new{success=true,rows});
        }
        finally{run.Waiting();run.offlinePreview=false;}
    }
    static int movieFrame;static RenderTexture movieRt;static Texture2D movieImage;static Camera movieCamera;static bool movieRunning;
    public static object StartMovie()
    {
        Guard();if(SessionState.GetBool("FullFilmMovie",false))throw new Exception("Movie already running");Directory.CreateDirectory(Tmp+"/movie-frames");
        movieCamera=Camera.main;movieRt=new RenderTexture(1600,900,24);movieRt.Create();movieImage=new Texture2D(1600,900,TextureFormat.RGB24,false);movieFrame=0;movieRunning=true;
        SessionState.SetBool("FullFilmMovie",true);SessionState.SetInt("FullFilmFrame",0);EditorApplication.update+=MovieTick;return new{started=true,frames=Frames,fps=25};
    }
    static void MovieTick()
    {
        if(!movieRunning)return;
        try
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<6&&movieFrame<Frames;i++)
            {
                float time=movieFrame/25f;if(time>=256)F<OWFullPresentation>().Finished();else Pose(time);
                var previous=movieCamera.targetTexture;var active=RenderTexture.active;
                try{movieCamera.targetTexture=movieRt;movieCamera.Render();RenderTexture.active=movieRt;movieImage.ReadPixels(new Rect(0,0,1600,900),0,0);movieImage.Apply();File.WriteAllBytes(Tmp+"/movie-frames/frame_"+movieFrame.ToString("00000")+".jpg",movieImage.EncodeToJPG(92));}
                finally{movieCamera.targetTexture=previous;RenderTexture.active=active;}
                movieFrame++;SessionState.SetInt("FullFilmFrame",movieFrame);if(watch.ElapsedMilliseconds>125)break;
            }
            if(movieFrame%25<6)Report("movie-progress.json",new{frame=movieFrame,total=Frames,complete=false,error=(string)null});
            if(movieFrame>=Frames)EndMovie(null);
        }
        catch(Exception e){EndMovie(e.ToString());}
    }
    static void EndMovie(string error)
    {
        movieRunning=false;EditorApplication.update-=MovieTick;SessionState.SetBool("FullFilmMovie",false);movieRt.Release();Object.DestroyImmediate(movieRt);Object.DestroyImmediate(movieImage);
        F<OWFullPresentation>().Waiting();F<OWFullPresentation>().offlinePreview=false;Report("movie-progress.json",new{frame=movieFrame,total=Frames,complete=movieFrame==Frames&&error==null,error});
    }
    public static object MovieStatus()=>new{running=SessionState.GetBool("FullFilmMovie",false),frame=SessionState.GetInt("FullFilmFrame",0),total=Frames};
    static void AutoTick(bool enabled)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand",false)).First(t=>t!=null);
        type.GetMethod("SetAutoTick",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{enabled,16,false});
    }
    public static object ReadSavedState()
    {
        Guard();var scene=SceneManager.GetActiveScene();var run=F<OWFullPresentation>();
        bool waiting=!run.offlinePreview&&!run.continuation.activeInHierarchy&&run.legacy.menuRoot.activeInHierarchy;
        if(scene.isDirty||!waiting)throw new Exception("An already saved, clean waiting scene is required");
        return Report("saved.json",new{success=true,scene=ScenePath,sha256=Hash(ScenePath),v5Sha256=Hash(V5),duration=run.director.duration,waiting,operation="Readback of the SDK-saved scene without resampling or rewriting proxy/material caches"});
    }
    public static Task<object> SdkBuildOnly()=>BuildSdk(true);
    public static Task<object> SdkBuildPrepared()=>BuildSdk(false);
    static async Task<object> BuildSdk(bool prepare)
    {
        if(prepare)Save();else ReadSavedState();string inputSceneSha256=Hash(ScenePath);Directory.CreateDirectory(Tmp+"/scene-snapshots");File.Copy(ScenePath,Tmp+"/scene-snapshots/sdk-input-"+inputSceneSha256+".unity",true);var panel=EditorWindow.GetWindow<VRCSdkControlPanel>();panel.Show();await Task.Delay(500);
        if(!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var api))throw new Exception("SDK builder unavailable");
        var builder=(VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder)api;builder.RegisterBuilder(panel);if(!builder.IsValidBuilder(out string why))throw new Exception(why);builder.Initialize();
        try{AutoTick(false);string built=await api.Build();string path=Tmp+"/OWM_FullFilm_v1.vrcw";File.Copy(built,path,true);string outputSceneSha256=Hash(ScenePath);File.Copy(ScenePath,Tmp+"/scene-snapshots/sdk-output-"+outputSceneSha256+".unity",true);return Report("sdk-build.json",new{success=true,path,bytes=new FileInfo(path).Length,sha256=Hash(path),scene=ScenePath,inputSceneSha256,outputSceneSha256,uploaded=false});}
        catch(Exception e){Report("sdk-build.json",new{success=false,error=e.ToString()});throw;}
        finally{AutoTick(true);}
    }
    public static object InspectBundle()
    {
        string path=Tmp+"/OWM_FullFilm_v1.vrcw";var bundle=AssetBundle.LoadFromFile(path);if(bundle==null)throw new Exception("Bundle load failed");
        try{var scenes=bundle.GetAllScenePaths();bool success=bundle.isStreamedSceneAssetBundle&&scenes.Length==1&&scenes[0].EndsWith("OWM_FullFilm_v1.unity");if(!success)throw new Exception("Wrong bundle scene table");return Report("sdk-bundle-inspection.json",new{success,scenes,sha256=Hash(path),scope="Bundle load and single-scene table; not client launch"});}finally{bundle.Unload(false);}
    }
    public static object ReopenCheck()
    {
        Guard();if(SceneManager.GetActiveScene().isDirty)throw new Exception("Save scene before reopen check");string before=Hash(ScenePath);string v5=Hash(V5);
        bool preserved=Directory.GetFiles(Tmp+"/baseline","v5-*.unity").Any(p=>Hash(p)==v5);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var run=F<OWFullPresentation>();
        bool waiting=run!=null&&!run.offlinePreview&&!run.continuation.activeInHierarchy&&run.legacy.menuRoot.activeInHierarchy;
        bool programs=Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b=>b.GetType().Name.StartsWith("OWFull")).All(b=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b)?.programSource!=null);
        bool success=waiting&&programs&&preserved&&before==Hash(ScenePath)&&Mathf.Abs(run.director.duration-256)<.001f;
        var report=Report("persistence.json",new{success,waiting,programs,v5Preserved=preserved,v5Sha256=v5,sceneSha256=before});if(!success)throw new Exception(report);return report;
    }
    public static object FinalState()
    {
        Guard();var scene=SceneManager.GetActiveScene();var run=F<OWFullPresentation>();
        bool waiting=run!=null&&!run.offlinePreview&&!run.continuation.activeInHierarchy&&run.legacy.menuRoot.activeInHierarchy;
        bool udonErrors=UdonSharpProgramAsset.AnyUdonSharpScriptHasError();
        var shaders=Object.FindObjectsOfType<Renderer>(true).Where(r=>r.gameObject.scene==scene).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.shader).Where(s=>s!=null).Distinct().ToArray();
        var errors=shaders.SelectMany(s=>ShaderUtil.GetShaderMessages(s).Where(m=>m.severity.ToString()=="Error").Select(m=>s.name+": "+m.message)).ToArray();
        bool success=!scene.isDirty&&waiting&&!udonErrors&&errors.Length==0;
        var report=Report("final-editor-state.json",new{success,scene=scene.path,playStopped=!EditorApplication.isPlaying,compiling=EditorApplication.isCompiling,sceneSaved=!scene.isDirty,waiting,udonCompileErrors=udonErrors,shaders=shaders.Length,shaderErrors=errors,sceneSha256=Hash(ScenePath),v5Sha256=Hash(V5),scope="Stopped, saved Editor and referenced-shader check; not device or network certification"});
        if(!success)throw new Exception(report);return report;
    }
    public static object DynamicMaterialsProof()
    {
        Guard();var descriptor=F<VRC.SDKBase.VRC_SceneDescriptor>();if(descriptor==null)throw new Exception("Scene descriptor missing");
        var field=descriptor.GetType().GetField("DynamicMaterials",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
        var materials=((System.Collections.Generic.IEnumerable<Material>)field.GetValue(descriptor)).ToArray();
        var packages=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        string worldsVersion=packages.First(p=>p.name=="com.vrchat.worlds").version;
        string baseVersion=packages.First(p=>p.name=="com.vrchat.base").version;
        var rows=materials.Select(m=>new{guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)),name=m.name,path=AssetDatabase.GetAssetPath(m)}).ToArray();
        string type=descriptor.GetType().FullName;ulong fileId=GlobalObjectId.GetGlobalObjectIdSlow(descriptor).targetObjectId;
        bool uniqueNames=rows.Select(m=>m.name).Distinct(StringComparer.Ordinal).Count()==rows.Length;
        bool success=type=="VRC.SDK3.Components.VRCSceneDescriptor"&&worldsVersion=="3.10.5"&&baseVersion=="3.10.5"&&materials.Length==13&&uniqueNames&&rows.Select(m=>m.guid).Distinct().Count()==13&&rows.All(m=>!string.IsNullOrEmpty(m.guid)&&!string.IsNullOrEmpty(m.name));
        string report=Report("sdk-dynamic-materials.json",new{success,sourceSceneSha256=Hash(ScenePath),descriptorType=type,descriptorLocalFileId=fileId.ToString(),worldsVersion,baseVersion,uniqueNames,materials=rows,scope="Read-only current saved SceneDescriptor references and native package versions; matches the actual SDK output snapshot. Only these thirteen unique names are covered."});
        if(!success)throw new Exception(report);return report;
    }
}
