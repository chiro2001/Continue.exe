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

// V5 atmosphere builder. Entries: Build() then Validate(); both refuse to run unless the editor is stopped
// and the active scene is exactly Assets/Scenes/OWM_Episode123_v5.unity. Neither saves the scene.
// Needs the OWV5Atmosphere UdonSharp program asset (created by OWEpisodeV5Build.RepairPrograms) and the four
// OWV5 shaders imported. Data: AgentScripts/data/episode123-v5-atmosphere.json (tools/prepare_episode123_v5_atmosphere.py).
public static class OWEpisodeV5Atmosphere
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v5.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision5/Atmosphere";
    const string DataPath="AgentScripts/data/episode123-v5-atmosphere.json";
    const string RootName="V5 Atmosphere",StarfieldName="Revision5 Starfield",FocusName="Revision5 Focus Light";
    static string Tmp=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v5");
    static T F<T>() where T:Object=>Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static void Folder(string p){if(AssetDatabase.IsValidFolder(p))return;int i=p.LastIndexOf('/');Folder(p.Substring(0,i));AssetDatabase.CreateFolder(p.Substring(0,i),p.Substring(i+1));}
    static string Report(string name,object value){Directory.CreateDirectory(Tmp+"/reports");string s=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Tmp+"/reports/"+name,s);return s;}
    static void Guard(){if(EditorApplication.isPlaying||EditorApplication.isCompiling||SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Stopped editor with active scene "+ScenePath+" required");}
    static Color Col(JToken a){return new Color((float)a[0],(float)a[1],(float)a[2],1);}
    static Vector3 Vec(JToken a){return new Vector3((float)a[0],(float)a[1],(float)a[2]);}
    static float[] Floats(JToken a){return a.Select(x=>(float)x).ToArray();}
    static Transform Node(Transform parent,string name){var t=parent.Find(name);if(t!=null)return t;var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static void Identity(Transform t){t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;}
    static void RenderStyle(Renderer r){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static void Show(Transform t,Mesh mesh,Material material)
    {
        var filter=t.GetComponent<MeshFilter>();if(filter==null)filter=t.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
        var renderer=t.GetComponent<MeshRenderer>();if(renderer==null)renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;RenderStyle(renderer);
    }
    sealed class Rng
    {
        uint s;
        public Rng(uint seed){s=seed*2654435761u+0x9E3779B9u;if(s==0)s=1;for(int i=0;i<8;i++)Next();}
        public float Next(){s^=s<<13;s^=s>>17;s^=s<<5;return (s&0xFFFFFFu)/16777216f;}
    }
    static Mesh MeshAsset(string name)
    {
        string path=Root+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();return mesh;
    }
    static Material MaterialAsset(string name,string shaderName)
    {
        string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find(shaderName);
        if(shader==null || !shader.isSupported)throw new InvalidOperationException("V5 shader missing/unsupported (import OWV5*.shader first): "+shaderName);
        if(mat==null){mat=new Material(shader);mat.name=name;AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    // Inside-facing UV sphere: triangles are wound so the inner surface is the front face (shader culls back).
    static Mesh DomeMesh(float radius,int lon,int lat)
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int i=0;i<=lat;i++)
        {
            float theta=Mathf.PI*i/lat,y=Mathf.Cos(theta),r=Mathf.Sin(theta);
            for(int j=0;j<=lon;j++)
            {
                float phi=2*Mathf.PI*j/lon;var d=new Vector3(r*Mathf.Cos(phi),y,r*Mathf.Sin(phi));
                vertices.Add(d*radius);normals.Add(-d);uv.Add(new Vector2((float)j/lon,1f-(float)i/lat));
            }
        }
        for(int i=0;i<lat;i++)for(int j=0;j<lon;j++){int a=i*(lon+1)+j,b=a+lon+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        int outward=CountFacing(vertices,triangles,out int inward);
        // Orient every triangle inward individually; pole fans may be wound opposite to the bands.
        for(int i=0;i<triangles.Count;i+=3){Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]],n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude>=1e-10f&&Vector3.Dot(n,(a+b+c)/3f)>=0){int t=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=t;}}
        var mesh=MeshAsset("V5 sky dome inside sphere");mesh.indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    // Counts triangles whose front face points away from the origin; returns outward count, inward via out.
    static int CountFacing(List<Vector3> v,List<int> t,out int inward)
    {
        int outward=0;inward=0;
        for(int i=0;i<t.Count;i+=3)
        {
            Vector3 a=v[t[i]],b=v[t[i+1]],c=v[t[i+2]],n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-10f)continue;
            if(Vector3.Dot(n,(a+b+c)/3f)<0)inward++;else outward++;
        }
        return outward;
    }
    static Color Palette(float u)
    {
        if(u<.50f)return new Color(.86f,.92f,1f,1);if(u<.68f)return new Color(1f,.92f,.78f,1);if(u<.82f)return new Color(.62f,.90f,1f,1);
        if(u<.92f)return new Color(1f,.68f,.95f,1);if(u<.97f)return new Color(1f,.95f,.6f,1);return Color.white;
    }
    // One quad (4 vertices sharing the centre) per star. uv0 = corner, uv1 = (phase,size), uv2 = (twinkle Hz,brightness), colour = (tint,twinkle depth).
    static Mesh StarMesh(string name,JToken layer)
    {
        int count=(int)layer["count"];var rng=new Rng((uint)(long)layer["seed"]);bool shell=(string)layer["shape"]=="shell";
        float rMin=(float)layer["r_min"],rMax=(float)layer["r_max"],wrap=(float)layer["wrap"];
        var position=new List<Vector3>(count*4);var corner=new List<Vector2>(count*4);var uv1=new List<Vector2>(count*4);var uv2=new List<Vector2>(count*4);var color=new List<Color>(count*4);var triangles=new List<int>(count*6);
        var corners=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};
        for(int s=0;s<count;s++)
        {
            Vector3 c;
            if(shell)
            {
                float z=rng.Next()*2-1,a=rng.Next()*2*Mathf.PI,r=Mathf.Sqrt(1-z*z);c=new Vector3(r*Mathf.Cos(a),z,r*Mathf.Sin(a))*Mathf.Lerp(rMin,rMax,rng.Next());
            }
            else{float h=wrap*.5f;c=new Vector3(Mathf.Lerp(-h,h,rng.Next()),Mathf.Lerp(-h,h,rng.Next()),Mathf.Lerp(-h,h,rng.Next()));}
            float brightness=.22f+.78f*Mathf.Pow(rng.Next(),2.2f),phase=rng.Next(),size=.65f+.85f*brightness+.25f*rng.Next();
            float rate=.12f+.9f*Mathf.Pow(rng.Next(),2f),depth=.15f+.55f*rng.Next();Color tint=Palette(rng.Next());tint.a=depth;
            int first=position.Count;
            for(int k=0;k<4;k++){position.Add(c);corner.Add(corners[k]);uv1.Add(new Vector2(phase,size));uv2.Add(new Vector2(rate,brightness));color.Add(tint);}
            triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
        }
        var mesh=MeshAsset(name);mesh.indexFormat=position.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(position);mesh.SetUVs(0,corner);mesh.SetUVs(1,uv1);mesh.SetUVs(2,uv2);mesh.SetColors(color);mesh.SetTriangles(triangles,0);
        // The vertex shader wraps/repositions stars around the camera, so object bounds must never cull the layer.
        mesh.bounds=new Bounds(Vector3.zero,Vector3.one*4000f);EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh GridMesh(float radius,int segments)
    {
        var vertices=new List<Vector3>{Vector3.zero};var normals=new List<Vector3>{Vector3.up};var uv=new List<Vector2>{new Vector2(.5f,.5f)};var triangles=new List<int>();
        for(int i=0;i<segments;i++)
        {
            float a=2*Mathf.PI*i/segments;vertices.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);normals.Add(Vector3.up);uv.Add(new Vector2(.5f+.5f*Mathf.Cos(a),.5f+.5f*Mathf.Sin(a)));
        }
        for(int i=0;i<segments;i++)triangles.AddRange(new[]{0,1+(i+1)%segments,1+i});
        var mesh=MeshAsset("V5 grid floor disc");mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh HaloMesh()
    {
        var mesh=MeshAsset("V5 focus halo quad");
        mesh.SetVertices(new List<Vector3>{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)});
        mesh.SetNormals(new List<Vector3>{Vector3.back,Vector3.back,Vector3.back,Vector3.back});
        mesh.SetUVs(0,new List<Vector2>{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)});
        mesh.SetTriangles(new List<int>{0,2,1,2,3,1},0);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static void ConfigureStars(Material m,JToken layer)
    {
        var fade=Floats(layer["fade"]);
        m.SetFloat("_Wrap",(float)layer["wrap"]);m.SetVector("_Fade",new Vector4(fade[0],fade[1],fade[2],fade[3]));m.SetFloat("_AngSize",(float)layer["ang_size"]);
        m.SetFloat("_Trail",(float)layer["trail"]);m.SetFloat("_StreakMax",Mathf.Max(.01f,(float)layer["streak_max"]));m.SetFloat("_Boost",(float)layer["boost"]);m.SetFloat("_SpeedRef",20f);
        m.SetFloat("_Twinkle",(float)layer["twinkle"]);m.SetFloat("_BelowFloor",(float)layer["below_floor"]);m.SetFloat("_FloorY",-.02f);m.SetFloat("_MinPixels",1f);
        m.SetFloat("_Timeline",0);m.SetFloat("_Intensity",(float)layer["intensity_waiting"]);m.SetColor("_Tint",Color.white);m.SetVector("_Velocity",Vector4.zero);EditorUtility.SetDirty(m);
    }
    public static object Build()
    {
        Guard();var data=JObject.Parse(File.ReadAllText(DataPath));
        var owner=GameObject.Find("OWEpisode123");if(owner==null)throw new InvalidOperationException("OWEpisode123 missing");
        var director=F<OWEpisodeDirector>();var journey=F<OWV4Journey>();var montage=F<OWV4Montage>();
        if(director==null || journey==null || journey.worldFrame==null || montage==null)throw new InvalidOperationException("V4 director, journey (worldFrame) and montage required");
        var keyLight=owner.transform.Find("Stable actor key")?.GetComponent<Light>();var fillLight=owner.transform.Find("Stable actor fill")?.GetComponent<Light>();
        if(keyLight==null || fillLight==null)throw new InvalidOperationException("Stable actor key/fill lights missing");
        Folder(Root+"/Meshes");Folder(Root+"/Materials");
        var sky=data["sky"];var grid=data["grid"];var halo=data["halo"];var screen=data["lights"]["screen"];var layers=(JArray)data["stars"];
        // Meshes
        var domeMesh=DomeMesh((float)sky["radius"],(int)sky["lon_segments"],(int)sky["lat_segments"]);
        var starMeshes=layers.Select(l=>StarMesh("V5 stars "+(string)l["name"],l)).ToArray();
        var gridMesh=GridMesh((float)grid["radius"],(int)grid["segments"]);var haloMesh=HaloMesh();
        // Materials (property defaults; Udon overwrites the animated ones every sample)
        var skyMat=MaterialAsset("V5 Sky","OldWebMap/V5/Sky");skyMat.SetFloat("_Curve",(float)sky["curve"]);skyMat.SetFloat("_HazeGain",(float)sky["haze_gain"]);skyMat.SetFloat("_HazeFalloff",(float)sky["haze_falloff"]);skyMat.SetFloat("_Dither",(float)sky["dither"]);
        skyMat.SetColor("_TopColor",Col(sky["waiting"]["top"]));skyMat.SetColor("_HorizonColor",Col(sky["waiting"]["horizon"]));
        var starMats=layers.Select(l=>{var m=MaterialAsset("V5 Stars "+(string)l["name"],"OldWebMap/V5/Starfield");ConfigureStars(m,l);return m;}).ToArray();
        var gridMat=MaterialAsset("V5 Grid floor","OldWebMap/V5/Grid Floor");
        gridMat.SetFloat("_Cell",(float)grid["cell"]);gridMat.SetFloat("_Major",(float)grid["major"]);gridMat.SetFloat("_FadeStart",(float)grid["fade_start"]);gridMat.SetFloat("_FadeEnd",(float)grid["fade_end"]);
        gridMat.SetFloat("_LineAlpha",(float)grid["line_alpha"]);gridMat.SetFloat("_MajorAlpha",(float)grid["major_alpha"]);gridMat.SetFloat("_FillAlpha",(float)grid["fill_alpha"]);gridMat.SetFloat("_Shimmer",(float)grid["shimmer"]);
        gridMat.SetFloat("_PoolRadius",(float)screen["pool_radius"]);gridMat.SetColor("_PoolColor",new Color(0,0,0,0));gridMat.SetFloat("_Opacity",(float)grid["waiting"]);
        var haloMat=MaterialAsset("V5 Focus halo","OldWebMap/V5/Focus Halo");haloMat.SetFloat("_Inner",1f/(float)halo["scale"]);haloMat.SetFloat("_Power",(float)halo["power"]);haloMat.SetFloat("_Intensity",0);
        foreach(var m in new[]{skyMat,gridMat,haloMat}.Concat(starMats))EditorUtility.SetDirty(m);
        // Scene objects: always-active root outside the world frame, stable layers inside it.
        var root=Node(owner.transform,RootName);root.gameObject.SetActive(true);Identity(root);
        var dome=Node(root,"V5 sky dome");Identity(dome);Show(dome,domeMesh,skyMat);
        var far=Node(root,"V5 stars far");Identity(far);Show(far,starMeshes[0],starMats[0]);
        var floor=Node(root,"V5 grid floor");Identity(floor);floor.localPosition=new Vector3(0,(float)grid["y"],0);Show(floor,gridMesh,gridMat);
        var clock=Node(root,"V5 atmosphere clock");Identity(clock);
        var frame=journey.worldFrame;
        var starfield=Node(frame,StarfieldName);Identity(starfield);starfield.gameObject.SetActive(true);
        var mid=Node(starfield,"V5 stars mid");Identity(mid);Show(mid,starMeshes[1],starMats[1]);
        var near=Node(starfield,"V5 stars near");Identity(near);Show(near,starMeshes[2],starMats[2]);
        var focus=Node(frame,FocusName);Identity(focus);focus.gameObject.SetActive(true);
        var haloT=Node(focus,"V5 focus halo");Identity(haloT);Show(haloT,haloMesh,haloMat);haloT.gameObject.SetActive(false);
        var lightT=Node(focus,"V5 focus screen light");Identity(lightT);var screenLight=lightT.GetComponent<Light>();if(screenLight==null)screenLight=lightT.gameObject.AddComponent<Light>();
        screenLight.type=LightType.Point;screenLight.range=(float)screen["range"];screenLight.intensity=0;screenLight.color=Color.white;screenLight.shadows=LightShadows.None;screenLight.renderMode=LightRenderMode.Auto;screenLight.lightmapBakeType=LightmapBakeType.Realtime;lightT.gameObject.SetActive(false);
        // Udon component
        var atm=F<OWV5Atmosphere>();if(atm==null)atm=clock.gameObject.AddUdonSharpComponent<OWV5Atmosphere>();
        atm.director=director;atm.journey=journey;atm.montage=montage;atm.frameRoot=frame.parent;atm.autoSample=true;
        atm.skyMaterial=skyMat;atm.farStarsMaterial=starMats[0];atm.midStarsMaterial=starMats[1];atm.nearStarsMaterial=starMats[2];atm.gridMaterial=gridMat;atm.haloMaterial=haloMat;
        atm.haloTransform=haloT;atm.keyLight=keyLight;atm.fillLight=fillLight;atm.screenLight=screenLight;
        atm.duration=(float)data["duration"];atm.transition=(float)data["transition"];atm.intro=(float)data["intro"];atm.outro=(float)data["outro"];atm.referenceEye=Vec(data["reference_eye"]);atm.groundY=(float)grid["y"];
        var skyKeys=(JArray)sky["keys"];var keyKeys=(JArray)data["lights"]["key"]["keys"];var fillKeys=(JArray)data["lights"]["fill"]["keys"];
        if(skyKeys.Count!=keyKeys.Count || skyKeys.Count!=fillKeys.Count)throw new InvalidOperationException("Sky and light scripts need the same keys");
        atm.scriptTimes=skyKeys.Select(k=>(float)k["t"]).ToArray();atm.skyTop=skyKeys.Select(k=>Col(k["top"])).ToArray();atm.skyHorizon=skyKeys.Select(k=>Col(k["horizon"])).ToArray();
        atm.keyColors=keyKeys.Select(k=>Col(k["color"])).ToArray();atm.keyIntensities=keyKeys.Select(k=>(float)k["intensity"]).ToArray();atm.fillColors=fillKeys.Select(k=>Col(k["color"])).ToArray();atm.fillIntensities=fillKeys.Select(k=>(float)k["intensity"]).ToArray();
        atm.waitingTop=Col(sky["waiting"]["top"]);atm.waitingHorizon=Col(sky["waiting"]["horizon"]);
        atm.v4KeyColor=Col(data["lights"]["key"]["waiting"]["color"]);atm.v4KeyIntensity=(float)data["lights"]["key"]["waiting"]["intensity"];atm.v4FillColor=Col(data["lights"]["fill"]["waiting"]["color"]);atm.v4FillIntensity=(float)data["lights"]["fill"]["waiting"]["intensity"];
        atm.groundDarken=(float)sky["ground_darken"];atm.starTintMix=(float)layers[0]["tint_mix"];atm.farWaiting=(float)layers[0]["intensity_waiting"];atm.farShowing=(float)layers[0]["intensity_showing"];atm.midShowing=(float)layers[1]["intensity_showing"];atm.nearShowing=(float)layers[2]["intensity_showing"];
        atm.gridTimes=((JArray)grid["keys"]).Select(k=>(float)k[0]).ToArray();atm.gridValues=((JArray)grid["keys"]).Select(k=>(float)k[1]).ToArray();atm.gridWaiting=(float)grid["waiting"];
        var journeys=(JArray)data["journey"];atm.journeyStart=journeys.Select(j=>(float)j["start"]).ToArray();atm.journeyDuration=journeys.Select(j=>(float)j["duration"]).ToArray();atm.journeyDistance=journeys.Select(j=>(float)j["distance"]).ToArray();
        atm.haloScale=(float)halo["scale"];atm.haloBack=(float)halo["back_offset"];atm.haloPeak=(float)halo["peak"];atm.haloMinBottom=(float)halo["min_bottom_y"];
        atm.lightPeak=(float)screen["peak"];atm.lightRange=(float)screen["range"];atm.lightTowardEye=(float)screen["toward_eye"];atm.poolRadius=(float)screen["pool_radius"];atm.poolGain=(float)screen["pool_gain"];
        // Focus events: match the montage by index, confirmed by start time (re-run the prepare tool if the score changed).
        var focusRows=((JArray)data["focus"]).OrderBy(f=>(float)f["start"]).ToArray();
        var montageFocus=Enumerable.Range(0,montage.roles.Length).Where(e=>montage.roles[e]==1).OrderBy(e=>montage.starts[e]).ToArray();
        if(montageFocus.Length!=focusRows.Length)throw new InvalidOperationException("Montage has "+montageFocus.Length+" focus events, data has "+focusRows.Length+"; re-run tools/prepare_episode123_v5_atmosphere.py");
        var matched=new int[focusRows.Length];
        for(int i=0;i<focusRows.Length;i++)
        {
            int e=(int)focusRows[i]["event"];
            if(e<0 || e>=montage.roles.Length || montage.roles[e]!=1 || Mathf.Abs(montage.starts[e]-(float)focusRows[i]["start"])>.011f)e=montageFocus[i];
            if(Mathf.Abs(montage.starts[e]-(float)focusRows[i]["start"])>.011f)throw new InvalidOperationException("Focus event "+i+" start differs from the montage: "+montage.starts[e]+" vs "+(float)focusRows[i]["start"]);
            matched[i]=e;
        }
        atm.focusEvents=matched;atm.focusColors=focusRows.Select(f=>Col(f["color"])).ToArray();
        atm.Waiting();ResetClock(atm);
        EditorUtility.SetDirty(atm);UdonSharpEditorUtility.CopyProxyToUdon(atm);var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(atm);if(vm!=null)EditorUtility.SetDirty(vm);
        foreach(var m in new[]{skyMat,gridMat,haloMat}.Concat(starMats))EditorUtility.SetDirty(m);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return Report("atmosphere-build.json",new{success=true,scene=ScenePath,root=RootName,meshes=new[]{domeMesh,starMeshes[0],starMeshes[1],starMeshes[2],gridMesh,haloMesh}.Select(o=>AssetDatabase.GetAssetPath(o)).ToArray(),materials=new[]{skyMat,starMats[0],starMats[1],starMats[2],gridMat,haloMat}.Select(o=>AssetDatabase.GetAssetPath(o)).ToArray(),
            starsPerLayer=layers.Select(l=>(int)l["count"]).ToArray(),focusEvents=matched,keyLight=HierarchyPath(keyLight.transform),fillLight=HierarchyPath(fillLight.transform),screenLight=HierarchyPath(lightT),halo=HierarchyPath(haloT),starfield=HierarchyPath(starfield),colorSpace=PlayerSettings.colorSpace.ToString(),sceneSaved=false});
    }
    // Waiting() animates with Time.time; store a neutral clock in the persisted materials.
    static void ResetClock(OWV5Atmosphere a){foreach(var m in new[]{a.farStarsMaterial,a.midStarsMaterial,a.nearStarsMaterial,a.gridMaterial,a.haloMaterial})if(m!=null){m.SetFloat("_Timeline",0);EditorUtility.SetDirty(m);}}
    static string HierarchyPath(Transform t){var parts=new List<string>();for(var c=t;c!=null;c=c.parent)parts.Add(c.name);parts.Reverse();return string.Join("/",parts);}
    // ---- Validation -------------------------------------------------------------------------------------------
    sealed class Ctx
    {
        public readonly List<string> failures=new List<string>();public readonly Dictionary<string,object> evidence=new Dictionary<string,object>();public int checks;
        public void Check(bool ok,string name,object detail=null){checks++;if(!ok)failures.Add(name+(detail!=null?" "+JsonConvert.SerializeObject(detail):""));}
    }
    static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3f-2f*x);}
    static float Lived(float t,float duration,float intro,float outro){return Ease(t/intro)*(1f-Ease((t-(duration-outro))/outro));}
    static Color Mix(Color a,Color b,float t){return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,1);}
    static float Delta(Color a,Color b){return Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));}
    static float[] Rgb(Color c){return new[]{c.r,c.g,c.b};}
    static int KeyAt(JArray keys,float t){int k=0;for(int i=1;i<keys.Count;i++)if(t>=(float)keys[i]["t"])k=i;return k;}
    // Independent reference of the colour script: smoothstep from the previous key over `transition` seconds starting at each key.
    static Color ScriptColor(JArray keys,string prop,float t,float transition)
    {
        int k=KeyAt(keys,t);Color c=Col(keys[k][prop]);return k>0?Mix(Col(keys[k-1][prop]),c,Ease((t-(float)keys[k]["t"])/transition)):c;
    }
    static float ScriptValue(JArray keys,string prop,float t,float transition)
    {
        int k=KeyAt(keys,t);float c=(float)keys[k][prop];return k>0?Mathf.Lerp((float)keys[k-1][prop],c,Ease((t-(float)keys[k]["t"])/transition)):c;
    }
    static float GridExpected(JArray keys,float t)
    {
        int g=0;for(int i=1;i<keys.Count;i++)if(t>=(float)keys[i][0])g=i;
        if(g>=keys.Count-1)return (float)keys[keys.Count-1][1];
        float t0=(float)keys[g][0],t1=(float)keys[g+1][0];return Mathf.Lerp((float)keys[g][1],(float)keys[g+1][1],Ease((t-t0)/(t1-t0)));
    }
    public static object Validate()
    {
        Guard();var data=JObject.Parse(File.ReadAllText(DataPath));
        var atm=F<OWV5Atmosphere>();if(atm==null)throw new InvalidOperationException("Run OWEpisodeV5Atmosphere.Build first");
        var journey=atm.journey;var montage=atm.montage;var director=atm.director;var ctx=new Ctx();
        var sky=data["sky"];var grid=data["grid"];var halo=data["halo"];var screen=data["lights"]["screen"];var layers=(JArray)data["stars"];
        float duration=(float)data["duration"],transition=(float)data["transition"],intro=(float)data["intro"],outro=(float)data["outro"];
        var skyKeys=(JArray)sky["keys"];var keyKeys=(JArray)data["lights"]["key"]["keys"];var fillKeys=(JArray)data["lights"]["fill"]["keys"];var gridKeys=(JArray)grid["keys"];
        Color waitingTop=Col(sky["waiting"]["top"]),waitingHorizon=Col(sky["waiting"]["horizon"]),v4Key=Col(data["lights"]["key"]["waiting"]["color"]),v4Fill=Col(data["lights"]["fill"]["waiting"]["color"]);
        float v4KeyI=(float)data["lights"]["key"]["waiting"]["intensity"],v4FillI=(float)data["lights"]["fill"]["waiting"]["intensity"];
        // 1. Scene objects and persisted assets
        var rootT=GameObject.Find("OWEpisode123")?.transform.Find(RootName);
        if(rootT==null || journey==null || journey.worldFrame==null)throw new InvalidOperationException("Run OWEpisodeV5Atmosphere.Build first");
        ctx.Check(rootT.gameObject.activeSelf && rootT.parent.name=="OWEpisode123" && !rootT.IsChildOf(journey.worldFrame),"V5 Atmosphere root always-active and outside worldFrame");
        var dome=rootT.Find("V5 sky dome");var far=rootT.Find("V5 stars far");var floor=rootT.Find("V5 grid floor");
        var starfield=journey.worldFrame.Find(StarfieldName);var mid=starfield!=null?starfield.Find("V5 stars mid"):null;var near=starfield!=null?starfield.Find("V5 stars near"):null;
        var haloT=atm.haloTransform;var screenLight=atm.screenLight;
        if(dome==null || far==null || floor==null || mid==null || near==null || haloT==null || screenLight==null)throw new InvalidOperationException("V5 scene objects missing; run Build");
        ctx.Check(mid.IsChildOf(journey.worldFrame) && near.IsChildOf(journey.worldFrame) && haloT.IsChildOf(journey.worldFrame) && screenLight.transform.IsChildOf(journey.worldFrame) && !far.IsChildOf(journey.worldFrame) && !floor.IsChildOf(journey.worldFrame) && !dome.IsChildOf(journey.worldFrame),"mid/near stars, halo and light travel with the frame; dome, far stars and grid do not");
        ctx.Check(Mathf.Abs(floor.position.y-(float)grid["y"])<1e-4f && dome.position.magnitude<1e-4f && far.position.magnitude<1e-4f,"grid height and dome centre",new{gridY=floor.position.y,dome=dome.position.magnitude});
        var materials=new[]{atm.skyMaterial,atm.farStarsMaterial,atm.midStarsMaterial,atm.nearStarsMaterial,atm.gridMaterial,atm.haloMaterial};
        foreach(var material in materials)ctx.Check(material!=null && AssetDatabase.GetAssetPath(material).StartsWith(Root+"/Materials/"),"material persisted",material==null?"null":AssetDatabase.GetAssetPath(material));
        var meshes=new[]{dome,far,mid,near,floor,haloT}.Select(t=>t.GetComponent<MeshFilter>().sharedMesh).ToArray();
        foreach(var mesh in meshes)ctx.Check(mesh!=null && AssetDatabase.GetAssetPath(mesh).StartsWith(Root+"/Meshes/"),"mesh persisted",mesh==null?"null":AssetDatabase.GetAssetPath(mesh));
        ctx.Check(dome.GetComponent<MeshRenderer>().sharedMaterial==atm.skyMaterial && far.GetComponent<MeshRenderer>().sharedMaterial==atm.farStarsMaterial && mid.GetComponent<MeshRenderer>().sharedMaterial==atm.midStarsMaterial && near.GetComponent<MeshRenderer>().sharedMaterial==atm.nearStarsMaterial && floor.GetComponent<MeshRenderer>().sharedMaterial==atm.gridMaterial && haloT.GetComponent<MeshRenderer>().sharedMaterial==atm.haloMaterial,"renderers use the persisted materials");
        ctx.Check(screenLight.shadows==LightShadows.None && screenLight.type==LightType.Point && Mathf.Abs(screenLight.range-(float)screen["range"])<1e-3f,"screen light is a shadowless point light",screenLight.range);
        // 2. Meshes: star counts and ranges, dome, grid disc
        var domeMesh=meshes[0];var gridMesh=meshes[4];
        for(int layerIndex=0;layerIndex<3;layerIndex++)
        {
            var mesh=meshes[1+layerIndex];var layer=layers[layerIndex];string name=(string)layer["name"];int count=(int)layer["count"];var v=new List<Vector3>();mesh.GetVertices(v);
            ctx.Check(v.Count==count*4 && mesh.GetIndexCount(0)==count*6 && count==2800,"star count "+name,new{vertices=v.Count,stars=v.Count/4});
            var centres=Enumerable.Range(0,v.Count/4).Select(i=>v[i*4]).ToArray();
            ctx.Check(Enumerable.Range(0,v.Count/4).All(i=>v[i*4+1]==v[i*4] && v[i*4+2]==v[i*4] && v[i*4+3]==v[i*4]),"star quad vertices share one centre "+name);
            float rMin=(float)layer["r_min"],rMax=(float)layer["r_max"],wrap=(float)layer["wrap"];
            if((string)layer["shape"]=="shell")
            {
                float lo=centres.Min(p=>p.magnitude),hi=centres.Max(p=>p.magnitude);ctx.Check(lo>=rMin-.01f && hi<=rMax+.01f,"far shell radius "+rMin+"-"+rMax,new{lo,hi});ctx.Check(centres.Count(p=>p.y<0)>count*.4f,"far stars also exist below the horizon");ctx.evidence["far_radius"]=new[]{lo,hi};
            }
            else
            {
                float extent=centres.Max(p=>Mathf.Max(Mathf.Abs(p.x),Mathf.Max(Mathf.Abs(p.y),Mathf.Abs(p.z))));
                ctx.Check(extent<=wrap*.5f+.01f && (float)layer["fade"][3]<=wrap*.5f+1e-3f && rMin>=10f-1e-3f,"wrap cube "+name,extent);
            }
            var colors=new List<Color>();mesh.GetColors(colors);var uv2=new List<Vector2>();mesh.GetUVs(2,uv2);
            ctx.Check(colors.Count==v.Count && uv2.Count==v.Count && colors.All(p=>p.a>.1f && p.a<.75f) && uv2.All(p=>p.y>=.21f && p.y<=1.001f),"star attributes "+name);
        }
        float farClip=Camera.main!=null?Camera.main.farClipPlane:(float)sky["far_clip"];var dv=new List<Vector3>();domeMesh.GetVertices(dv);float radius=dv.Max(p=>p.magnitude);
        ctx.evidence["dome_radius"]=radius;ctx.evidence["camera_far_clip"]=farClip;
        ctx.Check(radius<farClip-5f && Mathf.Abs(radius-(float)sky["radius"])<.01f && (float)layers[0]["r_max"]<radius,"dome radius below the camera far clip and outside the far stars",new{radius,farClip});
        var dt=new List<int>();domeMesh.GetTriangles(dt,0);int inward;int outward=CountFacing(dv,dt,out inward);ctx.Check(outward==0 && inward>1000,"dome front faces point inward",new{inward,outward});
        var gv=new List<Vector3>();gridMesh.GetVertices(gv);var gt=new List<int>();gridMesh.GetTriangles(gt,0);bool facingUp=true;
        for(int i=0;i<gt.Count;i+=3)if(Vector3.Cross(gv[gt[i+1]]-gv[gt[i]],gv[gt[i+2]]-gv[gt[i]]).y<=0)facingUp=false;
        ctx.Check(facingUp && Mathf.Abs(gv.Max(p=>p.magnitude)-(float)grid["radius"])<.01f,"grid disc radius and facing",gv.Max(p=>p.magnitude));
        // 3. Shaders
        var shaderRows=new List<object>();
        foreach(string shaderName in new[]{"OldWebMap/V5/Sky","OldWebMap/V5/Starfield","OldWebMap/V5/Grid Floor","OldWebMap/V5/Focus Halo"})
        {
            var shader=Shader.Find(shaderName);var errors=shader==null?new[]{"missing"}:ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Select(m=>m.message+" line "+m.line).ToArray();
            ctx.Check(shader!=null && shader.isSupported && errors.Length==0,"shader compiles: "+shaderName,errors);shaderRows.Add(new{shaderName,errors});
        }
        // 4. Colour script (sky + directional lights) at every key (arrived) and in mid-transition
        var colourRows=new List<object>();
        for(int k=0;k<skyKeys.Count;k++)
        {
            float key=(float)skyKeys[k]["t"];
            foreach(float t in new[]{key+transition*.25f,key+transition*.5f,key+transition+.02f})
            {
                if(t<intro+.02f)continue;
                atm.Sample(t);float lived=Lived(t,duration,intro,outro);
                Color top=Mix(waitingTop,ScriptColor(skyKeys,"top",t,transition),lived),hor=Mix(waitingHorizon,ScriptColor(skyKeys,"horizon",t,transition),lived);
                Color kc=Mix(v4Key,ScriptColor(keyKeys,"color",t,transition),lived),fc=Mix(v4Fill,ScriptColor(fillKeys,"color",t,transition),lived);
                float ki=Mathf.Lerp(v4KeyI,ScriptValue(keyKeys,"intensity",t,transition),lived),fi=Mathf.Lerp(v4FillI,ScriptValue(fillKeys,"intensity",t,transition),lived);
                Color gotTop=atm.skyMaterial.GetColor("_TopColor"),gotHor=atm.skyMaterial.GetColor("_HorizonColor");
                bool ok=Delta(gotTop,top)<2e-3f && Delta(gotHor,hor)<2e-3f && Delta(atm.keyLight.color,kc)<2e-3f && Mathf.Abs(atm.keyLight.intensity-ki)<2e-3f && Delta(atm.fillLight.color,fc)<2e-3f && Mathf.Abs(atm.fillLight.intensity-fi)<2e-3f;
                Color ground=atm.skyMaterial.GetColor("_GroundColor");ok&=Delta(ground,new Color(gotHor.r*atm.groundDarken,gotHor.g*atm.groundDarken,gotHor.b*atm.groundDarken,1))<2e-3f;
                if(t>=key+transition)ok&=Delta(gotTop,Mix(waitingTop,Col(skyKeys[k]["top"]),lived))<2e-3f && Delta(gotHor,Mix(waitingHorizon,Col(skyKeys[k]["horizon"]),lived))<2e-3f;
                ctx.Check(ok,"colour script key "+k+" t="+t,new{top=Rgb(gotTop),expectedTop=Rgb(top),horizon=Rgb(gotHor),expectedHorizon=Rgb(hor)});
                colourRows.Add(new{key=k,t,top=Rgb(gotTop),horizon=Rgb(gotHor),keyLight=Rgb(atm.keyLight.color),keyIntensity=atm.keyLight.intensity,fillLight=Rgb(atm.fillLight.color),fillIntensity=atm.fillLight.intensity});
            }
        }
        ctx.evidence["colour_script"]=colourRows;
        atm.Sample(.64f);ctx.Check(Delta(atm.skyMaterial.GetColor("_TopColor"),Mix(waitingTop,Col(skyKeys[0]["top"]),.5f))<2e-3f,"intro blends the waiting sky into the CRT night key");
        // 5. Grid floor opacity
        var gridRows=new List<object>();
        foreach(float t in new[]{.64f,1.4f,15f,30.72f,35f,38.4f,42f,46.08f,60f,76.8f,78.08f,79.36f,90f,102.4f,103.68f,104.96f,120f,131f,133f})
        {
            atm.Sample(t);float expected=Mathf.Lerp((float)grid["waiting"],GridExpected(gridKeys,t),Lived(t,duration,intro,outro)),got=atm.gridMaterial.GetFloat("_Opacity");
            ctx.Check(Mathf.Abs(got-expected)<2e-3f && Mathf.Abs(atm.sampledGrid-got)<1e-4f,"grid opacity t="+t,new{got,expected});gridRows.Add(new{t,got,expected});
        }
        foreach(var pair in new[]{new[]{0f,0f},new[]{30.72f,0f},new[]{46.08f,1f},new[]{79.36f,.35f},new[]{90f,.35f},new[]{102.4f,.35f},new[]{120f,1f},new[]{133.12f,1f}})ctx.Check(Mathf.Abs(GridExpected(gridKeys,pair[0])-pair[1])<1e-4f,"grid key value t="+pair[0],pair[1]);
        ctx.evidence["grid_opacity"]=gridRows;
        // 6. Waiting / finished states (exact V4 light values, near-black sky, dim far stars, no focus light)
        var stateRows=new List<object>();
        for(int s=0;s<3;s++)
        {
            if(s==0)atm.Waiting();else if(s==1)atm.Finished();else atm.Sample(0);
            Color top=atm.skyMaterial.GetColor("_TopColor"),hor=atm.skyMaterial.GetColor("_HorizonColor");
            bool ok=Delta(top,waitingTop)<1e-3f && Delta(hor,waitingHorizon)<1e-3f && Delta(atm.keyLight.color,v4Key)<1e-3f && Mathf.Abs(atm.keyLight.intensity-v4KeyI)<1e-4f && Delta(atm.fillLight.color,v4Fill)<1e-3f && Mathf.Abs(atm.fillLight.intensity-v4FillI)<1e-4f;
            ok&=Mathf.Abs(atm.gridMaterial.GetFloat("_Opacity")-(float)grid["waiting"])<1e-4f && Mathf.Abs(atm.farStarsMaterial.GetFloat("_Intensity")-(float)layers[0]["intensity_waiting"])<1e-4f && atm.midStarsMaterial.GetFloat("_Intensity")<1e-4f && atm.nearStarsMaterial.GetFloat("_Intensity")<1e-4f;
            ok&=!haloT.gameObject.activeSelf && !screenLight.gameObject.activeSelf && atm.sampledState==(s==1?2:0);
            ctx.Check(ok,"state "+(s==0?"Waiting()":s==1?"Finished()":"Sample(0)"),new{top=Rgb(top),horizon=Rgb(hor),state=atm.sampledState});
            stateRows.Add(new{call=s==0?"Waiting":s==1?"Finished":"Sample(0)",state=atm.sampledState,top=Rgb(top),horizon=Rgb(hor),keyLight=Rgb(atm.keyLight.color),keyIntensity=atm.keyLight.intensity,fillLight=Rgb(atm.fillLight.color),fillIntensity=atm.fillLight.intensity,farStars=atm.farStarsMaterial.GetFloat("_Intensity"),grid=atm.gridMaterial.GetFloat("_Opacity")});
        }
        atm.Sample(duration);ctx.Check(atm.sampledState==2,"Sample(duration) is finished",atm.sampledState);
        ctx.evidence["states"]=stateRows;
        // 7. Journey velocity: analytic table versus finite difference of the real VirtualPosition
        var velocityRows=new List<object>();
        foreach(float t in new[]{50f,79.36f,79.9f,80.64f,81.5f,81.92f,86.9f,87.36f,88f,103.1f,103.68f,104.5f,110f})
        {
            atm.Sample(t);float step=.002f,numeric=(journey.VirtualPosition(t+step).z-journey.VirtualPosition(t-step).z)/(2*step);Vector4 midV=atm.midStarsMaterial.GetVector("_Velocity"),nearV=atm.nearStarsMaterial.GetVector("_Velocity");
            ctx.Check(Mathf.Abs(midV.z-numeric)<.03f && Mathf.Abs(nearV.z-numeric)<.03f && Mathf.Abs(atm.sampledSpeed-numeric)<.03f && Mathf.Abs(midV.w-Mathf.Abs(numeric))<.03f && Mathf.Abs(midV.x)+Mathf.Abs(midV.y)<1e-6f,"star velocity t="+t,new{analytic=midV.z,numeric});velocityRows.Add(new{t,analytic=midV.z,numeric});
        }
        ctx.evidence["velocity"]=velocityRows;
        // 8. Focus halo and screen light for every focus event (enter, hold, exit) and in the gaps
        var focusRows=new List<object>();float minBottom=99,maxHaloError=0,maxLightError=0,bandY=(float)halo["band_y"];Vector3 worldEye=atm.referenceEye;
        try
        {
            for(int i=0;i<atm.focusEvents.Length;i++)
            {
                int e=atm.focusEvents[i];int slot=Enumerable.Range(0,montage.windows.Length).First(w=>e>=montage.slotFirst[w] && e<montage.slotFirst[w]+montage.slotCount[w]);
                float enter=Mathf.Max(.05f,montage.enters[e]),exit=Mathf.Max(.05f,montage.exits[e]),hold=montage.ends[e]-montage.starts[e]-enter-exit;
                var times=new[]{montage.starts[e]+enter*.5f,montage.starts[e]+enter+hold*.5f,montage.ends[e]-exit*.5f};
                for(int phase=0;phase<3;phase++)
                {
                    float t=times[phase];journey.Sample(t);montage.Seek(t);atm.Sample(t);var window=montage.windows[slot];var plane=montage.imagePlanes[slot];
                    Vector3 expected=plane.position+plane.forward*(float)halo["back_offset"];float haloError=Vector3.Distance(haloT.position,expected);maxHaloError=Mathf.Max(maxHaloError,haloError);
                    float angle=Quaternion.Angle(haloT.rotation,plane.rotation),sx=haloT.lossyScale.x/(plane.lossyScale.x*(float)halo["scale"]),sy=haloT.lossyScale.y/(plane.lossyScale.y*(float)halo["scale"]);
                    float bottom=haloT.position.y-haloT.lossyScale.y*.5f;minBottom=Mathf.Min(minBottom,bottom);
                    float lightDistance=Vector3.Distance(screenLight.transform.position,window.position),lightError=Mathf.Abs(lightDistance-(float)screen["toward_eye"]);if(phase==1)maxLightError=Mathf.Max(maxLightError,lightError);
                    float towardEye=Vector3.Dot((worldEye-window.position).normalized,(screenLight.transform.position-window.position).normalized);
                    Vector4 pool=atm.gridMaterial.GetVector("_PoolPos");float poolError=Vector2.Distance(new Vector2(pool.x,pool.z),new Vector2(screenLight.transform.position.x,screenLight.transform.position.z));
                    float haloI=atm.haloMaterial.GetFloat("_Intensity");Color glow=atm.haloMaterial.GetColor("_Color");
                    bool ok=window.gameObject.activeSelf && haloT.gameObject.activeSelf && haloError<.1f && angle<1f && Mathf.Abs(sx-1)<.01f && Mathf.Abs(sy-1)<.01f && bottom>=bandY && screenLight.gameObject.activeSelf && lightError<.01f && towardEye>.9995f && poolError<.01f;
                    ok&=phase==1?haloI>=(float)halo["peak"]*.95f && Mathf.Abs(screenLight.intensity-(float)screen["peak"])<.02f:haloI>.05f && screenLight.intensity>.05f;
                    ok&=Delta(glow,atm.focusColors[i])<1e-4f && Delta(screenLight.color,atm.focusColors[i])<1e-4f && atm.sampledFocus==i;
                    ctx.Check(ok,"focus "+i+" event "+e+" phase "+phase+" t="+t,new{haloError,angle,sx,sy,bottom,lightError,towardEye,poolError,haloIntensity=haloI,lightIntensity=screenLight.intensity});
                    if(phase==1)focusRows.Add(new{focus=i,eventIndex=e,t,haloErrorM=haloError,haloBottomY=bottom,haloIntensity=haloI,lightIntensity=screenLight.intensity,lightDistanceM=lightDistance,poolErrorM=poolError,color=Rgb(glow)});
                }
            }
            var order=atm.focusEvents.OrderBy(e=>montage.starts[e]).ToArray();int gaps=0;
            for(int i=0;i+1<order.Length;i++)
            {
                float a=montage.ends[order[i]],b=montage.starts[order[i+1]];if(b-a<=.1f)continue;
                float t=(a+b)*.5f;journey.Sample(t);montage.Seek(t);atm.Sample(t);ctx.Check(!haloT.gameObject.activeSelf && !screenLight.gameObject.activeSelf && atm.sampledFocus<0,"focus light hidden in the gap t="+t);gaps++;
            }
            atm.Sample(12f);ctx.Check(!haloT.gameObject.activeSelf && !screenLight.gameObject.activeSelf,"focus light hidden at 12 s");ctx.evidence["focus_gaps_checked"]=gaps;
        }
        finally{journey.Sample(0);montage.Seek(0);}
        ctx.evidence["focus"]=focusRows;ctx.evidence["halo_max_error_m"]=maxHaloError;ctx.evidence["halo_min_bottom_y"]=minBottom;ctx.evidence["screen_light_max_distance_error_m"]=maxLightError;
        // 9. Pure function of time
        atm.Sample(88.1f);var first=new[]{atm.skyMaterial.GetColor("_TopColor").ToString(),atm.gridMaterial.GetFloat("_Opacity").ToString("R"),atm.midStarsMaterial.GetVector("_Velocity").ToString()};
        atm.Sample(10f);atm.Sample(88.1f);var second=new[]{atm.skyMaterial.GetColor("_TopColor").ToString(),atm.gridMaterial.GetFloat("_Opacity").ToString("R"),atm.midStarsMaterial.GetVector("_Velocity").ToString()};
        ctx.Check(first.SequenceEqual(second),"reverse seek repeatable",new{first,second});
        ctx.Check(Mathf.Abs(atm.duration-director.duration)<1e-4f,"duration matches the director",new{atm.duration,director=director.duration});
        ctx.evidence["shaders"]=shaderRows;ctx.evidence["color_space"]=PlayerSettings.colorSpace.ToString();ctx.evidence["pixel_light_count"]=QualitySettings.pixelLightCount;
        atm.Waiting();ResetClock(atm);journey.Sample(0);
        var result=new{success=ctx.failures.Count==0,scene=ScenePath,checks=ctx.checks,failures=ctx.failures,evidence=ctx.evidence};string json=Report("atmosphere-validate.json",result);
        if(ctx.failures.Count>0)throw new InvalidOperationException(json);
        return new{success=true,checks=ctx.checks,haloMaxErrorM=maxHaloError,haloMinBottomY=minBottom,report=Tmp+"/reports/atmosphere-validate.json"};
    }
}
