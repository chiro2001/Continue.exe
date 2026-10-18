using System;
using System.IO;
using System.Linq;
using System.Text;
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

public static class OWEpisodeV4Montage
{
    const string Root="Assets/OldWebMap/Episode123/Revision4";
    const string ScenePath="Assets/Scenes/OWM_Episode123_v4.unity";
    const string ScorePath="AgentScripts/data/episode123-v4-score.json";
    static TMP_FontAsset Font;
    static string ReportDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"tmp/vrc-old-web-map/episode123-v4/montage");
    static readonly string[][] NaturalPhrases={
        new[]{"连上了","这声音我记得","hello!","好慢啊","终于进来了"},
        new[]{"楼主好人","收下了，谢谢","顶一下","签个到","找到组织了"},
        new[]{"字幕组辛苦了","这句笑死我了","感谢分享","留个种","补个档吧"},
        new[]{"前方高能","一起看才有意思","刚才谁在说话","有内味了","我也在这里"},
        new[]{"还能打开吗","链接失效了","有人留着吗","我这里还能用","谢谢补档"},
        new[]{"还有人在吗","我还在","慢慢来","记得留一份","辛苦维护的人了"}
    };
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');if(i<0)throw new InvalidOperationException(path);Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    static GameObject Child(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static Vector3 Vec(JToken a){return new Vector3((float)a[0],(float)a[1],(float)a[2]);}
    static Vector2 UV(JToken a,Vector2 fallback){return a is JArray && a.Count()==2?new Vector2((float)a[0],(float)a[1]):fallback;}
    static Color Hex(string value){Color c;ColorUtility.TryParseHtmlString(value,out c);return c;}
    static Material MaterialAsset(string name,string shaderName)
    {
        string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find(shaderName);
        if(shader==null || !shader.isSupported)throw new InvalidOperationException("V4 shader missing/unsupported: "+shaderName);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
    static void RenderStyle(Renderer r){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static Transform Plane(string name,Transform parent,Material mat,Vector3 position,Vector3 scale)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=mat;RenderStyle(renderer);return go.transform;
    }
    static TMP_Text Text(string name,string value,Transform parent,Vector3 position,float width,float height,float size,Color color)
    {
        var text=Child(name,parent).AddComponent<TextMeshPro>();text.font=Font;text.fontSharedMaterial=Font.material;text.text=value;text.fontSize=size;text.color=color;
        float min=size*Font.faceInfo.lineHeight/Mathf.Max(1,Font.faceInfo.pointSize)*.12f;
        text.transform.localPosition=position;text.rectTransform.sizeDelta=new Vector2(width,Mathf.Max(height,min));text.alignment=TextAlignmentOptions.Center;
        text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
    static void PersistFont()
    {
        Font.material.renderQueue=3020;if(Font.material.HasProperty("_ZTestMode"))Font.material.SetFloat("_ZTestMode",(float)CompareFunction.LessEqual);
        if(!AssetDatabase.Contains(Font.material))AssetDatabase.AddObjectToAsset(Font.material,Font);EditorUtility.SetDirty(Font.material);
        foreach(var atlas in Font.atlasTextures)if(atlas!=null){if(!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,Font);EditorUtility.SetDirty(atlas);}EditorUtility.SetDirty(Font);
    }
    static void PrepareFont(string visible)
    {
        Folder(Root+"/Fonts");string path=Root+"/Fonts/FocusAmbientDynamic4096.asset";Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(Font==null)
        {
            var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");if(source==null)throw new InvalidOperationException("NotoSansSC source TTF missing");
            Font=TMP_FontAsset.CreateFontAsset(source,64,7,GlyphRenderMode.SDFAA,4096,4096,AtlasPopulationMode.Dynamic,false);Font.name="V4FocusAmbientDynamic4096";Font.isMultiAtlasTexturesEnabled=false;
            AssetDatabase.CreateAsset(Font,path);PersistFont();
        }
        Font.isMultiAtlasTexturesEnabled=false;
        string required=new string(visible.Where(c=>!char.IsControl(c)&&!Font.HasCharacter(c)).Distinct().ToArray());
        if(required.Length>0){Font.TryAddCharacters(required,out string missing);if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("V4 font glyphs missing: "+missing);}
        PersistFont();
    }
    static Texture2D ImageTexture(JToken image)
    {
        string path=(string)image["path"]??Root+"/Images/"+(string)image["id"]+".png";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)throw new FileNotFoundException("Import the existing library image first",path);return texture;
    }
    static Mesh SaveMesh(string name,List<Vector3> vertices,List<int> triangles,List<Color> colors=null)
    {
        string path=Root+"/Meshes/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);if(colors!=null)mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }
    static Mesh CursorMesh()
    {
        var v=new List<Vector3>{new Vector3(-.3f,.5f,0),new Vector3(-.3f,-.4f,0),new Vector3(-.08f,-.18f,0),new Vector3(.09f,-.5f,0),new Vector3(.25f,-.41f,0),new Vector3(.07f,-.09f,0),new Vector3(.4f,-.09f,0)};
        return SaveMesh("V4 actual-image cursor",v,new List<int>{0,1,2,0,2,5,0,5,6,2,3,4,2,4,5});
    }
    static Transform MeshObject(string name,Transform parent,Mesh mesh,Material material)
    {
        var go=Child(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;RenderStyle(renderer);return go.transform;
    }
    static Dictionary<char,string[]> Glyphs()
    {
        string[] data={
            "A:01110/10001/10001/11111/10001/10001/10001","B:11110/10001/10001/11110/10001/10001/11110","C:01111/10000/10000/10000/10000/10000/01111",
            "D:11110/10001/10001/10001/10001/10001/11110","E:11111/10000/10000/11110/10000/10000/11111","F:11111/10000/10000/11110/10000/10000/10000",
            "G:01111/10000/10000/10111/10001/10001/01110","H:10001/10001/10001/11111/10001/10001/10001","I:11111/00100/00100/00100/00100/00100/11111",
            "J:00111/00010/00010/00010/10010/10010/01100","K:10001/10010/10100/11000/10100/10010/10001","L:10000/10000/10000/10000/10000/10000/11111",
            "M:10001/11011/10101/10101/10001/10001/10001","N:10001/11001/10101/10011/10001/10001/10001","O:01110/10001/10001/10001/10001/10001/01110",
            "P:11110/10001/10001/11110/10000/10000/10000","Q:01110/10001/10001/10001/10101/10010/01101","R:11110/10001/10001/11110/10100/10010/10001",
            "S:01111/10000/10000/01110/00001/00001/11110","T:11111/00100/00100/00100/00100/00100/00100","U:10001/10001/10001/10001/10001/10001/01110",
            "V:10001/10001/10001/10001/10001/01010/00100","W:10001/10001/10001/10101/10101/10101/01010","X:10001/10001/01010/00100/01010/10001/10001",
            "Y:10001/10001/01010/00100/00100/00100/00100","Z:11111/00001/00010/00100/01000/10000/11111",
            "0:01110/10001/10011/10101/11001/10001/01110","1:00100/01100/00100/00100/00100/00100/01110","2:01110/10001/00001/00010/00100/01000/11111",
            "3:11110/00001/00001/01110/00001/00001/11110","4:00010/00110/01010/10010/11111/00010/00010","5:11111/10000/10000/11110/00001/00001/11110",
            "6:01110/10000/10000/11110/10001/10001/01110","7:11111/00001/00010/00100/01000/01000/01000","8:01110/10001/10001/01110/10001/10001/01110","9:01110/10001/10001/01111/00001/00001/01110",
            "+:00000/00100/00100/11111/00100/00100/00000","-:00000/00000/00000/11111/00000/00000/00000","/:00001/00001/00010/00100/01000/10000/10000"};
        return data.ToDictionary(row=>row[0],row=>row.Substring(2).Split('/'));
    }
    static Mesh ExtrudedWord(string word,float cell,float thickness,float radius)
    {
        var glyphs=Glyphs();int columns=word.Length*6-1;var fill=new bool[columns,7];
        for(int letter=0;letter<word.Length;letter++)
        {
            if(word[letter]==' ')continue;if(!glyphs.TryGetValue(word[letter],out string[] rows))throw new InvalidOperationException("Unsupported native glyph: "+word[letter]);
            for(int y=0;y<7;y++)for(int x=0;x<5;x++)fill[letter*6+x,y]=rows[y][x]=='1';
        }
        var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
        Func<float,float,float,Vector3> bend=(x,y,z)=>{float angle=x/radius;return new Vector3(Mathf.Sin(angle)*(radius-z),y,radius-Mathf.Cos(angle)*(radius-z));};
        Action<Vector3,Vector3,Vector3,Vector3,Color> face=(a,b,c,d,color)=>{int first=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});for(int i=0;i<4;i++)colors.Add(color);};
        Color front=new Color(.78f,.93f,1,1),back=new Color(.11f,.27f,.52f,0),side=new Color(.08f,.35f,.73f,0);
        for(int x=0;x<columns;x++)for(int y=0;y<7;y++)if(fill[x,y])
        {
            float l=(x-columns*.5f)*cell,r=l+cell,top=(3.5f-y)*cell,bottom=top-cell,z0=-thickness*.5f,z1=thickness*.5f;
            face(bend(l,bottom,z0),bend(l,top,z0),bend(r,top,z0),bend(r,bottom,z0),front);
            face(bend(r,bottom,z1),bend(r,top,z1),bend(l,top,z1),bend(l,bottom,z1),back);
            // Side walls only along the outside contour, never between filled cells.
            if(x==0 || !fill[x-1,y])face(bend(l,bottom,z1),bend(l,top,z1),bend(l,top,z0),bend(l,bottom,z0),side);
            if(x==columns-1 || !fill[x+1,y])face(bend(r,bottom,z0),bend(r,top,z0),bend(r,top,z1),bend(r,bottom,z1),side);
            if(y==0 || !fill[x,y-1])face(bend(l,top,z0),bend(l,top,z1),bend(r,top,z1),bend(r,top,z0),side);
            if(y==6 || !fill[x,y+1])face(bend(l,bottom,z1),bend(l,bottom,z0),bend(r,bottom,z0),bend(r,bottom,z1),side);
        }
        return SaveMesh("Extruded cylindrical "+word.Replace(' ','_').Replace('/','_'),vertices,triangles,colors);
    }
    static int RegionAt(float time){return time<81.92f?0:time<88.32f?1:time<104.96f?2:3;}
    static JArray Comments(JObject score,OWEpisodeMusic music)
    {
        if(score["comments"] is JArray supplied)return new JArray(supplied.OrderBy(c=>(float)(c["time"]??c["start"])));
        var list=new JArray();var times=music.notes0.Concat(music.notes2).Concat(music.notes3).Distinct().OrderBy(t=>t);float next=5.12f;int index=0;
        foreach(float time in times)
        {
            if(time<next || time>=132)continue;int era=time<10.24f?0:time<30.72f?1:time<61.44f?2:time<102.4f?3:time<123?4:5;
            string[] pool=NaturalPhrases[era];list.Add(new JObject{{"time",time},{"text",pool[index%pool.Length]},{"region",RegionAt(time)},{"channel",index%4},{"duration",3.6f}});
            float gap=era==0?1.28f:era==1?.8f:era==2?.56f:time<81.92f?.32f:era==3?.64f:era==4?.96f:1.6f;next=time+gap;index++;
        }
        return list;
    }
    static JArray Keylines(JObject score)
    {
        if(score["keylines"] is JArray rows)return new JArray(rows.OrderBy(r=>(float)r["start"]));
        return JArray.Parse("[{\"start\":19.2,\"end\":23.2,\"text\":\"工具，也有作者\",\"region\":0,\"style\":\"white_on_black\"},{\"start\":45.12,\"end\":49.12,\"text\":\"一条字幕，许多双手\",\"region\":0,\"style\":\"black_on_white\"},{\"start\":112.64,\"end\":116.64,\"text\":\"入口仍在，条件变了\",\"region\":3,\"style\":\"white_on_black\"},{\"start\":128.64,\"end\":132.64,\"text\":\"还有人在维护\",\"region\":3,\"style\":\"white_on_black\"}]");
    }
    static void CheckScore(JObject score)
    {
        var events=(JArray)score["events"];var ids=new HashSet<string>(((JArray)score["images"]).Select(i=>(string)i["id"]));int slots=(int?)score["slots"]??20;
        if(slots<4 || slots>24 || events==null || events.Count==0)throw new InvalidOperationException("V4 score requires 4-24 persistent event slots and events");
        int previousSlot=-1;float previousEnd=-1;int focus=0;
        foreach(var e in events)
        {
            int slot=(int)e["slot"];float start=(float)e["start"],end=(float)e["end"];int region=(int?)e["region"]??0;
            if(slot<0 || slot>=slots || slot<previousSlot || slot==previousSlot && start<previousEnd-.0001f || end<=start || region<0 || region>3)throw new InvalidOperationException("V4 event slot, overlap or region invalid");
            if(!ids.Contains((string)e["image"]) || e["from"]?.Count()!=3 || e["to"]?.Count()!=3)throw new InvalidOperationException("V4 event image/geometry invalid");
            if((float)e["width"]<=0 || (float)e["height"]<=0)throw new InvalidOperationException("V4 image dimensions invalid");
            if((string)e["role"]=="focus")
            {
                float enter=(float?)e["enter"]??.45f,exit=(float?)e["exit"]??.55f,hold=end-start-enter-exit;
                if(hold<2.4f-.001f || hold>5.0f+.001f || e["hold"]!=null && Mathf.Abs(hold-(float)e["hold"])>.05f)throw new InvalidOperationException("Focus needs an explicit 2.4-5 second hold plus enter/exit, slot "+slot+" start "+start);
                focus++;
            }
            if(e["cursor_uv"] is JArray uv && (uv.Count!=2 || (float)uv[0]<0 || (float)uv[0]>1 || (float)uv[1]<0 || (float)uv[1]>1))throw new InvalidOperationException("Cursor UV outside actual source image");
            previousSlot=slot;previousEnd=end;
        }
        if(focus==0)throw new InvalidOperationException("V4 score needs recognizable focus anchors");
    }
    static void BuildAmbient(OWV4Montage m,Transform parent,JObject score,JArray images,JArray events)
    {
        var ids=images.Select(i=>(string)i["id"]).ToArray();
        JArray rows=score["ambient"] is JArray supplied?supplied:new JArray(events.OrderBy(e=>(float)e["start"]).GroupBy(e=>(string)e["image"]).Take(48).Select(g=>new JObject{{"image",g.Key},{"reveal",(float)g.First()["start"]},{"channel",((int?)g.First()["channel"]??0)}}));
        if(rows.Count==0 || rows.Count>48)throw new InvalidOperationException("Ambient history requires 1-48 existing images");
        m.ambientPerRegion=48;m.ambientRegions=new Transform[4];m.ambientWindows=new Transform[192];m.ambientPositions=new Vector3[192];m.ambientScales=new Vector3[192];m.ambientReveals=new float[48];m.ambientChannels=new int[48];
        for(int i=0;i<48;i++)
        {
            var row=rows[i%rows.Count];if(Array.IndexOf(ids,(string)row["image"])<0)throw new InvalidOperationException("Ambient image is not in the real library: "+row["image"]);
            m.ambientReveals[i]=(float?)row["reveal"]??0;m.ambientChannels[i]=Mathf.Clamp((int?)row["channel"]??i%4,0,3);
        }
        for(int region=0;region<4;region++)
        {
            var group=Child("Persistent history region "+region,parent).transform;group.localPosition=m.journey.RegionOffset(region);m.ambientRegions[region]=group;
            for(int i=0;i<48;i++)
            {
                var row=rows[i%rows.Count];int image=Array.IndexOf(ids,(string)row["image"]);string id=ids[image];
                var mat=MaterialAsset("V4 history "+id,"OldWebMap/V4/History Image");mat.mainTexture=m.imageTextures[image];mat.SetColor("_Tint",Color.white);mat.SetFloat("_Exposure",.74f);
                int band=i/8,voice=m.ambientChannels[i];float sign=voice<2?-1:1,angle=sign*(38+band*20+i%4*6-(voice==1||voice==2?7:0))*Mathf.Deg2Rad,radius=5.6f+band*.38f+(i%2)*.1f;
                var position=new Vector3(Mathf.Sin(angle)*radius,1.45f+(i/2%4)*.72f,Mathf.Cos(angle)*radius);
                float width=1.85f+i%3*.15f,height=width*(float)images[image]["height"]/(float)images[image]["width"];
                if(height>1.6f){width*=1.6f/height;height=1.6f;}
                var plane=Plane("Retained original "+i.ToString("00")+" "+id,group,mat,position,new Vector3(width,height,1));
                plane.localRotation=Quaternion.LookRotation((position-new Vector3(0,1.7f,0)).normalized,Vector3.up);
                int index=region*48+i;m.ambientWindows[index]=plane;m.ambientPositions[index]=position;m.ambientScales[index]=plane.localScale;plane.gameObject.SetActive(false);
            }
        }
    }
    static void BuildTypography(OWV4Montage m,Transform parent)
    {
        m.typeRegions=new Transform[4];m.typeWords=new Transform[12];m.typeMaterial=MaterialAsset("V4 true extruded blue white chrome","OldWebMap/V4/Extruded Chrome Typography");m.typeMaterial.SetFloat("_Grid",.62f);
        Mesh[] words={ExtrudedWord("EDGE",.105f,.18f,2.6f),ExtrudedWord("+ FC14",.065f,.13f,2.3f),ExtrudedWord("RHINO OF TORMENT",.032f,.10f,1.8f)};
        for(int region=0;region<4;region++)
        {
            var stage=Child("Actual extruded EDGE FC14 song credit region "+region,parent).transform;stage.localPosition=m.journey.RegionOffset(region)+new Vector3(0,4.2f,6.6f);m.typeRegions[region]=stage;
            for(int word=0;word<3;word++)
            {
                var part=MeshObject(word==0?"EDGE extruded contour":word==1?"FC14 extruded contour":"RHINO OF TORMENT extruded contour",stage,words[word],m.typeMaterial);
                part.localPosition=new Vector3(0,word==0?.3f:word==1?-.36f:-.78f,word==0?0:word==1?.08f:.14f);m.typeWords[region*3+word]=part;
            }
        }
    }
    static void BuildComments(OWV4Montage m,Transform parent,JArray rows)
    {
        m.commentsPerRegion=16;m.commentRoots=new Transform[64];m.commentTexts=new TMP_Text[64];
        m.commentTimes=rows.Select(c=>(float)(c["time"]??c["start"])).ToArray();m.commentDurations=rows.Select(c=>Mathf.Clamp((float?)c["duration"]??3.6f,2.4f,5)).ToArray();m.commentStrings=rows.Select(c=>(string)c["text"]??"").ToArray();
        m.commentRegions=rows.Select(c=>Mathf.Clamp((int?)c["region"]??RegionAt((float)(c["time"]??c["start"])),0,3)).ToArray();m.commentChannels=rows.Select(c=>Mathf.Clamp((int?)c["channel"]??0,0,3)).ToArray();
        for(int region=0;region<4;region++)
        {
            var group=Child("Era conversation region "+region,parent).transform;group.localPosition=m.journey.RegionOffset(region);
            for(int slot=0;slot<16;slot++)
            {
                int index=region*16+slot;var text=Text("Natural short conversation "+slot,"",group,Vector3.zero,4.5f,.24f,.94f,Color.white);text.alignment=TextAlignmentOptions.Left;
                m.commentRoots[index]=text.transform;m.commentTexts[index]=text;text.gameObject.SetActive(false);
            }
        }
    }
    static void BuildKeylines(OWV4Montage m,Transform parent,JArray rows)
    {
        m.keyStarts=rows.Select(r=>(float)r["start"]).ToArray();m.keyEnds=rows.Select(r=>(float)r["end"]).ToArray();m.keyStrings=rows.Select(r=>(string)r["text"]).ToArray();m.keyRegions=rows.Select(r=>(int?)r["region"]??RegionAt((float)r["start"])).ToArray();m.keyStyles=rows.Select(r=>(string)r["style"]=="black_on_white"?1:0).ToArray();
        m.keyRoots=new Transform[4];m.keyTexts=new TMP_Text[4];m.keyPanels=new Renderer[4];
        m.blackPanel=MaterialAsset("V4 keyline black paper","OldWebMap/V4/History Image");m.blackPanel.SetColor("_Tint",Color.black);m.blackPanel.SetFloat("_Exposure",1);
        m.whitePanel=MaterialAsset("V4 keyline white paper","OldWebMap/V4/History Image");m.whitePanel.SetColor("_Tint",Color.white);m.whitePanel.SetFloat("_Exposure",1);
        for(int region=0;region<4;region++)
        {
            var root=Child("Legible key sentence region "+region,parent).transform;root.localPosition=m.journey.RegionOffset(region)+new Vector3(0,3.55f,4.25f);m.keyRoots[region]=root;
            m.keyPanels[region]=Plane("Contrast plate",root,m.blackPanel,Vector3.zero,new Vector3(5.8f,.78f,1)).GetComponent<Renderer>();
            m.keyTexts[region]=Text("Key sentence","",root,new Vector3(0,0,-.035f),5.5f,.68f,3.15f,Color.white);root.gameObject.SetActive(false);
        }
    }
    const string LayoutPath="AgentScripts/data/episode123-v4-support-layout.json";
    static OWV4Montage ExistingMontage()
    {
        var scene=SceneManager.GetActiveScene();
        var m=Resources.FindObjectsOfTypeAll<OWV4Montage>().FirstOrDefault(value=>value.gameObject.scene==scene);
        if(m==null)throw new InvalidOperationException("The current V4 scene has no montage");return m;
    }
    static Vector3 SphereDirection(float yaw,float pitch)
    {
        yaw*=Mathf.Deg2Rad;pitch*=Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch));
    }
    static Vector3 FitOriginal(JToken image,float width,float height)
    {
        float aspect=(float)image["height"]/(float)image["width"];
        float w=width,h=w*aspect;if(h>height){w*=height/h;h=height;}
        return new Vector3(w,h,1);
    }
    static object ApplyPresentationLayoutInternal(OWV4Montage m,JObject score)
    {
        if(m.journey==null || m.journey.worldFrame==null || m.transform.parent!=m.journey.worldFrame)throw new InvalidOperationException("Montage must be directly local to the shared world frame");
        if(m.transform.localPosition.sqrMagnitude>.000001f || Quaternion.Angle(m.transform.localRotation,Quaternion.identity)>.001f || Vector3.Distance(m.transform.localScale,Vector3.one)>.001f)throw new InvalidOperationException("Montage root must retain the shared frame's identity local transform");
        if(m.ambientPerRegion!=48 || m.ambientWindows.Length!=192)throw new InvalidOperationException("Keep the existing four 48-window history regions");
        var layout=JObject.Parse(File.ReadAllText(LayoutPath));var images=(JArray)score["images"];var events=(JArray)score["events"];var ids=images.Select(i=>(string)i["id"]).ToArray();
        if(images.Count!=m.imageTextures.Length || events.Count!=m.starts.Length)throw new InvalidOperationException("Existing montage and source score disagree; refresh the source data first");
        m.layoutReferenceEye=Vec(layout["reference_eye"]);m.layoutViewerRadius=(float?)layout["viewer_position_radius_metres"]??.3f;m.usePresentationLayout=true;
        // Each source keeps its aspect. Angular cells reserve the full 1.015
        // pulse, 6.5 cm motion/arrival envelope and a 30 cm viewer envelope.
        // The history grid extends from front-side to rear in both directions.
        for(int region=0;region<4;region++){m.ambientRegions[region].localPosition=m.journey.RegionOffset(region);m.ambientRegions[region].localRotation=Quaternion.identity;m.ambientRegions[region].localScale=Vector3.one;}
        int[] sideOrdinal={0,0};var ambient=(JArray)score["ambient"];
        if(ambient==null || ambient.Count!=48)throw new InvalidOperationException("Presentation layout expects the authored 48 history rows");
        for(int i=0;i<48;i++)
        {
            int image=Array.IndexOf(ids,(string)ambient[i]["image"]);if(image<0)throw new InvalidOperationException("Unknown history source");
            int side=m.ambientChannels[i]<2?0:1,ordinal=sideOrdinal[side]++,column=ordinal/4,row=ordinal%4;
            if(column>=6)throw new InvalidOperationException("History channel imbalance exceeds its side's 24 angular cells");
            float yaw=(side==0?-1:1)*(48+column*24),pitch=-6+row*14,radius=9.5f+column*.26f+row*.06f;
            Vector3 pos=m.layoutReferenceEye+SphereDirection(yaw,pitch)*radius,scale=FitOriginal(images[image],1.52f,1.04f);
            // At higher elevations longitude spans grow; reduce both image
            // dimensions together, preserving every original source aspect.
            scale=new Vector3(scale.x*Mathf.Cos(pitch*Mathf.Deg2Rad),scale.y*Mathf.Cos(pitch*Mathf.Deg2Rad),1);
            Quaternion rotation=Quaternion.LookRotation((pos-m.layoutReferenceEye).normalized,Vector3.up);
            for(int region=0;region<4;region++)
            {
                int index=region*48+i;var plane=m.ambientWindows[index];m.ambientPositions[index]=pos;m.ambientScales[index]=scale;
                plane.localPosition=pos;plane.localScale=scale;plane.localRotation=rotation;
                var material=plane.GetComponent<Renderer>().sharedMaterial;material.SetFloat("_Exposure",(float?)layout["history_exposure"]??.50f);EditorUtility.SetDirty(material);
            }
        }
        // Fast images occupy separate interleaved angular columns. Allocate a
        // lane by actual event interval, not by slot or image aspect; 24 reusable
        // lanes on either side can carry all existing score events.
        m.eventRotations=Enumerable.Repeat(Quaternion.identity,m.starts.Length).ToArray();
        var laneEnds=new float[4,2,24];for(int r=0;r<4;r++)for(int s=0;s<2;s++)for(int lane=0;lane<24;lane++)laneEnds[r,s,lane]=-100;
        int reassigned=0;var laneRows=new List<object>();
        foreach(int e in Enumerable.Range(0,events.Count).Where(e=>m.roles[e]==0).OrderBy(e=>m.starts[e]).ThenBy(e=>(int)events[e]["slot"]))
        {
            int side=m.channels[e]<2?0:1,region=m.regions[e],preferredRow=m.channels[e]==0 || m.channels[e]==3?1:2,best=-1;float bestCost=float.PositiveInfinity;
            for(int lane=0;lane<24;lane++)
            {
                if(laneEnds[region,side,lane]>m.starts[e]+.0001f)continue;
                int col=lane/4,row=lane%4;float cost=col*3+Mathf.Abs(row-preferredRow)+((e+lane)%3)*.015f;
                if(cost<bestCost){best=lane;bestCost=cost;}
            }
            if(best<0)throw new InvalidOperationException("No nonoverlapping fast lane at "+m.starts[e]);
            laneEnds[region,side,best]=m.ends[e];int column=best/4,height=best%4;float azimuth=column==0?34:36+column*24;
            var direction=SphereDirection((side==0?-1:1)*azimuth,-6+height*14);Vector3 origin=m.layoutReferenceEye+m.journey.RegionOffset(region);
            m.from[e]=origin+direction*15.9f;m.to[e]=origin+direction*13.7f;m.away[e]=m.to[e];m.eventRotations[e]=Quaternion.LookRotation(direction,Vector3.up);
            Vector3 size=FitOriginal(images[m.images[e]],Mathf.Min(1.38f,(float)events[e]["width"]),1.02f);float elevationScale=Mathf.Cos((-6+height*14)*Mathf.Deg2Rad);size=new Vector3(size.x*elevationScale,size.y*elevationScale,1);m.widths[e]=size.x;m.heights[e]=size.y;reassigned++;
            laneRows.Add(new{index=e,slot=(int)events[e]["slot"],image=ids[m.images[e]],start=m.starts[e],end=m.ends[e],region,side,lane=best,yaw=(side==0?-1:1)*azimuth,pitch=-6+height*14,width=size.x,height=size.y});
        }
        var authored=new List<JObject>();
        foreach(JObject source in (JArray)layout["rows"])
        {
            var row=(JObject)source.DeepClone();int focus=-1;
            if((string)row["kind"]=="focus")
            {
                focus=Enumerable.Range(0,m.starts.Length).Single(e=>m.roles[e]==1 && ids[m.images[e]]==(string)row["focus_image"]);
                // Enter and leave paths stay clear. Support opens when the main
                // image settles, and stays throughout the entire readable hold.
                float holdMargin=(float?)layout["focus_hold_margin_seconds"]??.02f;
                row["start"]=m.starts[focus]+m.enters[focus]-holdMargin;row["end"]=m.ends[focus]-m.exits[focus]+holdMargin;row["region"]=m.regions[focus];
            }
            else row["region"]=RegionAt(((float)row["start"]+(float)row["end"])*.5f);
            row["focus_event"]=focus;authored.Add(row);
        }
        authored=authored.OrderBy(row=>(float)row["start"]).ToList();
        for(int i=0;i<authored.Count;i++)if(i>0 && (float)authored[i]["start"]<(float)authored[i-1]["end"]-.0001f)throw new InvalidOperationException("Central support rows must not overlap");
        var supportRoot=m.transform.Find("Central related originals");if(supportRoot==null)supportRoot=Child("Central related originals",m.transform).transform;
        supportRoot.localPosition=Vector3.zero;supportRoot.localRotation=Quaternion.identity;supportRoot.localScale=Vector3.one;
        m.supportPlanes=new Transform[4];m.supportMaterials=new Material[4];
        for(int slot=0;slot<4;slot++)
        {
            var material=MaterialAsset("V4 central related original "+slot,"OldWebMap/V4/History Image");material.SetColor("_Tint",Color.white);material.SetFloat("_Exposure",(float?)layout["support_exposure"]??.56f);
            var plane=supportRoot.Find("Related original "+slot);if(plane==null)plane=Plane("Related original "+slot,supportRoot,material,Vector3.zero,Vector3.one);
            plane.GetComponent<Renderer>().sharedMaterial=material;plane.gameObject.SetActive(false);m.supportPlanes[slot]=plane;m.supportMaterials[slot]=material;
        }
        int count=authored.Count;m.supportStarts=new float[count];m.supportEnds=new float[count];m.supportCounts=new int[count];m.supportKinds=new int[count];m.supportFocusEvents=new int[count];
        m.supportImages=new int[count*4];m.supportPositions=new Vector3[count*4];m.supportScales=new Vector3[count*4];m.supportProtectedPositions=new Vector3[count];m.supportProtectedScales=new Vector3[count];var supportRows=new List<object>();
        for(int row=0;row<count;row++)
        {
            var source=authored[row];int focus=(int)source["focus_event"],region=(int)source["region"];string kind=(string)source["kind"];var selected=(JArray)source["images"];
            if(selected.Count<2 || selected.Count>4 || selected.Select(i=>(string)i).Distinct().Count()!=selected.Count)throw new InvalidOperationException("Each central scene needs 2-4 distinct related originals");
            Vector3 anchor=focus>=0?m.to[focus]:m.journey.RegionOffset(region)+new Vector3(0,2.5f,(float?)source["protect_depth"]??5.9f);
            Vector3 protection=focus>=0?new Vector3(m.widths[focus]+.12f,m.heights[focus]+.35f,1):new Vector3((float?)source["protect_width"]??5.4f,(float?)source["protect_height"]??3.5f,1);
            m.supportStarts[row]=(float)source["start"];m.supportEnds[row]=(float)source["end"];m.supportCounts[row]=selected.Count;m.supportKinds[row]=focus>=0?0:kind=="operation"?1:kind=="cast"?2:3;m.supportFocusEvents[row]=focus;
            m.supportProtectedPositions[row]=anchor;m.supportProtectedScales[row]=protection;
            var selectedRows=new List<object>();
            for(int slot=0;slot<selected.Count;slot++)
            {
                string id=(string)selected[slot];int image=Array.IndexOf(ids,id);if(image<0 || focus>=0 && image==m.images[focus])throw new InvalidOperationException("Support must reuse a related library image distinct from its main focus: "+id);
                int index=row*4+slot;Vector3 size=FitOriginal(images[image],focus>=0?1.32f:1.16f,focus>=0?.86f:.78f);
                float depth=anchor.z+1.08f,originZ=m.layoutReferenceEye.z+m.journey.RegionOffset(region).z,ratio=(depth-originZ)/Mathf.Max(.5f,anchor.z-originZ);
                float x=(protection.x*.5f+.42f)*ratio+size.x*.5f;
                Vector3 position=new Vector3(anchor.x+(slot%2==0?-x:x),anchor.y+(slot==0?.14f:-.2f),depth);
                if(slot>=2)
                {
                    // Short focus images have room below the subject; the small
                    // lower card remains well above the contrast plate's rays.
                    if(focus>=0)
                    {
                        size=FitOriginal(images[image],1.05f,m.heights[focus]<1.4f?.6f:.48f);
                        position=new Vector3(0,m.heights[focus]<1.4f?1.42f:.83f,depth);
                        if(slot==3)position.x=1.3f;
                    }
                    else
                    {
                        size=FitOriginal(images[image],1.05f,.6f);
                        position=new Vector3(anchor.x+(slot%2==0?-x:x),anchor.y-1.4f,depth);
                    }
                }
                m.supportImages[index]=image;m.supportPositions[index]=position;m.supportScales[index]=size;
                selectedRows.Add(new{id,index=image,year=(string)images[image]["display_year"],position=new[]{position.x,position.y,position.z},size=new[]{size.x,size.y},path=(string)images[image]["path"]});
            }
            supportRows.Add(new{row,start=m.supportStarts[row],end=m.supportEnds[row],kind,region,focus,theme=(string)source["theme"],images=selectedRows});
        }
        // Persist the expensive stable-region bounds, including every scored
        // fast path. Starting playback does not run thousands of trig calls.
        m.authoredAmbientBounds=new Vector4[48];m.authoredEventBounds=new Vector4[m.starts.Length];
        for(int i=0;i<48;i++)m.authoredAmbientBounds[i]=AngularBox(m.ambientPositions[i],m.ambientWindows[i].localRotation,m.ambientScales[i]*1.015f,m.layoutReferenceEye,m.layoutViewerRadius+.065f);
        for(int e=0;e<m.starts.Length;e++)if(m.roles[e]==0)m.authoredEventBounds[e]=AngularBox(m.to[e],m.eventRotations[e],new Vector3(m.widths[e],m.heights[e]+.3f,1)*1.02f,m.layoutReferenceEye+m.journey.RegionOffset(m.regions[e]),m.layoutViewerRadius+.08f);
        m.ResetPresentationLayoutCache();m.Seek(0);UdonSharpEditorUtility.CopyProxyToUdon(m);EditorUtility.SetDirty(m);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(m));EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var result=new{layout=LayoutPath,sourceImages=images.Count,newIndependentImageSources=0,persistentSupportQuads=4,supportSceneCount=count,focusSupportScenes=m.supportKinds.Count(kind=>kind==0),operationSupportScenes=m.supportKinds.Count(kind=>kind==1),castSupportScenes=m.supportKinds.Count(kind=>kind==2),maintenanceSupportScenes=m.supportKinds.Count(kind=>kind==3),fastEventsReassigned=reassigned,persistentHistoryQuads=192,historyRendererBudget=48,referenceEye=new[]{m.layoutReferenceEye.x,m.layoutReferenceEye.y,m.layoutReferenceEye.z},viewerPositionRadius=m.layoutViewerRadius,rotationOfViewerUnrestricted=true,maximumHistoryPulseScale=1.015f,historyMotionEnvelope=.065f,sceneSaved=false,supportRows,fastLanes=laneRows};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"presentation-layout.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
    public static object ApplyPresentationLayout()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Apply layout in the stopped V4 scene");
        Folder(Root+"/Materials");var score=JObject.Parse(File.ReadAllText(ScorePath));CheckScore(score);var m=ExistingMontage();ApplyPresentationLayoutInternal(m,score);AssetDatabase.SaveAssets();
        return JsonConvert.SerializeObject(new{applied=true,persistentSupportQuads=m.supportPlanes.Length,supportScenes=m.supportStarts.Length,focusSupportScenes=m.supportKinds.Count(kind=>kind==0),fastEvents=m.roles.Count(role=>role==0),persistentHistoryQuads=m.ambientWindows.Length,historyBudget=48,newIndependentImages=0,sceneSaved=false,report=Path.Combine(ReportDirectory,"presentation-layout.json")});
    }
    public static object Build()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Build V4 montage in the stopped V4 scene");
        var journey=Object.FindObjectOfType<OWV4Journey>();var director=Object.FindObjectOfType<OWEpisodeDirector>();if(journey==null || journey.worldFrame==null || director==null || director.music==null)throw new InvalidOperationException("Create Journey.worldFrame and the real music director first");
        Folder(Root+"/Materials");Folder(Root+"/Meshes");var score=JObject.Parse(File.ReadAllText(ScorePath));CheckScore(score);
        var events=(JArray)score["events"];var images=(JArray)score["images"];var ids=images.Select(i=>(string)i["id"]).ToArray();
        var comments=Comments(score,director.music);var keys=Keylines(score);var visible=new StringBuilder("0123456789 ·");
        foreach(var e in events){visible.Append((string)e["year"]);visible.Append((string)e["web_title"]);}
        foreach(var i in images){visible.Append((string)i["display_year"]);visible.Append((string)i["web_title"]);}
        foreach(var c in comments)visible.Append((string)c["text"]);foreach(var k in keys)visible.Append((string)k["text"]);PrepareFont(visible.ToString());
        var old=journey.worldFrame.Find("Revision4Montage");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var root=Child("Revision4Montage",journey.worldFrame).transform;var m=root.gameObject.AddUdonSharpComponent<OWV4Montage>();m.journey=journey;m.director=director;m.music=director.music;m.overloadAt=(float?)score["overload_start"]??61.44f;
        m.imageTextures=images.Select(ImageTexture).ToArray();
        m.starts=events.Select(e=>(float)e["start"]).ToArray();m.ends=events.Select(e=>(float)e["end"]).ToArray();m.enters=events.Select(e=>(float?)e["enter"]??.45f).ToArray();m.exits=events.Select(e=>(float?)e["exit"]??.55f).ToArray();m.holds=events.Select((e,i)=>(float?)e["hold"]??m.ends[i]-m.starts[i]-m.enters[i]-m.exits[i]).ToArray();
        m.roles=events.Select(e=>(string)e["role"]=="focus"?1:0).ToArray();m.regions=events.Select(e=>(int?)e["region"]??0).ToArray();m.images=events.Select(e=>Array.IndexOf(ids,(string)e["image"])).ToArray();
        m.resultImages=events.Select(e=>e["result_image"]!=null?Array.IndexOf(ids,(string)e["result_image"]):-1).ToArray();m.channels=events.Select(e=>(int?)e["channel"]??-1).ToArray();
        m.from=events.Select(e=>Vec(e["from"])).ToArray();m.to=events.Select(e=>Vec(e["to"])).ToArray();m.away=events.Select(e=>e["out"] is JArray?Vec(e["out"]):Vec(e["to"])+new Vector3(Vec(e["to"]).x>=0?5.5f:-5.5f,.2f,1.5f)).ToArray();
        m.widths=events.Select(e=>(float)e["width"]).ToArray();m.heights=events.Select(e=>(float)e["height"]).ToArray();
        m.years=events.Select((e,i)=>(string)e["year"]??(string)images[m.images[i]]["display_year"]??"").ToArray();m.webTitles=events.Select((e,i)=>(string)e["web_title"]??(string)images[m.images[i]]["web_title"]??"").ToArray();
        m.hasCursor=events.Select(e=>e["cursor_uv"] is JArray).ToArray();m.cursorUV=events.Select(e=>UV(e["cursor_uv"],new Vector2(.5f,.5f))).ToArray();m.cursorFrom=events.Select(e=>UV(e["cursor_from_uv"],new Vector2(.08f,.12f))).ToArray();m.cursorAt=events.Select(e=>(float?)e["cursor_at"]??1.0f).ToArray();m.cursorDetach=events.Select(e=>(bool?)e["cursor_detach"]??false).ToArray();
        int count=(int?)score["slots"]??20;m.slotFirst=new int[count];m.slotCount=new int[count];m.windows=new Transform[count];m.imagePlanes=new Transform[count];m.imageRenderers=new Renderer[count];m.windowMaterials=new Material[count];m.cursors=new Transform[count];m.dateTexts=new TMP_Text[count];m.dateTransforms=new Transform[count];
        var pointerMaterial=MaterialAsset("V4 source-plane cursor white","OldWebMap/V4/History Image");pointerMaterial.SetColor("_Tint",Color.white);pointerMaterial.SetFloat("_Exposure",1);var pointerMesh=CursorMesh();int offset=0;
        for(int slot=0;slot<count;slot++)
        {
            m.slotFirst[slot]=offset;int number=events.Count(e=>(int)e["slot"]==slot);m.slotCount[slot]=number;offset+=number;
            var window=Child("Original image slot "+slot.ToString("00"),root).transform;m.windows[slot]=window;
            var mat=MaterialAsset("V4 original slot "+slot.ToString("00"),"OldWebMap/V4/History Image");mat.SetColor("_Tint",Color.white);mat.SetFloat("_Exposure",1);m.windowMaterials[slot]=mat;
            m.imagePlanes[slot]=Plane("Original source without invented chrome",window,mat,Vector3.zero,Vector3.one);m.imageRenderers[slot]=m.imagePlanes[slot].GetComponent<Renderer>();
            m.cursors[slot]=MeshObject("Cursor points inside the original image",window,pointerMesh,pointerMaterial);
            var date=Text("Year or actual webpage title","",window,Vector3.zero,4.5f,.2f,.95f,new Color(.9f,.94f,.95f));m.dateTexts[slot]=date;m.dateTransforms[slot]=date.transform;window.gameObject.SetActive(false);
        }
        BuildAmbient(m,root,score,images,events);BuildTypography(m,root);BuildComments(m,root,comments);BuildKeylines(m,root,keys);ApplyPresentationLayoutInternal(m,score);
        m.Seek(0);UdonSharpEditorUtility.CopyProxyToUdon(m);EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(m));AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var result=new{root=root.name,eventSlots=count,focusEvents=m.roles.Count(r=>r==1),peripheryEvents=m.roles.Count(r=>r==0),persistentHistoryQuads=192,maxVisibleHistory=48,comments=comments.Count,keylines=keys.Count,trueExtrudedVertices=m.typeWords.Take(3).Sum(t=>t.GetComponent<MeshFilter>().sharedMesh.vertexCount),sceneSaved=false,localToWorldFrame=true,sourceImages=images.Count,newIndependentImageSources=0};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"build.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return JsonConvert.SerializeObject(result);
    }
    public static object Sample(float time){var m=Object.FindObjectOfType<OWV4Montage>();if(m==null)throw new InvalidOperationException("Build V4 montage first");m.Seek(time);return Describe(m);}
    static object Describe(OWV4Montage m)
    {return new{time=m.sampledTime,focus=m.activeFocus,periphery=m.activePeriphery,history=m.activeAmbient,support=m.activeSupport,supportRow=m.sampledSupport,historySpacingSuppressed=m.spacingSuppressedAmbient,comments=m.activeComments,key=m.sampledKey,focusPhase=m.focusPhase,binarySearches=m.binarySearches};}
    static string Pose(OWV4Montage m)
    {return JsonConvert.SerializeObject(m.windows.Select((t,i)=>new{slot=i,active=t.gameObject.activeSelf,p=new[]{t.localPosition.x,t.localPosition.y,t.localPosition.z},s=new[]{t.localScale.x,t.localScale.y,t.localScale.z},cursor=new[]{m.cursors[i].localPosition.x,m.cursors[i].localPosition.y,m.cursors[i].localPosition.z}}).Where(r=>r.active));}
    static Vector2 AngularPoint(Vector3 p)
    {return new Vector2(Mathf.Atan2(p.x,p.z)*Mathf.Rad2Deg,Mathf.Atan2(p.y,Mathf.Sqrt(p.x*p.x+p.z*p.z))*Mathf.Rad2Deg);}
    static Vector4 AngularBox(Vector3 position,Quaternion rotation,Vector3 size,Vector3 eye,float expansion,float depth=0)
    {
        Vector3 delta=position-eye;Vector2 center=AngularPoint(delta);Vector3 u=rotation*Vector3.right*(size.x*.5f),v=rotation*Vector3.up*(size.y*.5f);float yaw=0,pitch=0;
        foreach(var corner in new[]{delta-u-v,delta-u+v,delta+u-v,delta+u+v})
        {var point=AngularPoint(corner);yaw=Mathf.Max(yaw,Mathf.Abs(Mathf.DeltaAngle(center.x,point.x)));pitch=Mathf.Max(pitch,Mathf.Abs(center.y-point.y));}
        if(depth>0)foreach(var corner in new[]{delta-u-v+Vector3.forward*depth,delta-u+v+Vector3.forward*depth,delta+u-v+Vector3.forward*depth,delta+u+v+Vector3.forward*depth})
        {var point=AngularPoint(corner);yaw=Mathf.Max(yaw,Mathf.Abs(Mathf.DeltaAngle(center.x,point.x)));pitch=Mathf.Max(pitch,Mathf.Abs(center.y-point.y));}
        float halfDiagonal=Mathf.Sqrt(size.x*size.x+size.y*size.y)*.5f;
        float yawMargin=Mathf.Atan2(expansion,Mathf.Max(.08f,Mathf.Sqrt(delta.x*delta.x+delta.z*delta.z)-halfDiagonal-expansion))*Mathf.Rad2Deg;
        float pitchMargin=Mathf.Atan2(expansion,Mathf.Max(.08f,delta.magnitude-halfDiagonal-expansion))*Mathf.Rad2Deg;
        return new Vector4(center.x,yaw+yawMargin,center.y,pitch+pitchMargin);
    }
    static bool AngularOverlap(Vector4 a,Vector4 b,float margin=0)
    {return Mathf.Abs(Mathf.DeltaAngle(a.x,b.x))<a.y+b.y+margin && Mathf.Abs(a.z-b.z)<a.w+b.w+margin;}
    sealed class Face
    {public string id;public Vector3 position,size;public Quaternion rotation=Quaternion.identity;public float motion,depth;}
    static Vector3 MontagePosition(OWV4Montage m,Transform value)
    {return m.transform.InverseTransformPoint(value.position);}
    static Quaternion MontageRotation(OWV4Montage m,Transform value)
    {return Quaternion.Inverse(m.transform.rotation)*value.rotation;}
    static string SupportPose(OWV4Montage m)
    {
        // Unused persistent slots deliberately keep their last material/pose.
        // Replay equivalence concerns the complete visible flags and the state
        // of the currently rendered originals, rather than hidden pool residue.
        return JsonConvert.SerializeObject(new{
            row=m.sampledSupport,active=m.activeSupport,
            visible=m.supportPlanes.Select(plane=>plane.gameObject.activeSelf).ToArray(),
            visibleSlots=Enumerable.Range(0,m.supportPlanes.Length).Where(slot=>m.supportPlanes[slot].gameObject.activeSelf).Select(slot=>new{
                slot,image=m.supportMaterials[slot].mainTexture==null?"":AssetDatabase.GetAssetPath(m.supportMaterials[slot].mainTexture),
                p=new[]{m.supportPlanes[slot].localPosition.x,m.supportPlanes[slot].localPosition.y,m.supportPlanes[slot].localPosition.z},
                s=new[]{m.supportPlanes[slot].localScale.x,m.supportPlanes[slot].localScale.y,m.supportPlanes[slot].localScale.z},
                r=new[]{m.supportPlanes[slot].localRotation.x,m.supportPlanes[slot].localRotation.y,m.supportPlanes[slot].localRotation.z,m.supportPlanes[slot].localRotation.w}
            }).ToArray()
        });
    }
    public static object ValidatePresentationLayout()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling)throw new InvalidOperationException("Stopped editor layout audit only; actual Udon/render review is separate");
        var m=ExistingMontage();if(!m.usePresentationLayout || m.supportPlanes==null || m.supportPlanes.Length!=4)throw new InvalidOperationException("ApplyPresentationLayout first");
        var score=JObject.Parse(File.ReadAllText(ScorePath));var sourceImages=(JArray)score["images"];
        var ambientRows=(JArray)score["ambient"];var sourceIds=sourceImages.Select(image=>(string)image["id"]).ToArray();
        if(ambientRows==null || ambientRows.Count!=m.ambientPerRegion || m.ambientWindows.Length!=m.ambientPerRegion*4 || m.imageTextures.Length!=sourceImages.Count)throw new InvalidOperationException("History/source library cardinality changed");
        var ambientSourceIndices=ambientRows.Select(row=>Array.IndexOf(sourceIds,(string)row["image"])).ToArray();
        if(ambientSourceIndices.Any(index=>index<0))throw new InvalidOperationException("History refers to a source missing from the score");
        int historySourceAspectChecks=0,historyTextureBindingChecks=0;var historyTextureEvidence=new List<object>();
        // Importers may resample NPOT images to a square POT texture. Geometry
        // always restores the original score aspect, not the imported GPU size.
        // Check binding separately, including an anonymized display override.
        for(int index=0;index<m.ambientWindows.Length;index++)
        {
            int row=index%m.ambientPerRegion,image=ambientSourceIndices[row];var source=sourceImages[image];string id=sourceIds[image];
            var texture=m.ambientWindows[index].GetComponent<Renderer>().sharedMaterial.mainTexture;string expectedPath=(string)source["display_path"]??(string)source["path"]??Root+"/Images/"+id+".png";
            string actualPath=texture==null?"":AssetDatabase.GetAssetPath(texture),libraryPath=m.imageTextures[image]==null?"":AssetDatabase.GetAssetPath(m.imageTextures[image]);
            if(texture==null || texture!=m.imageTextures[image] || !string.Equals(actualPath,expectedPath,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("History texture binding mismatch at "+index+" source "+id+": expected "+expectedPath+", renderer "+actualPath+", library "+libraryPath);
            historyTextureBindingChecks++;
            float sourceWidth=(float)source["width"],sourceHeight=(float)source["height"],aspect=sourceHeight/sourceWidth;Vector3 size=m.ambientScales[index];
            if(size.x<=0 || size.y<=0 || Mathf.Abs(size.y/size.x-aspect)>.001f)throw new InvalidOperationException("History original-source aspect changed at "+index+" source "+id+": geometry "+size.x+" x "+size.y+", score "+sourceWidth+" x "+sourceHeight+"; imported "+texture.width+" x "+texture.height+" is excluded from the geometry aspect check");
            historySourceAspectChecks++;
            if(index<m.ambientPerRegion)historyTextureEvidence.Add(new{row,source=id,imageIndex=image,expectedPath,actualPath,libraryPath,originalSize=new[]{sourceWidth,sourceHeight},importedSize=new[]{texture.width,texture.height},geometrySize=new[]{size.x,size.y},originalAspect=aspect,geometryAspect=size.y/size.x,bindingMatches=true});
        }
        var times=new SortedSet<float>();for(float t=0;t<133.12f;t+=.32f)times.Add(t);
        for(int e=0;e<m.starts.Length;e++)
        {times.Add(m.starts[e]+.002f);times.Add((m.starts[e]+m.ends[e])*.5f);times.Add(m.ends[e]-.002f);}
        for(int row=0;row<m.supportStarts.Length;row++)
        {times.Add(m.supportStarts[row]+.02f);times.Add((m.supportStarts[row]+m.supportEnds[row])*.5f);times.Add(m.supportEnds[row]-.02f);}
        // Include the whole journey at denser intervals. The test uses the same
        // virtual eye path, while keeping all geometry local to worldFrame.
        foreach(var interval in new[]{new Vector2(79.36f,81.92f),new Vector2(86.4f,88.32f),new Vector2(102.4f,104.96f)})for(float t=interval.x;t<=interval.y;t+=.08f)times.Add(t);
        var viewpoints=new[]{Vector3.zero,Vector3.right*.3f,Vector3.left*.3f,Vector3.up*.3f,Vector3.down*.3f,Vector3.forward*.3f,Vector3.back*.3f};
        int sourceAspectChecks=0,focusChecks=0,centralChecks=0,maxHistory=0,maxSupport=0,minLateHistory=48,pairChecks=0,headPointChecks=0;var rowEvidence=new List<object>();
        for(int row=0;row<m.supportStarts.Length;row++)
        {
            int count=m.supportCounts[row];if(count<2 || count>4)throw new InvalidOperationException("Support count outside 2-4 at row "+row);
            for(int slot=0;slot<count;slot++)
            {
                int index=row*4+slot,image=m.supportImages[index];Vector3 size=m.supportScales[index];float aspect=(float)sourceImages[image]["height"]/(float)sourceImages[image]["width"];
                if(Mathf.Abs(size.y/size.x-aspect)>.001f)throw new InvalidOperationException("Supporting source was stretched");
                if(m.supportFocusEvents[row]>=0 && image==m.images[m.supportFocusEvents[row]])throw new InvalidOperationException("Support repeats its primary source");sourceAspectChecks++;
            }
            m.Seek((m.supportStarts[row]+m.supportEnds[row])*.5f);
            if(m.activeSupport!=count || m.sampledSupport!=row)throw new InvalidOperationException("Supporting pool did not restore its absolute-time row");
            if(m.supportKinds[row]==0)
            {
                int e=m.supportFocusEvents[row];if(m.supportStarts[row]>m.starts[e]+m.enters[e]+.001f || m.supportEnds[row]<m.ends[e]-m.exits[e]-.001f || Mathf.Abs(m.supportStarts[row]-m.starts[e]-m.enters[e])>.021f || Mathf.Abs(m.supportEnds[row]-m.ends[e]+m.exits[e])>.021f)throw new InvalidOperationException("Focus support misses part of the readable hold");focusChecks++;
            }
            rowEvidence.Add(new{row,time=m.sampledTime,kind=m.supportKinds[row],count=m.activeSupport,focus=m.supportFocusEvents[row],images=Enumerable.Range(0,count).Select(slot=>(string)sourceImages[m.supportImages[row*4+slot]]["id"]).ToArray()});
        }
        if(focusChecks!=m.roles.Count(role=>role==1))throw new InvalidOperationException("A focus has no related central support");
        foreach(float time in times)
        {
            m.Seek(time);if(m.activeAmbient>48)throw new InvalidOperationException("Retained history exceeded 48 renderers");maxHistory=Mathf.Max(maxHistory,m.activeAmbient);maxSupport=Mathf.Max(maxSupport,m.activeSupport);
            if(time>120)minLateHistory=Mathf.Min(minLateHistory,m.activeAmbient);
            var history=new List<Face>();var fast=new List<Face>();var protect=new List<Face>();var support=new List<Face>();
            for(int i=0;i<m.ambientWindows.Length;i++)if(m.ambientWindows[i].gameObject.activeSelf && m.ambientRegions[i/48].gameObject.activeSelf)
            {
                Vector3 size=m.ambientScales[i];
                history.Add(new Face{id="history "+i,position=m.ambientRegions[i/48].localPosition+m.ambientPositions[i],rotation=m.ambientWindows[i].localRotation,size=size*1.015f,motion=.065f});
            }
            for(int slot=0;slot<m.windows.Length;slot++)if(m.windows[slot].gameObject.activeSelf)
            {
                int e=Enumerable.Range(m.slotFirst[slot],m.slotCount[slot]).Single(index=>time>=m.starts[index] && time<m.ends[index]);
                var face=new Face{id=(m.roles[e]==1?"focus ":"fast ")+e,position=m.windows[slot].localPosition,rotation=m.windows[slot].localRotation,size=new Vector3(m.widths[e],m.heights[e]+(m.roles[e]==1?.35f:.3f),1)*1.02f,motion=m.roles[e]==1?.04f:.08f};
                if(m.roles[e]==1)protect.Add(face);else fast.Add(face);
            }
            if(m.sampledSupport>=0)
            {
                int row=m.sampledSupport;
                protect.Add(new Face{id="subject "+row,position=m.supportProtectedPositions[row],size=m.supportProtectedScales[row],motion=.04f});
                for(int slot=0;slot<m.activeSupport;slot++)support.Add(new Face{id="support "+slot,position=m.supportPositions[row*4+slot],size=m.supportScales[row*4+slot],motion=.02f,depth=.12f});
            }
            if(m.sampledKey>=0)
            {
                int region=m.keyRegions[m.sampledKey];var panel=m.keyPanels[region].transform;protect.Add(new Face{id="key sentence",position=MontagePosition(m,panel),rotation=MontageRotation(m,panel),size=panel.localScale,motion=.03f});
            }
            foreach(var offset in viewpoints)
            {
                Vector3 eye=m.layoutReferenceEye+m.journey.VirtualPosition(time)+offset;headPointChecks++;
                var historyBoxes=history.Select(face=>AngularBox(face.position,face.rotation,face.size,eye,face.motion,face.depth)).ToArray();var fastBoxes=fast.Select(face=>AngularBox(face.position,face.rotation,face.size,eye,face.motion,face.depth)).ToArray();var protectBoxes=protect.Select(face=>AngularBox(face.position,face.rotation,face.size,eye,face.motion,face.depth)).ToArray();var supportBoxes=support.Select(face=>AngularBox(face.position,face.rotation,face.size,eye,face.motion,face.depth)).ToArray();
                for(int i=0;i<history.Count;i++)
                {
                    for(int j=0;j<i;j++){pairChecks++;if(AngularOverlap(historyBoxes[i],historyBoxes[j]))throw new InvalidOperationException("Maximum-pulse history projection overlap at "+time+": "+history[i].id+" / "+history[j].id);}
                    for(int j=0;j<fast.Count;j++){pairChecks++;if(AngularOverlap(historyBoxes[i],fastBoxes[j]))throw new InvalidOperationException("History crosses a fast lane at "+time+": "+history[i].id+" / "+fast[j].id);}
                    for(int j=0;j<protect.Count;j++){pairChecks++;if(AngularOverlap(historyBoxes[i],protectBoxes[j]))throw new InvalidOperationException("History covers protected subject at "+time+": "+history[i].id+" / "+protect[j].id);}
                    for(int j=0;j<support.Count;j++){pairChecks++;if(AngularOverlap(historyBoxes[i],supportBoxes[j]))throw new InvalidOperationException("History covers central support at "+time+": "+history[i].id);}
                }
                for(int i=0;i<fast.Count;i++)for(int j=0;j<i;j++){pairChecks++;if(AngularOverlap(fastBoxes[i],fastBoxes[j]))throw new InvalidOperationException("Fast lanes overlap at "+time+": "+fast[i].id+" / "+fast[j].id);}
                for(int i=0;i<support.Count;i++)
                {
                    for(int j=0;j<protect.Count;j++){centralChecks++;if(AngularOverlap(supportBoxes[i],protectBoxes[j]))throw new InvalidOperationException("Related image covers subject/key sentence at "+time+": "+support[i].id+" / "+protect[j].id);}
                    for(int j=0;j<i;j++){centralChecks++;if(AngularOverlap(supportBoxes[i],supportBoxes[j]))throw new InvalidOperationException("Related images overlap at "+time);}
                    for(int j=0;j<fast.Count;j++){centralChecks++;if(AngularOverlap(supportBoxes[i],fastBoxes[j]))throw new InvalidOperationException("Fast lane crosses related image at "+time+": "+fast[j].id);}
                }
            }
        }
        foreach(float reverseTime in new[]{55.9f,56.2f})
        {
            m.Seek(reverseTime);if(reverseTime==56.2f && m.activeSupport<2)throw new InvalidOperationException("Visible reverse-seek sample has no central supporting originals");
            string first=SupportPose(m);m.Seek(118.8f);m.Seek(reverseTime);
            if(first!=SupportPose(m))throw new InvalidOperationException("Reverse seek changed visible supporting texture/pose at "+reverseTime);
        }
        var result=new{scope="Authored rectangle projection audit around the fixed review eye, all viewer rotations, seven eye positions inside a 0.3 m radius. Maximum pulse/motion envelopes are conservative. Actor/Hub occlusion and stereo/HMD still require actual rendered review.",samples=times.Count,headPointChecks,pairChecks,centralChecks,focusChecks,sourceAspectChecks,historySourceAspectChecks,historyTextureBindingChecks,geometryAspectBasis="score original width/height",historyTextureEvidence,persistentSupportQuads=4,maxSupport,maxHistory,minLateHistory,historyBudget=48,independentImagesAdded=0,reverseSeekRepeatable=true,reverseSeekSamples=new[]{55.9f,56.2f},reverseSeekIntermediate=118.8f,referenceEye=new[]{m.layoutReferenceEye.x,m.layoutReferenceEye.y,m.layoutReferenceEye.z},viewerRadius=.3f,historyPulseMaximum=1.015f,historyMovementEnvelope=.065f,fastMovementEnvelope=.08f,supportMovementEnvelope=.14f,rows=rowEvidence};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"presentation-layout-validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));m.Seek(0);return JsonConvert.SerializeObject(result);
    }
    public static object Validate()
    {
        var m=Object.FindObjectOfType<OWV4Montage>();if(m==null)throw new InvalidOperationException("Build V4 montage first");var rows=new List<object>();
        foreach(float time in new[]{0f,10.24f,17.2f,30.72f,44.64f,61.44f,78f,80.4f,81.92f,87.2f,88.32f,103.6f,104.96f,115.2f,128f})
        {
            m.Seek(time);if(m.activeAmbient>48)throw new InvalidOperationException("V4 history renderer budget exceeded");
            if(!m.transform.IsChildOf(m.journey.worldFrame))throw new InvalidOperationException("V4 montage detached from Journey.worldFrame");rows.Add(Describe(m));
        }
        int focusChecked=0,cursorsChecked=0;
        for(int slot=0;slot<m.windows.Length;slot++)for(int e=m.slotFirst[slot];e<m.slotFirst[slot]+m.slotCount[slot];e++)if(m.roles[e]==1)
        {
            float holding=m.ends[e]-m.starts[e]-m.enters[e]-m.exits[e];if(holding<2.399f || holding>5.001f)throw new InvalidOperationException("Focus hold outside 2.4-5 seconds");
            m.Seek(m.starts[e]+m.enters[e]+holding*.5f);
            if(Vector3.Distance(m.windows[slot].localPosition,m.to[e])>.04f || Mathf.Abs(m.windows[slot].localScale.x-1)>.001f)throw new InvalidOperationException("Focus anchor fails to remain identifiable");focusChecked++;
            if(m.hasCursor[e] && m.starts[e]+m.cursorAt[e]+.12f<m.overloadAt)
            {
                m.Seek(m.starts[e]+Mathf.Max(m.cursorAt[e]+.12f,m.enters[e]+.5f));
                Vector3 expected=new Vector3((m.cursorUV[e].x-.5f)*m.widths[e],(.5f-m.cursorUV[e].y)*m.heights[e],-.018f);
                if(Vector3.Distance(m.cursors[slot].localPosition,expected)>.001f || Quaternion.Angle(m.cursors[slot].localRotation,Quaternion.identity)>.001f)throw new InvalidOperationException("Early cursor left the original image plane");cursorsChecked++;
            }
        }
        m.Seek(44.64f);string first=Pose(m);m.Seek(115.2f);m.Seek(44.64f);if(first!=Pose(m))throw new InvalidOperationException("V4 reverse seek changes focus/periphery state");
        // Recompute the same absolute sample after moving the common frame.
        // Local positions must remain identical and world positions follow it;
        // this catches accidental camera-offset cancellation in the montage.
        int firstFocus=Array.IndexOf(m.roles,1);int focusSlot=0;
        // Resolve explicitly because empty slots can share their first index.
        for(int i=0;i<m.windows.Length;i++)if(firstFocus>=m.slotFirst[i]&&firstFocus<m.slotFirst[i]+m.slotCount[i]){focusSlot=i;break;}
        float focusTime=m.starts[firstFocus]+m.enters[firstFocus]+.5f;m.Seek(focusTime);
        Vector3 localBefore=m.windows[focusSlot].localPosition,worldBefore=m.windows[focusSlot].position,frameBefore=m.journey.worldFrame.localPosition;
        Vector3 shift=new Vector3(1.7f,-.2f,-2.3f),expectedShift=m.journey.worldFrame.parent!=null?m.journey.worldFrame.parent.TransformVector(shift):shift;
        try
        {
            m.journey.worldFrame.localPosition=frameBefore+shift;m.Seek(focusTime);
            if(Vector3.Distance(m.windows[focusSlot].localPosition,localBefore)>.0001f || Vector3.Distance(m.windows[focusSlot].position-worldBefore,expectedShift)>.001f)throw new InvalidOperationException("Montage cancels the shared world-frame journey");
        }
        finally{m.journey.worldFrame.localPosition=frameBefore;}
        var meshes=m.typeWords.Take(3).Select(t=>t.GetComponent<MeshFilter>().sharedMesh).ToArray();
        if(meshes.Any(mesh=>mesh.bounds.size.z<.09f || !mesh.normals.Any(n=>Mathf.Abs(n.y)>.75f)) || m.typeRegions.Any(t=>t.GetComponentsInChildren<TMP_Text>(true).Length>0))throw new InvalidOperationException("V4 music lettering is not real extruded geometry");
        object keyText=null;if(m.keyStarts.Length>0)
        {
            m.Seek((m.keyStarts[0]+m.keyEnds[0])*.5f);var text=m.keyTexts[m.keyRegions[0]];text.ForceMeshUpdate(true,true);int count=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Count(c=>c.isVisible);
            if(count==0)throw new InvalidOperationException("High contrast keyline has no visible text mesh");keyText=new{text=text.text,count,size=text.fontSize,materialQueue=text.fontSharedMaterial.renderQueue};
        }
        var shaderMessages=new[]{"OldWebMap/V4/History Image","OldWebMap/V4/Extruded Chrome Typography"}.Select(name=>new{name,messages=ShaderUtil.GetShaderMessages(Shader.Find(name)).Select(e=>new{e.message,e.line,severity=e.severity.ToString()}).ToArray()}).ToArray();if(shaderMessages.Any(s=>s.messages.Any(e=>e.severity=="Error")))throw new InvalidOperationException("V4 shaders have compiler errors");
        var result=new{rows,focusChecked,cursorsChecked,historyRendererBudget=48,persistentHistoryObjects=192,reverseSeekRepeatable=true,worldFrameMotionVerified=true,extrudedWords=meshes.Select(mesh=>new{mesh.name,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,depth=mesh.bounds.size.z}).ToArray(),keyText,shaderMessages,actualVrEyesStillRequireReview=true};
        Directory.CreateDirectory(ReportDirectory);File.WriteAllText(Path.Combine(ReportDirectory,"validation.json"),JsonConvert.SerializeObject(result,Formatting.Indented));m.Seek(0);return JsonConvert.SerializeObject(result);
    }
}
