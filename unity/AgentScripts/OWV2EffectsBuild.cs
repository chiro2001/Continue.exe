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

public static class OWV2EffectsBuild
{
    const string Root = "Assets/OldWebMap/FullFilm/Revision2";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v2");
    static OWV2Presentation Run() { if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != "Assets/Scenes/OWM_FullFilm_v2.unity") throw new Exception("Stopped compiled V2 required"); return Object.FindObjectOfType<OWV2Presentation>(true); }
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
        var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;Style(r);return t;
    }
    static Transform Imported(string filename,string name,Transform parent)
    {
        var pivot=Node(name,parent);if(pivot.childCount==0){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+filename+".fbx");if(prefab==null)throw new Exception("Missing FBX: "+filename);var child=(GameObject)PrefabUtility.InstantiatePrefab(prefab);child.transform.SetParent(pivot,false);}return pivot;
    }
    static Transform Pointer(string name,Transform parent,OWV2Presentation run)
    {
        var source=run.basis.legacy.montage.cursors[0];var t=Node(name,parent);var f=t.GetComponent<MeshFilter>();if(f==null)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
        var m=Mat("Readable original operating-system pointer","OldWebMap/V2/Source Surface");m.SetColor("_Tint",Color.white);var r=t.GetComponent<MeshRenderer>();if(r==null)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=m;Style(r);t.localScale=Vector3.one*.22f;
        var edge=Node("Black pixel outline",t);edge.localScale=Vector3.one*1.22f;edge.localPosition=new Vector3(0,0,.003f);var ef=edge.GetComponent<MeshFilter>();if(ef==null)ef=edge.gameObject.AddComponent<MeshFilter>();ef.sharedMesh=f.sharedMesh;var er=edge.GetComponent<MeshRenderer>();if(er==null)er=edge.gameObject.AddComponent<MeshRenderer>();var em=Mat("Native pointer black contour","OldWebMap/V2/Source Surface");em.SetColor("_Tint",Color.black);er.sharedMaterial=em;Style(er);return t;
    }
    static void Opening(OWV2Presentation run)
    {
        var root=run.basis.legacy.startupObject;var oldField=root.Find("Field");oldField.localPosition=new Vector3(0,.1f,1.3f);oldField.localScale=Vector3.one*1.45f;oldField.GetComponent<Renderer>().sharedMaterial.SetFloat("_Gain",.38f);
        var depth=Node("Physical star and copper depth",root);var starGeo=new Geometry();var copperGeo=new Geometry();var railGeo=new Geometry();var rand=new System.Random(17017);
        for(int layer=0;layer<6;layer++)for(int n=0;n<42;n++)
        {
            float x=(float)(rand.NextDouble()*2.42-1.21),y=(float)(rand.NextDouble()*1.90-.85),z=.12f+layer*.19f;
            if(Mathf.Abs(x)<1.05f&&y>-.67f&&y<.70f)y=(n%2==0?-.74f:.88f)+(float)rand.NextDouble()*.05f;
            float size=.005f+(float)rand.NextDouble()*.009f;starGeo.Quad(new Vector3(x,y,z),Vector3.right*size,Vector3.up*size,new Vector4(n+layer*42,0,0,0),new Color(.50f+layer*.06f,.72f+layer*.035f,1,.75f));
        }
        Color[] pal={new Color(.2f,.48f,1),new Color(.1f,.9f,.8f),new Color(.9f,.2f,.7f),new Color(1,.64f,.16f)};
        for(int i=0;i<8;i++)copperGeo.Quad(new Vector3(0,i<4?.87f+i*.025f:-.86f+(i-4)*.028f,.14f+(i%4)*.22f),new Vector3(1.27f,0,0),new Vector3(0,.025f,0),new Vector4(i,0,0,0),pal[i%4]);
        for(int layer=0;layer<4;layer++)
        {
            float z=.08f+layer*.32f,hx=1.31f-layer*.035f,hy=1.02f-layer*.025f;
            railGeo.Quad(new Vector3(0,.1f+hy,z),new Vector3(hx,0,0),new Vector3(0,.006f,0),new Vector4(layer,0,0,0),new Color(.13f,.35f,.55f,.6f));
            railGeo.Quad(new Vector3(0,.1f-hy,z),new Vector3(hx,0,0),new Vector3(0,.006f,0),new Vector4(layer,0,0,0),new Color(.13f,.35f,.55f,.6f));
            for(int side=-1;side<=1;side+=2)railGeo.Quad(new Vector3(side*hx,.1f,z),new Vector3(.006f,0,0),new Vector3(0,hy,0),new Vector4(layer,0,0,0),new Color(.13f,.35f,.55f,.6f));
        }
        string[] names={"Window physical stars","Window physical copper bars","Window recess contours"};Geometry[] geometries={starGeo,copperGeo,railGeo};run.openingMaterials=new Material[3];
        for(int i=0;i<3;i++){var mat=Mat(names[i],"OldWebMap/V2/Depth Copper");mat.SetFloat("_Idle",1);mat.SetFloat("_Mode",i);mat.SetFloat("_Opacity",i==2?.38f:.8f);mat.SetColor("_Tint",Color.white);MeshNode(names[i],depth,geometries[i],mat);run.openingMaterials[i]=mat;}
    }
    static void Archives(OWV2Presentation run)
    {
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v2-atlas.json"));var sources=(JArray)data["sources"];int count=sources.Count;
        var root=Node("V2 accumulated archive space",run.transform);run.overloadField=root.gameObject;var atlas=Tex("archive-atlas.png",4096);
        var m=Mat("Provenance-linked archive field","OldWebMap/V2/Archive Field");m.mainTexture=atlas;m.SetFloat("_Count",count);m.SetFloat("_Columns",(float?)data["atlas_columns"]??8f);m.SetFloat("_Rows",(float?)data["atlas_rows"]??8f);run.fieldMaterial=m;
        var g=new Geometry();
        // Four staggered, spaced banks. The centre stays open for the primary pair.
        for(int bank=0;bank<4;bank++)for(int row=0;row<6;row++)for(int col=0;col<12;col++)
        {
            int i=bank*72+row*12+col;float yaw=-165f+col*30f+bank*5.2f;float radius=9.6f+bank*2.9f;
            float angle=yaw*Mathf.Deg2Rad;float height=-.7f+row*1.45f+bank*.12f;var centre=new Vector3(Mathf.Sin(angle)*radius,height,Mathf.Cos(angle)*radius-2.8f);
            var q=Quaternion.Euler(0,yaw,(col%3-1)*3f);float w=bank==0?2.25f:2.65f,h=bank==0?1.25f:1.48f;
            g.Quad(centre,q*Vector3.right*w*.5f,q*Vector3.up*h*.5f,new Vector4(i%count,1-bank*.18f,((i*17)%97)/125f+bank*.04f,.32f+(i%4)*.32f),Color.white);
        }
        MeshNode("288 spaced physical archive windows",root,g,m);
        var focus=Node("Readable archive and related detail",run.transform);run.archiveFocus=focus;
        run.archiveMainMaterial=Mat("Archive readable primary","OldWebMap/V2/Source Surface");run.archiveDetailMaterial=Mat("Archive related detail","OldWebMap/V2/Source Surface");
        run.archiveMain=Plane("Primary full source",focus,run.archiveMainMaterial,new Vector3(-.78f,2.15f,5.2f),new Vector3(4.3f,2.7f,1));
        run.archiveDetail=Plane("Related title and page detail",focus,run.archiveDetailMaterial,new Vector3(2.5f,2.55f,5.05f),new Vector3(2,1.6f,1));
        var dark=Mat("Archive source footer dark","Unlit/Color");dark.color=new Color(.012f,.025f,.035f);
        Plane("Footer belongs to the source window",focus,dark,new Vector3(-.78f,.51f,5.17f),new Vector3(4.3f,.25f,1));
        run.archiveFile=Text("Source file and page state",focus,new Vector3(-.78f,.51f,5.14f),new Vector2(4.1f,.22f),1.12f,run.basis.keyText.font);
        run.archiveTextures=new Texture2D[count];run.archiveNames=new string[count];run.archiveAspects=new float[count];
        var events=(JArray)data["focus_events"];run.archiveSequence=events.Select(e=>(int)e["primary"]).ToArray();run.archiveCompanionSequence=events.Select(e=>(int)e["secondary"]).ToArray();
        var readable=new HashSet<int>(run.archiveSequence.Concat(run.archiveCompanionSequence));
        for(int i=0;i<count;i++){run.archiveTextures[i]=readable.Contains(i)?Tex("source-"+i.ToString("00")+".png"):null;run.archiveAspects[i]=(float)sources[i]["display_aspect"];run.archiveNames[i]=(string)sources[i]["display_label"]??(string)sources[i]["title"];}
        run.archivePointer=Pointer("Pointer leaves the overloaded screen",focus,run);
    }
    static void Native(OWV2Presentation run)
    {
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v2-native-ui.json"));var groups=(JArray)data["groups"];run.nativeCompanionAspects=new float[24];run.nativePointerTargets=new Vector3[6];
        run.nativeMainMaterial.shader=Shader.Find("OldWebMap/V2/Source Surface");
        run.nativeSecondaryMaterial.shader=Shader.Find("OldWebMap/V2/Source Surface");
        var carrier=Node("Companion remains beside the primary document",run.nativeRoot.transform);carrier.localPosition=new Vector3(.42f,0,0);run.nativeSecondary.transform.SetParent(carrier,false);
        var paper=Mat("Native detail paper carrier","OldWebMap/V2/Source Surface");paper.SetColor("_Tint",new Color(.94f,.95f,.95f,1));
        Plane("Readable backing for source text",run.nativeSecondary.transform,paper,new Vector3(0,0,.012f),new Vector3(1.045f,1.10f,1));
        for(int i=0;i<6;i++)
        {
            string id=(string)groups[i]["id"];
            for(int state=0;state<4;state++){int at=i*4+state;var tex=Tex("native-"+id+"-"+state.ToString("00")+"-secondary-crop.png");run.nativeCompanions[at]=tex;run.nativeCompanionAspects[at]=tex.width/(float)tex.height;}
            // Aim at the actual source control or content region, never a drawn replacement button.
            var regions=groups[i]["change_regions"] as JArray;var rect=regions!=null&&regions.Count>0?regions[0]["uv_rect"]:null;
            float u=rect==null?.48f:(float)rect[0]+(float)rect[2]*.50f,v=rect==null?.45f:(float)rect[1]+(float)rect[3]*.50f;
            run.nativePointerTargets[i]=new Vector3(-.65f+(u-.5f)*4.45f,2.25f+(v-.5f)*2.9f,5.40f);
        }
        run.nativeStatus.fontSize=1.1f;run.nativePointer=Pointer("Planar document pointer",run.nativeRoot.transform,run);
        string[] history={"Assets/OldWebMap/Episode123/Revision4/Images/3q-cnet-popup.png","Assets/OldWebMap/Episode123/Revision4/Images/google-reader-shutdown.png","Assets/OldWebMap/Episode123/Revision4/Images/adobe-flash-eol-announcement.png","Assets/OldWebMap/Episode123/Revision4/Images/vrchat-eac-2022.png","Assets/OldWebMap/Episode123/Revision4/Images/reddit-private-notice.png"};
        run.accessHistory=history.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();if(run.accessHistory.Any(t=>t==null))throw new Exception("Historical access source not imported");run.accessHistoryAspects=run.accessHistory.Select(t=>t.width/(float)t.height).ToArray();run.accessHistoryLabels=new[]{"2010","2013","2017","2022","2023"};
    }
    static void Failures(OWV2Presentation run)
    {
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v2-failure-ui.json"));var states=(JArray)data["states"];
        run.failureStates=new Texture2D[4];run.failureDetails=new Texture2D[4];run.failureDetailAspects=new float[4];
        for(int i=0;i<4;i++){run.failureStates[i]=Tex((string)states[i]["main"]);run.failureDetails[i]=Tex((string)states[i]["detail"]);run.failureDetailAspects[i]=run.failureDetails[i].width/(float)run.failureDetails[i].height;}
    }
    static void Architecture(OWV2Presentation run)
    {
        var parent=run.basis.legacy.journey.worldFrame;var barriers=Node("Native windows become access structures",parent);run.barrierRoot=barriers;
        var m=Mat("Dark access frames","Standard");m.color=new Color(.16f,.22f,.25f);m.SetFloat("_Metallic",.65f);m.SetFloat("_Glossiness",.3f);
        var glow=Mat("Access red status","Unlit/Color");glow.color=new Color(.85f,.22f,.18f);run.barrierSlats=new Transform[8];
        for(int i=0;i<8;i++)
        {
            var slat=Node("Gate frame "+i,barriers);run.barrierSlats[i]=slat;
            Solid("Upright",slat,m,Vector3.zero,new Vector3(.14f,4.2f,.16f));Solid("Lintel",slat,m,new Vector3((i%2==0?1:-1)*.75f,2.02f,0),new Vector3(1.6f,.14f,.18f));
            Solid("Status light",slat,glow,new Vector3(0,.65f,-.1f),new Vector3(.04f,.20f,.02f));
        }
        run.hubDetails=Imported("fullfilm_v2_hub_detail_kit","Detailed VRC architectural pieces",parent);run.hubDetails.localPosition=run.basis.legacy.journey.RegionOffset(2)+new Vector3(0,-.12f,3.6f);run.hubDetails.localRotation=Quaternion.Euler(0,0,0);
        foreach(var r in run.hubDetails.GetComponentsInChildren<Renderer>(true))Style(r);
    }
    static void Climax(OWV2Presentation run)
    {
        var root=Node("Channel-associated spatial contours",run.transform);run.climaxEffects=root.gameObject;run.channelEffects=new Transform[4];run.channelParticles=new ParticleSystem[4];run.channelParticleMaterials=new Material[4];run.channelLights=new Light[4];
        var material=Mat("Musical spatial contour accent","OldWebMap/V2/Depth Copper");material.SetFloat("_Mode",2);material.SetColor("_Tint",Color.white);run.contourMaterial=material;
        Color[] colors={new Color(.30f,1,.74f,.7f),new Color(.22f,.78f,1,.7f),new Color(1,.75f,.30f,.7f),new Color(1,.42f,.8f,.7f)};
        for(int ch=0;ch<4;ch++)
        {
            var geometry=new Geometry();
            for(int band=0;band<3;band++)for(int k=0;k<96;k++)
            {
                float a=k/96f*Mathf.PI*2,r=.58f+band*.22f;var centre=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,(band-1)*.32f+Mathf.Sin(a*2+band)*.13f);
                var tangent=new Vector3(-Mathf.Sin(a),Mathf.Cos(a),0)*(.012f+band*.003f);var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.006f;
                geometry.Quad(centre,tangent,radial,new Vector4(ch,0,0,0),colors[ch]);
            }
            run.channelEffects[ch]=MeshNode("CH"+ch+" rendered near its physical source",root,geometry,material);
            var node=Node("CH"+ch+" authored short particles",root);var ps=node.GetComponent<ParticleSystem>();if(ps==null)ps=node.gameObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(17017+ch);
            var main=ps.main;main.duration=4;main.loop=true;main.playOnAwake=true;main.startLifetime=new ParticleSystem.MinMaxCurve(.55f,1.4f);main.startSpeed=new ParticleSystem.MinMaxCurve(.02f,.12f);main.startSize=new ParticleSystem.MinMaxCurve(.009f,.025f);main.maxParticles=72;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=colors[ch];
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.8f;var emission=ps.emission;emission.rateOverTime=30;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();var pmat=Mat("CH"+ch+" spatial particles","OldWebMap/V3/Signal Particles");pmat.SetColor("_Tint",colors[ch]);pmat.SetFloat("_Shape",ch%2);renderer.sharedMaterial=pmat;Style(renderer);run.channelParticles[ch]=ps;run.channelParticleMaterials[ch]=pmat;
            var lightNode=Node("CH"+ch+" world light",root);var light=lightNode.GetComponent<Light>();if(light==null)light=lightNode.gameObject.AddComponent<Light>();light.type=LightType.Point;light.shadows=LightShadows.None;light.range=6f;light.intensity=.3f;light.color=colors[ch];run.channelLights[ch]=light;
        }
    }
    static void EditorFrame(OWV2Presentation run)
    {
        run.editorFrame=Imported("fullfilm_v2_editor_frame","Hollow editor frame expands into the world",run.editorStage.transform);
        foreach(var r in run.editorFrame.GetComponentsInChildren<Renderer>(true))
        {
            var source=r.sharedMaterial;if(source==null)continue;var m=Mat("Editor frame "+source.name,"Standard");if(source.HasProperty("_Color"))m.color=source.color;m.SetFloat("_Glossiness",.28f);r.sharedMaterial=m;Style(r);
        }
    }
    static void WorldTopology(OWV2Presentation run)
    {
        var root=run.finalWorld;var g=new Geometry();var replacements=new Dictionary<Material,Material>();var originals=new Dictionary<Material,Color>();int part=0;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.name.StartsWith("UI_")||r.name=="Same world actual mesh edges")continue;
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;var v=mesh.vertices;var triangles=mesh.triangles;var edges=new HashSet<ulong>();
            for(int i=0;i<triangles.Length;i+=3)for(int k=0;k<3;k++)
            {
                int a=triangles[i+k],b=triangles[i+(k+1)%3];int lo=Math.Min(a,b),hi=Math.Max(a,b);ulong key=((ulong)(uint)lo<<32)|(uint)hi;if(!edges.Add(key))continue;
                Vector3 p=root.InverseTransformPoint(r.transform.TransformPoint(v[a])),q=root.InverseTransformPoint(r.transform.TransformPoint(v[b]));var d=q-p;if(d.sqrMagnitude<.001f)continue;
                var side=Vector3.Cross(d.normalized,Vector3.forward);if(side.sqrMagnitude<.01f)side=Vector3.Cross(d.normalized,Vector3.up);side=side.normalized*.0018f;
                g.Quad((p+q)*.5f,d*.5f,side,new Vector4(part%4,0,0,0),new Color(.20f,.75f,1f,.7f));
            }
            part++;
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var source=mats[i];if(source==null||!source.HasProperty("_Color"))continue;
                if(!replacements.TryGetValue(source,out var copy))
                {
                    string baseName=source.name.StartsWith("FinalWorld-")?source.name.Substring("FinalWorld-".Length):source.name;
                    var original=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+baseName+".mat")??source;
                    copy=Mat("FinalWorld-"+baseName,"Standard");copy.CopyPropertiesFromMaterial(original);replacements[source]=copy;originals[copy]=original.color;
                }
                mats[i]=copy;
            }
            r.sharedMaterials=mats;
        }
        run.worldColorMaterials=originals.Keys.ToArray();run.worldOriginalColors=run.worldColorMaterials.Select(m=>originals[m]).ToArray();
        var material=Mat("Same world actual topology","OldWebMap/V2/Depth Copper");material.SetFloat("_Mode",3);material.SetColor("_Tint",Color.white);run.worldTopologyMaterial=material;
        MeshNode("Same world actual mesh edges",root,g,material);
    }
    public static object Build()
    {
        var run=Run();Opening(run);Archives(run);Native(run);Failures(run);Architecture(run);Climax(run);EditorFrame(run);WorldTopology(run);
        run.sourceStatus.fontSize=1.25f;run.finalWord.fontSize=6.5f;
        run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(run);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(run));EditorUtility.SetDirty(run);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        var report=new {success=true,independentArchiveImages=run.archiveTextures.Length,physicalArchiveWindows=288,actualParticleSystems=run.channelParticles.Length,realWindowDepthLayers=6,sourceClipHoldsSeconds=3.84,modelsKeepImportedUnitAxisRoots=true};
        Directory.CreateDirectory(Tmp+"/reports");var json=JsonConvert.SerializeObject(report,Formatting.Indented);File.WriteAllText(Tmp+"/reports/effects-integration.json",json);return json;
    }
    public static object NativePolish()
    {
        var run=Run();Native(run);run.Waiting();run.offlinePreview=false;UdonSharpEditorUtility.CopyProxyToUdon(run);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(run));EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());return new{success=true,readableTextCarrier=true};
    }
}
