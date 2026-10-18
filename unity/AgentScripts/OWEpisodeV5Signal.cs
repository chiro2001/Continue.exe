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
using Object = UnityEngine.Object;

// V5 Signal module: phosphor oscilloscope materials + tracker side panels.
// Run only through Pipeline in the stopped V5 scene. Entries: BuildPhosphor, BuildTracker, Validate.
public static class OWEpisodeV5Signal
{
    const string ScenePath = "Assets/Scenes/OWM_Episode123_v5.unity";
    const string Root = "Assets/OldWebMap/Episode123/Revision5/Signal";
    const string V4Root = "Assets/OldWebMap/Episode123/Revision4";
    const string DataPath = "AgentScripts/data/episode123-v5-tracker.json";
    const string PhosphorShader = "OldWebMap/V5/Phosphor Oscilloscope";
    const string TrackerShader = "OldWebMap/V5/Tracker Pattern";
    const string TrackerRootName = "Revision5 Tracker";
    // Panel placement: world (performance root is identity), yawed to face the reference eye.
    static readonly Vector3 Eye = new Vector3(0, 1.35f, -2.8f);
    static readonly Vector3[] PanelCentres = { new Vector3(-5.0f, 1.52f, 6.2f), new Vector3(5.0f, 1.52f, 6.2f) };
    static readonly string[] PanelNames = { "Tracker panel CH1-CH2 (left)", "Tracker panel CH3-CH4 (right)" };
    const float PanelW = 1.7f, PanelH = 3.0f;
    static readonly Color[] Phosphor = { new Color(.35f, 1f, .45f, 1), new Color(.3f, .85f, 1f, 1), new Color(1f, .7f, .25f, 1), new Color(1f, .4f, .85f, 1) };
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/episode123-v5");

    static T F<T>() where T : Object => Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static void Folder(string p) { if (AssetDatabase.IsValidFolder(p)) return; int i = p.LastIndexOf('/'); Folder(p.Substring(0, i)); AssetDatabase.CreateFolder(p.Substring(0, i), p.Substring(i + 1)); }
    static string Report(string name, object value) { Directory.CreateDirectory(Tmp + "/reports"); string s = JsonConvert.SerializeObject(value, Formatting.Indented); File.WriteAllText(Tmp + "/reports/" + name, s); return s; }
    static void Require() { if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Stopped, compiled V5 scene required: " + ScenePath); }
    static void Sync(UdonSharpBehaviour b) { UdonSharpEditorUtility.CopyProxyToUdon(b); var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(b); if (vm != null) EditorUtility.SetDirty(vm); EditorUtility.SetDirty(b); }
    static Material MaterialAsset(string name, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Required shader unavailable: " + shaderName);
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
    }
    static Mesh RibbonMesh(string name, int columns)
    {
        string path = Root + "/Meshes/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); } else mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        var vertices = new Vector3[columns * 2]; var uv = new Vector2[vertices.Length]; var indices = new int[(columns - 1) * 6];
        for (int i = 0; i < columns; i++)
        {
            float u = i / (float)(columns - 1);
            vertices[i * 2] = new Vector3(u - .5f, -1, 0); vertices[i * 2 + 1] = new Vector3(u - .5f, 1, 0);
            uv[i * 2] = new Vector2(u, 0); uv[i * 2 + 1] = new Vector2(u, 1);
            if (i == columns - 1) continue;
            int at = i * 6, a = i * 2;
            indices[at] = a; indices[at + 1] = a + 1; indices[at + 2] = a + 2; indices[at + 3] = a + 2; indices[at + 4] = a + 1; indices[at + 5] = a + 3;
        }
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices; mesh.bounds = new Bounds(Vector3.zero, new Vector3(18, 8, 10));
        EditorUtility.SetDirty(mesh); return mesh;
    }
    static Mesh PanelQuad()
    {
        string path = Root + "/Meshes/Tracker panel quad.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); mesh.name = "Tracker panel quad"; AssetDatabase.CreateAsset(mesh, path); } else mesh.Clear();
        mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1, 1, .1f));
        EditorUtility.SetDirty(mesh); return mesh;
    }
    static void ConfigureSurface(MeshRenderer r)
    {
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off; r.allowOcclusionWhenDynamic = false;
    }

    // ------------------------------------------------------------------ phosphor
    public static object BuildPhosphor()
    {
        Require();
        var osc = F<OWV4Oscilloscope>();
        if (osc == null || osc.waveMaterials == null || osc.waveMaterials.Length != 4 || osc.regionalRenderers == null || osc.regionalRenderers.Length != 16 || osc.waveRenderers == null || osc.waveRenderers.Length != 4)
            throw new InvalidOperationException("V4 oscilloscope with 4 materials, 4 wave renderers and 16 regional renderers required");
        Folder(Root + "/Materials"); Folder(Root + "/Meshes"); Folder(Root + "/Textures");
        var v4 = new Material[4]; var v5 = new Material[4]; var map = new Dictionary<Material, Material>();
        string[] copied = { "_Channel", "_Row", "_Length", "_Amplitude", "_Width", "_Bend", "_Curl", "_Twist", "_Timeline", "_Energy", "_Constrain", "_Gain" };
        for (int ch = 0; ch < 4; ch++)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(V4Root + "/Materials/Actual waveform CH0" + (ch + 1) + ".mat");
            var current = osc.waveMaterials[ch];
            if (src == null && current != null && AssetDatabase.GetAssetPath(current).StartsWith(V4Root)) src = current;
            if (src == null) throw new InvalidOperationException("V4 waveform material missing for channel " + ch);
            var atlas = src.GetTexture("_WaveAtlas") as Texture2D;
            if (atlas == null) throw new InvalidOperationException("V4 waveform atlas missing on " + src.name);
            var mat = MaterialAsset("V5 Phosphor CH0" + (ch + 1), PhosphorShader);
            foreach (string name in copied) if (src.HasProperty(name) && mat.HasProperty(name)) mat.SetFloat(name, src.GetFloat(name));
            mat.SetTexture("_WaveAtlas", atlas); mat.SetFloat("_Channel", ch); mat.SetColor("_Color", Phosphor[ch]);
            if (mat.GetFloat("_Width") <= 0) mat.SetFloat("_Width", .04f);
            mat.SetFloat("_RowStep", 1f / atlas.height);
            mat.SetFloat("_Persistence", .5f); mat.SetFloat("_GhostWiden", .2f); mat.SetFloat("_SlewDim", .05f); mat.SetFloat("_SlewFloor", .35f);
            mat.SetFloat("_HaloWidth", 1.6f); mat.SetFloat("_CoreK", 7f); mat.SetFloat("_HotCore", .55f); mat.SetFloat("_Intensity", ch == 0 ? .95f : 1.25f); mat.SetFloat("_NearFadeStart", 2.5f); mat.SetFloat("_NearFadeEnd", 5f); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcColor);
            EditorUtility.SetDirty(mat); v4[ch] = src; v5[ch] = mat; map[src] = mat;
        }
        // Ribbon resolution: the atlas has one sample per column; fewer mesh columns would smear the trace.
        int atlasColumns = ((Texture2D)v5[0].GetTexture("_WaveAtlas")).width;
        var firstFilter = osc.regionalRenderers[0].GetComponent<MeshFilter>();
        Mesh mesh = firstFilter.sharedMesh; int columns = mesh == null ? 0 : mesh.vertexCount / 2; bool replaced = false;
        if (columns < atlasColumns) { mesh = RibbonMesh("V5 phosphor ribbon " + atlasColumns * 2, atlasColumns * 2); columns = atlasColumns * 2; replaced = true; }
        int renderersChanged = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            var shared = r.sharedMaterials; bool dirty = false;
            for (int i = 0; i < shared.Length; i++) if (shared[i] != null && map.ContainsKey(shared[i])) { shared[i] = map[shared[i]]; dirty = true; }
            if (dirty) { r.sharedMaterials = shared; EditorUtility.SetDirty(r); renderersChanged++; }
        }
        for (int i = 0; i < 16; i++)
        {
            var r = osc.regionalRenderers[i]; r.sharedMaterial = v5[i % 4];
            if (replaced) r.GetComponent<MeshFilter>().sharedMesh = mesh;
            EditorUtility.SetDirty(r);
        }
        for (int ch = 0; ch < 4; ch++) { osc.waveRenderers[ch].sharedMaterial = v5[ch]; if (replaced) osc.waveRenderers[ch].GetComponent<MeshFilter>().sharedMesh = mesh; }
        osc.waveMaterials = v5; Sync(osc);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return JsonConvert.SerializeObject(new { scene = ScenePath, materials = v5.Select(m => AssetDatabase.GetAssetPath(m)).ToArray(), regionalRenderers = 16, renderersChanged, ribbonColumns = columns, ribbonReplaced = replaced, v4MaterialsUntouched = v4.Select(m => AssetDatabase.GetAssetPath(m)).ToArray(), sceneSaved = false });
    }

    // ------------------------------------------------------------------ tracker
    static Texture2D ImportAtlas(JObject data)
    {
        string path = Root + "/Textures/" + (string)data["atlas"]["file"];
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Copy art/export/episode123-v5/signal/tracker-atlas.png to " + path + " first", path);
        int width = (int)data["atlas"]["width"], height = (int)data["atlas"]["height"], size = Mathf.Max(width, height);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = size > 2048 ? 4096 : 2048; importer.isReadable = false;
        var settings = importer.GetPlatformTextureSettings("Standalone");
        settings.name = "Standalone"; settings.overridden = true; settings.format = TextureImporterFormat.RGBA32; settings.maxTextureSize = importer.maxTextureSize; settings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(settings); importer.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null || tex.width != width || tex.height != height) throw new InvalidOperationException("Tracker atlas dimensions changed during import");
        return tex;
    }
    public static object BuildTracker()
    {
        Require();
        var data = JObject.Parse(File.ReadAllText(DataPath));
        if ((string)data["schema"] != "oldwebmap.tracker.v5") throw new InvalidOperationException("Unexpected tracker data schema");
        Folder(Root + "/Materials"); Folder(Root + "/Meshes"); Folder(Root + "/Textures");
        var atlas = ImportAtlas(data); var quad = PanelQuad();
        var director = F<OWEpisodeDirector>(); var osc = F<OWV4Oscilloscope>(); var presentation = F<OWV4Presentation>(); var journey = F<OWV4Journey>();
        if (director == null || osc == null) throw new InvalidOperationException("Director and V4 oscilloscope required");
        GameObject performance = presentation != null ? presentation.performanceRoot : null;
        if (performance == null) { var episode = GameObject.Find("OWEpisode123"); var t = episode == null ? null : episode.transform.Find("Revision4Performance"); performance = t == null ? null : t.gameObject; }
        if (performance == null) throw new InvalidOperationException("OWEpisode123/Revision4Performance not found");
        if (journey != null && journey.worldFrame != null && performance.transform.IsChildOf(journey.worldFrame)) throw new InvalidOperationException("Performance root must not be inside the world frame");
        var old = performance.transform.Find(TrackerRootName); if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(TrackerRootName); root.transform.SetParent(performance.transform, false);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
        var materials = new Material[2]; var renderers = new Renderer[2];
        for (int side = 0; side < 2; side++)
        {
            var mat = MaterialAsset(side == 0 ? "V5 Tracker CH1-CH2" : "V5 Tracker CH3-CH4", TrackerShader);
            mat.SetTexture("_Atlas", atlas); mat.SetFloat("_Panel", side); mat.SetVector("_PanelPx", new Vector4(144, 254, 0, 0));
            mat.SetColor("_Ch0", Phosphor[0]); mat.SetColor("_Ch1", Phosphor[1]); mat.SetColor("_Ch2", Phosphor[2]); mat.SetColor("_Ch3", Phosphor[3]);
            mat.SetFloat("_Opacity", 1f); mat.SetFloat("_RowPosition", 0f); mat.SetVector("_Flash", Vector4.zero); mat.SetVector("_Levels", Vector4.zero); mat.SetFloat("_Timeline", 0f);
            mat.SetFloat("_BackAlpha", .55f); mat.SetFloat("_Brightness", 1.15f); mat.SetFloat("_TrailSeconds", .38f); mat.SetFloat("_Smooth", .6f);
            EditorUtility.SetDirty(mat);
            var go = new GameObject(PanelNames[side]); go.transform.SetParent(root.transform, false);
            Vector3 look = PanelCentres[side] - Eye; look.y = 0;
            go.transform.SetPositionAndRotation(PanelCentres[side], Quaternion.LookRotation(look.normalized, Vector3.up)); go.transform.localScale = new Vector3(PanelW, PanelH, 1);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; ConfigureSurface(r);
            materials[side] = mat; renderers[side] = r;
        }
        var tracker = root.AddUdonSharpComponent<OWV5Tracker>();
        tracker.director = director; tracker.oscilloscope = osc; tracker.materials = materials; tracker.renderers = renderers;
        tracker.noteTimes0 = data["channels"][0]["noteOnTimes"].ToObject<float[]>(); tracker.noteTimes1 = data["channels"][1]["noteOnTimes"].ToObject<float[]>();
        tracker.noteTimes2 = data["channels"][2]["noteOnTimes"].ToObject<float[]>(); tracker.noteTimes3 = data["channels"][3]["noteOnTimes"].ToObject<float[]>();
        tracker.rowSeconds = (float)data["rowSeconds"]; tracker.duration = director.duration; tracker.autoSample = true;
        tracker.Sample(0f); Sync(tracker);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return JsonConvert.SerializeObject(new { scene = ScenePath, root = TrackerRootName, parent = performance.name, panels = PanelNames, atlas = AssetDatabase.GetAssetPath(atlas), atlasSize = new[] { atlas.width, atlas.height }, materials = materials.Select(m => AssetDatabase.GetAssetPath(m)).ToArray(), noteOnCounts = new[] { tracker.noteTimes0.Length, tracker.noteTimes1.Length, tracker.noteTimes2.Length, tracker.noteTimes3.Length }, sceneSaved = false });
    }

    // ------------------------------------------------------------------ validation helpers
    static uint[] _crcTable;
    static uint Crc32(byte[] bytes)
    {
        if (_crcTable == null) { _crcTable = new uint[256]; for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; _crcTable[n] = c; } }
        uint crc = 0xFFFFFFFFu; foreach (byte b in bytes) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8); return crc ^ 0xFFFFFFFFu;
    }
    static string Sha256(string path) { using (var sha = System.Security.Cryptography.SHA256.Create()) using (var s = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
    static Vector2 Ndc(Vector3 p) { Vector3 d = p - Eye; float t = Mathf.Tan(26f * Mathf.Deg2Rad); return new Vector2(d.x / d.z / (t * 16f / 9f), d.y / d.z / t); }
    static Rect NdcRect(IEnumerable<Vector3> points)
    {
        var n = points.Select(Ndc).ToArray(); float x0 = n.Min(v => v.x), x1 = n.Max(v => v.x), y0 = n.Min(v => v.y), y1 = n.Max(v => v.y); return Rect.MinMaxRect(x0, y0, x1, y1);
    }
    static bool Near(float a, float b, float tol) => Mathf.Abs(a - b) <= tol;
    static void Fail(List<string> problems, string message) { problems.Add(message); }

    public static object Validate()
    {
        Require();
        var problems = new List<string>();
        var osc = F<OWV4Oscilloscope>(); var tracker = F<OWV5Tracker>(); var journey = F<OWV4Journey>();
        if (osc == null) throw new InvalidOperationException("V4 oscilloscope missing");
        if (tracker == null) throw new InvalidOperationException("Run BuildTracker first (OWV5Tracker missing)");
        UdonSharpEditorUtility.CopyUdonToProxy(osc); UdonSharpEditorUtility.CopyUdonToProxy(tracker);
        var data = JObject.Parse(File.ReadAllText(DataPath));

        // ---- phosphor materials
        var shaderMessages = new[] { PhosphorShader, TrackerShader }.Select(name => new { name, messages = ShaderUtil.GetShaderMessages(Shader.Find(name)).Select(m => new { m.message, m.line, severity = m.severity.ToString() }).ToArray() }).ToArray();
        if (shaderMessages.Any(s => s.messages.Any(m => m.severity == "Error"))) Fail(problems, "shader compiler errors: " + JsonConvert.SerializeObject(shaderMessages));
        var phosphorOk = new List<object>();
        for (int ch = 0; ch < 4; ch++)
        {
            var mat = osc.waveMaterials[ch]; string path = AssetDatabase.GetAssetPath(mat);
            bool good = mat != null && mat.shader != null && mat.shader.name == PhosphorShader && path.StartsWith(Root + "/Materials/") && AssetDatabase.Contains(mat) && mat.passCount == 4 && (int)mat.GetFloat("_Channel") == ch;
            var atlas = mat == null ? null : mat.GetTexture("_WaveAtlas") as Texture2D;
            good = good && atlas != null && atlas.filterMode == FilterMode.Point && Near(mat.GetFloat("_RowStep"), 1f / atlas.height, 1e-9f);
            phosphorOk.Add(new { ch, path, good }); if (!good) Fail(problems, "phosphor material wrong for channel " + ch);
            var v4 = AssetDatabase.LoadAssetAtPath<Material>(V4Root + "/Materials/Actual waveform CH0" + (ch + 1) + ".mat");
            if (v4 == null || v4.shader == null || v4.shader.name != "OldWebMap/V3/Triggered Oscilloscope") Fail(problems, "V4 waveform material changed or missing for channel " + ch);
        }
        int rendererMismatch = 0;
        for (int i = 0; i < 16; i++) if (osc.regionalRenderers[i].sharedMaterial != osc.waveMaterials[i % 4]) rendererMismatch++;
        for (int ch = 0; ch < 4; ch++) if (osc.waveRenderers[ch].sharedMaterial != osc.waveMaterials[ch]) rendererMismatch++;
        if (rendererMismatch > 0) Fail(problems, "wave renderers not on phosphor materials: " + rendererMismatch);
        var v4Materials = Enumerable.Range(1, 4).Select(i => AssetDatabase.LoadAssetAtPath<Material>(V4Root + "/Materials/Actual waveform CH0" + i + ".mat")).Where(m => m != null).ToArray();
        var stale = Object.FindObjectsOfType<Renderer>(true).Where(r => r.sharedMaterials.Any(m => m != null && v4Materials.Contains(m))).Select(r => r.name).ToArray();
        if (stale.Length > 0) Fail(problems, "renderers still use the V4 waveform materials: " + string.Join(",", stale));
        var ribbon = osc.regionalRenderers[0].GetComponent<MeshFilter>().sharedMesh;
        int atlasColumns = ((Texture2D)osc.waveMaterials[0].GetTexture("_WaveAtlas")).width;
        if (ribbon == null || ribbon.vertexCount / 2 < atlasColumns) Fail(problems, "ribbon has fewer columns than the atlas");
        // Ghost rows follow the atlas: _Row + k*_RowStep == row of frame max(f-k,0), clamped at frame 0.
        var rowRows = new List<object>();
        foreach (float t in new[] { 0f, .05f, .1f, 5.12f, 40f, 86.4f, 130f })
        {
            osc.Seek(t); var mat = osc.waveMaterials[2]; int h = ((Texture2D)mat.GetTexture("_WaveAtlas")).height; int f = Mathf.FloorToInt(t * 30f);
            float row = mat.GetFloat("_Row"), step = mat.GetFloat("_RowStep");
            if (!Near(row, 1f - (f + .5f) / h, 1e-6f)) Fail(problems, "V4 _Row convention changed at " + t);
            var ghosts = new List<float>();
            for (int k = 1; k <= 3; k++)
            {
                float g = Mathf.Min(row + k * step, 1f - .5f / h), want = 1f - (Mathf.Max(f - k, 0) + .5f) / h; ghosts.Add(g);
                if (!Near(g, want, 1e-6f)) Fail(problems, "ghost " + k + " row mismatch at " + t);
            }
            rowRows.Add(new { t, frame = f, row, ghosts });
        }
        osc.Seek(0f);

        // ---- tracker data and atlas
        string atlasPath = Root + "/Textures/" + (string)data["atlas"]["file"];
        var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter; var atlasTex = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        if (importer == null || atlasTex == null) throw new InvalidOperationException("Tracker atlas missing at " + atlasPath);
        int aw = (int)data["atlas"]["width"], ah = (int)data["atlas"]["height"];
        if (importer.sRGBTexture || importer.mipmapEnabled || importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.alphaIsTransparency || atlasTex.width != aw || atlasTex.height != ah)
            Fail(problems, "tracker atlas import settings/dimensions wrong");
        string fileHash = Sha256(atlasPath); if (fileHash != (string)data["atlas"]["sha256"]) Fail(problems, "atlas PNG hash differs from tracker JSON");
        var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); decoded.LoadImage(File.ReadAllBytes(atlasPath)); var px = decoded.GetPixels32();
        if (decoded.width != aw || decoded.height != ah) Fail(problems, "decoded atlas dimensions differ");
        var expected = new Dictionary<float, string[]> {
            // tracks.csv: 1,2,0,5.12 note5 inst11 | 2,2,0,5.12 note27 inst7
            { 5.12f, new[] { "F-0 0B", "D#2 07", "--- --", "--- --" } },
            // 1,11,16,29.44 note12 inst5 | 3,11,16,29.44 note29 inst7
            { 29.44f, new[] { "C-1 05", "--- --", "F-2 07", "--- --" } },
            // 1,32,0,81.92 note12 inst4 | 3,32,0,81.92 note29 inst1 | 4,32,0,81.92 note5 inst3
            { 81.92f, new[] { "C-1 04", "--- --", "F-2 01", "F-0 03" } },
            // 1,40,0,102.4 note12 inst4 | 2,40,0,102.4 note5 inst6
            { 102.4f, new[] { "C-1 04", "F-0 06", "--- --", "--- --" } } };
        var spotRows = new List<object>(); int slotW = (int)data["atlas"]["slotWidth"], slotH = (int)data["atlas"]["slotHeight"], slotsX = (int)data["atlas"]["slotsX"];
        foreach (var s in (JArray)data["spot"])
        {
            float time = (float)s["time"]; int row = (int)s["row"]; int slot = (int)s["slot"]; var cells = s["cells"].ToObject<string[]>();
            if (row != Mathf.RoundToInt(time / .08f) || (int)s["order"] != row / 32 || (int)s["rowInOrder"] != row % 32) Fail(problems, "spot row bookkeeping wrong at " + time);
            foreach (var kv in expected) if (Near(kv.Key, time, 1e-4f) && !cells.SequenceEqual(kv.Value)) Fail(problems, "baked row at " + time + " differs from tracks.csv: " + string.Join("|", cells));
            int x0 = (slot % slotsX) * slotW, y0 = (slot / slotsX) * slotH; var bytes = new byte[slotW * slotH * 4]; int at = 0;
            for (int yy = 0; yy < slotH; yy++) for (int xx = 0; xx < slotW; xx++) { var p = px[(ah - 1 - (y0 + yy)) * aw + x0 + xx]; bytes[at++] = p.r; bytes[at++] = p.g; bytes[at++] = p.b; bytes[at++] = p.a; }
            uint crc = Crc32(bytes); if (crc != (uint)(long)s["stripCrc32"]) Fail(problems, "atlas strip CRC differs from Python bake at " + time);
            spotRows.Add(new { time, row, cells, crc });
        }
        Object.DestroyImmediate(decoded);
        foreach (var kv in expected) if (!((JArray)data["spot"]).Any(s => Near((float)s["time"], kv.Key, 1e-4f))) Fail(problems, "missing spot row " + kv.Key);
        var counts = data["eventCounts"].ToObject<int[]>(); if (!counts.SequenceEqual(new[] { 963, 487, 656, 847 })) Fail(problems, "event counts differ from tracks.csv");

        // ---- flash timing equals baked CSV note-on times
        float[][] baked = { tracker.noteTimes0, tracker.noteTimes1, tracker.noteTimes2, tracker.noteTimes3 };
        var flashRows = new List<object>();
        for (int ch = 0; ch < 4; ch++)
        {
            var want = data["channels"][ch]["noteOnTimes"].ToObject<float[]>();
            if (baked[ch] == null || baked[ch].Length != want.Length) { Fail(problems, "baked note-on count differs on ch" + (ch + 1)); continue; }
            for (int i = 0; i < want.Length; i++) if (!Near(baked[ch][i], want[i], 1e-5f) || (i > 0 && baked[ch][i] <= baked[ch][i - 1])) { Fail(problems, "baked note-on differs/unsorted on ch" + (ch + 1) + " at " + i); break; }
            int sample = Mathf.Max(1, want.Length / 14);
            for (int i = 0; i < want.Length; i += sample)
            {
                float t = want[i];
                if (t > tracker.duration - .4f) break;
                tracker.Sample(t); float on = ch == 0 ? tracker.sampledFlash.x : ch == 1 ? tracker.sampledFlash.y : ch == 2 ? tracker.sampledFlash.z : tracker.sampledFlash.w;
                tracker.Sample(Mathf.Max(0f, t - .0005f)); float before = ch == 0 ? tracker.sampledFlash.x : ch == 1 ? tracker.sampledFlash.y : ch == 2 ? tracker.sampledFlash.z : tracker.sampledFlash.w;
                if (!Near(on, 1f, 1e-3f) || (t > .01f && before > .95f)) Fail(problems, "flash not aligned to note-on ch" + (ch + 1) + " t=" + t);
                bool next = i + 1 < want.Length && want[i + 1] - t < tracker.flashDecay + .02f;
                if (!next) { tracker.Sample(t + tracker.flashDecay); float late = ch == 0 ? tracker.sampledFlash.x : ch == 1 ? tracker.sampledFlash.y : ch == 2 ? tracker.sampledFlash.z : tracker.sampledFlash.w; if (!Near(late, Mathf.Exp(-1f), .01f)) Fail(problems, "flash decay wrong ch" + (ch + 1) + " t=" + t); }
                if (flashRows.Count < 24) flashRows.Add(new { ch = ch + 1, t, on, before });
            }
        }

        // ---- opacity envelope (hand-derived plateau/midpoint values)
        var opacityRows = new List<object>();
        var opacityWant = new (float t, float v)[] { (0f, 0f), (.2f, .5f), (.4f, 1f), (15f, 1f), (30.72f, 1f), (31.36f, .64f), (32f, .28f), (50f, .28f), (79.36f, .28f), (80f, .64f), (80.64f, 1f), (85f, 1f), (92.16f, 1f), (92.8f, .64f), (93.44f, .28f), (100f, .28f), (122.88f, .28f), (123.52f, .44f), (124.16f, .6f), (128f, .6f), (132.32f, .6f), (132.72f, .3f), (133.12f, 0f) };
        foreach (var (t, v) in opacityWant) { tracker.Sample(t); opacityRows.Add(new { t, v, got = tracker.sampledOpacity }); if (!Near(tracker.sampledOpacity, v, 2e-3f)) Fail(problems, "opacity at " + t + " = " + tracker.sampledOpacity + ", expected " + v); }
        tracker.Sample(86.4f); int frame = Mathf.FloorToInt(86.4f * osc.frameRate);
        var lv = tracker.sampledLevels; if (lv.x < 0 || lv.x > 1 || lv.y < 0 || lv.y > 1 || lv.z < 0 || lv.z > 1 || lv.w < 0 || lv.w > 1 || lv.x + 1e-4f < Mathf.Min(1f, osc.energy0[frame]) || lv.z + 1e-4f < Mathf.Min(1f, osc.energy2[frame])) Fail(problems, "levels not derived from oscilloscope energy at 86.4 s");
        if (!Near(tracker.sampledRow, 86.4f / .08f, 1e-2f)) Fail(problems, "row position != time/0.08");

        // ---- panels: placement, projection, room
        var rootT = tracker.transform; var rows = new List<object>();
        if (journey != null && journey.worldFrame != null && rootT.IsChildOf(journey.worldFrame)) Fail(problems, "tracker lives inside the travelling world frame");
        if (rootT.parent == null || rootT.parent.name != "Revision4Performance") Fail(problems, "tracker is not directly under Revision4Performance");
        var focus = NdcRect(new[] { -2f, 2f }.SelectMany(x => new[] { 1.6f, 3.7f }.Select(y => new Vector3(x, y, 5.4f))));
        var lane = NdcRect(new[] { -5.8f, 5.8f }.SelectMany(x => new[] { 3.11f - .12f, 3.95f + .12f }.SelectMany(y => new[] { 4.6f, 5.32f }.Select(z => new Vector3(x, y, z)))));
        var room = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "OpeningRoom");
        if (journey != null) journey.Sample(0f);
        var shellNames = new[] { "Visible opening floor", "Room side", "Room ceiling", "Room far wall" };
        for (int side = 0; side < 2; side++)
        {
            var panel = rootT.Find(PanelNames[side]); if (panel == null) { Fail(problems, "missing " + PanelNames[side]); continue; }
            var corners = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) }.Select(panel.TransformPoint).ToArray();
            bool front = corners.All(c => c.z - Eye.z > .5f); var rect = front ? NdcRect(corners) : new Rect();
            bool inFrame = front && rect.xMin > -.97f && rect.xMax < .97f && rect.yMin > -.95f && rect.yMax < .95f;
            bool focusOverlap = front && rect.Overlaps(focus); bool laneOverlap = front && rect.Overlaps(lane);
            bool insideRoom = corners.All(c => Mathf.Abs(c.x) < 8.3f && c.z > -4.3f && c.z < 14.3f && c.y > -.1f && c.y < 5.6f);
            var bounds = new Bounds(corners[0], Vector3.zero); foreach (var c in corners) bounds.Encapsulate(c); bounds.Expand(new Vector3(.05f, .05f, .3f));
            var hits = room == null ? new string[0] : room.GetComponentsInChildren<Renderer>(true).Where(r => !shellNames.Contains(r.name) && r.bounds.Intersects(bounds)).Select(r => r.transform.parent == null ? r.name : r.transform.parent.name + "/" + r.name).Distinct().ToArray();
            Vector3 toEye = Eye - panel.position; toEye.y = 0; float yawError = Vector3.Angle(-panel.forward, toEye.normalized);
            if (!inFrame) Fail(problems, PanelNames[side] + " leaves the reference frame");
            if (focusOverlap) Fail(problems, PanelNames[side] + " overlaps the focus rectangle");
            if (!insideRoom) Fail(problems, PanelNames[side] + " is outside the opening room volume");
            if (hits.Length > 0) Fail(problems, PanelNames[side] + " intersects room props: " + string.Join(",", hits));
            if (yawError > 1f) Fail(problems, PanelNames[side] + " does not face the reference eye");
            if (laneOverlap) Fail(problems, PanelNames[side] + " overlaps the danmaku lane band");
            rows.Add(new { panel = PanelNames[side], ndc = new[] { rect.xMin, rect.yMin, rect.xMax, rect.yMax }, inFrame, focusOverlap, laneOverlap, danmakuClearanceNdc = lane.yMin - rect.yMax, insideRoom, roomPropHits = hits, yawError });
        }
        var panelMaterials = tracker.materials; if (panelMaterials == null || panelMaterials.Length != 2 || panelMaterials.Any(m => m == null || !AssetDatabase.Contains(m) || m.shader.name != TrackerShader || m.GetTexture("_Atlas") != atlasTex)) Fail(problems, "tracker materials not persisted with the atlas");
        if (tracker.renderers == null || tracker.renderers.Length != 2 || tracker.renderers.Any(r => r == null || r.GetComponent<MeshFilter>().sharedMesh == null || !AssetDatabase.Contains(r.GetComponent<MeshFilter>().sharedMesh))) Fail(problems, "tracker panel meshes not persisted");
        tracker.Sample(0f); osc.Seek(0f);

        var result = new { success = problems.Count == 0, problems, scene = ScenePath, shaderMessages, phosphor = phosphorOk, ribbonColumns = ribbon == null ? 0 : ribbon.vertexCount / 2, ghostRows = rowRows, atlas = new { atlasPath, fileHash, aw, ah }, spotRows, flashRows, opacityRows, panels = rows, stereoVisualCheckStillRequired = true };
        string json = Report("signal-validate.json", result);
        if (problems.Count > 0) throw new InvalidOperationException(json);
        return new { success = true, phosphorMaterials = 4, regionalRenderers = 16, spotRows = spotRows.Count, flashSamples = flashRows.Count, opacitySamples = opacityRows.Count, panels = rows.Count, report = Tmp + "/reports/signal-validate.json" };
    }
}
