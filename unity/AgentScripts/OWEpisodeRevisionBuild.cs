using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEditor.Events;
using UnityEditor.Animations;
using UnityEngine.Rendering;
using VRC.SDK3.Components;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using OldWebMap;
using UdonSharp;
using UdonSharpEditor;
using Object=UnityEngine.Object;

public static class OWEpisodeRevisionBuild
{
    const string Original="Assets/Scenes/OWM_Episode123.unity";
    const string ScenePath="Assets/Scenes/OWM_Episode123_v2.unity";
    const string AssetRoot="Assets/OldWebMap/Episode123/Revision2";
    static string DirectoryPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v2");
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    static object Snapshot()
    {
        return new {
            scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,
            layers=Enumerable.Range(0,32).Select(LayerMask.LayerToName).ToArray(),
            bodies=Object.FindObjectsOfType<Rigidbody>(true).Select(b=>new{path=PathOf(b.transform),b.isKinematic,b.useGravity,b.detectCollisions,b.constraints}).ToArray(),
            actors=Object.FindObjectsOfType<OWActorRig>(true).Select(a=>new{path=PathOf(a.transform),a.appearanceId,animator=EditorJsonUtility.ToJson(a.animator),renderers=a.GetComponentsInChildren<Renderer>(true).Select(r=>new{path=PathOf(r.transform),r.enabled,r.shadowCastingMode,r.receiveShadows,materials=r.sharedMaterials.Select(m=>m==null?"null":AssetDatabase.GetAssetPath(m)).ToArray()}).ToArray()}).ToArray(),
            materials=Object.FindObjectsOfType<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Select(m=>new{path=AssetDatabase.GetAssetPath(m),m.name,shader=m.shader.name,m.renderQueue,m.enableInstancing}).ToArray(),
            udon=Object.FindObjectsOfType<VRC.Udon.UdonBehaviour>(true).Select(u=>new{path=PathOf(u.transform),u.enabled}).ToArray(),
            descriptor=EditorJsonUtility.ToJson(Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>())
        };
    }
    static string PathOf(Transform t){return t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;}
    public static object PreserveUserBaseline()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling)throw new InvalidOperationException("Stop playback and complete compilation first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserve the user's unsaved scene before revision");
        if(SceneManager.GetActiveScene().path!=Original && SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Unexpected project scene");
        Directory.CreateDirectory(DirectoryPath);
        if(SceneManager.GetActiveScene().path==Original)
        {
            File.WriteAllText(Path.Combine(DirectoryPath,"user-baseline.json"),JsonConvert.SerializeObject(Snapshot(),Formatting.Indented));
            string backup=Path.Combine(DirectoryPath,"user-saved-baseline.unity");File.Copy(Original,backup,true);
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null && !AssetDatabase.CopyAsset(Original,ScenePath))throw new InvalidOperationException("Cannot clone saved baseline");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        return JsonConvert.SerializeObject(new{scene=SceneManager.GetActiveScene().path,dirty=SceneManager.GetActiveScene().isDirty,originalPreserved=true,backup=Path.Combine(DirectoryPath,"user-saved-baseline.unity")});
    }
    public static object ActorHands()
    {
        var rows=new System.Collections.Generic.List<object>();
        foreach(var actor in Object.FindObjectsOfType<OWActorRig>().Take(2))
        {
            foreach(string name in new[]{"Idle","Type","Offer","Point","Cheer","Inspect","Walk"})
            {
                foreach(float phase in new[]{.05f,.35f,.5f,.75f})
                {
                    actor.animator.Play(name,0,phase);actor.animator.SetFloat("PoseTime",phase);actor.animator.Update(.02f);actor.animator.Update(0);
                    Vector3 left=actor.transform.InverseTransformPoint(actor.animator.GetBoneTransform(HumanBodyBones.LeftHand).position),right=actor.transform.InverseTransformPoint(actor.rightHand.position);
                    rows.Add(new{actor.appearanceId,motion=name,phase,left=new[]{left.x,left.y,left.z},right=new[]{right.x,right.y,right.z},crossed=left.x>right.x});
                }
            }
        }
        return JsonConvert.SerializeObject(rows);
    }
    public static object CalibrateTyping()
    {
        var rows=new System.Collections.Generic.List<object>();
        foreach(var actor in Object.FindObjectsOfType<OWActorRig>().Take(2))
        {
            actor.animator.Play("Type",0,.5f);actor.animator.SetFloat("PoseTime",.5f);actor.animator.Update(.02f);actor.animator.Update(0);
            var pose=new HumanPose();
            using(var handler=new HumanPoseHandler(actor.animator.avatar,actor.animator.transform))
            {
                handler.GetHumanPose(ref pose);var original=(float[])pose.muscles.Clone();
                foreach(float front in new[]{-.6f,-.4f,-.2f,0f,.2f})foreach(float down in new[]{-.5f,-.65f,-.3f})foreach(float stretch in new[]{.6f,.8f,.95f})
                {
                    pose.muscles=(float[])original.Clone();
                    for(int i=0;i<HumanTrait.MuscleName.Length;i++)
                    {
                        var name=HumanTrait.MuscleName[i];
                        if(name=="Left Arm Front-Back" || name=="Right Arm Front-Back")pose.muscles[i]=front;
                        if(name=="Left Arm Down-Up" || name=="Right Arm Down-Up")pose.muscles[i]=down;
                        if(name.Contains("Forearm Stretch"))pose.muscles[i]=stretch;
                    }
                    handler.SetHumanPose(ref pose);
                    var left=actor.transform.InverseTransformPoint(actor.leftHand.position);var right=actor.transform.InverseTransformPoint(actor.rightHand.position);
                    if(left.x<-.045f && right.x>.045f && left.z>.08f)
                        rows.Add(new{actor.appearanceId,front,down,stretch,left=new[]{left.x,left.y,left.z},right=new[]{right.x,right.y,right.z},score=Mathf.Abs(left.x+.12f)+Mathf.Abs(right.x-.12f)+Mathf.Abs(left.y-.63f)*.6f+Mathf.Abs(left.z-.22f)*.5f});
                }
            }
        }
        return JsonConvert.SerializeObject(rows);
    }

    static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static Color Hex(string value){Color c;ColorUtility.TryParseHtmlString(value,out c);return c;}
    static Material Solid(string name,string color,float emission=0)
    {
        string path=AssetRoot+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.color=Hex(color);mat.enableInstancing=true;mat.SetFloat("_Glossiness",.15f);
        if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Hex(color)*emission);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static GameObject Prim(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static TMP_FontAsset Font;
    static TMP_Text Text(string name,string text,Transform parent,Vector3 position,float width,float size,string color="#f4efdf")
    {
        var label=Child(name,parent).AddComponent<TextMeshPro>();label.font=Font;label.text=text;label.fontSize=size*10;
        label.alignment=TextAlignmentOptions.Center;label.enableWordWrapping=false;label.rectTransform.sizeDelta=new Vector2(width,.7f);label.transform.localPosition=position;label.color=Hex(color);return label;
    }
    public static object RepairPrograms()
    {
        Folder(AssetRoot+"/Programs");
        foreach(string type in new[]{"OWEpisodeMontage","OWEpisodeCaption"})
        {
            string scriptPath="Assets/OldWebMap/Scripts/"+type+".cs";var script=AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if(script==null)throw new InvalidOperationException("Missing "+scriptPath);
            if(AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Any(a=>a!=null && a.sourceCsScript==script))continue;
            var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;AssetDatabase.CreateAsset(program,AssetRoot+"/Programs/"+type+".asset");
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new InvalidOperationException("Udon compile failed");
        AssetDatabase.SaveAssets();return "Revision Udon programs compiled";
    }
    static void PrepareFont(string characters)
    {
        Folder(AssetRoot+"/Fonts");string path=AssetRoot+"/Fonts/RevisionChinese.asset";
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)==null)AssetDatabase.CopyAsset("Assets/OldWebMap/Episode123/Fonts/EpisodeChinese.asset",path);
        Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);var needed=new string(characters.Where(c=>!char.IsControl(c) && !Font.HasCharacter(c)).Distinct().ToArray());
        if(needed.Length>0)Font.TryAddCharacters(needed,out string missing);
        foreach(var tex in Font.atlasTextures)if(tex!=null && !AssetDatabase.Contains(tex))AssetDatabase.AddObjectToAsset(tex,Font);
        EditorUtility.SetDirty(Font);
    }
    public static object ImportImages()
    {
        Folder(AssetRoot+"/Materials");
        var images=(JArray)JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-v2-montage.json"))["images"];
        foreach(var row in images)
        {
            string id=(string)row["id"];string path=AssetRoot+"/Images/"+id+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new InvalidOperationException("Image missing: "+path);
            importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.alphaIsTransparency=true;
            importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        }
        return JsonConvert.SerializeObject(new{count=images.Count});
    }
    static Material ImageMaterial(string id)
    {
        string path=AssetRoot+"/Materials/image_"+id+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("OldWebMap/Page Surface"));AssetDatabase.CreateAsset(mat,path);}
        mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot+"/Images/"+id+".png");mat.enableInstancing=true;mat.SetFloat("_Exposure",1);mat.SetFloat("_Cutoff",.02f);EditorUtility.SetDirty(mat);return mat;
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open owned stopped v2 scene");
        Folder(AssetRoot+"/Materials");Folder(AssetRoot+"/Motion");
        var data=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-v2-montage.json"));var fft=JObject.Parse(File.ReadAllText("AgentScripts/data/episode123-v2-spectrum.json"));
        PrepareFont(data.ToString()+File.ReadAllText("AgentScripts/OWEpisodeRevisionBuild.cs")+"共同观看开始演出正在准备回车开始射线点击演出期间收起控制面板可重播真实历史原图当前制作自截翻译共享上载下载同步声部频谱来源资料已经结束");
        var owner=GameObject.Find("OWEpisode123").transform;var director=Object.FindObjectOfType<OWEpisodeDirector>();var stage=director.stage;
        var old=owner.Find("Revision2");if(old!=null){if(stage.captionText!=null)stage.captionText.transform.SetParent(owner,true);Object.DestroyImmediate(old.gameObject);}
        var root=Child("Revision2",owner).transform;var montage=Child("Beat driven historical montage",root).AddUdonSharpComponent<OWEpisodeMontage>();
        director.continuousPerformance=true;director.montage=montage;montage.music=director.music;
        var images=(JArray)data["images"];var cuts=(JArray)data["cuts"];
        montage.imageMaterials=images.Select(i=>ImageMaterial((string)i["id"])).ToArray();montage.imageWidths=images.Select(i=>(int)i["width"]).ToArray();montage.imageHeights=images.Select(i=>(int)i["height"]).ToArray();
        montage.cutTimes=cuts.Select(c=>(float)c["time"]).ToArray();montage.cutImages=cuts.Select(c=>(int)c["image"]).ToArray();montage.cutLayouts=cuts.Select(c=>(int)c["layout"]).ToArray();montage.cutWords=cuts.Select(c=>(string)c["word"]).ToArray();montage.cutLabels=cuts.Select(c=>(string)c["label"]).ToArray();
        montage.bandCount=(int)fft["bands"];montage.spectrumFrames=(int)fft["frames"];montage.spectrumStep=(float)fft["step"];
        montage.spectrum0=fft["channels"][0].ToObject<float[]>();montage.spectrum1=fft["channels"][1].ToObject<float[]>();montage.spectrum2=fft["channels"][2].ToObject<float[]>();montage.spectrum3=fft["channels"][3].ToObject<float[]>();montage.channelEnergy=new float[4];
        montage.imageSlots=new Renderer[4];montage.slotRoots=new Transform[4];montage.slotPlanes=new Transform[4];
        for(int i=0;i<4;i++)
        {
            var slot=Child(i==0?"Main image":"Memory flash "+i,root).transform;montage.slotRoots[i]=slot;
            var plane=Prim("Cited image",PrimitiveType.Quad,slot,Vector3.zero,Vector3.one,montage.imageMaterials[0]);montage.imageSlots[i]=plane.GetComponent<Renderer>();montage.slotPlanes[i]=plane.transform;
        }
        montage.focusWord=Text("Changing central word",montage.cutWords[0],root,new Vector3(0,3.45f,4.15f),6,.32f,"#f3d47f");
        var wordStyle=AssetDatabase.LoadAssetAtPath<Material>(AssetRoot+"/Materials/FocusWord.mat");if(wordStyle!=null)montage.focusWord.fontSharedMaterial=wordStyle;
        montage.wordTransform=montage.focusWord.transform;
        montage.imageLabel=Text("Image era and provenance",montage.cutLabels[0],root,new Vector3(0,.76f,3.8f),6,.095f,"#c2dde4");
        var greet=Text("GREETZ ribbon","GREETZ TO · 写工具的人 · 汉化组 · 字幕组 · 画图的人 · 做种的人 · BBS · 站长 · 楼主好人 · 世界作者 · AVATAR · 和你一起的人 ·",root,new Vector3(0,.52f,2.25f),25,.095f);
        montage.greeting=greet;montage.greetingRoot=greet.transform;
        BuildSpectrum(montage,root);BuildLogo(montage,root);BuildControls(stage,director,root);FixTyping(stage);
        // Keep body/contact references and the geometric Hub from the user's saved scene.
        // Retire the slow full-page graphic layer; the cited montage replaces it.
        foreach(var cue in stage.cueRoots)
        {
            foreach(var r in cue.GetComponentsInChildren<Renderer>(true))
                if(r.sharedMaterial!=null && (r.sharedMaterial.shader.name=="OldWebMap/Page Surface" || r.name=="Page thickness" || r.name=="Shared CRT carrier" || r.name=="CRT base" || r is MeshRenderer && r.GetComponent<TMP_Text>()!=null))r.enabled=false;
        }
        var bt=stage.cueRoots[5];var bitGroup=bt.Find("BT mechanism aside");if(bitGroup==null)bitGroup=Child("BT mechanism aside",bt).transform;bitGroup.localPosition=new Vector3(-3.5f,-.2f,2.5f);bitGroup.localScale=Vector3.one*.4f;
        foreach(var t in stage.animated)if(t!=null && t.IsChildOf(bt) && (t.name.StartsWith("Received piece") || t.name.StartsWith("Providing existing piece") || t.name=="Transfer completion" || t.name=="Complete percentage"))t.SetParent(bitGroup,false);
        var manga=stage.cueRoots[3].Find("Layered original manga");if(manga!=null){manga.localPosition=new Vector3(-3.8f,2.2f,7.4f);manga.localScale=Vector3.one*.55f;foreach(var r in manga.GetComponentsInChildren<Renderer>(true))r.enabled=true;}
        var illustration=stage.cueRoots[4].Find("Illustration with actual depth");if(illustration!=null){illustration.localPosition=new Vector3(3.8f,2.2f,7.5f);illustration.localScale=Vector3.one*.65f;foreach(var r in illustration.GetComponentsInChildren<Renderer>(true))r.enabled=true;}
        var memory=owner.Find("MovingScenery/ContentAcrossDistances");
        if(memory!=null)
        {
            int index=0;
            foreach(var r in memory.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null && r.sharedMaterial.shader.name=="OldWebMap/Page Surface" && r.name=="Authored image")
            {
                int k=(index*7)%montage.imageMaterials.Length;r.sharedMaterial=montage.imageMaterials[k];index++;
            }
        }
        foreach(var b in owner.GetComponentsInChildren<UdonSharpBehaviour>(true))UdonSharpEditorUtility.CopyProxyToUdon(b);
        Pose(0);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        var result=new{scene=ScenePath,images=images.Count,cuts=cuts.Count,fftBands=4*montage.bandCount,originalUserScenePreserved=true};
        File.WriteAllText(Path.Combine(DirectoryPath,"build-report.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(result);
    }
    static void BuildSpectrum(OWEpisodeMontage m,Transform root)
    {
        string[] colors={"#53d2e2","#edbd67","#bf90ef","#86cf83"};
        m.spectrumBars=new Transform[4*m.bandCount];m.spectrumOrigins=new Vector3[m.spectrumBars.Length];m.energyObjects=new Transform[4];m.energyOrigins=new Vector3[4];m.channelLights=new Light[4];
        for(int ch=0;ch<4;ch++)
        {
            var material=Solid("Voice "+ch,colors[ch],.35f);float left=-1.86f+ch*1.04f;
            Text("Voice label "+ch,"CH 0"+(ch+1),root,new Vector3(left+.34f,.16f,1.5f),.8f,.065f,colors[ch]);
            for(int b=0;b<m.bandCount;b++)
            {
                int i=ch*m.bandCount+b;Vector3 pos=new Vector3(left+b*.044f,.23f,1.6f);
                m.spectrumOrigins[i]=pos;m.spectrumBars[i]=Prim("CH"+(ch+1)+" FFT "+b,PrimitiveType.Cube,root,pos,new Vector3(.037f,.04f,.03f),material).transform;
            }
            var energy=Prim("Voice energy orbit "+ch,PrimitiveType.Cube,root,new Vector3((ch-1.5f)*1.9f,3.35f,4.1f),Vector3.one*.12f,material).transform;
            m.energyObjects[ch]=energy;m.energyOrigins[ch]=energy.localPosition;
            var light=Child("Local music light "+ch,energy).AddComponent<Light>();light.type=LightType.Point;light.range=3;light.intensity=.2f;light.color=Hex(colors[ch]);light.shadows=LightShadows.None;m.channelLights[ch]=light;
        }
    }
    static void BuildLogo(OWEpisodeMontage m,Transform root)
    {
        var logo=Child("EDGE style native depth dot-matrix",root).transform;logo.localPosition=new Vector3(-2f,3.15f,4.5f);m.logoRoot=logo;
        var blue=Solid("Dot matrix blue","#ffffff",.5f);blue.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot+"/Images/edge-grid.png");if(blue.mainTexture!=null){blue.SetTexture("_EmissionMap",blue.mainTexture);blue.SetColor("_EmissionColor",Color.white*.55f);}var blocks=new List<Transform>();var origins=new List<Vector3>();
        string[] glyphs={"11111/10000/10000/11110/10000/10000/11111","11110/10001/10001/10001/10001/10001/11110","01111/10000/10000/10111/10001/10001/01110","11111/10000/10000/11110/10000/10000/11111"};
        for(int letter=0;letter<glyphs.Length;letter++)
        {
            var rows=glyphs[letter].Split('/');for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(rows[y][x]=='1')
            {
                var pos=new Vector3((letter*6+x-11.5f)*.085f,(3-y)*.085f,0);var dot=Prim("LUT pixel block",PrimitiveType.Cube,logo,pos,new Vector3(.066f,.066f,.035f),blue).transform;
                blocks.Add(dot);origins.Add(pos);
            }
        }
        m.letterBlocks=blocks.ToArray();m.letterOrigins=origins.ToArray();
        m.starLayers=new Transform[3];var starMat=Solid("Depth star","#a8cadd",.35f);
        for(int layer=0;layer<3;layer++)
        {
            var stars=Child("Star depth "+layer,root).transform;m.starLayers[layer]=stars;
            // One persistent mesh per depth layer keeps the distant star field inexpensive.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<90;i++)
            {
                float a=i*2.399963f+layer*.7f;float r=3+(i%13)*.59f;Vector3 p=new Vector3(Mathf.Sin(a)*r,Mathf.Cos(a)*r+2,10+layer*5+i%4);
                float size=.012f+layer*.004f;int start=vertices.Count;vertices.Add(p+new Vector3(-size,-size,0));vertices.Add(p+new Vector3(size,-size,0));vertices.Add(p+new Vector3(size,size,0));vertices.Add(p+new Vector3(-size,size,0));triangles.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            var mesh=new Mesh{name="StarLayer"+layer};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=AssetRoot+"/StarLayer"+layer+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);existing=mesh;}else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);}
            stars.gameObject.AddComponent<MeshFilter>().sharedMesh=existing;stars.gameObject.AddComponent<MeshRenderer>().sharedMaterial=starMat;
        }
    }
    static void BuildControls(OWEpisodeStage stage,OWEpisodeDirector director,Transform root)
    {
        var old=GameObject.Find("OWEpisode123/Audience playback desk");if(old!=null)old.SetActive(false);
        var canvasObj=new GameObject("Ray UI - start and replay",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster),typeof(VRCUiShape),typeof(BoxCollider));canvasObj.transform.SetParent(root,false);canvasObj.layer=17;
        canvasObj.transform.localPosition=new Vector3(-2.2f,1.05f,-1.2f);canvasObj.transform.localRotation=Quaternion.Euler(0,-54,0);canvasObj.transform.localScale=Vector3.one*.003f;
        var rect=canvasObj.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(550,330);canvasObj.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var collider=canvasObj.GetComponent<BoxCollider>();collider.size=new Vector3(550,330,2);collider.isTrigger=false;
        var backing=new GameObject("Panel",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));backing.layer=17;backing.transform.SetParent(canvasObj.transform,false);backing.GetComponent<RectTransform>().sizeDelta=rect.sizeDelta;backing.GetComponent<Image>().color=Hex("#243d50");
        var labelObj=new GameObject("Title",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));labelObj.layer=17;labelObj.transform.SetParent(canvasObj.transform,false);
        var title=labelObj.GetComponent<TextMeshProUGUI>();title.font=Font;title.text="共同观看\n前三段 · 2:13";title.fontSize=34;title.alignment=TextAlignmentOptions.Center;title.raycastTarget=false;title.rectTransform.sizeDelta=new Vector2(510,120);title.rectTransform.anchoredPosition=new Vector2(0,80);
        var buttonObj=new GameObject("Start performance",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(UnityEngine.UI.Button));buttonObj.layer=17;buttonObj.transform.SetParent(canvasObj.transform,false);
        var br=buttonObj.GetComponent<RectTransform>();br.sizeDelta=new Vector2(460,88);br.anchoredPosition=new Vector2(0,-48);buttonObj.GetComponent<Image>().color=Hex("#5c9ba7");
        var button=buttonObj.GetComponent<UnityEngine.UI.Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.targetGraphic=buttonObj.GetComponent<Image>();
        var handler=buttonObj.AddUdonSharpComponent<OWEpisodeButton>();handler.director=director;handler.action=0;
        UnityEventTools.AddStringPersistentListener(button.onClick,UdonSharpEditorUtility.GetBackingUdonBehaviour(handler).SendCustomEvent,"Press");
        var caption=new GameObject("Start label",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));caption.layer=17;caption.transform.SetParent(buttonObj.transform,false);var text=caption.GetComponent<TextMeshProUGUI>();text.font=Font;text.text="开始演出 / 重播";text.fontSize=31;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.rectTransform.sizeDelta=br.sizeDelta;
        var sources=GameObject.Find("OWEpisode123/Sources and reconstruction");
        if(sources!=null){var sourceText=sources.GetComponentInChildren<TMP_Text>(true);if(sourceText!=null){sourceText.font=Font;sourceText.text="来源与制作\n\n真实历史图像与网页存档\n同期配图 / 旧系统后世截图\n原创演示与本项目当前自截\n各自按性质登记\n\n2017 Hub：VRChat 官方公告\n3Q：2010 同期报道\nEAC：2022 官方公开说明\n\n音乐：FC14 · I.C.E. 2 来源记载\nRhino of Torment（资料记载）\n视觉参考：EDGE 模板\n操作音效：本项目原创";}}
        director.audiencePanels=sources==null?new[]{canvasObj}:new[]{canvasObj,sources};
        // A near, per-player caption layer remains readable above every scene prop.
        stage.captionText.transform.SetParent(root,false);var follow=Child("Unoccluded local captions",root).AddUdonSharpComponent<OWEpisodeCaption>();follow.fallbackCamera=Camera.main.transform;
        stage.captionText.transform.SetParent(follow.transform,false);stage.captionText.transform.localPosition=Vector3.zero;stage.captionText.font=Font;stage.captionText.fontSize=.56f;follow.headOffset=new Vector3(0,-.55f,1.65f);stage.captionText.rectTransform.sizeDelta=new Vector2(2.7f,.22f);
        var material=new Material(Font.material);material.name="CaptionOverlay";material.shader=Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");material.renderQueue=5000;
        string path=AssetRoot+"/Materials/CaptionOverlay.mat";var prior=AssetDatabase.LoadAssetAtPath<Material>(path);if(prior==null)AssetDatabase.CreateAsset(material,path);else{EditorUtility.CopySerialized(material,prior);Object.DestroyImmediate(material);material=prior;}
        stage.captionText.fontSharedMaterial=material;
        follow.transform.position=Camera.main.transform.position+Camera.main.transform.rotation*follow.headOffset;follow.transform.rotation=Camera.main.transform.rotation;
        stage.captions=new[]{"这些界面，你认得吗？","工具也有作者。","空间，也是人做的。","翻译，让更多人读懂。","一句一句，对到一起。","收到了，也继续传出去。","生活，在这里留下。","换个身体，共同在场。","会出错，也会互相帮助。","入口还在，条件变了。","安全、便利、能力。","继续可用，需要有人维护。"};
        stage.captionText.text=stage.captions[0];
        var end=stage.endCard.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();if(end!=null)end.text="前三段演出到这里。\n射线点击左侧面板，可以重播。";
    }
    static void FixTyping(OWEpisodeStage stage)
    {
        string controllerPath=AssetRoot+"/Motion/RevisionMotions.controller";
        if(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)==null)AssetDatabase.CopyAsset("Assets/OldWebMap/Episode123/Motion/EpisodeMotions.controller",controllerPath);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);var state=controller.layers[0].stateMachine.states.Select(s=>s.state).First(s=>s.name=="Type");
        string clipPath=AssetRoot+"/Motion/TypingSeparated.anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if(clip==null){clip=Object.Instantiate((AnimationClip)state.motion);clip.name="TypingSeparated";AssetDatabase.CreateAsset(clip,clipPath);}
        foreach(string side in new[]{"Left","Right"})
        {
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),side+" Arm Front-Back"),AnimationCurve.Constant(0,4,0));
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),side+" Forearm Stretch"),AnimationCurve.Constant(0,4,.8f));
        }
        state.motion=clip;EditorUtility.SetDirty(clip);EditorUtility.SetDirty(controller);
        foreach(var actor in stage.actors){actor.animator.runtimeAnimatorController=controller;actor.animator.Rebind();}
        for(int i=0;i<2;i++)
        {
            var actor=stage.actors[i];actor.transform.localPosition=stage.actorPositions[i];actor.transform.localRotation=Quaternion.Euler(0,stage.actorYaws[i],0);
            actor.animator.Play("Type",0,.5f);actor.animator.SetFloat("PoseTime",.5f);actor.animator.Update(.02f);actor.animator.Update(0);Vector3 hands=(actor.leftHand.position+actor.rightHand.position)*.5f;
            var station=stage.contactScreens[i].transform;while(station!=null && station.name!="Contact workstation")station=station.parent;
            if(station==null)continue;station.localRotation=Quaternion.Euler(0,180,0);station.position=hands-new Vector3(0,.65f,.22f);
            foreach(Transform leg in station)if(leg.name=="Leg"){float height=.55f+station.position.y;leg.localScale=new Vector3(.055f,height,.055f);var pos=leg.localPosition;pos.y=height*.5f-station.position.y;leg.localPosition=pos;}
        }
    }
    public static object Save()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Only stopped v2 scene may be saved");
        Pose(0);foreach(var b in GameObject.Find("OWEpisode123").GetComponentsInChildren<UdonSharpBehaviour>(true))UdonSharpEditorUtility.CopyProxyToUdon(b);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return "Saved v2 opening state";
    }
    static void Pose(float time)
    {
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var stage=director.stage;stage.Sample(time);director.music.SampleVisual(time);if(director.montage!=null)director.montage.Sample(time);
        string[] names={"Idle","Type","Offer","Point","Cheer","Inspect","Walk"};
        foreach(var a in stage.actors){int pose=a.animator.GetInteger("Pose");float phase=a.animator.GetFloat("PoseTime");a.animator.Play(names[Mathf.Clamp(pose,0,6)],0,phase);a.animator.Update(.02f);a.animator.Update(0);}
        var follow=Object.FindObjectOfType<OWEpisodeCaption>();if(follow!=null){follow.transform.position=Camera.main.transform.position+Camera.main.transform.rotation*follow.headOffset;follow.transform.rotation=Camera.main.transform.rotation;}
        Canvas.ForceUpdateCanvases();
    }

    public static async Task<object> CaptureAuthoringViews()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stopped editor only");
        string dir=Path.Combine(DirectoryPath,"self-captures");Directory.CreateDirectory(dir);var scene=SceneView.GetWindow<SceneView>();scene.Show();scene.Focus();
        var pivot=scene.pivot;var rotation=scene.rotation;float size=scene.size;var selection=Selection.activeObject;var rows=new List<object>();
        var moving=GameObject.Find("OWEpisode123/MovingScenery");var desks=GameObject.Find("OWEpisode123/StablePeopleAndContact").GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Contact workstation").Select(t=>t.gameObject).ToArray();
        try
        {
            var stage=Object.FindObjectOfType<OWEpisodeStage>();
            for(int i=0;i<3;i++)
            {
                var actor=stage.actors[i%2];
                moving.SetActive(i==2);foreach(var desk in desks)desk.SetActive(i==2);foreach(var a in stage.actors)a.gameObject.SetActive(i==2 || a==actor);
                actor.animator.Play("Idle",0,.5f);actor.animator.Update(.02f);actor.animator.Update(0);
                Selection.activeGameObject=i==2?GameObject.Find("OWEpisode123"):actor.animator.gameObject;
                scene.in2DMode=false;scene.drawGizmos=true;
                Vector3 focus=i==2?new Vector3(0,1.6f,4):actor.transform.position+Vector3.up*.55f;
                scene.LookAt(focus,i==2?Quaternion.Euler(22,-25,0):actor.transform.rotation*Quaternion.Euler(7,180,0),i==2?11:1.05f,false,true);scene.Repaint();
                await Task.Delay(900);
                int width=1600,height=1000;var camera=scene.camera;var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);Texture2D image=null;
                string path=Path.Combine(dir,"current-unity-"+(i==0?"kipfel":i==1?"nemesis":"world-authoring")+".png");
                try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
                finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.DestroyImmediate(image);}
                rows.Add(new{path,width,height,kind="current project Unity SceneView camera capture; no desktop or editor chrome"});
            }
        }
        finally{moving.SetActive(true);foreach(var desk in desks)desk.SetActive(true);foreach(var a in Object.FindObjectsOfType<OWActorRig>(true))a.gameObject.SetActive(true);scene.LookAt(pivot,rotation,size,false,true);Selection.activeObject=selection;Pose(0);}
        File.WriteAllText(Path.Combine(dir,"capture-report.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));return JsonConvert.SerializeObject(rows);
    }

    static string WriteFrame(Camera camera,string name,int width=1600,int height=900)
    {
        string directory=Path.Combine(DirectoryPath,"frames");Directory.CreateDirectory(directory);string path=Path.Combine(directory,name+".png");
        var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24);Texture2D image=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.DestroyImmediate(image);}
        return path;
    }
    public static object CaptureAll()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Use stopped authored scene for references");
        var rows=new List<object>();float[] times={1.92f,8.96f,13.44f,23.04f,33.92f,43.52f,56.32f,65.28f,74.88f,85.12f,96.64f,108.8f,115.2f,127.36f};
        try{for(int i=0;i<times.Length;i++){Pose(times[i]);rows.Add(new{time=times[i],path=WriteFrame(Camera.main,"v2_"+i.ToString("00")),cut=Object.FindObjectOfType<OWEpisodeMontage>().sampledCut});}}
        finally{Save();}
        File.WriteAllText(Path.Combine(DirectoryPath,"capture-report.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));return JsonConvert.SerializeObject(rows);
    }
    public static object TypingChecks()
    {
        var rows=new List<object>();bool success=true;float minimum=100;
        try
        {
            foreach(var actor in Object.FindObjectOfType<OWEpisodeStage>().actors.Take(2))foreach(float phase in new[]{.05f,.25f,.5f,.75f,.95f})
            {
                actor.animator.Play("Type",0,phase);actor.animator.SetFloat("PoseTime",phase);actor.animator.Update(.02f);actor.animator.Update(0);
                var left=actor.transform.InverseTransformPoint(actor.leftHand.position);var right=actor.transform.InverseTransformPoint(actor.rightHand.position);float separation=right.x-left.x;
                bool crossed=separation<=.04f;minimum=Mathf.Min(minimum,separation);success&=!crossed;rows.Add(new{actor.appearanceId,phase,leftX=left.x,rightX=right.x,crossed,leftY=left.y,rightY=right.y,leftZ=left.z,rightZ=right.z});
            }
        }
        finally{Save();}
        string dir=Path.Combine(DirectoryPath,"reports");Directory.CreateDirectory(dir);var result=new{success,rows,minSeparation=minimum};File.WriteAllText(Path.Combine(dir,"typing-checks.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
        if(!success)throw new InvalidOperationException("Typing hands still cross: "+JsonConvert.SerializeObject(result));return JsonConvert.SerializeObject(result);
    }

    static VRC.Udon.UdonBehaviour Vm(UdonSharpBehaviour b){return UdonSharpEditorUtility.GetBackingUdonBehaviour(b);}
    static float Number(VRC.Udon.UdonBehaviour b,string name){return Convert.ToSingle(b.GetProgramVariable(name));}
    static bool Flag(VRC.Udon.UdonBehaviour b,string name){return Convert.ToBoolean(b.GetProgramVariable(name));}
    static string Report(string name,object value){string dir=Path.Combine(DirectoryPath,"reports");Directory.CreateDirectory(dir);string text=JsonConvert.SerializeObject(value,Formatting.Indented);File.WriteAllText(Path.Combine(dir,name),text);return text;}
    public static async Task<object> InteractionChecks()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var vm=Vm(director);var stage=Vm(director.stage);var montage=Vm(director.montage);vm.SendCustomEvent("StopAtStart");await Task.Delay(150);
        var handler=Object.FindObjectsOfType<OWEpisodeButton>(true).First(b=>b.name=="Start performance");var button=handler.GetComponent<UnityEngine.UI.Button>();var canvas=handler.GetComponentInParent<Canvas>();
        bool rayShapeHit=Physics.Raycast(handler.transform.position-canvas.transform.forward*.25f,canvas.transform.forward,out var hit,.35f,1<<17,QueryTriggerInteraction.Ignore) && hit.collider.gameObject==canvas.gameObject;
        bool target=button.onClick.GetPersistentTarget(0)==Vm(handler) && button.onClick.GetPersistentMethodName(0)=="SendCustomEvent";
        ExecuteEvents.Execute(handler.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);await Task.Delay(600);
        bool pointerClickPlayed=Flag(vm,"_playing") && Number(vm,"_elapsed")>.05f && target;
        bool panelHiddenWhilePlaying=director.audiencePanels.All(p=>!p.activeInHierarchy);float before=Number(vm,"_elapsed");vm.SendCustomEvent("Pause");await Task.Delay(500);
        bool pauseIgnored=Flag(vm,"_playing") && Number(vm,"_elapsed")>before+.2f && director.music.sources.All(a=>a.isPlaying);
        vm.SetProgramVariable("_offset",0f);vm.SetProgramVariable("_anchor",VRC.SDKBase.Networking.GetServerTimeInSeconds()-73.6);vm.SetProgramVariable("_playing",true);vm.SetProgramVariable("_revision",Convert.ToInt32(vm.GetProgramVariable("_revision"))+1);vm.SendCustomEvent("_onDeserialization");await Task.Delay(450);
        float reconstructed=Number(vm,"_elapsed");bool lateJoinRebuilt=reconstructed>=73.6f && reconstructed<75 && Convert.ToInt32(stage.GetProgramVariable("sampledCue"))==6 && Convert.ToInt32(montage.GetProgramVariable("sampledCut"))>=115 && director.audiencePanels.All(p=>!p.activeInHierarchy) && director.music.sources.All(a=>a.isPlaying);
        vm.SetProgramVariable("_offset",133.0f);vm.SetProgramVariable("_anchor",VRC.SDKBase.Networking.GetServerTimeInSeconds());vm.SetProgramVariable("_revision",Convert.ToInt32(vm.GetProgramVariable("_revision"))+1);await Task.Delay(450);
        bool endPanelShown=!Flag(vm,"_playing") && director.audiencePanels.All(p=>p.activeInHierarchy) && !director.music.sources.Any(a=>a.isPlaying);
        bool success=rayShapeHit && pointerClickPlayed && panelHiddenWhilePlaying && pauseIgnored && lateJoinRebuilt && endPanelShown;
        var result=new{success,rayShapeHit,pointerClickPlayed,panelHiddenWhilePlaying,pauseIgnored,lateJoinRebuilt,endPanelShown,reconstructed,scope="Real Udon/ClientSim pointerClick -> persistent SendCustomEvent -> Udon. Late join checks received-anchor local reconstruction, not real multiclient network transport."};
        string report=Report("interaction-v2.json",result);vm.SendCustomEvent("StopAtStart");if(!success)throw new InvalidOperationException(report);return report;
    }
    public static async Task<object> LiveValidation()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode");
        var director=Object.FindObjectOfType<OWEpisodeDirector>();var vm=Vm(director);var stage=Vm(director.stage);var montage=Vm(director.montage);var cuts=new HashSet<int>();var cues=new HashSet<int>();var transients=new List<object>();
        int maxSpread=0,maxRaw=0;float maxMontage=0,maxClockError=0,maxListener=0;var rms=new float[4];var output=new float[1024];var snapshots=new List<object>();
        vm.SendCustomEvent("StopAtStart");await Task.Delay(150);vm.SendCustomEvent("PlayFromConnect");var watch=System.Diagnostics.Stopwatch.StartNew();
        AudioSettings.GetDSPBufferSize(out int bufferLength,out int bufferCount);
        try
        {
            while(watch.Elapsed.TotalSeconds<136)
            {
                await Task.Delay(80);float time=Number(vm,"_elapsed");int cue=Convert.ToInt32(stage.GetProgramVariable("sampledCue")),cut=Convert.ToInt32(montage.GetProgramVariable("sampledCut"));
                if(cut<0 || cut>=208)throw new InvalidOperationException("Invalid montage cut "+cut+" at "+time);cuts.Add(cut);
                maxMontage=Mathf.Max(maxMontage,Number(vm,"lastMontageSampleMilliseconds"));
                if(time>.2f && time<132.8f)
                {
                    if(director.music.sources.Any(a=>!a.isPlaying))throw new InvalidOperationException("Music stopped at "+time);
                    int raw=int.MaxValue;int[] readings=null;
                    for(int tries=0;tries<3;tries++){var sample=director.music.sources.Select(a=>a.timeSamples).ToArray();int delta=sample.Max()-sample.Min();if(delta<raw){raw=delta;readings=sample;}}
                    maxRaw=Math.Max(maxRaw,raw);int spread=raw;
                    if(raw>960)
                    {
                        await Task.Delay(Math.Max(60,(int)(bufferLength/48f*2.2f)));var next=director.music.sources.Select(a=>a.timeSamples).ToArray();spread=next.Max()-next.Min();transients.Add(new{time,raw,readings,confirmed=spread,next});
                    }
                    maxSpread=Math.Max(maxSpread,spread);if(spread>960)throw new InvalidOperationException("Persistent stem drift "+spread);
                    maxClockError=Mathf.Max(maxClockError,Mathf.Abs(director.music.sources[0].time-time));
                    for(int ch=0;ch<4;ch++){director.music.sources[ch].GetOutputData(output,0);double sum=0;foreach(var v in output)sum+=v*v;rms[ch]=Mathf.Max(rms[ch],(float)Math.Sqrt(sum/output.Length));}
                    AudioListener.GetOutputData(output,0);double listener=0;foreach(var v in output)listener+=v*v;maxListener=Mathf.Max(maxListener,(float)Math.Sqrt(listener/output.Length));
                }
                if(!cues.Contains(cue) && time>director.stage.starts[cue]+.5f)
                {
                    cues.Add(cue);snapshots.Add(new{cue,time,cut,path=WriteFrame(Camera.main,"live_"+cue.ToString("00"))});
                }
                if(!Flag(vm,"_playing") && time>=133.11f)break;
                File.WriteAllText(Path.Combine(DirectoryPath,"live-progress.json"),JsonConvert.SerializeObject(new{time,cue,cut,cuesSeen=cues.Count,cutsSeen=cuts.Count,maxMontage}));
            }
            bool endStopped=!Flag(vm,"_playing") && Mathf.Abs(Number(vm,"_elapsed")-133.12f)<.02f && !director.music.sources.Any(a=>a.isPlaying);
            bool success=endStopped && cuts.Count==208 && cues.Count==12 && rms.All(r=>r>.0001f) && maxListener>.0001f;
            var result=new{success,duration=133.12,cuesSeen=cues.Count,cutsSeen=cuts.Count,fftChannels=4,fftBands=16,maxIntertrackSamples=maxSpread,maxRawSamples=maxRaw,maxMontageMs=maxMontage,maxAudioClockError=maxClockError,maxListenerRms=maxListener,maxSourceRms=rms,endStopped,bufferLength,bufferCount,transients,snapshots};
            string report=Report("live-v2.json",result);if(!success)throw new InvalidOperationException(report);return report;
        }
        catch(Exception error){Report("live-v2-failure.json",new{error=error.ToString(),cutsSeen=cuts.Count,cuesSeen=cues.Count,maxSpread,maxRaw,maxMontage});throw;}
        finally{vm.SendCustomEvent("StopAtStart");}
    }

    static Camera MovieCamera;static RenderTexture MovieTarget;static Texture2D MovieImage;static int MovieFrame;static string MovieDir;
    const int MovieFrames=3376;const int MovieFps=25;
    public static object StartMovie()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open stopped v2 scene");
        if(SessionState.GetBool("EpisodeV2Movie",false))throw new InvalidOperationException("Capture already running");
        MovieCamera=Camera.main;MovieFrame=0;MovieDir=Path.Combine(DirectoryPath,"movie-frames");Directory.CreateDirectory(MovieDir);MovieTarget=new RenderTexture(1600,900,24);MovieTarget.Create();MovieImage=new Texture2D(1600,900,TextureFormat.RGB24,false);
        SessionState.SetBool("EpisodeV2Movie",true);SessionState.SetInt("EpisodeV2Frame",0);SessionState.SetString("EpisodeV2MovieError","");EditorApplication.update+=MovieTick;return new{started=true,frames=MovieFrames,fps=MovieFps,directory=MovieDir};
    }
    static void MovieTick()
    {
        try
        {
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Capture interrupted");
            Pose(Mathf.Min(133.12f,MovieFrame/(float)MovieFps));var target=MovieCamera.targetTexture;var active=RenderTexture.active;
            try{MovieCamera.targetTexture=MovieTarget;MovieCamera.Render();RenderTexture.active=MovieTarget;MovieImage.ReadPixels(new Rect(0,0,1600,900),0,0);MovieImage.Apply(false);File.WriteAllBytes(Path.Combine(MovieDir,"frame_"+MovieFrame.ToString("00000")+".jpg"),MovieImage.EncodeToJPG(94));}
            finally{MovieCamera.targetTexture=target;RenderTexture.active=active;}
            MovieFrame++;SessionState.SetInt("EpisodeV2Frame",MovieFrame);if(MovieFrame>=MovieFrames)FinishMovie();
        }
        catch(Exception error){SessionState.SetString("EpisodeV2MovieError",error.ToString());FinishMovie();}
    }
    static void FinishMovie()
    {
        EditorApplication.update-=MovieTick;SessionState.SetBool("EpisodeV2Movie",false);if(MovieTarget!=null){MovieTarget.Release();Object.DestroyImmediate(MovieTarget);}if(MovieImage!=null)Object.DestroyImmediate(MovieImage);Save();
        File.WriteAllText(Path.Combine(MovieDir,"render-report.json"),JsonConvert.SerializeObject(new{complete=MovieFrame==MovieFrames,frames=MovieFrame,fps=MovieFps,showDuration=133.12,reviewDuration=135.04,error=SessionState.GetString("EpisodeV2MovieError","")},Formatting.Indented));
    }
    public static object MovieStatus(){return new{running=SessionState.GetBool("EpisodeV2Movie",false),frame=SessionState.GetInt("EpisodeV2Frame",0),total=MovieFrames,error=SessionState.GetString("EpisodeV2MovieError","")};}
    public static async Task<object> SdkBuildOnly()
    {
        Save();var panel=EditorWindow.GetWindow<VRCSdkControlPanel>();panel.Show();await Task.Delay(500);
        if(!VRCSdkControlPanel.TryGetBuilder<VRC.SDK3.Editor.IVRCSdkWorldBuilderApi>(out var api))throw new InvalidOperationException("SDK builder unavailable");
        var builder=(VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder)api;builder.RegisterBuilder(panel);if(!builder.IsValidBuilder(out string message))throw new InvalidOperationException(message);builder.Initialize();
        try{string path=await api.Build();string copy=Path.Combine(DirectoryPath,"OWM_Episode123_v2.vrcw");File.Copy(path,copy,true);return Report("sdk-build-v2.json",new{success=true,path=copy,bytes=new FileInfo(copy).Length,uploaded=false});}
        catch(Exception error){Report("sdk-build-v2.json",new{success=false,error=error.ToString(),errors=error.GetType().GetField("Errors")?.GetValue(error)});throw;}
    }

    public static object ViewpointChecks()
    {
        var camera=Camera.main;var pos=camera.transform.position;var rot=camera.transform.rotation;var rows=new List<object>();
        try
        {
            for(int h=0;h<3;h++)foreach(float eye in new[]{-.032f,.032f})
            {
                float height=h==0?.59f:h==1?1.35f:1.9f;camera.transform.position=new Vector3(pos.x+eye,height,pos.z);Pose(h==2?85.12f:33.92f);rows.Add(new{height,eye,path=WriteFrame(camera,"view_"+h+(eye<0?"_left":"_right"))});
            }
        }
        finally{camera.transform.position=pos;camera.transform.rotation=rot;Save();}
        var caption=Object.FindObjectOfType<OWEpisodeStage>().captionText;var material=caption.fontSharedMaterial;
        bool captionOverlay=material.shader.name=="TextMeshPro/Mobile/Distance Field Overlay" && material.renderQueue==5000;
        bool baselinePreserved=File.ReadAllBytes(Original).SequenceEqual(File.ReadAllBytes(Path.Combine(DirectoryPath,"user-saved-baseline.unity")));
        var shader=Shader.Find("OldWebMap/Page Surface");var variants=new ShaderVariantCollection();
        foreach(var keyword in new[]{"STEREO_INSTANCING_ON","STEREO_MULTIVIEW_ON"})variants.Add(new ShaderVariantCollection.ShaderVariant(shader,PassType.Normal,new[]{keyword}));variants.WarmUp();Object.DestroyImmediate(variants);
        var messages=ShaderUtil.GetShaderMessages(shader).Select(m=>new{m.message,severity=m.severity.ToString()}).ToArray();
        bool success=captionOverlay && baselinePreserved && !messages.Any(m=>m.severity=="Error");var report=new{success,captionOverlay,baselinePreserved,views=rows,stereoVariants="compiled and warmed",messages,headsetTested=false};
        string text=Report("viewpoints-v2.json",report);if(!success)throw new InvalidOperationException(text);return text;
    }

    public static object RepairCaptionMaterial()
    {
        var caption=Object.FindObjectOfType<OWEpisodeStage>().captionText;var material=caption.fontSharedMaterial;material.shader=Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");material.name="CaptionOverlay";material.renderQueue=5000;EditorUtility.SetDirty(material);
        for(int i=0;i<3;i++){var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(AssetRoot+"/StarLayer"+i+".asset");if(mesh!=null){mesh.name="StarLayer"+i;EditorUtility.SetDirty(mesh);}}
        Save();return new{shader=material.shader.name,material.renderQueue,source=AssetDatabase.GetAssetPath(material.shader)};
    }

    public static object RefineKeyword()
    {
        var montage=Object.FindObjectOfType<OWEpisodeMontage>();montage.logoRoot.localPosition=new Vector3(-2f,3.15f,4.5f);var label=montage.focusWord;label.transform.localPosition=new Vector3(0,3.45f,4.15f);label.fontSize=3.2f;
        string path=AssetRoot+"/Materials/FocusWord.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(label.fontSharedMaterial);material.name="FocusWord";AssetDatabase.CreateAsset(material,path);}
        material.EnableKeyword("OUTLINE_ON");material.SetFloat("_OutlineWidth",.18f);material.SetColor("_OutlineColor",new Color(.04f,.07f,.1f,1));label.fontSharedMaterial=material;EditorUtility.SetDirty(material);Save();return new{labelPosition=label.transform.position,font=label.fontSize};
    }

    public static object AlignRayPanel()
    {
        var canvas=Object.FindObjectsOfType<Canvas>(true).First(c=>c.name=="Ray UI - start and replay");canvas.transform.localRotation=Quaternion.Euler(0,-54,0);
        var button=canvas.GetComponentInChildren<UnityEngine.UI.Button>(true);var rows=new List<object>();bool success=true;
        foreach(float height in new[]{.59f,1.35f,1.9f})
        {
            Vector3 origin=new Vector3(0,height,-2.8f),delta=button.transform.position-origin;var ray=new Ray(origin,delta.normalized);
            bool hit=Physics.Raycast(ray,out var result,delta.magnitude+.1f,1<<17,QueryTriggerInteraction.Ignore) && result.collider.gameObject==canvas.gameObject;
            float facing=Vector3.Dot(-canvas.transform.forward,-delta.normalized);success&=hit && facing>.8f;rows.Add(new{height,hit,facing});
        }
        Save();string text=Report("ray-panel-facing.json",new{success,rows,yaw=-54});if(!success)throw new InvalidOperationException(text);return text;
    }

}
