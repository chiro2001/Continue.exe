using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using TMPro;
using OldWebMap;
using UdonSharp;
using UdonSharpEditor;
using Newtonsoft.Json;
using Object=UnityEngine.Object;

/// <summary>Editor-only persisted geometry and cast builder. Run through Unity Pipeline.</summary>
public static class OWEpisodeV4HubCast
{
    const string Root="Assets/OldWebMap/Episode123/Revision4";
    const string ScenePath="Assets/Scenes/OWM_Episode123_v4.unity";
    static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
    static readonly List<object> UsedAssets=new List<object>();
    static readonly List<Transform> Cores=new List<Transform>(),Rims=new List<Transform>();
    static readonly List<string> MirrorReports=new List<string>();
    static readonly List<object> CastScaleReports=new List<object>();
    static TMP_FontAsset Font;
    static Material Stone,Brass,Slate,Wood,DarkWood,Grass,Teal,Cyan,Blue,Paper,Red;
    static OWV4HubCast Run;
    static OWV4Journey Journey;
    static string Temp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v4/hub-cast");
    static Color C(string s){Color c;ColorUtility.TryParseHtmlString(s,out c);return c;}
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int at=path.LastIndexOf('/');Folder(path.Substring(0,at));AssetDatabase.CreateFolder(path.Substring(0,at),path.Substring(at+1));}
    static GameObject Child(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
    static Material Mat(string name,string hex,float emission=0)
    {
        if(Mats.TryGetValue(name,out var found))return found;
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=C(hex);m.enableInstancing=true;m.SetFloat("_Glossiness",.16f);
        if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",C(hex)*emission);}else m.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);Mats[name]=m;return m;
    }
    static GameObject Prim(string name,PrimitiveType type,Transform parent,Vector3 p,Vector3 s,Material mat)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=s;
        g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static Transform Box(string name,Transform parent,Vector3 p,Vector3 s,Material mat){return Prim(name,PrimitiveType.Cube,parent,p,s,mat).transform;}
    static TMP_Text Label(string name,string value,Transform parent,Vector3 p,float width,float size,string color="#ecf3ed",TextAlignmentOptions align=TextAlignmentOptions.Center)
    {
        var t=Child(name,parent).AddComponent<TextMeshPro>();t.font=Font;t.text=value;t.fontSize=size*10;t.color=C(color);t.alignment=align;t.enableWordWrapping=false;t.rectTransform.sizeDelta=new Vector2(width,size*3.5f);t.transform.localPosition=p;return t;
    }
    static Transform MeshObject(string name,string id,Transform parent,List<Vector3> vertices,List<int> triangles,List<Vector2> uvs,Material mat)
    {
        string path=Root+"/Meshes/"+id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name=id};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(uvs!=null && uvs.Count==vertices.Count)mesh.SetUVs(0,uvs);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var g=Child(name,parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.On;return g.transform;
    }
    static Transform Polygon(string name,string id,Transform parent,Vector3 position,Vector2[] outline,float thickness,Material mat)
    {
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
        v.Add(new Vector3(0,thickness,0));uv.Add(Vector2.zero);
        foreach(var p in outline){v.Add(new Vector3(p.x,thickness,p.y));uv.Add(p*.3f);}
        for(int i=0;i<outline.Length;i++){t.Add(0);t.Add((i+1)%outline.Length+1);t.Add(i+1);}
        for(int i=0;i<outline.Length;i++)
        {
            int j=(i+1)%outline.Length,n=v.Count;var a=outline[i];var b=outline[j];
            v.Add(new Vector3(a.x,0,a.y));v.Add(new Vector3(b.x,0,b.y));v.Add(new Vector3(b.x,thickness,b.y));v.Add(new Vector3(a.x,thickness,a.y));
            uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.one);uv.Add(Vector2.up);t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        var g=MeshObject(name,id,parent,v,t,uv,mat);g.localPosition=position;return g;
    }
    static Vector2[] Circle(float radius,int count=32){return Enumerable.Range(0,count).Select(i=>new Vector2(Mathf.Cos(i*Mathf.PI*2/count)*radius,Mathf.Sin(i*Mathf.PI*2/count)*radius)).ToArray();}
    static Transform Arc(string name,string id,Transform parent,Vector3 p,float inner,float outer,float from,float to,float height,Material mat,int segments=36)
    {
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
        for(int i=0;i<=segments;i++)
        {
            float a=Mathf.Lerp(from,to,i/(float)segments)*Mathf.Deg2Rad;
            foreach(var q in new[]{new Vector2(inner,0),new Vector2(outer,0),new Vector2(inner,height),new Vector2(outer,height)})
            {v.Add(new Vector3(Mathf.Sin(a)*q.x,q.y,Mathf.Cos(a)*q.x));uv.Add(new Vector2(i/(float)segments*4,q.x==inner?0:1));}
        }
        for(int i=0;i<segments;i++){int n=i*4;t.AddRange(new[]{n+2,n+3,n+6,n+3,n+7,n+6,n,n+4,n+2,n+2,n+4,n+6,n+1,n+3,n+5,n+3,n+7,n+5});}
        t.AddRange(new[]{0,2,1,1,2,3});int e=segments*4;t.AddRange(new[]{e,e+1,e+2,e+1,e+3,e+2});
        var g=MeshObject(name,id,parent,v,t,uv,mat);g.localPosition=p;return g;
    }
    static Transform Torus(string name,string id,Transform parent,Vector3 p,float rx,float ry,float tube,Material mat,int n=48)
    {
        var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
        for(int i=0;i<=n;i++)for(int j=0;j<=6;j++)
        {
            float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/6;v.Add(new Vector3(Mathf.Cos(a)*(rx+Mathf.Cos(b)*tube),Mathf.Sin(a)*(ry+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube));uv.Add(new Vector2(i/(float)n,j/6f));
            if(i<n && j<6){int k=i*7+j;t.AddRange(new[]{k,k+7,k+1,k+1,k+7,k+8});}
        }
        var g=MeshObject(name,id,parent,v,t,uv,mat);g.localPosition=p;return g;
    }
    static Transform Crystal(string name,string id,Transform parent,Vector3 p,float width,float height,Material mat)
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<6;i++){float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;int n=v.Count;v.Add(new Vector3(0,height*.5f,0));v.Add(new Vector3(Mathf.Sin(a)*width,0,Mathf.Cos(a)*width));v.Add(new Vector3(Mathf.Sin(b)*width,0,Mathf.Cos(b)*width));v.Add(new Vector3(0,-height*.5f,0));t.AddRange(new[]{n,n+1,n+2,n+3,n+2,n+1});}
        var g=MeshObject(name,id,parent,v,t,null,mat);g.localPosition=p;return g;
    }
    static Texture2D Texture(string name,int kind)
    {
        string path=Root+"/Textures/"+name+".asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(tex==null){tex=new Texture2D(256,256,TextureFormat.RGBA32,true){name=name,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};AssetDatabase.CreateAsset(tex,path);}
        var pixels=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float n=Mathf.PerlinNoise(x*.19f,y*.19f),fine=Mathf.PerlinNoise(x*.73f+17f,y*.73f);
            float lum=.83f+n*.11f+fine*.06f;
            if(kind==0){bool seam=x%64<2 || y%64<2;lum=seam?.58f:lum;}
            if(kind==1){float grain=Mathf.PerlinNoise(x*.7f,y*.025f);lum=.60f+grain*.30f+fine*.10f;if(x%48<2)lum*=.6f;}
            if(kind==2)lum=.70f+Mathf.PerlinNoise(x*.06f,y*.06f)*.25f+fine*.05f;
            pixels[y*256+x]=new Color(lum,lum,lum,1);
        }
        tex.SetPixels(pixels);tex.Apply(true,false);EditorUtility.SetDirty(tex);return tex;
    }
    static Material PortalMaterial()
    {
        string path=Root+"/Textures/PortalIon.asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(tex==null){tex=new Texture2D(256,256,TextureFormat.RGBA32,true){name="PortalIon",wrapMode=TextureWrapMode.Clamp};AssetDatabase.CreateAsset(tex,path);}
        var pixels=new Color[256*256];
        for(int y=0;y<256;y++)for(int x=0;x<256;x++)
        {
            float dx=(x-127.5f)/127.5f,dy=(y-127.5f)/127.5f,r=Mathf.Sqrt(dx*dx+dy*dy),a=Mathf.Atan2(dy,dx);
            float bands=.5f+.5f*Mathf.Sin(r*62f-a*5f+Mathf.PerlinNoise(x*.08f,y*.08f)*5f);
            Color c=Color.Lerp(C("#11234f"),C("#2ab6ef"),bands*.65f);c=Color.Lerp(c,C("#e5d6ff"),Mathf.Pow(bands,9)*.65f);c.a=r<1?Mathf.Clamp01((1-r)*14):0;pixels[y*256+x]=c;
        }
        tex.SetPixels(pixels);tex.Apply();EditorUtility.SetDirty(tex);
        var m=Mat("Portal ion surface","#ffffff",.3f);m.shader=Shader.Find("Unlit/Transparent");m.mainTexture=tex;EditorUtility.SetDirty(m);return m;
    }
    static Transform Portal(Transform parent,string id,Vector3 position,string title,bool crossPlatform=false)
    {
        var root=Child("Portal · "+title,parent).transform;root.localPosition=position;
        Box("Portal stone or timber surround",root,new Vector3(0,1.23f,.13f),new Vector3(1.75f,2.7f,.33f),parent.name.Contains("2019")?DarkWood:Stone);
        var dark=Mat("Portal recess","#0c1723");var back=Prim("Recess",PrimitiveType.Quad,root,new Vector3(0,1.23f,-.045f),new Vector3(1.55f,2.45f,1),dark);
        var aperture=Child("Elliptical portal aperture",root).transform;aperture.localPosition=new Vector3(0,1.32f,-.08f);aperture.localScale=new Vector3(1.44f,1.94f,1);
        var core=Prim("Animated circular world surface",PrimitiveType.Quad,aperture,Vector3.zero,Vector3.one,PortalMaterial()).transform;Cores.Add(core);
        Rims.Add(Torus("Faceted world threshold",id+"_rim",root,new Vector3(0,1.32f,-.10f),.76f,1.02f,.045f,Cyan));
        Torus("Outer engraved portal rim",id+"_outer",root,new Vector3(0,1.32f,-.07f),.83f,1.10f,.025f,Brass);
        Label("World name",title,root,new Vector3(0,1.25f,-.12f),1.5f,.14f);
        Label("Portal state",crossPlatform?"PC  +  QUEST":"PUBLIC  ·  JOIN",root,new Vector3(0,.40f,-.13f),1.4f,.065f,"#9ce5eb");
        if(crossPlatform)PlatformIcons(root,new Vector3(.37f,2.14f,-.15f),.105f);
        return root;
    }
    static void PlatformIcons(Transform parent,Vector3 p,float size)
    {
        foreach(int i in new[]{0,1})
        {
            var d=Prim(i==0?"PC blue icon":"Quest green icon",PrimitiveType.Cylinder,parent,p+new Vector3(i*size*1.2f,0,0),new Vector3(size,.014f,size),i==0?Blue:Mat("Quest green","#08d65a",.55f));d.transform.localRotation=Quaternion.Euler(90,0,0);
        }
    }
    static Transform Board(Transform parent,string id,Vector3 p,string heading,string body,float width=1.4f)
    {
        var g=Child("2017 sign · "+heading,parent).transform;g.localPosition=p;
        Box("Brass sign slab",g,Vector3.zero,new Vector3(width+.14f,2.30f,.13f),Brass);
        Box("Recessed dark enamel",g,new Vector3(0,0,-.075f),new Vector3(width,2.14f,.035f),Teal);
        foreach(float x in new[]{-.5f,.5f}){var a=Box("Splayed brass sign foot",g,new Vector3(x*(width+.03f),-1.28f,.035f),new Vector3(.12f,.43f,.26f),Brass);a.localRotation=Quaternion.Euler(0,0,x*12f);}
        Label("Original sign title",heading,g,new Vector3(0,.82f,-.102f),width,.115f);
        Label("Readable supporting text",body,g,new Vector3(0,-.33f,-.103f),width,.075f);
        return g;
    }
    static void Steps(Transform parent,string id,Vector3 p,float width,int count,Material mat)
    {
        for(int i=0;i<count;i++)Box("Human-scale stair tread "+i,parent,p+new Vector3(0,i*.105f,i*.23f),new Vector3(width,.10f,.28f),mat);
        for(int side=-1;side<=1;side+=2){Box("Stepped balustrade base",parent,p+new Vector3(side*(width*.5f+.10f),count*.065f,count*.12f),new Vector3(.16f,count*.14f,count*.24f),Stone);Box("Gold stair edge",parent,p+new Vector3(side*(width*.5f+.1f),count*.14f,count*.12f),new Vector3(.18f,.04f,count*.24f),Brass);}
    }
    static void Bench(Transform parent,string id,Vector3 p,float yaw=0)
    {
        var g=Child("Octagonal social seat",parent).transform;g.localPosition=p;g.localRotation=Quaternion.Euler(0,yaw,0);
        Polygon("Faceted stone seat",id+"_seat",g,new Vector3(0,.30f,0),Circle(.64f,8),.12f,Stone);
        Polygon("Blue inset seat top",id+"_inset",g,new Vector3(0,.425f,0),Circle(.53f,8),.035f,Slate);
        foreach(float x in new[]{-.38f,.38f})Box("Seat plinth leg",g,new Vector3(x,.15f,0),new Vector3(.18f,.30f,.38f),Stone);
    }
    static void Build2017(Transform parent)
    {
        var g=Child("2017 New Hub · compressed documented areas",parent).transform;Run.hub2017=g;
        var cross=new[]{new Vector2(-2.7f,-3.2f),new Vector2(2.7f,-3.2f),new Vector2(2.7f,-1.8f),new Vector2(8f,-1.8f),new Vector2(8f,5.8f),new Vector2(3.5f,5.8f),new Vector2(3.5f,9f),new Vector2(-3.5f,9f),new Vector2(-3.5f,5.8f),new Vector2(-8f,5.8f),new Vector2(-8f,-1.8f),new Vector2(-2.7f,-1.8f)};
        Polygon("Chamfered cruciform white-stone base","hub17_base",g,new Vector3(0,-.25f,0),cross,.23f,Stone);
        Polygon("Dark blue tiled walking court","hub17_floor",g,new Vector3(0,-.025f,0),cross,.025f,Slate);
        Steps(g,"entry",new Vector3(0,-.19f,-3.72f),4.8f,3,Stone);
        // Low parapets preserve a walkable foreground; the open side makes depth legible.
        foreach(float x in new[]{-7.7f,7.7f}){Box("Courtyard edge stone",g,new Vector3(x,.30f,1.8f),new Vector3(.28f,.6f,7.2f),Stone);Box("Inset gold upper edge",g,new Vector3(x,.615f,1.8f),new Vector3(.30f,.045f,7.3f),Brass);}
        foreach(float x in new[]{-3.5f,3.5f})
        {
            var b=Board(g,x<0?"nice":"travel",new Vector3(x,1.65f,-1.0f),x<0?"BE NICE":"TRAVEL MAPS",x<0?"Be friendly.\nHelp each other.\nMake something.":"Find other users\nby the dots\non the map.",1.4f);b.localRotation=Quaternion.Euler(0,x<0?-9:9,0);
            if(x>0)
            {
                var map=Child("Live local travel map",b).transform;map.localPosition=new Vector3(0,.24f,-.12f);Box("Cross-shaped map north-south",map,Vector3.zero,new Vector3(.28f,.61f,.018f),Paper);Box("Cross-shaped map east-west",map,Vector3.zero,new Vector3(.85f,.26f,.018f),Paper);
                Run.travelDots=new Transform[4];for(int i=0;i<4;i++){var dot=Prim("User dot "+i,PrimitiveType.Sphere,map,new Vector3(0,0,-.035f),Vector3.one*.065f,Red);Run.travelDots[i]=dot.transform;}
            }
        }
        // Entrance: stepped octagonal drums, gold bands, open doorway, split obelisks.
        var tower=Child("Layered entrance pavilion",g).transform;tower.localPosition=new Vector3(0,0,6.3f);
        foreach(int level in new[]{0,1,2})
        {
            float radius=3.15f-level*.48f,y=level*1.0f;
            Arc("Broken octagonal gallery wall","entrance_wall_"+level,tower,new Vector3(0,y,0),radius-.23f,radius,-142,142,.78f,Stone,20);
            Arc("Gold horizontal building band","entrance_band_"+level,tower,new Vector3(0,y+.79f,0),radius-.24f,radius+.08f,-142,142,.13f,Brass,20);
        }
        foreach(float x in new[]{-1.0f,1.0f})Box("Entrance stone jamb",tower,new Vector3(x,1.0f,-2.45f),new Vector3(.35f,2,.7f),Stone);
        Box("Open doorway carved cap",tower,new Vector3(0,2.08f,-2.45f),new Vector3(2.35f,.35f,.8f),Stone);
        Label("Pavilion identity","VRChat",tower,new Vector3(0,2.02f,-2.89f),1.7f,.16f,"#1d4b58");
        foreach(float x in new[]{-.55f,.55f})
        {
            for(int h=0;h<3;h++){var a=Box("Split fin obelisk",tower,new Vector3(x,3.35f+h*.65f,0),new Vector3(.40f,.53f,.62f),Stone);a.localRotation=Quaternion.Euler(0,0,x<0?8:-8);}
        }
        // Avatar Hall: real floor, framing piers, upper beams, selector plinths, native mirror.
        var hall=Child("Avatar Hall and mirror room",g).transform;hall.localPosition=new Vector3(-5f,0,2.55f);
        Box("Raised avatar hall foundation",hall,new Vector3(0,.09f,.5f),new Vector3(4.5f,.18f,4.0f),Stone);Box("Avatar hall blue inset",hall,new Vector3(0,.19f,.5f),new Vector3(4.15f,.035f,3.65f),Slate);
        for(int s=-1;s<=1;s+=2)
        {
            Box("Framed stone mirror pier",hall,new Vector3(s*2.1f,1.75f,1.65f),new Vector3(.36f,3.1f,.65f),Stone);
            for(int l=0;l<2;l++)Box("Upper gold gallery beam",hall,new Vector3(0,2.85f+l*.55f,1.65f),new Vector3(4.55f,.18f,.24f),Brass);
        }
        Run.mirror2017=Mirror(hall,"2017",new Vector3(0,1.42f,1.52f),new Vector2(3.75f,2.18f));
        Label("Avatar hall sign","AVATAR HALL",hall,new Vector3(0,2.82f,1.24f),3.5f,.17f,"#21515d");
        Board(hall,"avatar_by_you",new Vector3(-1.5f,1.25f,-.7f),"AVATAR BY: YOU!","YOUR AVATAR HERE\nMake a great avatar\nfor everyone.",.7f).localScale=Vector3.one*.58f;
        Bench(hall,"avatar_seat",new Vector3(.5f,.2f,-.2f));
        for(int i=0;i<3;i++)
        {
            float x=3.9f+i*1.8f;var p=Portal(g,"hub17_world_"+i,new Vector3(x,0,2.0f+i*.6f),new[]{"New Worlds","Featured","Help Videos"}[i]);p.localRotation=Quaternion.Euler(0,-12f,0);
            Box("Portal crown stone",p,new Vector3(0,2.79f,.16f),new Vector3(1.95f,.24f,.42f),Stone);
            Torus("Gold gear crown","world_crown_"+i,p,new Vector3(0,3.15f,.1f),.35f,.35f,.10f,Brass,12);
        }
        var events=Child("Event Hall · blue hanging crystals",g).transform;events.localPosition=new Vector3(5.0f,0,6.8f);
        for(int l=0;l<3;l++)Arc("Open Event Hall stone ring","event_ring_"+l,events,new Vector3(0,1.35f+l*.75f,0),2.1f,2.32f,-135,135,.19f,l==1?Brass:Stone,24);
        for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector3 p=new Vector3(Mathf.Sin(a)*1.7f,2.1f,Mathf.Cos(a)*1.7f);Crystal("Hanging blue event crystal","event_crystal_"+i,events,p,.15f,1.6f,Blue);Box("Crystal suspension",events,p+Vector3.up*.9f,new Vector3(.075f,.35f,.075f),Brass);}
        Label("Event hall fascia","EVENT HALL",events,new Vector3(0,3.25f,-2.0f),3.5f,.16f);
        Bench(g,"social_left",new Vector3(-1.55f,0,.75f));Bench(g,"social_right",new Vector3(1.2f,0,2.6f));
        // Lawn strips, polygonal Hub trees and short wall modules make the silhouette specific.
        foreach(float x in new[]{-7.15f,7.15f})
        {
            Box("Grass garden strip",g,new Vector3(x,.018f,4f),new Vector3(.7f,.04f,3.2f),Grass);
            for(int i=0;i<3;i++)HubTree(g,new Vector3(x,0,3f+i*1.2f),i+(x<0?10:20),false);
        }
        PointLight(g,new Vector3(-2,4,0),C("#dfedf3"),1.0f,18);
        PointLight(g,new Vector3(5,3,5),C("#63c4ff"),.65f,8);
    }
    static void HubTree(Transform parent,Vector3 p,int id,bool warm)
    {
        var g=Child(warm?"2019 angular potted foliage":"2017 geometric Hub tree",parent).transform;g.localPosition=p;
        var trunk=Box("Trunk",g,new Vector3(0,.9f,0),new Vector3(.16f,1.8f,.17f),Wood);trunk.localRotation=Quaternion.Euler(0,0,id%2==0?10:-8);
        for(int i=0;i<3;i++)
        {
            var leaf=Box("Broad angular leaf cluster",g,new Vector3((i-1)*.25f,1.55f+i*.22f,0),new Vector3(.72f,.52f,.62f),Mat(warm?"Home leaves":"Hub leaves",warm?"#17563d":"#405d4d"));leaf.localRotation=Quaternion.Euler(10,i*34+id*13,i%2==0?18:-13);
        }
    }
    static GameObject Mirror(Transform parent,string id,Vector3 p,Vector2 size)
    {
        Box("Stone mirror backing",parent,p+new Vector3(0,0,.08f),new Vector3(size.x+.17f,size.y+.17f,.14f),Stone);
        var surface=Prim("SDK mirror · "+id,PrimitiveType.Quad,parent,p,new Vector3(size.x,size.y,1),Mat("Mirror fallback silver","#526571"));
        var shader=Shader.Find("FX/MirrorReflection");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VRC.SDK3.Components.VRCMirrorReflection",false)).FirstOrDefault(t=>t!=null);
        if(shader!=null && type!=null)
        {
            string mp=Root+"/Materials/Mirror_"+id+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,mp);}else mat.shader=shader;
            string fallbackPath=Root+"/Textures/MirrorUninitialized.asset";var fallback=AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPath);
            if(fallback==null)
            {
                fallback=new Texture2D(16,16,TextureFormat.RGBA32,false){name="Neutral uninitialized mirror reflection",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var pixels=new Color[16*16];for(int y=0;y<16;y++)for(int x=0;x<16;x++)pixels[y*16+x]=Color.Lerp(C("#344c5b"),C("#607a87"),y/15f);
                fallback.SetPixels(pixels);fallback.Apply();AssetDatabase.CreateAsset(fallback,fallbackPath);
            }
            // SDK replaces these per eye when reflection rendering is initialized.
            // Neutral defaults only prevent an uninitialized editor view becoming white.
            mat.SetTexture("_MainTex",Texture2D.whiteTexture);mat.SetTexture("_ReflectionTex0",fallback);mat.SetTexture("_ReflectionTex1",fallback);EditorUtility.SetDirty(mat);
            surface.GetComponent<Renderer>().sharedMaterial=mat;surface.AddComponent(type);MirrorReports.Add(id+": SDK native stereo mirror retained; two neutral fallback textures for uninitialized offline capture (not a verified reflection)");
        }
        else MirrorReports.Add(id+": native SDK mirror unavailable; silver surface retained, verify before claiming reflection");
        return surface;
    }
    static Light PointLight(Transform parent,Vector3 p,Color c,float intensity,float range)
    {
        var light=Child("Local architectural light",parent).AddComponent<Light>();light.transform.localPosition=p;light.color=c;light.type=LightType.Point;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;return light;
    }
    static void Build2019(Transform parent)
    {
        var g=Child("2019 Hub · campfire timber gallery",parent).transform;Run.hub2019=g;
        Polygon("Dark timber circular deck","hub19_deck",g,new Vector3(0,-.25f,0),Circle(7.4f,40),.25f,Wood);
        Arc("Raised outer boardwalk edge","hub19_edge",g,new Vector3(0,0,0),6.8f,7.4f,0,360,.14f,DarkWood,48);
        Polygon("Green circular campfire lawn","hub19_lawn",g,new Vector3(0,.01f,0),Circle(3.35f,40),.04f,Grass);
        Arc("Lawn stone boundary","hub19_lawn_edge",g,new Vector3(0,0,0),3.35f,3.52f,0,360,.075f,Stone,48);
        for(int i=0;i<7;i++)
        {
            float a=(35+i*46)*Mathf.Deg2Rad;Vector3 p=new Vector3(Mathf.Sin(a)*6.15f,0,Mathf.Cos(a)*6.15f);
            if(p.z<-2.5f)continue;
            var pier=Child("Timber post with stone foot",g).transform;pier.localPosition=p;pier.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
            Box("Stone post shoe",pier,new Vector3(0,.16f,0),new Vector3(.65f,.32f,.65f),Stone);
            Box("Exposed wooden structural post",pier,new Vector3(0,2.35f,0),new Vector3(.45f,4.3f,.48f),Wood);
            Box("Post capital",pier,new Vector3(0,4.4f,0),new Vector3(.85f,.25f,.75f),DarkWood);
            for(int j=0;j<3;j++)Box("Gallery timber ribs",pier,new Vector3(0,1.1f+j*.24f,.45f),new Vector3(.68f,.13f,.9f),Wood);
        }
        Arc("Open timber overhead ring","hub19_roof_edge",g,new Vector3(0,4.5f,0),5.8f,6.6f,-95,95,.26f,DarkWood,24);
        for(int i=0;i<5;i++)
        {
            var beam=Box("Open pergola rafter",g,new Vector3((i-2)*1.4f,4.62f,3.4f),new Vector3(.22f,.28f,4.4f),Wood);beam.localRotation=Quaternion.Euler(0,-14+(i-2)*4,0);
        }
        // The official screenshot shows a bent central tree behind a stone-ring campfire.
        var tree=Child("Angular bent camp tree",g).transform;tree.localPosition=new Vector3(.25f,0,1.1f);
        var tr=Prim("Bent main trunk",PrimitiveType.Cylinder,tree,new Vector3(.1f,1.25f,0),new Vector3(.43f,1.4f,.43f),Wood).transform;tr.localRotation=Quaternion.Euler(0,0,-17);
        var branch=Prim("Bending crown branch",PrimitiveType.Cylinder,tree,new Vector3(-.28f,2.6f,0),new Vector3(.33f,.63f,.33f),Wood).transform;branch.localRotation=Quaternion.Euler(0,0,44);
        for(int i=0;i<4;i++)
        {
            var leaf=Prim("Faceted dark canopy",PrimitiveType.Sphere,tree,new Vector3((i%2-.5f)*1.5f,3.4f+(i/2)*.7f,.3f),new Vector3(2.4f,1.25f,1.9f),Mat("Hub19 canopy","#12583e")).transform;leaf.localRotation=Quaternion.Euler(i*19,i*38,0);
        }
        for(int i=0;i<11;i++)
        {
            float a=i*Mathf.PI*2/11;var rock=Box("Uneven campfire stone",g,new Vector3(Mathf.Sin(a)*.73f,.12f,Mathf.Cos(a)*.73f-.55f),new Vector3(.40f,.18f,.33f),Stone);rock.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,i%3*5);
        }
        for(int i=0;i<5;i++)
        {
            var log=Prim("Campfire radial wood",PrimitiveType.Cylinder,g,new Vector3(Mathf.Sin(i*1.25f)*.27f,.24f,-.55f+Mathf.Cos(i*1.25f)*.27f),new Vector3(.16f,.47f,.16f),Wood).transform;log.localRotation=Quaternion.Euler(58,i*72,0);
        }
        Run.fireFlames=new Transform[3];for(int i=0;i<3;i++)Run.fireFlames[i]=Crystal("Warm fire shape","fire_"+i,g,new Vector3((i-1)*.16f,.53f,-.55f),.16f,1,Mat("Fire warm "+i,i==1?"#ffd16c":"#f37d35",i==1?1.8f:1.2f));
        Run.fireLight=PointLight(g,new Vector3(0,1,-.5f),C("#ffb16d"),1.2f,7);
        foreach(float angle in new[]{-120f,-52f,58f,130f})
        {
            float a=angle*Mathf.Deg2Rad;var log=Prim("Whole log conversation seat",PrimitiveType.Cylinder,g,new Vector3(Mathf.Sin(a)*2.30f,.30f,Mathf.Cos(a)*2.30f),new Vector3(.52f,1.1f,.52f),Wood).transform;log.localRotation=Quaternion.Euler(90,angle+90,0);
            var cap=Prim("Visible cut endgrain",PrimitiveType.Cylinder,log,new Vector3(0,.99f,0),new Vector3(1.01f,.012f,1.01f),Mat("Fresh endgrain","#a16f40"));
        }
        for(int i=0;i<4;i++)
        {
            var portal=Portal(g,"hub19_portal_"+i,new Vector3(-3.5f+i*2.2f,0,5.4f),i<2?"Cross-Platform":"PC Worlds",i<2);portal.localRotation=Quaternion.Euler(0,(i-1.5f)*-4,0);
            Box("Black portal category fascia",portal,new Vector3(0,2.7f,-.1f),new Vector3(1.92f,.25f,.13f),DarkWood);
            Label("Portal category",i<2?"CROSS-PLATFORM":"PC WORLDS",portal,new Vector3(0,2.70f,-.18f),1.8f,.09f,"#69caff");
        }
        // Galaxy rings sit behind the timber room, never over the near action.
        var galaxy=Mat("Galaxy structure blue","#2671d9",.45f);
        for(int i=0;i<3;i++)
        {
            var ring=Torus("Overhead orbit sculpture","hub19_orbit_"+i,g,new Vector3(1.1f,4.9f,5.2f),2.6f,1.5f,.015f,galaxy,64);ring.localRotation=Quaternion.Euler(20+i*31,25+i*35,0);
        }
        HubTree(g,new Vector3(-4.7f,0,3.5f),41,true);HubTree(g,new Vector3(4.5f,0,3.5f),42,true);
        Label("Campfire world epoch","VRChat Hub  ·  2019",g,new Vector3(0,3.0f,5.0f),7,.19f,"#88cfff");
        PointLight(g,new Vector3(-4,3,-1),C("#897adf"),.85f,12);PointLight(g,new Vector3(4,3,1),C("#6685d9"),.85f,12);
        BuildHome2019(parent);
    }
    static void BuildHome2019(Transform parent)
    {
        var g=Child("2019 Home · official lounge vignette",parent).transform;Run.home2019=g;
        Box("Lounge pale tiled floor",g,new Vector3(0,-.12f,0),new Vector3(6.8f,.24f,5.8f),Stone);
        Box("Warm pale rear wall",g,new Vector3(0,1.8f,2.7f),new Vector3(6.8f,3.6f,.18f),Mat("Home plaster","#bbad91"));
        Box("Dark upper wall band",g,new Vector3(0,3.05f,2.58f),new Vector3(6.8f,.31f,.16f),DarkWood);
        for(int i=0;i<5;i++)Box("Exposed wooden ceiling slat",g,new Vector3(-2.5f+i*1.2f,3.45f,1.1f),new Vector3(.20f,.25f,3.7f),Wood);
        Polygon("Circular woven lounge rug","home19_rug",g,new Vector3(0,.015f,-.1f),Circle(1.75f,40),.018f,Teal);
        for(int i=0;i<3;i++)Arc("Concentric woven rug band","home19_rug_band_"+i,g,new Vector3(0,.035f,-.1f),1.2f+i*.16f,1.24f+i*.16f,0,360,.005f,i==1?Red:Brass,48);
        if(!AssetPart(g,"Home sofa",new[]{"Sofa","sofa"},new Vector3(.2f,0,.7f),3.45f,0))
        {
            Box("Wood lounge sofa base",g,new Vector3(.2f,.28f,.8f),new Vector3(3.7f,.40f,1.2f),Wood);
            Box("Wood lounge sofa back",g,new Vector3(.2f,.85f,1.3f),new Vector3(3.7f,1.1f,.28f),Wood);
            foreach(float x in new[]{-1.55f,1.95f})Box("Wood sofa arm",g,new Vector3(x,.60f,.8f),new Vector3(.28f,.65f,1.35f),Wood);
            for(int i=0;i<3;i++)Box("Red cushion",g,new Vector3(-.9f+i*1.1f,.58f,.65f),new Vector3(1.06f,.18f,.90f),Red);
        }
        foreach(float x in new[]{-1.1f,1.1f}){var pillow=Box("Loose white lounge pillow",g,new Vector3(x,.85f,1.06f),new Vector3(.70f,.55f,.20f),Paper);pillow.localRotation=Quaternion.Euler(-13,0,x*8);}
        if(!AssetPart(g,"Home coffee table",new[]{"LowTable_wood","LowTable_White","Coffee Table","CoffeeTable","lowtable"},new Vector3(.1f,0,-.8f),1.45f,0))
        {
            Box("Low timber coffee table",g,new Vector3(.1f,.37f,-.8f),new Vector3(1.45f,.32f,.72f),Wood);
            foreach(float x in new[]{-.5f,.5f})Box("Coffee table leg",g,new Vector3(x,.13f,-.8f),new Vector3(.18f,.26f,.48f),DarkWood);
        }
        var portal=Portal(g,"home19_to_hub",new Vector3(-2.25f,0,2.35f),"The Hub",true);portal.localScale=Vector3.one*.85f;
        Label("Home title","VRChat HOME  /  2019.05",g,new Vector3(0,3.18f,2.45f),6,.15f,"#7ed9f1");
        Run.mirror2019=Mirror(g,"2019",new Vector3(2.1f,1.45f,2.53f),new Vector2(1.55f,1.9f));
        var selector=Box("Avatar selector next to mirror",g,new Vector3(2.88f,.74f,1.85f),new Vector3(.45f,1.3f,.20f),Teal);
        Label("Mirror-side avatar selector","AVATARS\n◀  SELECT  ▶",g,new Vector3(2.88f,1.24f,1.72f),1.1f,.085f,"#8ae1ff");
        foreach(float x in new[]{-3f,3f})
        {
            Prim("Large terracotta planter",PrimitiveType.Cylinder,g,new Vector3(x,.32f,-.15f),new Vector3(.85f,.32f,.85f),Mat("Home planter","#967050"));HubTree(g,new Vector3(x,.55f,-.15f),60+(int)x,true);
        }
        PointLight(g,new Vector3(0,2.7f,-.5f),C("#ffe0b0"),.85f,8);
    }
    static Texture2D AvatarPreview(OWActorRig actor,int index)
    {
        string path=Root+"/Textures/AvatarPreview_"+index+".asset";
        var preview=new PreviewRenderUtility();Texture2D rendered=null;
        try
        {
            var clone=Object.Instantiate(actor.visualRoot.gameObject);clone.name="Preview actual Humanoid "+actor.appearanceId;clone.SetActive(true);
            clone.transform.position=new Vector3(0,actor.visualRoot.localPosition.y,0);clone.transform.rotation=Quaternion.Euler(0,180,0);clone.transform.localScale=actor.visualRoot.lossyScale;
            foreach(var prop in clone.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Carried shared work")).ToArray())Object.DestroyImmediate(prop.gameObject);
            preview.AddSingleGO(clone);
            var animator=clone.GetComponentInChildren<Animator>();if(animator!=null && animator.gameObject.activeInHierarchy){animator.Rebind();animator.SetFloat("V4PoseTime",.1f);animator.Play("V4Idle",0,.1f);animator.Update(0);}
            preview.camera.orthographic=true;preview.camera.orthographicSize=.72f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10f;
            preview.camera.transform.position=new Vector3(0,.64f,-3f);preview.camera.transform.LookAt(new Vector3(0,.64f,0));preview.camera.backgroundColor=C("#203c49");preview.camera.clearFlags=CameraClearFlags.Color;
            preview.lights[0].intensity=.75f;preview.lights[0].transform.rotation=Quaternion.Euler(35,30,0);preview.lights[1].intensity=.25f;preview.ambientColor=new Color(.25f,.25f,.25f,1);
            preview.BeginStaticPreview(new Rect(0,0,256,256));preview.Render(true);rendered=preview.EndStaticPreview();
            var image=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(image==null){image=new Texture2D(256,256,TextureFormat.RGBA32,false){name="Actual avatar preview "+actor.appearanceId};AssetDatabase.CreateAsset(image,path);}
            image.SetPixels(rendered.GetPixels());image.Apply(false,false);EditorUtility.SetDirty(image);return image;
        }
        finally{if(rendered!=null)Object.DestroyImmediate(rendered);preview.Cleanup();}
    }
    static void BuildMenuAt(Transform parent,int region)
    {
        var ui=Child("VRChat spatial UI · performance reconstruction",parent).transform;ui.localPosition=Journey.RegionOffset(region)+new Vector3(0,3.20f,6.4f);ui.localScale=Vector3.one*.70f;
        Run.epochLabel=Label("Visible world epoch","",ui,new Vector3(0,.68f,0),7,.14f,"#dceff0");
        var menus=Child("Compact Avatar and World selection",ui).transform;
        for(int i=0;i<2;i++)
        {
            var pane=Child(i==0?"Avatar menu":"World menu",menus).transform;pane.localPosition=new Vector3(i==0?-1.02f:1.02f,0,.15f);
            Box("Compact dark menu backing",pane,Vector3.zero,new Vector3(1.78f,1.0f,.045f),Teal);
            Box("Cyan tab top border",pane,new Vector3(0,.51f,-.015f),new Vector3(1.82f,.025f,.06f),Cyan);
            var label=Label(i==0?"Avatar selector status":"World selector status","",pane,new Vector3(0,.33f,-.04f),1.72f,.105f);
            if(i==0)Run.avatarSelection=label;else Run.worldSelection=label;
            var cards=Child("Selection cards",pane).transform;cards.localPosition=new Vector3(0,-.10f,-.055f);
            for(int j=0;j<2;j++)
            {
                float x=j==0?-.34f:.34f;
                Box("Selectable card "+j,cards,new Vector3(x,0,0),new Vector3(.56f,.59f,.025f),Mat("UI card "+j,j==0?"#38778b":"#625c89"));
                if(i==0)
                {
                    var mat=Mat("Actual Humanoid preview "+j,"#ffffff");mat.shader=Shader.Find("Unlit/Texture");mat.mainTexture=AvatarPreview(Run.actors[j],j);EditorUtility.SetDirty(mat);
                    Prim("Real avatar preview · "+Run.actors[j].appearanceId,PrimitiveType.Quad,cards,new Vector3(x,0,-.025f),new Vector3(.52f,.56f,1),mat);
                }
                else Label("World card",j==0?"2017 HUB":"2019 HUB",cards,new Vector3(x,0,-.03f),.56f,.075f);
            }
            var cursor=Box("Visible current selection",cards,new Vector3(-.34f,-.02f,-.045f),new Vector3(.62f,.65f,.02f),Mat("Selection highlight","#66d9df",.8f));cursor.GetComponent<Renderer>().enabled=false;
            foreach(float y in new[]{-.31f,.31f})Box("Selection border",cursor,new Vector3(0,y/.65f,0),new Vector3(1,.035f,.7f),Cyan);
            foreach(float x in new[]{-.31f,.31f})Box("Selection border",cursor,new Vector3(x/.62f,0,0),new Vector3(.035f,1,.7f),Cyan);
            if(i==0)Run.avatarCursor=cursor;else Run.worldCursor=cursor;
        }
        Run.presence=Label("Compact selection acknowledgement","",ui,new Vector3(0,-.67f,-.05f),5,.10f,"#ddcfac");
    }
    static void BuildMenus(Transform parent)
    {
        Run.eraUiRoots=new GameObject[2];Run.eraEpochLabels=new TMP_Text[2];Run.eraAvatarLabels=new TMP_Text[2];Run.eraWorldLabels=new TMP_Text[2];Run.eraPresenceLabels=new TMP_Text[2];Run.eraAvatarCursors=new Transform[2];Run.eraWorldCursors=new Transform[2];
        for(int i=0;i<2;i++)
        {
            BuildMenuAt(parent,i+1);var ui=Run.epochLabel.transform.parent;ui.name=i==0?"2017 Avatar and World menu":"2019 Avatar and World menu";
            Run.eraUiRoots[i]=ui.gameObject;Run.eraEpochLabels[i]=Run.epochLabel;Run.eraAvatarLabels[i]=Run.avatarSelection;Run.eraWorldLabels[i]=Run.worldSelection;Run.eraPresenceLabels[i]=Run.presence;Run.eraAvatarCursors[i]=Run.avatarCursor;Run.eraWorldCursors[i]=Run.worldCursor;
        }
    }
    static void BuildWorkshop(Transform parent)
    {
        var w=Child("Working library · translation distribution maintenance",parent).transform;Run.workshop=w;
        foreach(float x in new[]{-5.5f,5.5f})
        {
            var zone=Child(x<0?"Shared archive and translation":"Tools and maintenance",w).transform;zone.localPosition=new Vector3(x,0,3.4f);zone.localRotation=Quaternion.Euler(0,x<0?20:-20,0);
            Box("Actual library platform",zone,new Vector3(0,-.06f,0),new Vector3(2.6f,.12f,4.5f),Wood);
            bool shelf=AssetPart(zone,"Imported archive shelf",new[]{"Living_Shelf","bookshelf","BookShelf","Bookcase","Shelf"},new Vector3(0,0,1.5f),1.9f,0);
            if(!shelf)
            {
                foreach(float sx in new[]{-.94f,.94f})Box("Shelf upright",zone,new Vector3(sx,1.0f,1.5f),new Vector3(.10f,2,.46f),Wood);
                for(int level=0;level<5;level++)
                {
                    Box("Archive shelf board",zone,new Vector3(0,.12f+level*.42f,1.5f),new Vector3(1.98f,.07f,.5f),Wood);
                    for(int b=0;b<8;b++)
                    {
                        var book=Box("Numbered archived volume",zone,new Vector3(-.77f+b*.215f,.3f+level*.42f,1.5f),new Vector3(.13f,.29f+(b%3)*.035f,.31f),Mat("Archive spine "+b,new[]{"#447a89","#ab985f","#7c678a","#65866b"}[b%4]));book.localRotation=Quaternion.Euler(0,0,b==6?9:0);
                        Box("Spine number strip",book,new Vector3(0,.1f,-.52f),new Vector3(.65f,.10f,.02f),Paper);
                    }
                }
            }
            AssetPart(zone,"Imported archive floor lamp",new[]{"Lamp"},new Vector3(1.0f,0,.7f),.55f,0);
            Label("Working place title",x<0?"TRANSLATE  /  ARCHIVE":"TOOLS  /  MAINTAIN",zone,new Vector3(0,2.28f,1.25f),2.6f,.115f,"#b9dbe1");
            var cart=Child("Distribution cart",zone).transform;cart.localPosition=new Vector3(-.25f,0,-1.0f);
            foreach(float y in new[]{.25f,.68f})Box("Two-level cart tray",cart,new Vector3(0,y,0),new Vector3(1.05f,.065f,.62f),Brass);
            foreach(float sx in new[]{-.43f,.43f})foreach(float z in new[]{-.24f,.24f}){Box("Cart post",cart,new Vector3(sx,.45f,z),new Vector3(.045f,.75f,.045f),DarkWood);var wheel=Prim("Cart wheel",PrimitiveType.Cylinder,cart,new Vector3(sx,.09f,z),new Vector3(.15f,.045f,.15f),DarkWood);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
            for(int b=0;b<4;b++)Box("Delivered work pile",cart,new Vector3((b%2-.5f)*.42f,.75f+(b/2)*.08f,0),new Vector3(.36f,.07f,.36f),b%2==0?Paper:Teal);
            AssetPart(cart,"Imported reference books",new[]{"Books","Book"},new Vector3(-.28f,.73f,.02f),.32f,0);
            AssetPart(cart,"Imported working desk lamp",new[]{"Desk_Lamp"},new Vector3(.27f,.73f,.02f),.27f,0);
        }
    }
    static void BuildConditions(Transform parent)
    {
        // Open structural UI, not a room shell: no wall, ceiling or foreground panel.
        var g=Child("Conditions changed · open layered UI entrances",parent).transform;Run.conditionsRoot=g;
        Run.conditionFrames=new Transform[5];Run.conditionFrameOrigins=new Vector3[5];Run.conditionLeftJambs=new Transform[5];Run.conditionRightJambs=new Transform[5];Run.conditionLintels=new Transform[5];Run.conditionReadyMarks=new GameObject[5];
        var window=Mat("Old window ivory extrusion","#b6b6a8");var cobalt=Mat("Verification calm blue","#396a93");var access=Mat("Access route jade","#408f83");
        string[] titles={"01  同一套工具  /  USE","02  选择与兼容  /  CHOOSE","03  安全与验证  /  VERIFY","04  便利与可访问性  /  ADAPT","05  维护与保存  /  SAVE"};
        Material[] accents={Teal,Brass,cobalt,access,Teal};
        for(int i=0;i<5;i++)
        {
            var frame=Child("Thick window threshold "+i,g).transform;Vector3 origin=new Vector3(0,0,6.4f+i*3.15f);frame.localPosition=origin;Run.conditionFrames[i]=frame;Run.conditionFrameOrigins[i]=origin;
            float width=7.8f-i*.72f;
            Run.conditionLeftJambs[i]=Box("Left entrance jamb",frame,new Vector3(-width*.5f,1.55f,0),new Vector3(.15f,3.10f,.32f),window);
            Run.conditionRightJambs[i]=Box("Right entrance jamb",frame,new Vector3(width*.5f,1.55f,0),new Vector3(.15f,3.10f,.32f),window);
            Run.conditionLintels[i]=Box("Window titlebar with real depth",frame,new Vector3(0,3.12f,0),new Vector3(width+.24f,.18f,.32f),accents[i]);
            foreach(var jamb in new[]{Run.conditionLeftJambs[i],Run.conditionRightJambs[i]})
            {
                Box("Recessed inner edge",jamb,new Vector3(0,0,-.57f),new Vector3(.29f,.96f,.08f),accents[i]);
                Box("Free-standing UI foot",jamb,new Vector3(0,-.51f,.1f),new Vector3(3.5f,.04f,2.7f),DarkWood);
            }
            Label("Gate meaning",titles[i],frame,new Vector3(0,3.40f,-.19f),7,.13f,i==2?"#a4d0f3":i==3?"#9adfd2":"#e1e4d3");
            // Multiple raised titlebar controls connect the old flat window to a thick doorway.
            for(int b=0;b<3;b++)Box("Raised window chrome control",frame,new Vector3(.30f+b*.23f,3.12f,-.21f),new Vector3(.14f,.09f,.09f),Paper);
            var ready=Child("Verified route remains open "+i,frame);ready.transform.localPosition=new Vector3(-1.65f,3.1f,-.23f);
            Prim("Green ready signal",PrimitiveType.Sphere,ready.transform,Vector3.zero,Vector3.one*.11f,Mat("Condition ready green","#80c78d",.55f));Label("Check result","✓",ready.transform,new Vector3(.19f,0,-.01f),.3f,.085f,"#b9e2b9");Run.conditionReadyMarks[i]=ready;
        }
        // Sparse rails and choice arrows describe two usable paths around validation.
        for(int side=-1;side<=1;side+=2)
        {
            for(int dash=0;dash<9;dash++)Box("Open path ground guide",g,new Vector3(side*(3.6f+dash*.035f),.025f,6.7f+dash*1.3f),new Vector3(.07f,.035f,.52f),side<0?Brass:access);
            var rail=Box("Short assistance handrail",g,new Vector3(side*4.8f,.72f,13.1f),new Vector3(.065f,.065f,2.2f),access);
            foreach(float z in new[]{12.2f,14f})Box("Handrail upright",g,new Vector3(side*4.8f,.36f,z),new Vector3(.045f,.72f,.045f),access);
        }
        Run.conditionChoiceArrows=new Transform[2];
        for(int i=0;i<2;i++)
        {
            var arrow=Polygon("Alternate open route arrow","condition_path_arrow_"+i,g,Vector3.zero,new[]{new Vector2(-.12f,-.4f),new Vector2(.12f,-.4f),new Vector2(.12f,.05f),new Vector2(.30f,.05f),new Vector2(0,.42f),new Vector2(-.30f,.05f),new Vector2(-.12f,.05f)},.035f,i==0?Brass:access);arrow.localRotation=Quaternion.Euler(0,i==0?-15:15,0);Run.conditionChoiceArrows[i]=arrow;
        }
        var review=Child("Compatibility and assistance review point",g).transform;review.localPosition=new Vector3(-4.7f,0,5.0f);review.localRotation=Quaternion.Euler(0,16,0);
        Box("Review standing rail",review,new Vector3(0,.52f,0),new Vector3(.07f,1.04f,.07f),DarkWood);
        var reading=Box("Review sloping display",review,new Vector3(0,1.04f,0),new Vector3(1.1f,.07f,.70f),Teal);reading.localRotation=Quaternion.Euler(-14,0,0);
        Run.conditionStatus=Label("Current change and competing needs","",review,new Vector3(0,1.9f,.0f),3.4f,.105f,"#ccddd7");
        Label("Needs held together","安全   ·   便利   ·   可访问性",review,new Vector3(0,.90f,-.40f),3.6f,.080f,"#afcdcc");
        BuildMaintenance(parent);
    }
    static void BuildMaintenance(Transform parent)
    {
        var g=Child("Still maintaining · lamp and saved archive",parent).transform;Run.maintenanceRoot=g;g.localPosition=new Vector3(4.65f,0,4.9f);g.localRotation=Quaternion.Euler(0,-16,0);
        var workingActor=Run.alternateActors!=null && Run.alternateActors.Length>1 && Run.alternateActors[1]!=null?Run.alternateActors[1]:Run.actors[1];
        float height=Mathf.Clamp(workingActor.modelHeight*.62f,.40f,.90f);Run.maintenanceWorkHeight=height;
        if(!AssetPart(g,"Imported maintenance table",new[]{"Wooden_Table","Wood_Table"},Vector3.zero,1.9f,0))
        {
            Box("Maintenance work top",g,new Vector3(0,height-.04f,0),new Vector3(1.9f,.08f,.8f),Wood);
            foreach(float x in new[]{-.75f,.75f})foreach(float z in new[]{-.28f,.28f})Box("Maintenance desk leg",g,new Vector3(x,height*.48f,z),new Vector3(.07f,height*.96f,.07f),DarkWood);
        }
        else
        {
            var table=g.Find("Imported maintenance table");var renderer=table.GetComponentInChildren<Renderer>();float current=renderer.bounds.max.y-g.position.y;
            if(current>.05f){var scale=table.localScale;scale.y*=height/current;table.localScale=scale;}
        }
        AssetPart(g,"Imported maintained archive shelf",new[]{"Living_Shelf","Shelf"},new Vector3(.20f,0,1.15f),1.75f,0);
        AssetPart(g,"Imported maintenance reference books",new[]{"Books","Book"},new Vector3(-.57f,height+.03f,.10f),.37f,0);
        AssetPart(g,"Imported maintenance task lamp",new[]{"Desk_Lamp"},new Vector3(.65f,height+.03f,.08f),.35f,0);
        Box("Small maintained computer chassis",g,new Vector3(.05f,height+.22f,.12f),new Vector3(.64f,.40f,.26f),Mat("Old window ivory extrusion","#b6b6a8"));
        Box("Computer active blue face",g,new Vector3(.05f,height+.22f,-.025f),new Vector3(.53f,.30f,.035f),Teal);
        Label("Useful tool remains running","CHECK\nSAVE",g,new Vector3(.05f,height+.22f,-.055f),.55f,.06f,"#a6dcd5");
        Box("Accessible compact keyboard",g,new Vector3(.02f,height+.03f,-.25f),new Vector3(.55f,.035f,.20f),Paper);
        Run.archiveDrawer=Box("Archive drawer being used",g,new Vector3(-.48f,height*.56f,-.14f),new Vector3(.72f,.20f,.56f),Wood);Box("Archive drawer handle",Run.archiveDrawer,new Vector3(0,0,-.53f),new Vector3(.25f,.11f,.07f),Brass);
        var document=Child("Work checked then saved",g).transform;Run.savedDocument=document;
        if(!AssetPart(document,"Imported volume to save",new[]{"Book"},Vector3.zero,.18f,0))Box("Saved bound pages",document,Vector3.zero,new Vector3(.18f,.025f,.24f),Paper);
        var archived=Child("Saved copy left in archive",g);archived.transform.localPosition=new Vector3(-.65f,1.0f,.85f);Run.archivedCopy=archived;
        if(!AssetPart(archived.transform,"Imported archived copy",new[]{"Book"},Vector3.zero,.18f,0))Box("Archived bound pages",archived.transform,Vector3.zero,new Vector3(.18f,.25f,.03f),Paper);
        Label("Archive acknowledgement","SAVED  ✓",archived.transform,new Vector3(0,.32f,-.03f),1.1f,.075f,"#e5c784");
        Run.maintenanceStatus=Label("Maintenance outcome","",g,new Vector3(0,2.20f,.85f),4.4f,.11f,"#dfcf9e");
        Run.maintenanceLight=PointLight(g,new Vector3(.65f,height+.62f,-.08f),C("#ffd69a"),.75f,4.7f);
    }
    static Material Glass(string name,string color,float alpha)
    {
        var m=Mat(name,color);var c=C(color);c.a=alpha;m.color=c;m.SetFloat("_Mode",3);m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.DisableKeyword("_ALPHATEST_ON");m.EnableKeyword("_ALPHABLEND_ON");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;EditorUtility.SetDirty(m);return m;
    }
    static Transform Fence(Transform parent,string id,float width,float height,Material material)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        Action<Vector3,Vector3> box=(center,size)=>
        {
            int n=vertices.Count;foreach(int z in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int x in new[]{-1,1})vertices.Add(center+Vector3.Scale(size,new Vector3(x,y,z))*.5f);
            triangles.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3,n+4,n+5,n+6,n+5,n+7,n+6,n,n+4,n+2,n+2,n+4,n+6,n+1,n+3,n+5,n+3,n+7,n+5,n,n+1,n+4,n+1,n+5,n+4,n+2,n+6,n+3,n+3,n+6,n+7});
        };
        foreach(float y in new[]{.16f,height*.53f,height-.08f})box(new Vector3(width*.5f,y,0),new Vector3(width,.075f,.10f));
        int bars=Mathf.Max(3,Mathf.CeilToInt(width/.28f));for(int i=0;i<=bars;i++)box(new Vector3(i*width/bars,height*.5f,0),new Vector3(.055f,height,.075f));
        return MeshObject("Solid metal barrier",id,parent,vertices,triangles,null,material);
    }
    static GameObject Signal(Transform parent,string name,Vector3 position,bool unlocked)
    {
        var root=Child(name,parent);root.transform.localPosition=position;var material=Mat(unlocked?"Unlocked green":"Blocked red",unlocked?"#85c99a":"#d66d61",.2f);
        if(unlocked)
        {
            var a=Box("Check short stroke",root.transform,new Vector3(-.11f,-.03f,0),new Vector3(.09f,.29f,.045f),material);a.localRotation=Quaternion.Euler(0,0,42);
            var b=Box("Check long stroke",root.transform,new Vector3(.10f,.035f,0),new Vector3(.09f,.52f,.045f),material);b.localRotation=Quaternion.Euler(0,0,-38);
        }
        else foreach(float angle in new[]{-45f,45f}){var a=Box("Closed crossing",root.transform,Vector3.zero,new Vector3(.085f,.58f,.045f),material);a.localRotation=Quaternion.Euler(0,0,angle);}
        return root;
    }
    static void BuildBarriers()
    {
        var parent=Run.conditionsRoot;Run.barrierLeftLeaves=new Transform[3];Run.barrierRightLeaves=new Transform[3];Run.barrierLockedSigns=new GameObject[3];Run.barrierOpenSigns=new GameObject[3];Run.verificationArms=new Transform[3];Run.barrierMessages=new TMP_Text[3];Run.sampledGateAngles=new float[3];Run.sampledGateLocked=new bool[3];
        Vector3[] centers={new Vector3(0,0,6.0f),new Vector3(0,0,10.2f),new Vector3(4.8f,0,6.0f)};
        string[] titles={"选择与兼容","安全验证","辅助与维护"};
        var steel=Mat("Barrier dark steel","#56656e");var amber=Mat("Choice gate brass","#b39b62");var blue=Mat("Security gate blue","#3d6d8c");
        for(int i=0;i<3;i++)
        {
            var gate=Child(titles[i],parent).transform;gate.localPosition=centers[i];float width=i==2?1.6f:7.8f,height=i==1?1.85f:i==2?1.1f:1.4f;
            Material trim=i==0?amber:i==1?blue:Teal;
            foreach(float side in new[]{-1f,1f})
            {
                Box("Heavy gate post",gate,new Vector3(side*width*.5f,1.12f,.0f),new Vector3(.18f,2.24f,.24f),steel);
                Box("Post foot",gate,new Vector3(side*width*.5f,.06f,0),new Vector3(.36f,.12f,.48f),DarkWood);
            }
            Box("Gate header",gate,new Vector3(0,2.23f,.02f),new Vector3(width+.25f,.21f,.24f),trim);
            Label("Public gate title",titles[i],gate,new Vector3(0,2.52f,-.15f),Mathf.Max(2.4f,width),.14f,"#d2dddd");
            for(int side=0;side<2;side++)
            {
                var hinge=Child(side==0?"Left hinged gate":"Right hinged gate",gate).transform;hinge.localPosition=new Vector3((side==0?-1:1)*width*.5f,0,0);
                var leaf=Child("Framed moving gate leaf",hinge).transform;if(side==1)leaf.localRotation=Quaternion.Euler(0,180,0);
                Fence(leaf,"barrier_"+i+"_"+side,width*.5f,height,steel);
                Box("Framed tinted partition",leaf,new Vector3(width*.25f,height*.52f,.065f),new Vector3(width*.5f-.12f,height-.20f,.025f),Glass("Gate glass "+i,i==1?"#6394b0":"#9a9b84",i==1?.23f:.15f));
                if(side==0)Run.barrierLeftLeaves[i]=hinge;else Run.barrierRightLeaves[i]=hinge;
            }
            Run.barrierLockedSigns[i]=Signal(gate,"Closed entrance",new Vector3(0,1.12f,-.25f),false);Run.barrierOpenSigns[i]=Signal(gate,"Open entrance",new Vector3(0,1.12f,-.25f),true);
            var arm=Child("Lifting verification bar",gate).transform;arm.localPosition=new Vector3(-Mathf.Min(1.35f,width*.35f),.78f,-.27f);Box("Striped gate arm",arm,new Vector3(Mathf.Min(1.35f,width*.35f),0,0),new Vector3(Mathf.Min(2.7f,width*.7f),.08f,.08f),trim);Run.verificationArms[i]=arm;
            Run.barrierMessages[i]=Label("Public gate result","",gate,new Vector3(0,1.96f,-.2f),Mathf.Max(2.3f,width),.10f,"#e5d6bc");
        }
        foreach(float x in new[]{-5.2f,5.8f})Fence(parent,"side_boundary_"+(x<0?0:1),1.3f,1.05f,steel).localPosition=new Vector3(x,0,6.0f);
        var choice=Child("Two compatibility choices",parent).transform;choice.localPosition=new Vector3(0,1.47f,5.65f);
        foreach(int side in new[]{-1,1})
        {
            Box("Choice plaque",choice,new Vector3(side*1.45f,0,0),new Vector3(1.45f,.32f,.07f),Teal);
            Label("Available choice",side<0?"保留现有方法":"切换兼容方法",choice,new Vector3(side*1.45f,0,-.05f),1.4f,.09f);
        }
        for(int step=0;step<6;step++)Box("Usable side route",parent,new Vector3(4.8f,.03f,4.6f+step*.62f),new Vector3(.70f,.045f,.23f),Mat("Accessible route green","#66a98c",.12f));
    }
    static Material PlateMaterial(string name,string color,float alpha)
    {
        string path=Root+"/Textures/"+name+".asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null){texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name=name};var c=C(color);c.a=alpha;texture.SetPixels(new[]{c,c,c,c});texture.Apply();AssetDatabase.CreateAsset(texture,path);}
        var mat=Mat(name,"#ffffff");mat.shader=Shader.Find("Unlit/Transparent");mat.mainTexture=texture;EditorUtility.SetDirty(mat);return mat;
    }
    static Transform Nameplate(Transform parent,string name,string value,out TMP_Text text,out Renderer background,out Renderer border)
    {
        var root=Child(name,parent).transform;root.localScale=Vector3.one*.88f;
        var outline=new[]{new Vector2(-.60f,-.11f),new Vector2(.60f,-.11f),new Vector2(.66f,-.055f),new Vector2(.66f,.055f),new Vector2(.60f,.11f),new Vector2(.35f,.11f),new Vector2(.30f,.075f),new Vector2(-.18f,.075f),new Vector2(-.23f,.11f),new Vector2(-.60f,.11f),new Vector2(-.66f,.055f),new Vector2(-.66f,-.055f)};
        var edge=Polygon("Classic stepped nameplate outline","nameplate_outline",root,Vector3.zero,outline,.008f,Run.nameplates.eraBorders[0]);edge.localRotation=Quaternion.Euler(-90,0,0);border=edge.GetComponent<Renderer>();
        var fill=Polygon("Dark nameplate fill","nameplate_fill",root,new Vector3(0,0,-.012f),outline,.008f,Run.nameplates.eraBackgrounds[0]);fill.localRotation=Quaternion.Euler(-90,0,0);fill.localScale=new Vector3(.966f,1,.80f);background=fill.GetComponent<Renderer>();
        background.shadowCastingMode=ShadowCastingMode.Off;border.shadowCastingMode=ShadowCastingMode.Off;background.receiveShadows=false;border.receiveShadows=false;
        text=Label("Display name",value,root,new Vector3(0,-.005f,-.035f),1.18f,.10f);text.richText=false;text.enableAutoSizing=true;text.fontSizeMin=.65f;text.fontSizeMax=1f;text.overflowMode=TextOverflowModes.Truncate;
        string mp=Root+"/Materials/HistoricalNameText.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(mp);if(material==null){material=new Material(Font.material);material.name="HistoricalNameText";material.SetFloat("_OutlineWidth",.14f);material.SetColor("_OutlineColor",Color.black);AssetDatabase.CreateAsset(material,mp);}text.fontSharedMaterial=material;
        root.gameObject.SetActive(false);return root;
    }
    static void BuildNameplates(Transform owner)
    {
        // Real player heads stay in physical space while the historical world travels.
        var root=new GameObject("OW V4 Historical Nameplates");Run.nameplates=root.AddUdonSharpComponent<OWV4Nameplates>();var names=Run.nameplates;names.cast=Run;names.observer=Camera.main==null?null:Camera.main.transform;
        names.eraBackgrounds=new[]{PlateMaterial("Nameplate 2017 dark","#172127",.80f),PlateMaterial("Nameplate 2019 dark","#16242e",.83f)};
        names.eraBorders=new[]{PlateMaterial("Nameplate 2017 neutral","#a3b5a6",.95f),PlateMaterial("Nameplate 2019 neutral","#99b9c5",.95f)};
        names.anonymousLabel="xxxx";names.actorNicknames=new[]{"xxxx","xxxx","xxxx","xxxx"};names.actorPlates=new Transform[4];names.actorLabels=new TMP_Text[4];names.playerPlates=new Transform[80];names.playerLabels=new TMP_Text[80];names.playerIds=Enumerable.Repeat(-1,80).ToArray();names.previewWorldHeads=new Vector3[80];
        var backgrounds=new List<Renderer>();var borders=new List<Renderer>();
        for(int i=0;i<4;i++){TMP_Text label;Renderer back,border;names.actorPlates[i]=Nameplate(root.transform,"Actor nickname "+i,names.actorNicknames[i],out label,out back,out border);names.actorLabels[i]=label;backgrounds.Add(back);borders.Add(border);}
        for(int i=0;i<80;i++){TMP_Text label;Renderer back,border;names.playerPlates[i]=Nameplate(root.transform,"Player nameplate slot "+i,"",out label,out back,out border);names.playerLabels[i]=label;backgrounds.Add(back);borders.Add(border);}
        names.plateBackgrounds=backgrounds.ToArray();names.plateBorders=borders.ToArray();names.Sample(0);
    }
    static Bounds LocalMeshBounds(Transform root)
    {
        bool first=true;var bounds=new Bounds();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null)continue;var b=filter.sharedMesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var point=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
    static bool AssetPart(Transform parent,string label,string[] hints,Vector3 position,float width,float yaw)
    {
        // Read the existing imported model only. Do not import packages or copy third-party source.
        var paths=AssetDatabase.FindAssets("t:Model").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.IndexOf("Room01",StringComparison.OrdinalIgnoreCase)>=0 || p.IndexOf("Modern",StringComparison.OrdinalIgnoreCase)>=0 || p.IndexOf("Furniture",StringComparison.OrdinalIgnoreCase)>=0).OrderBy(p=>p.Contains("Room01")?0:1).ToArray();
        foreach(var path in paths)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(model==null)continue;
            foreach(string hint in hints)
            {
                var candidates=model.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponent<MeshFilter>()!=null).ToArray();
                var source=candidates.FirstOrDefault(r=>r.name.Equals(hint,StringComparison.OrdinalIgnoreCase))??candidates.FirstOrDefault(r=>r.name.IndexOf(hint,StringComparison.OrdinalIgnoreCase)>=0);
                if(source==null)continue;
                var root=Child(label,parent).transform;root.localPosition=position;root.localRotation=Quaternion.Euler(0,yaw,0);
                var part=Object.Instantiate(source.gameObject,root);part.name=source.name;part.transform.localPosition=Vector3.zero;part.transform.localRotation=source.transform.rotation;part.transform.localScale=source.transform.lossyScale;
                // Room01 FBX also contains embedded material stubs. Prefer the matching
                // imported .mat so its original shader and texture links remain intact.
                var external=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Room01"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null).ToArray();
                foreach(var renderer in part.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m==null?null:external.FirstOrDefault(e=>e.name==m.name || e.name==m.name.Replace(" (Instance)",""))??m).ToArray();
                foreach(var collider in part.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                bool shelf=hints.Any(h=>h.IndexOf("Shelf",StringComparison.OrdinalIgnoreCase)>=0 || h.IndexOf("Bookcase",StringComparison.OrdinalIgnoreCase)>=0);
                bool book=!shelf && hints.Any(h=>h.IndexOf("Book",StringComparison.OrdinalIgnoreCase)>=0);
                bool deskLamp=hints.Any(h=>h.IndexOf("Desk_Lamp",StringComparison.OrdinalIgnoreCase)>=0);
                bool lamp=!deskLamp && hints.Any(h=>h.IndexOf("Lamp",StringComparison.OrdinalIgnoreCase)>=0);
                bool sofa=hints.Any(h=>h.IndexOf("Sofa",StringComparison.OrdinalIgnoreCase)>=0);
                bool table=hints.Any(h=>h.IndexOf("Table",StringComparison.OrdinalIgnoreCase)>=0);
                var bounds=LocalMeshBounds(root);bool turned=false;
                if(shelf && bounds.size.z>bounds.size.x*1.3f){part.transform.localRotation=Quaternion.Euler(0,90,0)*part.transform.localRotation;bounds=LocalMeshBounds(root);turned=true;}
                float maxHeight=shelf ? 2.0f : book ? .40f : deskLamp ? .50f : lamp ? 1.60f : sofa ? 1.05f : table ? .85f : 2.0f;
                float maxDepth=shelf ? .65f : book ? .40f : deskLamp ? .45f : lamp ? .65f : sofa ? 1.40f : table ? .90f : 1.1f;
                float scale=Mathf.Min(width/Mathf.Max(.01f,bounds.size.x),maxHeight/Mathf.Max(.01f,bounds.size.y),maxDepth/Mathf.Max(.01f,bounds.size.z));
                part.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);root.localScale=Vector3.one*scale;
                Vector3 fitted=bounds.size*scale;
                UsedAssets.Add(new{label,path,node=source.name,sourceMesh=AssetDatabase.GetAssetPath(source.GetComponent<MeshFilter>().sharedMesh),turned90=turned,fittedSize=new[]{fitted.x,fitted.y,fitted.z},caps=new[]{width,maxHeight,maxDepth},materials=part.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Select(m=>new{name=m.name,path=AssetDatabase.GetAssetPath(m),shader=m.shader==null?"null":m.shader.name,texture=AssetDatabase.GetAssetPath(m.mainTexture)}).ToArray()});return true;
            }
        }
        return false;
    }
    static AnimatorController Motions(OWActorRig reference)
    {
        string path=Root+"/Motion/HubCastMotions.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach(var pair in new[]{new{n="Pose",t=AnimatorControllerParameterType.Int},new{n="PoseTime",t=AnimatorControllerParameterType.Float},new{n="V4Pose",t=AnimatorControllerParameterType.Int},new{n="V4PoseTime",t=AnimatorControllerParameterType.Float}})if(!controller.parameters.Any(p=>p.name==pair.n))controller.AddParameter(pair.n,pair.t);
        // A scene actor may currently contain the sampled head aim or a walking pose.
        // Read the immutable imported bind pose, so rebuilding cannot feed that sampled
        // pose back into every clip and slowly change head/leg rest or measured height.
        var pose=new HumanPose();var basis=PrefabUtility.LoadPrefabContents("Assets/OldWebActors/Imported/Kipfel/Kipfel.prefab");
        try{var animator=basis.GetComponent<Animator>();using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))handler.GetHumanPose(ref pose);}
        finally{PrefabUtility.UnloadPrefabContents(basis);}
        var sm=controller.layers[0].stateMachine;
        string[] names={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
        for(int id=0;id<names.Length;id++)
        {
            string cp=Root+"/Motion/"+names[id]+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(cp);if(clip==null){clip=new AnimationClip{name=names[id],frameRate=30};AssetDatabase.CreateAsset(clip,cp);}
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))AnimationUtility.SetEditorCurve(clip,binding,null);
            for(int m=0;m<HumanTrait.MuscleName.Length;m++)
            {
                string muscle=HumanTrait.MuscleName[m];float rest=pose.muscles[m],peak=rest;
                if(muscle.Contains("Arm Down-Up"))rest=-.70f;
                // This is the corrected V2 separated-hands axis; never use its old -.6 front-back.
                if(muscle.Contains("Arm Front-Back"))rest=0f;
                if(muscle.Contains("Forearm Stretch"))rest=.82f;
                if(muscle.Contains("Arm Twist In-Out"))rest=0f;
                peak=rest;
                if(id==1){if(muscle.Contains("Arm Down-Up")){rest=-.52f;peak=-.48f;}if(muscle.Contains("Forearm Stretch")){rest=.80f;peak=.72f;}}
                if(id==2 || id==11){if(muscle.Contains("Arm Down-Up")){rest=-.52f;peak=-.40f;}if(muscle.Contains("Forearm Stretch")){rest=.62f;peak=.48f;}}
                if(id==3 || id==4 || id==7 || id==9 || id==10)
                {
                    if(muscle=="Right Arm Down-Up")peak=id==7?.52f:id==4?.05f:id==10?-.18f:-.30f;
                    if(muscle=="Right Forearm Stretch")peak=id==4?.92f:id==7?.35f:.5f;
                    if(id==9 && muscle=="Left Arm Down-Up")peak=-.45f;
                }
                if(id==5){if(muscle=="Right Arm Down-Up")peak=-.39f;if(muscle=="Right Forearm Stretch")peak=.3f;}
                if(id==8){if(muscle.Contains("Arm Down-Up")){rest=.10f;peak=.16f;}if(muscle.Contains("Forearm Stretch")){rest=.96f;peak=.96f;}}
                if(id==11 && muscle=="Right Arm Down-Up")peak=.22f;
                if(muscle=="Head Nod Down-Up")peak=id==1 || id==5 || id==10?-.13f:.035f;
                var curve=new AnimationCurve(new Keyframe(0,rest),new Keyframe(.65f,peak),new Keyframe(1.35f,peak),new Keyframe(2,rest));
                if(id==6 && (muscle.Contains("Upper Leg Front-Back") || muscle.Contains("Arm Front-Back")))
                {
                    float amp=muscle.Contains("Upper Leg")?.22f:.09f,sign=muscle.StartsWith("Left")?1:-1;curve=new AnimationCurve(new Keyframe(0,rest),new Keyframe(.5f,rest+amp*sign),new Keyframe(1,rest),new Keyframe(1.5f,rest-amp*sign),new Keyframe(2,rest));
                }
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),muscle),curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==names[id])??sm.AddState(names[id]);state.motion=clip;state.timeParameterActive=true;state.timeParameter="V4PoseTime";
            if(id==0)sm.defaultState=state;
            if(!sm.anyStateTransitions.Any(t=>t.destinationState==state)){var tr=sm.AddAnyStateTransition(state);tr.hasExitTime=false;tr.duration=.12f;tr.canTransitionToSelf=false;tr.AddCondition(AnimatorConditionMode.Equals,id,"V4Pose");}
            EditorUtility.SetDirty(clip);EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);return controller;
    }
    static void ActivateActor(OWActorRig actor)
    {
        actor.gameObject.SetActive(true);if(actor.visualRoot!=null)actor.visualRoot.gameObject.SetActive(true);
        if(actor.animator==null)return;
        for(var t=actor.animator.transform;t!=null && t.IsChildOf(actor.transform);t=t.parent)t.gameObject.SetActive(true);
        actor.animator.enabled=true;
    }
    static Bounds ActorBounds(OWActorRig actor)
    {
        // Use the renderer's world-space envelope, the same once-scaled metric as
        // InspectCastGeometry. BakeMesh also folds in this rig's parent scaling.
        var renderers=actor.visualRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && r.gameObject.activeInHierarchy && (r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>()!=null)).ToArray();
        if(renderers.Length==0)throw new InvalidOperationException("No visible body geometry: "+actor.actorSlot);
        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
    }
    static void NormalizeActor(OWActorRig actor)
    {
        ActivateActor(actor);actor.transform.localScale=Vector3.one;
        // Always restore an immutable Cast-prefab basis. Rebuilding a saved V4 never
        // multiplies last build's visual scale or accumulated floor correction again.
        string baselinePath=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(baselinePath);var baseline=source==null?null:source.GetComponent<OWActorRig>();
        if(baseline==null || baseline.visualRoot==null)throw new InvalidOperationException("Cast prefab baseline required for stable normalization: "+actor.actorSlot);
        actor.visualRoot.localScale=baseline.visualRoot.localScale;actor.visualRoot.localPosition=baseline.visualRoot.localPosition;
        actor.animator.Rebind();actor.animator.SetFloat("V4PoseTime",0);actor.animator.Play("V4Idle",0,0);actor.animator.Update(0);
        float before=ActorBounds(actor).size.y;float factor=1.25f/Mathf.Max(.01f,before);
        actor.visualRoot.localScale=baseline.visualRoot.localScale*factor;actor.visualRoot.localPosition=baseline.visualRoot.localPosition*factor;actor.animator.Update(0);
        var bounds=ActorBounds(actor);actor.visualRoot.position+=Vector3.up*(actor.transform.position.y-bounds.min.y);actor.animator.Update(0);
        var measured=ActorBounds(actor);actor.modelHeight=measured.size.y;
        float sole=measured.min.y-actor.transform.position.y;
        if(Mathf.Abs(measured.size.y-1.25f)>.015f || Mathf.Abs(sole)>.006f)throw new InvalidOperationException("Cast normalization did not converge: "+actor.actorSlot+" height="+measured.size.y+" sole="+sole);
        CastScaleReports.Add(new{actor.actorSlot,actor.appearanceId,baselinePath,baselineScale=baseline.visualRoot.localScale.y,metric="visible Renderer.bounds in V4Idle, world space",sourceHeight=before,targetHeight=1.25f,measuredHeight=measured.size.y,soleMinRelativeToRoot=sole,leftFoot=actor.leftFoot.position.y-actor.transform.position.y,rightFoot=actor.rightFoot.position.y-actor.transform.position.y,visualScale=new[]{actor.visualRoot.localScale.x,actor.visualRoot.localScale.y,actor.visualRoot.localScale.z}});
    }
    static GameObject BuildWorkTray(OWActorRig actor,Material screenMaterial)
    {
        actor.animator.SetFloat("V4PoseTime",.5f);actor.animator.Play("V4Type",0,.5f);actor.animator.Update(0);
        Vector3 hands=actor.transform.InverseTransformPoint((actor.leftHand.position+actor.rightHand.position)*.5f);
        var tray=Child("Readable low contact tray",actor.transform);tray.transform.localPosition=new Vector3(0,hands.y-.04f,hands.z+.015f);
        Box("Thin working surface",tray.transform,Vector3.zero,new Vector3(.68f,.035f,.39f),DarkWood);
        Box("Slim supporting stem",tray.transform,new Vector3(0,-tray.transform.localPosition.y*.5f,.05f),new Vector3(.032f,tray.transform.localPosition.y,.032f),Brass);
        Box("Small floor foot",tray.transform,new Vector3(0,-tray.transform.localPosition.y+.015f,.05f),new Vector3(.34f,.03f,.28f),DarkWood);
        for(int row=0;row<3;row++)for(int col=0;col<8;col++)Box("Contact keyboard key",tray.transform,new Vector3((col-3.5f)*.047f,.025f,-.11f+row*.042f),new Vector3(.039f,.016f,.033f),Paper);
        var display=Prim("Low tilted tool display",PrimitiveType.Quad,tray.transform,new Vector3(0,.075f,.11f),new Vector3(.48f,.27f,1),screenMaterial==null?Teal:screenMaterial);display.transform.localRotation=Quaternion.Euler(65,0,0);
        actor.animator.Play("V4Idle",0,0);actor.animator.Update(0);tray.SetActive(false);return tray;
    }
    static void OwnCastMaterials(OWActorRig actor)
    {
        foreach(var renderer in actor.visualRoot.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
            {
                if(source==null)return null;string original=AssetDatabase.GetAssetPath(source);
                if(original.StartsWith(Root+"/Materials/Actor_",StringComparison.Ordinal))return source;
                string id=AssetDatabase.AssetPathToGUID(original);if(string.IsNullOrEmpty(id))throw new InvalidOperationException("Actor material must be persisted: "+source.name);
                string name="Actor_"+id,path=Root+"/Materials/"+name+".mat";var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(copy==null){copy=new Material(source);AssetDatabase.CreateAsset(copy,path);}else EditorUtility.CopySerialized(source,copy);
                copy.name=name;EditorUtility.SetDirty(copy);return copy;
            }).ToArray();
    }
    static void BuildCast(OWActorRig[] preservedActors,Transform parent)
    {
        Run.actors=preservedActors;int count=Run.actors.Length;
        if(count!=4)throw new InvalidOperationException("V4 authored for four preserved cast slots");
        Run.externalMotion=new bool[count];Run.carriedWorks=new GameObject[count];Run.alternateCarriedWorks=new GameObject[count];Run.headRestRotations=new Quaternion[count];Run.alternateHeadRestRotations=new Quaternion[count];Run.alternateActors=new OWActorRig[count];Run.workTrays=new GameObject[count];
        foreach(var actor in Run.actors)ActivateActor(actor);
        var bodyRenderers=new List<Renderer>();var bodySlots=new List<int>();var bodyAlternates=new List<bool>();var bodyEnabled=new List<bool>();
        Action<OWActorRig,int,bool> rememberBody=(actor,slot,alternate)=>{foreach(var renderer in actor.visualRoot.GetComponentsInChildren<Renderer>(true)){bodyRenderers.Add(renderer);bodySlots.Add(slot);bodyAlternates.Add(alternate);bodyEnabled.Add(renderer.enabled);}};
        var controller=Motions(Run.actors[0]);
        for(int i=0;i<count;i++)
        {
            var a=Run.actors[i];OwnCastMaterials(a);a.animator.runtimeAnimatorController=controller;a.animator.applyRootMotion=false;a.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;NormalizeActor(a);rememberBody(a,i,false);
            Run.headRestRotations[i]=a.head==null?Quaternion.identity:a.head.localRotation;
            var stage=Run.director.stage;var screen=stage.contactScreens!=null && stage.contactScreens.Length>0?stage.contactScreens[i%stage.contactScreens.Length]:null;Material contact=screen==null?null:screen.sharedMaterial;Run.workTrays[i]=BuildWorkTray(a,contact);
            var work=Child("Carried shared work · mocap replaceable prop",a.rightHand==null?a.transform:a.rightHand);work.transform.localPosition=new Vector3(.02f,0,.035f);work.transform.localRotation=Quaternion.Euler(0,0,15);
            Box("Bound pages",work.transform,Vector3.zero,new Vector3(.12f,.025f,.17f),Paper);Box("Colored work cover",work.transform,new Vector3(0,.015f,0),new Vector3(.13f,.01f,.18f),i%2==0?Teal:Brass);Run.carriedWorks[i]=work;
            if(i<2)
            {
                string prefab="Assets/OldWebActors/Cast/Actor_"+(i==0?"B":"A")+".prefab";var asset=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if(asset!=null)
                {
                    var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);obj.name="Avatar switch result · slot "+i;var alt=obj.GetComponent<OWActorRig>();alt.actorSlot=a.actorSlot+"-alternate";
                    var appearance=Run.actors.FirstOrDefault(r=>r.appearanceId==alt.appearanceId);if(appearance!=null)foreach(var renderer in alt.visualRoot.GetComponentsInChildren<Renderer>(true)){var shared=appearance.visualRoot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name==renderer.name);if(shared!=null)renderer.sharedMaterials=shared.sharedMaterials;}
                    OwnCastMaterials(alt);alt.animator.runtimeAnimatorController=controller;alt.animator.applyRootMotion=false;alt.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;NormalizeActor(alt);rememberBody(alt,i,true);
                    Run.alternateHeadRestRotations[i]=alt.head==null?Quaternion.identity:alt.head.localRotation;Run.alternateActors[i]=alt;
                    var altWork=Object.Instantiate(work,alt.rightHand==null?alt.transform:alt.rightHand);altWork.name="Carried shared work · alternate body";altWork.transform.localPosition=work.transform.localPosition;altWork.transform.localRotation=work.transform.localRotation;Run.alternateCarriedWorks[i]=altWork;
                    obj.SetActive(true);
                }
            }
        }
        Run.bodyRenderers=bodyRenderers.ToArray();Run.bodySlots=bodySlots.ToArray();Run.bodyAlternates=bodyAlternates.ToArray();Run.bodyBaseEnabled=bodyEnabled.ToArray();
        // Four independent jobs have explicit destinations and handover results.
        Run.keyTimes=new[]{0f,2.56f,7.68f,10.24f,15.36f,20.48f,25.6f,30.72f,35.84f,40.96f,46.08f,51.2f,56.32f,61.44f,66.56f,71.68f,76.8f,79.36f,81.92f,83.2f,84.48f,85.76f,87.04f,88.32f,89.6f,90.88f,92.16f,97.28f,102.4f,104.96f,107.52f,110.08f,112.64f,115.2f,117.76f,120.32f,122.88f,125.44f,128f,133.12f};
        var tracks=new[]{
            new[]{P(-2.3f,1.8f),P(-2.3f,1.8f),P(-4.55f,3.4f),P(-4.55f,3.4f),P(-2.5f,3.0f),P(-2.5f,3),P(-4.5f,4.5f),P(-3.9f,4.0f),P(-3.9f,4),P(-2.5f,3),P(-4.5f,3.4f),P(-4.5f,3.4f),P(-2.2f,4.1f),P(-2.2f,4.1f),P(-4.4f,4.7f),P(-3.8f,4.3f),P(-3.8f,4.3f),P(-3.0f,4.8f),P(-3.1f,5.2f),P(-3.5f,5.8f),P(-3.5f,5.8f),P(-1.8f,5.4f),P(-1.8f,5.4f),P(-1.8f,4.0f),P(-1.7f,4.0f),P(-1.7f,4),P(-1.7f,4),P(-1.7f,4),P(-2.4f,4.7f),P(-2.4f,5.35f),P(-2.4f,5.35f),P(-2.4f,5.35f),P(-2.4f,5.35f),P(-2.4f,5.35f),P(-2.4f,5.35f),P(-2.4f,7.2f),P(-1.8f,7.2f),P(-1.2f,4f),P(-1.2f,4f),P(-1.2f,4f)},
            new[]{P(2.3f,1.8f),P(2.3f,1.8f),P(3.8f,3.8f),P(3.8f,3.8f),P(4.6f,4.6f),P(4.6f,4.6f),P(2.7f,3.1f),P(2.7f,3.1f),P(4.6f,4.6f),P(4.6f,4.6f),P(2.8f,4.4f),P(2.8f,4.4f),P(4.6f,3.4f),P(4.6f,3.4f),P(3.8f,4.6f),P(2.8f,4.5f),P(2.8f,4.5f),P(1.8f,5.3f),P(-1.7f,5.4f),P(-2.3f,5.9f),P(-2.3f,5.9f),P(.7f,5.5f),P(.7f,5.5f),P(1.8f,4.1f),P(1.8f,4.1f),P(1.8f,4.1f),P(1.8f,4.1f),P(1.8f,4.1f),P(2.8f,4.6f),P(2.8f,5.2f),P(2.8f,5.2f),P(2.8f,5.2f),P(2.8f,5.2f),P(2.8f,5.2f),P(2.8f,4.6f),P(3.8f,4.2f),P(4.55f,4.35f),P(4.55f,4.35f),P(4.55f,4.35f),P(4.55f,4.35f)},
            new[]{P(-4.7f,4.7f),P(-4.7f,4.7f),P(-3.5f,4.2f),P(-3.5f,4.2f),P(-4.9f,3.0f),P(-4.9f,3),P(-3.2f,3.6f),P(-3.2f,3.6f),P(-4.7f,4.7f),P(-4.7f,4.7f),P(-3.0f,3.7f),P(-3,3.7f),P(-4.7f,4.7f),P(-4.7f,4.7f),P(-3.0f,4.0f),P(-3,4),P(-2.2f,4.8f),P(-.3f,5.3f),P(1.4f,6.1f),P(2.3f,6.2f),P(2.3f,6.2f),P(.8f,6.1f),P(-.3f,6.1f),P(-1.4f,6.6f),P(-1.4f,6.6f),P(-1.4f,6.6f),P(-1.4f,6.6f),P(-1.4f,6.6f),P(-1.1f,5f),P(-1.1f,5.35f),P(-1.1f,5.35f),P(-1.1f,5.35f),P(-1.1f,5.35f),P(-1.1f,5.35f),P(-1.1f,5.35f),P(-0.8f,9.35f),P(0.6f,10.8f),P(1.0f,8f),P(0.8f,5.5f),P(0.8f,5.5f)},
            new[]{P(4.8f,4.7f),P(4.8f,4.7f),P(3.5f,4.4f),P(3.5f,4.4f),P(4.7f,3.4f),P(4.7f,3.4f),P(3.4f,4.8f),P(3.4f,4.8f),P(4.8f,4.7f),P(4.8f,4.7f),P(3.7f,3.4f),P(3.7f,3.4f),P(4.8f,4.7f),P(4.8f,4.7f),P(3.2f,4),P(3.2f,4),P(4.4f,4.7f),P(4.4f,4.7f),P(3.8f,5.3f),P(4.0f,5.5f),P(4.0f,5.5f),P(3.8f,6.2f),P(2.8f,6.4f),P(1.5f,6.8f),P(1.5f,6.8f),P(1.5f,6.8f),P(1.5f,6.8f),P(1.5f,6.8f),P(4.5f,4.7f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,5.3f),P(4.6f,6.5f),P(3.9f,5.75f),P(3.9f,5.75f)}
        };
        int[] aGest={1,1,5,10,3,3,11,1,1,3,5,5,2,3,11,5,5,7,8,8,7,4,4,7,9,9,9,9,5,5,4,4,4,5,5,4,5,3,3,3};
        int[] bGest={5,5,2,2,11,11,3,3,1,1,5,5,11,11,3,5,5,7,8,8,7,4,4,7,9,9,9,9,5,5,4,4,4,10,10,5,5,5,1,1};
        int[] cGest={11,11,2,3,11,11,3,3,11,11,2,3,11,11,2,3,11,7,4,4,7,4,4,7,9,9,9,9,2,2,4,4,5,5,5,4,4,2,2,3};
        int[] dGest={10,10,4,4,10,10,4,4,10,10,3,3,10,10,4,4,10,7,4,4,7,4,4,7,9,9,9,9,10,10,4,4,4,10,10,10,10,11,11,11};
        var gestureTracks=new[]{aGest,bGest,cGest,dGest};int total=Run.keyTimes.Length*count;Run.positions=new Vector3[total];Run.yaws=new float[total];Run.gestures=new int[total];Run.gazePositions=new Vector3[total];
        for(int k=0;k<Run.keyTimes.Length;k++)for(int i=0;i<count;i++)
        {
            if(tracks[i].Length!=Run.keyTimes.Length || gestureTracks[i].Length!=Run.keyTimes.Length)throw new InvalidOperationException("Cast score length mismatch");
            int at=k*count+i;var p=tracks[i][k];float time=Run.keyTimes[k];if(i<2 && time>=81.92f && time<=84.48f)p.y=.20f;
            Run.positions[at]=p;Run.gestures[at]=gestureTracks[i][k];
            Vector3 gaze=time>=87.04f && time<102.4f?new Vector3(0,1.1f,5.0f):time>=81.92f && time<85.76f?new Vector3(-5f,1.2f,8f):time>=85.76f && time<87.04f?new Vector3(5f,1.4f,6.2f):new Vector3(i%2==0?-4.9f:4.9f,.95f,4.2f);
            if(Run.gestures[at]==3 || Run.gestures[at]==7)gaze=new Vector3(i%2==0?2.7f:-2.7f,1.0f,p.z);
            if(time>=89.60f && time<102.4f)gaze=new Vector3(i%2==0?1.5f:-1.5f,1.0f,i<2?6.6f:4.1f);
            if(time>=102.4f)gaze=i==3?new Vector3(4.8f,1.0f,6.0f):new Vector3(p.x,1.15f,time<117.76f?6.0f:10.2f);
            if(time>=122.88f)gaze=i==3?new Vector3(4.25f,1.04f,5.9f):new Vector3(4.72f,.85f,4.65f);
            Run.gazePositions[at]=gaze;Vector3 d=gaze-p;Run.yaws[at]=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;
        }
    }
    static Vector3 P(float x,float z){return new Vector3(x,0,z);}
    static void PrepareFont()
    {
        string path=Root+"/Fonts/HubChinese.asset";
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)==null)
        {
            string from=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/OldWebMap/Episode123/Revision3/Fonts/HubChinese.asset")!=null?"Assets/OldWebMap/Episode123/Revision3/Fonts/HubChinese.asset":"Assets/OldWebMap/Episode123/Revision2/Fonts/RevisionChinese.asset";
            if(!AssetDatabase.CopyAsset(from,path))throw new InvalidOperationException("Cannot copy project font");
        }
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        string chars=File.ReadAllText("AgentScripts/OWEpisodeV4HubCast.cs")+File.ReadAllText("Assets/OldWebMap/Scripts/OWV4HubCast.cs")+"小满阿澄木木北辰访客✓→◀▶ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /+·:.,()";
        Font.atlasPopulationMode=AtlasPopulationMode.Dynamic;Font.isMultiAtlasTexturesEnabled=true;
        string missing=new string(chars.Where(c=>!char.IsControl(c) && !Font.HasCharacter(c)).Distinct().ToArray());if(missing.Length>0)Font.TryAddCharacters(missing,out string remaining);
        foreach(var tex in Font.atlasTextures)if(tex!=null && !AssetDatabase.Contains(tex))AssetDatabase.AddObjectToAsset(tex,Font);EditorUtility.SetDirty(Font);
    }
    public static object RepairPrograms()
    {
        Folder(Root+"/Programs");
        foreach(string type in new[]{"OWV4HubCast","OWV4Nameplates"})
        {
            string path="Assets/OldWebMap/Scripts/"+type+".cs";var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);if(script==null)throw new InvalidOperationException("Missing "+path);
            if(!AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Any(a=>a!=null && a.sourceCsScript==script)){var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;AssetDatabase.CreateAsset(program,Root+"/Programs/"+type+".asset");}
        }
        AssetDatabase.SaveAssets();UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new InvalidOperationException("V4 hub/nameplate Udon compile failed");return "V4 hub and nameplate programs compiled";
    }
    public static object RepairProgram(){return RepairPrograms();}
    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the stopped owned V4 scene; keep the user's v2 saved scene untouched");
        Mats.Clear();UsedAssets.Clear();Cores.Clear();Rims.Clear();MirrorReports.Clear();CastScaleReports.Clear();
        foreach(string folder in new[]{"Materials","Meshes","Textures","Fonts","Motion","Programs"})Folder(Root+"/"+folder);
        PrepareFont();Stone=Mat("Pale textured stone","#c7ccca");Stone.mainTexture=Texture("Stone speckled tile",0);Stone.mainTextureScale=new Vector2(3,3);
        Slate=Mat("Deep blue floor tiles","#334e5a");Slate.mainTexture=Texture("Slate floor grid",0);Slate.mainTextureScale=new Vector2(3,3);
        Brass=Mat("Muted ochre gold trim","#b5a668");Wood=Mat("Warm timber grain","#624027");Wood.mainTexture=Texture("Wood long grain",1);Wood.mainTextureScale=new Vector2(1,3);
        DarkWood=Mat("Dark timber edges","#30261e");Grass=Mat("Textured green gathering lawn","#1b604a");Grass.mainTexture=Texture("Mottled garden ground",2);Grass.mainTextureScale=new Vector2(4,4);
        Teal=Mat("Historical enamel teal","#174552");Cyan=Mat("VRC cyan","#58dce7",.65f);Blue=Mat("Portal blue","#278dda",.55f);Paper=Mat("Ivory pages and fabric","#e5dfca");Red=Mat("Terracotta red upholstery","#8f3e2d");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();if(director==null || director.stage==null)throw new InvalidOperationException("Existing episode director/stage required");
        var owner=GameObject.Find("OWEpisode123").transform;Journey=Object.FindObjectOfType<OWV4Journey>();
        if(Journey==null || Journey.worldFrame==null)throw new InvalidOperationException("Build the V4 journey/worldFrame first");
        var cast=director.stage.actors!=null && director.stage.actors.Length==4?director.stage.actors:owner.GetComponentsInChildren<OWActorRig>(true).Where(a=>a.actorSlot!=null && a.actorSlot.StartsWith("episode-ensemble-") && !a.actorSlot.EndsWith("-alternate")).OrderBy(a=>a.actorSlot).Take(4).ToArray();
        if(cast.Length!=4)throw new InvalidOperationException("Four preserved actor slots required");
        foreach(var previous in owner.GetComponentsInChildren<OWV3HubCast>(true))
        {
            if(previous.bodyRenderers!=null && previous.bodyBaseEnabled!=null)for(int i=0;i<previous.bodyRenderers.Length && i<previous.bodyBaseEnabled.Length;i++)if(previous.bodyRenderers[i]!=null)previous.bodyRenderers[i].enabled=previous.bodyBaseEnabled[i];
        }
        foreach(var previous in owner.GetComponentsInChildren<OWV4HubCast>(true))
        {
            if(previous.bodyRenderers!=null && previous.bodyBaseEnabled!=null)for(int i=0;i<previous.bodyRenderers.Length && i<previous.bodyBaseEnabled.Length;i++)if(previous.bodyRenderers[i]!=null)previous.bodyRenderers[i].enabled=previous.bodyBaseEnabled[i];
        }
        foreach(var actor in cast)actor.transform.SetParent(Journey.worldFrame,true);
        foreach(var previous in owner.GetComponentsInChildren<OWV3HubCast>(true).ToArray())Object.DestroyImmediate(previous.transform.parent.gameObject);
        foreach(var previous in owner.GetComponentsInChildren<OWV4HubCast>(true).ToArray())Object.DestroyImmediate(previous.transform.parent.gameObject);
        var oldNames=GameObject.Find("OW V4 Historical Nameplates");if(oldNames!=null)Object.DestroyImmediate(oldNames);
        foreach(var actor in cast)foreach(var child in actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Role · ") || t.name.StartsWith("Carried shared work · ") || t.name=="Readable low contact tray").ToArray())if(child!=null)Object.DestroyImmediate(child.gameObject);
        var root=Child("Revision4 Hub and Cast",Journey.worldFrame).transform;Run=Child("World and cast clock",root).AddUdonSharpComponent<OWV4HubCast>();Run.director=director;Run.journey=Journey;Run.worldRoot=root;Run.regionOffsets=Enumerable.Range(0,4).Select(Journey.RegionOffset).ToArray();
        foreach(var actor in cast)actor.transform.SetParent(root,false);
        BuildWorkshop(root);Build2017(root);Build2019(root);BuildCast(cast,root);BuildMenus(root);BuildConditions(root);BuildBarriers();BuildNameplates(owner);
        Run.workshop.localPosition=Journey.RegionOffset(0);
        Run.hub2017.localPosition=Journey.RegionOffset(1)+new Vector3(0,-.045f,4.1f);Run.hub2017.localRotation=Quaternion.identity;
        Run.hub2019.localPosition=Journey.RegionOffset(2)+new Vector3(0,-.04f,5.1f);Run.hub2019.localRotation=Quaternion.identity;
        Run.home2019.localPosition=Journey.RegionOffset(2)+new Vector3(-7.8f,-.04f,6.2f);Run.home2019.localRotation=Quaternion.Euler(0,24,0);
        Run.conditionsRoot.localPosition=Journey.RegionOffset(3);Run.maintenanceRoot.localPosition=Journey.RegionOffset(3)+new Vector3(4.65f,0,4.9f);
        Run.legacyWorkstations=owner.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Contact workstation").Select(t=>t.gameObject).ToArray();
        Run.legacyContactDiscs=cast.SelectMany(a=>a.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Local contact surface").Select(t=>t.gameObject).ToArray();
        Run.portalCores=Cores.ToArray();Run.portalRims=Rims.ToArray();
        // The menu assembly shares the Hub visibility window without entering a Stage cue root.
        Run.Sample(0);UdonSharpEditorUtility.CopyProxyToUdon(Run);
        foreach(var actor in Run.actors)UdonSharpEditorUtility.CopyProxyToUdon(actor);
        foreach(var actor in Run.alternateActors)if(actor!=null)UdonSharpEditorUtility.CopyProxyToUdon(actor);
        UdonSharpEditorUtility.CopyProxyToUdon(Run.nameplates);
        foreach(var actor in Run.actors.Concat(Run.alternateActors).Where(a=>a!=null)){PrefabUtility.RecordPrefabInstancePropertyModifications(actor);PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(actor.visualRoot);PrefabUtility.RecordPrefabInstancePropertyModifications(actor.animator);foreach(var renderer in actor.visualRoot.GetComponentsInChildren<Renderer>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        foreach(var mat in Mats.Values)EditorUtility.SetDirty(mat);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Temp);var report=new{scene=ScenePath,persistedMeshes=AssetDatabase.FindAssets("t:Mesh",new[]{Root+"/Meshes"}).Length,cast=Run.actors.Length,keyframes=Run.keyTimes.Length,humanoidClips=12,alternateBodies=Run.alternateActors.Count(a=>a!=null),castReadability=CastScaleReports.ToArray(),contactTrays=Run.workTrays.Length,actualAvatarPreviews=2,portals=Cores.Count,conditionEntrances=Run.conditionFrames.Length,maintenanceArchive=Run.maintenanceRoot!=null,mirrors=MirrorReports.ToArray(),actualImportedParts=UsedAssets.ToArray(),source="2017 official New Hub images + 2019 official Home/Hub images",reconstruction="Source-based condensed theatrical layout; no metric reconstruction claim",originalUserRigObjectsPreserved=true};
        File.WriteAllText(Path.Combine(Temp,"hub-cast-build.json"),JsonConvert.SerializeObject(report,Formatting.Indented));return JsonConvert.SerializeObject(report);
    }
    public static object Sample(float time)
    {
        var run=Object.FindObjectOfType<OWV4HubCast>();if(run==null)throw new InvalidOperationException("Build V4 hub/cast first");
        if(run.director!=null && run.director.stage!=null)run.director.stage.Sample(time);
        run.Sample(time);
        string[] names={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
        foreach(var actor in run.actors.Concat(run.alternateActors).Where(a=>a!=null))
        {
            if(actor.animator==null || !actor.animator.gameObject.activeInHierarchy || !actor.animator.enabled)continue;int gesture=actor.animator.GetInteger("V4Pose");float phase=actor.animator.GetFloat("V4PoseTime");actor.animator.Play(names[Mathf.Clamp(gesture,0,names.Length-1)],0,phase);actor.animator.Update(0);
        }
        run.Sample(time);return JsonConvert.SerializeObject(new{time,run.visibleEra,run.sampledKey,run.appearanceChanged,run.worldChosen,run.conditionsPhase,savedCopy=run.archivedCopy!=null && run.archivedCopy.activeSelf,actors=run.actors.Select(a=>new{a.actorSlot,position=new[]{a.transform.localPosition.x,a.transform.localPosition.y,a.transform.localPosition.z},yaw=a.transform.localEulerAngles.y,gesture=a.animator.GetInteger("V4Pose")}).ToArray()});
    }
    public static object ValidateHands()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor verification only");
        var run=Object.FindObjectOfType<OWV4HubCast>();if(run==null)throw new InvalidOperationException("Build V4 hub/cast first");
        var rows=new List<object>();int crossed=0;
        string[] names={"V4Idle","V4Type","V4Carry","V4Present","V4Point","V4Inspect","V4Walk","V4Wave","V4Calibrate","V4Talk","V4Adjust","V4Shelve"};
        try
        {
            foreach(var actor in run.actors.Concat(run.alternateActors).Where(a=>a!=null))
            {
                ActivateActor(actor);
                foreach(string action in names)foreach(float phase in new[]{.1f,.35f,.5f,.7f,.9f})
                {
                    actor.animator.SetFloat("V4PoseTime",phase);actor.animator.Play(action,0,phase);actor.animator.Update(.02f);actor.animator.Update(0);
                    Vector3 left=actor.transform.InverseTransformPoint(actor.leftHand.position),right=actor.transform.InverseTransformPoint(actor.rightHand.position);
                    bool bad=left.x>=right.x-.015f;if(bad)crossed++;
                    rows.Add(new{actor.actorSlot,actor.appearanceId,action,phase,left=new[]{left.x,left.y,left.z},right=new[]{right.x,right.y,right.z},crossed=bad,separation=right.x-left.x});
                }
            }
        }
        finally{Sample(0);}
        Directory.CreateDirectory(Temp);var result=new{samples=rows.Count,crossed,success=crossed==0,rows};File.WriteAllText(Path.Combine(Temp,"hands-verification.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(new{samples=rows.Count,crossed,success=crossed==0,path=Path.Combine(Temp,"hands-verification.json")});
    }
    public static object InspectCastGeometry()
    {
        var run=Object.FindObjectOfType<OWV4HubCast>();if(run==null)throw new InvalidOperationException("Build V4 hub/cast first");
        var rows=new List<object>();
        foreach(var actor in run.actors.Concat(run.alternateActors).Where(a=>a!=null))
        {
            var skins=actor.visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);var bounds=skins[0].bounds;foreach(var skin in skins.Skip(1))bounds.Encapsulate(skin.bounds);
            string prefabPath=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor.gameObject);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);var baseline=prefab==null?null:prefab.GetComponent<OWActorRig>();
            rows.Add(new{actor.actorSlot,actor.appearanceId,visualScale=actor.visualRoot.localScale.y,rendererHeight=bounds.size.y,rendererMin=bounds.min.y-actor.transform.position.y,leftFoot=actor.leftFoot.position.y-actor.transform.position.y,rightFoot=actor.rightFoot.position.y-actor.transform.position.y,prefabPath,baselineHeight=baseline==null?0:baseline.modelHeight,baselineScale=baseline==null?0:baseline.visualRoot.localScale.y,skins=skins.Select(s=>new{name=s.name,height=s.bounds.size.y,localHeight=s.localBounds.size.y,scale=s.transform.lossyScale.y}).ToArray()});
        }
        return JsonConvert.SerializeObject(rows);
    }
    public static object ValidateNameplates()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped owned V4 scene required");
        var run=Object.FindObjectOfType<OWV4HubCast>();if(run==null || run.nameplates==null)throw new InvalidOperationException("Build V4 nameplates first");
        var names=run.nameplates;float originalTime=run.sampledTime;var position=names.transform.position;var rotation=names.transform.rotation;var framePosition=run.journey.worldFrame.localPosition;var rows=new List<object>();float error=0;
        bool pool=names.playerPlates.Length==80 && names.playerLabels.Length==80;int full=0,left=0,rejoined=0,hidden=0;
        try
        {
            names.transform.position=new Vector3(3,.2f,-1);names.transform.rotation=Quaternion.Euler(0,31,0);run.journey.worldFrame.localPosition=-run.RegionOffset(1);Sample(85.12f);
            for(int i=0;i<4;i++)
            {
                var actor=run.appearanceChanged && run.alternateActors[i]!=null?run.alternateActors[i]:run.actors[i];float e=Vector3.Distance(names.actorPlates[i].position,actor.head.position+Vector3.up*names.headGap);error=Mathf.Max(error,e);rows.Add(new{actor=i,name=names.actorLabels[i].text,worldPositionError=e});
            }
            for(int i=0;i<80;i++)names.SetPreviewPlayer(i,"访客"+(i+1).ToString("00"),new Vector3((i%10-4.5f)*.6f,1.5f,3f+i/10));
            names.Sample(85.12f);full=names.activePlayerCount;
            for(int i=0;i<80;i++)error=Mathf.Max(error,Vector3.Distance(names.playerPlates[i].position,names.previewWorldHeads[i]+Vector3.up*names.headGap));
            names.ClearPreviewPlayer(31);names.Sample(85.12f);left=names.activePlayerCount;
            names.SetPreviewPlayer(31,"后来的人",new Vector3(.5f,1.6f,4));names.Sample(88.64f);rejoined=names.activePlayerCount;
            bool namesRestored=names.playerLabels[31].text=="xxxx" && names.playerLabels.All(label=>label.text=="xxxx") && names.actorLabels.All(label=>label.text=="xxxx") && names.sampledEra==2019;names.Sample(0);hidden=names.visiblePlayerCount;
            bool success=pool && full==80 && left==79 && rejoined==80 && hidden==0 && namesRestored && error<.001f && !names.transform.IsChildOf(run.journey.worldFrame);
            var result=new{success,pool=names.playerPlates.Length,full,left,rejoined,hidden,namesRestored,maxWorldPositionError=error,actorNames=rows,scope="Pooled join/leave and parent-transform simulation; real player API path still requires client integration."};Directory.CreateDirectory(Temp);File.WriteAllText(Path.Combine(Temp,"nameplate-validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(result);
        }
        finally{names.EndPreview();names.transform.position=position;names.transform.rotation=rotation;run.journey.worldFrame.localPosition=framePosition;Sample(originalTime);}
    }
    public static object Validate()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped owned V4 scene required");
        var run=Object.FindObjectOfType<OWV4HubCast>();if(run==null || run.journey==null)throw new InvalidOperationException("Build V4 hub/cast first");
        float originalTime=run.sampledTime;var frame=run.journey.worldFrame;var framePosition=frame.localPosition;var snapshots=new List<object>();bool fixedSets=true,blocked=true;float maxHeightError=0,maxSoleError=0;
        try
        {
            foreach(float time in new[]{0f,79.36f,81.92f,85.12f,88.32f,104.96f,110.08f,114f,118.8f,121.6f,124.16f,129.28f})
            {
                frame.localPosition=-run.ActorRegionOffset(time);Sample(time);
                fixedSets&=Vector3.Distance(run.hub2017.localPosition,run.RegionOffset(1)+new Vector3(0,-.045f,4.1f))<.001f;
                fixedSets&=Vector3.Distance(run.hub2019.localPosition,run.RegionOffset(2)+new Vector3(0,-.04f,5.1f))<.001f;
                fixedSets&=Vector3.Distance(run.maintenanceRoot.localPosition,run.RegionOffset(3)+new Vector3(4.65f,0,4.9f))<.001f;
                var actor2=run.actors[2].transform.localPosition-run.ActorRegionOffset(time);var actor3=run.actors[3].transform.localPosition-run.ActorRegionOffset(time);
                if(time>=102.4f && run.sampledGateLocked[0])blocked&=actor2.z<5.6f;
                if(time>=102.4f && run.sampledGateLocked[1])blocked&=actor2.z<9.6f;
                if(time>=102.4f && run.sampledGateLocked[2])blocked&=actor3.z<5.6f;
                snapshots.Add(new{time,era=run.visibleEra,region=run.ActorRegionOffset(time).z,gateAngles=(float[])run.sampledGateAngles.Clone(),locked=(bool[])run.sampledGateLocked.Clone()});
            }
            foreach(var actor in run.actors.Concat(run.alternateActors).Where(a=>a!=null))
            {
                actor.animator.SetFloat("V4PoseTime",0);actor.animator.Play("V4Idle",0,0);actor.animator.Update(0);
                int slot=Array.IndexOf(run.actors,actor);bool alternate=slot<0;if(alternate)slot=Array.IndexOf(run.alternateActors,actor);
                var renderers=run.bodyRenderers.Where((renderer,index)=>renderer!=null && run.bodySlots[index]==slot && run.bodyAlternates[index]==alternate && run.bodyBaseEnabled[index] && renderer.gameObject.activeInHierarchy).ToArray();
                var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);maxHeightError=Mathf.Max(maxHeightError,Mathf.Abs(bounds.size.y-1.25f));maxSoleError=Mathf.Max(maxSoleError,Mathf.Abs(bounds.min.y-actor.transform.position.y));
            }
            int gateColliders=run.conditionsRoot.GetComponentsInChildren<Collider>(true).Length;int movingColliders=run.worldRoot.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled);
            bool sharedFrame=run.worldRoot.IsChildOf(frame) && run.eraUiRoots.All(g=>g.transform.IsChildOf(frame));
            bool materialsOwned=run.bodyRenderers.Where(r=>r!=null).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).All(m=>AssetDatabase.GetAssetPath(m).StartsWith(Root+"/Materials/Actor_",StringComparison.Ordinal));
            var plateResult=JsonConvert.DeserializeObject<Dictionary<string,object>>((string)ValidateNameplates());bool plates=(bool)plateResult["success"];
            bool success=fixedSets && blocked && sharedFrame && materialsOwned && gateColliders==0 && movingColliders==0 && maxHeightError<.015f && maxSoleError<.01f && plates;
            var result=new{success,fixedSets,blocked,sharedFrame,materialsOwned,gateColliders,movingColliders,maxHeightError,maxSoleError,soleEnvelopeToleranceMetres=.01f,soleMetric="Conservative Renderer.bounds in retargeted idle; 1 cm floor-envelope budget, not a contact solver",nameplates=plates,snapshots};Directory.CreateDirectory(Temp);File.WriteAllText(Path.Combine(Temp,"validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(result);
        }
        finally{frame.localPosition=framePosition;Sample(originalTime);}
    }
}
