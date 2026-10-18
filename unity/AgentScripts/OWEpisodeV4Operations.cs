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

public static class OWEpisodeV4Operations
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v4.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision4/Operations";
    const string ScorePath="AgentScripts/data/episode123-v4-score.json";
    // ExCSS also defines System.Tuple in this Editor; keep score hits local.
    struct NoteHit
    {
        public float time;
        public int channel;
        public NoteHit(float time,int channel){this.time=time;this.channel=channel;}
    }
    static TMP_FontAsset Font;
    static JObject Score;
    static JObject OperationData;
    const string OperationPath="AgentScripts/data/episode123-v4-operations.json";
    static JObject Config(string id){return (JObject)((JArray)OperationData["groups"]).First(g=>(string)g["id"]==id);}
    static readonly string[] ChannelColors={"#53d2e2","#edbd67","#bf90ef","#86cf83"};
    static string ReportDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v4/operations");
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
        var image=((JArray)OperationData["image_sources"]).FirstOrDefault(i=>(string)i["id"]==id)??((JArray)Score["images"]).FirstOrDefault(i=>(string)i["id"]==id);
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
    static void BuildExplorer(OWV4Operations op,Transform root)
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
        Text("Opened document title",(string)Config("explorer")["readme_title"],page,new Vector3(0,.51f,-.055f),1.43f,.09f,.65f,Color.white);
        Text("Notepad menu","文件  编辑  搜索  帮助",page,new Vector3(-.05f,.405f,-.045f),1.32f,.08f,.59f,Hex("#243346"),TextAlignmentOptions.Left);
        op.readmeBody=Text("The opened readable original document","",page,new Vector3(0,-.02f,-.055f),1.34f,.66f,.72f,Hex("#203140"),TextAlignmentOptions.TopLeft);
        op.readmeContent=(string)Config("explorer")["readme_content"];
        var result=Text("README operation has a result","README.TXT 已打开",page,new Vector3(0,-.48f,-.065f),1.43f,.08f,.62f,Hex("#21814c"));op.readmeResult=result.gameObject;
    }
    static void BuildSubtitles(OWV4Operations op,Transform root)
    {
        var config=Config("subtitles");var lines=(JArray)config["lines"];var parts=new List<Transform>();
        const string source="aegisub-translation-assistant-doc";
        // Genuine official 3.2 Translation Assistant sections; no v2.2 title is
        // transplanted into 2014. Native waveform/timeline are reconstructed below.
        parts.Add(Panel("Aegisub 3.2 Translation Assistant title",root,new Vector3(-1.07f,.74f,-.1f),1.15f,25*1.15f/347,Crop("Aegisub32 title",source,14,13,347,25)));
        parts.Add(Panel("Aegisub32 real Original field",root,new Vector3(-1.07f,.36f,-.1f),1.1f,127*1.1f/330,Crop("Aegisub32 Original",source,25,43,330,127)));
        parts.Add(Panel("Aegisub32 real Translation field",root,new Vector3(-1.07f,-.10f,-.1f),1.1f,119*1.1f/330,Crop("Aegisub32 Translation",source,25,178,330,119)));
        op.subtitleActions=Panel("Aegisub32 real Keys and Actions",root,new Vector3(-1.07f,-.61f,-.16f),1.1f,181*1.1f/333,Crop("Aegisub32 Keys Actions",source,23,309,333,181));parts.Add(op.subtitleActions);
        var action=(JArray)config["play_video_click"]["normalized_crop_uv"];op.subtitleActionUV=new Vector2((float)action[0],(float)action[1]);
        var black=Mat("Aegisub32 live audio black",Hex("#071015"));parts.Add(Panel("Live audio field",root,new Vector3(.62f,.625f,-.1f),1.84f,.36f,black));
        var paper=Mat("Aegisub32 native editor paper",Hex("#f5f6ec"));parts.Add(Panel("Editable translation field",root,new Vector3(.62f,.08f,-.38f),1.84f,.40f,paper));
        parts.Add(Panel("Aegisub32 native subtitle grid",root,new Vector3(.62f,-.62f,-.26f),1.84f,.61f,Mat("Aegisub32 timeline pale",Hex("#d7eceb"))));
        op.subtitleParts=parts.ToArray();op.subtitlePositions=parts.Select(p=>p.localPosition).ToArray();
        var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldWebMap/Episode123/Revision3/Data/waveform_rgba.png");var ribbon=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/OldWebMap/Episode123/Revision3/Meshes/Triggered waveform ribbon.asset");
        if(atlas==null || ribbon==null)throw new InvalidOperationException("Shared real-PCM waveform resources are required");
        op.miniWaves=new Transform[4];op.miniWaveMaterials=new Material[4];
        for(int ch=0;ch<4;ch++)
        {
            string path=Root+"/Materials/Aegisub32 CH0"+(ch+1)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("OldWebMap/V3/Triggered Oscilloscope"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_WaveAtlas",atlas);mat.SetFloat("_Channel",ch);mat.SetColor("_Color",Hex(ChannelColors[ch]));mat.SetFloat("_Length",1.70f);mat.SetFloat("_Amplitude",.21f);mat.SetFloat("_Width",.008f);mat.SetFloat("_Gain",.9f);
            mat.SetFloat("_Bend",0);mat.SetFloat("_Curl",0);mat.SetFloat("_Twist",0);mat.SetFloat("_Constrain",0);EditorUtility.SetDirty(mat);
            var go=Child("Live audio CH0"+(ch+1),root);go.transform.localPosition=new Vector3(.62f,.75f-ch*.08f,-.28f);go.AddComponent<MeshFilter>().sharedMesh=ribbon;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;RenderStyle(renderer);op.miniWaves[ch]=go.transform;op.miniWaveMaterials[ch]=mat;
        }
        op.audioCursor=Panel("Audio selection playhead",root,Vector3.zero,.013f,.36f,Mat("Aegisub32 time cursor",Hex("#ffe06e")));
        op.timingValue=Text("Editable current timing","",root,new Vector3(.62f,.325f,-.44f),1.83f,.11f,.53f,Hex("#d4e2e9"));
        op.sourceLine=Text("Current source line","",root,new Vector3(.62f,.145f,-.45f),1.72f,.12f,.56f,Hex("#566779"),TextAlignmentOptions.Left);
        op.translationLine=Text("Current translation","",root,new Vector3(.62f,-.035f,-.45f),1.72f,.16f,.82f,Hex("#183e54"),TextAlignmentOptions.Left);
        op.correctionLine=Text("Editor status","",root,new Vector3(.62f,-.205f,-.49f),1.84f,.12f,.58f,Hex("#88e6c0"));
        Text("Timeline column headers","Start     End     Text",root,new Vector3(.62f,-.34f,-.46f),1.72f,.10f,.54f,Hex("#304e67"));
        op.timelineLines=new TMP_Text[6];for(int i=0;i<6;i++)op.timelineLines[i]=Text("Subtitle grid row "+i,"",root,new Vector3(.62f,-.45f-i*.07f,-.46f),1.72f,.08f,.45f,Hex("#29485e"),TextAlignmentOptions.Left);
        op.subtitleRow=Panel("Selected subtitle row",root,Vector3.zero,1.76f,.06f,TransparentMat("Timeline selection transparent",new Color(.05f,.3f,.86f,.26f)));
        op.subtitleSelectionMaterial=TransparentMat("Translation caret selection",new Color(.1f,.5f,.9f,.5f));op.subtitleSelection=Panel("Translation caret",root,Vector3.zero,.022f,.13f,op.subtitleSelectionMaterial);
        op.subtitlePointer=Pointer(root,Mat("Subtitle editor pointer",Color.white));
        op.sourceLines=lines.Select(l=>(string)l["source"]).ToArray();op.translatedLines=lines.Select(l=>(string)l["translation"]).ToArray();op.correctedLines=lines.Select(l=>(string)l["corrected"]).ToArray();op.timeCodes=lines.Select(l=>(string)l["timecode"]).ToArray();op.correctedTimeCodes=lines.Select(l=>(string)l["corrected_timecode"]).ToArray();
        op.subtitleOutput=FileTile("Saved ASS file",root,paper,Mat("ASS file fold",Hex("#4296c1")),Vector3.zero,1);
        Text("Saved subtitle filename",(string)config["filename"],op.subtitleOutput,new Vector3(0,-.23f,-.04f),.8f,.09f,.62f,Hex("#b4e9e6"));
        op.subtitleResult=Text("Saved subtitle status","保存完成 · movie.zh-cn.ass",root,new Vector3(0,-.91f,-.48f),3.2f,.12f,.7f,Hex("#88e6c0")).gameObject;
    }
    static void BuildTorrent(OWV4Operations op,Transform root)
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
            Text("Peer connection state","已连接",node,new Vector3(0,-.3f,-.12f),1.05f,.08f,.52f,Hex("#9fc1d0"));
            // Native thin bridge from peer to file; packets travel above the bridge.
            Vector3 target=new Vector3(ch%2==0?-.5f:.5f,0,-.08f),delta=target-op.nodePositions[ch];
            var link=Prim("Peer-to-file physical link",PrimitiveType.Cylinder,root,(target+op.nodePositions[ch])*.5f,new Vector3(.006f,delta.magnitude*.5f,.006f),metal);link.localRotation=Quaternion.FromToRotation(Vector3.up,delta);
        }
        float[][] notes={op.music.notes0,op.music.notes1,op.music.notes2,op.music.notes3};
        var receive=(JArray)Config("torrent")["receive"];var selected=new List<NoteHit>();
        foreach(var hit in receive)
        {
            int channel=(int)hit["channel"],index=(int)hit["note_index"];float time=(float)hit["time"];
            if(index<0 || index>=notes[channel].Length || Mathf.Abs(notes[channel][index]-time)>.0001f)throw new InvalidOperationException("V4 BT receive no longer matches its original track note index");
            selected.Add(new NoteHit(time,channel));
        }
        op.pieceFills=new Transform[16];op.piecePositions=new Vector3[16];op.pieceMaterials=new Material[16];op.receivePackets=new Transform[16];
        op.receiveTimes=new float[16];op.arrivalTimes=new float[16];op.receiveChannels=new int[16];op.receiveNodes=new int[16];
        var empty=Mat("BT empty piece slots",Hex("#243c51"));
        for(int i=0;i<16;i++)
        {
            int channel=selected[i].channel,node=(int)receive[i]["source_node"];Vector3 p=new Vector3((i%4-1.5f)*.26f,(1.5f-i/4)*.145f,-.24f);
            op.piecePositions[i]=p;op.receiveTimes[i]=selected[i].time;op.receiveChannels[i]=channel;op.receiveNodes[i]=node;op.arrivalTimes[i]=(float)receive[i]["arrival"];
            Prim("Empty independently received piece "+i,PrimitiveType.Cube,root,p+Vector3.forward*.04f,new Vector3(.24f,.13f,.028f),empty);
            var mat=Mat("Verified real-note piece "+i,Color.Lerp(Hex(ChannelColors[channel]),Hex("#8fe6ab"),.45f));op.pieceMaterials[i]=mat;
            op.pieceFills[i]=Prim("Piece "+i+" is complete only after arrival",PrimitiveType.Cube,root,p,new Vector3(.22f,.11f,.055f),mat);
            op.receivePackets[i]=Prim("Real-note incoming packet "+i,PrimitiveType.Cube,root,Vector3.zero,Vector3.one*.09f,Mat("Incoming packet trigger CH"+channel+" index"+i,Hex(ChannelColors[channel])));
        }
        op.completeAt=op.arrivalTimes.Max();
        var completed=FileTile("Complete file coalesces behind all received pieces",root,white,blue,new Vector3(0,.015f,-.13f),1);
        // The silhouette is enlarged into a native file body behind the piece mosaic.
        completed.localScale=new Vector3(5.2f,2.75f,1);op.fileMaterial=white;white.SetFloat("_Opacity",0);
        var uploadRows=(JArray)Config("torrent")["upload"];var upload=uploadRows.Select(h=>new NoteHit((float)h["time"],(int)h["channel"])).ToArray();
        foreach(var hit in uploadRows)
        {
            int channel=(int)hit["channel"],index=(int)hit["note_index"];
            if(index<0 || index>=notes[channel].Length || Mathf.Abs(notes[channel][index]-(float)hit["time"])>.0001f)throw new InvalidOperationException("V4 BT upload no longer matches its original track note index");
        }
        if(upload.Length<4)throw new InvalidOperationException("V4 BT needs at least four actual-note uploads");
        op.seedAt=upload[0].time;op.uploadTimes=upload.Select(n=>n.time).ToArray();op.uploadChannels=upload.Select(n=>n.channel).ToArray();op.uploadNodes=uploadRows.Select(r=>(int)r["destination_node"]).ToArray();op.uploadPackets=new Transform[upload.Length];
        for(int i=0;i<upload.Length;i++)op.uploadPackets[i]=Prim("Complete file continues uploading packet "+i,PrimitiveType.Cube,root,Vector3.zero,Vector3.one*.09f,Mat("Outgoing packet "+i,Hex(ChannelColors[upload[i].channel])));
        op.hashCursor=Panel("Full-file hash verification scan",root,Vector3.zero,.025f,.67f,Mat("BT passing hash scan",Hex("#c7f9b4")));
        op.torrentProgress=Text("Actual piece completion count","",root,new Vector3(0,.59f,-.34f),1.6f,.15f,.96f,Hex("#f0efd8"));
        op.torrentResult=Text("Receive verify then provide result","",root,new Vector3(0,-.69f,-.34f),3.3f,.11f,.77f,Hex("#8be6c3"));
        Text("Shared file identity",(string)Config("torrent")["filename"],root,new Vector3(0,.83f,-.23f),2.5f,.12f,.87f,Hex("#c3dce7"));
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Build persistent Operations only in stopped V4 scene");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var journey=Object.FindObjectOfType<OWV4Journey>();if(director==null || director.music==null || journey==null || journey.worldFrame==null)throw new InvalidOperationException("V4 Journey.worldFrame and real music director required");
        Folder(Root+"/Meshes");Folder(Root+"/Materials");OperationData=JObject.Parse(File.ReadAllText(OperationPath));Score=File.Exists(ScorePath)?JObject.Parse(File.ReadAllText(ScorePath)):new JObject{{"images",new JArray()}};
        // Collect this source's visible strings too, including all original demo lines.
        PrepareFont(director,File.ReadAllText("AgentScripts/OWEpisodeV4Operations.cs")+File.ReadAllText("Assets/OldWebMap/Scripts/OWV4Operations.cs")+OperationData.ToString()+"0123456789✓→ / 分片完整文件校验通过已连接做种中");
        var owner=journey.worldFrame;
        var old=owner.Find("Revision4Operations");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var root=Child("Revision4Operations",owner).transform;var op=root.gameObject.AddUdonSharpComponent<OWV4Operations>();op.journey=journey;op.director=director;op.music=director.music;op.groupRoots=new Transform[3];
        string[] names={"10.24—20.48 Explorer actual working parts","61.44—71.68 Aegisub 3.2 editing","40.96—51.20 actual-note BitTorrent pieces"};
        for(int i=0;i<3;i++){var group=Child(names[i],root).transform;group.localPosition=new Vector3(0,i==1?2.42f:2.45f,i==0?6.1f:i==1?5.9f:5.9f);op.groupRoots[i]=group;}
        BuildExplorer(op,op.groupRoots[0]);BuildSubtitles(op,op.groupRoots[1]);BuildTorrent(op,op.groupRoots[2]);
        op.Seek(0);UdonSharpEditorUtility.CopyProxyToUdon(op);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(op));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var result=new{root=root.name,font=new{path=AssetDatabase.GetAssetPath(Font),source=AssetDatabase.GetAssetPath(Font.sourceFontFile),characters=Font.characterTable.Count,atlasCount=Font.atlasTextures.Length,atlasWidth=Font.atlasWidth,atlasHeight=Font.atlasHeight,multiAtlas=Font.isMultiAtlasTexturesEnabled,materialPersistent=AssetDatabase.Contains(Font.material),atlasesPersistent=Font.atlasTextures.All(AssetDatabase.Contains)},groups=3,explorerIndependentParts=op.explorerParts.Length,subtitleIndependentParts=op.subtitleParts.Length,realAudioMiniWaves=4,btPieces=16,receiveOnsets=op.receiveTimes,receiveChannels=op.receiveChannels,uploadOnsets=op.uploadTimes,completeAt=op.completeAt,seedAt=op.seedAt,automaticSampling="LateUpdate reads director.Elapsed()",sceneSaved=false,sourceImages=new[]{"windows98-se-explorer","aegisub-translation-assistant-doc"},sharedImagesReadOnly=true,newIndependentImages=0,operationData=OperationPath};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"operations-build.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(result);
    }
    public static object Sample(float time){var op=Object.FindObjectOfType<OWV4Operations>();if(op==null)throw new InvalidOperationException("Build Operations first");op.Seek(time);return Describe(op);}
    static object Describe(OWV4Operations op)
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
        var op=Object.FindObjectOfType<OWV4Operations>();if(op==null)throw new InvalidOperationException("Build V4 Operations first");
        var data=JObject.Parse(File.ReadAllText(OperationPath));var bt=(JObject)((JArray)data["groups"]).First(g=>(string)g["id"]=="torrent");
        float[][] notes={op.music.notes0,op.music.notes1,op.music.notes2,op.music.notes3};
        foreach(var hit in ((JArray)bt["receive"]).Concat((JArray)bt["upload"]))
        {
            int channel=(int)hit["channel"],index=(int)hit["note_index"];float time=(float)hit["time"];
            if(index<0 || index>=notes[channel].Length || Mathf.Abs(notes[channel][index]-time)>.0001f)throw new InvalidOperationException("V4 BT hit does not match its actual original note index");
            if(time<40.96f || time>=51.2f)throw new InvalidOperationException("V4 BT note escaped its actual time range");
        }
        for(int i=0;i<op.receiveTimes.Length;i++)if(!notes[op.receiveChannels[i]].Any(t=>Mathf.Abs(t-op.receiveTimes[i])<.0001f))throw new InvalidOperationException("An incoming packet invents a beat");
        for(int i=0;i<op.uploadTimes.Length;i++)if(!notes[op.uploadChannels[i]].Any(t=>Mathf.Abs(t-op.uploadTimes[i])<.0001f))throw new InvalidOperationException("An upload packet invents a beat");
        if(op.receiveNodes.Distinct().Count()!=4)throw new InvalidOperationException("All four BT peers must contribute actual pieces");
        if(Mathf.Abs(op.completeAt-47.36f)>.02f || Mathf.Abs(op.seedAt-48f)>.02f)throw new InvalidOperationException("V4 BT does not reach the intended complete/seed transition");
        var rows=new List<object>();var forbidden=new[]{"原创演示","原图拆","本项目原创","真实起音","触发传递","听译 →"};
        foreach(float time in new[]{0f,10.24f,11.6f,13.6f,15.6f,17.2f,19.2f,20.48f,40.96f,43.52f,46.08f,47.5f,48.4f,50.9f,51.12f,51.2f,61.44f,64.64f,65.12f,70.98f,71.67f,71.68f})
        {
            op.Seek(time);int expected=time>=10.24f&&time<20.48f?0:time>=61.44f&&time<71.68f?1:time>=40.96f&&time<51.2f?2:-1;
            if(op.sampledGroup!=expected || op.groupRoots.Count(t=>t.gameObject.activeSelf)!=(expected<0?0:1))throw new InvalidOperationException("V4 operation group leaked into another period");
            if(expected==2 && op.receivedPieces!=op.arrivalTimes.Count(t=>time>=t))throw new InvalidOperationException("V4 piece state disagrees with real arrivals");
            foreach(var text in op.GetComponentsInChildren<TMP_Text>(false))if(forbidden.Any(word=>text.text.Contains(word)))throw new InvalidOperationException("Audit/source copy leaked into the visible performance: "+text.text);
            rows.Add(Describe(op));
        }
        op.Seek(19.2f);if(op.readmeCharacters!=op.readmeContent.Length || !op.readmeResult.activeSelf)throw new InvalidOperationException("README has no completed result");
        var readmeTextMeshes=op.groupRoots[0].GetComponentsInChildren<TMP_Text>(false).Select(VisibleTextMesh).ToArray();
        op.Seek(65.12f);var subtitleTextMeshes=new[]{op.sourceLine,op.translationLine,op.correctionLine,op.timingValue}.Select(VisibleTextMesh).ToArray();
        op.Seek(68.5f);string beforeTiming=op.timingValue.text;op.Seek(68.78f);
        if(beforeTiming!=op.timeCodes[5] || op.timingValue.text!=op.correctedTimeCodes[5] || beforeTiming==op.timingValue.text)throw new InvalidOperationException("Aegisub's visible timing field does not really change by the corrected 80ms step");
        string afterTiming=op.timingValue.text;object timingMesh=VisibleTextMesh(op.timingValue);
        op.Seek(71.28f);if(op.correctedCount!=8 || !op.subtitleResult.activeSelf || !op.subtitleOutput.gameObject.activeSelf)throw new InvalidOperationException("V4 Aegisub has no eight-line correction/export result");
        op.Seek(69.04f);Vector3 buttonTarget=op.subtitleActions.localPosition+op.subtitleActions.localRotation*new Vector3((op.subtitleActionUV.x-.5f)*op.subtitleActions.localScale.x,(.5f-op.subtitleActionUV.y)*op.subtitleActions.localScale.y,-.008f);
        Vector3 pointerTip=op.subtitlePointer.localPosition+op.subtitlePointer.localRotation*(new Vector3(-.3f,.5f,0)*op.subtitlePointer.localScale.x);
        if(Vector3.Distance(pointerTip,buttonTarget)>.001f)throw new InvalidOperationException("Aegisub pointer misses the actual 3.2 Play Video image coordinate");
        foreach(float time in new[]{11.6f,13.6f,17.2f})
        {
            op.Seek(time);Vector3 tip=op.filePointer.localPosition+op.filePointer.localRotation*(new Vector3(-.3f,.5f,0)*op.filePointer.localScale.x);
            if(Vector3.Distance(tip,op.filePointerTarget)>.0001f)throw new InvalidOperationException("README pointer tip misses its working file/text plane");
        }
        op.Seek(51.12f);if(op.receivedPieces!=16 || op.uploadsStarted!=8 || op.uploadsCompleted!=8)throw new InvalidOperationException("V4 BT does not finish all eight seeded transfers before its group exits");
        op.Seek(44.32f);string a=JsonConvert.SerializeObject(op.receivePackets.Select(t=>new{active=t.gameObject.activeSelf,p=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z}}).Where(r=>r.active));
        op.Seek(17f);op.Seek(70f);op.Seek(44.32f);string b=JsonConvert.SerializeObject(op.receivePackets.Select(t=>new{active=t.gameObject.activeSelf,p=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z}}).Where(r=>r.active));if(a!=b)throw new InvalidOperationException("V4 packet seek is not deterministic");
        if(!op.transform.IsChildOf(op.journey.worldFrame))throw new InvalidOperationException("V4 Operations is not part of the common worldFrame");
        Vector3 frame=op.journey.worldFrame.localPosition;op.Seek(17.2f);Vector3 before=op.readmePage.position,local=op.readmePage.localPosition;Vector3 shift=new Vector3(.5f,.1f,-2),expectedShift=op.journey.worldFrame.parent!=null?op.journey.worldFrame.parent.TransformVector(shift):shift;
        try{op.journey.worldFrame.localPosition=frame+shift;op.Seek(17.2f);if(Vector3.Distance(op.readmePage.localPosition,local)>.0001f || Vector3.Distance(op.readmePage.position-before,expectedShift)>.001f)throw new InvalidOperationException("Operations cancels the common world-frame motion");}finally{op.journey.worldFrame.localPosition=frame;}
        var operationMaterials=AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null && m.shader.name=="OldWebMap/V3/Operation Crop").ToArray();
        foreach(var material in operationMaterials)
        {
            bool transparent=material.renderQueue>=3000;
            if(transparent && (material.renderQueue!=3010 || material.GetFloat("_ZWrite")!=0))throw new InvalidOperationException("Transparent V4 operation layer writes depth: "+material.name);
            if(!transparent && (material.renderQueue!=2020 || material.GetFloat("_ZWrite")!=1))throw new InvalidOperationException("Opaque V4 operation layer has no depth: "+material.name);
            if(material.GetFloat("_Cutoff")<=0)throw new InvalidOperationException("V4 operation material can write invisible depth");
        }
        var textMaterials=op.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.fontSharedMaterial).Where(m=>m!=null).Distinct().ToArray();if(textMaterials.Any(m=>m.renderQueue!=3020))throw new InvalidOperationException("V4 operation text does not render after selection layers");
        var shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("OldWebMap/V3/Operation Crop")).Select(m=>new{m.message,m.line,severity=m.severity.ToString()}).ToArray();if(shaderMessages.Any(m=>m.severity=="Error"))throw new InvalidOperationException("Shared operation crop shader errors");
        var result=new{rows,readmeTextMeshes,subtitleTextMeshes,actualNoteIndicesChecked=true,allFourPeersContribute=true,visible80msTimingCorrection=true,timingCorrection=new{before=beforeTiming,after=afterTiming,deltaMilliseconds=80,mesh=timingMesh},completeAt=op.completeAt,seedAt=op.seedAt,packetSeekRepeatable=true,trueImagePointerHitVerified=true,worldFrameMotionVerified=true,visibleCopyAuditPassed=true,
            renderLayers=new{opaqueRenderers=op.GetComponentsInChildren<Renderer>(true).Count(r=>r.sharedMaterials.Any(m=>m!=null && m.shader.name=="OldWebMap/V3/Operation Crop" && m.renderQueue==2020)),transparentRenderers=op.GetComponentsInChildren<Renderer>(true).Count(r=>r.sharedMaterials.Any(m=>m!=null && m.shader.name=="OldWebMap/V3/Operation Crop" && m.renderQueue==3010)),opaqueMaterials=operationMaterials.Count(m=>m.renderQueue==2020),transparentMaterials=operationMaterials.Where(m=>m.renderQueue==3010).Select(m=>m.name).ToArray(),fontQueues=textMaterials.Select(m=>m.renderQueue).ToArray(),normalWorldDepthTest=true},shaderMessages,visualScreenshotStillRequired=true};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"operations-validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));op.Seek(0);return JsonConvert.SerializeObject(result);
    }
}
