using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using TMPro;
using OldWebMap;
using Object = UnityEngine.Object;

public static class OWV3EffectsBuild
{
    const string Root = "Assets/OldWebMap/FullFilm/Revision3";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v3");
    static OWV3Presentation Run() { if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != "Assets/Scenes/OWM_FullFilm_v3.unity") throw new Exception("Stopped compiled V2 required"); return Object.FindObjectOfType<OWV3Presentation>(true); }
    static Transform Node(string name, Transform parent) { var old = parent.Find(name); if (old != null) return old; var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    static void Style(Renderer r) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; }
    static Material Mat(string name, string shader)
    {
        var s = Shader.Find(shader); if (s == null) throw new Exception("Shader not imported: " + shader);
        string path = Root + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); } else m.shader = s;
        m.enableInstancing = true; return m;
    }
    static Texture2D Tex(string file, int max = 2048)
    {
        string path = Root + "/Textures/" + file; var importer = AssetImporter.GetAtPath(path) as TextureImporter; if (importer == null) throw new Exception("Missing external source texture: " + path);
        bool change = importer.maxTextureSize != max || importer.npotScale != TextureImporterNPOTScale.None || importer.wrapMode != TextureWrapMode.Clamp || !importer.mipmapEnabled;
        importer.textureType = TextureImporterType.Default; importer.maxTextureSize = max; importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (change) importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Transform Plane(string name, Transform parent, Material material, Vector3 position, Vector3 size)
    {
        var t = Node(name, parent); var filter = t.GetComponent<MeshFilter>(); if (filter == null) filter = t.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx"); var r = t.GetComponent<MeshRenderer>(); if (r == null) r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = material; Style(r);
        t.localPosition = position; t.localScale = size; return t;
    }
    static Transform Solid(string name, Transform parent, Material material, Vector3 position, Vector3 size)
    {
        var t = Node(name, parent); var filter = t.GetComponent<MeshFilter>(); if (filter == null) filter = t.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"); var r = t.GetComponent<MeshRenderer>(); if (r == null) r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = material; Style(r);
        t.localPosition = position; t.localScale = size; return t;
    }
    static TMP_Text Text(string name, Transform parent, Vector3 position, Vector2 size, float fontSize, TMP_FontAsset font)
    {
        var t = Node(name, parent); var text = t.GetComponent<TextMeshPro>(); if (text == null) text = t.gameObject.AddComponent<TextMeshPro>(); text.font = font; text.fontSharedMaterial = font.material; text.fontSize = fontSize;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis; text.alignment = TextAlignmentOptions.Left; text.color = new Color(.76f,.85f,.88f); text.rectTransform.sizeDelta = size; text.transform.localPosition = position; return text;
    }
    class Geometry
    {
        public List<Vector3> v = new List<Vector3>(); public List<int> t = new List<int>(); public List<Vector2> uv = new List<Vector2>(); public List<Vector4> info = new List<Vector4>(); public List<Color> colors = new List<Color>();
        public void Quad(Vector3 centre, Vector3 right, Vector3 up, Vector4 meta, Color color)
        {
            int at = v.Count; v.AddRange(new[] {centre-right-up,centre-right+up,centre+right+up,centre+right-up});
            t.AddRange(new[] {at,at+1,at+2,at,at+2,at+3}); uv.AddRange(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
            for (int i=0;i<4;i++) { info.Add(meta); colors.Add(color); }
        }
        public Mesh Save(string name)
        {
            string path=Root+"/Meshes/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(m==null){m=new Mesh();AssetDatabase.CreateAsset(m,path);}else m.Clear();
            m.name=name;m.indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;m.SetVertices(v);m.SetTriangles(t,0);m.SetUVs(0,uv);m.SetUVs(1,info);m.SetColors(colors);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(3);m.bounds=b;EditorUtility.SetDirty(m);return m;
        }
    }
    static Transform MeshNode(string name, Transform parent, Geometry geometry, Material material)
    {
        var t=Node(name,parent);var f=t.GetComponent<MeshFilter>();if(f==null)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=geometry.Save(name);
        var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.enabled=true;Style(r);return t;
    }
    static Transform Imported(string filename,string name,Transform parent)
    {
        var pivot=Node(name,parent);if(pivot.childCount==0){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+filename+".fbx");if(prefab==null)throw new Exception("Missing FBX: "+filename);var child=(GameObject)PrefabUtility.InstantiatePrefab(prefab);child.transform.SetParent(pivot,false);}return pivot;
    }
    static Transform Pointer(string name,Transform parent,OWV3Presentation run)
    {
        var source=run.basis.legacy.montage.cursors[0];var t=Node(name,parent);var f=t.GetComponent<MeshFilter>();if(f==null)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
        var m=Mat("Readable original operating-system pointer","OldWebMap/V2/Source Surface");m.SetColor("_Tint",Color.white);var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=m;Style(r);t.localScale=Vector3.one*.22f;
        var edge=Node("Black pixel outline",t);edge.localScale=Vector3.one*1.22f;edge.localPosition=new Vector3(0,0,.003f);var ef=edge.GetComponent<MeshFilter>();if(ef==null)ef=edge.gameObject.AddComponent<MeshFilter>();ef.sharedMesh=f.sharedMesh;var er=edge.GetComponent<MeshRenderer>();if(er==null)er=edge.gameObject.AddComponent<MeshRenderer>();var em=Mat("Native pointer black contour","OldWebMap/V2/Source Surface");em.SetColor("_Tint",Color.black);er.sharedMaterial=em;Style(er);return t;
    }

    static void Audio(OWV3Presentation run)
    {
        var type=typeof(VRC.SDK3.Components.VRCSpatialAudioSource);
        foreach(var source in run.director.music.sources.Concat(run.audioFloor.drySources))
        {
            source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=30;source.maxDistance=150;source.dopplerLevel=0;source.priority=32;
            var spatial=source.GetComponent(type);if(spatial==null)spatial=source.gameObject.AddComponent(type);
            var so=new SerializedObject(spatial);so.FindProperty("Near").floatValue=30;so.FindProperty("Far").floatValue=150;so.FindProperty("Gain").floatValue=0;so.FindProperty("UseAudioSourceVolumeCurve").boolValue=true;
            so.FindProperty("EnableSpatialization").boolValue=Array.IndexOf(run.director.music.sources,source)>=0;so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    static void Panel(OWV3Presentation run)
    {
        var d=run.details;var root=run.basis.legacy.startupObject;d.menuGeometry=root;
        var field=root.Find("Field");field.localPosition=new Vector3(0,.1f,.025f);field.localScale=new Vector3(2.7f,2.1f,1);
        field.GetComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        d.portalMaterial=Mat("Binocular window interior","OldWebMap/V3/Window Interior");field.GetComponent<Renderer>().sharedMaterial=d.portalMaterial;
        var depth=root.Find("Physical star and copper depth");if(depth!=null)depth.gameObject.SetActive(false);
        var scroller=root.Find("Scroller");if(scroller!=null)scroller.GetComponent<Renderer>().enabled=false;
        var canvas=run.basis.legacy.menuRoot;d.menuCanvasGroup=canvas.GetComponent<CanvasGroup>();if(d.menuCanvasGroup==null)d.menuCanvasGroup=canvas.AddComponent<CanvasGroup>();
        var panel=canvas.transform.Find("Panel");if(panel!=null){var image=panel.GetComponent<UnityEngine.UI.Image>();var c=image.color;c.a=.20f;image.color=c;}
        d.menuButtons=canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        var dark=Mat("Opening rear wall","Unlit/Color");dark.color=new Color(.012f,.021f,.041f);
        d.rearWall=Solid("Rear wall before the room opens",run.basis.legacy.journey.worldFrame,dark,new Vector3(0,1.7f,-7),new Vector3(18,4,.12f));
        if(d.rearWall.GetComponent<BoxCollider>()==null)d.rearWall.gameObject.AddComponent<BoxCollider>();
        var word=Plane("Quiet continue background",run.finalWordRoot.transform,Mat("Continue soft backing","OldWebMap/V2/Source Surface"),new Vector3(0,0,.025f),new Vector3(1.6f,.58f,1));
        d.wordBackground=word.GetComponent<Renderer>().sharedMaterial;d.wordBackground.SetColor("_Tint",new Color(.005f,.012f,.023f,1));
    }
    static void Ribbon(OWV3Presentation run)
    {
        var g=new Geometry();const int count=768;
        for(int i=0;i<count;i++)
        {
            int at=g.v.Count;float a=i/(float)count,b=(i+1)/(float)count;
            g.v.AddRange(new[]{new Vector3(a,-.5f,0),new Vector3(a,.5f,0),new Vector3(b,.5f,0),new Vector3(b,-.5f,0)});
            g.uv.AddRange(new[]{new Vector2(a,0),new Vector2(a,1),new Vector2(b,1),new Vector2(b,0)});g.t.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});
            for(int n=0;n<4;n++){g.info.Add(Vector4.zero);g.colors.Add(Color.white);}
        }
        var mat=Mat("Returning colored greetings","OldWebMap/V3/Returning Ribbon");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldWebMap/Episode123/Revision5/Identity/Textures/greetz-atlas.png");if(texture==null)throw new Exception("Original per-glyph greetz atlas missing");
        mat.mainTexture=texture;mat.SetVector("_Atlas",new Vector4(4096,1024,112,5));mat.SetVector("_Row",new Vector4(4064,16,18189,1));mat.SetVector("_Grad",new Vector4(13,90,82,0));mat.SetFloat("_ArcLength",40.8407f);mat.SetFloat("_PxPerMetre",190.476f);mat.SetFloat("_UseTimeline",1);mat.SetFloat("_EndFade",.015f);
        var node=MeshNode("One ribbon from the opening to the finished world",run.transform,g,mat);node.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.zero,new Vector3(30,12,30));run.details.textRibbon=node;run.details.textRibbonMaterial=mat;
    }
    static void PixelCylinder(OWV3Presentation run)
    {
        var bitmap=new Dictionary<char,string[]>{
            {'E',new[]{"11111","10000","10000","11110","10000","10000","11111"}},
            {'D',new[]{"11110","10001","10001","10001","10001","10001","11110"}},
            {'G',new[]{"01111","10000","10000","10111","10001","10001","01110"}},
            {'T',new[]{"11111","00100","00100","00100","00100","00100","00100"}},
            {'A',new[]{"01110","10001","10001","11111","10001","10001","10001"}},
            {'M',new[]{"10001","11011","10101","10101","10001","10001","10001"}}};
        string text="EDGE TEAM";int columns=text.Sum(c=>c==' '?4:6);var g=new Geometry();
        for(int copy=0;copy<2;copy++){int col=0;foreach(char ch in text){if(ch==' '){col+=4;continue;}var rows=bitmap[ch];for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(rows[y][x]=='1')
        {float angle=Mathf.PI-((copy*columns+col+x)-(columns-1)*.5f)*Mathf.PI/columns;g.Quad(Vector3.zero,Vector3.right*.5f,Vector3.up*.5f,new Vector4(angle,y,0,0),Color.white);}col+=6;}}
        foreach(var r in run.cylinder.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        var mat=Mat("EDGE TEAM blue bordered pixel cells","OldWebMap/V3/EDGE TEAM Pixel Cylinder");mat.SetFloat("_Radius",1.7f);mat.SetFloat("_Pitch",.135f);mat.SetFloat("_Cell",.11f);mat.SetFloat("_Fill",.78f);
        var node=MeshNode("EDGE TEAM actual cylindrical pixel field",run.cylinder,g,mat);node.localPosition=Vector3.zero;run.cylinder.localPosition=new Vector3(0,4.75f,6.5f);run.details.pixelCylinderMaterial=mat;
    }
    static void RoundWaves(OWV3Presentation run)
    {
        string path=Root+"/Meshes/Round PCM capsules.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null)
        {
            var vertices=new List<Vector3>();var info=new List<Vector4>();var triangles=new List<int>();const int segments=256,sides=8;
            float[] radius={0,.7071068f,1,1,.7071068f,0},cap={-1,-.7071068f,0,0,.7071068f,1},end={0,0,0,1,1,1};
            for(int s=0;s<segments;s++){int start=vertices.Count;for(int r=0;r<6;r++)for(int k=0;k<sides;k++){float a=k*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*radius[r],Mathf.Sin(a)*radius[r],0));info.Add(new Vector4(s/(float)segments,(s+1)/(float)segments,end[r],cap[r]));}
            for(int r=0;r<5;r++)for(int k=0;k<sides;k++){int a=start+r*sides+k,b=start+r*sides+(k+1)%sides,c=b+sides,d=a+sides;triangles.AddRange(new[]{a,b,c,a,c,d});}}
            mesh=new Mesh{name="Real PCM with round caps and round glow"};mesh.SetVertices(vertices);mesh.SetUVs(1,info);mesh.SetTriangles(triangles,0);mesh.bounds=new Bounds(Vector3.zero,new Vector3(24,16,16));AssetDatabase.CreateAsset(mesh,path);
        }
        var osc=run.basis.signal.oscilloscope;foreach(var wave in osc.regionalWaves.Concat(run.basis.signal.continuationWaves))wave.GetComponent<MeshFilter>().sharedMesh=mesh;
        foreach(var material in osc.waveMaterials){material.shader=Shader.Find("OldWebMap/V3/Round Oscilloscope");material.SetFloat("_Width",.055f);EditorUtility.SetDirty(material);}
    }
    static void HalosAndCursors(OWV3Presentation run)
    {
        var d=run.details;d.focusHalos=new Transform[2];d.haloMaterials=new Material[2];
        for(int i=0;i<2;i++){var mat=Mat("Attached source halo "+i,"OldWebMap/V3/Attached Focus Halo");mat.SetColor("_Color",new Color(.22f,.46f,.8f));d.haloMaterials[i]=mat;d.focusHalos[i]=Plane("Halo follows actual image "+i,run.transform,mat,Vector3.zero,Vector3.one);}
        var pointer=Tex("pixel-pointer.png",64);var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(pointer));importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var matPointer=Mat("Point filtered native arrow","Unlit/Transparent");matPointer.mainTexture=pointer;
        var pg=new Geometry();pg.Quad(new Vector3(.3f,-.45f,0),new Vector3(.3f,0,0),new Vector3(0,.45f,0),Vector4.zero,Color.white);var pm=pg.Save("Native arrow tip at origin");
        foreach(var t in run.basis.legacy.montage.cursors.Concat(new[]{run.nativePointer,run.archivePointer,run.editorPlayPointer}).Where(t=>t!=null))
        {t.GetComponent<MeshFilter>().sharedMesh=pm;t.GetComponent<Renderer>().sharedMaterial=matPointer;var old=t.Find("Black pixel outline");if(old!=null)old.gameObject.SetActive(false);}
        var g=new Geometry();for(int i=0;i<128;i++)g.Quad(Vector3.zero,Vector3.right*.5f,Vector3.up*.5f,new Vector4(i,i/128f,0,0),Color.white);
        var swarm=Mat("128 independently moving pixel cursors","OldWebMap/V3/Pixel Cursor Swarm");swarm.mainTexture=pointer;var node=MeshNode("128 cursors in an accumulating information field",run.transform,g,swarm);node.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.zero,new Vector3(40,24,40));d.cursorSwarm=node.GetComponent<Renderer>();d.cursorSwarmMaterial=swarm;
        var ui=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v2-native-ui.json"));d.nativePointerPositions=new Vector3[24];var groups=(JArray)ui["groups"];string[] order={"readme","manga","torrent","forum","subtitles","access"};
        for(int group=0;group<6;group++)for(int state=0;state<4;state++){var gr=groups.First(t=>(string)t["id"]==order[group]);var st=gr["states"][state];var layer=st["layers"].FirstOrDefault(l=>(string)l["id"]=="primary")??st["layers"].First(l=>l["rect_px"]!=null);var rect=(JArray)layer["rect_px"];float x=(float)rect[0]+(float)rect[2]*.5f,y=(float)rect[1]+(float)rect[3]*.5f;d.nativePointerPositions[group*4+state]=new Vector3(-.65f+(x/(float)gr["width"]-.5f)*4.45f,2.25f+(.5f-y/(float)gr["height"])*2.9f,5.40f);}
    }
    static void Comments(OWV3Presentation run)
    {
        var d=run.details;d.comments=new Transform[12];d.commentTexts=new TMP_Text[12];var font=run.basis.keyText.font;
        d.commentWords=new[]{"嵌字好了","这一句再润色一下","辛苦了","校对中","还有种子","已补种","继续做种","谢谢分享","沙发","顶一下","楼主还在吗","签名档更新了","时间轴+1","这句听清了","已校对","字幕已补","你好","去哪玩","来这边","同一个房间","链接呢","需要登录","打不开","换个入口","求来源","原帖呢","正在加载","重发一次","链接失效了","缓存还在","先存一份","已失联","能跑了","这里也能改","一起试试","再来一次"};
        for(int i=0;i<12;i++){var text=Text("Community comment "+i,run.transform,Vector3.zero,new Vector2(4,.3f),1.65f,font);text.overflowMode=TextOverflowModes.Overflow;d.comments[i]=text.transform;d.commentTexts[i]=text;}
    }
    public static object Core()
    {
        var run=Run();Audio(run);Panel(run);Ribbon(run);PixelCylinder(run);RoundWaves(run);HalosAndCursors(run);Comments(run);
        run.Waiting();run.offlinePreview=false;foreach(var b in new UdonSharpBehaviour[]{run,run.details,run.basis,run.basis.signal,run.audioFloor}){UdonSharpEditorUtility.CopyProxyToUdon(b);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(b));EditorUtility.SetDirty(b);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return new{success=true,stereoPortal=true,cylinderText="EDGE TEAM",roundCapsuleSegments=256,pixelCursors=128,unityAndVrcNear=30,unityAndVrcFar=150};
    }
    public static object Timing()
    {
        var run=Run();run.director.duration=253.44f;run.director.music.performanceEnd=253.44f;run.director.music.endFadeSeconds=.16f;run.basis.signal.duration=253.44f;
        for(int i=0;i<run.basis.historyStarts.Length;i++)if(run.basis.historyStarts[i]>=255.99f&&run.basis.historyStarts[i]<=256.01f)run.basis.historyStarts[i]=253.44f;
        run.details.replayMenuPosition=new Vector3(0,1.05f,.5f);
        run.Waiting();run.offlinePreview=false;
        foreach(var b in new UdonSharpBehaviour[]{run.director,run.director.music,run,run.details,run.basis,run.basis.signal}){UdonSharpEditorUtility.CopyProxyToUdon(b);EditorUtility.SetDirty(b);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(b));}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return new{success=true,duration=253.44f,sourceClipSeconds=256f,lastRetainedOrder=98,discardedTriggerOrder=99,discardedPlaybackSeconds=2.56f,sourceClipsModified=false};
    }
    static void ReplaceModel(Transform pivot,string filename)
    {
        for(int i=pivot.childCount-1;i>=0;i--)Object.DestroyImmediate(pivot.GetChild(i).gameObject);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+filename+".fbx");if(prefab==null)throw new Exception("V3 FBX missing: "+filename);
        var child=(GameObject)PrefabUtility.InstantiatePrefab(prefab);child.transform.SetParent(pivot,false);
        // Preserve the importer-authored axis and unit conversion root.
    }
    static void ModelStyle(Transform root,string scope)
    {
        var map=new Dictionary<Material,Material>();var toon=Shader.Find("lilToon");if(toon==null)throw new Exception("Existing lilToon package shader unavailable");
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if(r.name.StartsWith("UI_"))continue;var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(source==null)continue;if(!map.TryGetValue(source,out var material))
                {
                    string name=source.name.Replace('/','_');bool hard=name.Contains("Metal")||name.Contains("Silver")||name.Contains("Rubber")||name.Contains("Graphite");
                    material=Mat(scope+"-"+name,hard?"Standard":"lilToon");Color color=source.HasProperty("_Color")?source.color:new Color(.5f,.5f,.5f);
                    material.color=color;if(hard){material.SetFloat("_Glossiness",.20f);material.SetFloat("_Metallic",name.Contains("Metal")?.45f:0);}
                    else{foreach(var setting in new[]{new[]{"_UseShadow","1"},new[]{"_ShadowStrength","0.35"},new[]{"_LightMinLimit","0.20"},new[]{"_LightMaxLimit","1"},new[]{"_AsUnlit","0.08"}})if(material.HasProperty(setting[0]))material.SetFloat(setting[0],float.Parse(setting[1],System.Globalization.CultureInfo.InvariantCulture));}
                    map[source]=material;
                }
                materials[i]=material;
            }
            r.sharedMaterials=materials;
        }
    }
    static void ReadableUi(Transform root)
    {
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("UI_")))
        {
            var f=r.GetComponent<MeshFilter>();var source=f.sharedMesh;if(AssetDatabase.GetAssetPath(source).Contains("/V3ReadableUi-"))continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id);string path=Root+"/Meshes/V3ReadableUi-"+guid+"-"+id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(source);mesh.name="V3 readable "+source.name;var uv=mesh.uv;for(int i=0;i<uv.Length;i++)uv[i].x=1-uv[i].x;mesh.uv=uv;AssetDatabase.CreateAsset(mesh,path);}f.sharedMesh=mesh;PrefabUtility.RecordPrefabInstancePropertyModifications(f);
        }
    }
    static Material ImageMaterial(string name,Texture texture)
    {var m=Mat(name,"Unlit/Texture");m.mainTexture=texture;return m;}
    static void BindModelUi(OWV3Presentation run)
    {
        var primary=run.screenshotMaterials[0];string[] names={"UI_Main_16x9","UI_LeftHierarchy","UI_RightAssets","UI_Timeline","UI_Thumbnail_00","UI_Thumbnail_01","UI_Thumbnail_02","UI_LowReadingCard"};
        string[] files={"software-unity-editor.png","software-unity-hierarchy.png","software-unity-inspector.png","software-blender-timeline.png","software-blender-viewport.png","software-blender-wire-viewport.png","software-unity-viewport.png","software-unity-viewport.png"};
        foreach(var r in run.workspace.GetComponentsInChildren<Renderer>(true))
        {
            int at=Array.IndexOf(names,r.name);if(at<0)continue;
            r.sharedMaterial=at==0?primary:ImageMaterial("V3 actual software slot "+r.name,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldWebMap/FullFilm/Revision2/Textures/"+files[at]));
        }
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v3-model-assets.json"));var labels=(JArray)data["semantic_labels"]["labels"];
        foreach(var pivot in new[]{run.miniature,run.finalWorld})foreach(var r in pivot.GetComponentsInChildren<Renderer>(true))
        {
            var label=labels.FirstOrDefault(t=>(string)t["target_mesh"]==r.name);
            if(label!=null)r.sharedMaterial=ImageMaterial("V3 label "+r.name,Tex((string)label["file"]));
            else if(r.name=="UI_SharedWork_00")r.sharedMaterial=ImageMaterial("Shared original comic and translation",Tex("shared-comic-board.png"));
            else if(r.name=="UI_SharedWork_01")r.sharedMaterial=ImageMaterial("Shared original game localization",Tex("shared-game-board.png"));
            else if(r.name=="UI_WorldNotice")r.sharedMaterial=run.workflowMaterials[2];
        }
    }
    static Vector3 JVec(JToken token){return new Vector3((float)token[0],(float)token[1],(float)token[2]);}
    static void Collisions(OWV3Presentation run)
    {
        var contract=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v3-model-assets.json"))["collision_contract"];var list=new List<Collider>();
        foreach(var entry in contract["floor_mesh_colliders"])
        {
            var name=(string)entry["object"];var f=run.finalWorld.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.name==name);var c=f.GetComponent<MeshCollider>();if(c==null)c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=false;c.enabled=false;list.Add(c);
        }
        var rig=Node("Final floor and seat collision rig",run.finalWorld);rig.localPosition=Vector3.zero;rig.localRotation=Quaternion.identity;rig.localScale=Vector3.one;
        foreach(var entry in contract["unity_floor_box_colliders"].Concat(contract["unity_seat_box_colliders"]))
        {
            var node=Node((string)entry["name"],rig);node.localPosition=JVec(entry["center_unity_m"]);node.localEulerAngles=JVec(entry["rotation_unity_deg"]);node.localScale=Vector3.one;
            var c=node.GetComponent<BoxCollider>();if(c==null)c=node.gameObject.AddComponent<BoxCollider>();c.size=JVec(entry["size_unity_m"]);c.enabled=false;list.Add(c);
        }
        run.details.worldFloorColliders=list.ToArray();run.details.originalFloorColliders=Object.FindObjectsOfType<Collider>(true).Where(c=>c.gameObject.scene==SceneManager.GetActiveScene()&&c.name=="Stable invisible support").ToArray();
        if(run.details.originalFloorColliders.Length!=1)throw new Exception("Expected the one stable audience floor");
        foreach(var collider in run.basis.legacy.performanceRoot.GetComponentsInChildren<Collider>(true))if(!run.details.originalFloorColliders.Contains(collider))collider.enabled=false;
    }
    public static object Models()
    {
        var run=Run();ReplaceModel(run.workspace,"fullfilm_v3_workbench");ReplaceModel(run.miniature,"fullfilm_v3_scene_block");ReplaceModel(run.finalWorld,"fullfilm_v3_scene_block");
        ModelStyle(run.workspace,"V3-table");ModelStyle(run.miniature,"V3-miniature");ModelStyle(run.finalWorld,"V3-world");foreach(var pivot in new[]{run.workspace,run.miniature,run.finalWorld})ReadableUi(pivot);BindModelUi(run);Collisions(run);WorldEdges(run);
        var colored=run.finalWorld.GetComponentsInChildren<Renderer>(true).Where(r=>!r.name.StartsWith("UI_")).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.HasProperty("_Color")).Distinct().ToArray();run.worldColorMaterials=colored;run.worldOriginalColors=colored.Select(m=>m.color).ToArray();
        run.Waiting();run.offlinePreview=false;foreach(var b in new UdonSharpBehaviour[]{run,run.details}){UdonSharpEditorUtility.CopyProxyToUdon(b);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(b));EditorUtility.SetDirty(b);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return new{success=true,workbenchWorldTop=.60f,worldBridgeTop=-.05f,finalFloorAndSeatColliders=run.details.worldFloorColliders.Length,lilToonMaterials=colored.Count(m=>m.shader.name=="lilToon")};
    }
    static void WorldEdges(OWV3Presentation run)
    {
        var root=run.finalWorld;var g=new Geometry();int part=0;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.name.StartsWith("UI_"))continue;var mesh=r.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;var edges=new HashSet<ulong>();
            for(int i=0;i<triangles.Length;i+=3)for(int k=0;k<3;k++)
            {
                int a=triangles[i+k],b=triangles[i+(k+1)%3];ulong key=((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);if(!edges.Add(key))continue;
                Vector3 p=root.InverseTransformPoint(r.transform.TransformPoint(vertices[a])),q=root.InverseTransformPoint(r.transform.TransformPoint(vertices[b]));var line=q-p;if(line.sqrMagnitude<.001f)continue;
                Vector3 side=Vector3.Cross(line.normalized,Vector3.forward);if(side.sqrMagnitude<.01f)side=Vector3.Cross(line.normalized,Vector3.up);side=side.normalized*.0015f;
                g.Quad((p+q)*.5f,line*.5f,side,new Vector4(part%4,0,0,0),new Color(.18f,.72f,1,.65f));
            }
            part++;
        }
        var mat=Mat("V3 same world mesh topology","OldWebMap/V2/Depth Copper");mat.SetFloat("_Mode",3);mat.SetColor("_Tint",Color.white);run.worldTopologyMaterial=mat;MeshNode("Same world actual mesh edges",root,g,mat);
    }
    public static object UserArt()
    {
        var run=Run();var d=run.details;var root=Node("User original artwork and localization",run.transform);d.generatedArtRoot=root.gameObject;
        d.comicStates=new[]{Tex("user-comic-original.png"),Tex("user-comic-clean.png"),Tex("user-comic-chinese.png")};d.gameStates=new[]{Tex("user-game-original.png"),Tex("user-game-chinese.png")};d.flashStates=Enumerable.Range(0,6).Select(i=>Tex("user-flash-"+i.ToString("00")+".png")).ToArray();
        d.comicMaterial=ImageMaterial("User original comic",d.comicStates[0]);d.gameMaterial=ImageMaterial("User game translation",d.gameStates[0]);d.gameDetailMaterial=Mat("Same game dialogue detail","OldWebMap/V2/Source Surface");d.gameDetailMaterial.mainTexture=d.gameStates[1];
        d.homepageMaterial=ImageMaterial("User original night post webpage",Tex("user-homepage.png"));d.flashMaterial=ImageMaterial("Six real user animation frames",d.flashStates[0]);d.conceptMaterial=ImageMaterial("User imagined concept before the actual model",Tex("user-concept.png"));
        d.comicPlane=Plane("Original comic page",root,d.comicMaterial,new Vector3(2.6f,1.8f,5.6f),new Vector3(1.5f,1.627f,1)).GetComponent<Renderer>();
        d.gamePlane=Plane("Recognizable game art with changing dialogue",root,d.gameMaterial,new Vector3(-.7f,2.2f,5.4f),new Vector3(2.65f,3.0f,1)).GetComponent<Renderer>();
        d.gameDetailPlane=Plane("Same scene second language and dialogue",root,d.gameDetailMaterial,new Vector3(2.0f,2.1f,5.25f),new Vector3(2.1f,.79f,1)).GetComponent<Renderer>();
        d.homepagePlane=Plane("Night post original personal artwork",root,d.homepageMaterial,new Vector3(2.75f,2.42f,5.65f),new Vector3(2.15f,1.21f,1)).GetComponent<Renderer>();
        d.flashPlane=Plane("Original six-frame pixel animation",root,d.flashMaterial,new Vector3(2.6f,2.25f,5.6f),new Vector3(2,1.505f,1)).GetComponent<Renderer>();
        d.conceptPlane=Plane("An early imagined version of the place",root,d.conceptMaterial,new Vector3(2.65f,2,5.1f),new Vector3(2.3f,1.29375f,1)).GetComponent<Renderer>();
        d.stillLifePlane=Plane("Creative tools and shared culture",root,ImageMaterial("User culture artifact still life",Tex("user-still-life.png")),new Vector3(-2.9f,2.0f,6.0f),new Vector3(2.3f,1.29375f,1)).GetComponent<Renderer>();
        d.mangaNativeStates=new Texture2D[8];for(int i=0;i<4;i++){d.mangaNativeStates[i*2]=Tex("native-manga-user-"+i.ToString("00")+"-full.png");d.mangaNativeStates[i*2+1]=Tex("native-manga-user-"+i.ToString("00")+"-detail.png");run.nativeStates[run.nativeOffsets[1]+i]=d.mangaNativeStates[i*2];run.nativeCompanions[run.nativeOffsets[1]+i]=d.mangaNativeStates[i*2+1];d.nativePointerPositions[4+i]=new Vector3(-.65f+(605f/800-.5f)*4.45f,2.25f+(.5f-(365+i*30)/540f)*2.9f,5.40f);}
        d.stickerPlanes=new Transform[12];for(int i=0;i<12;i++){var texture=Tex("user-sticker-"+i.ToString("00")+".png");d.stickerPlanes[i]=Plane("Original community reaction "+i,root,ImageMaterial("User reaction "+i,texture),Vector3.zero,Vector3.one);}
        run.sourceStatus.transform.localPosition=new Vector3(0,1.88f,4.45f);run.sourceStatus.fontSize=1.1f;
        run.Waiting();run.offlinePreview=false;foreach(var b in new UdonSharpBehaviour[]{run,d}){UdonSharpEditorUtility.CopyProxyToUdon(b);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(b));EditorUtility.SetDirty(b);}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return new{success=true,userOriginalImages=8,cutouts=26,mangaNativeStates=4};
    }
    public static object Provenance()
    {
        var run=Run();var d=run.details;var root=Node("Avatar authorship and actual public discussion",run.transform);d.provenanceRoot=root.gameObject;
        d.provenanceTextures=Enumerable.Range(0,3).Select(i=>Tex("provenance-"+i.ToString("00")+"-main.png")).ToArray();d.provenanceAspects=d.provenanceTextures.Select(t=>(float)t.width/t.height).ToArray();
        d.provenanceDetails=Enumerable.Range(0,6).Select(i=>Tex("provenance-"+(i/2).ToString("00")+"-detail-"+(i%2)+".png")).ToArray();d.provenanceDetailAspects=d.provenanceDetails.Select(t=>(float)t.width/t.height).ToArray();
        d.provenanceMainMaterial=ImageMaterial("Actual VRC anti ripping public statements",d.provenanceTextures[0]);d.provenanceDetailMaterial=ImageMaterial("Associated original source paragraphs",d.provenanceDetails[0]);
        d.provenanceMain=Plane("Public statement stays recognizable",root,d.provenanceMainMaterial,new Vector3(-.85f,2.22f,5.3f),new Vector3(3.5f,3,1)).GetComponent<Renderer>();
        d.provenanceDetail=Plane("Same source relevant changing paragraph",root,d.provenanceDetailMaterial,new Vector3(2.45f,2.3f,5.25f),new Vector3(2.4f,1,1)).GetComponent<Renderer>();
        d.provenanceLabels=new[]{"ask.vrchat.com / 2024-07-25 · 平台公告","ask.vrchat.com / 2024-08-08 · 平台公告","feedback.vrchat.com / 2026-09-22 · 社区提案"};
        d.provenanceLabel=Text("Visible source and its own publication date",root,new Vector3(-.6f,.51f,5.2f),new Vector2(5.6f,.25f),1.04f,run.basis.keyText.font);
        run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(d);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(d));EditorUtility.SetDirty(d);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return new{success=true,publicSources=3,derivedCrops=9,allegationsAboutPrivatePeople=false};
    }
}
