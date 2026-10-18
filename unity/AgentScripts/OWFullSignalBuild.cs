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

// Independent Pipeline entry; does not call any transient run_script builder class.
// Requires a copied V5 scene and imported OWFullSignal UdonSharp program.
public static class OWFullSignalBuild
{
    const string ScenePath = "Assets/Scenes/OWM_FullFilm_v1.unity";
    const string Root = "Assets/OldWebMap/FullFilm/Revision1/Signal";
    const string DataPath = "AgentScripts/data/fullfilm-v1-signal.json";
    const string WavePath = "AgentScripts/data/fullfilm-v1-waveforms.json";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v1");
    static T F<T>() where T : Component => Object.FindObjectsOfType<T>(true).FirstOrDefault(x => x.gameObject.scene == SceneManager.GetActiveScene());
    static void Guard()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Stopped, compiled full-film scene required: " + ScenePath);
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/'); Folder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
    static void Sync(UdonSharpBehaviour behaviour)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
        if (vm != null) EditorUtility.SetDirty(vm);
        EditorUtility.SetDirty(behaviour);
    }
    static string Report(string file, object value)
    {
        Directory.CreateDirectory(Tmp + "/reports");
        string text = JsonConvert.SerializeObject(value, Formatting.Indented);
        File.WriteAllText(Tmp + "/reports/" + file, text); return text;
    }
    static Transform Node(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found;
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    static float[] Floats(JToken values) => values.Select(x => (float)x).ToArray();
    static Color Col(JToken rgb) => new Color((float)rgb[0], (float)rgb[1], (float)rgb[2], 1f);
    static Material Clone(string name, Material source, Dictionary<Material, Material> map)
    {
        if (source == null) throw new InvalidOperationException("Material source missing: " + name);
        string path = Root + "/Materials/" + name + ".mat";
        var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (copy == source) return copy;
        if (copy == null) { copy = new Material(source); copy.name = name; AssetDatabase.CreateAsset(copy, path); }
        else { copy.shader = source.shader; copy.CopyPropertiesFromMaterial(source); }
        map[source] = copy; EditorUtility.SetDirty(copy); return copy;
    }
    static Texture2D Import(string file, int width, int height)
    {
        string path = Root + "/Textures/" + file;
        if (!File.Exists(path)) throw new FileNotFoundException("Copy fullfilm-v1/signal/" + file + " to " + path, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Texture importer unavailable: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false; importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false; importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height)); importer.isReadable = false;
        var settings = importer.GetPlatformTextureSettings("Standalone");
        settings.name = "Standalone"; settings.overridden = true; settings.format = TextureImporterFormat.RGBA32;
        settings.maxTextureSize = importer.maxTextureSize; settings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(settings); importer.SaveAndReimport();
        var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (result == null || result.width != width || result.height != height)
            throw new InvalidOperationException("Imported dimensions changed: " + path);
        return result;
    }
    static void Style(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false;
    }
    static Renderer RendererFor(Material material)
    {
        return Object.FindObjectsOfType<Renderer>(true).FirstOrDefault(r => r.gameObject.scene == SceneManager.GetActiveScene() && r.sharedMaterials.Contains(material));
    }

    public static object Build()
    {
        Guard();
        var data = JObject.Parse(File.ReadAllText(DataPath));
        var wave = JObject.Parse(File.ReadAllText(WavePath));
        if ((string)data["schema"] != "oldwebmap.fullfilm.signal.1" || (int)wave["frames"] != 7681)
            throw new InvalidOperationException("Full-film signal / PCM metadata required");
        var osc = F<OWV4Oscilloscope>(); var tracker = F<OWV5Tracker>();
        var greetz = F<OWV5Greetz>(); var atmo = F<OWV5Atmosphere>(); var director = F<OWEpisodeDirector>();
        if (osc == null || tracker == null || greetz == null || atmo == null || director == null)
            throw new InvalidOperationException("Copied V5 signal, tracker, greetz, atmosphere and director required");
        Folder(Root + "/Materials"); Folder(Root + "/Textures");
        var atlas = Import((string)wave["atlas"], (int)wave["columns"], (int)wave["atlas_height"]);
        var ga = data["greetz"]["atlas"];
        var greetzAtlas = Import((string)ga["file"], (int)ga["width"], (int)ga["height"]);

        // Only these new material assets are written. All renderer substitutions
        // affect this scene; no shared V5 material is ever sampled after this point.
        var map = new Dictionary<Material, Material>();
        var mats = new Material[4];
        for (int ch = 0; ch < 4; ch++)
        {
            mats[ch] = Clone("FullFilm phosphor CH0" + (ch + 1), osc.waveMaterials[ch], map);
            mats[ch].SetTexture("_WaveAtlas", atlas); mats[ch].SetFloat("_RowStep", 1f / atlas.height);
        }
        for (int side = 0; side < tracker.materials.Length; side++)
            tracker.materials[side] = Clone("FullFilm tracker " + side, tracker.materials[side], map);
        greetz.ringMaterial = Clone("FullFilm inherited greetz", greetz.ringMaterial, map);
        atmo.skyMaterial = Clone("FullFilm sky", atmo.skyMaterial, map);
        atmo.farStarsMaterial = Clone("FullFilm stars far", atmo.farStarsMaterial, map);
        atmo.midStarsMaterial = Clone("FullFilm stars mid", atmo.midStarsMaterial, map);
        atmo.nearStarsMaterial = Clone("FullFilm stars near", atmo.nearStarsMaterial, map);
        atmo.gridMaterial = Clone("FullFilm grid", atmo.gridMaterial, map);
        atmo.haloMaterial = Clone("FullFilm focus halo", atmo.haloMaterial, map);
        int changed = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r.gameObject.scene != SceneManager.GetActiveScene()) continue;
            Material[] shared = r.sharedMaterials; bool dirty = false;
            for (int i = 0; i < shared.Length; i++) if (shared[i] != null && map.ContainsKey(shared[i]))
            { shared[i] = map[shared[i]]; dirty = true; }
            if (dirty) { r.sharedMaterials = shared; EditorUtility.SetDirty(r); changed++; }
        }
        osc.waveMaterials = mats;
        osc.frameRate = (float)wave["fps"]; osc.frameCount = (int)wave["frames"];
        osc.atlasHeight = (int)wave["atlas_height"]; osc.duration = 256f; osc.autoSample = false;
        osc.energy0 = Floats(wave["energy"][0]); osc.energy1 = Floats(wave["energy"][1]);
        osc.energy2 = Floats(wave["energy"][2]); osc.energy3 = Floats(wave["energy"][3]);
        if (osc.music != null) { osc.music.externalVoicePositionDriver = true; Sync(osc.music); }
        tracker.duration = 256f; tracker.autoSample = false; tracker.oscilloscope = osc;
        greetz.duration = 256f; greetz.autoSample = false;
        atmo.duration = 256f; atmo.autoSample = false; atmo.outro = .01f;
        atmo.scriptTimes = data["sky"].Select(x => (float)x["t"]).ToArray();
        atmo.skyTop = data["sky"].Select(x => Col(x["top"])).ToArray();
        atmo.skyHorizon = data["sky"].Select(x => Col(x["horizon"])).ToArray();
        atmo.keyColors = data["key"].Select(x => Col(x["color"])).ToArray();
        atmo.keyIntensities = data["key"].Select(x => (float)x["intensity"]).ToArray();
        atmo.fillColors = data["fill"].Select(x => Col(x["color"])).ToArray();
        atmo.fillIntensities = data["fill"].Select(x => (float)x["intensity"]).ToArray();
        atmo.gridTimes = data["grid"].Select(x => (float)x["t"]).ToArray();
        atmo.gridValues = data["grid"].Select(x => (float)x["opacity"]).ToArray();
        atmo.journeyStart = data["journeys"].Select(x => (float)x["start"]).ToArray();
        atmo.journeyDuration = data["journeys"].Select(x => (float)x["end"] - (float)x["start"]).ToArray();
        atmo.journeyDistance = data["journeys"].Select(x => (float)x["distance"]).ToArray();

        var continuation = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == "FullFilm Continuation");
        if (continuation == null) continuation = new GameObject("FullFilm Continuation");
        if (continuation.transform.position != Vector3.zero || continuation.transform.rotation != Quaternion.identity || continuation.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException("FullFilm Continuation must have physical identity transform");
        // Main.Build can move this root to the always-active Playback hierarchy.
        // Reuse the scene component there instead of creating a second clock.
        var runtime = F<OWFullSignal>();
        var root = runtime != null ? runtime.transform : Node(continuation.transform, "FullFilm Signal");
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
        if (runtime == null) runtime = root.GetComponent<OWFullSignal>();
        if (runtime == null) runtime = root.gameObject.AddUdonSharpComponent<OWFullSignal>();
        runtime.director = director; runtime.oscilloscope = osc; runtime.tracker = tracker;
        runtime.greetz = greetz; runtime.atmosphere = atmo; runtime.worldFrame = osc.journey != null ? osc.journey.worldFrame : null;
        runtime.autoSample = false; runtime.duration = 256f;
        runtime.journeyStarts = data["journeys"].Select(x => (float)x["start"]).ToArray();
        runtime.journeyEnds = data["journeys"].Select(x => (float)x["end"]).ToArray();
        runtime.journeyDistances = data["journeys"].Select(x => (float)x["distance"]).ToArray();
        runtime.basePhosphorIntensities = mats.Select(m => m.GetFloat("_Intensity")).ToArray();
        runtime.farStarsRenderer = RendererFor(atmo.farStarsMaterial);
        runtime.midStarsRenderer = RendererFor(atmo.midStarsMaterial);
        runtime.nearStarsRenderer = RendererFor(atmo.nearStarsMaterial);
        runtime.gridRenderer = RendererFor(atmo.gridMaterial);
        runtime.continuationWaves = new Transform[4]; runtime.continuationRenderers = new Renderer[4];
        for (int ch = 0; ch < 4; ch++)
        {
            var t = Node(root, "FullFilm PCM CH0" + (ch + 1));
            var filter = t.GetComponent<MeshFilter>(); if (filter == null) filter = t.gameObject.AddComponent<MeshFilter>();
            var source = osc.regionalRenderers[ch].GetComponent<MeshFilter>();
            if (source == null || source.sharedMesh == null) throw new InvalidOperationException("V5 phosphor ribbon mesh missing");
            filter.sharedMesh = source.sharedMesh;
            var renderer = t.GetComponent<MeshRenderer>(); if (renderer == null) renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mats[ch]; Style(renderer); renderer.enabled = false;
            runtime.continuationWaves[ch] = t; runtime.continuationRenderers[ch] = renderer;
        }

        string lateMatPath = Root + "/Materials/FullFilm continuation greetz.mat";
        var lateMat = AssetDatabase.LoadAssetAtPath<Material>(lateMatPath);
        var shader = Shader.Find("OldWebMap/FullFilm/Greetz");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Import OWFullGreetz.shader first");
        if (lateMat == null) { lateMat = new Material(greetz.ringMaterial); AssetDatabase.CreateAsset(lateMat, lateMatPath); }
        lateMat.shader = shader; lateMat.CopyPropertiesFromMaterial(greetz.ringMaterial);
        lateMat.SetTexture("_MainTex", greetzAtlas);
        lateMat.SetVector("_Atlas", new Vector4((float)ga["width"], (float)ga["height"], (float)ga["row_px"], (float)ga["rows"]));
        lateMat.SetVector("_Row", new Vector4((float)ga["row_stride"], (float)ga["row_pad"], (float)ga["text_length_px"], 0));
        lateMat.SetVector("_CursorSpan", new Vector4((float)data["greetz"]["cursor_min"], (float)data["greetz"]["cursor_max"], 0, 0));
        var ring = Node(root, "FullFilm greetz continuation");
        ring.SetPositionAndRotation(greetz.ringRenderer.transform.position, greetz.ringRenderer.transform.rotation);
        ring.localScale = greetz.ringRenderer.transform.lossyScale;
        var ringFilter = ring.GetComponent<MeshFilter>(); if (ringFilter == null) ringFilter = ring.gameObject.AddComponent<MeshFilter>();
        ringFilter.sharedMesh = greetz.ringRenderer.GetComponent<MeshFilter>().sharedMesh;
        var ringRenderer = ring.GetComponent<MeshRenderer>(); if (ringRenderer == null) ringRenderer = ring.gameObject.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = lateMat; Style(ringRenderer); ringRenderer.enabled = false;
        runtime.continuationGreetzRenderer = ringRenderer; runtime.continuationGreetzMaterial = lateMat;
        runtime.overloadFirst = (float)data["greetz"]["overload_first"]; runtime.overloadLast = (float)data["greetz"]["overload_last"];
        runtime.overloadMin = (float)data["greetz"]["overload_min"]; runtime.overloadMax = (float)data["greetz"]["overload_max"];
        runtime.cursorPx = (float)data["greetz"]["cursor_px"]; runtime.cursorMin = (float)data["greetz"]["cursor_min"];
        runtime.cursorMax = (float)data["greetz"]["cursor_max"]; runtime.renewalFirst = (float)data["greetz"]["renewal_first"];
        runtime.renewalLast = (float)data["greetz"]["renewal_last"];
        runtime.renewalMin = (float)data["greetz"]["renewal_min"]; runtime.renewalMax = (float)data["greetz"]["renewal_max"];

        runtime.Sample(0f);
        foreach (var b in new UdonSharpBehaviour[] { osc, tracker, greetz, atmo, runtime }) Sync(b);
        var newMaterials = mats.Concat(tracker.materials).Concat(new[] { greetz.ringMaterial, atmo.skyMaterial,
            atmo.farStarsMaterial, atmo.midStarsMaterial, atmo.nearStarsMaterial, atmo.gridMaterial, atmo.haloMaterial, lateMat }).Distinct();
        foreach (var material in newMaterials) { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        EditorUtility.SetDirty(root.gameObject); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("signal-build.json", new { success = true, scene = ScenePath, sceneSaved = false, changedRenderers = changed,
            frames = osc.frameCount, atlas = atlas.width + "x" + atlas.height, newMaterials = newMaterials.Select(AssetDatabase.GetAssetPath).ToArray(),
            newRuntime = "OWFullSignal", autoSample = runtime.autoSample, journeys = data["journeys"], audioPlayed = false });
    }

    public static object Validate()
    {
        Guard(); var s = F<OWFullSignal>();
        if (s == null) throw new InvalidOperationException("Run OWFullSignalBuild.Build first");
        var checks = new List<object>(); bool success = true;
        void Check(string name, bool ok, object detail = null) { checks.Add(new { name, ok, detail }); if (!ok) success = false; }
        var o = s.oscilloscope; var a = s.atmosphere;
        Check("single explicit driver", !s.autoSample && !o.autoSample && !s.tracker.autoSample && !s.greetz.autoSample && !a.autoSample);
        Check("full real PCM frames", o.duration == 256f && o.frameCount == 7681 && o.atlasHeight == 8192 &&
            new[] { o.energy0, o.energy1, o.energy2, o.energy3 }.All(e => e.Length == 7681));
        var materials = o.waveMaterials.Concat(s.tracker.materials).Concat(new[] { s.greetz.ringMaterial, a.skyMaterial,
            a.farStarsMaterial, a.midStarsMaterial, a.nearStarsMaterial, a.gridMaterial, a.haloMaterial, s.continuationGreetzMaterial }).ToArray();
        Check("every animated material is an isolated new asset", materials.All(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(Root + "/Materials/")));
        foreach (var material in o.waveMaterials)
        {
            var tex = material.GetTexture("_WaveAtlas") as Texture2D;
            var imp = tex == null ? null : AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            Check("RGBA PCM " + material.name, tex != null && tex.width == 512 && tex.height == 8192 && imp != null &&
                !imp.sRGBTexture && !imp.alphaIsTransparency && !imp.mipmapEnabled && imp.filterMode == FilterMode.Point && imp.textureCompression == TextureImporterCompression.Uncompressed);
        }
        float firstHalfError = 0f;
        for (float t = 0f; t < 133.12f; t += .08f) for (int ch = 0; ch < 4; ch++)
            firstHalfError = Mathf.Max(firstHalfError, Vector3.Distance(s.SourceWorldPosition(t, ch), o.SourceWorldPosition(t, ch)));
        Check("first-half V4 physical trajectory retained", firstHalfError < .00001f, firstHalfError);
        var freezes = new List<object>();
        for (int i = 0; i < s.journeyStarts.Length; i++)
        {
            float error = 0f;
            for (int ch = 0; ch < 4; ch++)
            {
                Vector3 reference = s.SourceWorldPosition(s.journeyStarts[i], ch);
                for (int k = 1; k <= 32; k++)
                    error = Mathf.Max(error, Vector3.Distance(reference, s.SourceWorldPosition(Mathf.Lerp(s.journeyStarts[i], s.journeyEnds[i], k / 32f), ch)));
            }
            freezes.Add(new { start = s.journeyStarts[i], end = s.journeyEnds[i], maxError = error });
            Check("all four sources freeze for journey " + i, error < .00001f, error);
        }
        var eye = new Vector3(0, 1.35f, -2.8f);
        float[] distances = Enumerable.Range(0, 4).Select(ch => Vector3.Distance(s.SourceWorldPosition(148.48f, ch), eye)).ToArray();
        Check("second peak selects only CH3/CH4", distances[0] > 7f && distances[1] > 7f && distances[2] > 1.5f && distances[2] < 3f && distances[3] > 1.5f && distances[3] < 3f, distances);
        Check("whole field retreats after stable void entry", Enumerable.Range(0, 4).All(ch => Vector3.Distance(s.SourceWorldPosition(190f, ch), eye) > 18f));
        Check("extended atmosphere arrays share an axis", a.scriptTimes.Length == 17 && a.scriptTimes.Length == a.skyTop.Length && a.scriptTimes.Length == a.keyColors.Length && a.scriptTimes.Length == a.fillIntensities.Length);
        s.Sample(190f);
        Check("void removes stars and grid geometry", !s.farStarsRenderer.enabled && !s.midStarsRenderer.enabled && !s.nearStarsRenderer.enabled && !s.gridRenderer.enabled && s.sampledStarPresence < .025f);
        Check("void greetz extinguished", s.sampledGreetzOpacity == 0f && !s.continuationGreetzRenderer.enabled);
        s.Sample(184.32f); float stop = s.sampledGreetzScroll; s.Sample(186f);
        Check("greetz scroll really stops", Mathf.Abs(stop - s.sampledGreetzScroll) < .00001f);
        s.Sample(200f); Check("renewal begins with isolated cursor", s.sampledGreetzOpacity > .1f && s.continuationGreetzMaterial.GetFloat("_CursorOnly") == 1f);
        s.Sample(252f);
        Check("sustained final CH1/CH2/CH4 have PCM energy", s.tracker.sampledLevels.x > .01f && s.tracker.sampledLevels.y > .01f && s.tracker.sampledLevels.w > .01f,
            s.tracker.sampledLevels.ToString());
        float pure = s.sampledGreetzScroll; s.Sample(150f); s.Sample(252f);
        Check("reverse seek is deterministic", Mathf.Abs(pure - s.sampledGreetzScroll) < .00001f);
        Check("full-film shader has no compiler errors", !ShaderUtil.GetShaderMessages(s.continuationGreetzMaterial.shader).Any(m => m.severity.ToString() == "Error"));
        Check("physical waveform anchors agree", s.maxVoiceAnchorError < .0001f, s.maxVoiceAnchorError);
        s.Sample(0f);
        string report = Report("signal-validate.json", new { success, provenance = "Unity Pipeline editor sampling; no playback or listening", checks, journeys = freezes });
        if (!success) throw new InvalidOperationException(report);
        return report;
    }
}
