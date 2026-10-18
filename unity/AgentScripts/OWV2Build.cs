using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using TMPro;
using OldWebMap;
using Object = UnityEngine.Object;

public static class OWV2Build
{
    public const string ScenePath = "Assets/Scenes/OWM_FullFilm_v2.unity";
    public const string Root = "Assets/OldWebMap/FullFilm/Revision2";
    const string Source = "Assets/Scenes/OWM_FullFilm_v1.unity";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v2");
    static T F<T>() where T : Component => Object.FindObjectsOfType<T>(true).FirstOrDefault(b => b.gameObject.scene == SceneManager.GetActiveScene());
    static string Hash(string path) { using (var h = System.Security.Cryptography.SHA256.Create()) using (var s = File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
    static string Report(string name, object value) { Directory.CreateDirectory(Tmp + "/reports"); string json = JsonConvert.SerializeObject(value, Formatting.Indented); File.WriteAllText(Tmp + "/reports/" + name + ".json", json); return json; }
    static void Guard() { if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != ScenePath) throw new Exception("Stopped, compiled V2 scene required"); }
    static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; int cut = path.LastIndexOf('/'); Folder(path.Substring(0, cut)); AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1)); }
    static void Sync(UdonSharpBehaviour b) { UdonSharpEditorUtility.CopyProxyToUdon(b); var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(b); if (vm != null) EditorUtility.SetDirty(vm); EditorUtility.SetDirty(b); }
    static void CopyFields(object from, object to)
    {
        foreach (var field in from.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var target = to.GetType().GetField(field.Name); if (target == null || !target.FieldType.IsAssignableFrom(field.FieldType)) continue;
            target.SetValue(to, field.GetValue(from));
        }
    }
    static Transform Node(string name, Transform parent)
    {
        var old = parent == null ? SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name)?.transform : parent.Find(name);
        if (old != null) return old; var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    static void Style(Renderer r) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; }
    static Material Material(string name, string shader)
    {
        string path = Root + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }
    static Transform Quad(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material; Style(go.GetComponent<Renderer>()); return go.transform;
    }
    static TMP_Text Text(string name, Transform parent, string text, Vector3 position, Vector2 size, float fontSize, TMP_FontAsset font)
    {
        var go = new GameObject(name); var t = go.AddComponent<TextMeshPro>(); t.font = font; t.fontSharedMaterial = font.material; t.text = text; t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.color = Color.white; go.transform.SetParent(parent, false); go.transform.localPosition = position; t.rectTransform.sizeDelta = size; return t;
    }
    public static object Folders()
    {
        foreach (string directory in new[] { Root, Root + "/Materials", Root + "/Meshes", Root + "/Models", Root + "/Textures", Root + "/Programs" }) Folder(directory);
        return new { success = true, root = Root };
    }
    public static object Programs()
    {
        Folders(); var existing = AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).Where(p => p != null).ToArray();
        var made = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("OWV2 t:MonoScript", new[] { "Assets/OldWebMap/Scripts" }))
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid)); if (script == null || existing.Any(p => p.sourceCsScript == script)) continue;
            var asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); asset.sourceCsScript = script; AssetDatabase.CreateAsset(asset, Root + "/Programs/" + script.name + ".asset"); made.Add(script.name);
        }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(); if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile errors"); AssetDatabase.SaveAssets();
        return new { success = true, made };
    }
    public static object Create()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty) throw new Exception("Clean, stopped current scene required; user edits must be preserved");
        Directory.CreateDirectory(Tmp + "/baseline"); string sourceHash = Hash(Source); File.Copy(Source, Tmp + "/baseline/v1-" + sourceHash + ".unity", true);
        if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(Source, ScenePath)) throw new Exception("V2 scene copy failed");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var old = F<OWFullPresentation>(); var oldSignal = F<OWFullSignal>(); var root = Node("V2 Performance", null);
        var run = root.GetComponent<OWV2Presentation>(); if (run == null) run = root.gameObject.AddUdonSharpComponent<OWV2Presentation>();
        var basis = root.GetComponent<OWV2BasePresentation>(); if (basis == null) basis = root.gameObject.AddUdonSharpComponent<OWV2BasePresentation>();
        var signalRoot = Node("V2 Signal", root); var signal = signalRoot.GetComponent<OWV2Signal>(); if (signal == null) signal = signalRoot.gameObject.AddUdonSharpComponent<OWV2Signal>();
        CopyFields(old, basis); CopyFields(oldSignal, signal); basis.signal = signal; basis.drivenExternally = true;
        run.director = old.director; run.basis = basis;
        old.enabled = false; old.offlinePreview = true; var oldVm = UdonSharpEditorUtility.GetBackingUdonBehaviour(old); if (oldVm != null) oldVm.enabled = false;
        oldSignal.enabled = false; var signalVm = UdonSharpEditorUtility.GetBackingUdonBehaviour(oldSignal); if (signalVm != null) signalVm.enabled = false;
        run.frontActors = old.legacy.cast.actors.Concat(old.legacy.cast.alternateActors).Where(a => a != null).Select(a => a.gameObject).Distinct().ToArray();
        run.laterActors = old.overload.actors.Concat(old.renewal.actors).Where(a => a != null).Select(a => a.gameObject).Distinct().ToArray();
        IsolateMaterials(); SetupCylinder(run); SetupModels(run); SetupAudioFloor(run); SetupNative(run);
        run.Waiting(); run.offlinePreview = false;
        foreach (var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b => b.gameObject.scene == SceneManager.GetActiveScene() && b.GetType().Name.StartsWith("OW"))) Sync(b);
        if (oldVm != null) oldVm.enabled = false; if (signalVm != null) signalVm.enabled = false;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("integration", new { success = true, scene = ScenePath, source = Source, sourceHash, frontActors = run.frontActors.Length, laterActors = run.laterActors.Length, materials = AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }).Length });
    }
    static void IsolateMaterials()
    {
        var map = new Dictionary<Material, Material>(); var behaviours = Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b => b.gameObject.scene == SceneManager.GetActiveScene() && b.GetType().Name.StartsWith("OW")).ToArray();
        foreach (var b in behaviours) foreach (var field in b.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var list = field.FieldType == typeof(Material) ? new[] { (Material)field.GetValue(b) } : field.FieldType == typeof(Material[]) ? (Material[])field.GetValue(b) : null;
            if (list == null) continue;
            foreach (var m in list.Where(m => m != null))
            {
                if (AssetDatabase.GetAssetPath(m).StartsWith(Root + "/") || map.ContainsKey(m)) continue;
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)); if (string.IsNullOrEmpty(guid)) throw new Exception("Nonpersistent material " + m.name);
                string path = Root + "/Materials/Inherited-" + guid + ".mat"; var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy == null) { copy = new Material(m) { name = "V2 " + m.name }; AssetDatabase.CreateAsset(copy, path); } map[m] = copy;
            }
        }
        foreach (var b in behaviours) foreach (var field in b.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.FieldType == typeof(Material)) { var m = (Material)field.GetValue(b); if (m != null && map.TryGetValue(m, out var copy)) field.SetValue(b, copy); }
            else if (field.FieldType == typeof(Material[])) { var arr = (Material[])field.GetValue(b); if (arr == null) continue; field.SetValue(b, arr.Select(m => m != null && map.TryGetValue(m, out var copy) ? copy : m).ToArray()); }
        }
        foreach (var renderer in Object.FindObjectsOfType<Renderer>(true).Where(r => r.gameObject.scene == SceneManager.GetActiveScene())) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && map.TryGetValue(m, out var copy) ? copy : m).ToArray();
        foreach (var text in Object.FindObjectsOfType<TMP_Text>(true).Where(t => t.gameObject.scene == SceneManager.GetActiveScene())) if (text.fontSharedMaterial != null && map.TryGetValue(text.fontSharedMaterial, out var material)) text.fontSharedMaterial = material;
        if (RenderSettings.skybox != null && map.TryGetValue(RenderSettings.skybox, out var sky)) RenderSettings.skybox = sky;
    }
    static void SetupCylinder(OWV2Presentation run)
    {
        var root = Node("True cylinder chrome lettering", run.transform); root.localPosition = new Vector3(0f, 4.65f, 10.2f); run.cylinder = root;
        var legacy = run.basis.legacy.montage; run.cylinderBands = new Transform[3]; float[] radii = { 2.6f, 2.3f, 1.8f };
        for (int i = 0; i < 3; i++)
        {
            var original = legacy.typeWords[i].GetComponent<MeshFilter>().sharedMesh; string path = Root + "/Meshes/Centered cylinder band " + i + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = Object.Instantiate(original); mesh.name = "Centered cylindrical original EDGE contour " + i; var vertices = mesh.vertices; for (int k = 0; k < vertices.Length; k++) vertices[k].z -= radii[i]; mesh.vertices = vertices; mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); }
            var band = Node("Cylinder band " + i, root); var filter = band.GetComponent<MeshFilter>(); if (filter == null) filter = band.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = band.GetComponent<MeshRenderer>(); if (renderer == null) renderer = band.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = legacy.typeMaterial; Style(renderer);
            band.localPosition = new Vector3(0f, i == 0 ? .22f : i == 1 ? -.45f : -.95f, 0f); run.cylinderBands[i] = band;
            int repeats = i < 2 ? 4 : 2;
            for (int copy = 1; copy < repeats; copy++)
            {
                var sector = Node("Continuous cylindrical sector " + copy, band); sector.localPosition = Vector3.zero; sector.localRotation = Quaternion.Euler(0f, 360f * copy / repeats, 0f); sector.localScale = Vector3.one;
                var f = sector.GetComponent<MeshFilter>(); if (f == null) f = sector.gameObject.AddComponent<MeshFilter>(); f.sharedMesh = mesh;
                var r = sector.GetComponent<MeshRenderer>(); if (r == null) r = sector.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = legacy.typeMaterial; Style(r);
            }
        }
    }
    public static object RefreshCylinder()
    {
        Guard(); var run = F<OWV2Presentation>(); SetupCylinder(run); Sync(run); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); return new { success = true, sectors = new[] { 4, 4, 2 }, axis = "True cylindrical centre, vertical yaw" };
    }
    static Transform Model(string file, string instance, Transform parent)
    {
        var old = parent.Find(instance); if (old != null) return old;
        string path = Root + "/Models/" + file + ".fbx"; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab == null) throw new Exception("Import model first: " + path);
        var pivot = Node(instance, parent);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.name = "Imported source with intact units and axes"; go.transform.SetParent(pivot, false);
        return pivot;
    }
    public static object RefreshModels()
    {
        Guard(); var run = F<OWV2Presentation>();
        if (run.editorStage != null) Object.DestroyImmediate(run.editorStage);
        SetupModels(run); run.Waiting(); run.offlinePreview = false; Sync(run); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return new { success = true, operation = "Preserve original imported FBX root conversion under independent animation pivots" };
    }
    static void SetupModels(OWV2Presentation run)
    {
        var stage = Node("V2 creation workspace", run.transform); run.editorStage = stage.gameObject;
        run.workspace = Model("fullfilm_v2_workbench", "Real production desk", stage);
        run.miniature = Model("fullfilm_v2_scene_block", "Same world miniature", stage); run.miniature.localPosition = new Vector3(0f, 1.8f, 6.1f);
        run.finalWorld = Model("fullfilm_v2_scene_block", "Same world at world scale", stage);
        var replacement = new Dictionary<Material, Material>();
        foreach (var r in stage.GetComponentsInChildren<Renderer>(true))
        {
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i]; if (m == null) continue;
                if (!replacement.TryGetValue(m, out var material))
                {
                    string safe = m.name.Replace('/', '_'); material = Material("Model-" + safe, "Standard");
                    if (m.HasProperty("_Color")) material.SetColor("_Color", m.color); material.SetFloat("_Glossiness", .22f); material.SetFloat("_Metallic", safe.Contains("Metal") ? .55f : 0f); replacement[m] = material;
                }
                materials[i] = material;
            }
            r.sharedMaterials = materials;
        }
        var font = run.basis.keyText.font;
        var status = Text("Active production file", stage, "", new Vector3(0f, .95f, 3.8f), new Vector2(4.8f, .3f), 1.25f, font); run.sourceStatus = status; run.sourceStatusRoot = status.gameObject;
        var word = Text("Continue", stage, "继续", new Vector3(0f, 1.1f, 3f), new Vector2(3f, 1f), 6.5f, font); run.finalWord = word; run.finalWordRoot = word.gameObject;
        run.screenshotMaterials = stage.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "UI_Main_16x9").Select(r => r.sharedMaterial).ToArray();
    }
    static void SetupAudioFloor(OWV2Presentation run)
    {
        var node = Node("Fused audibility floor", run.transform); var floor = node.GetComponent<OWV2AudioFloor>(); if (floor == null) floor = node.gameObject.AddUdonSharpComponent<OWV2AudioFloor>();
        floor.music = run.director.music; floor.retreatFloor = .30f; floor.drySources = new AudioSource[4];
        var spatialType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VRC.SDK3.Components.VRCSpatialAudioSource") ?? a.GetType("VRC.SDKBase.VRC_SpatialAudioSource")).First(t => t != null);
        for (int ch = 0; ch < 4; ch++)
        {
            var child = Node("Unchanged PCM contribution CH" + ch, node); var source = child.GetComponent<AudioSource>(); if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.clip = floor.music.sources[ch].clip; source.playOnAwake = false; source.loop = false; source.volume = 0f; source.spatialBlend = 0f; source.spatialize = false; source.dopplerLevel = 0f; source.panStereo = (ch - 1.5f) * .07f;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 30f; source.maxDistance = 150f;
            var spatial = child.GetComponent(spatialType); if (spatial == null) spatial = child.gameObject.AddComponent(spatialType);
            var so = new SerializedObject(spatial);
            foreach (var field in new[] { "EnableSpatialization", "enableSpatialization" }) { var p = so.FindProperty(field); if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = false; }
            foreach (var field in new[] { "Gain", "gain" }) { var p = so.FindProperty(field); if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = 0f; }
            so.ApplyModifiedPropertiesWithoutUndo(); floor.drySources[ch] = source;
        }
        run.audioFloor = floor;
    }
    static Texture2D Texture(string name)
    {
        string path = Root + "/Textures/" + name; var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new Exception("Texture not imported: " + path);
        importer.textureType = TextureImporterType.Default; importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
        importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void SetupNative(OWV2Presentation run)
    {
        string[] groups = { "readme", "manga", "torrent", "forum", "subtitles", "access" };
        run.nativeStarts = new[] { 10.24f, 30.72f, 40.96f, 51.2f, 61.44f, 104.96f }; run.nativeEnds = new[] { 20.48f, 40.96f, 51.2f, 61.44f, 71.68f, 122.88f };
        run.nativeOffsets = new[] { 0, 4, 8, 12, 16, 20 }; run.nativeStates = new Texture2D[24]; run.nativeCompanions = new Texture2D[24];
        for (int group = 0; group < 6; group++) for (int state = 0; state < 4; state++)
        {
            int at = group * 4 + state;
            run.nativeStates[at] = Texture("native-" + groups[group] + "-" + state.ToString("00") + "-full.png");
            run.nativeCompanions[at] = Texture("native-" + groups[group] + "-" + state.ToString("00") + "-secondary.png");
        }
        var root = Node("Two related contents in one focus", run.basis.legacy.journey.worldFrame); run.nativeRoot = root.gameObject;
        run.nativeMainMaterial = Material("Native main state", "OldWebMap/V2/Source Surface"); run.nativeMainMaterial.shader = Shader.Find("OldWebMap/V2/Source Surface"); run.nativeSecondaryMaterial = Material("Native subordinate detail", "Unlit/Transparent");
        run.nativeMain = Quad("Recognizable application and content", root, new Vector3(-.65f, 2.25f, 5.45f), new Vector3(4.45f, 2.9f, 1f), run.nativeMainMaterial).GetComponent<Renderer>();
        run.nativeSecondary = Quad("Associated detail and changing state", root, new Vector3(2.2f, 2.7f, 5.9f), new Vector3(2.3f, 1.55f, 1f), run.nativeSecondaryMaterial).GetComponent<Renderer>();
        var text = Text("Native document status", root, "", new Vector3(-.65f, .68f, 5.42f), new Vector2(4.45f, .2f), 1.15f, run.basis.keyText.font); run.nativeStatus = text;
        // This is a document footer, not a floating statement explaining the film.
        text.color = new Color(.65f, .78f, .88f, 1f);
    }
    public static object Save()
    {
        Guard(); var run = F<OWV2Presentation>(); run.Waiting(); run.offlinePreview = false;
        foreach (var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b => b.gameObject.scene == SceneManager.GetActiveScene() && b.GetType().Name.StartsWith("OW"))) Sync(b);
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t.gameObject.scene == SceneManager.GetActiveScene() && PrefabUtility.IsPartOfPrefabInstance(t)))
        { PrefabUtility.RecordPrefabInstancePropertyModifications(t); PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject); foreach (var component in t.GetComponents<Component>()) if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
        var old = F<OWFullPresentation>(); if (old != null) UdonSharpEditorUtility.GetBackingUdonBehaviour(old).enabled = false;
        var signal = F<OWFullSignal>(); if (signal != null) UdonSharpEditorUtility.GetBackingUdonBehaviour(signal).enabled = false;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        return Report("saved", new { success = true, scene = ScenePath, sha256 = Hash(ScenePath), v1Sha256 = Hash(Source), playing = EditorApplication.isPlaying });
    }
}
