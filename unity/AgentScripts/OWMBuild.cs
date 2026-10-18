using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using OldWebMap;
using VRC.SDK3.Components;
using VRC.Udon;

#pragma warning disable 0649 // JSON DTO 字段由 JsonUtility 填充

// Pipeline 入口（工程根目录执行）：
//   unity command run_script --file AgentScripts/OWMBuild.cs --entry OWMBuild.All
// 只做编辑器侧的资产与场景制作：字体、材质、数据组件、场景骨架。
// 运行时行为全部由 Assets/OldWebMap/Scripts 下的 UdonSharp 组件负责。
public static class OWMBuild
{
    private const string Root = "Assets/OldWebMap";
    private const string FontSourceDir = Root + "/Fonts/FontSources";
    private const string FontAssetPath = Root + "/Fonts/OWM_Chinese-SDF.asset";
    private const string MaterialsDir = Root + "/Materials";
    private const string DataDir = "AgentScripts/data";
    private const string ScenePath = "Assets/Scenes/OWM_Main.unity";
    private const string DefaultScenePath = "Assets/Scenes/VRCDefaultWorldScene.unity";
    private const string CjkFontFile = "NotoSansSC-Regular.ttf";
    private const string CjkFontFrom =
        @"D:\Projects\VRChatProjects2\vrc-cpu-world\Assets\CircuitLab\Localization\FontSources\NotoSansSC-Regular.ttf";
    private const string CjkLicenseFile = "NotoSans-OFL.txt";
    private const string CjkLicenseFrom =
        @"D:\Projects\VRChatProjects2\vrc-cpu-world\Assets\CircuitLab\Localization\FontSources\NotoSans-OFL.txt";

    private static readonly Color Amber = new Color32(0xFF, 0xB0, 0x00, 0xFF);
    private static readonly Color Cyan = new Color32(0x00, 0xD8, 0xD8, 0xFF);
    private static readonly Color Magenta = new Color32(0xFF, 0x00, 0xAA, 0xFF);
    private static readonly Color Paper = new Color32(0xE8, 0xE8, 0xE8, 0xFF);
    private static readonly Color Background = new Color32(0x0A, 0x0A, 0x0F, 0xFF);
    private static readonly Color Hope = new Color32(0x3D, 0xFF, 0xB0, 0xFF);

    private static readonly List<UdonSharpBehaviour> Behaviours = new List<UdonSharpBehaviour>();
    private static TMP_FontAsset _font;
    private static Material _gridMaterial;
    private static Material _nodeIdleMaterial;
    private static Material _nodeOnlineMaterial;
    private static Material _nodeThrottledMaterial;
    private static Material _nodeDeadMaterial;
    private static Material _nodeArchivedMaterial;
    private static Material _nodeExportedMaterial;
    private static Material _nodeHopeMaterial;
    private static Material _beaconIdleMaterial;
    private static Material _beaconActiveMaterial;
    private static Material _beaconHopeMaterial;
    private static Material _linkMaterial;
    private static Material _plateMaterial;

    // ---------------------------------------------------------------- entries

    public static object All()
    {
        EnsureFolders();
        EnsureTmpEssentials();
        JObject timeline = LoadJson(DataDir + "/timeline.json");
        JObject lyrics = LoadJson(DataDir + "/lyrics.json");
        JObject stageData = LoadJson(DataDir + "/stage.json");
        JObject labels = LoadJson(DataDir + "/labels.json");
        BuildFont(timeline, lyrics);
        BuildMaterials();
        BuildScene(timeline, lyrics, stageData, labels);
        return new
        {
            scene = ScenePath,
            segments = ((JArray)timeline["segments"]).Count,
            lyricLines = ((JArray)lyrics["lines"]).Count,
            greetz = ((JArray)lyrics["greetz"]["entries"]).Count,
            zones = ((JArray)stageData["zones"]).Count,
            nodes = ((JArray)stageData["nodes"]).Count,
            font = FontAssetPath,
            udonBehaviours = Behaviours.Count
        };
    }

    /// <summary>只重建字体资产（改词后重跑即可）。</summary>
    public static object FontsOnly()
    {
        EnsureFolders();
        EnsureTmpEssentials();
        JObject timeline = LoadJson(DataDir + "/timeline.json");
        JObject lyrics = LoadJson(DataDir + "/lyrics.json");
        BuildFont(timeline, lyrics);
        return new { font = FontAssetPath, ok = _font != null };
    }

    /// <summary>诊断：报告当前场景里的演出对象与绑定关系。</summary>
    public static object Inspect()
    {
        Scene scene = SceneManager.GetActiveScene();
        OWTimelineDirector director = UnityEngine.Object.FindObjectOfType<OWTimelineDirector>();
        OWLyricTrackPlayer lyricPlayer = UnityEngine.Object.FindObjectOfType<OWLyricTrackPlayer>();
        OWGreetzRoll greetz = UnityEngine.Object.FindObjectOfType<OWGreetzRoll>();
        OWVisitorCounter counter = UnityEngine.Object.FindObjectOfType<OWVisitorCounter>();
        OWStageBeacon[] beacons = UnityEngine.Object.FindObjectsOfType<OWStageBeacon>();
        string[] beaconNames = new string[beacons.Length];
        for (int i = 0; i < beacons.Length; i++)
        {
            beaconNames[i] = beacons[i].gameObject.name + "[" +
                (beacons[i].activeSegments == null ? "-" : string.Join(",", beacons[i].activeSegments)) + "]";
        }
        return new
        {
            scene = scene.path,
            dirty = scene.isDirty,
            director = director == null ? "-" : director.gameObject.name,
            lyricPlayer = lyricPlayer == null ? "-" : lyricPlayer.gameObject.name,
            greetz = greetz == null ? "-" : greetz.gameObject.name,
            counter = counter == null ? "-" : counter.gameObject.name,
            beacons = beaconNames
        };
    }

    /// <summary>
    /// 补齐 UdonSharp 程序资产：脚本用文件同步进来时，Unity 的自动刷新可能没有
    /// 触发 U# 的程序资产生成，这里显式导入并编译一次。
    /// </summary>
    public static object RepairPrograms()
    {
        string[] files = Directory.GetFiles(Root + "/Scripts", "*.cs");
        List<string> report = new List<string>();
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i].Replace('\\', '/');
            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            string assetPath = path.Substring(0, path.Length - 3) + ".asset";
            UdonSharpProgramAsset program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);
            if (program == null)
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                {
                    report.Add("missing MonoScript " + path);
                    continue;
                }
                program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = script;
                AssetDatabase.CreateAsset(program, assetPath);
                report.Add("created " + assetPath);
            }
            else
            {
                report.Add("exists " + assetPath);
            }
        }
        AssetDatabase.SaveAssets();
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
        AssetDatabase.Refresh();
        return report.ToArray();
    }

    /// <summary>诊断：确认 JSON 是否被正确解析。</summary>
    public static object JsonCheck()
    {
        string path = DataDir + "/timeline.json";
        JObject timeline = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
        JObject lyrics = JObject.Parse(File.ReadAllText(DataDir + "/lyrics.json", Encoding.UTF8));
        JArray segments = timeline["segments"] as JArray;
        JArray lines = lyrics["lines"] as JArray;
        return new
        {
            cwd = Directory.GetCurrentDirectory(),
            exists = File.Exists(path),
            segments = segments == null ? -1 : segments.Count,
            title = (string)timeline["title"],
            duration = (float)timeline["duration_seconds"],
            lines = lines == null ? -1 : lines.Count,
            greetz = lyrics["greetz"] != null && lyrics["greetz"]["entries"] != null
                ? ((JArray)lyrics["greetz"]["entries"]).Count : -1
        };
    }

    // ---------------------------------------------------------------- helpers

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/OldWebMap");
        EnsureFolder(Root + "/Fonts");
        EnsureFolder(FontSourceDir);
        EnsureFolder(MaterialsDir);
        EnsureFolder("Assets/Scenes");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    /// <summary>工程尚未导入 TMP 基础资源时先补齐（TMP 的 shader 与设置资产）。</summary>
    private static void EnsureTmpEssentials()
    {
        if (AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_SDF.shader") != null) return;
        string[] dirs = Directory.GetDirectories("Library/PackageCache", "com.unity.textmeshpro*");
        if (dirs.Length == 0) throw new InvalidOperationException("找不到 TextMeshPro 包缓存目录");
        string package = dirs[0].Replace('\\', '/') + "/Package Resources/TMP Essential Resources.unitypackage";
        if (!File.Exists(package)) throw new FileNotFoundException("找不到 TMP Essential Resources", package);
        Debug.Log("导入 TMP Essential Resources：" + package);
        AssetDatabase.ImportPackage(package, false);
        AssetDatabase.Refresh();
    }

    private static JObject LoadJson(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("缺少数据文件：" + path);
        return JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    private static T Udon<T>(GameObject go) where T : UdonSharpBehaviour
    {
        T behaviour = go.GetComponent<T>();
        if (behaviour == null) behaviour = go.AddUdonSharpComponent<T>();
        if (!Behaviours.Contains(behaviour)) Behaviours.Add(behaviour);
        return behaviour;
    }

    private static void CopyProxies()
    {
        for (int i = 0; i < Behaviours.Count; i++)
        {
            UdonSharpBehaviour proxy = Behaviours[i];
            if (proxy == null) continue;
            UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            EditorUtility.SetDirty(proxy);
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            if (backing != null) EditorUtility.SetDirty(backing);
        }
        Behaviours.Clear();
    }

    // ---------------------------------------------------------------- fonts

    private static void BuildFont(JObject timeline, JObject lyrics)
    {
        string fontPath = FontSourceDir + "/" + CjkFontFile;
        if (!File.Exists(fontPath))
        {
            if (!File.Exists(CjkFontFrom))
                throw new FileNotFoundException("找不到中文字体源文件：" + CjkFontFrom);
            Directory.CreateDirectory(FontSourceDir);
            File.Copy(CjkFontFrom, fontPath);
            string licensePath = FontSourceDir + "/" + CjkLicenseFile;
            if (File.Exists(CjkLicenseFrom) && !File.Exists(licensePath)) File.Copy(CjkLicenseFrom, licensePath);
            AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceUpdate);
        }

        string characters = CollectGlyphs(timeline, lyrics);
        Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (source == null) throw new InvalidOperationException("字体未按 Font 导入：" + fontPath);

        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(source, 48, 4, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("无法从字体生成 SDF 资产");
            asset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            asset.TryAddCharacters(characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning("OWM 字体缺少字形：" + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            AddFontSubAssets(asset, FontAssetPath);
        }
        else
        {
            asset.ReadFontAssetDefinition();
            string pending = new string(characters.Where(c => !asset.HasCharacter(c)).ToArray());
            if (pending.Length > 0)
            {
                asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                asset.TryAddCharacters(pending, out string missing);
                if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("OWM 字体缺少字形：" + missing);
                asset.atlasPopulationMode = AtlasPopulationMode.Static;
            }
            AddFontSubAssets(asset, FontAssetPath);
        }
        EditorUtility.SetDirty(asset);
        _font = asset;
    }

    private static void AddFontSubAssets(TMP_FontAsset asset, string assetPath)
    {
        if (asset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset.material)))
            AssetDatabase.AddObjectToAsset(asset.material, assetPath);
        Texture2D[] textures = asset.atlasTextures;
        if (textures == null) return;
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(textures[i])))
                AssetDatabase.AddObjectToAsset(textures[i], assetPath);
        }
    }

    private static string CollectGlyphs(JObject timeline, JObject lyrics)
    {
        StringBuilder sb = new StringBuilder();
        for (int c = 32; c < 127; c++) sb.Append((char)c);
        sb.Append("世界地图共享精神的颂歌与安魂曲致所有失效的链接");
        sb.Append("访客计数世界在线限速归档复活被导出");
        sb.Append("Best viewed in 800×600  LOADING WORLD MAP…  C:\\>_");
        sb.Append("「」（）《》，。、：；！？—…×·");
        JArray segments = timeline == null ? null : timeline["segments"] as JArray;
        if (segments != null)
        {
            for (int i = 0; i < segments.Count; i++) sb.Append(Text(segments[i], "name"));
        }
        if (lyrics != null)
        {
            JArray lines = lyrics["lines"] as JArray;
            if (lines != null)
                for (int i = 0; i < lines.Count; i++) sb.Append(Text(lines[i], "text"));
            JArray entries = lyrics["greetz"] == null ? null : lyrics["greetz"]["entries"] as JArray;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++) sb.Append(entries[i].ToString());
        }
        HashSet<char> set = new HashSet<char>(sb.ToString().ToCharArray());
        char[] sorted = set.ToArray();
        Array.Sort(sorted);
        return new string(sorted);
    }

    /// <summary>安全读取 JSON 字符串字段。</summary>
    private static string Text(JToken token, string field)
    {
        if (token == null || token[field] == null) return "";
        return token[field].ToString();
    }

    /// <summary>安全读取 JSON 数值字段。</summary>
    private static float Number(JToken token, string field, float fallback)
    {
        if (token == null || token[field] == null) return fallback;
        float value;
        if (float.TryParse(token[field].ToString(), out value)) return value;
        return fallback;
    }

    // ---------------------------------------------------------------- materials

    private static void BuildMaterials()
    {
        _gridMaterial = EnsureMaterial("OWM_Grid", Background, Color.black, 0f);
        _nodeIdleMaterial = EnsureMaterial("OWM_NodeIdle", new Color32(0x3A, 0x3A, 0x44, 0xFF), Color.black, 0f);
        _nodeOnlineMaterial = EnsureMaterial("OWM_NodeOnline", Amber, Amber, 0.45f);
        _nodeThrottledMaterial = EnsureMaterial("OWM_NodeThrottled", Cyan, Cyan, 0.35f);
        _nodeDeadMaterial = EnsureMaterial("OWM_NodeDead", new Color32(0x4A, 0x4A, 0x50, 0xFF), Color.black, 0f);
        _nodeArchivedMaterial = EnsureMaterial("OWM_NodeArchived", new Color32(0x6A, 0x6A, 0x72, 0xFF),
            new Color32(0x30, 0x30, 0x38, 0xFF), 0.1f);
        _nodeExportedMaterial = EnsureMaterial("OWM_NodeExported", Magenta, Magenta, 0.5f);
        _nodeHopeMaterial = EnsureMaterial("OWM_NodeHope", Hope, Hope, 0.45f);
        _beaconIdleMaterial = EnsureMaterial("OWM_BeaconIdle", new Color32(0x1E, 0x1E, 0x28, 0xFF), Color.black, 0f);
        _beaconActiveMaterial = EnsureMaterial("OWM_BeaconActive", Amber, Amber, 0.8f);
        _beaconHopeMaterial = EnsureMaterial("OWM_BeaconHope", Hope, Hope, 0.8f);
        _linkMaterial = EnsureMaterial("OWM_Link", new Color32(0x20, 0x60, 0x70, 0xFF), Cyan, 0.25f);
        _plateMaterial = EnsureMaterial("OWM_Plate", new Color32(0x0E, 0x0E, 0x16, 0xFF), Color.black, 0f);
    }

    private static Material EnsureMaterial(string name, Color color, Color emission, float emissionStrength)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("找不到 Standard shader");
            material = new Material(shader);
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (emissionStrength > 0.01f)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission * emissionStrength);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        material.SetFloat("_Glossiness", 0.05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---------------------------------------------------------------- scene

    private static void BuildScene(JObject timeline, JObject lyrics, JObject stageData, JObject labels)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.isDirty && active.path != ScenePath)
            throw new InvalidOperationException("当前打开的场景有未保存改动，先保存或关闭再构建：" + active.path);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            if (!AssetDatabase.CopyAsset(DefaultScenePath, ScenePath))
                throw new InvalidOperationException("无法从默认场景派生：" + DefaultScenePath);
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject stale = GameObject.Find("OWM_WorldRoot");
        if (stale != null) UnityEngine.Object.DestroyImmediate(stale);
        GameObject templateFloor = GameObject.Find("Floor");
        if (templateFloor != null) UnityEngine.Object.DestroyImmediate(templateFloor);

        GameObject worldRoot = new GameObject("OWM_WorldRoot");
        GameObject stage = Child(worldRoot.transform, "Stage");

        // 数据组件
        GameObject dataGo = Child(stage.transform, "Data");
        OWTimelineData timelineData = Udon<OWTimelineData>(dataGo);
        FillTimelineData(timelineData, timeline);
        OWLyricData lyricData = Udon<OWLyricData>(dataGo);
        FillLyricData(lyricData, lyrics);

        // 空间分区：按 data/stage.json 的坐标施工
        GameObject zonesRoot = Child(stage.transform, "Zones");
        List<OWStageBeacon> beacons = BuildZones(zonesRoot.transform, stageData);
        GameObject nodesRoot = Child(stage.transform, "Nodes");
        Dictionary<string, Vector3> nodePositions = BuildNodes(nodesRoot.transform, stageData, labels);
        GameObject linksRoot = Child(stage.transform, "Links");
        BuildLinks(linksRoot.transform, stageData, nodePositions);

        // 歌词带：跟随本地玩家视野下缘的 HUD
        GameObject lyricRoot = Child(worldRoot.transform, "LyricTrack");
        GameObject lyricCanvasGo = Child(lyricRoot.transform, "LyricCanvas");
        lyricCanvasGo.transform.localPosition = Vector3.zero;
        lyricCanvasGo.transform.localRotation = Quaternion.identity;
        lyricCanvasGo.transform.localScale = Vector3.one * 0.0018f;
        RectTransform lyricCanvasRect = lyricCanvasGo.AddComponent<RectTransform>();
        lyricCanvasRect.sizeDelta = new Vector2(1600f, 320f);
        Canvas lyricCanvas = lyricCanvasGo.AddComponent<Canvas>();
        lyricCanvas.renderMode = RenderMode.WorldSpace;
        lyricCanvas.sortingOrder = 10;

        TextMeshProUGUI lyricLine = WorldText(lyricCanvasGo.transform, "LyricLine", "",
            new Vector3(0f, -30f, 0f), new Vector2(1560f, 140f), 64f, Amber, TextAlignmentOptions.Center);

        GameObject greetzTrack = Child(lyricCanvasGo.transform, "GreetzTrack");
        RectTransform greetzRect = greetzTrack.AddComponent<RectTransform>();
        greetzRect.sizeDelta = new Vector2(1800f, 60f);
        greetzRect.anchoredPosition = new Vector2(0f, 100f);
        TextMeshProUGUI greetzText = WorldText(greetzTrack.transform, "GreetzText", "",
            new Vector3(0f, 0f, 0f), new Vector2(1800f, 60f), 40f, Cyan, TextAlignmentOptions.Center);

        OWGreetzRoll greetzRoll = Udon<OWGreetzRoll>(lyricRoot);
        greetzRoll.textField = greetzText;
        greetzRoll.track = greetzRect;
        greetzRoll.scrollSpeed = 70f;
        greetzRoll.cycleLength = 3200f;
        greetzRoll.startX = 900f;

        OWLyricTrackPlayer lyricPlayer = Udon<OWLyricTrackPlayer>(lyricRoot);
        lyricPlayer.lineField = lyricLine;
        lyricPlayer.greetzRoll = greetzRoll;
        lyricPlayer.lineColor = Amber;

        OWHeadHud headHud = Udon<OWHeadHud>(lyricRoot);
        headHud.offset = new Vector3(0f, -0.34f, 1.45f);
        headHud.eulerOffset = new Vector3(-8f, 0f, 0f);
        headHud.followSpeed = 0f;

        // 空间 HUD：入口门头上的标题与访客计数器（挂在世界里的数字牌位置）
        GameObject hud = Child(worldRoot.transform, "HUD");
        GameObject hudCanvasGo = Child(hud.transform, "HudCanvas");
        hudCanvasGo.transform.localPosition = new Vector3(0f, 4.4f, -11f);
        hudCanvasGo.transform.localRotation = Quaternion.identity;
        hudCanvasGo.transform.localScale = Vector3.one * 0.006f;
        RectTransform hudRect = hudCanvasGo.AddComponent<RectTransform>();
        hudRect.sizeDelta = new Vector2(1600f, 220f);
        Canvas hudCanvas = hudCanvasGo.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.WorldSpace;
        hudCanvas.sortingOrder = 11;

        TextMeshProUGUI titleText = WorldText(hudCanvasGo.transform, "Title",
            Text(labels, "title"),
            new Vector3(0f, 40f, 0f), new Vector2(1560f, 70f), 40f, Paper, TextAlignmentOptions.Center);
        titleText.text = UiText(labels, "title") + " / WORLD MAP";
        TextMeshProUGUI counterText = WorldText(hudCanvasGo.transform, "VisitorCounter", "1048576",
            new Vector3(0f, -40f, 0f), new Vector2(1560f, 80f), 56f, Cyan, TextAlignmentOptions.Center);
        WorldText(hudCanvasGo.transform, "VisitorLabel", UiText(labels, "visitor_label"),
            new Vector3(0f, -95f, 0f), new Vector2(1560f, 40f), 28f, Paper, TextAlignmentOptions.Center);

        OWVisitorCounter counter = Udon<OWVisitorCounter>(hud);
        counter.digits = counterText;
        counter.resetSegmentId = "outro";
        counter.startValue = 42;
        counter.digitCount = 3;
        counter.idlePerSecond = 0f;
        counter.resetDuration = 4f;

        // 音频占位：音乐源确定后把 WAV 挂到这里
        GameObject audioGo = Child(worldRoot.transform, "Audio");
        AudioSource music = audioGo.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0.8f;

        // 导演
        GameObject directorGo = Child(stage.transform, "Director");
        OWTimelineDirector director = Udon<OWTimelineDirector>(directorGo);
        director.data = timelineData;
        director.beacons = beacons.ToArray();
        director.visitorCounter = counter;
        director.segmentListeners = new UdonBehaviour[0];
        lyricPlayer.director = director;
        lyricPlayer.lyrics = lyricData;
        greetzRoll.director = director;
        greetzRoll.lyrics = lyricData;
        counter.director = director;

        // 入口与出生点：坐标取 data/stage.json 的 player.spawn
        GameObject entrance = Child(worldRoot.transform, "PlayerSpawn");
        JToken spawnPoint = stageData["player"]["spawn"];
        entrance.transform.localPosition = new Vector3(
            Number(spawnPoint, "x", 0f), Number(spawnPoint, "y", 0.05f), Number(spawnPoint, "z", -6f));
        MakePlate(entrance.transform, "EntrancePlate", new Vector3(3.6f, 1.6f, 4.5f), 0f,
            PlateText(labels, "entrance"), "", "", Paper, 0.13f, 0.09f);
        SetSpawn(entrance);

        ApplyEnvironment();

        CopyProxies();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EnsureSceneInBuild();
    }

    private static void FillTimelineData(OWTimelineData target, JObject timeline)
    {
        target.title = Text(timeline, "title");
        target.subtitle = Text(timeline, "subtitle");
        target.durationSeconds = Number(timeline, "duration_seconds", 180f);

        JArray segments = (JArray)timeline["segments"];
        int count = segments.Count;
        target.segmentIds = new string[count];
        target.segmentNames = new string[count];
        target.segmentSpaces = new string[count];
        target.segmentStarts = new float[count];
        target.segmentEnds = new float[count];
        target.eventStartIndex = new int[count];
        target.eventCount = new int[count];

        List<float> times = new List<float>();
        List<string> types = new List<string>();
        List<string> ids = new List<string>();
        for (int i = 0; i < count; i++)
        {
            JToken segment = segments[i];
            target.segmentIds[i] = Text(segment, "id");
            target.segmentNames[i] = Text(segment, "name");
            target.segmentSpaces[i] = Text(segment, "space");
            target.segmentStarts[i] = Number(segment, "start", 0f);
            target.segmentEnds[i] = Number(segment, "end", 0f);
            target.eventStartIndex[i] = times.Count;
            JArray events = segment["events"] as JArray;
            if (events == null)
            {
                target.eventCount[i] = 0;
                continue;
            }
            for (int e = 0; e < events.Count; e++)
            {
                times.Add(Number(events[e], "t", 0f));
                types.Add(Text(events[e], "type"));
                ids.Add(Text(events[e], "id"));
            }
            target.eventCount[i] = events.Count;
        }
        target.eventTimes = times.ToArray();
        target.eventTypes = types.ToArray();
        target.eventIds = ids.ToArray();
    }

    private static void FillLyricData(OWLyricData target, JObject lyrics)
    {
        JArray lines = (JArray)lyrics["lines"];
        int count = lines.Count;
        target.lineTexts = new string[count];
        target.lineSegments = new string[count];
        target.lineNumbers = new int[count];
        for (int i = 0; i < count; i++)
        {
            target.lineTexts[i] = Text(lines[i], "text");
            target.lineSegments[i] = Text(lines[i], "segment");
            target.lineNumbers[i] = (int)Number(lines[i], "index", i + 1);
        }

        JArray entries = (JArray)lyrics["greetz"]["entries"];
        JArray switches = (JArray)lyrics["greetz"]["switches"];
        target.greetzEntries = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++) target.greetzEntries[i] = entries[i].ToString();

        target.greetzSwitchTimes = new float[switches.Count];
        target.greetzSwitchIndex = new int[switches.Count];
        for (int i = 0; i < switches.Count; i++)
        {
            // 形如 "act1-golden@40"：编号为整片全局秒数。
            string entry = switches[i].ToString();
            int at = entry.LastIndexOf('@');
            string timeText = at >= 0 ? entry.Substring(at + 1) : "0";
            float seconds;
            if (!float.TryParse(timeText, out seconds)) seconds = 0f;
            target.greetzSwitchTimes[i] = seconds;
            target.greetzSwitchIndex[i] = i;
        }
    }

    private static string[] SpatialSegmentIds(JObject timeline)
    {
        JArray segments = (JArray)timeline["segments"];
        List<string> ids = new List<string>();
        for (int i = 0; i < segments.Count; i++)
        {
            string space = Text(segments[i], "space");
            if (string.IsNullOrEmpty(space)) continue;
            if (space == "Stage/HUD") continue;
            ids.Add(Text(segments[i], "id"));
        }
        return ids.ToArray();
    }

    private static JToken FindSegment(JObject timeline, string id)
    {
        JArray segments = (JArray)timeline["segments"];
        for (int i = 0; i < segments.Count; i++)
        {
            if (Text(segments[i], "id") == id) return segments[i];
        }
        throw new InvalidOperationException("数据里没有段落：" + id);
    }

    private static string SectionName(JToken segment)
    {
        string space = Text(segment, "space");
        if (string.IsNullOrEmpty(space)) return Text(segment, "id");
        int slash = space.LastIndexOf('/');
        if (slash < 0) return space;
        return space.Substring(slash + 1);
    }

    private static void NodeGrid(Transform parent, string name, Vector3 center, Material material,
        int columns, int rows)
    {
        // 保留给临时方阵用；正式节点来自 data/stage.json 的 nodes。
    }

    /// <summary>按 data/stage.json 的分区铺地板、放信标，并在分区之间架桥。</summary>
    private static List<OWStageBeacon> BuildZones(Transform parent, JObject stageData)
    {
        List<OWStageBeacon> beacons = new List<OWStageBeacon>();
        JArray zones = (JArray)stageData["zones"];
        float halfWidth = Number(stageData["grid"], "corridor_half_width", 3f);
        float previousEnd = 0f;
        bool hasPrevious = false;
        for (int i = 0; i < zones.Count; i++)
        {
            JToken zone = zones[i];
            string zoneId = Text(zone, "id");
            string segmentId = Text(zone, "segment");
            float zStart = Number(zone, "z_start", 0f);
            float zEnd = Number(zone, "z_end", 0f);
            float length = zEnd - zStart;
            if (length <= 0.1f) continue;
            float center = (zStart + zEnd) * 0.5f;

            GameObject zoneGo = Child(parent, ZoneObjectName(zoneId));
            GameObject floor = Primitive(PrimitiveType.Cube, zoneGo.transform, "Floor",
                new Vector3(0f, -0.15f, center), new Vector3(halfWidth * 2f, 0.3f, length),
                _gridMaterial, true);
            floor.isStatic = true;

            // 分区之间的过渡桥：宽度收窄到 2.4 m，保证玩家不会掉下去。
            if (hasPrevious && zStart - previousEnd > 0.1f)
            {
                float gapStart = previousEnd;
                float gapEnd = zStart;
                float gapCenter = (gapStart + gapEnd) * 0.5f;
                GameObject bridge = Primitive(PrimitiveType.Cube, zoneGo.transform, "Bridge",
                    new Vector3(0f, -0.15f, gapCenter), new Vector3(2.4f, 0.3f, gapEnd - gapStart),
                    _gridMaterial, true);
                bridge.isStatic = true;
            }
            previousEnd = zEnd;
            hasPrevious = true;

            bool isHope = string.Equals(Text(zone, "palette"), "hope", StringComparison.OrdinalIgnoreCase);
            Material activeMaterial = isHope ? _beaconHopeMaterial : _beaconActiveMaterial;

            GameObject pillar = Primitive(PrimitiveType.Cube, zoneGo.transform, "Beacon",
                new Vector3(halfWidth + 0.6f, 2.2f, center), new Vector3(0.3f, 4.4f, 0.3f),
                _beaconIdleMaterial, false);
            GameObject glow = Primitive(PrimitiveType.Cube, pillar.transform, "Glow",
                new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.05f, 1.8f), activeMaterial, false);
            glow.SetActive(false);

            OWStageBeacon beacon = Udon<OWStageBeacon>(zoneGo);
            beacon.activeSegments = new string[] { segmentId };
            beacon.renderers = new Renderer[] { pillar.GetComponent<Renderer>() };
            beacon.activeMaterial = activeMaterial;
            beacon.idleMaterial = _beaconIdleMaterial;
            beacon.activeObjects = new GameObject[] { glow };
            beacon.idleObjects = new GameObject[0];
            beacons.Add(beacon);
        }
        return beacons;
    }

    private static string ZoneObjectName(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return "Zone";
        return char.ToUpperInvariant(zoneId[0]) + zoneId.Substring(1);
    }

    /// <summary>按 data/stage.json 的 nodes 摆放坐标点与名片卡，文字取 data/labels.json。</summary>
    private static Dictionary<string, Vector3> BuildNodes(Transform parent, JObject stageData, JObject labels)
    {
        Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
        JArray nodes = (JArray)stageData["nodes"];
        JToken card = stageData["card"];
        float plateWidth = Number(card["plate_size"], "x", 1.6f);
        float plateHeight = Number(card["plate_size"], "y", 0.9f);
        float titleHeight = Number(card, "title_text_height", 0.13f);
        float bodyHeight = Number(card, "text_height", 0.09f);
        JObject stateText = labels["state_text"] as JObject;

        for (int i = 0; i < nodes.Count; i++)
        {
            JToken node = nodes[i];
            string id = Text(node, "id");
            string state = Text(node, "state");
            JToken position = node["position"];
            Vector3 point = new Vector3(
                Number(position, "x", 0f), Number(position, "y", 1.4f), Number(position, "z", 0f));
            positions[id] = point;

            GameObject nodeGo = Child(parent, id);
            nodeGo.transform.localPosition = point;
            // 施工单的 rotation_y 是「牌面朝向玩家」；Unity 里可读朝向相反，统一 +180°。
            nodeGo.transform.localRotation = Quaternion.Euler(0f, Number(node, "rotation_y", 0f) + 180f, 0f);

            // 坐标点：状态色自发光小块，压在地板之上的高度
            Primitive(PrimitiveType.Cube, nodeGo.transform, "Dot",
                new Vector3(0f, 0.55f, 0f), new Vector3(0.34f, 0.34f, 0.34f),
                StateMaterial(state), false);

            // 名片卡
            JToken label = null;
            string labelId = Text(node, "label_id");
            if (!string.IsNullOrEmpty(labelId) && labels["node_labels"] != null)
                label = labels["node_labels"][labelId];
            string title = label == null ? id : Text(label, "title");
            string meta = "";
            if (label != null && label["meta"] != null)
            {
                JArray metaLines = label["meta"] as JArray;
                if (metaLines != null)
                {
                    for (int m = 0; m < metaLines.Count; m++)
                    {
                        if (m > 0) meta = meta + "\n";
                        meta = meta + metaLines[m].ToString();
                    }
                }
            }
            string status = stateText == null || stateText[state] == null ? "" : stateText[state].ToString();
            MakePlate(nodeGo.transform, "Plate", Vector3.zero, 0f, title, meta, status,
                StateColor(state), titleHeight, bodyHeight, plateWidth, plateHeight);
        }
        return positions;
    }

    /// <summary>节点之间的连线（LineRenderer，宽度 0.06 m）。</summary>
    private static void BuildLinks(Transform parent, JObject stageData, Dictionary<string, Vector3> positions)
    {
        JArray nodes = (JArray)stageData["nodes"];
        float width = Number(stageData["grid"], "marker_line_width", 0.06f);
        HashSet<string> done = new HashSet<string>();
        for (int i = 0; i < nodes.Count; i++)
        {
            JToken node = nodes[i];
            string fromId = Text(node, "id");
            JArray links = node["links"] as JArray;
            if (links == null || !positions.ContainsKey(fromId)) continue;
            for (int l = 0; l < links.Count; l++)
            {
                string toId = links[l].ToString();
                if (!positions.ContainsKey(toId)) continue;
                string key = string.CompareOrdinal(fromId, toId) < 0 ? fromId + "|" + toId : toId + "|" + fromId;
                if (!done.Add(key)) continue;

                GameObject linkGo = Child(parent, "link_" + fromId + "_" + toId);
                LineRenderer line = linkGo.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = width;
                line.endWidth = width;
                line.sharedMaterial = _linkMaterial;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.SetPosition(0, positions[fromId]);
                line.SetPosition(1, positions[toId]);
            }
        }
    }

    /// <summary>世界空间铭牌：背板 + 标题 + 元信息 + 状态行。</summary>
    private static void MakePlate(Transform parent, string name, Vector3 localPosition, float rotationY,
        string title, string meta, string status, Color statusColor, float titleHeight, float bodyHeight,
        float plateWidth = 1.6f, float plateHeight = 0.9f)
    {
        GameObject plate = Child(parent, name);
        plate.transform.localPosition = localPosition;
        plate.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

        Primitive(PrimitiveType.Cube, plate.transform, "Panel",
            new Vector3(0f, 0f, 0.03f), new Vector3(plateWidth, plateHeight, 0.03f), _plateMaterial, false);

        WorldText3D(plate.transform, "Title", title, new Vector3(0f, plateHeight * 0.28f, 0f), titleHeight,
            Paper, TextAlignmentOptions.Center);
        if (!string.IsNullOrEmpty(meta))
            WorldText3D(plate.transform, "Meta", meta, new Vector3(0f, -plateHeight * 0.16f, 0f), bodyHeight,
                Paper, TextAlignmentOptions.Center);
        if (!string.IsNullOrEmpty(status))
            WorldText3D(plate.transform, "Status", status, new Vector3(0f, -plateHeight * 0.42f, 0f),
                bodyHeight, statusColor, TextAlignmentOptions.Center);
    }

    /// <summary>世界空间 3D 文本（TextMeshPro）：按目标字高反推缩放，跨客户端一致。</summary>
    private static TextMeshPro WorldText3D(Transform parent, string name, string value, Vector3 localPosition,
        float targetHeight, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        TextMeshPro text = go.GetComponent<TextMeshPro>();
        text.text = value;
        text.fontSize = 24f;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        if (_font != null)
        {
            text.font = _font;
            if (_font.material != null) text.fontSharedMaterial = _font.material;
        }
        text.ForceMeshUpdate();
        float measured = text.preferredHeight;
        if (measured > 0.01f) go.transform.localScale = Vector3.one * (targetHeight / measured);
        return text;
    }

    private static Material StateMaterial(string state)
    {
        if (state == "throttled") return _nodeThrottledMaterial;
        if (state == "dead") return _nodeDeadMaterial;
        if (state == "archived") return _nodeArchivedMaterial;
        if (state == "exported") return _nodeExportedMaterial;
        if (state == "revived") return _nodeHopeMaterial;
        return _nodeOnlineMaterial;
    }

    private static Color StateColor(string state)
    {
        if (state == "throttled") return Cyan;
        if (state == "dead") return new Color32(0x88, 0x88, 0x90, 0xFF);
        if (state == "archived") return new Color32(0xB0, 0xB0, 0xB8, 0xFF);
        if (state == "exported") return Magenta;
        if (state == "revived") return Hope;
        return Amber;
    }

    private static string UiText(JObject labels, string key)
    {
        JToken ui = labels == null ? null : labels["ui"];
        if (ui == null || ui[key] == null) return "";
        return ui[key].ToString();
    }

    private static string PlateText(JObject labels, string key)
    {
        JToken plates = labels == null || labels["ui"] == null ? null : labels["ui"]["plates"];
        if (plates == null || plates[key] == null) return "";
        return plates[key].ToString();
    }

    private static void SetSpawn(GameObject spawn)
    {
        VRCSceneDescriptor descriptor = UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
        if (descriptor == null)
        {
            Debug.LogWarning("场景里没有 VRCSceneDescriptor，未能设置出生点。");
            return;
        }
        SerializedObject serialized = new SerializedObject(descriptor);
        SerializedProperty spawns = serialized.FindProperty("spawns");
        if (spawns == null)
        {
            Debug.LogWarning("VRCSceneDescriptor 没有 spawns 字段，保持原出生点。");
            return;
        }
        spawns.arraySize = 1;
        spawns.GetArrayElementAtIndex(0).objectReferenceValue = spawn;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(descriptor);
    }

    /// <summary>九十年代终端底色：去掉天空盒，环境光压暗，只留一盏低强度定向光。</summary>
    private static void ApplyEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color32(0x2A, 0x2A, 0x33, 0xFF);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Background;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 150f;

        Light sun = null;
        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
        }
        if (sun != null)
        {
            sun.intensity = 0.35f;
            sun.color = new Color32(0xC8, 0xD8, 0xFF, 0xFF);
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            EditorUtility.SetDirty(sun);
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            EditorUtility.SetDirty(camera);
        }
    }

    private static void EnsureSceneInBuild()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        bool found = false;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != ScenePath) continue;
            scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
            found = true;
        }
        if (!found)
        {
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            Debug.Log("已把 " + ScenePath + " 加入 Build Settings。");
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- primitives

    private static GameObject Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject Primitive(PrimitiveType type, Transform parent, string name,
        Vector3 position, Vector3 scale, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        if (!collider)
        {
            Collider existing = go.GetComponent<Collider>();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        }
        return go;
    }

    private static TextMeshProUGUI WorldText(Transform parent, string name, string value,
        Vector3 localPosition, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        if (_font != null)
        {
            text.font = _font;
            if (_font.material != null) text.fontSharedMaterial = _font.material;
        }
        return text;
    }
}
