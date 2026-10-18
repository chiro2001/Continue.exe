using System;
using System.IO;
using System.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

// Run through Pipeline. Every visible object is authored and saved in the scene.
public static class BuildSharedMemory
{
    const string Root = "Assets/OldWebMV";
    public const string ScenePath = Root + "/Scenes/SharedMemory.unity";
    static TMP_FontAsset font;
    static Material ink, slab, paper, cyan, lime, red, violet, muted;
    static readonly string[] Titles = {"01 / 共享 · THE COMMONS", "02 / 封闭 · LINK EXPIRED", "03 / 耗尽 · MAINTAINER WANTED", "04 / 边界 · A BODY IS NOT A FILE", "05 / 再连接 · FORK THE FUTURE"};
    static readonly string[] Lyrics = {
        "我把一个文件，交给从未见过的人。", "一条细细的线，连起彼此的房间。", "我们曾把分享，叫作一种美德。",
        "后来，门还在，钥匙却过期了。", "进度条停住的时候，等候也开始计价。", "那些失效的链接，曾经通向一个人。",
        "仓库还在，最后一次提交已很遥远。", "有人独自修补着，所有人走过的桥。", "请别把沉默，当成永不疲倦。",
        "我愿意与你相遇，不等于放弃自己的名字。", "可以分享的世界，也需要被尊重的边界。", "留下作者，留下选择，留下说不的权利。",
        "后来，新的工具，让旧门有了新的钥匙。", "把协议重新写下，把没有走完的路接上。", "带着名字，带着许可，让我们再连接一次。"
    };

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int at = path.LastIndexOf('/');
        Folder(path.Substring(0, at));
        AssetDatabase.CreateFolder(path.Substring(0, at), path.Substring(at + 1));
    }
    static Material Mat(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Unlit/Color")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; EditorUtility.SetDirty(m); return m;
    }
    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material m, bool collision = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collision) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    static TextMeshPro Text(Transform p, string name, string content, Vector3 pos, float size, Color color, float width = 12)
    {
        var g = new GameObject(name); g.transform.SetParent(p, false); g.transform.localPosition = pos;
        var t = g.AddComponent<TextMeshPro>(); t.font = font; t.fontSize = size;
        t.color = color; t.text = content; t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(width, 1.1f); t.enableWordWrapping = false;
        t.richText = true; return t;
    }
    static void Wire(Transform p, string name, Vector3 a, Vector3 b, Material m, float radius = .027f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name;
        g.transform.SetParent(p, false); g.transform.localPosition = (a + b) * .5f;
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        g.transform.localScale = new Vector3(radius, (b - a).magnitude * .5f, radius);
        g.GetComponent<Renderer>().sharedMaterial = m; Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    static void Window(Transform p, string name, Vector3 pos, string caption, string body, Material accent, float scale = 1)
    {
        var g = new GameObject(name); g.transform.SetParent(p, false); g.transform.localPosition = pos;
        g.transform.localScale = Vector3.one * scale;
        Box(g.transform, "Extruded frame", Vector3.zero, new Vector3(4.4f, 2.8f, .25f), muted);
        Box(g.transform, "Interior", new Vector3(0, -.14f, -.15f), new Vector3(4.16f, 2.28f, .08f), ink);
        Box(g.transform, "Title bar", new Vector3(0, 1.14f, -.17f), new Vector3(4.16f, .4f, .08f), accent);
        Text(g.transform, "Caption", caption, new Vector3(-.12f, 1.13f, -.24f), .21f, Color.white, 3.8f);
        Text(g.transform, "Content", body, new Vector3(0, -.04f, -.26f), .32f, new Color(.85f, .9f, .9f), 4);
        Box(g.transform, "Window close", new Vector3(1.83f, 1.14f, -.24f), new Vector3(.24f,.24f,.03f), paper);
    }
    static void Node(Transform p, string name, Vector3 pos, Material m, float size = .45f)
    {
        Box(p, name, pos, Vector3.one * size, m);
        Box(p, name + " pedestal", new Vector3(pos.x, .42f, pos.z), new Vector3(.65f,.85f,.65f), slab);
    }
    static void MakeFont()
    {
        string path = Root + "/Fonts/SharedMemory-SDF.asset";
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return;
        var source = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansSC-Regular.ttf");
        if (source == null) throw new Exception("Import NotoSansSC font before building");
        font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        // Include all the text in this source and the runtime controller, then freeze the atlas.
        string chars = File.ReadAllText("AgentScripts/BuildSharedMemory.cs") + File.ReadAllText(Root + "/Scripts/SharedMemoryDirector.cs");
        chars = new string(chars.Where(c => !char.IsControl(c)).Distinct().ToArray());
        font.TryAddCharacters(chars, out string missing);
        if (!string.IsNullOrEmpty(missing)) throw new Exception("Missing font glyphs: " + missing);
        font.name = "SharedMemory-SDF"; font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path); AssetDatabase.AddObjectToAsset(font.material, path);
        foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, path);
        EditorUtility.SetDirty(font);
    }
    static void Save(UdonSharpBehaviour b)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(b));
    }
    public static object Compile()
    {
        Folder(Root + "/Programs");
        foreach (var name in new[]{"SharedMemoryDirector", "SharedMemoryButton"})
        {
            string path = Root + "/Programs/" + name + ".asset";
            var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            if (program == null) { program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); AssetDatabase.CreateAsset(program, path); }
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(Root + "/Scripts/" + name + ".cs");
            if (program.sourceCsScript == null) throw new Exception("Missing script: " + name);
            EditorUtility.SetDirty(program);
        }
        AssetDatabase.SaveAssets(); UdonSharpProgramAsset.CompileAllCsPrograms(true, true);
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("UdonSharp compilation failed");
        return new {compiled = true};
    }
    public static object Build()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Stop PlayMode / finish compilation first");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Existing unsaved scene must be preserved");
        // Never recreate an existing authored scene and erase later hand edits.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new Exception("Scene already exists; use targeted edits instead of rebuilding");
        foreach (var path in new[]{"Scenes","Fonts","Materials"}) Folder(Root + "/" + path);
        MakeFont();
        ink = Mat("Ink", new Color(.022f,.034f,.05f)); slab = Mat("Slab", new Color(.055f,.08f,.10f));
        paper = Mat("Paper", new Color(.84f,.9f,.88f)); muted = Mat("Muted", new Color(.2f,.28f,.32f));
        cyan = Mat("Cyan", new Color(.08f,.82f,.82f)); lime = Mat("Lime", new Color(.68f,.95f,.28f));
        red = Mat("Coral", new Color(.98f,.28f,.23f)); violet = Mat("Violet", new Color(.6f,.46f,.96f));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("SharedMemoryWorld").transform;
        RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientLight = new Color(.2f,.25f,.28f);
        Box(root, "Walkable auditorium", new Vector3(0,-.25f,1), new Vector3(25,.5f,32), ink, true);
        Box(root, "Viewing platform", new Vector3(0,-.04f,-4), new Vector3(16,.08f,12), slab, true);
        for (int i = -8; i <= 8; i++)
        {
            Box(root, "Floor longitude " + i, new Vector3(i * 1.3f,.01f,4), new Vector3(.012f,.012f,25), muted);
            Box(root, "Floor latitude " + i, new Vector3(0,.012f,i * 1.5f + 4), new Vector3(23,.012f,.012f), muted);
        }
        // Invisible perimeter keeps users on the platform without moving their viewpoint.
        foreach (float x in new[]{-12.5f,12.5f}) Box(root,"Side boundary",new Vector3(x,1,1),new Vector3(.15f,2,32),ink,true).GetComponent<Renderer>().enabled=false;
        foreach (float z in new[]{-15f,17f}) Box(root,"End boundary",new Vector3(0,1,z),new Vector3(25,2,.15f),ink,true).GetComponent<Renderer>().enabled=false;
        Box(root, "Stage plinth", new Vector3(0,.18f,10), new Vector3(18,.36f,10), slab, true);
        Box(root, "Luminous stage lip", new Vector3(0,.38f,5), new Vector3(18,.04f,.035f), cyan);
        Text(root,"Kicker","AN INTERNET ELEGY / VRCHAT SPATIAL MV",new Vector3(0,9,13),.25f,cyan.color,20);
        Text(root,"Main title","致每一条还愿意连接的线",new Vector3(0,8.1f,13),.68f,paper.color,23);
        var title = Text(root,"Chapter title",Titles[0],new Vector3(0,6.9f,12),.32f,lime.color,20);
        var lyrics = Text(root,"Lyric line",Lyrics[0],new Vector3(0,1.55f,4.4f),.34f,paper.color,14);
        var status = Text(root,"Transport status","静音预演 / 原始音源待核实",new Vector3(0,1.0f,4.4f),.18f,cyan.color,14);
        Text(root,"Concept note","共享 / 封闭 / 耗尽 / 边界 / 再连接",new Vector3(0,.65f,4.4f),.17f,muted.color,14);
        // Five authored scene variants share one stage; no runtime instantiation.
        var acts = new Transform[5];
        for (int i = 0; i < acts.Length; i++)
        {
            acts[i] = new GameObject("Act " + (i+1) + " - " + Titles[i]).transform; acts[i].SetParent(root,false);
        }
        Window(acts[0],"Personal homepage",new Vector3(0,3.7f,10),"home / welcome.html","HELLO, STRANGER.\n[ view source ]",cyan,1.35f);
        Window(acts[0],"Left share",new Vector3(-6,3.2f,11.8f),"public / share","COPY -> SHARE\n留一份给下一个人",cyan,.72f);
        Window(acts[0],"Right fork",new Vector3(6,3.9f,12.8f),"community / fork","OPEN DIRECTORY\n从这里，去任何地方",lime,.72f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2 / 12; var pos = new Vector3(Mathf.Cos(a)*7,1.2f+(i%3)*.65f,10+Mathf.Sin(a)*3.7f);
            Node(acts[0],"Shared node " + i,pos,i%3==0?lime:cyan,.3f);
            Wire(acts[0],"Peer connection " + i,pos,new Vector3(0,3.2f,10),i%3==0?lime:cyan);
        }
        Window(acts[1],"Expired link",new Vector3(-2.6f,3.9f,10),"share / access denied","LINK EXPIRED\n分享的文件已失效",red,1.15f);
        Window(acts[1],"Rate limit",new Vector3(4.1f,3.2f,12),"download / waiting","99%\n等候，也开始计价",muted,.9f);
        for(int i=0;i<7;i++) Box(acts[1],"Closed gate " + i,new Vector3(i*1.55f-4.6f,2,7.2f),new Vector3(.18f,3.2f,.3f),red);
        Box(acts[1],"Progress track",new Vector3(3.9f,1.4f,9),new Vector3(4,.22f,.18f),muted);
        Box(acts[1],"Frozen progress",new Vector3(3.5f,1.4f,8.86f),new Vector3(3.1f,.18f,.12f),red);
        Window(acts[2],"Archived repository",new Vector3(0,4.1f,11),"repository / read only","ARCHIVED\nMAINTAINER WANTED",violet,1.25f);
        Node(acts[2],"One maintainer",new Vector3(0,1.4f,8),violet,.7f);
        for(int i=0;i<9;i++)
        {
            var pos=new Vector3(i*1.5f-6,3.3f+Mathf.Abs(i-4)*.28f,13);
            Node(acts[2],"Dependent " + i,pos,muted,.4f); Wire(acts[2],"Unpaid dependency " + i,new Vector3(0,1.4f,8),pos,violet);
        }
        Text(acts[2],"Bridge inscription","谁在维护，我们习以为常的路？",new Vector3(0,5.95f,10),.3f,paper.color,14);
        Window(acts[3],"Consent boundary",new Vector3(-4.6f,4.2f,12),"avatar / author.txt","VISIBLE != GIVEN\n保留名字，保留选择",red,.9f);
        Window(acts[3],"Permission",new Vector3(4.9f,3.5f,12),"permissions / ask first","SHARE WITH CONSENT\n可以说不",cyan,.85f);
        // An abstract, deliberately original body, never a copied player avatar.
        Box(acts[3],"Body",new Vector3(0,2.8f,9),new Vector3(.85f,1.2f,.6f),paper);
        Box(acts[3],"Head",new Vector3(0,3.85f,9),Vector3.one*.65f,cyan);
        foreach(float x in new[]{-.3f,.3f}) Box(acts[3],"Leg",new Vector3(x,1.6f,9),new Vector3(.26f,1.1f,.3f),paper);
        foreach(float x in new[]{-.72f,.72f}) Box(acts[3],"Arm",new Vector3(x,2.75f,9),new Vector3(.28f,1.1f,.3f),paper);
        for(int i=0;i<4;i++) {float x=i%2==0?-1.5f:1.5f;float y=i<2?1:4.7f;Box(acts[3],"Selection corner",new Vector3(x,y,8.3f),new Vector3(.5f,.08f,.1f),red);}
        Text(acts[3],"Boundary label","相遇，不等于拥有。",new Vector3(0,5.6f,10),.38f,paper.color,12);
        Window(acts[4],"Future fork",new Vector3(0,4.2f,11),"community / next","FORK THE FUTURE\n带着名字与许可，再连接",lime,1.25f);
        for(int i=0;i<11;i++)
        {
            var pos=new Vector3(i*1.35f-6.75f,1.4f+Mathf.Sin(i*.7f)*.35f,8+Mathf.Cos(i)*1.5f);
            Node(acts[4],"New peer " + i,pos,i%2==0?lime:cyan,.34f);
            if(i>0) Wire(acts[4],"Reimplemented protocol " + i,pos,new Vector3((i-1)*1.35f-6.75f,1.4f+Mathf.Sin((i-1)*.7f)*.35f,8+Mathf.Cos(i-1)*1.5f),lime,.055f);
        }
        Text(acts[4],"Hope note","新的工具，降低门槛；接下去的路，仍由人选择。",new Vector3(0,5.95f,10),.25f,paper.color,18);
        var director = new GameObject("SharedMemoryDirector").AddUdonSharpComponent<SharedMemoryDirector>();
        director.transform.SetParent(root,false); director.chapterRoots=acts; director.chapterTitles=Titles;
        director.titleText=title; director.lyricText=lyrics; director.statusText=status;
        director.duration=180; director.chapterStarts=new float[]{0,36,72,108,144};
        director.lyrics=Lyrics; director.lyricStarts=Enumerable.Range(0,Lyrics.Length).Select(i=>i*12f).ToArray();
        director.music=director.gameObject.AddComponent<AudioSource>(); director.music.playOnAwake=false; director.music.spatialBlend=0; director.music.volume=.55f;
        var indicators = new Renderer[5];
        for (int i=0;i<5;i++) indicators[i]=Box(root,"Chapter lamp " + i,new Vector3(i*.5f-1,6.35f,11),new Vector3(.32f,.05f,.05f),i==0?lime:muted).GetComponent<Renderer>();
        director.chapterIndicators=indicators;
        // Transport is a physical control desk, reachable from the spawn.
        Box(root,"Transport desk",new Vector3(0,.78f,-3),new Vector3(4.5f,.22f,.8f),slab,true);
        string[] labels={"播放","暂停","重播","上一章","下一章"};
        for(int i=0;i<5;i++)
        {
            var button=Box(root,"Control " + labels[i],new Vector3(i*.84f-1.68f,1.01f,-3),new Vector3(.75f,.21f,.5f),i==0?cyan:muted,true);
            var b=button.AddUdonSharpComponent<SharedMemoryButton>(); b.director=director; b.action=i;
            b.GetComponent<VRC.Udon.UdonBehaviour>().interactText=labels[i]; b.GetComponent<VRC.Udon.UdonBehaviour>().proximity=3;
            // Parent text outside scaled box so font size remains world-scale.
            Text(root,"Button label " + i,labels[i],new Vector3(i*.84f-1.68f,1.22f,-3.15f),.17f,paper.color,1);
            Save(b);
        }
        Text(root,"Desk note","控制台 / 全场同步 · 首版静音预演",new Vector3(0,1.55f,-3),.18f,cyan.color,8);
        var world=new GameObject("VRCWorld");world.transform.SetParent(root,false);var descriptor=world.AddComponent<VRCSceneDescriptor>();world.AddComponent<VRC.Core.PipelineManager>();
        var spawn=new GameObject("Spawn").transform;spawn.SetParent(root,false);spawn.position=new Vector3(0,.06f,-5.5f);descriptor.spawns=new[]{spawn};
        var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.SetParent(root,false);
        camera.transform.position=new Vector3(0,3,-10.8f);camera.transform.LookAt(new Vector3(0,3.9f,10));camera.fieldOfView=52;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ink.color;camera.nearClipPlane=.03f;camera.farClipPlane=70;
        camera.gameObject.AddComponent<AudioListener>();
        for(int i=0;i<5;i++) acts[i].gameObject.SetActive(i==0);
        Save(director); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
        return Inspect();
    }
    public static object Inspect()
    {
        var d=Object.FindObjectOfType<SharedMemoryDirector>();
        if(d==null)throw new Exception("Director missing");
        var scripts=Object.FindObjectsOfType<UdonSharpBehaviour>(true);
        bool bound=scripts.All(b=>UdonSharpEditorUtility.GetBackingUdonBehaviour(b)!=null);
        bool refs=d.chapterRoots.Length==5&&d.chapterRoots.All(x=>x!=null)&&d.titleText!=null&&d.lyricText!=null&&d.statusText!=null;
        if(!bound||!refs)throw new Exception("Scene references incomplete");
        return new {scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,chapters=d.chapterRoots.Length,buttons=Object.FindObjectsOfType<SharedMemoryButton>(true).Length,udonBound=bound,lyrics=d.lyrics.Length,musicAssigned=d.music.clip!=null,renderers=Object.FindObjectsOfType<Renderer>(true).Length};
    }
    // Edit-mode review only: unsaved preview state is reset before saving/snapshot.
    public static object Preview(float time)
    {
        var d=Object.FindObjectOfType<SharedMemoryDirector>();d.PreviewAt(time);
        return new {time,chapter=d.titleText.text,lyric=d.lyricText.text};
    }
}
