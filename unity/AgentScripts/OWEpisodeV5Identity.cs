using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using VRC.SDK3.Components;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

// V5 Identity module: cracktro-style waiting/finished window (replaces "One waiting terminal") and the greetz sine-scroller ring.
// Entries (stopped editor, scene Assets/Scenes/OWM_Episode123_v5.unity, no scene save): BuildCracktro, BuildGreetz, Validate.
// Texture sources: art/export/episode123-v5/identity/*.png copied to Root/Textures by the main agent; data: AgentScripts/data/episode123-v5-greetz.json.
public static class OWEpisodeV5Identity
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision5/Identity";
    const string DataPath="AgentScripts/data/episode123-v5-greetz.json";
    const string WindowName="V5 Cracktro window";
    const string RingName="Revision5 Greetz ring";
    const string CanvasName="Ray UI - start and replay";
    // Window layout in window-local metres (canvas centre = origin, window root sits 0.22 m behind the canvas).
    const float FrameHx=1.45f,FrameHy=1.15f,FrameCy=.10f,Bevel=.10f;
    const float LogoY=.895f,ScrollY=-.795f,CanvasBack=.22f;
    static readonly Regex ForbiddenEnglish=new Regex(@"\b(serial|key|keygen|generate[ds]?|generator|crack(ed|ing|s)?|patch(ed|es)?|register(ed|s)?|registration|licen[sc]e|password|username)\b",RegexOptions.IgnoreCase);
    static readonly string[] ForbiddenChinese={"序列号","注册码","注册机","破解","补丁","激活码","生成器","用户名","密码"};
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v5");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    static void Sync(UdonSharpBehaviour b){UdonSharpEditorUtility.CopyProxyToUdon(b);var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(b);if(vm!=null)EditorUtility.SetDirty(vm);EditorUtility.SetDirty(b);}
    static void Require(){if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped V5 scene required: "+ScenePath);}
    static JObject Data(){if(!File.Exists(DataPath))throw new InvalidOperationException("Missing "+DataPath+" (copy data/episode123-v5-greetz.json)");return JObject.Parse(File.ReadAllText(DataPath));}
    static Transform Owner(){var g=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x=>x.name=="OWEpisode123");if(g==null)throw new InvalidOperationException("OWEpisode123 missing");return g.transform;}
    static float Num(JToken t)=>(float)t;
    static void Style(Renderer r){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}

    // ---------------------------------------------------------------- assets
    static Texture2D Tex(string file,bool data)
    {
        string path=Root+"/Textures/"+file;
        if(!File.Exists(path))throw new InvalidOperationException("Texture missing: copy art/export/episode123-v5/identity/"+file+" to "+path);
        if(AssetDatabase.LoadAssetAtPath<Texture2D>(path)==null)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var imp=AssetImporter.GetAtPath(path) as TextureImporter;if(imp==null)throw new InvalidOperationException("No texture importer: "+path);
        var filter=data?FilterMode.Trilinear:FilterMode.Point;
        if(!TexOk(imp,data))
        {
            imp.textureType=TextureImporterType.Default;imp.sRGBTexture=!data;imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.alphaIsTransparency=!data;
            imp.mipmapEnabled=data;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=filter;imp.anisoLevel=data?4:1;imp.textureCompression=TextureImporterCompression.Uncompressed;
            imp.maxTextureSize=4096;imp.npotScale=TextureImporterNPOTScale.None;imp.isReadable=false;imp.streamingMipmaps=false;imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static bool TexOk(TextureImporter imp,bool data)=>imp.textureType==TextureImporterType.Default&&imp.sRGBTexture==!data&&imp.alphaSource==TextureImporterAlphaSource.FromInput&&imp.alphaIsTransparency==!data&&imp.mipmapEnabled==data&&imp.wrapMode==TextureWrapMode.Clamp&&imp.filterMode==(data?FilterMode.Trilinear:FilterMode.Point)&&imp.textureCompression==TextureImporterCompression.Uncompressed&&imp.maxTextureSize==4096&&imp.npotScale==TextureImporterNPOTScale.None&&!imp.isReadable;
    static Material Mat(string name,string shaderName,int queue)
    {
        string path=Root+"/Materials/"+name+".mat";var shader=Shader.Find(shaderName);
        if(shader==null||!shader.isSupported)throw new InvalidOperationException("V5 shader missing/unsupported: "+shaderName);
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
        mat.enableInstancing=true;mat.renderQueue=queue;EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh SaveMesh(string name,List<Vector3> v,List<int> t,List<Color> c,List<Vector2> uv,List<Vector3> n)
    {
        string path=Root+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(v);mesh.SetTriangles(t,0);if(c!=null)mesh.SetColors(c);if(uv!=null)mesh.SetUVs(0,uv);
        if(n!=null)mesh.SetNormals(n);else mesh.RecalculateNormals();
        mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh QuadMesh(string name,float w,float h,Vector3 offset)
    {
        var v=new List<Vector3>{new Vector3(-w/2,-h/2,0)+offset,new Vector3(-w/2,h/2,0)+offset,new Vector3(w/2,h/2,0)+offset,new Vector3(w/2,-h/2,0)+offset};
        return SaveMesh(name,v,new List<int>{0,1,2,0,2,3},Enumerable.Repeat(Color.white,4).ToList(),new List<Vector2>{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)},Enumerable.Repeat(new Vector3(0,0,-1),4).ToList());
    }
    static Mesh StripMesh(string name,float w,float h,int segments)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
        for(int j=0;j<=segments;j++){float u=(float)j/segments;v.Add(new Vector3((u-.5f)*w,-h/2,0));uv.Add(new Vector2(u,0));v.Add(new Vector3((u-.5f)*w,h/2,0));uv.Add(new Vector2(u,1));}
        for(int j=0;j<segments;j++){int i=2*j;t.AddRange(new[]{i,i+1,i+3,i,i+3,i+2});}
        var mesh=SaveMesh(name,v,t,null,uv,Enumerable.Repeat(new Vector3(0,0,-1),v.Count).ToList());
        var b=mesh.bounds;b.Expand(new Vector3(0,.2f,.02f));mesh.bounds=b;EditorUtility.SetDirty(mesh);return mesh;
    }
    // Rectangular bevel loop: profile points (r inward from the outer edge, z toward the audience is negative), per-facet normal (nr inward, nz).
    static Mesh LoopMesh(string name,float hx,float hy,float[] r,float[] z,float[] nr,float[] nz,float[] sheen)
    {
        var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();var n=new List<Vector3>();
        var light=new Vector3(-.45f,.6f,-.66f).normalized;
        for(int k=0;k<r.Length-1;k++)
        {
            float ax=hx-r[k],ay=hy-r[k],bx=hx-r[k+1],by=hy-r[k+1];
            for(int side=0;side<4;side++)
            {
                Vector3 p0,p1,p2,p3,normal;
                if(side==0){p0=new Vector3(-ax,ay,z[k]);p1=new Vector3(ax,ay,z[k]);p2=new Vector3(bx,by,z[k+1]);p3=new Vector3(-bx,by,z[k+1]);normal=new Vector3(0,-nr[k],nz[k]);}
                else if(side==1){p0=new Vector3(ax,ay,z[k]);p1=new Vector3(ax,-ay,z[k]);p2=new Vector3(bx,-by,z[k+1]);p3=new Vector3(bx,by,z[k+1]);normal=new Vector3(-nr[k],0,nz[k]);}
                else if(side==2){p0=new Vector3(ax,-ay,z[k]);p1=new Vector3(-ax,-ay,z[k]);p2=new Vector3(-bx,-by,z[k+1]);p3=new Vector3(bx,-by,z[k+1]);normal=new Vector3(0,nr[k],nz[k]);}
                else{p0=new Vector3(-ax,-ay,z[k]);p1=new Vector3(-ax,ay,z[k]);p2=new Vector3(-bx,by,z[k+1]);p3=new Vector3(-bx,-by,z[k+1]);normal=new Vector3(nr[k],0,nz[k]);}
                normal.Normalize();float shade=Mathf.Clamp01(.12f+.9f*Mathf.Max(0,Vector3.Dot(normal,light)));
                int first=v.Count;v.AddRange(new[]{p0,p1,p2,p3});t.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                for(int i=0;i<4;i++){c.Add(new Color(shade,sheen[k],0,1));n.Add(normal);}
            }
        }
        return SaveMesh(name,v,t,c,null,n);
    }
    static Mesh RingMesh(JToken ring)
    {
        float radius=Num(ring["radius"]),arc=Num(ring["arc_degrees"])*Mathf.Deg2Rad,height=Num(ring["band_height_m"]);int segments=(int)ring["segments"];
        var v=new List<Vector3>();var uv=new List<Vector2>();var n=new List<Vector3>();var t=new List<int>();
        for(int j=0;j<=segments;j++)
        {
            float u=(float)j/segments,theta=(u-.5f)*arc,s=Mathf.Sin(theta),c=Mathf.Cos(theta);
            v.Add(new Vector3(radius*s,0,radius*c));uv.Add(new Vector2(u,0));n.Add(new Vector3(-s,0,-c));
            v.Add(new Vector3(radius*s,height,radius*c));uv.Add(new Vector2(u,1));n.Add(new Vector3(-s,0,-c));
        }
        for(int j=0;j<segments;j++){int i=2*j;t.AddRange(new[]{i,i+3,i+1,i,i+2,i+3});}
        var mesh=SaveMesh("GreetzRing",v,t,null,uv,n);
        var b=mesh.bounds;b.Expand(new Vector3(.4f,1.2f,.4f));mesh.bounds=b;EditorUtility.SetDirty(mesh);return mesh;
    }
    static Transform Part(Transform root,string name,Mesh mesh,Material mat,Vector3 position)
    {
        var existing=root.Find(name);var go=existing!=null?existing.gameObject:Child(name,root);
        var mf=go.GetComponent<MeshFilter>();if(mf==null)mf=go.AddComponent<MeshFilter>();mf.sharedMesh=mesh;
        var mr=go.GetComponent<MeshRenderer>();if(mr==null)mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.enabled=true;Style(mr);
        foreach(var collider in go.GetComponents<Collider>())Object.DestroyImmediate(collider);
        go.transform.localPosition=position;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;go.SetActive(true);EditorUtility.SetDirty(go);return go.transform;
    }
    static void SetTexture(Material m,string property,Texture tex){m.SetTexture(property,tex);m.SetTextureScale(property,Vector2.one);m.SetTextureOffset(property,Vector2.zero);}

    // ---------------------------------------------------------------- cracktro window
    public static object BuildCracktro()
    {
        Require();var d=Data();var owner=Owner();
        Folder(Root+"/Meshes");Folder(Root+"/Materials");Folder(Root+"/Textures");
        var win=d["window"];var logo=d["logo"];
        var logoTexture=Tex((string)logo["file"],false);var stripTexture=Tex((string)win["file"],true);
        // meshes
        float fieldHx=FrameHx-Bevel,fieldHy=FrameHy-Bevel;
        var frameMesh=LoopMesh("CracktroFrame",FrameHx,FrameHy,new[]{0f,0f,.022f,.05f,.10f},new[]{.04f,-.08f,-.11f,-.11f,0f},new[]{-1f,-.806f,0f,.91f},new[]{0f,-.591f,-1f,-.41f},new[]{0f,1f,1f,.6f});
        var insetMesh=LoopMesh("CracktroInset",1.075f,.68f,new[]{0f,0f,.04f,.04f},new[]{0f,-.014f,-.014f,0f},new[]{-1f,0f,1f},new[]{0f,-1f,0f},new[]{.3f,1f,.3f});
        var fieldMesh=QuadMesh("CracktroField",fieldHx*2,fieldHy*2,Vector3.zero);
        float logoW=Num(logo["width"])*Num(logo["metres_per_texel"]),logoH=Num(logo["height"])*Num(logo["metres_per_texel"]);
        var logoMesh=QuadMesh("CracktroLogo",logoW,logoH,Vector3.zero);
        var scrollerMesh=StripMesh("CracktroScroller",Num(win["strip_width_m"]),Num(win["ribbon_height_m"]),(int)win["segments"]);
        // materials
        var field=Mat("CracktroField","OldWebMap/V5/Cracktro Field",2000);
        field.SetFloat("_Mode",0);field.SetFloat("_Src",1);field.SetFloat("_Dst",0);field.SetFloat("_ZW",1);field.SetFloat("_Aspect",fieldHx/fieldHy);field.SetFloat("_Gain",1);field.SetFloat("_Lines",168);
        float x0=(-1.04f+fieldHx)/(2*fieldHx),x1=(1.04f+fieldHx)/(2*fieldHx),y0=(-.645f-(FrameCy-fieldHy))/(2*fieldHy),y1=(.645f-(FrameCy-fieldHy))/(2*fieldHy);
        field.SetVector("_Safe",new Vector4(x0,y0,x1,y1));
        field.SetVector("_Bars",new Vector4((LogoY-(FrameCy-fieldHy))/(2*fieldHy),(ScrollY-(FrameCy-fieldHy))/(2*fieldHy),.05f,.04f));
        var metal=Mat("CracktroMetal","OldWebMap/V5/Cracktro Field",2000);
        metal.SetFloat("_Mode",1);metal.SetFloat("_Src",1);metal.SetFloat("_Dst",0);metal.SetFloat("_ZW",1);metal.SetFloat("_Gain",1);metal.SetFloat("_Sweep",1);
        var logoMat=Mat("CracktroLogo","OldWebMap/V5/Cracktro Field",3000);
        logoMat.SetFloat("_Mode",2);logoMat.SetFloat("_Src",5);logoMat.SetFloat("_Dst",10);logoMat.SetFloat("_ZW",0);logoMat.SetFloat("_Gain",1);logoMat.SetFloat("_Sweep",1);SetTexture(logoMat,"_MainTex",logoTexture);
        var scroller=Mat("CracktroScroller","OldWebMap/V5/Sine Scroller",3010);
        SetTexture(scroller,"_MainTex",stripTexture);
        float ppm=Num(win["px_per_metre"]);float em=Num(win["em_px"]);float baseline=Num(win["baseline_px"]);
        scroller.SetFloat("_UseTimeline",0);scroller.SetFloat("_Scroll",0);scroller.SetFloat("_ScrollRate",Num(win["scroll_px_per_s"]));scroller.SetFloat("_Opacity",1);
        scroller.SetFloat("_ArcLength",Num(win["strip_width_m"]));scroller.SetFloat("_PxPerMetre",ppm);
        scroller.SetVector("_Atlas",new Vector4(Num(win["width"]),Num(win["height"]),Num(win["band_px"]),1));
        scroller.SetVector("_Row",new Vector4(Num(win["text_length_px"]),Num(win["row_pad"]),Num(win["text_length_px"]),1));
        scroller.SetVector("_Grad",new Vector4(baseline-.88f*em,baseline+.08f*em,baseline,0));
        scroller.SetFloat("_Amp",Num(win["amplitude"]));scroller.SetFloat("_Wave",Num(win["wavelength"]));scroller.SetFloat("_Omega",Num(win["omega"]));scroller.SetFloat("_HueSpeed",.125f);scroller.SetFloat("_EndFade",Num(win["end_fade"]));
        scroller.SetVector("_Shadow",new Vector4(3,3,.55f,0));
        // scene objects
        var root=owner.Find(WindowName);if(root==null)root=Child(WindowName,owner).transform;root.gameObject.SetActive(true);
        Part(root,"Frame",frameMesh,metal,new Vector3(0,FrameCy,0));
        Part(root,"Inset",insetMesh,metal,Vector3.zero);
        Part(root,"Field",fieldMesh,field,new Vector3(0,FrameCy,.004f));
        Part(root,"Logo",logoMesh,logoMat,new Vector3(0,LogoY,-.012f));
        Part(root,"Scroller",scrollerMesh,scroller,new Vector3(0,ScrollY,-.012f));
        var canvas=Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(c=>c.name==CanvasName);if(canvas==null)throw new InvalidOperationException("Canvas missing: "+CanvasName);
        root.SetPositionAndRotation(canvas.transform.position+canvas.transform.forward*CanvasBack,canvas.transform.rotation);root.localScale=Vector3.one;
        // canvas restyle: colours only. Button, OWEpisodeButton, VRCUiShape, collider, layers and raycast targets are untouched.
        var panel=canvas.transform.Find("Panel");if(panel!=null&&panel.GetComponent<Image>()!=null){var image=panel.GetComponent<Image>();image.color=new Color(0,0,0,.55f);EditorUtility.SetDirty(image);}
        var title=canvas.transform.Find("Title");if(title!=null&&title.GetComponent<TMP_Text>()!=null){var text=title.GetComponent<TMP_Text>();text.color=Color.white;EditorUtility.SetDirty(text);}
        var body=canvas.transform.Find("Invitation");if(body!=null&&body.GetComponent<TMP_Text>()!=null){var text=body.GetComponent<TMP_Text>();text.color=new Color(.70f,.96f,1f,1f);EditorUtility.SetDirty(text);}
        var button=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault();
        if(button!=null)
        {
            var image=button.GetComponent<Image>();if(image!=null){image.color=new Color(.30f,.55f,.85f,1f);EditorUtility.SetDirty(image);}
            var label=button.GetComponentInChildren<TMP_Text>(true);if(label!=null){label.color=new Color(.97f,.99f,1f,1f);EditorUtility.SetDirty(label);}
        }
        var old=owner.Find("One waiting terminal");if(old!=null){old.gameObject.SetActive(false);EditorUtility.SetDirty(old.gameObject);}
        var p=F<OWV4Presentation>();if(p==null)throw new InvalidOperationException("OWV4Presentation missing");p.startupObject=root;Sync(p);
        EditorUtility.SetDirty(root.gameObject);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("identity-cracktro-build.json",new{window=WindowName,startupAssigned=p.startupObject==root,oldCubeActive=old!=null&&old.gameObject.activeSelf,frame=new[]{FrameHx*2,FrameHy*2},field=new[]{fieldHx*2,fieldHy*2},safe=new[]{x0,y0,x1,y1},logo=new[]{logoW,logoH},scrollerText=(string)win["text"],textureFilter=new{logo=logoTexture.filterMode.ToString(),strip=stripTexture.filterMode.ToString()}});
    }

    // ---------------------------------------------------------------- greetz ring
    public static object BuildGreetz()
    {
        Require();var d=Data();var owner=Owner();
        Folder(Root+"/Meshes");Folder(Root+"/Materials");Folder(Root+"/Textures");
        var ring=d["ring"];var atlas=d["atlas"];var curve=d["curve"];var opacity=d["opacity"];
        var atlasTexture=Tex((string)atlas["file"],true);
        var mesh=RingMesh(ring);
        var mat=Mat("GreetzRing","OldWebMap/V5/Sine Scroller",3010);SetTexture(mat,"_MainTex",atlasTexture);
        mat.SetFloat("_UseTimeline",1);mat.SetFloat("_ScrollRate",0);mat.SetFloat("_ArcLength",Num(ring["arc_length_m"]));mat.SetFloat("_PxPerMetre",Num(ring["px_per_metre"]));
        mat.SetVector("_Atlas",new Vector4(Num(atlas["width"]),Num(atlas["height"]),Num(atlas["row_px"]),Num(atlas["rows"])));
        mat.SetVector("_Row",new Vector4(Num(atlas["row_stride"]),Num(atlas["row_pad"]),Num(atlas["text_length_px"]),0));
        mat.SetVector("_Grad",new Vector4(Num(atlas["grad_top_px"]),Num(atlas["grad_bottom_px"]),Num(atlas["baseline_px"]),0));
        mat.SetFloat("_Amp",Num(ring["amplitude"]));mat.SetFloat("_Wave",Num(ring["wavelength"]));mat.SetFloat("_Omega",Num(ring["omega"]));mat.SetFloat("_HueSpeed",Num(ring["hue_speed"]));mat.SetFloat("_EndFade",Num(ring["end_fade"]));
        var shadow=ring["shadow_px"];mat.SetVector("_Shadow",new Vector4(Num(shadow[0]),Num(shadow[1]),.55f,0));
        var performance=owner.Find("Revision4Performance");if(performance==null)throw new InvalidOperationException("Revision4Performance missing");
        var ringT=performance.Find(RingName);if(ringT==null)ringT=Child(RingName,performance).transform;
        ringT.localPosition=new Vector3(Num(ring["centre_x"]),Num(ring["root_y"]),Num(ring["centre_z"]));ringT.localRotation=Quaternion.identity;ringT.localScale=Vector3.one;ringT.gameObject.SetActive(true);
        var mf=ringT.GetComponent<MeshFilter>();if(mf==null)mf=ringT.gameObject.AddComponent<MeshFilter>();mf.sharedMesh=mesh;
        var mr=ringT.GetComponent<MeshRenderer>();if(mr==null)mr=ringT.gameObject.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;Style(mr);
        foreach(var collider in ringT.GetComponents<Collider>())Object.DestroyImmediate(collider);
        var director=F<OWEpisodeDirector>();if(director==null)throw new InvalidOperationException("OWEpisodeDirector missing");
        var g=ringT.GetComponent<OWV5Greetz>();if(g==null)g=ringT.gameObject.AddUdonSharpComponent<OWV5Greetz>();
        g.director=director;g.ringRenderer=mr;g.ringMaterial=mat;g.autoSample=true;
        g.curvePx=curve["px"].Select(x=>(float)x).ToArray();g.curveStart=Num(curve["start"]);g.curveStep=Num(curve["step"]);
        g.fadeInStart=Num(opacity["fade_in_start"]);g.fadeInLength=Num(opacity["fade_in_length"]);g.dimStart=Num(opacity["dim_start"]);g.dimEnd=Num(opacity["dim_end"]);g.dimOpacity=Num(opacity["dim_opacity"]);g.dimRamp=Num(opacity["dim_ramp"]);g.fadeOutLength=Num(opacity["fade_out_length"]);g.duration=director.duration;
        g.Sample(0f);Sync(g);
        EditorUtility.SetDirty(mat);EditorUtility.SetDirty(ringT.gameObject);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("identity-greetz-build.json",new{ring=ringT.name,parent=performance.name,position=new[]{ringT.position.x,ringT.position.y,ringT.position.z},vertices=mesh.vertexCount,segments=mesh.vertexCount/2-1,curvePoints=g.curvePx.Length,atlas=atlasTexture.width+"x"+atlasTexture.height,tokens=((JArray)d["tokens"]).Count,udon=g.GetType().Name});
    }

    // ---------------------------------------------------------------- validation
    public static object Validate()
    {
        Require();var d=Data();var owner=Owner();var rows=new List<object>();bool all=true;
        void Check(string name,bool ok,object detail=null){rows.Add(new{name,ok,detail});if(!ok)all=false;}
        var ring=d["ring"];var atlas=d["atlas"];var win=d["window"];var tokens=(JArray)d["tokens"];var curve=d["curve"];
        // ---- persisted assets
        string[] textureFiles={(string)atlas["file"],(string)win["file"],(string)d["logo"]["file"]};
        foreach(string file in textureFiles)
        {
            string path=Root+"/Textures/"+file;var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var imp=AssetImporter.GetAtPath(path) as TextureImporter;
            bool data=file!=(string)d["logo"]["file"];
            Check("texture "+file,tex!=null&&imp!=null&&TexOk(imp,data),tex==null?"missing":tex.width+"x"+tex.height+" filter="+tex.filterMode+" mips="+(imp!=null&&imp.mipmapEnabled));
        }
        var atlasTex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+(string)atlas["file"]);
        Check("atlas size matches data",atlasTex!=null&&atlasTex.width==(int)atlas["width"]&&atlasTex.height==(int)atlas["height"]);
        string[] meshNames={"CracktroFrame","CracktroInset","CracktroField","CracktroLogo","CracktroScroller","GreetzRing"};
        var meshes=new Dictionary<string,Mesh>();
        foreach(string name in meshNames){var m=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+name+".asset");meshes[name]=m;Check("mesh "+name,m!=null&&AssetDatabase.Contains(m)&&m.vertexCount>=4,m==null?"missing":m.vertexCount+" vertices");}
        if(meshes["GreetzRing"]!=null)Check("ring mesh has >=512 segments",meshes["GreetzRing"].vertexCount/2-1>=512,meshes["GreetzRing"].vertexCount/2-1);
        if(meshes["CracktroFrame"]!=null){var s=meshes["CracktroFrame"].bounds.size;Check("frame outer size 2.9 x 2.3",Mathf.Abs(s.x-2.9f)<.01f&&Mathf.Abs(s.y-2.3f)<.01f,new[]{s.x,s.y,s.z});}
        var shaderNames=new[]{"OldWebMap/V5/Cracktro Field","OldWebMap/V5/Sine Scroller"};
        foreach(string shaderName in shaderNames){var shader=Shader.Find(shaderName);var errors=shader==null?new[]{"missing"}:ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Select(m=>m.message+" line "+m.line).ToArray();Check("shader "+shaderName,shader!=null&&shader.isSupported&&errors.Length==0,errors);}
        string[] materialNames={"CracktroField","CracktroMetal","CracktroLogo","CracktroScroller","GreetzRing"};
        foreach(string name in materialNames){var m=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");Check("material "+name,m!=null&&AssetDatabase.Contains(m)&&m.shader!=null&&m.shader.name.StartsWith("OldWebMap/V5/"),m==null?"missing":m.shader.name);}
        var ringMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/GreetzRing.mat");
        Check("ring material bound to atlas",ringMaterial!=null&&ringMaterial.GetTexture("_MainTex")==atlasTex&&Mathf.Abs(ringMaterial.GetVector("_Row").x-(float)atlas["row_stride"])<.01f);
        // ---- cracktro window
        var p=F<OWV4Presentation>();var root=owner.Find(WindowName);
        Check("presentation startupObject is the cracktro window",p!=null&&root!=null&&p.startupObject==root);
        var old=owner.Find("One waiting terminal");Check("old waiting terminal inactive",old==null||!old.gameObject.activeSelf);
        var canvas=Object.FindObjectsOfType<Canvas>(true).FirstOrDefault(c=>c.name==CanvasName);
        if(root!=null&&canvas!=null)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.activeSelf&&r.enabled).ToArray();
            Check("window has frame, inset, field, logo, scroller renderers",new[]{"Frame","Inset","Field","Logo","Scroller"}.All(n=>renderers.Any(r=>r.name==n)),renderers.Select(r=>r.name).ToArray());
            Check("window has no colliders",root.GetComponentsInChildren<Collider>(true).Length==0);
            Check("window sits 0.22 m behind the canvas, same facing",Vector3.Distance(root.position,canvas.transform.position+canvas.transform.forward*CanvasBack)<.002f&&Quaternion.Angle(root.rotation,canvas.transform.rotation)<.01f,root.position.ToString());
            var frame=root.Find("Frame");var field=root.Find("Field");
            if(frame!=null&&field!=null)
            {
                var fb=frame.GetComponent<MeshFilter>().sharedMesh.bounds.size;var ib=field.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                Check("frame 2.9 x 2.3 and field inside it",Mathf.Abs(fb.x-2.9f)<.02f&&Mathf.Abs(fb.y-2.3f)<.02f&&ib.x<fb.x&&ib.y<fb.y,new[]{fb.x,fb.y,ib.x,ib.y});
                var canvasRect=(RectTransform)canvas.transform;float cw=canvasRect.rect.width*canvas.transform.lossyScale.x,ch=canvasRect.rect.height*canvas.transform.lossyScale.y;
                float k=root.lossyScale.x;Check("canvas inside field with room for logo and scroller",cw<(ib.x-.4f)*k&&ch<(ib.y-.8f)*k,new[]{cw,ch,k});
            }
            // canvas intact
            var button=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault();var handler=button!=null?button.GetComponent<OWEpisodeButton>():null;var vm=handler!=null?UdonSharpEditorUtility.GetBackingUdonBehaviour(handler):null;
            bool wired=false;if(button!=null)for(int i=0;i<button.onClick.GetPersistentEventCount();i++)if(vm!=null&&button.onClick.GetPersistentTarget(i)==vm&&button.onClick.GetPersistentMethodName(i)=="SendCustomEvent")wired=true;
            Check("button onClick still wired to OWEpisodeButton Udon",wired&&button.interactable);
            Check("canvas components intact",canvas.GetComponent<VRCUiShape>()!=null&&canvas.GetComponent<GraphicRaycaster>()!=null&&canvas.GetComponent<BoxCollider>()!=null&&canvas.gameObject.layer==17&&canvas.renderMode==RenderMode.WorldSpace,"layer "+canvas.gameObject.layer);
            var panel=canvas.transform.Find("Panel");var panelImage=panel!=null?panel.GetComponent<Image>():null;
            Check("panel tint (0,0,0,0.55)",panelImage!=null&&Mathf.Abs(panelImage.color.a-.55f)<.01f&&panelImage.color.r<.01f&&panelImage.color.g<.01f&&panelImage.color.b<.01f);
            Check("button tint metal blue",button!=null&&button.GetComponent<Image>()!=null&&Mathf.Abs(button.GetComponent<Image>().color.r-.30f)<.01f&&Mathf.Abs(button.GetComponent<Image>().color.b-.85f)<.01f);
            var texts=canvas.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.text).ToArray();
            Check("canvas texts free of forbidden words",texts.All(t=>Clean(t)),texts);
        }
        else Check("window and canvas present",false,"root="+(root!=null)+" canvas="+(canvas!=null));
        // ---- greetz ring
        var performance=owner.Find("Revision4Performance");var ringT=performance!=null?performance.Find(RingName):null;
        Check("ring object under Revision4Performance",ringT!=null);
        if(ringT!=null)
        {
            var journey=F<OWV4Journey>();Check("ring not inside the shared world frame",journey==null||journey.worldFrame==null||!ringT.IsChildOf(journey.worldFrame));
            Vector3 expected=new Vector3(Num(ring["centre_x"]),Num(ring["root_y"]),Num(ring["centre_z"]));
            Check("ring pose",Vector3.Distance(ringT.position,expected)<.01f&&Quaternion.Angle(ringT.rotation,Quaternion.identity)<.01f&&ringT.lossyScale==Vector3.one,ringT.position.ToString());
            var g=ringT.GetComponent<OWV5Greetz>();
            Check("OWV5Greetz linked",g!=null&&g.director!=null&&g.ringRenderer==ringT.GetComponent<MeshRenderer>()&&g.ringMaterial==ringMaterial&&g.curvePx!=null&&g.curvePx.Length==curve["px"].Count());
            var mf=ringT.GetComponent<MeshFilter>();var mesh=mf!=null?mf.sharedMesh:null;Check("ring mesh assigned",mesh!=null&&mesh==meshes["GreetzRing"]);
            if(mesh!=null)
            {
                var verts=mesh.vertices;var eye=new Vector3(Num(ring["eye"][0]),Num(ring["eye"][1]),Num(ring["eye"][2]));
                float amp=Num(ring["amplitude"]),baselineFrom=Num(ring["baseline_from_bottom_m"]),below=Num(ring["ink_bottom_from_baseline_m"]),above=Num(ring["ink_top_from_baseline_m"]);
                float minProjected=float.MaxValue,minElev=float.MaxValue,maxElev=float.MinValue,minClear=float.MaxValue;
                // focus rectangle (region-local, widest/tallest events of the V4 score): x +-2.0, y 1.275..4.025 at z 5.4
                float azMin=float.MaxValue,azMax=float.MinValue,eMin=float.MaxValue,eMax=float.MinValue;
                foreach(float fx in new[]{-2f,0f,2f})foreach(float fy in new[]{1.275f,4.025f}){float dz=5.4f-eye.z,dx=fx-eye.x;float az=Mathf.Atan2(dx,dz)*Mathf.Rad2Deg;float el=Mathf.Atan2(fy-eye.y,Mathf.Sqrt(dx*dx+dz*dz))*Mathf.Rad2Deg;azMin=Mathf.Min(azMin,az);azMax=Mathf.Max(azMax,az);eMin=Mathf.Min(eMin,el);eMax=Mathf.Max(eMax,el);}
                bool intersects=false;
                for(int j=0;j<verts.Length/2;j++)
                {
                    var b=verts[2*j];var wb=ringT.TransformPoint(new Vector3(b.x,b.y+baselineFrom-below-amp,b.z));var wt=ringT.TransformPoint(new Vector3(b.x,b.y+baselineFrom+above+amp,b.z));
                    float dxb=wb.x-eye.x,dzb=wb.z-eye.z,dh=Mathf.Sqrt(dxb*dxb+dzb*dzb);
                    float elb=Mathf.Atan2(wb.y-eye.y,dh)*Mathf.Rad2Deg,elt=Mathf.Atan2(wt.y-eye.y,dh)*Mathf.Rad2Deg,az=Mathf.Atan2(dxb,dzb)*Mathf.Rad2Deg;
                    minElev=Mathf.Min(minElev,elb);maxElev=Mathf.Max(maxElev,elt);
                    if(dzb>.5f)foreach(float zp in new[]{4.6f,5.3f,6.5f,8f}){float f=(zp-eye.z)/dzb;minProjected=Mathf.Min(minProjected,eye.y+(wb.y-eye.y)*f);}
                    if(az>=azMin&&az<=azMax){minClear=Mathf.Min(minClear,elb-eMax);if(elb<=eMax&&elt>=eMin)intersects=true;}
                }
                Check("ring ink (with wobble) projects above y=4.2 for z 4.6..8",minProjected>=4.2f,minProjected);
                Check("ring never intersects the focus rectangle",!intersects&&minClear>1f,"clearance degrees "+minClear+" ring elevation "+minElev+".."+maxElev+" focus elevation "+eMin+".."+eMax);
                Check("ring elevation band near 24 degrees centre",(minElev+maxElev)/2>21f&&(minElev+maxElev)/2<26f,(minElev+maxElev)/2);
            }
            // scroll monotonic, opacity script behaviour, runtime pure function
            var px=curve["px"].Select(x=>(float)x).ToArray();bool monotone=true;for(int i=1;i<px.Length;i++)if(px[i]<px[i-1])monotone=false;
            Check("baked scroll is monotonic",monotone&&px.Length>=300,px.Length);
            Check("curve covers the performance",Mathf.Abs(Num(curve["start"])+Num(curve["step"])*(px.Length-1)-133.12f)<.02f);
            if(g!=null)
            {
                float last=-1,worst=0;bool runtimeMonotone=true;
                for(float t=0;t<=133.12f;t+=.08f){g.Sample(t);if(g.sampledScroll<last-1e-3f)runtimeMonotone=false;last=g.sampledScroll;int i=Mathf.Clamp(Mathf.FloorToInt((t-Num(curve["start"]))/Num(curve["step"])),0,px.Length-2);float f=Mathf.Clamp01((t-Num(curve["start"]))/Num(curve["step"])-i);float expect=t<=Num(curve["start"])?px[0]:Mathf.Lerp(px[i],px[i+1],f);worst=Mathf.Max(worst,Mathf.Abs(expect-g.sampledScroll));}
                Check("runtime scroll monotonic and equal to curve",runtimeMonotone&&worst<.05f,worst);
                g.Sample(100f);float a=g.sampledScroll;g.Sample(50f);g.Sample(100f);Check("Sample is a pure function of time",Mathf.Approximately(a,g.sampledScroll));
                float[] times={0f,30.72f,31.36f,32f,60f,81.5f,86f,93f,125f,132.72f,133.12f};float[] want={0f,0f,.5f,1f,1f,1f,.55f,1f,1f,.5f,0f};
                var seen=new List<float>();bool opacityOk=true;
                for(int i=0;i<times.Length;i++){g.Sample(times[i]);seen.Add(g.sampledOpacity);if(i==2||i==9){if(g.sampledOpacity<=.05f||g.sampledOpacity>=.95f)opacityOk=false;}else if(Mathf.Abs(g.sampledOpacity-want[i])>.02f)opacityOk=false;}
                Check("opacity: hidden, 1.28 s fade-in, 55% climax, fade-out",opacityOk,seen);
                g.Sample(0f);Check("ring hidden at time 0",!g.ringRenderer.enabled&&g.sampledOpacity==0f);
            }
            // tokens in atlas order
            float stride=Num(atlas["row_stride"]),pad=Num(atlas["row_pad"]),length=Num(atlas["text_length_px"]);int atlasRows=(int)atlas["rows"];
            bool ordered=true;float prevEnd=0;foreach(var t in tokens){float s=Num(t["start"]),e=Num(t["end"]);if(s<prevEnd-.01f||e<=s)ordered=false;prevEnd=e;}
            Check("tokens strictly in atlas order, inside text length",ordered&&prevEnd<=length&&atlasRows*stride+pad>=length&&atlasRows*Num(atlas["row_px"])<=Num(atlas["height"]),new{count=tokens.Count,length,atlasRows});
            Check("interrupt tokens flagged (4) and in segment B",tokens.Count(t=>(int)t["style"]==1)==4&&tokens.Where(t=>(int)t["style"]==1).All(t=>(string)t["segment"]=="B"));
            var aTimes=tokens.Where(t=>(string)t["segment"]=="A").Select(t=>t["front_time"].Type==JTokenType.Null?-1f:Num(t["front_time"])).ToArray();bool aOrdered=aTimes.All(t=>t>=30.72f&&t<=102.4f);for(int i=1;i<aTimes.Length;i++)if(aTimes[i]<=aTimes[i-1])aOrdered=false;
            Check("A phrases cross the front in order between 30.72 and 102.40 s",aOrdered,aTimes);
            float speedA=Num(d["stats"]["a_speed_em_per_s"]);Check("A speed <= 2.3 em/s",speedA<=2.3f,speedA);
            var strings=tokens.Select(t=>(string)t["text"]).Concat(new[]{(string)win["text"],(string)d["logo"]["text"]}).Concat(root!=null?root.GetComponentsInChildren<Transform>(true).Select(t=>t.name):new string[0]).Concat(materialNames).Concat(meshNames).Concat(new[]{RingName,WindowName}).ToArray();
            Check("no forbidden program words in authored text/names",strings.All(Clean),strings.Where(s=>!Clean(s)).ToArray());
        }
        var result=new{success=all,checks=rows};string json=Report("identity-validate.json",result);
        if(!all)throw new InvalidOperationException(json);
        return new{success=true,checks=rows.Count,report=Tmp+"/reports/identity-validate.json"};
    }
    static bool Clean(string text){if(string.IsNullOrEmpty(text))return true;if(ForbiddenEnglish.IsMatch(text))return false;foreach(string w in ForbiddenChinese)if(text.Contains(w))return false;return true;}
}
