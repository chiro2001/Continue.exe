using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using OldWebMap;
using UdonSharp;
using UdonSharpEditor;
using Object=UnityEngine.Object;

public static class OWEpisodeV3Operations
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v3.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision3/Operations";
    const string ScorePath="AgentScripts/data/episode123-v3-score.json";
    // ExCSS also defines System.Tuple in this Editor; keep score hits local.
    struct NoteHit
    {
        public float time;
        public int channel;
        public NoteHit(float time,int channel){this.time=time;this.channel=channel;}
    }
    static TMP_FontAsset Font;
    static JObject Score;
    static readonly string[] ChannelColors={"#53d2e2","#edbd67","#bf90ef","#86cf83"};
    static string ReportDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v3/operations");
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');if(i<0)throw new InvalidOperationException(path);Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static Color Hex(string html){Color color;ColorUtility.TryParseHtmlString(html,out color);return color;}
    static Material Mat(string name,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find("OldWebMap/V3/Operation Crop");
        if(shader==null || !shader.isSupported)throw new InvalidOperationException("Operation crop shader missing/unsupported");
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
        mat.SetColor("_Tint",color);mat.SetVector("_Region",new Vector4(0,0,1,1));mat.SetFloat("_Opacity",1);mat.SetFloat("_Shape",0);mat.SetFloat("_Highlight",0);
        // Source crops, paper, title bars, frames and solid parts use actual
        // depth, independent of centre-distance transparent sorting.
        mat.renderQueue=2020;mat.SetOverrideTag("RenderType","Opaque");
        mat.SetFloat("_SrcBlend",(float)BlendMode.One);mat.SetFloat("_DstBlend",(float)BlendMode.Zero);
        mat.SetFloat("_ZWrite",1);mat.SetFloat("_Cutoff",.001f);
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    static Material TransparentMat(string name,Color color)
    {
        var mat=Mat(name,color);mat.renderQueue=3010;mat.SetOverrideTag("RenderType","Transparent");
        mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite",0);EditorUtility.SetDirty(mat);return mat;
    }
    static JToken Image(string id)
    {
        var image=((JArray)Score["images"]).FirstOrDefault(i=>(string)i["id"]==id);
        if(image==null)throw new InvalidOperationException("Required real source missing from score: "+id);return image;
    }
    static Material Crop(string name,string id,int x,int y,int width,int height)
    {
        var image=Image(id);float fullWidth=(float)image["width"],fullHeight=(float)image["height"];
        if(x<0 || y<0 || x+width>fullWidth || y+height>fullHeight)throw new InvalidOperationException("Crop exceeds source: "+id+" / "+name);
        var mat=Mat(name,Color.white);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>((string)image["path"]);
        if(texture==null)throw new FileNotFoundException("Source image is not imported",(string)image["path"]);
        mat.mainTexture=texture;mat.SetVector("_Region",new Vector4(x/fullWidth,1-(y+height)/fullHeight,width/fullWidth,height/fullHeight));return mat;
    }
    static void RenderStyle(Renderer renderer){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static Transform Prim(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;RenderStyle(renderer);return go.transform;
    }
    static Transform Panel(string name,Transform parent,Vector3 position,float width,float height,Material material)
    {return Prim(name,PrimitiveType.Quad,parent,position,new Vector3(width,height,1),material);}
    static TMP_Text Text(string name,string text,Transform parent,Vector3 position,float width,float height,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
    {
        var t=Child(name,parent).AddComponent<TextMeshPro>();t.font=Font;t.fontSharedMaterial=Font.material;t.text=text;t.fontSize=size;t.color=color;
        // TMP Ellipsis suppresses the entire line when its full line box does
        // not fit, even if the visible glyph ink would fit the requested height.
        float lineBox=size*(Font.faceInfo.lineHeight/Mathf.Max(1,Font.faceInfo.pointSize))*.1f*1.2f;
        t.transform.localPosition=position;t.rectTransform.sizeDelta=new Vector2(width,Mathf.Max(height,lineBox));t.alignment=alignment;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    static void PersistFontChildren(TMP_FontAsset font)
    {
        if(font.material!=null)
        {
            if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
            EditorUtility.SetDirty(font.material);
        }
        // Text draws after translucent selections, with ordinary world depth.
        if(font.material!=null)
        {
            font.material.renderQueue=3020;
            if(font.material.HasProperty("_ZTestMode"))font.material.SetFloat("_ZTestMode",(float)CompareFunction.LessEqual);
            if(font.material.HasProperty("_ZTest"))font.material.SetFloat("_ZTest",(float)CompareFunction.LessEqual);
            if(font.material.HasProperty("_ZWrite"))font.material.SetFloat("_ZWrite",0);
            EditorUtility.SetDirty(font.material);
        }
        var atlases=font.atlasTextures;
        if(atlases!=null)for(int i=0;i<atlases.Length;i++)
        {
            var atlas=atlases[i];if(atlas==null)continue;
            if(!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
            EditorUtility.SetDirty(atlas);
        }
        EditorUtility.SetDirty(font);
    }
    static void PrepareFont(OWEpisodeDirector director,string text)
    {
        Folder(Root+"/Fonts");
        // Never clone a populated multi-atlas TMP asset. Its hidden atlas packing
        // state can reference absent children and fail in SetupNewAtlasTexture.
        // This new owned path also avoids reusing the failed first-build clone.
        string path=Root+"/Fonts/OperationsChineseDynamic4096.asset";
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(Font==null)
        {
            TMP_FontAsset reference=director.stage!=null && director.stage.captionText!=null?director.stage.captionText.font:null;
            UnityEngine.Font source=reference!=null?reference.sourceFontFile:null;
            if(source==null)source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
            if(source==null)throw new InvalidOperationException("A real source TTF is required for the independent Operations font");
            Font=TMP_FontAsset.CreateFontAsset(source,64,7,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,false);
            if(Font==null || Font.material==null || Font.atlasTextures==null || Font.atlasTextures.Length!=1 || Font.atlasTextures[0]==null)
                throw new InvalidOperationException("Fresh TMP font did not initialize a material and atlas");
            Font.name="OperationsChineseDynamic4096";
            Font.isMultiAtlasTexturesEnabled=false;
            AssetDatabase.CreateAsset(Font,path);
            // Register the initial material/atlas before TMP begins glyph filling;
            // after filling, persist again in case the atlas texture was resized.
            PersistFontChildren(Font);
        }
        if(Font.atlasPopulationMode!=AtlasPopulationMode.Dynamic || Font.sourceFontFile==null || Font.atlasWidth!=4096 || Font.atlasHeight!=4096)
            throw new InvalidOperationException("Operations owned font is not the expected fresh 4096 dynamic asset");
        Font.isMultiAtlasTexturesEnabled=false;
        string visible=new string(text.Where(c=>!char.IsControl(c)).Distinct().ToArray());
        string required=new string(visible.Where(c=>!Font.HasCharacter(c)).ToArray());
        if(required.Length>0)
        {
            Font.TryAddCharacters(required,out string missing);
            if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Operations source TTF or single atlas cannot cover required glyphs: "+missing);
        }
        string absent=new string(visible.Where(c=>!Font.HasCharacter(c)).ToArray());
        if(absent.Length>0)throw new InvalidOperationException("Operations glyph prewarm failed: "+absent);
        PersistFontChildren(Font);
        if(Font.material==null || !AssetDatabase.Contains(Font.material) || Font.atlasTextures.Any(a=>a==null || !AssetDatabase.Contains(a)))
            throw new InvalidOperationException("Operations font material/atlas children were not persisted");
        AssetDatabase.SaveAssets();
    }
    static Mesh MeshAsset(string name,Vector3[] vertices,int[] triangles)
    {
        string path=Root+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=vertices.Select(v=>new Vector2(v.x+.5f,v.y+.5f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Transform Pointer(Transform parent,Material material)
    {
        Vector3[] vertices={new Vector3(-.3f,.5f,0),new Vector3(-.3f,-.4f,0),new Vector3(-.08f,-.18f,0),new Vector3(.09f,-.5f,0),new Vector3(.25f,-.41f,0),new Vector3(.07f,-.09f,0),new Vector3(.4f,-.09f,0)};
        var mesh=MeshAsset("Persistent operation pointer",vertices,new[]{0,1,2,0,2,5,0,5,6,2,3,4,2,4,5});
        var go=Child("Operation pointer",parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;RenderStyle(renderer);go.transform.localScale=Vector3.one*.095f;return go.transform;
    }
    static Transform FileTile(string name,Transform parent,Material body,Material fold,Vector3 position,float scale)
    {
        var root=Child(name,parent).transform;root.localPosition=position;root.localScale=Vector3.one*scale;
        Prim("File has real thickness",PrimitiveType.Cube,root,Vector3.zero,new Vector3(.23f,.3f,.045f),body);
        Prim("Folded top corner",PrimitiveType.Cube,root,new Vector3(.073f,.105f,-.032f),new Vector3(.06f,.06f,.014f),fold).localRotation=Quaternion.Euler(0,0,45);
        for(int i=0;i<4;i++)Prim("Embossed text line "+i,PrimitiveType.Cube,root,new Vector3(-.008f,.04f-i*.04f,-.031f),new Vector3(.14f-i*.015f,.009f,.009f),fold);
        return root;
    }
    static void BuildExplorer(OWV3Operations op,Transform root)
    {
        var body=Mat("Win98 warm grey bevel",Hex("#c6c6c6"));var white=Mat("Readme white paper",Hex("#f8f7ef"));var blue=Mat("Win98 title blue",Hex("#000080"));var ink=Mat("Readme file fold ink",Hex("#4a7396"));
        var baseRoot=Child("Win98 true source split into working parts",root).transform;baseRoot.localPosition=new Vector3(-.45f,0,.05f);
        var parts=new List<Transform>();
        // These rectangles come from the real 1280x1024 Windows 98 Explorer image.
        const string image="windows98-se-explorer";float factor=2.2f/1280;
        parts.Add(Prim("Real bevel backing with depth",PrimitiveType.Cube,baseRoot,new Vector3(0,0,.13f),new Vector3(2.24f,1.8f,.045f),body));
        parts.Add(Panel("Real title bar",baseRoot,new Vector3(0,.86f,-.15f),2.2f,24*factor,Crop("Win98 source title",image,0,0,1280,24)));
        parts.Add(Panel("Real menus tools and address",baseRoot,new Vector3(0,.76f,-.08f),2.2f,92*factor,Crop("Win98 source tools",image,0,24,1280,92)));
        parts.Add(Panel("Real directory tree",baseRoot,new Vector3(-.926f,-.13f,-.04f),202*factor,864*factor,Crop("Win98 source directory",image,0,116,202,864)));
        parts.Add(Panel("Real file content",baseRoot,new Vector3(.174f,-.13f,.015f),1078*factor,864*factor,Crop("Win98 source contents",image,202,116,1078,864)));
        parts.Add(Panel("Real source status bar",baseRoot,new Vector3(0,-.87f,-.08f),2.2f,44*factor,Crop("Win98 source status",image,0,980,1280,44)));
        op.explorerParts=parts.ToArray();op.explorerPositions=parts.Select(p=>p.localPosition).ToArray();
        var selectionMaterial=TransparentMat("README selected blue",new Color(.04f,.16f,.63f,.42f));
        op.selection=Panel("Selected README source file target",root,new Vector3(-.12f,-.13f,-.13f),.34f,.34f,selectionMaterial).gameObject;
        var tile=Child("README draggable extracted file tile",root).transform;tile.localPosition=op.fileHome;
        // License is a real text-file icon in this Explorer reference. Its crop is
        // reused as an icon; README label and document below are project originals.
        Panel("Real Windows text file icon crop",tile,new Vector3(0,.05f,-.015f),.12f,.15f,Crop("Win98 original text file icon",image,1099,201,18,18));
        Text("Extracted filename","README.TXT",tile,new Vector3(0,-.09f,-.04f),.55f,.1f,.7f,Hex("#132b4a"));op.fileTile=tile;
        op.filePointer=Pointer(root,Mat("Explorer raised white pointer",Color.white));
        op.clickMaterial=TransparentMat("Explorer double click ring",new Color(.9f,.67f,.2f,1));op.clickMaterial.SetFloat("_Shape",1);op.clickRing=Panel("Selection and double click feedback",root,Vector3.zero,.2f,.2f,op.clickMaterial);
        var page=Child("README opens as native Notepad parts",root).transform;op.readmePage=page;
        Prim("Notepad paper thickness",PrimitiveType.Cube,page,new Vector3(0,0,.03f),new Vector3(1.55f,1.14f,.04f),body);
        Panel("Notepad body white page",page,new Vector3(0,-.025f,-.01f),1.49f,.91f,white);
        Panel("Native Notepad blue title",page,new Vector3(0,.51f,-.025f),1.49f,.1f,blue);
        Text("Opened document title","README.TXT — 记事本",page,new Vector3(0,.51f,-.055f),1.43f,.09f,.65f,Color.white);
        Text("Notepad menu","文件  编辑  搜索  帮助",page,new Vector3(-.05f,.405f,-.045f),1.32f,.08f,.59f,Hex("#243346"),TextAlignmentOptions.Left);
        op.readmeBody=Text("The opened readable original document","",page,new Vector3(0,-.02f,-.055f),1.34f,.66f,.72f,Hex("#203140"),TextAlignmentOptions.TopLeft);
        op.readmeContent="把说明和作品一起留下。\n\n作者：本项目原创\n你可以阅读、校对、继续传递。\n谢谢把它带给下一位。";
        var result=Text("README operation has a result","✓ README 已打开 · 说明随作品保存",page,new Vector3(0,-.48f,-.065f),1.43f,.08f,.62f,Hex("#21814c"));op.readmeResult=result.gameObject;
        Text("Historical source and native document distinction","Win98 原图拆层 / README 为项目原创演示",root,new Vector3(-.38f,-.9f,-.22f),3.2f,.1f,.6f,Hex("#a9c2d3"));
    }
    static void BuildSubtitles(OWV3Operations op,Transform root)
    {
        var parts=new List<Transform>();const string source="fansub-51517e462c93";
        parts.Add(Panel("Real Aegisub v2.2 title menus",root,new Vector3(0,.77f,.05f),3.05f,76*3.05f/920,Crop("Aegisub real title and tools",source,0,0,920,76)));
        parts.Add(Panel("Real Aegisub dummy-video preview and visual handles",root,new Vector3(-1.15f,.25f,-.11f),.78f,.61f,Crop("Aegisub real visual preview","fansub-acae7d9029f6",0,0,349,273)));
        var black=Mat("Aegisub real time audio black",Hex("#071015"));parts.Add(Panel("Actual music waveform area",root,new Vector3(.42f,.44f,-.1f),2.12f,.40f,black));
        parts.Add(Panel("Real Aegisub audio ruler and transport controls",root,new Vector3(.42f,.11f,-.13f),2.12f,92*2.12f/815,Crop("Aegisub real ruler transport",source,16,198,815,92)));
        parts.Add(Panel("Real editing-strip settings",root,new Vector3(0,-.105f,-.23f),3.05f,55*3.05f/900,Crop("Aegisub real translation header",source,8,300,900,55)));
        parts.Add(Panel("Real timeline first six rows",root,new Vector3(0,-.65f,-.26f),3.05f,120*3.05f/900,Crop("Aegisub real subtitle timeline",source,8,444,900,120)));
        op.subtitleParts=parts.ToArray();op.subtitlePositions=parts.Select(p=>p.localPosition).ToArray();
        var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldWebMap/Episode123/Revision3/Data/waveform_rgba.png");
        var ribbon=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/OldWebMap/Episode123/Revision3/Meshes/Triggered waveform ribbon.asset");
        if(atlas==null || ribbon==null)throw new InvalidOperationException("Build AudioFx first: actual waveform atlas and persisted ribbon required");
        op.miniWaves=new Transform[4];op.miniWaveMaterials=new Material[4];
        for(int ch=0;ch<4;ch++)
        {
            string path=Root+"/Materials/Aegisub actual CH0"+(ch+1)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("OldWebMap/V3/Triggered Oscilloscope"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_WaveAtlas",atlas);mat.SetFloat("_Channel",ch);mat.SetColor("_Color",Hex(ChannelColors[ch]));mat.SetFloat("_Length",1.96f);mat.SetFloat("_Amplitude",.22f);mat.SetFloat("_Width",.008f);mat.SetFloat("_Gain",.9f);
            mat.SetFloat("_Bend",0);mat.SetFloat("_Curl",0);mat.SetFloat("_Twist",0);mat.SetFloat("_Constrain",0);EditorUtility.SetDirty(mat);
            var go=Child("Independent actual audio CH0"+(ch+1),root);go.transform.localPosition=new Vector3(.42f,.575f-ch*.09f,-.28f);go.AddComponent<MeshFilter>().sharedMesh=ribbon;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;RenderStyle(renderer);op.miniWaves[ch]=go.transform;op.miniWaveMaterials[ch]=mat;
        }
        op.audioCursor=Panel("Audio playhead on actual music",root,Vector3.zero,.013f,.4f,Mat("Aegisub audible playhead",Hex("#ffe06e")));
        var paper=Mat("Translation native paper",Hex("#f5f6ec"));Panel("Native working translation layer",root,new Vector3(0,-.295f,-.38f),3.05f,.23f,paper);
        op.sourceLine=Text("Source line being translated","",root,new Vector3(0,-.225f,-.45f),2.93f,.07f,.64f,Hex("#566779"),TextAlignmentOptions.Left);
        op.translationLine=Text("Typed and corrected native translation","",root,new Vector3(0,-.325f,-.45f),2.93f,.1f,.86f,Hex("#183e54"),TextAlignmentOptions.Left);
        op.correctionLine=Text("Visible correction outcome","",root,new Vector3(0,-.435f,-.52f),3.1f,.08f,.65f,Hex("#88e6c0"));
        op.subtitleRow=Panel("Chosen actual timeline row",root,Vector3.zero,2.96f,.047f,TransparentMat("Timeline selection transparent",new Color(.05f,.3f,.86f,.26f)));
        op.subtitleSelectionMaterial=TransparentMat("Translation character selection",new Color(.1f,.5f,.9f,.5f));op.subtitleSelection=Panel("Selection caret advances at 80ms",root,Vector3.zero,.035f,.13f,op.subtitleSelectionMaterial);
        op.subtitlePointer=Pointer(root,Mat("Subtitle editor pointer",Color.white));
        op.sourceLines=new[]{"Let's make a little thing together.","I'll check this line.","Keep the names consistent.","Leave enough reading time.","Does this cover the picture?","Move the start by eighty milliseconds.","Preview it once more.","Now share it with everyone."};
        op.translatedLines=new[]{"今天也一起做一点。","这句我来校对。","把专有明词统一。","留出足够的阅读时间。","这句会遮住画面吗？","入点前移八十毫秒。","修改后再预览一次。","让下一位也能看懂。"};
        op.correctedLines=new[]{"今天也一起做一点。","这句我来校对。","把专有名词统一。","留出足够的阅读时间。","这句会遮住画面吗？","入点前移 80 毫秒。","修改后再预览一次。","让下一位也能看懂。"};
        op.subtitleOutput=FileTile("Exported ASS file leaves the editing layer",root,paper,Mat("ASS blue file fold",Hex("#4296c1")),Vector3.zero,1);
        Text("Exported subtitle filename","movie.zh-cn.ass",op.subtitleOutput,new Vector3(0,-.23f,-.04f),.8f,.08f,.62f,Hex("#b4e9e6"));
        op.subtitleResult=Text("The correction and export result","✓ 8 行校对 / UTF-8 ASS 已导出",root,new Vector3(0,-.91f,-.48f),3.2f,.09f,.7f,Hex("#88e6c0")).gameObject;
    }
    static void BuildTorrent(OWV3Operations op,Transform root)
    {
        op.nodes=new Transform[4];op.nodeMaterials=new Material[4];op.nodeLabels=new TMP_Text[4];
        op.nodePositions=new[]{new Vector3(-1.55f,.43f,.03f),new Vector3(1.55f,.43f,.03f),new Vector3(-1.55f,-.4f,-.06f),new Vector3(1.55f,-.4f,-.06f)};
        var metal=Mat("BT real node casing",Hex("#546c80"));var white=TransparentMat("BT original complete-file paper",Hex("#ecf5eb"));var blue=Mat("BT file folded corner",Hex("#5683a6"));
        for(int ch=0;ch<4;ch++)
        {
            var node=Child("Native peer node "+(ch+1),root).transform;node.localPosition=op.nodePositions[ch];op.nodes[ch]=node;
            Prim("Peer display casing",PrimitiveType.Cube,node,Vector3.zero,new Vector3(.54f,.32f,.16f),metal);
            var mat=Mat("BT node "+ch+" actual voice accent",Hex(ChannelColors[ch]));op.nodeMaterials[ch]=mat;
            Panel("Peer screen",node,new Vector3(0,0,-.09f),.47f,.24f,mat);
            Prim("Peer machine stand",PrimitiveType.Cube,node,new Vector3(0,-.22f,.015f),new Vector3(.26f,.06f,.25f),metal);
            op.nodeLabels[ch]=Text("Node contribution count","",node,new Vector3(0,.25f,-.12f),1.05f,.1f,.67f,Hex("#e7f0f6"));
            Text("Sustained voice is not a fabricated beat",ch==1?"持续音 · 节点继续提供":"真实起音触发传递",node,new Vector3(0,-.3f,-.12f),1.05f,.08f,.52f,Hex("#9fc1d0"));
            // Native thin bridge from peer to file; packets travel above the bridge.
            Vector3 target=new Vector3(ch%2==0?-.5f:.5f,0,-.08f),delta=target-op.nodePositions[ch];
            var link=Prim("Peer-to-file physical link",PrimitiveType.Cylinder,root,(target+op.nodePositions[ch])*.5f,new Vector3(.006f,delta.magnitude*.5f,.006f),metal);link.localRotation=Quaternion.FromToRotation(Vector3.up,delta);
        }
        var candidates=new List<NoteHit>();float[][] notes={op.music.notes0,op.music.notes1,op.music.notes2,op.music.notes3};
        for(int ch=0;ch<4;ch++)foreach(float note in notes[ch])if(note>=51.2f && note<58.6f)candidates.Add(new NoteHit(note,ch));
        candidates=candidates.OrderBy(n=>n.time).ThenBy(n=>n.channel).ToList();
        var selected=new List<NoteHit>();float cursor=51.2f;float[] gaps={.16f,.32f,.48f,.24f,.56f,.16f,.64f,.32f};
        for(int i=0;i<16;i++)
        {
            var eligible=candidates.Where(n=>n.time>=cursor-.0001f).ToArray();if(eligible.Length==0)throw new InvalidOperationException("Not enough actual note onsets for the 16 BT pieces");var earliest=eligible[0];
            int desired=i%4;var next=candidates.Where(n=>Mathf.Abs(n.time-earliest.time)<.0001f).OrderBy(n=>(n.channel-desired+4)%4).First();
            selected.Add(next);cursor=next.time+gaps[i%gaps.Length];
        }
        op.pieceFills=new Transform[16];op.piecePositions=new Vector3[16];op.pieceMaterials=new Material[16];op.receivePackets=new Transform[16];
        op.receiveTimes=new float[16];op.arrivalTimes=new float[16];op.receiveChannels=new int[16];op.receiveNodes=new int[16];
        var empty=Mat("BT empty piece slots",Hex("#243c51"));
        for(int i=0;i<16;i++)
        {
            int channel=selected[i].channel,node=(i+channel)%4;Vector3 p=new Vector3((i%4-1.5f)*.26f,(1.5f-i/4)*.145f,-.24f);
            op.piecePositions[i]=p;op.receiveTimes[i]=selected[i].time;op.receiveChannels[i]=channel;op.receiveNodes[i]=node;op.arrivalTimes[i]=selected[i].time+.48f+(node%3)*.08f;
            Prim("Empty independently received piece "+i,PrimitiveType.Cube,root,p+Vector3.forward*.04f,new Vector3(.24f,.13f,.028f),empty);
            var mat=Mat("Verified real-note piece "+i,Color.Lerp(Hex(ChannelColors[channel]),Hex("#8fe6ab"),.45f));op.pieceMaterials[i]=mat;
            op.pieceFills[i]=Prim("Piece "+i+" is complete only after arrival",PrimitiveType.Cube,root,p,new Vector3(.22f,.11f,.055f),mat);
            op.receivePackets[i]=Prim("Real-note incoming packet "+i,PrimitiveType.Cube,root,Vector3.zero,Vector3.one*.09f,Mat("Incoming packet trigger CH"+channel+" index"+i,Hex(ChannelColors[channel])));
        }
        op.completeAt=op.arrivalTimes.Max();
        var completed=FileTile("Complete file coalesces behind all received pieces",root,white,blue,new Vector3(0,.015f,-.13f),1);
        // The silhouette is enlarged into a native file body behind the piece mosaic.
        completed.localScale=new Vector3(5.2f,2.75f,1);op.fileMaterial=white;white.SetFloat("_Opacity",0);
        var up=new List<NoteHit>();for(int ch=0;ch<4;ch++)foreach(float note in notes[ch])if(note>=op.completeAt+.56f && note<60.74f)up.Add(new NoteHit(note,ch));
        var uploadCandidates=up.OrderBy(n=>n.time).ThenBy(n=>n.channel).GroupBy(n=>Mathf.RoundToInt(n.time*10000)).Select(g=>g.First()).ToArray();
        int uploadCount=Mathf.Min(8,uploadCandidates.Length);
        var upload=Enumerable.Range(0,uploadCount).Select(i=>uploadCandidates[Mathf.RoundToInt(i*(uploadCandidates.Length-1f)/Mathf.Max(1,uploadCount-1))]).ToArray();
        if(upload.Length<4)throw new InvalidOperationException("Need at least four real onsets after file verification to show seeding");
        op.seedAt=upload[0].time;op.uploadTimes=upload.Select(n=>n.time).ToArray();op.uploadChannels=upload.Select(n=>n.channel).ToArray();op.uploadNodes=Enumerable.Range(0,upload.Length).Select(i=>i%4).ToArray();op.uploadPackets=new Transform[upload.Length];
        for(int i=0;i<upload.Length;i++)op.uploadPackets[i]=Prim("Complete file continues uploading packet "+i,PrimitiveType.Cube,root,Vector3.zero,Vector3.one*.09f,Mat("Outgoing packet "+i,Hex(ChannelColors[upload[i].channel])));
        op.hashCursor=Panel("Full-file hash verification scan",root,Vector3.zero,.025f,.67f,Mat("BT passing hash scan",Hex("#c7f9b4")));
        op.torrentProgress=Text("Actual piece completion count","",root,new Vector3(0,.59f,-.34f),1.6f,.15f,.96f,Hex("#f0efd8"));
        op.torrentResult=Text("Receive verify then provide result","",root,new Vector3(0,-.69f,-.34f),3.3f,.11f,.77f,Hex("#8be6c3"));
        Text("Shared file identity","原创演示短片.mkv",root,new Vector3(0,.83f,-.23f),2.5f,.12f,.87f,Hex("#c3dce7"));
        Text("Bittorrent operation is not a client download link","BitTorrent 分片机制 · 只演示本项目原创文件",root,new Vector3(0,-.86f,-.3f),3.5f,.08f,.58f,Hex("#a9c2d3"));
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Build persistent Operations only in stopped V3 scene");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();if(director==null || director.music==null)throw new InvalidOperationException("Director with real music required");
        Folder(Root+"/Meshes");Folder(Root+"/Materials");Score=JObject.Parse(File.ReadAllText(ScorePath));
        // Collect this source's visible strings too, including all original demo lines.
        PrepareFont(director,File.ReadAllText("AgentScripts/OWEpisodeV3Operations.cs")+File.ReadAllText("Assets/OldWebMap/Scripts/OWV3Operations.cs")+"0123456789✓→ / 分片完整文件校验通过收到了也继续传出去持续音正在提供");
        var episode=GameObject.Find("OWEpisode123");var owner=episode!=null?episode.transform:director.transform;
        var old=owner.Find("Revision3Operations");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var root=Child("Revision3Operations",owner).transform;var op=root.gameObject.AddUdonSharpComponent<OWV3Operations>();op.director=director;op.music=director.music;op.groupRoots=new Transform[3];
        string[] names={"10.24—20.48 Explorer actual working parts","40.96—51.20 Aegisub actual working parts","51.20—61.44 actual-note BitTorrent pieces"};
        for(int i=0;i<3;i++){var group=Child(names[i],root).transform;group.position=new Vector3(0,i==1?2.42f:2.45f,i==0?6.1f:i==1?5.9f:5.9f);op.groupRoots[i]=group;}
        BuildExplorer(op,op.groupRoots[0]);BuildSubtitles(op,op.groupRoots[1]);BuildTorrent(op,op.groupRoots[2]);
        op.Seek(0);UdonSharpEditorUtility.CopyProxyToUdon(op);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(op));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var result=new{root=root.name,font=new{path=AssetDatabase.GetAssetPath(Font),source=AssetDatabase.GetAssetPath(Font.sourceFontFile),characters=Font.characterTable.Count,atlasCount=Font.atlasTextures.Length,atlasWidth=Font.atlasWidth,atlasHeight=Font.atlasHeight,multiAtlas=Font.isMultiAtlasTexturesEnabled,materialPersistent=AssetDatabase.Contains(Font.material),atlasesPersistent=Font.atlasTextures.All(AssetDatabase.Contains)},groups=3,explorerIndependentParts=op.explorerParts.Length,subtitleIndependentParts=op.subtitleParts.Length,realAudioMiniWaves=4,btPieces=16,receiveOnsets=op.receiveTimes,receiveChannels=op.receiveChannels,uploadOnsets=op.uploadTimes,completeAt=op.completeAt,seedAt=op.seedAt,automaticSampling="LateUpdate reads director.Elapsed()",sceneSaved=false,sourceImages=new[]{"windows98-se-explorer","fansub-51517e462c93","fansub-acae7d9029f6"}};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"operations-build.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(result);
    }
    public static object Sample(float time){var op=Object.FindObjectOfType<OWV3Operations>();if(op==null)throw new InvalidOperationException("Build Operations first");op.Seek(time);return Describe(op);}
    static object Describe(OWV3Operations op)
    {return new{time=op.sampledTime,group=op.sampledGroup,readmeCharacters=op.readmeCharacters,editingLine=op.editingLine,correctedCount=op.correctedCount,receivedPieces=op.receivedPieces,uploadsStarted=op.uploadsStarted,uploadsCompleted=op.uploadsCompleted,activePackets=op.activePackets,tick80ms=op.sampled80msTick,groupActive=op.groupRoots.Select(t=>t.gameObject.activeSelf).ToArray()};}
    static object VisibleTextMesh(TMP_Text text)
    {
        text.ForceMeshUpdate(true,true);
        int characters=text.textInfo.characterCount;
        int visible=characters>0 && text.textInfo.characterInfo!=null?text.textInfo.characterInfo.Take(characters).Count(c=>c.isVisible):0;
        if(!string.IsNullOrWhiteSpace(text.text) && (characters==0 || visible==0))
            throw new InvalidOperationException("Operation text has no visible mesh: "+text.name+" / height="+text.rectTransform.rect.height+" / size="+text.fontSize);
        return new{name=text.name,characters,visible,fontSize=text.fontSize,rectHeight=text.rectTransform.rect.height,text=text.text};
    }
    public static object Validate()
    {
        var op=Object.FindObjectOfType<OWV3Operations>();if(op==null)throw new InvalidOperationException("Build Operations first");
        float[][] notes={op.music.notes0,op.music.notes1,op.music.notes2,op.music.notes3};
        for(int i=0;i<op.receiveTimes.Length;i++)if(!notes[op.receiveChannels[i]].Any(t=>Mathf.Abs(t-op.receiveTimes[i])<.0001f))throw new InvalidOperationException("An incoming packet invents a beat");
        for(int i=0;i<op.uploadTimes.Length;i++)if(!notes[op.uploadChannels[i]].Any(t=>Mathf.Abs(t-op.uploadTimes[i])<.0001f))throw new InvalidOperationException("An upload packet invents a beat");
        var rows=new List<object>();
        foreach(float time in new[]{0f,10.24f,11.6f,13.6f,15.4f,17.2f,19.2f,20.48f,40.96f,43.52f,46.08f,50.8f,51.2f,53.2f,56f,op.completeAt+.05f,op.seedAt+.2f,61.43f,61.44f})
        {
            op.Seek(time);int expected=time>=10.24f&&time<20.48f?0:time>=40.96f&&time<51.2f?1:time>=51.2f&&time<61.44f?2:-1;
            if(op.sampledGroup!=expected || op.groupRoots.Count(t=>t.gameObject.activeSelf)!=(expected<0?0:1))throw new InvalidOperationException("Operation group leaked into another segment");
            if(expected==2 && op.receivedPieces!=op.arrivalTimes.Count(t=>time>=t))throw new InvalidOperationException("Piece state disagrees with real arrivals");
            rows.Add(Describe(op));
        }
        op.Seek(19.2f);if(op.readmeCharacters!=op.readmeContent.Length || !op.readmeResult.activeSelf)throw new InvalidOperationException("README action has no completed result");
        var readmeTextMeshes=op.groupRoots[0].GetComponentsInChildren<TMP_Text>(false).Select(VisibleTextMesh).ToArray();
        op.Seek(44.64f);
        var subtitleTextMeshes=new[]{op.sourceLine,op.translationLine,op.correctionLine}.Select(VisibleTextMesh).ToArray();
        op.Seek(50.8f);if(op.correctedCount!=8 || !op.subtitleResult.activeSelf || !op.subtitleOutput.gameObject.activeSelf)throw new InvalidOperationException("Subtitle edit/export has no completed result");
        op.Seek(60.9f);if(op.receivedPieces!=16 || op.uploadsStarted<4)throw new InvalidOperationException("BT does not reach complete file and continuing upload");
        // Verify the same packet path, file and typed result after a reverse seek.
        op.Seek(54.32f);string a=JsonConvert.SerializeObject(op.receivePackets.Select(t=>new{active=t.gameObject.activeSelf,p=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z}}).Where(r=>r.active));
        op.Seek(17f);op.Seek(60f);op.Seek(54.32f);string b=JsonConvert.SerializeObject(op.receivePackets.Select(t=>new{active=t.gameObject.activeSelf,p=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z}}).Where(r=>r.active));
        if(a!=b)throw new InvalidOperationException("Packet seek is not deterministic");
        var shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("OldWebMap/V3/Operation Crop")).Select(m=>new{m.message,m.line,severity=m.severity.ToString()}).ToArray();if(shaderMessages.Any(m=>m.severity=="Error"))throw new InvalidOperationException("Operation crop shader errors");
        var operationMaterials=AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null && m.shader.name=="OldWebMap/V3/Operation Crop").ToArray();
        foreach(var material in operationMaterials)
        {
            bool transparent=material.renderQueue>=3000;
            if(transparent && (material.renderQueue!=3010 || material.GetFloat("_ZWrite")!=0))throw new InvalidOperationException("Transparent operation layer writes depth: "+material.name);
            if(!transparent && (material.renderQueue!=2020 || material.GetFloat("_ZWrite")!=1))throw new InvalidOperationException("Opaque operation layer has no depth: "+material.name);
            if(material.GetFloat("_Cutoff")<=0)throw new InvalidOperationException("Operation material can write invisible depth: "+material.name);
        }
        var textMaterials=op.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.fontSharedMaterial).Where(m=>m!=null).Distinct().ToArray();
        if(textMaterials.Any(m=>m.renderQueue!=3020))throw new InvalidOperationException("Operation text must render after paper and transparent selection");
        var renderLayers=new{opaque=operationMaterials.Count(m=>m.renderQueue==2020),transparent=operationMaterials.Where(m=>m.renderQueue==3010).Select(m=>m.name).ToArray(),fontQueues=textMaterials.Select(m=>m.renderQueue).ToArray(),normalWorldDepthTest=true,visualScreenshotStillRequired=true};
        var result=new{rows,renderLayers,readmeTextMeshes,subtitleTextMeshes,incomingExactlyOnRealNotes=true,outgoingExactlyOnRealNotes=true,packetSeekRepeatable=true,persistentMaterials=op.miniWaveMaterials.All(AssetDatabase.Contains),cropMaterials=AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}).Length,completeAt=op.completeAt,seedAt=op.seedAt,shaderMessages};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"operations-validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));op.Seek(0);return JsonConvert.SerializeObject(result);
    }
}
