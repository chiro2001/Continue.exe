using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object=UnityEngine.Object;

public static class OWEpisodeV3Montage
{
    const string ScenePath="Assets/Scenes/OWM_Episode123_v3.unity";
    const string Root="Assets/OldWebMap/Episode123/Revision3";
    const string ScorePath="AgentScripts/data/episode123-v3-score.json";
    static TMP_FontAsset Font;
    static string ReportDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v3/montage");
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');
        if(slash<0)throw new InvalidOperationException("Missing asset root "+path);
        Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
    static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static Vector3 Vector(JToken token){return new Vector3((float)token[0],(float)token[1],(float)token[2]);}
    static Color Hex(string value){Color c;ColorUtility.TryParseHtmlString(value,out c);return c;}
    static Material Mat(string name,string shaderName,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find(shaderName);
        if(shader==null || !shader.isSupported)throw new InvalidOperationException("Shader missing/unsupported: "+shaderName);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
        if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
        if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",.55f);
        if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.68f);
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    static void NoShadows(Renderer renderer)
    {
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
    }
    static Transform Prim(string name,PrimitiveType type,Transform parent,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;NoShadows(renderer);return go.transform;
    }
    static TMP_Text Text(string name,string text,Transform parent,Vector3 position,Vector2 size,float fontSize,Color color)
    {
        var t=Child(name,parent).AddComponent<TextMeshPro>();t.font=Font;t.text=text;t.fontSize=fontSize;
        t.transform.localPosition=position;t.rectTransform.sizeDelta=size;t.alignment=TextAlignmentOptions.Center;
        t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Ellipsis;t.color=color;return t;
    }
    static void PrepareFont(string text,OWEpisodeDirector director)
    {
        string path=Root+"/Fonts/MontageChinese.asset";Folder(Root+"/Fonts");
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(Font==null)
        {
            string source=director.stage!=null && director.stage.captionText!=null?AssetDatabase.GetAssetPath(director.stage.captionText.font):"";
            if(string.IsNullOrEmpty(source))source="Assets/OldWebMap/Episode123/Fonts/EpisodeChinese.asset";
            if(!AssetDatabase.CopyAsset(source,path))throw new InvalidOperationException("Cannot preserve source Chinese font for V3 montage");
            Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
        string chars=new string(text.Where(c=>!char.IsControl(c) && !Font.HasCharacter(c)).Distinct().ToArray());
        if(chars.Length>0)
        {
            Font.TryAddCharacters(chars,out string missing);
            if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Montage font missing glyphs: "+missing);
        }
        foreach(var texture in Font.atlasTextures)if(texture!=null && !AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,Font);
        EditorUtility.SetDirty(Font);
    }
    static Texture2D ImportImage(JToken image)
    {
        string path=(string)image["path"];
        if(string.IsNullOrEmpty(path))path=Root+"/Images/"+(string)image["id"]+".png";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new FileNotFoundException("Montage image not imported: "+(string)image["id"],path);
        importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;importer.sRGBTexture=true;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.isReadable=false;
        string category=(string)image["category"],kind=(string)image["kind"];
        importer.filterMode=(category=="desktop" || category=="system" || category=="software" || category=="tools" || kind=="original-operation-reconstruction")?FilterMode.Point:FilterMode.Bilinear;
        importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static int Motion(JToken token)
    {
        if(token==null)return 0;if(token.Type==JTokenType.Integer)return (int)token;
        string name=(string)token;
        switch(name){case "slide":return 1;case "tilt":return 2;case "depth":return 3;case "scroll":return 4;case "stamp":return 5;case "transfer":return 6;default:return 0;}
    }
    static Mesh SaveMesh(string name,Vector3[] vertices,Vector3[] normals,int[] triangles,Color[] colors=null)
    {
        string path=Root+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.vertices=vertices;mesh.triangles=triangles;if(normals!=null)mesh.normals=normals;else mesh.RecalculateNormals();
        if(colors!=null)mesh.colors=colors;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh PointerMesh()
    {
        // An actual raised desktop pointer silhouette, not another image plane.
        Vector3[] front={new Vector3(-.34f,.52f,-.02f),new Vector3(-.34f,-.42f,-.02f),new Vector3(-.1f,-.2f,-.02f),new Vector3(.08f,-.55f,-.02f),new Vector3(.26f,-.46f,-.02f),new Vector3(.07f,-.1f,-.02f),new Vector3(.44f,-.1f,-.02f)};
        int[] face={0,1,2,0,2,5,0,5,6,2,3,4,2,4,5};
        var vertices=new List<Vector3>(front);vertices.AddRange(front.Select(p=>p+Vector3.forward*.08f));var triangles=new List<int>(face);
        for(int i=0;i<face.Length;i+=3){triangles.Add(face[i]+7);triangles.Add(face[i+2]+7);triangles.Add(face[i+1]+7);}
        for(int i=0;i<7;i++){int j=(i+1)%7;triangles.AddRange(new[]{i,j,i+7,j,j+7,i+7});}
        return SaveMesh("Native raised operation pointer",vertices.ToArray(),null,triangles.ToArray());
    }
    static Mesh MatrixMesh()
    {
        float phi=(1+Mathf.Sqrt(5))*.5f;
        Vector3[] unit={new Vector3(-1,phi,0),new Vector3(1,phi,0),new Vector3(-1,-phi,0),new Vector3(1,-phi,0),new Vector3(0,-1,phi),new Vector3(0,1,phi),new Vector3(0,-1,-phi),new Vector3(0,1,-phi),new Vector3(phi,0,-1),new Vector3(phi,0,1),new Vector3(-phi,0,-1),new Vector3(-phi,0,1)};
        for(int i=0;i<unit.Length;i++)unit[i]=unit[i].normalized;
        int[] faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        Action<Vector3,float,Color> dot=(center,radius,color)=>
        {
            int first=vertices.Count;
            for(int i=0;i<unit.Length;i++){vertices.Add(center+unit[i]*radius);normals.Add(unit[i]);colors.Add(color);}
            foreach(int face in faces)triangles.Add(first+face);
        };
        Color blue=new Color(.08f,.35f,.72f),white=new Color(.8f,.94f,1);
        // Latitude/longitude lattice is a fully volumetric globe. Distinct white
        // meridians and two tilted orbital rings remain legible while rotating.
        for(int latitude=-75;latitude<=75;latitude+=15)
        {
            float lat=latitude*Mathf.Deg2Rad;
            for(int longitude=0;longitude<360;longitude+=15)
            {
                float lon=longitude*Mathf.Deg2Rad;
                var p=new Vector3(Mathf.Cos(lat)*Mathf.Sin(lon),Mathf.Sin(lat),Mathf.Cos(lat)*Mathf.Cos(lon))*1.35f;
                bool highlight=(longitude%90==0 || latitude==0);dot(p,highlight?.068f:.052f,highlight?white:blue);
            }
        }
        dot(Vector3.up*1.35f,.09f,white);dot(Vector3.down*1.35f,.09f,white);
        for(int ring=0;ring<2;ring++)for(int i=0;i<72;i++)
        {
            float a=i/72f*Mathf.PI*2;Vector3 p=new Vector3(Mathf.Sin(a)*1.69f,0,Mathf.Cos(a)*1.69f);
            p=Quaternion.Euler(ring==0?28:73,0,ring==0?33:-28)*p;
            dot(p,i%9==0?.071f:.037f,i%9==0?white:Color.Lerp(blue,white,.35f));
        }
        string[] glyphs={"11111/10000/10000/11110/10000/10000/10000","01111/10000/10000/10000/10000/10000/01111","00100/01100/00100/00100/00100/00100/01110","10010/10010/10010/11111/00010/00010/00010"};
        for(int letter=0;letter<glyphs.Length;letter++)
        {
            var lines=glyphs[letter].Split('/');for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(lines[y][x]=='1')
            {var p=new Vector3((letter*6+x-11.5f)*.103f,(3-y)*.103f,-.70f);dot(p,.049f,y%3==0?white:Color.Lerp(blue,white,.7f));dot(p+Vector3.forward*.16f,.038f,blue);}
        }
        return SaveMesh("Combined native blue white metal dot globe",vertices.ToArray(),normals.ToArray(),triangles.ToArray(),colors.ToArray());
    }
    static Transform MeshObject(string name,Transform parent,Mesh mesh,Material material)
    {
        var go=Child(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;NoShadows(renderer);return go.transform;
    }
    static void CheckScore(JObject score)
    {
        int slots=(int?)score["slots"]??20;
        if(slots<16 || slots>20)throw new InvalidOperationException("V3 requires 16-20 persistent window slots");
        var images=(JArray)score["images"];var events=(JArray)score["events"];
        if(images==null || images.Count==0 || events==null || events.Count==0)throw new InvalidOperationException("Score needs images and concrete events");
        var ids=new HashSet<string>(images.Select(i=>(string)i["id"]));
        if(ids.Count!=images.Count)throw new InvalidOperationException("Image IDs must be unique");
        int lastSlot=-1;float lastStart=-1,lastEnd=-1;
        foreach(var ev in events)
        {
            int slot=(int)ev["slot"];float start=(float)ev["start"],end=(float)ev["end"];
            if(slot<0 || slot>=slots || end<=start || start<0)throw new InvalidOperationException("Invalid event slot/time");
            if(slot<lastSlot || slot==lastSlot && start<lastStart)throw new InvalidOperationException("Events must be sorted by slot/start");
            if(slot==lastSlot && start<lastEnd-.0001f)throw new InvalidOperationException("Overlapping events in slot "+slot);
            if(!ids.Contains((string)ev["image"]))throw new InvalidOperationException("Unknown image ID: "+ev["image"]);
            if(ev["from"]?.Count()!=3 || ev["to"]?.Count()!=3 || (float)ev["width"]<=0 || (float)ev["height"]<=0)throw new InvalidOperationException("Invalid window geometry");
            if((int?)ev["channel"]<-1 || (int?)ev["channel"]>3)throw new InvalidOperationException("Invalid voice channel");
            lastSlot=slot;lastStart=start;lastEnd=end;
        }
    }
    static void BuildComments(OWV3Montage montage,JArray events)
    {
        var selected=new List<JToken>();float nextTime=-1;
        foreach(var ev in events.OrderBy(e=>(float)e["start"]).ThenBy(e=>(int)e["slot"]))
        {
            float time=(float)ev["start"];string label=(string)ev["label"];
            if(time<nextTime || string.IsNullOrEmpty(label))continue;
            selected.Add(ev);nextTime=time+.52f;
        }
        montage.commentStarts=selected.Select(e=>(float)e["start"]).ToArray();
        montage.commentChannels=selected.Select(e=>(int?)e["channel"]??0).ToArray();
        montage.commentLabels=selected.Select(e=>
        {
            string label=(string)e["label"],result=(string)e["result"];
            string value=string.IsNullOrEmpty(result)?label:label+" → "+result;
            return value.Length<=34?value:value.Substring(0,33)+"…";
        }).ToArray();
        montage.commentRoots=new Transform[12];montage.commentTexts=new TMP_Text[12];
        for(int i=0;i<12;i++)
        {
            var t=Text("Forward authored comment "+i,"",montage.transform,Vector3.zero,new Vector2(5.5f,.18f),.72f,new Color(.7f,.9f,1));
            t.alignment=TextAlignmentOptions.Left;montage.commentRoots[i]=t.transform;montage.commentTexts[i]=t;
        }
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Build montage in stopped V3 scene");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();if(director==null)throw new InvalidOperationException("Director required");
        var score=JObject.Parse(File.ReadAllText(ScorePath));CheckScore(score);
        Folder(Root+"/Materials");Folder(Root+"/Meshes");
        var imageRows=(JArray)score["images"];var events=(JArray)score["events"];var scenes=(JArray)score["scenes"];
        string rendered=string.Join("",imageRows.Select(r=>(string)r["title"]))+string.Join("",events.Select(r=>(string)r["label"]+(string)r["result"]))+string.Join("",scenes.Select(r=>(string)r["title"]+(string)r["caption"]));
        PrepareFont(rendered+"共同创造正在传送连接完成校对完成发布完成收到归档正在打开正在操作结果弹幕→…",director);
        var imageIds=imageRows.Select(i=>(string)i["id"]).ToArray();
        var episode=GameObject.Find("OWEpisode123");var owner=episode!=null?episode.transform:director.transform;
        var old=owner.Find("Revision3Montage");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var root=Child("Revision3Montage",owner).transform;
        var montage=root.gameObject.AddUdonSharpComponent<OWV3Montage>();montage.director=director;montage.music=director.music;
        montage.imageTextures=imageRows.Select(ImportImage).ToArray();montage.imageTitles=imageRows.Select(i=>(string)i["title"]??(string)i["id"]).ToArray();
        montage.starts=events.Select(e=>(float)e["start"]).ToArray();montage.ends=events.Select(e=>(float)e["end"]).ToArray();
        montage.from=events.Select(e=>Vector(e["from"])).ToArray();montage.to=events.Select(e=>Vector(e["to"])).ToArray();
        montage.widths=events.Select(e=>(float)e["width"]).ToArray();montage.heights=events.Select(e=>(float)e["height"]).ToArray();
        montage.images=events.Select(e=>Array.IndexOf(imageIds,(string)e["image"])).ToArray();montage.motions=events.Select(e=>Motion(e["motion"])).ToArray();
        montage.channels=events.Select(e=>(int?)e["channel"]??-1).ToArray();montage.labels=events.Select(e=>(string)e["label"]??"正在操作").ToArray();
        montage.results=events.Select(e=>!string.IsNullOrEmpty((string)e["result"])?(string)e["result"]:((string)e["label"]??"操作")+" · 完成").ToArray();
        montage.sceneStarts=scenes==null?new float[0]:scenes.Select(e=>(float)e["start"]).ToArray();montage.sceneEnds=scenes==null?new float[0]:scenes.Select(e=>(float)e["end"]).ToArray();
        montage.sceneTitles=scenes==null?new string[0]:scenes.Select(e=>(string)e["title"]??"").ToArray();montage.sceneCaptions=scenes==null?new string[0]:scenes.Select(e=>(string)e["caption"]??"").ToArray();
        int count=(int?)score["slots"]??20;
        montage.slotFirst=new int[count];montage.slotCount=new int[count];montage.windowRoots=new Transform[count];montage.surfaces=new Transform[count];montage.backings=new Transform[count];
        montage.headerBars=new Transform[count];montage.pointers=new Transform[count];montage.actionPads=new Transform[count];montage.windowTitles=new TMP_Text[count];montage.operationTexts=new TMP_Text[count];montage.slotMaterials=new Material[count];
        montage.windowTitleTransforms=new Transform[count];montage.windowTitleRects=new RectTransform[count];
        montage.operationTextTransforms=new Transform[count];montage.operationTextRects=new RectTransform[count];
        var backing=Mat("Montage window metal backing","Standard",Hex("#182a40"));
        var header=Mat("Montage white blue title chrome","Standard",Hex("#b8d8ed"));
        var pointerMat=Mat("Montage raised white pointer","Standard",Hex("#eef8ff"));
        var actionMat=Mat("Montage operation pad","Standard",Hex("#30678b"));
        Mesh pointer=PointerMesh();
        int offset=0;
        for(int slot=0;slot<count;slot++)
        {
            montage.slotFirst[slot]=offset;int size=events.Count(e=>(int)e["slot"]==slot);montage.slotCount[slot]=size;offset+=size;
            var window=Child("Persistent operation window "+slot.ToString("00"),root).transform;montage.windowRoots[slot]=window;
            var material=Mat("Montage window slot "+slot.ToString("00"),"OldWebMap/V3/Layered Window",Color.white);montage.slotMaterials[slot]=material;
            montage.surfaces[slot]=Prim("Actual cited image surface",PrimitiveType.Quad,window,material);
            montage.backings[slot]=Prim("Metal depth backing",PrimitiveType.Cube,window,backing);
            montage.headerBars[slot]=Prim("Readable native title chrome",PrimitiveType.Cube,window,header);
            montage.actionPads[slot]=Prim("Pointer operation target",PrimitiveType.Cube,window,actionMat);
            montage.pointers[slot]=MeshObject("Raised pointer with completed action",window,pointer,pointerMat);
            montage.windowTitles[slot]=Text("Window source title","",window,Vector3.zero,new Vector2(2,.14f),.62f,new Color(.05f,.14f,.23f));
            montage.operationTexts[slot]=Text("Actual operation and result","",window,Vector3.zero,new Vector2(2,.14f),.56f,new Color(.8f,.9f,1));
            montage.windowTitleTransforms[slot]=montage.windowTitles[slot].transform;montage.windowTitleRects[slot]=montage.windowTitles[slot].rectTransform;
            montage.operationTextTransforms[slot]=montage.operationTexts[slot].transform;montage.operationTextRects[slot]=montage.operationTexts[slot].rectTransform;
            window.gameObject.SetActive(false);
        }
        montage.sceneTitle=Text("Current scene title","",root,new Vector3(0,4.65f,8),new Vector2(9,.4f),1.45f,new Color(.82f,.94f,1));
        montage.sceneCaption=Text("Current operation context","",root,new Vector3(0,.48f,3.8f),new Vector2(6.6f,.28f),.86f,new Color(.85f,.89f,.9f));
        var matrixMat=Mat("Blue white native metal point matrix","OldWebMap/V3/Metal Point Matrix",Color.white);
        montage.matrixRoot=MeshObject("Native 3D blue white globe and crossing orbital rings",root,MatrixMesh(),matrixMat);montage.matrixMaterial=matrixMat;
        BuildComments(montage,events);
        montage.Seek(0);UdonSharpEditorUtility.CopyProxyToUdon(montage);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(montage));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return JsonConvert.SerializeObject(new{root=root.name,slots=count,events=events.Count,images=imageRows.Count,comments=montage.commentStarts.Length,nativeMatrixVertices=montage.matrixRoot.GetComponent<MeshFilter>().sharedMesh.vertexCount,automaticSampling="LateUpdate reads director.Elapsed()",sceneSaved=false,retireOldV2MontageInMainBuilder=true});
    }
    public static object Sample(float time)
    {
        var montage=Object.FindObjectOfType<OWV3Montage>();if(montage==null)throw new InvalidOperationException("Build montage first");
        montage.Seek(time);return Describe(montage);
    }
    static object Describe(OWV3Montage m)
    {
        return new{time=m.sampledTime,activeWindows=m.activeWindows,activeComments=m.activeComments,scene=m.sampledScene,eventTransitions=m.eventTransitions,binarySearches=m.binarySearches,
            visibleSlots=m.windowRoots.Select((t,i)=>new{slot=i,visible=t.gameObject.activeSelf,position=new[]{t.position.x,t.position.y,t.position.z}}).Where(r=>r.visible).ToArray()};
    }
    public static object Validate()
    {
        var m=Object.FindObjectOfType<OWV3Montage>();if(m==null)throw new InvalidOperationException("Build montage first");
        if(m.windowRoots.Length<16 || m.windowRoots.Length>20)throw new InvalidOperationException("Persistent layer count invalid");
        var rows=new List<object>();int max=0;
        foreach(float time in new[]{0f,5.12f,10.24f,20.48f,30.72f,43.52f,64f,81.92f,84.48f,89.6f,92.16f,102.4f,115.2f,128f,133.12f})
        {
            m.Seek(time);int expected=Enumerable.Range(0,m.starts.Length).Count(i=>time>=m.starts[i]&&time<m.ends[i]);
            if(expected!=m.activeWindows)throw new InvalidOperationException("Slot seek disagrees with score at "+time);
            max=Math.Max(max,m.activeWindows);rows.Add(Describe(m));
        }
        // Compare steady cursor advancement against the independently evaluated
        // score, then seek back over already displayed events to catch stale slots.
        m.Seek(0);int before=m.binarySearches;
        for(float time=.1f;time<133.12f;time+=.1f)
        {
            m.Sample(time);int expected=Enumerable.Range(0,m.starts.Length).Count(i=>time>=m.starts[i]&&time<m.ends[i]);
            if(expected!=m.activeWindows)throw new InvalidOperationException("Cursor disagrees with score at "+time);
        }
        if(m.binarySearches!=before)throw new InvalidOperationException("Steady playback performed event binary searches");
        m.Seek(43.52f);string first=JsonConvert.SerializeObject(m.windowRoots.Select((t,i)=>new{slot=i,visible=t.gameObject.activeSelf,p=new[]{t.position.x,t.position.y,t.position.z},s=new[]{t.localScale.x,t.localScale.y,t.localScale.z}}).Where(r=>r.visible));
        m.Seek(115.2f);m.Seek(43.52f);string second=JsonConvert.SerializeObject(m.windowRoots.Select((t,i)=>new{slot=i,visible=t.gameObject.activeSelf,p=new[]{t.position.x,t.position.y,t.position.z},s=new[]{t.localScale.x,t.localScale.y,t.localScale.z}}).Where(r=>r.visible));
        if(first!=second)throw new InvalidOperationException("Montage seek is not repeatable");
        var shaderMessages=new[]{"OldWebMap/V3/Layered Window","OldWebMap/V3/Metal Point Matrix"}.Select(name=>new{name,messages=ShaderUtil.GetShaderMessages(Shader.Find(name)).Select(e=>new{e.message,e.line,severity=e.severity.ToString()}).ToArray()}).ToArray();
        if(shaderMessages.Any(s=>s.messages.Any(e=>e.severity=="Error")))throw new InvalidOperationException("Montage shader errors");
        var result=new{rows,maxActive=max,steadyCursorVerified=true,seekRepeatable=true,persistentLayerCount=m.windowRoots.Length,persistentSlotMaterials=m.slotMaterials.All(AssetDatabase.Contains),nativeMeshVertices=m.matrixRoot.GetComponent<MeshFilter>().sharedMesh.vertexCount,shaderMessages};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"montage-validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
        m.Seek(0);return JsonConvert.SerializeObject(result);
    }
}
