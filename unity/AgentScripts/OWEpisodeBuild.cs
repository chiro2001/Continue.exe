using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OldWebMap;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static class OWEpisodeBuild
{
    const string Root="Assets/OldWebMap/Episode123";
    const string ScenePath="Assets/Scenes/OWM_Episode123.unity";
    static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
    static readonly List<Transform> Animated=new List<Transform>();
    static readonly List<float> On=new List<float>(),Off=new List<float>(),Begin=new List<float>(),Length=new List<float>(),Phase=new List<float>();
    static readonly List<Vector3> From=new List<Vector3>(),To=new List<Vector3>(),ScaleFrom=new List<Vector3>(),ScaleTo=new List<Vector3>(),Wobble=new List<Vector3>();
    static TMP_FontAsset Font;
    static OWEpisodeMusic CaptureMusic;
    static OWEpisodeStage MovieStage;
    static Camera MovieCamera;
    static RenderTexture MovieTarget;
    static Texture2D MovieImage;
    static RenderTexture MoviePreviousTarget;
    static string MovieDirectory;
    static int MovieFrame;
    const int MovieFps=25;
    const int MovieFrames=3376;
    static JArray Cues;
    static Transform World;

    static Color C(string hex) { Color c;ColorUtility.TryParseHtmlString(hex,out c);return c; }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
    static GameObject Child(string name,Transform parent)
    {
        var g=new GameObject(name);if(parent!=null)g.transform.SetParent(parent,false);return g;
    }
    static Material Mat(string name,string color,float glow=0)
    {
        Material mat;if(Mats.TryGetValue(name,out mat))return mat;
        string path=Root+"/Materials/"+name+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.name=name;mat.color=C(color);mat.enableInstancing=true;mat.SetFloat("_Glossiness",.15f);
        if(glow>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",C(color)*glow);}
        else mat.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);Mats[name]=mat;return mat;
    }
    static Material PageMat(string id)
    {
        string key="page_"+id;Material mat;if(Mats.TryGetValue(key,out mat))return mat;
        string path=Root+"/Materials/"+key+".mat";mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("OldWebMap/Page Surface");if(shader==null)throw new InvalidOperationException("Page shader missing");
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;
        mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/"+id+".png");
        if(mat.mainTexture==null)throw new InvalidOperationException("Missing artwork "+id);
        mat.SetFloat("_Exposure",1);mat.SetFloat("_Cutoff",.02f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);Mats[key]=mat;return mat;
    }
    static GameObject Prim(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material mat,bool collide=false)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;
        g.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collide)Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static Transform Page(string id,Transform parent,Vector3 pos,float width,float yaw=0,bool framed=true)
    {
        var holder=Child(id,parent).transform;holder.localPosition=pos;holder.localRotation=Quaternion.Euler(0,yaw,0);
        var mat=PageMat(id);var tex=mat.mainTexture;float height=width*tex.height/tex.width;
        Prim("Authored image",PrimitiveType.Quad,holder,Vector3.zero,new Vector3(width,height,1),mat);
        if(framed)Prim("Page thickness",PrimitiveType.Cube,holder,new Vector3(0,0,.037f),new Vector3(width+.045f,height+.045f,.07f),Mat("FrameBone","#c5c8bd"));
        return holder;
    }
    static TMP_Text Label(string name,string text,Transform parent,Vector3 pos,float width,float size,string color="#eee9da")
    {
        var t=Child(name,parent).AddComponent<TextMeshPro>();t.font=Font;t.text=text;t.fontSize=size;
        t.fontSize=size*10f;t.alignment=TextAlignmentOptions.Center;t.enableWordWrapping=true;t.rectTransform.sizeDelta=new Vector2(width,1f);
        t.transform.localPosition=pos;t.color=C(color);return t;
    }
    static void Move(Transform t,float on,float off,Vector3 from,Vector3 to,float begin,float length,Vector3? fromScale=null,Vector3? toScale=null,Vector3? wobble=null)
    {
        Animated.Add(t);On.Add(on);Off.Add(off);From.Add(from);To.Add(to);Begin.Add(begin);Length.Add(length);
        ScaleFrom.Add(fromScale??Vector3.one);ScaleTo.Add(toScale??Vector3.one);Wobble.Add(wobble??Vector3.zero);Phase.Add(Animated.Count*.71f);
    }

    public static object ImportAssets()
    {
        Folder(Root+"/Materials");Folder(Root+"/Motion");Folder(Root+"/Programs");Folder(Root+"/Fonts");
        var art=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-art.json"));
        foreach(var entry in (JArray)art["assets"])
        {
            string p=Root+"/Art/"+(string)entry["id"]+".png";
            var importer=AssetImporter.GetAtPath(p) as TextureImporter;if(importer==null)throw new InvalidOperationException("No texture importer "+p);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.isReadable=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.filterMode=(string)entry["filter"]=="Point"?FilterMode.Point:FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=1024;importer.SaveAndReimport();
        }
        for(int i=1;i<=4;i++)
        {
            string p=Root+"/Audio/FC14_Mono_"+i.ToString("00")+".wav";var importer=AssetImporter.GetAtPath(p) as AudioImporter;
            if(importer==null)throw new InvalidOperationException("No audio importer "+p);
            importer.forceToMono=false;importer.loadInBackground=false;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.PCM;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        string fontPath=Root+"/Fonts/EpisodeChinese.asset";Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if(Font==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
            if(source==null)throw new InvalidOperationException("Chinese source font missing");
            Font=TMP_FontAsset.CreateFontAsset(source,70,7,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            Font.name="EpisodeChinese";Font.isMultiAtlasTexturesEnabled=true;AssetDatabase.CreateAsset(Font,fontPath);
            AssetDatabase.AddObjectToAsset(Font.material,Font);
            foreach(var atlas in Font.atlasTextures)if(atlas!=null)AssetDatabase.AddObjectToAsset(atlas,Font);
        }
        string required="0123456789: /ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz接入繁荣条件改变播放暂停重播从头开始第段作者使用说明制作贡献下载上传共同世界近看原图资料历史界面按年代重建原创示例文字图片校对字幕读取工作台安全更新便利维护承担欢迎请选择继续预览结束这些普通的人让作品可以被看懂使用和传递新的活动正在发生";
        string newCharacters=new string(required.Where(c=>!Font.HasCharacter(c)).ToArray());
        if(newCharacters.Length>0)Font.TryAddCharacters(newCharacters,out string unusedMissing);
        string missing=new string(required.Where(c=>!Font.HasCharacter(c)).ToArray());
        EditorUtility.SetDirty(Font);AssetDatabase.SaveAssets();
        return JsonConvert.SerializeObject(new{art=((JArray)art["assets"]).Count,audio=4,font=fontPath,missing});
    }

    public static object ImportFoley()
    {
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-foley.json"));
        foreach(string filename in data["files"].ToObject<string[]>())
        {
            string p=Root+"/Audio/"+filename;var importer=AssetImporter.GetAtPath(p) as AudioImporter;
            if(importer==null)throw new InvalidOperationException("Foley importer missing "+p);
            importer.forceToMono=false;importer.loadInBackground=false;var settings=importer.defaultSampleSettings;
            settings.compressionFormat=AudioCompressionFormat.PCM;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.preloadAudioData=true;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        return "Five original non-vocal Foley clips imported";
    }

    public static object CheckArtPixels()
    {
        var doc=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-art.json"));var rows=new List<object>();
        foreach(var entry in (JArray)doc["assets"])
        {
            string id=(string)entry["id"],p=Root+"/Art/"+id+".png";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(p);var importer=(TextureImporter)AssetImporter.GetAtPath(p);
            bool exact=tex.width==(int)entry["width"] && tex.height==(int)entry["height"];
            rows.Add(new{id,tex.width,tex.height,exact,npot=importer.npotScale.ToString(),filter=importer.filterMode.ToString()});
            if(!exact)throw new InvalidOperationException("Artwork aspect/pixel grid changed during import: "+id);
        }
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");
        File.WriteAllText(Path.Combine(dir,"art-import-check.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));
        return JsonConvert.SerializeObject(new{success=true,assets=rows.Count,allOriginalPixelDimensionsPreserved=true});
    }

    public static object RepairPrograms()
    {
        Folder(Root+"/Programs");
        foreach(string file in new[]{"OWEpisodeDirector","OWEpisodeMusic","OWEpisodeStage","OWEpisodeButton","OWEpisodeFoley"})
        {
            string source="Assets/OldWebMap/Scripts/"+file+".cs";
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(source);if(script==null)throw new InvalidOperationException("Missing script "+source);
            var existing=AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).FirstOrDefault(a=>a!=null && a.sourceCsScript==script);
            if(existing!=null)continue;
            var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;
            AssetDatabase.CreateAsset(program,Root+"/Programs/"+file+".asset");
        }
        AssetDatabase.SaveAssets();UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new InvalidOperationException("Udon compilation failed");
        return "Episode programs compiled";
    }

    static AnimatorController Motions()
    {
        string p=Root+"/Motion/EpisodeMotions.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(p);
        if(!controller.parameters.Any(v=>v.name=="Pose"))controller.AddParameter("Pose",AnimatorControllerParameterType.Int);
        if(!controller.parameters.Any(v=>v.name=="PoseTime"))controller.AddParameter("PoseTime",AnimatorControllerParameterType.Float);
        var reference=PrefabUtility.LoadPrefabContents("Assets/OldWebActors/Imported/Kipfel/Kipfel.prefab");
        var pose=new HumanPose();
        try { using(var handler=new HumanPoseHandler(reference.GetComponent<Animator>().avatar,reference.transform))handler.GetHumanPose(ref pose); }
        finally { PrefabUtility.UnloadPrefabContents(reference); }
        string[] names={"Idle","Type","Offer","Point","Cheer","Inspect","Walk"};
        var machine=controller.layers[0].stateMachine;
        for(int id=0;id<names.Length;id++)
        {
            string cp=Root+"/Motion/"+names[id]+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(cp);
            if(clip==null){clip=new AnimationClip{name=names[id],frameRate=30};AssetDatabase.CreateAsset(clip,cp);}
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))AnimationUtility.SetEditorCurve(clip,binding,null);
            for(int m=0;m<HumanTrait.MuscleName.Length;m++)
            {
                string muscle=HumanTrait.MuscleName[m];float rest=pose.muscles[m],peak=rest;
                if(muscle.Contains("Arm Down-Up"))rest=-.72f;
                if(muscle.Contains("Forearm Stretch"))rest=.75f;
                peak=rest;
                if(id==0 && muscle=="Head Nod Down-Up")peak=rest+.035f;
                if(id==1)
                {
                    if(muscle.Contains("Arm Down-Up")){rest=-.50f;peak=-.44f;}
                    if(muscle.Contains("Arm Front-Back")){rest=-.59f;peak=-.65f;}
                    if(muscle.Contains("Forearm Stretch")){rest=.60f;peak=.68f;}
                    if(muscle=="Head Nod Down-Up"){rest=-.12f;peak=-.16f;}
                }
                if(id==2 || id==3)
                {
                    if(muscle=="Right Arm Down-Up")peak=id==2?-.22f:.05f;
                    if(muscle=="Right Arm Front-Back")peak=-.58f;
                    if(muscle=="Right Forearm Stretch")peak=id==2?.18f:.7f;
                    if(muscle=="Head Turn Left-Right")peak=-.15f;
                }
                if(id==4)
                {
                    if(muscle.Contains("Arm Down-Up"))peak=.82f;
                    if(muscle.Contains("Forearm Stretch"))peak=.68f;
                    if(muscle=="Head Tilt Left-Right")peak=.1f;
                }
                if(id==5)
                {
                    if(muscle=="Head Nod Down-Up")peak=-.17f;
                    if(muscle=="Head Turn Left-Right")peak=.22f;
                    if(muscle=="Right Arm Down-Up")peak=-.32f;
                    if(muscle=="Right Forearm Stretch")peak=.08f;
                }
                var curve=new AnimationCurve(new Keyframe(0,rest),new Keyframe(.9f,rest),new Keyframe(1.7f,peak),new Keyframe(2.7f,peak),new Keyframe(4,rest));
                if(id==6 && (muscle.Contains("Upper Leg Front-Back") || muscle.Contains("Arm Front-Back")))
                {
                    float amplitude=muscle.Contains("Upper Leg")?.23f:.12f;float sign=muscle.StartsWith("Left")?1:-1;
                    curve=new AnimationCurve(new Keyframe(0,rest),new Keyframe(1,rest+amplitude*sign),new Keyframe(2,rest),new Keyframe(3,rest-amplitude*sign),new Keyframe(4,rest));
                }
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),muscle),curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var state=machine.states.Select(x=>x.state).FirstOrDefault(x=>x.name==names[id])??machine.AddState(names[id]);
            state.motion=clip;state.timeParameterActive=true;state.timeParameter="PoseTime";
            if(id==0)machine.defaultState=state;
            if(!machine.anyStateTransitions.Any(t=>t.destinationState==state))
            {
                var transition=machine.AddAnyStateTransition(state);transition.hasExitTime=false;transition.duration=.16f;transition.canTransitionToSelf=false;
                transition.AddCondition(AnimatorConditionMode.Equals,id,"Pose");
            }
            EditorUtility.SetDirty(clip);EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return controller;
    }

    static Renderer Desk(Transform parent,float x,string id)
    {
        var root=Child("Contact workstation",parent).transform;root.localPosition=new Vector3(x,0,2.1f);root.localRotation=Quaternion.Euler(0,x<0?-90:90,0);
        float screenX=x<0?.48f:-.48f;
        Prim("Wood desktop",PrimitiveType.Cube,root,new Vector3(0,.57f,0),new Vector3(1.75f,.07f,.68f),Mat("Wood","#a08a6d"));
        foreach(float lx in new[]{-.56f,.56f})foreach(float z in new[]{-.24f,.24f})Prim("Leg",PrimitiveType.Cube,root,new Vector3(lx,.275f,z),new Vector3(.055f,.55f,.055f),Mat("DeskMetal","#687b7f"));
        Prim("CRT beige body",PrimitiveType.Cube,root,new Vector3(screenX,.99f,.11f),new Vector3(.67f,.48f,.32f),Mat("OldPlastic","#c2c3b3"));
        Prim("CRT dark bezel",PrimitiveType.Cube,root,new Vector3(screenX,.99f,-.065f),new Vector3(.60f,.405f,.035f),Mat("ScreenBezel","#49565d"));
        var screen=Page(id,root,new Vector3(screenX,.995f,-.087f),.55f,0,false);
        Prim("Monitor stand",PrimitiveType.Cube,root,new Vector3(screenX,.685f,.12f),new Vector3(.34f,.15f,.22f),Mat("OldPlastic","#c2c3b3"));
        Prim("Keyboard case",PrimitiveType.Cube,root,new Vector3(0,.626f,-.22f),new Vector3(.46f,.025f,.17f),Mat("OldPlastic","#c2c3b3"));
        for(int j=0;j<4;j++)Prim("Key row",PrimitiveType.Cube,root,new Vector3(0,.643f,-.275f+j*.035f),new Vector3(.405f,.015f,.024f),Mat("Keycap","#e1ddcb"));
        Prim("Mouse",PrimitiveType.Capsule,root,new Vector3(.4f,.64f,-.22f),new Vector3(.075f,.027f,.11f),Mat("OldPlastic","#c2c3b3"));
        return screen.GetComponentInChildren<Renderer>();
    }

    static void PhoneModel(Transform parent,Vector3 pos,bool early)
    {
        var root=Child(early?"Early smartphone":"Feature phone",parent).transform;root.localPosition=pos;root.localRotation=Quaternion.Euler(-16,-16,0);
        Prim("Handset",PrimitiveType.Cube,root,Vector3.zero,new Vector3(.36f,.68f,.085f),Mat("PhonePlastic","#344956"));
        Page(early?"phone_early":"phone_feature",root,new Vector3(0,early?0:.10f,-.045f),early?.31f:.28f,0,false);
        if(!early)for(int y=0;y<4;y++)for(int x=0;x<3;x++)Prim("Button",PrimitiveType.Cube,root,new Vector3((x-1)*.082f,-.15f-y*.047f,-.052f),new Vector3(.066f,.029f,.016f),Mat("PhoneKeys","#b5c5c3"));
        Prim("Earpiece",PrimitiveType.Cube,root,new Vector3(0,.302f,-.051f),new Vector3(.12f,.012f,.012f),Mat("ScreenBezel","#49565d"));
    }

    static void Hub(Transform parent)
    {
        Material stone=Mat("HubStone","#cad3d4"),gold=Mat("HubGold","#ada574"),dark=Mat("HubDark","#385867");
        for(int i=0;i<12;i++)
        {
            float angle=i*Mathf.PI*2/12;var segment=Prim("Open circular walkway",PrimitiveType.Cube,parent,new Vector3(Mathf.Sin(angle)*3f,.08f,9+Mathf.Cos(angle)*3),new Vector3(1.6f,.18f,.75f),stone);
            segment.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
        }
        foreach(float x in new[]{-1.4f,1.4f})
        {
            Prim("Hub entrance pillar",PrimitiveType.Cube,parent,new Vector3(x,1.4f,7.3f),new Vector3(.30f,2.8f,.4f),stone);
            for(int j=0;j<4;j++)Prim("Tower fragment",PrimitiveType.Cube,parent,new Vector3(x*.48f,3.4f+j*.58f,9.3f),new Vector3(.38f,.46f,.48f),stone);
        }
        Prim("Entrance header",PrimitiveType.Cube,parent,new Vector3(0,2.9f,7.3f),new Vector3(3.2f,.32f,.44f),stone);
        for(int j=0;j<3;j++)
        {
            float radius=1.3f+j*.45f;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI*2/16;var piece=Prim("Gold ring",PrimitiveType.Cube,parent,new Vector3(Mathf.Sin(angle)*radius,2.4f+j*.5f,9.1f+Mathf.Cos(angle)*radius),new Vector3(.56f,.12f,.16f),gold);
                piece.transform.localRotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
            }
        }
        foreach(float x in new[]{-3.7f,3.7f})
        {
            Prim("Hub sign",PrimitiveType.Cube,parent,new Vector3(x,1.85f,7.8f),new Vector3(1.55f,2.1f,.10f),gold);
            Prim("Sign face",PrimitiveType.Cube,parent,new Vector3(x,1.85f,7.73f),new Vector3(1.35f,1.83f,.03f),dark);
            Label("Hall name",x<0?"BE NICE\n\n♡\n\nA public place\nfor everyone":"TRAVEL MAPS\n\nMAP\n\nFind a world\ntogether",parent,new Vector3(x,1.9f,7.69f),1.24f,.17f);
        }
        Label("Historical scene identity","2017  NEW HUB\n按官方公告配图自绘重建的空间片段",parent,new Vector3(0,3.65f,7.1f),5,.22f);
    }

    static void Compose(int index,Transform root,float start,float end)
    {
        if(index==0)
        {
            Prim("Shared CRT carrier",PrimitiveType.Cube,root,new Vector3(0,2.16f,5.95f),new Vector3(3.75f,2.83f,.62f),Mat("OldPlastic","#c2c3b3"));
            Page("desktop98",root,new Vector3(0,2.16f,5.61f),3.45f,0,false);
            Prim("CRT base",PrimitiveType.Cube,root,new Vector3(0,.55f,6),new Vector3(1.15f,.5f,.6f),Mat("OldPlastic","#c2c3b3"));
            PhoneModel(root,new Vector3(2.65f,1.1f,5.4f),false);PhoneModel(root,new Vector3(3.25f,1.13f,5.7f),true);
            var xp=Page("desktop_xp",root,new Vector3(-3.4f,2.7f,7.4f),1.7f,12);
            Move(xp,4.8f,end,new Vector3(-4.8f,2.7f,9),new Vector3(-3.4f,2.7f,7.4f),4.8f,2.56f);
        }
        else if(index==1)
        {
            var files=Page("explorer",root,new Vector3(-1.85f,2.1f,5.85f),2.45f,8);
            var readme=Page("readme",root,new Vector3(1.15f,2.15f,5.5f),3.35f,-5);
            Move(files,start,end+1,new Vector3(-2.8f,2.1f,7.5f),new Vector3(-1.85f,2.1f,5.85f),start,1.6f);
            Move(readme,start+2.56f,end+1,new Vector3(1.15f,2.15f,7.3f),new Vector3(1.15f,2.15f,5.5f),start+2.56f,1.8f);
            Page("personal_home",root,new Vector3(3.6f,2.9f,9),1.85f,-18);
        }
        else if(index==2)
        {
            Page("world_editor",root,new Vector3(-1.35f,2.2f,5.6f),3.5f,3);
            Page("vrchat2017",root,new Vector3(2.3f,2.3f,6.8f),2.65f,-8);
            var maquette=Child("Making a place",root).transform;maquette.localPosition=new Vector3(.8f,.9f,4.9f);
            Prim("Preview platform",PrimitiveType.Cube,maquette,Vector3.zero,new Vector3(.95f,.06f,.65f),Mat("ModelPaper","#c8be9f"));
            for(int i=0;i<3;i++)Prim("Place component",PrimitiveType.Cube,maquette,new Vector3((i-1)*.3f,.15f,i*.12f),new Vector3(.16f,.25f,.16f),Mat("ModelStone","#adbec2"));
            Move(maquette,start,end+1,new Vector3(.8f,.5f,6.3f),new Vector3(.8f,.9f,4.9f),start,2.56f);
        }
        else if(index==3)
        {
            var group=Child("Layered original manga",root).transform;group.localPosition=new Vector3(0,2.1f,5.55f);
            var ink=Page("manga_ink",group,Vector3.zero,3.4f,0,false);
            var bubbles=Page("manga_bubbles",group,new Vector3(0,0,-.035f),3.4f,0,false);
            var original=Page("manga_original",group,new Vector3(0,0,-.07f),3.4f,0,false);
            var chinese=Page("manga_chinese",group,new Vector3(0,0,-.075f),3.4f,0,false);
            Move(ink,start,end+1,new Vector3(-1.1f,0,.7f),Vector3.zero,start,5.12f);
            Move(bubbles,start,end+1,new Vector3(.6f,.1f,-.8f),new Vector3(0,0,-.035f),start,5.12f);
            Move(original,start,start+5.12f,new Vector3(.65f,.1f,-.84f),new Vector3(0,0,-.07f),start,5.12f);
            Move(chinese,start+5.12f,end+1,new Vector3(.5f,0,-.55f),new Vector3(0,0,-.075f),start+5.12f,1.5f);
            Page("credits",root,new Vector3(3.5f,2.15f,7),1.75f,-12);
        }
        else if(index==4)
        {
            Page("subtitle_editor",root,new Vector3(-1.6f,2.2f,5.9f),3.2f,4);
            var scene=Child("Illustration with actual depth",root).transform;scene.localPosition=new Vector3(1.8f,2.25f,5.75f);
            var bg=Page("animation_bg",scene,new Vector3(0,0,.25f),3.05f,0,false);
            var mid=Page("animation_mid",scene,new Vector3(0,0,-.08f),3.05f,0,false);
            var fg=Page("animation_front",scene,new Vector3(0,0,-.3f),3.05f,0,false);
            var line=Page("subtitle_line",scene,new Vector3(0,-.70f,-.4f),3.05f,0,false);
            Move(mid,start,end+1,Vector3.zero,new Vector3(0,0,-.08f),start+2.56f,2.56f);
            Move(fg,start,end+1,Vector3.zero,new Vector3(0,0,-.3f),start+2.56f,2.56f);
            Move(line,start+1.28f,end+1,new Vector3(.22f,-.60f,-.4f),new Vector3(0,-.70f,-.4f),start+2.56f,2.56f);
            var playhead=Prim("Subtitle playhead",PrimitiveType.Cube,root,new Vector3(-3.02f,1.48f,5.78f),new Vector3(.012f,.42f,.008f),Mat("Playhead","#edc15b",.3f)).transform;
            Move(playhead,start,end+1,new Vector3(-3.02f,1.48f,5.78f),new Vector3(-.15f,1.48f,5.78f),start,8.96f,playhead.localScale,playhead.localScale);
        }
        else if(index==5)
        {
            Page("bt_transfer",root,new Vector3(0,2.1f,5.65f),3.6f);
            Page("release_post",root,new Vector3(-3.3f,2.45f,7.4f),2.5f,14);
            Page("credits",root,new Vector3(3.15f,2.4f,7.1f),1.85f,-13);
            string[] colors={"#78a7cc","#8dbfa8","#d5b67f","#bba6cf"};
            for(int j=0;j<64;j++)
            {
                int col=j%16,row=j/16;
                var to=new Vector3((36+col*54+22.5f-480)*3.6f/960,2.1f+(320-230-row*43-16)*3.6f/960,5.60f);
                var from=new Vector3((j%2==0?-5f:5f)+(j%3)*.3f,1.1f+(j%7)*.3f,9+j%5);
                var piece=Prim("Received piece "+j,PrimitiveType.Cube,root,to,new Vector3(.155f,.108f,.024f),Mat("Piece"+(j%4),colors[j%4])).transform;
                float enter=start+j*.19f;Move(piece,enter,end+1,from,to,enter,1.8f,piece.localScale,piece.localScale);
                if(j%4==0)
                {
                    var outPiece=Prim("Providing existing piece "+j,PrimitiveType.Cube,root,to,new Vector3(.155f,.108f,.024f),Mat("Piece"+(j%4),colors[j%4])).transform;
                    float send=start+12.8f+j*.055f;Move(outPiece,send,end+1,to,new Vector3(j%8==0?-5.8f:5.8f,2+j%5*.35f,11+j%3),send,3.2f,outPiece.localScale,outPiece.localScale);
                }
            }
            var bar=Prim("Transfer completion",PrimitiveType.Cube,root,new Vector3(-1.663f,1.361f,5.595f),new Vector3(.003f,.112f,.014f),Mat("TransferBar","#77a8c1")).transform;
            Move(bar,start,end+1,new Vector3(-1.663f,1.361f,5.595f),new Vector3(-.0525f,1.361f,5.595f),start,13.77f,new Vector3(.003f,.112f,.014f),new Vector3(3.225f,.112f,.014f));
            var done=Label("Complete percentage","100%",root,new Vector3(0,1.361f,5.575f),1,.12f,"#ffffff").transform;
            Move(done,start+13.77f,end+1,done.localPosition,done.localPosition,start+13.77f,1);
        }
        ComposeLater(index,root,start,end);
    }

    static void ComposeLater(int index,Transform root,float start,float end)
    {
        if(index==6)
        {
            string[] pages={"tianya","xiaonei","weibo"};
            for(int i=0;i<3;i++)
            {
                float a=start+i*3.41333f,b=i==2?end:start+(i+1)*3.41333f;
                var page=Page(pages[i],root,new Vector3(0,2.15f,5.65f),4.15f);
                Move(page,a,b+.12f,new Vector3(.4f,2.15f,6.1f),new Vector3(0,2.15f,5.65f),a,.6f);
                if(i==0)
                {
                    var reply=Page("tianya_reply",page,new Vector3(0,0,-.015f),4.15f,0,false);
                    Move(reply,a+1.28f,b+.12f,new Vector3(0,-.025f,-.015f),new Vector3(0,0,-.015f),a+1.28f,.3f);
                }
                Page(pages[i],root,new Vector3((i-1)*4.3f,3.2f,10.5f+i*.4f),2.1f,(i-1)*-12);
            }
        }
        else if(index==7)
        {
            Hub(root);
            Page("vrchat2017",root,new Vector3(-5.2f,2.75f,9.7f),2.2f,22);
            var greeting=Page("sticker_hello",root,new Vector3(3.45f,1.55f,5.3f),1.4f,-12,false);
            Move(greeting,start+2.56f,end+1,new Vector3(3.45f,.8f,6.5f),new Vector3(3.45f,1.55f,5.3f),start+2.56f,1f,wobble:new Vector3(0,.06f,0));
            for(int i=0;i<6;i++)
            {
                Prim("Avatar display stand",PrimitiveType.Cylinder,root,new Vector3((i-2.5f)*1.35f,.12f,10.9f),new Vector3(.85f,.1f,.85f),Mat("DisplayPlinth","#b7c9c9"));
                Page(i%2==0?"sticker_smile":"sticker_greetz",root,new Vector3((i-2.5f)*1.35f,1.05f,10.75f),.8f,0,false);
            }
        }
        else if(index==8)
        {
            Page("help_thread",root,new Vector3(0,2.1f,5.6f),4.05f);
            var error=Page("error_dialog",root,new Vector3(-2.1f,2.7f,5.15f),2.25f,-4);
            Move(error,start,start+4.5f,new Vector3(-2.1f,2.7f,5.15f),new Vector3(-3.6f,2.7f,8),start+2.56f,1.9f);
            var panda=Page("panda_icon",root,new Vector3(-3.5f,1.6f,6.1f),.9f,0,false);
            Move(panda,start,start+4.5f,panda.localPosition,panda.localPosition,start,1);
            Label("Virus history source","2007 熊猫烧香\n自绘历史示意",root,new Vector3(-3.5f,.9f,6.1f),1.8f,.15f);
            var orz=Page("sticker_orz",root,new Vector3(3.2f,2.1f,5.8f),1.7f,0,false);
            Move(orz,start,end+1,new Vector3(3.2f,1.8f,6.5f),new Vector3(3.2f,2.1f,5.8f),start+2.56f,1.5f,wobble:new Vector3(0,.07f,0));
            Page("sticker_smile",root,new Vector3(2.85f,3.2f,8),1.3f,0,false);
        }
        else if(index==9)
        {
            var access=Page("access_conditions",root,new Vector3(.7f,2.12f,5.6f),3.8f,-3);
            Move(access,start+3.84f,end+1,new Vector3(.7f,2.12f,7.2f),new Vector3(.7f,2.12f,5.6f),start+3.84f,1.28f);
            var report=Page("news_3q",root,new Vector3(0,2.2f,5.6f),3.6f,0);
            Move(report,start,end+1,new Vector3(0,2.2f,5.6f),new Vector3(-3.7f,2.4f,8.6f),start+3.84f,2.56f,Vector3.one,new Vector3(.7f,.7f,.7f));
            for(int i=0;i<3;i++)
            {
                var step=Prim("Additional condition",PrimitiveType.Cube,root,new Vector3(2.9f,1.1f+i*.34f,6.5f),new Vector3(.95f,.18f,.16f),Mat("Condition"+i,i==1?"#9db5bd":"#cfb984")).transform;
                Move(step,start+i*2.56f,end+1,new Vector3(4.8f,1.1f+i*.34f,8),new Vector3(2.9f,1.1f+i*.34f,6.5f),start+i*2.56f,1.28f,step.localScale,step.localScale);
            }
        }
        else if(index==10)
        {
            Page("eac_notice",root,new Vector3(-.85f,2.1f,5.65f),3.9f,2);
            var bear=Page("blue_bear",root,new Vector3(2.7f,2.1f,5.5f),1.25f,0,false);
            Move(bear,start,end+1,new Vector3(2.7f,2.1f,7.5f),new Vector3(2.7f,2.1f,5.5f),start,1.8f);
            foreach(float x in new[]{-4.15f,4.15f})
            {
                for(int side=-1;side<=1;side+=2)Prim("Local path pillar",PrimitiveType.Cube,root,new Vector3(x+side*.5f,1.1f,8.1f),new Vector3(.12f,2.2f,.15f),Mat("PathStone","#aebfc2"));
                Prim("Path header",PrimitiveType.Cube,root,new Vector3(x,2.2f,8.1f),new Vector3(1.14f,.15f,.15f),Mat("PathStone","#aebfc2"));
            }
            var changedPath=Prim("Changed capability path",PrimitiveType.Cube,root,new Vector3(4.15f,1.1f,8.1f),new Vector3(.9f,2,.05f),Mat("PathNotice","#718897")).transform;
            Move(changedPath,start,end+1,new Vector3(4.15f,3.4f,8.1f),new Vector3(4.15f,1.1f,8.1f),start+2.56f,2.56f,changedPath.localScale,changedPath.localScale);
            Label("Needs and response","辅助与便利功能需求\n后续公开回应",root,new Vector3(3.9f,.35f,7.8f),2.2f,.15f);
        }
        else if(index==11)
        {
            Page("maintenance_queue",root,new Vector3(0,2.15f,5.7f),4.15f);
            for(int i=0;i<9;i++)
            {
                var item=Page(i%2==0?"readme":"error_dialog",root,new Vector3(3.5f+i*.21f,1.5f+i*.2f,6.5f+i*.45f),1.25f,-12);
                Move(item,start+i*.8f,end+1,new Vector3(6.5f+i*.2f,2.5f+i*.2f,10+i*.6f),new Vector3(3.5f+i*.21f,1.5f+i*.2f,6.5f+i*.45f),start+i*.8f,2.56f);
            }
            Page("personal_home",root,new Vector3(-4.4f,2.75f,9.6f),1.75f,16);
        }
    }

    static void MemoryField(Transform parent)
    {
        string[] pictures={"personal_home","readme","xiaonei","tianya","weibo","credits","world_editor","release_post","subtitle_editor","explorer"};
        for(int i=0;i<26;i++)
        {
            float angle=(-145+i*11.6f)*Mathf.Deg2Rad;
            float radius=9+(i%5)*2.1f;
            var pos=new Vector3(Mathf.Sin(angle)*radius,.8f+(i%4)*1.45f,Mathf.Cos(angle)*radius+3);
            var page=Page(pictures[i%pictures.Length],parent,pos,2.1f+(i%3)*.6f,angle*Mathf.Rad2Deg);
            float entry=40.96f+(i%8)*2.56f;
            Move(page,entry,134,new Vector3(pos.x*1.1f,pos.y+1,pos.z+7),pos,entry,7.68f,wobble:new Vector3(.03f,.055f,.02f));
        }
        for(int i=0;i<24;i++)
        {
            float a=i*2.399963f;
            var pos=new Vector3(Mathf.Sin(a)*(8+i%5),1+i%4*.9f,Mathf.Cos(a)*(8+i%5)+5);
            string sticker=i%8==0?"sticker_jiong":i%8==4?"sticker_233":i%4==0?"sticker_greetz":"pixel_star";
            var star=Page(sticker,parent,pos,i%4==0?.85f:.08f,a*Mathf.Rad2Deg,false);
            Move(star,51.2f,134,pos,pos,51.2f,1,wobble:new Vector3(0,.07f,0));
        }
        for(int i=0;i<32;i++)
        {
            float a=i*2.399963f;float radius=7.5f+i%5*1.6f;
            var pos=new Vector3(Mathf.Sin(a)*radius,.5f+(i%6)*.64f,Mathf.Cos(a)*radius+4);
            var folder=Child("Stored contribution "+i,parent).transform;folder.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
            Prim("Folder body",PrimitiveType.Cube,folder,Vector3.zero,new Vector3(.58f,.38f,.065f),Mat("StoredPaper"+(i%3),i%3==0?"#cab677":i%3==1?"#8ab1be":"#a7b89a"));
            Prim("Folder tab",PrimitiveType.Cube,folder,new Vector3(-.15f,.215f,0),new Vector3(.23f,.085f,.065f),Mat("StoredPaper"+(i%3),"#cab677"));
            Move(folder,51.2f,134,pos+Vector3.forward*4,pos,51.2f+i%5,5.12f,wobble:new Vector3(0,.06f,0));
        }
    }

    static Transform OpeningDetails(Transform room)
    {
        var props=Child("Opening room details",room).transform;
        for(int side=0;side<2;side++)
        {
            float x=side==0?-5.8f:5.8f;var shelf=Child("Books and tools",props).transform;shelf.localPosition=new Vector3(x,0,8.9f);
            for(int row=0;row<3;row++)
            {
                Prim("Shelf",PrimitiveType.Cube,shelf,new Vector3(0,.45f+row*.55f,0),new Vector3(2.15f,.06f,.43f),Mat("ShelfWood","#806d56"));
                for(int j=0;j<7;j++)
                    Prim("Book spine",PrimitiveType.Cube,shelf,new Vector3(-.88f+j*.27f,.67f+row*.55f,-.01f),new Vector3(.13f+j%2*.05f,.32f+(j%3)*.03f,.29f),Mat("Book"+(j%4),new[]{"#798f99","#a89a75","#a98479","#8fa48b"}[j%4]));
            }
            foreach(float sx in new[]{-1.05f,1.05f})Prim("Shelf side",PrimitiveType.Cube,shelf,new Vector3(sx,.95f,0),new Vector3(.08f,1.85f,.43f),Mat("ShelfWood","#806d56"));
        }
        var plant=Child("Houseplant",props).transform;plant.localPosition=new Vector3(5.8f,.1f,4.4f);
        Prim("Plant pot",PrimitiveType.Cylinder,plant,new Vector3(0,.18f,0),new Vector3(.45f,.18f,.45f),Mat("PlantPot","#a68a76"));
        Prim("Stem",PrimitiveType.Cylinder,plant,new Vector3(0,.72f,0),new Vector3(.035f,.47f,.035f),Mat("PlantStem","#71846a"));
        for(int i=0;i<7;i++)
        {
            float a=i*2.4f;var leaf=Prim("Leaf",PrimitiveType.Sphere,plant,new Vector3(Mathf.Sin(a)*.22f,.64f+i*.07f,Mathf.Cos(a)*.22f),new Vector3(.38f,.12f,.2f),Mat("PlantLeaf","#819b78"));
            leaf.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,22);
        }
        var clock=Child("Wall clock",props).transform;clock.localPosition=new Vector3(4.4f,3.8f,10.2f);
        var face=Prim("Clock face",PrimitiveType.Cylinder,clock,Vector3.zero,new Vector3(.65f,.035f,.65f),Mat("ClockFace","#dfddc7"));face.transform.localRotation=Quaternion.Euler(90,0,0);
        var hour=Prim("Hour hand",PrimitiveType.Cube,clock,new Vector3(-.08f,.025f,-.038f),new Vector3(.17f,.018f,.012f),Mat("ClockHand","#40515b"));hour.transform.localRotation=Quaternion.Euler(0,0,-8);
        var minute=Prim("Minute hand",PrimitiveType.Cube,clock,new Vector3(.02f,.10f,-.04f),new Vector3(.014f,.23f,.012f),Mat("ClockHand","#40515b"));minute.transform.localRotation=Quaternion.Euler(0,0,-24);
        return props;
    }

    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling)throw new InvalidOperationException("Stop Play Mode and complete compilation first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("An unsaved scene is open; preserve it before building: "+SceneManager.GetSceneAt(i).path);
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)
            if(!AssetDatabase.CopyAsset("Assets/Scenes/VRCDefaultWorldScene.unity",ScenePath))throw new InvalidOperationException("Default world template missing");
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var stale=GameObject.Find("OWEpisode123");if(stale!=null)Object.DestroyImmediate(stale);
        var floor=GameObject.Find("Floor");if(floor!=null)Object.DestroyImmediate(floor);
        Mats.Clear();Animated.Clear();On.Clear();Off.Clear();From.Clear();To.Clear();Begin.Clear();Length.Clear();Phase.Clear();ScaleFrom.Clear();ScaleTo.Clear();Wobble.Clear();
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/EpisodeChinese.asset");if(Font==null)throw new InvalidOperationException("Import Episode assets first");
        string glyphs=new string(File.ReadAllText("AgentScripts/OWEpisodeBuild.cs").Where(c=>!char.IsControl(c)).Distinct().ToArray());
        string needed=new string(glyphs.Where(c=>!Font.HasCharacter(c)).ToArray());string missing="";
        if(needed.Length>0)Font.TryAddCharacters(needed,out missing);
        foreach(var atlas in Font.atlasTextures)if(atlas!=null && !AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,Font);
        var controller=Motions();
        Cues=new JArray(((JArray)JObject.Parse(File.ReadAllText("AgentScripts/data/scene-score.json"))["scenes"]).Where(c=>(float)c["end"]<=133.1201f));
        string[] expected={"connect-carriers","connect-tool","connect-worldmaking","create-manga","create-subtitle","create-distribute","create-public-life","create-bodies-worlds","create-humour-help","constrain-access","constrain-security","constrain-maintenance"};
        if(!Cues.Select(c=>(string)c["id"]).SequenceEqual(expected))throw new InvalidOperationException("Scene score order changed; update composition mapping before building");
        var owner=Child("OWEpisode123",null).transform;
        World=Child("MovingScenery",owner).transform;
        var stable=Child("StablePeopleAndContact",owner).transform;
        var systems=Child("ShowSystems",owner).transform;
        var stage=systems.gameObject.AddUdonSharpComponent<OWEpisodeStage>();
        var director=Child("SharedDirector",systems).AddUdonSharpComponent<OWEpisodeDirector>();
        var music=Child("FrontalMusic",systems).AddUdonSharpComponent<OWEpisodeMusic>();
        director.stage=stage;director.music=music;director.autoStart=false;director.duration=133.12f;
        stage.cueRoots=new Transform[Cues.Count];stage.starts=new float[Cues.Count];stage.ends=new float[Cues.Count];stage.rootPositions=new Vector3[Cues.Count];stage.rootScales=new float[Cues.Count];stage.travelOffsets=new Vector3[Cues.Count];
        stage.captions=new[]{"这些熟悉的界面背后，有人在做事。","工具和说明，传到了另一个人手里。","一个能进入的空间，也在被人一点点做出来。","有人把它译成了，我们读得懂的样子。","把一句话，放到准确的位置。","下载完成以后，仍有人留着灯。","写下、回应、记录，平常的生活也有去处。","不同的身体，可以共同生活在一个空间里。","会出错、会恶搞，也会互相帮忙。","入口还在，继续使用的条件发生了变化。","安全、便利与使用能力，都有具体的需要。","让它继续可用，需要有人维持。"};
        for(int i=0;i<Cues.Count;i++)
        {
            stage.starts[i]=(float)Cues[i]["start"];stage.ends[i]=(float)Cues[i]["end"];stage.rootScales[i]=i>=9?1.4f:1.6f;
            stage.rootPositions[i]=i>=9?new Vector3(0,-.85f,-3.9f):new Vector3(0,-1.25f,-4.8f);
            if(i==7){stage.rootScales[i]=1.7f;stage.rootPositions[i]=new Vector3(0,-.15f,-7);stage.travelOffsets[i]=new Vector3(0,0,-8);}
            else if(i==4 || i==5)stage.travelOffsets[i]=new Vector3(0,0,-.85f);
            var cue=Child(i.ToString("00")+"_"+(string)Cues[i]["id"],World).transform;
            stage.cueRoots[i]=cue;Compose(i,cue,stage.starts[i],stage.ends[i]);
        }
        MemoryField(Child("ContentAcrossDistances",World).transform);
        var room=Child("OpeningRoom",World).transform;var shellList=new List<Transform>();var exits=new List<Vector3>();
        shellList.Add(Prim("Visible opening floor",PrimitiveType.Cube,room,new Vector3(0,-.08f,5),new Vector3(17,.12f,19),Mat("RoomFloor","#776657")).transform);exits.Add(new Vector3(0,-1,30));
        foreach(float x in new[]{-8.5f,8.5f}){shellList.Add(Prim("Room side",PrimitiveType.Cube,room,new Vector3(x,2.8f,5),new Vector3(.14f,5.7f,19),Mat("RoomWall","#bdc8c2")).transform);exits.Add(new Vector3(x*2,0,4));}
        shellList.Add(Prim("Room ceiling",PrimitiveType.Cube,room,new Vector3(0,5.7f,5),new Vector3(17,.12f,19),Mat("RoomCeiling","#d5d6cb")).transform);exits.Add(new Vector3(0,15,0));
        shellList.Add(Prim("Room far wall",PrimitiveType.Cube,room,new Vector3(0,2.8f,14.5f),new Vector3(17,5.7f,.14f),Mat("RoomWall","#bdc8c2")).transform);exits.Add(new Vector3(0,1,22));
        shellList.Add(OpeningDetails(room));exits.Add(new Vector3(0,.3f,22));
        stage.shells=shellList.ToArray();stage.shellPositions=stage.shells.Select(t=>t.localPosition).ToArray();stage.shellExits=exits.ToArray();
        var support=Prim("Stable invisible support",PrimitiveType.Cube,stable,new Vector3(0,-.15f,4),new Vector3(50,.20f,50),Mat("Support","#283945"),true);support.GetComponent<Renderer>().enabled=false;
        stage.contactScreens=new[]{Desk(stable,-2.75f,"desktop98"),Desk(stable,2.75f,"desktop98")};
        string[] screenIds={"desktop98","readme","world_editor","manga_ink","subtitle_editor","bt_transfer","tianya","vrchat2017","help_thread","access_conditions","eac_notice","maintenance_queue"};
        stage.contactScreenMaterials=screenIds.Select(PageMat).ToArray();
        BuildCast(stage,stable,controller);CalibrateContacts(stage);BuildMusic(music,owner);BuildFoley(director,stage,owner);BuildControls(stage,director,owner);BuildCameraAndLighting(owner);
        stage.animated=Animated.ToArray();stage.animOn=On.ToArray();stage.animOff=Off.ToArray();stage.animFrom=From.ToArray();stage.animTo=To.ToArray();stage.animBegin=Begin.ToArray();stage.animLength=Length.ToArray();stage.scaleFrom=ScaleFrom.ToArray();stage.scaleTo=ScaleTo.ToArray();stage.animWobble=Wobble.ToArray();stage.animPhase=Phase.ToArray();
        var spawn=Child("EpisodeSpawn",owner).transform;spawn.position=new Vector3(0,0,-2.8f);
        var descriptor=Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>();
        if(descriptor==null)throw new InvalidOperationException("World descriptor missing from template");
        var so=new SerializedObject(descriptor);var spawns=so.FindProperty("spawns");spawns.arraySize=1;spawns.GetArrayElementAtIndex(0).objectReferenceValue=spawn;so.ApplyModifiedPropertiesWithoutUndo();
        foreach(var behaviour in owner.GetComponentsInChildren<UdonSharpBehaviour>(true)){UdonSharpEditorUtility.CopyProxyToUdon(behaviour);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));}
        PoseForCapture(stage,0);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return JsonConvert.SerializeObject(new{scene=ScenePath,cues=Cues.Count,animated=Animated.Count,actors=stage.actors.Length,materials=Mats.Count,sourceFontMissing=missing,objects=owner.GetComponentsInChildren<Transform>(true).Length});
    }

    static void BuildCast(OWEpisodeStage stage,Transform parent,AnimatorController controller)
    {
        var actors=new List<OWActorRig>();
        for(int i=0;i<4;i++)
        {
            string path="Assets/OldWebActors/Cast/Actor_"+(i%2==0?"A":"B")+".prefab";
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent);
            obj.name="Ensemble_"+(i+1);var actor=obj.GetComponent<OWActorRig>();actor.actorSlot="episode-ensemble-"+(i+1);
            actor.animator.runtimeAnimatorController=controller;actor.animator.applyRootMotion=false;actor.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            actor.animator.Rebind();actor.animator.SetInteger("Pose",0);actor.animator.SetFloat("PoseTime",0);actor.animator.Update(0);
            actors.Add(actor);
            var foot=Prim("Local contact surface",PrimitiveType.Cylinder,parent,new Vector3(0,-.035f,0),new Vector3(1,.03f,.8f),Mat("ContactGround","#52676b"));
            // Each local surface stays with the body reference rather than the moving scenery.
            foot.transform.SetParent(obj.transform,false);foot.transform.localPosition=new Vector3(0,-.035f,0);
            var chest=actor.animator.GetBoneTransform(HumanBodyBones.Chest);
            if(chest!=null && i>=2)
                Prim("Distinct ensemble pin",PrimitiveType.Sphere,chest,new Vector3(.055f,.04f,.045f),new Vector3(.035f,.035f,.018f),Mat("CastPin"+i,i==2?"#d7b771":"#8fbbba"));
        }
        stage.actors=actors.ToArray();stage.actorPositions=new Vector3[Cues.Count*4];stage.actorYaws=new float[Cues.Count*4];stage.actorPoses=new int[Cues.Count*4];stage.actorResultPoses=new int[Cues.Count*4];
        int[][] gestures={new[]{1,5,0,0},new[]{2,5,0,0},new[]{3,1,2,0},new[]{1,5,3,0},new[]{1,3,0,2},new[]{2,2,3,0},new[]{3,5,2,0},new[]{4,2,4,3},new[]{5,3,4,0},new[]{5,3,1,0},new[]{3,5,1,2},new[]{1,5,2,0}};
        int[][] outcomes={new[]{2,3,0,0},new[]{2,4,0,0},new[]{3,4,2,0},new[]{2,4,4,0},new[]{3,4,2,0},new[]{2,4,3,4},new[]{2,4,2,0},new[]{4,4,4,4},new[]{4,2,4,2},new[]{5,5,1,0},new[]{5,3,1,2},new[]{5,5,2,0}};
        for(int cue=0;cue<Cues.Count;cue++)for(int i=0;i<4;i++)
        {
            var pos=i==0?new Vector3(-1.9f,0,1.75f):i==1?new Vector3(1.9f,0,1.75f):i==2?new Vector3(-4.35f,0,5.5f):new Vector3(4.35f,0,5.5f);
            if(cue==7){pos.x=i<2?Mathf.Sign(pos.x)*1.55f:pos.x;pos.z=i<2?.3f:pos.z-.5f;}
            int at=cue*4+i;stage.actorPositions[at]=pos;stage.actorYaws[at]=i%2==0?170:-170;
            stage.actorPoses[at]=gestures[cue][i];stage.actorResultPoses[at]=outcomes[cue][i];
        }
    }

    static void CalibrateContacts(OWEpisodeStage stage)
    {
        for(int i=0;i<2;i++)
        {
            var actor=stage.actors[i];actor.transform.localPosition=stage.actorPositions[i];actor.transform.localRotation=Quaternion.Euler(0,stage.actorYaws[i],0);
            actor.animator.SetInteger("Pose",1);actor.animator.SetFloat("PoseTime",.5f);actor.animator.Play("Type",0,.5f);actor.animator.Update(.02f);actor.animator.Update(0);
            Vector3 hands=(actor.leftHand.position+actor.rightHand.position)*.5f;
            Transform station=stage.contactScreens[i].transform;
            while(station!=null && station.name!="Contact workstation")station=station.parent;
            if(station==null)throw new InvalidOperationException("Contact workstation parent missing");
            station.localRotation=Quaternion.Euler(0,180,0);
            station.position=hands-new Vector3(0,.65f,.22f);
            foreach(Transform leg in station)if(leg.name=="Leg")
            {
                float height=.55f+station.position.y;leg.localScale=new Vector3(.055f,height,.055f);
                var pos=leg.localPosition;pos.y=height*.5f-station.position.y;leg.localPosition=pos;
            }
        }
    }

    static void BuildMusic(OWEpisodeMusic music,Transform parent)
    {
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-audio.json"));
        music.sources=new AudioSource[4];music.voiceTransforms=new Transform[4];music.normalPositions=new Vector3[4];music.expandedPositions=new Vector3[4];music.pulseGlyphs=new Transform[4];music.pulseBaseScale=new float[4];
        music.notes0=data["notes"][0].ToObject<float[]>();music.notes1=data["notes"][1].ToObject<float[]>();music.notes2=data["notes"][2].ToObject<float[]>();music.notes3=data["notes"][3].ToObject<float[]>();
        music.envelope0=data["envelopes"][0].ToObject<float[]>();music.envelope1=data["envelopes"][1].ToObject<float[]>();music.envelope2=data["envelopes"][2].ToObject<float[]>();music.envelope3=data["envelopes"][3].ToObject<float[]>();
        var spatialType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VRC.SDK3.Components.VRCSpatialAudioSource")??a.GetType("VRC.SDKBase.VRC_SpatialAudioSource")).FirstOrDefault(t=>t!=null);
        for(int i=0;i<4;i++)
        {
            var g=Child("FC14 voice "+(i+1),music.transform);var source=g.AddComponent<AudioSource>();
            source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/FC14_Mono_"+(i+1).ToString("00")+".wav");
            source.playOnAwake=false;source.loop=false;source.volume=.8f;source.spatialBlend=.88f;source.spatialize=true;source.dopplerLevel=0;source.spread=28;
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=10;source.maxDistance=25;source.priority=0;
            if(spatialType!=null)
            {
                var component=g.AddComponent(spatialType);var so=new SerializedObject(component);
                foreach(string field in new[]{"Gain","gain"}){var p=so.FindProperty(field);if(p!=null && p.propertyType==SerializedPropertyType.Float)p.floatValue=0;}
                foreach(string field in new[]{"Near","near"}){var p=so.FindProperty(field);if(p!=null && p.propertyType==SerializedPropertyType.Float)p.floatValue=10;}
                foreach(string field in new[]{"Far","far"}){var p=so.FindProperty(field);if(p!=null && p.propertyType==SerializedPropertyType.Float)p.floatValue=25;}
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            music.sources[i]=source;music.voiceTransforms[i]=g.transform;music.normalPositions[i]=new Vector3((i-1.5f)*.8f,1.6f+(i%2)*.13f,3.2f);
            music.expandedPositions[i]=i==0?new Vector3(-3.8f,2.3f,2.5f):i==3?new Vector3(3.8f,2.8f,2.5f):music.normalPositions[i];g.transform.localPosition=music.normalPositions[i];
            var glyph=Page("pixel_star",parent,new Vector3((i-1.5f)*1.45f,3.9f+(i%2)*.16f,6.2f),.12f,0,false);
            music.pulseGlyphs[i]=glyph;music.pulseBaseScale[i]=1;
        }
        var stage=parent.GetComponentInChildren<OWEpisodeStage>();var intro=stage.cueRoots[0];
        music.meterBars=new Transform[4];music.meterSizes=new Vector3[4];music.meterBottoms=new Vector3[4];
        for(int i=0;i<4;i++)
        {
            var size=new Vector3(38*3.45f/960,65*3.45f/960,.008f);
            var bottom=new Vector3((485+i*64+19-480)*3.45f/960,2.16f+(360-630)*3.45f/960,5.595f);
            var bar=Prim("FC14 actual voice meter "+(i+1),PrimitiveType.Cube,intro,bottom+Vector3.up*(size.y*.5f),size,Mat("ModuleMeter","#9ebf6d",.35f)).transform;
            music.meterBars[i]=bar;music.meterSizes[i]=size;music.meterBottoms[i]=bottom;
        }
    }

    static void BuildControls(OWEpisodeStage stage,OWEpisodeDirector director,Transform parent)
    {
        var panel=Child("Audience playback desk",parent).transform;panel.localPosition=new Vector3(-2.65f,0,-1.25f);panel.localRotation=Quaternion.Euler(0,-17,0);
        Prim("Playback stand",PrimitiveType.Cube,panel,new Vector3(0,.8f,0),new Vector3(1.6f,1.25f,.10f),Mat("ControlCase","#394e5a"),true);
        Label("Control title","共同观看",panel,new Vector3(0,1.25f,-.065f),1.45f,.19f);
        stage.chapterText=Label("Current chapter","01  接入",panel,new Vector3(0,1.03f,-.065f),1.4f,.12f);
        stage.progressText=Label("Playback progress","0:00 / 2:13",panel,new Vector3(0,.87f,-.065f),1.4f,.1f);
        Label("Desktop playback key","桌面：回车键播放 / 暂停",panel,new Vector3(0,1.48f,-.065f),1.8f,.075f);
        string[] names={"播放 / 暂停","从头开始","第 1 段","第 2 段","第 3 段"};
        for(int i=0;i<names.Length;i++)
        {
            float x=i<2?0:(i-3)*.46f;float y=i==0?.64f:i==1?.44f:.21f;float w=i<2?1.35f:.42f;
            var button=Prim(names[i],PrimitiveType.Cube,panel,new Vector3(x,y,-.09f),new Vector3(w,.145f,.075f),Mat("Button"+i,i==0?"#829c87":"#6e8795"),true);
            var behaviour=button.AddUdonSharpComponent<OWEpisodeButton>();behaviour.director=director;behaviour.action=i;
            var backing=UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);var interaction=new SerializedObject(backing);
            var proximity=interaction.FindProperty("proximity");if(proximity!=null)proximity.floatValue=3.5f;
            var interactText=interaction.FindProperty("interactText");if(interactText!=null)interactText.stringValue=names[i];interaction.ApplyModifiedPropertiesWithoutUndo();
            Label(names[i]+" label",names[i],panel,new Vector3(x,y-.003f,-.135f),w-.035f,i<2?.09f:.07f);
        }
        stage.captionText=Label("One readable action",stage.captions[0],parent,new Vector3(0,.24f,4.7f),5.8f,.19f,"#f4ead2");
        var end=Child("Preview completion",parent).transform;end.localPosition=new Vector3(0,1.2f,3.8f);
        Prim("Completion card",PrimitiveType.Cube,end,Vector3.zero,new Vector3(3.3f,.72f,.055f),Mat("ControlCase","#394e5a"));
        Label("Completion message","前三段演出到这里。\n可以重播，或选择段落近看。",end,new Vector3(0,0,-.04f),3,.15f);stage.endCard=end.gameObject;end.gameObject.SetActive(false);
        var sources=Child("Sources and reconstruction",parent).transform;sources.localPosition=new Vector3(3.5f,1.25f,-1.4f);sources.localRotation=Quaternion.Euler(0,20,0);
        Prim("Sources panel",PrimitiveType.Cube,sources,Vector3.zero,new Vector3(1.8f,1.6f,.06f),Mat("SourcePaper","#d3d3bd"));
        Label("Sources","来源与制作\n\n年代界面为自绘重建，\n示例作品与名单为原创。\n\n2017 Hub：VRChat 官方公告图\n3Q：2010-11-03 同期报道\nEAC：2022-07 官方公开说明\n\n音乐：FC14 · I.C.E. 2 来源记载\nRhino of Torment（资料记载）\n图形技术参考：EDGE 模板\n非语音音效：本项目原创",sources,new Vector3(0,.02f,-.045f),1.65f,.075f,"#253e51");
    }

    static void BuildFoley(OWEpisodeDirector director,OWEpisodeStage stage,Transform parent)
    {
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-foley.json"));
        var root=Child("Local action sounds",parent).transform;var foley=root.gameObject.AddUdonSharpComponent<OWEpisodeFoley>();foley.director=director;foley.stage=stage;
        var files=data["files"].ToObject<string[]>();foley.sources=new AudioSource[files.Length];
        for(int i=0;i<files.Length;i++)
        {
            var audio=Child("Original "+Path.GetFileNameWithoutExtension(files[i]),root).AddComponent<AudioSource>();
            audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/"+files[i]);audio.playOnAwake=false;audio.loop=false;audio.volume=(float)data["volume"];
            if(audio.clip==null)throw new InvalidOperationException("Missing original Foley clip "+files[i]);
            audio.spatialBlend=1;audio.spatialize=true;audio.dopplerLevel=0;audio.minDistance=3;audio.maxDistance=18;audio.rolloffMode=AudioRolloffMode.Linear;foley.sources[i]=audio;
        }
        foley.eventTimes=((JArray)data["events"]).Select(e=>(float)e["time"]).ToArray();foley.eventClips=((JArray)data["events"]).Select(e=>(int)e["clip"]).ToArray();foley.eventEmitters=((JArray)data["events"]).Select(e=>(int)e["emitter"]).ToArray();
        var content=Child("Main content sound point",root).transform;content.position=new Vector3(0,2,3.8f);
        var keyboard=Child("Keyboard contact sound point",root).transform;Transform station=stage.contactScreens[0].transform;
        while(station!=null && station.name!="Contact workstation")station=station.parent;
        keyboard.position=station.TransformPoint(new Vector3(0,.65f,-.22f));foley.emitters=new[]{content,keyboard};
    }

    static void BuildCameraAndLighting(Transform owner)
    {
        var camera=Camera.main;
        if(camera==null){camera=Child("Episode Review Camera",null).AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();}
        camera.name="Episode Review Camera";camera.transform.position=new Vector3(0,1.35f,-2.8f);camera.transform.rotation=Quaternion.Euler(0,0,0);
        camera.fieldOfView=52;camera.nearClipPlane=.035f;camera.farClipPlane=180;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=C("#20333e");camera.allowHDR=false;
        foreach(var light in Object.FindObjectsOfType<Light>())if(!light.transform.IsChildOf(owner))light.enabled=false;
        var key=Child("Stable actor key",owner).AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.color=C("#fff2d9");key.transform.rotation=Quaternion.Euler(35,-32,0);key.shadows=LightShadows.Soft;key.shadowStrength=.65f;
        var fill=Child("Stable actor fill",owner).AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.28f;fill.color=C("#c5e0e8");fill.transform.rotation=Quaternion.Euler(22,150,0);fill.shadows=LightShadows.None;
        RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=C("#a0aaa6");
    }

    static void PoseForCapture(OWEpisodeStage stage,float time)
    {
        stage.Sample(time);
        if(CaptureMusic==null)CaptureMusic=Object.FindObjectOfType<OWEpisodeMusic>();
        if(CaptureMusic!=null)CaptureMusic.SampleVisual(time);
        string[] names={"Idle","Type","Offer","Point","Cheer","Inspect","Walk"};
        foreach(var actor in stage.actors)
        {
            int pose=actor.animator.GetInteger("Pose");float phase=actor.animator.GetFloat("PoseTime");
            actor.animator.Play(names[Mathf.Clamp(pose,0,6)],0,phase);actor.animator.Update(.02f);actor.animator.Update(0);
        }
        Canvas.ForceUpdateCanvases();
    }

    static string WriteFrame(Camera camera,string filename,int width=1600,int height=900)
    {
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123/frames");Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,filename+".png");
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        var old=camera.targetTexture;var active=RenderTexture.active;Texture2D image=null;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.DestroyImmediate(image);}
        return path;
    }

    public static object CaptureAt(float seconds)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("CaptureAt is an authored-state preview; use live capture for Play Mode");
        var stage=Object.FindObjectOfType<OWEpisodeStage>();if(stage==null)throw new InvalidOperationException("Open Episode123 scene");
        var camera=Camera.main;PoseForCapture(stage,seconds);
        string p=WriteFrame(camera,"frame_"+seconds.ToString("000.00").Replace('.','_'));
        var report=DescribeStage(stage);
        PoseForCapture(stage,0);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return JsonConvert.SerializeObject(new{path=p,time=seconds,stage=report});
    }

    public static object CaptureAll()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authored-state previews");
        var stage=Object.FindObjectOfType<OWEpisodeStage>();if(stage==null)throw new InvalidOperationException("Open Episode123 scene");
        var camera=Camera.main;var reports=new List<object>();
        try
        {
            for(int i=0;i<stage.starts.Length;i++)
            {
                float time=Mathf.Min(stage.ends[i]-.8f,stage.starts[i]+(stage.ends[i]-stage.starts[i])*.68f);
                PoseForCapture(stage,time);string path=WriteFrame(camera,"cue_"+i.ToString("00"));
                reports.Add(new{cue=i,time,path,stage=DescribeStage(stage)});
            }
        }
        finally{PoseForCapture(stage,0);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());}
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");
        File.WriteAllText(Path.Combine(directory,"capture-report.json"),JsonConvert.SerializeObject(reports,Formatting.Indented));
        return JsonConvert.SerializeObject(new{frames=reports.Count,report=directory+"/capture-report.json"});
    }

    static object DescribeStage(OWEpisodeStage stage)
    {
        return new {
            cue=stage.sampledCue,time=stage.sampledTime,visibleRoots=stage.activeCueRoots,
            actors=stage.actors.Select(a=>new { a.actorSlot,a.appearanceId,pose=a.animator.GetInteger("Pose"),footY=new[]{a.leftFoot.position.y,a.rightFoot.position.y},rightHand=new[]{a.rightHand.position.x,a.rightHand.position.y,a.rightHand.position.z} }).ToArray(),
            visibleRenderers=Object.FindObjectsOfType<Renderer>().Count(r=>r.enabled),
            badMaterials=Object.FindObjectsOfType<Renderer>().SelectMany(r=>r.sharedMaterials).Count(m=>m==null || m.shader==null || m.shader.name=="Hidden/InternalErrorShader")
        };
    }

    public static object InspectEpisode()
    {
        var stage=Object.FindObjectOfType<OWEpisodeStage>();var director=Object.FindObjectOfType<OWEpisodeDirector>();var music=Object.FindObjectOfType<OWEpisodeMusic>();
        if(stage==null || director==null || music==null)throw new InvalidOperationException("Episode components missing");
        return JsonConvert.SerializeObject(new{
            scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,playing=EditorApplication.isPlaying,
            cues=stage.cueRoots.Length,animated=stage.animated.Length,caption=stage.captionText.text,
            audio=music.sources.Select(s=>new {s.name,channels=s.clip.channels,samples=s.clip.samples,frequency=s.clip.frequency,seconds=s.clip.length,s.spatialBlend,s.dopplerLevel}).ToArray(),
            actors=stage.actors.Length,stage=DescribeStage(stage),
            programs=stage.transform.root.GetComponentsInChildren<UdonSharpBehaviour>(true).Select(b=>new{type=b.GetType().Name,backing=UdonSharpEditorUtility.GetBackingUdonBehaviour(b)!=null}).ToArray()
        });
    }

    public static object InspectFont()
    {
        var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/EpisodeChinese.asset");
        var face=f.faceInfo;var caption=Object.FindObjectOfType<OWEpisodeStage>().captionText;
        return JsonConvert.SerializeObject(new{
            source=AssetDatabase.GetAssetPath(f.sourceFontFile),population=f.atlasPopulationMode.ToString(),characters=f.characterTable.Count,
            face=new{face.familyName,face.styleName,face.pointSize,face.scale,face.lineHeight,face.ascentLine,face.descentLine},
            atlas=f.atlasTextures.Select(t=>new{t.width,t.height,t.isReadable}).ToArray(),
            caption=new{caption.fontSize,width=caption.textBounds.size.x,height=caption.textBounds.size.y,scaleX=caption.transform.lossyScale.x}
        });
    }

    public static object SaveWorkingScene()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath || EditorApplication.isPlaying)throw new InvalidOperationException("Only the stopped owned Episode scene may be saved here");
        var root=GameObject.Find("OWEpisode123");var stage=root.GetComponentInChildren<OWEpisodeStage>();
        PoseForCapture(stage,0);
        foreach(var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))UdonSharpEditorUtility.CopyProxyToUdon(b);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Episode baseline saved; legacy scene untouched";
    }

    static object LiveState(OWEpisodeDirector director,OWEpisodeStage stage,OWEpisodeMusic music)
    {
        var d=UdonSharpEditorUtility.GetBackingUdonBehaviour(director);var s=UdonSharpEditorUtility.GetBackingUdonBehaviour(stage);var m=UdonSharpEditorUtility.GetBackingUdonBehaviour(music);
        return new {
            playing=d.GetProgramVariable("_playing"),elapsed=d.GetProgramVariable("_elapsed"),stageTime=s.GetProgramVariable("sampledTime"),cue=s.GetProgramVariable("sampledCue"),
            scheduled=m.GetProgramVariable("scheduleCount"),drift=m.GetProgramVariable("measuredMaxIntertrackDrift"),stageMs=d.GetProgramVariable("lastStageSampleMilliseconds"),audioMs=d.GetProgramVariable("lastAudioSyncMilliseconds"),
            clips=music.sources.Select(a=>new{a.name,a.isPlaying,a.isVirtual,a.timeSamples,a.time,a.volume}).ToArray(),
            localPlayer=VRC.SDKBase.Networking.LocalPlayer!=null,
            cameras=Object.FindObjectsOfType<Camera>().Select(c=>new{c.name,c.enabled,c.depth,c.fieldOfView,position=new[]{c.transform.position.x,c.transform.position.y,c.transform.position.z}}).ToArray()
        };
    }

    public static async Task<object> LiveSmoke()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var stage=Object.FindObjectOfType<OWEpisodeStage>();var music=Object.FindObjectOfType<OWEpisodeMusic>();
        if(director==null || stage==null || music==null)throw new InvalidOperationException("Episode scene components missing");
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(director);var stageVm=UdonSharpEditorUtility.GetBackingUdonBehaviour(stage);
        var results=new List<object>();
        vm.SendCustomEvent("PlayFromConnect");await Task.Delay(1700);results.Add(new{step="start",state=LiveState(director,stage,music)});
        float started=Convert.ToSingle(vm.GetProgramVariable("_elapsed"));if(started<.2f || started>5)throw new InvalidOperationException("Live Udon clock did not start: "+started);
        WriteFrame(Camera.main,"live_connect");
        vm.SendCustomEvent("Pause");await Task.Delay(180);float before=Convert.ToSingle(vm.GetProgramVariable("_elapsed"));await Task.Delay(700);float after=Convert.ToSingle(vm.GetProgramVariable("_elapsed"));
        results.Add(new{step="pause",before,after,state=LiveState(director,stage,music)});
        if(Mathf.Abs(after-before)>.02f || music.sources.Any(a=>a.isPlaying))throw new InvalidOperationException("Pause failed to freeze time and all four sources");
        vm.SendCustomEvent("PlayFromCreate");await Task.Delay(1550);results.Add(new{step="seek-create",state=LiveState(director,stage,music)});
        if(Convert.ToInt32(stageVm.GetProgramVariable("sampledCue"))!=3)throw new InvalidOperationException("Create seek did not restore first manga cue");
        WriteFrame(Camera.main,"live_create");
        vm.SendCustomEvent("PlayFromConstrain");await Task.Delay(1500);results.Add(new{step="seek-constrain",state=LiveState(director,stage,music)});
        if(Convert.ToInt32(stageVm.GetProgramVariable("sampledCue"))!=9)throw new InvalidOperationException("Constrain seek did not restore cue");
        WriteFrame(Camera.main,"live_constrain");
        var values=music.sources.Select(a=>a.timeSamples).ToArray();int spread=values.Max()-values.Min();
        if(spread>960)throw new InvalidOperationException("Four-source sample spread exceeds 20 ms: "+spread);
        vm.SendCustomEvent("Restart");await Task.Delay(900);results.Add(new{step="restart",state=LiveState(director,stage,music)});
        if(Convert.ToInt32(stageVm.GetProgramVariable("sampledCue"))!=0)throw new InvalidOperationException("Restart did not restore scene 0");
        vm.SendCustomEvent("StopAtStart");await Task.Delay(100);
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");
        File.WriteAllText(Path.Combine(dir,"live-smoke.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
        return JsonConvert.SerializeObject(new{success=true,sampleSpread=spread,steps=results.Count,report=dir+"/live-smoke.json"});
    }

    public static async Task<object> LiveEndurance()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var stage=Object.FindObjectOfType<OWEpisodeStage>();var music=Object.FindObjectOfType<OWEpisodeMusic>();
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(director);var stageVm=UdonSharpEditorUtility.GetBackingUdonBehaviour(stage);
        var rows=new List<object>();var frameDurations=new List<float>();var seen=new HashSet<int>();
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");
        int maxSpread=0,maxRawSpread=0,maxBatches=0;float maxAudioClockError=0,maxStageMs=0,maxAudioMs=0;int snapshots=0;
        var transientReads=new List<object>();AudioSettings.GetDSPBufferSize(out int bufferLength,out int bufferCount);
        float maxListenerRms=0;var maxSourceRms=new float[4];var output=new float[1024];
        // Received-anchor reconstruction: this checks local state restoration, not network transport.
        vm.SetProgramVariable("_offset",0f);vm.SetProgramVariable("_anchor",VRC.SDKBase.Networking.GetServerTimeInSeconds()-70.0);
        vm.SetProgramVariable("_playing",true);vm.SetProgramVariable("_revision",Convert.ToInt32(vm.GetProgramVariable("_revision"))+1);
        await Task.Delay(500);
        float reconstructed=Convert.ToSingle(vm.GetProgramVariable("_elapsed"));
        if(reconstructed<70 || reconstructed>72 || Convert.ToInt32(stageVm.GetProgramVariable("sampledCue"))!=5)throw new InvalidOperationException("Received anchor did not reconstruct BT scene");
        rows.Add(new{step="received-anchor-reconstruction",state=LiveState(director,stage,music)});
        vm.SendCustomEvent("Restart");var watch=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            while(watch.Elapsed.TotalSeconds<135.0)
            {
                await Task.Delay(200);
                float time=Convert.ToSingle(vm.GetProgramVariable("_elapsed"));int cue=Convert.ToInt32(stageVm.GetProgramVariable("sampledCue"));
                bool playing=Convert.ToBoolean(vm.GetProgramVariable("_playing"));
                if(time>.2f && time<132.8f)
                {
                    if(music.sources.Any(s=>!s.isPlaying))throw new InvalidOperationException("A music voice stopped at "+time);
                    int spread=int.MaxValue;int[] bestSamples=null;
                    for(int attempt=0;attempt<3;attempt++){var samples=music.sources.Select(s=>s.timeSamples).ToArray();int value=samples.Max()-samples.Min();if(value<spread){spread=value;bestSamples=samples;}}
                    maxRawSpread=Math.Max(maxRawSpread,spread);
                    if(spread>960)
                    {
                        var initial=bestSamples;int initialSpread=spread;
                        await Task.Delay(Math.Max(60,(int)(bufferLength*2500.0/AudioSettings.outputSampleRate)));
                        spread=int.MaxValue;
                        for(int attempt=0;attempt<3;attempt++){var samples=music.sources.Select(s=>s.timeSamples).ToArray();int value=samples.Max()-samples.Min();if(value<spread){spread=value;bestSamples=samples;}}
                        transientReads.Add(new{time,initialSpread,initial,recheckSpread=spread,rechecked=bestSamples});
                    }
                    maxSpread=Math.Max(maxSpread,spread);
                    double anchor=Convert.ToDouble(vm.GetProgramVariable("_anchor"));float offset=Convert.ToSingle(vm.GetProgramVariable("_offset"));
                    float currentClock=offset+(float)Math.Max(0,VRC.SDKBase.Networking.GetServerTimeInSeconds()-anchor);
                    maxAudioClockError=Mathf.Max(maxAudioClockError,Mathf.Abs(music.sources[0].time-currentClock));
                    if(spread>960)throw new InvalidOperationException("Persistent intertrack spread >20ms at "+time+": "+string.Join(",",bestSamples));
                }
                maxStageMs=Mathf.Max(maxStageMs,Convert.ToSingle(vm.GetProgramVariable("lastStageSampleMilliseconds")));
                maxAudioMs=Mathf.Max(maxAudioMs,Convert.ToSingle(vm.GetProgramVariable("lastAudioSyncMilliseconds")));
                AudioListener.GetOutputData(output,0);double energy=0;for(int n=0;n<output.Length;n++)energy+=output[n]*output[n];maxListenerRms=Mathf.Max(maxListenerRms,(float)Math.Sqrt(energy/output.Length));
                for(int voice=0;voice<4;voice++)
                {
                    music.sources[voice].GetOutputData(output,0);energy=0;for(int n=0;n<output.Length;n++)energy+=output[n]*output[n];maxSourceRms[voice]=Mathf.Max(maxSourceRms[voice],(float)Math.Sqrt(energy/output.Length));
                }
                if(cue>=0 && cue<stage.cueRoots.Length && time-stage.starts[cue]>.8f && !seen.Contains(cue))
                {
                    if(!stage.cueRoots[cue].gameObject.activeInHierarchy)throw new InvalidOperationException("Expected cue root invisible at "+time);
                    seen.Add(cue);rows.Add(new{step="cue",cue,time,state=LiveState(director,stage,music)});
                    WriteFrame(Camera.main,"endurance_"+cue.ToString("00"));snapshots++;
                }
                if(Time.unscaledDeltaTime>.001f && Time.unscaledDeltaTime<1)frameDurations.Add(Time.unscaledDeltaTime);
                maxBatches=Math.Max(maxBatches,UnityStats.batches);
                string progress=JsonConvert.SerializeObject(new{time,cue,seen=seen.Count,maxSpread,playing});
                SessionState.SetString("EpisodeLiveProgress",progress);File.WriteAllText(Path.Combine(directory,"live-progress.json"),progress);
            }
            float end=Convert.ToSingle(vm.GetProgramVariable("_elapsed"));bool stillPlaying=Convert.ToBoolean(vm.GetProgramVariable("_playing"));
            if(Mathf.Abs(end-133.12f)>.02f || stillPlaying || music.sources.Any(s=>s.isPlaying) || seen.Count!=12)throw new InvalidOperationException("End state or cue coverage invalid");
            WriteFrame(Camera.main,"endurance_complete");
            var ordered=frameDurations.OrderBy(x=>x).ToArray();float median=ordered.Length==0?0:ordered[ordered.Length/2];
            if(maxListenerRms<.0001f || maxSourceRms.Any(v=>v<.0001f))throw new InvalidOperationException("Runtime audio output remained silent");
            var result=new{success=true,showSeconds=133.12,cuesSeen=seen.Count,maxIntertrackSamples=maxSpread,maxIntertrackSeconds=maxSpread/48000f,maxRawSpread,bufferLength,bufferCount,transientReads,maxAudioClockError,maxStageMs,maxAudioMs,maxListenerRms,maxSourceRms,editorClientSimMedianFps=median>0?1/median:0,maxEditorBatches=maxBatches,snapshots,endCard=stage.endCard.activeInHierarchy,rows};
            File.WriteAllText(Path.Combine(directory,"live-endurance.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
            return JsonConvert.SerializeObject(new{success=true,cues=seen.Count,maxSpread,maxAudioClockError,report=directory+"/live-endurance.json"});
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(directory,"live-endurance-failure.json"),JsonConvert.SerializeObject(new{error=error.ToString(),state=LiveState(director,stage,music),rows},Formatting.Indented));throw;
        }
        finally{vm.SendCustomEvent("StopAtStart");}
    }

    public static object LiveProgress() { return SessionState.GetString("EpisodeLiveProgress","not started"); }

    public static object StartMovie()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open stopped Episode scene for reference rendering");
        if(SessionState.GetBool("EpisodeMovieRunning",false))throw new InvalidOperationException("Movie capture already active");
        MovieStage=Object.FindObjectOfType<OWEpisodeStage>();MovieCamera=Camera.main;MovieFrame=0;
        MovieDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123/movie-frames");Directory.CreateDirectory(MovieDirectory);
        MoviePreviousTarget=MovieCamera.targetTexture;MovieTarget=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);MovieTarget.Create();MovieImage=new Texture2D(1600,900,TextureFormat.RGB24,false);
        SessionState.SetBool("EpisodeMovieRunning",true);SessionState.SetBool("EpisodeMovieCancel",false);SessionState.SetInt("EpisodeMovieFrame",0);SessionState.SetString("EpisodeMovieError","");SessionState.SetString("EpisodeMovieDirectory",MovieDirectory);
        EditorApplication.update+=MovieTick;
        return JsonConvert.SerializeObject(new{started=true,directory=MovieDirectory,fps=MovieFps,frames=MovieFrames,showSeconds=133.12,reviewHoldSeconds=1.92,kind="deterministic-authored-state-reference-not-headset-capture"});
    }

    static void MovieTick()
    {
        try
        {
            if(SessionState.GetBool("EpisodeMovieCancel",false)){FinishMovie();return;}
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Capture interrupted by scene or play-state change");
            float time=Mathf.Min(133.12f,MovieFrame/(float)MovieFps);PoseForCapture(MovieStage,time);
            var active=RenderTexture.active;
            try
            {
                MovieCamera.targetTexture=MovieTarget;MovieCamera.Render();RenderTexture.active=MovieTarget;
                MovieImage.ReadPixels(new Rect(0,0,1600,900),0,0);MovieImage.Apply(false);
                File.WriteAllBytes(Path.Combine(MovieDirectory,"frame_"+MovieFrame.ToString("00000")+".jpg"),MovieImage.EncodeToJPG(94));
            }
            finally{RenderTexture.active=active;MovieCamera.targetTexture=MoviePreviousTarget;}
            MovieFrame++;SessionState.SetInt("EpisodeMovieFrame",MovieFrame);
            if(MovieFrame>=MovieFrames)FinishMovie();
        }
        catch(Exception error)
        {
            SessionState.SetString("EpisodeMovieError",error.ToString());FinishMovie();
        }
    }

    static void FinishMovie()
    {
        EditorApplication.update-=MovieTick;SessionState.SetBool("EpisodeMovieRunning",false);
        if(MovieCamera!=null)MovieCamera.targetTexture=MoviePreviousTarget;
        if(MovieTarget!=null){MovieTarget.Release();Object.DestroyImmediate(MovieTarget);MovieTarget=null;}
        if(MovieImage!=null){Object.DestroyImmediate(MovieImage);MovieImage=null;}
        if(SceneManager.GetActiveScene().path==ScenePath && !EditorApplication.isPlaying && MovieStage!=null)SaveWorkingScene();
        File.WriteAllText(Path.Combine(MovieDirectory,"render-report.json"),JsonConvert.SerializeObject(new{fps=MovieFps,frames=MovieFrame,complete=MovieFrame==MovieFrames,showSeconds=133.12,reviewHoldSeconds=1.92,error=SessionState.GetString("EpisodeMovieError","")},Formatting.Indented));
    }

    public static object MovieStatus()
    {
        return JsonConvert.SerializeObject(new{running=SessionState.GetBool("EpisodeMovieRunning",false),frame=SessionState.GetInt("EpisodeMovieFrame",0),total=MovieFrames,error=SessionState.GetString("EpisodeMovieError",""),directory=SessionState.GetString("EpisodeMovieDirectory","")});
    }

    public static object CancelMovie(){SessionState.SetBool("EpisodeMovieCancel",true);return "Cancellation requested; capture callback will restore the baseline";}

    public static object ViewpointAndShaderChecks()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open stopped Episode scene");
        var stage=Object.FindObjectOfType<OWEpisodeStage>();var camera=Camera.main;var pos=camera.transform.position;var rotation=camera.transform.rotation;
        var frames=new List<object>();
        try
        {
            foreach(float time in new[]{33.28f,47.36f,66.56f,87.04f,117.76f})
            {
                PoseForCapture(stage,time);
                foreach(float side in new[]{-.032f,.032f})
                {
                    camera.transform.position=pos+Vector3.right*side;
                    string name="eye_"+time.ToString("000.00").Replace('.','_')+(side<0?"_left":"_right");
                    frames.Add(new{time,side,path=WriteFrame(camera,name)});
                }
            }
            PoseForCapture(stage,47.36f);
            foreach(var offset in new[]{new Vector3(-.7f,0,0),new Vector3(.7f,0,0),new Vector3(0,-.35f,0),new Vector3(0,.4f,0)})
            {
                camera.transform.position=pos+offset;
                frames.Add(new{time=47.36f,offset=new[]{offset.x,offset.y,offset.z},path=WriteFrame(camera,"view_"+frames.Count)});
            }
        }
        finally{camera.transform.position=pos;camera.transform.rotation=rotation;SaveWorkingScene();}
        var shader=Shader.Find("OldWebMap/Page Surface");var variants=new ShaderVariantCollection();var variantResults=new List<object>();
        foreach(string keyword in new[]{"STEREO_INSTANCING_ON","STEREO_MULTIVIEW_ON"})
        {
            try{bool added=variants.Add(new ShaderVariantCollection.ShaderVariant(shader,PassType.Normal,new[]{keyword}));variantResults.Add(new{keyword,added,error=""});}
            catch(Exception error){variantResults.Add(new{keyword,added=false,error=error.Message});}
        }
        variants.WarmUp();Object.DestroyImmediate(variants);
        var messages=ShaderUtil.GetShaderMessages(shader).Select(m=>new{m.message,severity=m.severity.ToString(),m.line,platform=m.platform.ToString()}).ToArray();
        if(messages.Any(m=>m.severity=="Error"))throw new InvalidOperationException("Page shader compiler error: "+JsonConvert.SerializeObject(messages));
        var result=new{frames,shader=shader.name,shader.isSupported,variantResults,messages,scope="Parallel-view geometry checks and shader variants, not actual headset stereo/comfort verification"};
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");File.WriteAllText(Path.Combine(dir,"viewpoint-shader-checks.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
        return JsonConvert.SerializeObject(new{frames=frames.Count,shader=shader.name,shader.isSupported,variantResults,messages});
    }

    public static object SdkBuildInfo()
    {
        var types=new List<Type>();
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try{types.AddRange(assembly.GetTypes().Where(t=>t.Name.Contains("WorldBuilder") || t.Name=="VRCSdkControlPanel"));}catch(System.Reflection.ReflectionTypeLoadException){}
        }
        return JsonConvert.SerializeObject(types.Select(t=>new{type=t.FullName,methods=t.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance).Where(m=>m.Name.Contains("Build") || m.Name.Contains("ExportScene") || m.Name=="TryGetBuilder").Select(m=>m.ToString()).ToArray()}).ToArray());
    }

    public static object MotionChecks()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Use stopped authored scene for motion retarget checks");
        var stage=Object.FindObjectOfType<OWEpisodeStage>();var rows=new List<object>();
        try
        {
            foreach(var actor in stage.actors.Take(2))
            {
                if(actor.animator.avatar==null || !actor.animator.avatar.isHuman || !actor.animator.avatar.isValid)throw new InvalidOperationException("Invalid Humanoid mapping");
                actor.animator.Play("Idle",0,.5f);actor.animator.SetFloat("PoseTime",.5f);actor.animator.Update(.02f);actor.animator.Update(0);Vector3 rest=actor.rightHand.position;
                float maxTravel=0,minFoot=10,maxFoot=-10;
                foreach(string motion in new[]{"Type","Offer","Point","Cheer","Inspect"})
                {
                    actor.animator.Play(motion,0,.5f);actor.animator.SetFloat("PoseTime",.5f);actor.animator.Update(.02f);actor.animator.Update(0);
                    maxTravel=Mathf.Max(maxTravel,Vector3.Distance(rest,actor.rightHand.position));minFoot=Mathf.Min(minFoot,actor.leftFoot.position.y,actor.rightFoot.position.y);maxFoot=Mathf.Max(maxFoot,actor.leftFoot.position.y,actor.rightFoot.position.y);
                }
                if(maxTravel<.08f || minFoot<-.02f || maxFoot>.3f)throw new InvalidOperationException("Motion retarget/contact range invalid for "+actor.appearanceId);
                rows.Add(new{actor.appearanceId,humanoidBones=actor.humanoidBones.Count(b=>b!=null),maxHandTravel=maxTravel,minFoot,maxFoot,clips=actor.animator.runtimeAnimatorController.animationClips.Select(c=>new{c.name,c.humanMotion}).ToArray()});
            }
        }
        finally{SaveWorkingScene();}
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");File.WriteAllText(Path.Combine(dir,"motion-checks.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));
        return JsonConvert.SerializeObject(new{success=true,actors=rows});
    }

    public static object SetupSdkLayers()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open stopped Episode scene for SDK setup");
        int[] layers=Enumerable.Range(0,32).ToArray();
        Func<object> snapshot=()=>new{names=layers.Select(LayerMask.LayerToName).ToArray(),ignoreCollisions=layers.Select(a=>layers.Select(b=>Physics.GetIgnoreLayerCollision(a,b)).ToArray()).ToArray()};
        var before=snapshot();bool setLayers=!UpdateLayers.AreLayersSetup(),setCollisions=!UpdateLayers.IsCollisionLayerMatrixSetup();
        if(setLayers)UpdateLayers.SetupEditorLayers();
        if(setCollisions)UpdateLayers.SetupCollisionLayerMatrix();
        AssetDatabase.SaveAssets();
        bool success=UpdateLayers.AreLayersSetup() && UpdateLayers.IsCollisionLayerMatrixSetup();
        var result=new{success,setLayers,setCollisions,before,after=snapshot(),method="Official SDK UpdateLayers setup"};
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");
        File.WriteAllText(Path.Combine(directory,"sdk-layer-setup.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
        if(!success)throw new InvalidOperationException("SDK layer setup did not pass its own checks");
        return JsonConvert.SerializeObject(new{success,setLayers,setCollisions});
    }

    public static async Task<object> SdkBuildOnly()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open stopped Episode scene for SDK build");
        SaveWorkingScene();
        var panel=EditorWindow.GetWindow<VRCSdkControlPanel>();panel.Show();
        await Task.Delay(900);
        if(!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var builder))throw new InvalidOperationException("World SDK builder is not available");
        // A headless editor does not necessarily draw the Builder tab. Mount the
        // official builder through its public lifecycle before asking it to build.
        var worldBuilder=builder as VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder;
        if(worldBuilder==null)throw new InvalidOperationException("Unexpected World SDK builder implementation");
        worldBuilder.RegisterBuilder(panel);
        if(!worldBuilder.IsValidBuilder(out string validationMessage))throw new InvalidOperationException(validationMessage);
        worldBuilder.Initialize();
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");
        File.WriteAllText(Path.Combine(directory,"sdk-build-status.txt"),"Building local world bundle");
        try
        {
            string path=await builder.Build();
            if(!File.Exists(path))throw new InvalidOperationException("SDK returned no local bundle");
            string copy=Path.Combine(directory,"OWM_Episode123.vrcw");File.Copy(path,copy,true);
            var result=new{success=true,kind="local-sdk-build-only",path=copy,bytes=new FileInfo(copy).Length};
            File.WriteAllText(Path.Combine(directory,"sdk-build.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
            File.WriteAllText(Path.Combine(directory,"sdk-build-status.txt"),"completed");
            return JsonConvert.SerializeObject(result);
        }
        catch(Exception error)
        {
            var validationErrors=error.GetType().GetField("Errors")?.GetValue(error);
            File.WriteAllText(Path.Combine(directory,"sdk-build.json"),JsonConvert.SerializeObject(new{success=false,kind="local-sdk-build-only",error=error.GetType().Name,message=error.Message,validationErrors,details=error.ToString()},Formatting.Indented));
            File.WriteAllText(Path.Combine(directory,"sdk-build-status.txt"),"failed: "+error.GetType().Name+": "+error.Message);
            throw;
        }
    }

    public static async Task<object> LiveButtons()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(director);var buttons=Object.FindObjectsOfType<OWEpisodeButton>();
        var stageVm=UdonSharpEditorUtility.GetBackingUdonBehaviour(Object.FindObjectOfType<OWEpisodeStage>());var rows=new List<object>();
        vm.SendCustomEvent("StopAtStart");await Task.Delay(150);bool expectedToggle=true;
        foreach(int action in new[]{0,0,3,4,2,1})
        {
            var button=buttons.First(b=>b.action==action);var backing=UdonSharpEditorUtility.GetBackingUdonBehaviour(button);
            backing.SendCustomEvent("_interact");await Task.Delay(500);
            bool playing=Convert.ToBoolean(vm.GetProgramVariable("_playing"));int cue=Convert.ToInt32(stageVm.GetProgramVariable("sampledCue"));
            rows.Add(new{action,playing,cue});
            if(action==0){if(playing!=expectedToggle)throw new InvalidOperationException("World play/pause button did not toggle");expectedToggle=!expectedToggle;}
            if(action==3 && cue!=3 || action==4 && cue!=9 || action==2 && cue!=0 || action==1 && cue!=0)throw new InvalidOperationException("World button did not dispatch correctly");
        }
        vm.SendCustomEvent("StopAtStart");
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123");File.WriteAllText(Path.Combine(dir,"live-buttons.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));
        return JsonConvert.SerializeObject(new{success=true,interactionEvents=rows.Count,rows});
    }
    public static object PrepareImport()
    {
        if (!SessionState.GetBool("OWEpisodeImportsHeld", false))
        {
            AssetDatabase.DisallowAutoRefresh();
            SessionState.SetBool("OWEpisodeImportsHeld",true);
        }
        return "Episode batch import prepared";
    }

    public static object ResumeImport()
    {
        if (SessionState.GetBool("OWEpisodeImportsHeld",false))
        {
            AssetDatabase.AllowAutoRefresh();
            SessionState.SetBool("OWEpisodeImportsHeld",false);
        }
        AssetDatabase.Refresh();
        return "Episode batch import refreshed";
    }

    public static object Inspect()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i);
            return new { s.name, s.path, s.isDirty, s.isLoaded, roots = s.GetRootGameObjects().Select(g => g.name).ToArray() };
        }).ToArray();
        var actors = new[] { "Assets/OldWebActors/Cast/Actor_A.prefab", "Assets/OldWebActors/Cast/Actor_B.prefab" }.Select(path => {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var rig = p == null ? null : p.GetComponent<OldWebMap.OWActorRig>();
            var scale = p == null ? Vector3.zero : p.transform.localScale;
            return new { path, found = p != null, height = rig == null ? 0 : rig.modelHeight, scale = new [] { scale.x, scale.y, scale.z }, renderers = p == null ? 0 : p.GetComponentsInChildren<Renderer>(true).Length };
        }).ToArray();
        return JsonConvert.SerializeObject(new {
            scenes, actors, playing = Application.isPlaying,
            fonts = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).ToArray(),
            audio = AssetDatabase.FindAssets("t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).Take(12).ToArray(),
            descriptorType = typeof(VRC.SDK3.Components.VRCSceneDescriptor).FullName,
            activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
            stereoPath = PlayerSettings.stereoRenderingPath.ToString()
        });
    }
}
