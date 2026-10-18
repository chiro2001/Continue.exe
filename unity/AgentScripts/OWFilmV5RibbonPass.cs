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
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWFilmV5RibbonPass
{
    const string Root="Assets/OldWebMap/FullFilm/Revision5";
    const string Scene="Assets/Scenes/OWM_FullFilm_v5.unity";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/fullfilm-v5-ribbon-pass");
    static OWFilmV5Presentation Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=Scene)throw new Exception("Stopped, compiled full-film V5 required");
        return Object.FindObjectsOfType<OWFilmV5Presentation>(true).Single(x=>x.gameObject.scene==SceneManager.GetActiveScene());
    }
    static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    static JObject Data()=>JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-ribbon-flow.json"));
    static string Report(string name,object value)
    {
        Directory.CreateDirectory(Tmp+"/reports");string json=JsonConvert.SerializeObject(value,Formatting.Indented);
        File.WriteAllText(Tmp+"/reports/"+name+".json",json);return json;
    }
    static Material Material(string name,string shader)
    {
        string path=Root+"/Materials/"+name+".mat";var sh=Shader.Find(shader);Need(sh!=null,"Missing shader "+shader);
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(sh){name=name};AssetDatabase.CreateAsset(mat,path);}else mat.shader=sh;
        mat.enableInstancing=true;return mat;
    }
    static void Sync(UdonSharpBehaviour proxy)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(proxy);EditorUtility.SetDirty(proxy);
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);if(vm!=null)EditorUtility.SetDirty(vm);
    }
    static Texture2D DataTexture(string name)
    {
        string path=Root+"/Textures/"+name;var importer=AssetImporter.GetAtPath(path)as TextureImporter;
        Need(importer!=null,"Missing texture "+path);importer.textureType=TextureImporterType.Default;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=false;importer.sRGBTexture=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Mesh GlyphMesh(JObject data)
    {
        string path=Root+"/Meshes/Cumulative detached glyphs.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name="Cumulative detached glyphs"};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        float aw=(float)data["atlas"]["width"],ah=(float)data["atlas"]["height"];
        var vertices=new List<Vector3>();var uv0=new List<Vector2>();var uv1=new List<Vector2>();var uv2=new List<Vector2>();var uv3=new List<Vector2>();var triangles=new List<int>();
        foreach(JToken g in (JArray)data["glyphs"])
        {
            var rect=(JArray)g["rect"];float x=(float)rect[0],y=(float)rect[1],w=(float)rect[2],h=(float)rect[3];int first=vertices.Count;
            for(int j=0;j<4;j++)
            {
                bool right=j>=2,top=j%2==1;float s=(float)g[right?"right":"left"];
                vertices.Add(new Vector3(right?.5f:-.5f,top?.5f:-.5f,0));
                uv0.Add(new Vector2((x+(right?w:0))/aw,1-(y+(top?0:h))/ah));
                uv1.Add(new Vector2(s,top?1:0));uv2.Add(new Vector2((float)g["centre"],(float)g["word_centre"]));
                uv3.Add(new Vector2((bool)g["selected"]?1:0,(float)g["hue"]));
            }
            triangles.AddRange(new[]{first,first+1,first+2,first+2,first+1,first+3});
        }
        mesh.SetVertices(vertices);mesh.SetUVs(0,uv0);mesh.SetUVs(1,uv1);mesh.SetUVs(2,uv2);mesh.SetUVs(3,uv3);mesh.SetTriangles(triangles,0);
        mesh.bounds=new Bounds(new Vector3(0,-1.3f,0),new Vector3(23,16,23));EditorUtility.SetDirty(mesh);return mesh;
    }
    public static object Build()
    {
        var run=Run();var d=run.details;var p=run.provenance;var data=Data();
        var oldMesh=d.textRibbon.GetComponent<MeshFilter>().sharedMesh;
        var dock=d.textRibbon.parent.Find("Waiting and replay ribbon V5");
        if(dock==null)
        {
            dock=new GameObject("Waiting and replay ribbon V5").transform;dock.SetParent(d.textRibbon.parent,false);
            dock.gameObject.AddComponent<MeshFilter>().sharedMesh=oldMesh;
            var renderer=dock.gameObject.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        d.ribbonDock=dock;d.ribbonDockMaterial=Material("Story ribbon waiting and replay","OldWebMap/FilmV5/Story Ribbon");
        dock.GetComponent<Renderer>().sharedMaterial=d.ribbonDockMaterial;
        d.textRibbon.GetComponent<MeshFilter>().sharedMesh=GlyphMesh(data);
        d.textRibbonMaterial=Material("Story ribbon cumulative greetz","OldWebMap/FilmV5/Cumulative Greetz");
        d.textRibbon.GetComponent<Renderer>().sharedMaterial=d.textRibbonMaterial;d.textRibbon.localScale=Vector3.one;
        d.ribbonFlowAtlas=DataTexture("ribbon-flow-atlas.png");d.textRibbonMaterial.mainTexture=d.ribbonFlowAtlas;
        d.textRibbonMaterial.SetVector("_AtlasSize",new Vector4(d.ribbonFlowAtlas.width,d.ribbonFlowAtlas.height,1f/d.ribbonFlowAtlas.width,1f/d.ribbonFlowAtlas.height));
        d.ribbonFlowPixels=((JArray)data["curve_px"]).Select(x=>(float)x).ToArray();d.ribbonFlowStep=(float)data["curve_step"];
        d.ribbonStreamStarts=new float[d.ribbonStates.Length];d.ribbonStreamEnds=new float[d.ribbonStates.Length];
        foreach(JToken cue in (JArray)data["cues"]){int i=(int)cue["index"];d.ribbonStreamStarts[i]=(float)cue["stream_start"];d.ribbonStreamEnds[i]=(float)cue["stream_end"];}
        d.ribbonRadius=(float)data["geometry"]["radius"];d.ribbonBandHeight=(float)data["geometry"]["band_height"];d.ribbonEntryDegrees=(float)data["geometry"]["entry_degrees"];
        d.ribbonStages=new Vector4((float)data["geometry"]["grey_start_turn"],(float)data["geometry"]["grey_flat_turn"],(float)data["geometry"]["drop_start_turn"],(float)data["geometry"]["drop_end_turn"]);
        var story=JObject.Parse(File.ReadAllText("AgentScripts/data/fullfilm-v5-story.json"));
        var rewritten=story["ribbons"][30];d.ribbonStates[30]=DataTexture(Path.GetFileName((string)rewritten["file"]));d.ribbonPixels[30]=(float)rewritten["pixels"];d.ribbonSpeeds[30]=(float)rewritten["speed_px_s"];
        p.mainSequence=new[]{6,4,-1,7,5,-1,9,8,-1,10,9,-1,4,-1};
        p.companionA=new[]{1,6,2,3,0,4,10,9,8,0,12,6,2,4};p.companionB=new[]{4,1,0,5,3,7,3,10,1,12,13,2,6,0};
        d.excludedArt=(d.excludedArt??new GameObject[0]).Concat(new[]{d.gamePlane==null?null:d.gamePlane.gameObject,d.gameDetailPlane==null?null:d.gameDetailPlane.gameObject}).Where(x=>x!=null).Distinct().ToArray();
        run.Waiting();run.offlinePreview=false;Sync(run);Sync(d);Sync(p);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return Report("integration",new{success=true,glyphs=((JArray)data["glyphs"]).Count,vertices=d.textRibbon.GetComponent<MeshFilter>().sharedMesh.vertexCount,
            flowSamples=d.ribbonFlowPixels.Length,entryDegrees=d.ribbonEntryDegrees,stages=new[]{d.ribbonStages.x,d.ribbonStages.y,d.ribbonStages.z,d.ribbonStages.w},
            protocolSpan=new[]{125.44f,130.56f},protocolPosition=new[]{2.4f,2.05f,5.05f},protocolMaxHeight=2.15f,gameTranslation="disabled",branches="disabled"});
    }
    public static object Check()
    {
        var run=Run();var d=run.details;var p=run.provenance;var data=Data();var rows=new List<object>();
        try
        {
            var mesh=d.textRibbon.GetComponent<MeshFilter>().sharedMesh;var triangles=mesh.triangles;
            Need(mesh.vertexCount==((JArray)data["glyphs"]).Count*4,"Glyph cell count mismatch");
            for(int i=0;i<triangles.Length;i+=3)Need(triangles[i]/4==triangles[i+1]/4&&triangles[i]/4==triangles[i+2]/4,"A triangle connects separate glyphs");
            Need(d.ribbonFlowPixels.Zip(d.ribbonFlowPixels.Skip(1),(a,b)=>b>=a).All(x=>x),"Cumulative curve rewinds");
            Need(!ShaderUtil.GetShaderMessages(d.textRibbonMaterial.shader).Any(x=>x.severity.ToString()=="Error"),"Flow shader failed");
            Need(p.mainSequence.All(i=>i!=11)&&p.companionA.All(i=>i!=11)&&p.companionB.All(i=>i!=11),"Eku table repeats in overload");
            run.Waiting();Need(d.ribbonDock.gameObject.activeInHierarchy&&!d.textRibbon.gameObject.activeInHierarchy,"Idle ribbon lifecycle mismatch");
            foreach(float t in new[]{.12f,5.12f,20.46f,20.50f,50f,74.25f,77.5f,95f,120.6f,126f,130.8f,138f,160.5f,183f,190f,213f,226f,250f})
            {
                run.Preview(t);Need(d.textRibbonMaterial.mainTexture==d.ribbonFlowAtlas,"A chapter replaces the cumulative atlas");
                Need(d.ribbonBranches.All(b=>b==null||!b.gameObject.activeInHierarchy),"Duplicated reading ring still active");
                Need(d.gamePlane==null||!d.gamePlane.gameObject.activeInHierarchy,"Ending game translation remains");
                Need(d.gameDetailPlane==null||!d.gameDetailPlane.gameObject.activeInHierarchy,"Ending game detail remains");
                if(t>20&&t<249)Need(d.sampledRibbonHistory>0,"History disappears at a chapter boundary");
                rows.Add(new{time=t,cue=d.sampledRibbonState,scroll=d.sampledRibbonScroll,history=d.sampledRibbonHistory,head=d.sampledRibbonHead,
                    permission=p.contentRoot.activeInHierarchy&&p.cards[1].gameObject.activeInHierarchy&&p.cardMaterials[1].mainTexture==p.plates[11]});
            }
            run.Preview(126);Need(p.cards[1].gameObject.activeInHierarchy&&p.cardMaterials[1].mainTexture==p.plates[11]&&p.cards[1].localPosition.x>2&&p.cards[1].localScale.y<=2.151f,"Eku side display mismatch");
            foreach(float t in new[]{121f,124f,131f,132f}){run.Preview(t);Need(!p.cards.Any(c=>c.gameObject.activeInHierarchy&&c.GetComponent<Renderer>().sharedMaterial.mainTexture==p.plates[11]),"Eku outside its short display span");}
            var selected=((JArray)data["words"]).Where(w=>(bool)w["selected"]).ToArray();
            float minHold=selected.Where(w=>w["phases"]["drop_start"].Type!=JTokenType.Null&&w["phases"]["grey_flat"].Type!=JTokenType.Null)
                .Min(w=>(float)w["phases"]["drop_start"]-(float)w["phases"]["grey_flat"]);
            Need(minHold>3,"Grey hold is too short");
            run.Finished();Need(d.ribbonDock.gameObject.activeInHierarchy&&!d.textRibbon.gameObject.activeInHierarchy&&run.basis.legacy.menuRoot.activeInHierarchy,"Replay lifecycle mismatch");
            return Report("editor-check",new{success=true,glyphs=mesh.vertexCount/4,noTrianglesBetweenGlyphs=true,minGreyHoldSeconds=minHold,rows,scope="Targeted Editor sampling; actual ClientSim checked separately"});
        }
        catch(Exception e){Report("editor-failure",new{success=false,error=e.ToString(),rows});throw;}
        finally{run.Waiting();run.offlinePreview=false;}
    }
}
