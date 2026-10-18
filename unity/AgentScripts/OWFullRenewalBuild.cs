using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using OldWebMap;
using UdonSharp;
using UdonSharpEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

/// <summary>Pipeline-only, persisted late-film authoring. Never starts playback or changes audio.</summary>
public static class OWFullRenewalBuild
{
    const string Root = "Assets/OldWebMap/FullFilm/Revision1/Renewal";
    const string ScenePath = "Assets/Scenes/OWM_FullFilm_v1.unity";
    const string ParentName = "FullFilm Continuation";
    const string ModuleName = "FullFilm Renewal";
    const string DataPath = "AgentScripts/data/fullfilm-renewal.json";
    static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    static readonly List<object> ReusedAssets = new List<object>();
    static readonly List<Transform> LayerCards = new List<Transform>();
    static readonly List<Vector3> LayerOrigins = new List<Vector3>();
    static readonly List<Transform> LocalCursors = new List<Transform>();
    static readonly List<Transform> RemoteCursors = new List<Transform>();
    static readonly List<GameObject> Published = new List<GameObject>();
    static TMP_FontAsset Font;
    static Material Wood, Metal, Rim, Paper, Mint, Gold, Blue, Dark;
    static OWV4HubCast SourceCast;
    static OWFullRenewal Run;
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-renewal");
    static T Find<T>() where T : Object => Object.FindObjectsOfType<T>(true).FirstOrDefault();
    static GameObject PhysicalParent() => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go => go.name == ParentName);
    static Color C(string hex) { Color color; ColorUtility.TryParseHtmlString(hex, out color); return color; }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int at = path.LastIndexOf('/');
        if (at < 0) throw new InvalidOperationException("Missing asset folder " + path);
        Folder(path.Substring(0, at)); AssetDatabase.CreateFolder(path.Substring(0, at), path.Substring(at + 1));
    }
    static Transform Node(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform;
    }
    static string Report(string name, object result)
    {
        Directory.CreateDirectory(Tmp + "/reports"); string json = JsonConvert.SerializeObject(result, Formatting.Indented);
        File.WriteAllText(Tmp + "/reports/" + name, json); return json;
    }
    static void Guard()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Stopped full-film scene required: " + ScenePath);
        if (PhysicalParent() == null) throw new InvalidOperationException("Create the stable identity parent first: " + ParentName);
    }
    static void Sync(UdonSharpBehaviour behaviour)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour); if (vm != null) EditorUtility.SetDirty(vm);
        EditorUtility.SetDirty(behaviour);
    }
    static Material Mat(string name, string hex, float emission = 0f, bool unlit = false)
    {
        if (Mats.TryGetValue(name, out var found)) return found;
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
        if (shader == null) throw new InvalidOperationException("Shader missing");
        if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.color = C(hex); material.enableInstancing = true;
        if (!unlit)
        {
            material.SetFloat("_Glossiness", .24f); material.SetFloat("_Metallic", name.Contains("Metal") ? .65f : .08f);
            if (emission > 0f) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", C(hex) * emission); }
            else material.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(material); Mats[name] = material; return material;
    }
    static Material ImageMaterial(string file, bool alpha = false)
    {
        string path = Root + "/Materials/Image_" + file + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + file + ".png");
        if (texture == null) throw new InvalidOperationException("Import the renewal texture first: " + file);
        Shader shader = Shader.Find(alpha ? "Unlit/Transparent" : "Unlit/Texture");
        if (material == null) { material = new Material(shader) { name = "Image_" + file }; AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.mainTexture = texture; material.enableInstancing = true;
        EditorUtility.SetDirty(material); return material;
    }
    static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
        if (type == PrimitiveType.Quad) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
        Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
    }
    static Transform Box(string name, Transform parent, Vector3 p, Vector3 s, Material material) => Primitive(name, PrimitiveType.Cube, parent, p, s, material);
    static TMP_Text Label(string name, string value, Transform parent, Vector3 position, float width, float height, string color = "#d5e8e6")
    {
        // TextMeshPro.font calls LoadFontAsset immediately. Its renderer is cached
        // by Awake, which has not run for a freshly created component under an
        // inactive presentation parent. Initialize this one new label as an active
        // scene root, then attach it; never activate the user's presentation root.
        var go = new GameObject(name);
        var label = go.AddComponent<TextMeshPro>();
        label.font = Font; label.fontSharedMaterial = Font.material; label.text = value; label.richText = false;
        label.fontSize = height * 10f; label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = false;
        label.color = C(color); label.rectTransform.sizeDelta = new Vector2(width, height * 3f);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; return label;
    }
    static Mesh MeshAsset(string name, Vector3[] vertices, int[] triangles)
    {
        string path = Root + "/Meshes/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); } else mesh.Clear();
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
    }
    static Transform MeshNode(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = Node(name, parent, Vector3.zero).gameObject; go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material; return go.transform;
    }
    static Transform Star(string id, Transform parent, Vector3 position, float size, Material material)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        v.Add(new Vector3(0, 0, -.06f)); v.Add(new Vector3(0, 0, .06f));
        for (int i = 0; i < 10; i++) { float a = i * Mathf.PI / 5f + Mathf.PI / 2f, r = i % 2 == 0 ? 1f : .43f; v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f)); }
        for (int i = 0; i < 10; i++) { int a = 2 + i, b = 2 + (i + 1) % 10; t.AddRange(new[] { 0, b, a, 1, a, b }); }
        var star = MeshNode(id, parent, MeshAsset("OriginalStar", v.ToArray(), t.ToArray()), material);
        star.localPosition = position; star.localScale = Vector3.one * size; return star;
    }
    static Transform Cursor(string name, Transform parent, Material material, bool remote)
    {
        // Mesh lies on its screen; it is not a camera overlay or a floating system cursor.
        var cursor = Node(name, parent, Vector3.zero);
        var mesh = MeshAsset("RenewalArrow", new[] { new Vector3(0, 0, 0), new Vector3(0, -.22f, 0), new Vector3(.06f, -.16f, 0), new Vector3(.12f, -.26f, 0), new Vector3(.16f, -.24f, 0), new Vector3(.10f, -.14f, 0), new Vector3(.19f, -.14f, 0) }, new[] { 0, 2, 1, 0, 6, 2, 2, 4, 3, 2, 5, 4, 2, 6, 5 });
        MeshNode("Raised pointer", cursor, mesh, material).localScale = Vector3.one * .7f;
        if (remote) Label("Anonymous remote cursor", "xxxx", cursor, new Vector3(.17f, -.20f, -.005f), .5f, .065f, "#78dfc4");
        return cursor;
    }
    static Renderer Screen(string name, Transform parent, Vector3 position, float width, float height, Material image, bool cursors = false, float yaw = 0f)
    {
        var root = Node(name, parent, position); root.localRotation = Quaternion.Euler(0, yaw, 0);
        Box("Extruded dark display body", root, new Vector3(0, 0, .052f), new Vector3(width + .10f, height + .10f, .12f), Dark);
        Box("Top copper lip", root, new Vector3(0, height * .5f + .035f, -.022f), new Vector3(width + .11f, .035f, .07f), Rim);
        Box("Bottom copper lip", root, new Vector3(0, -height * .5f - .035f, -.022f), new Vector3(width + .11f, .035f, .07f), Metal);
        foreach (float side in new[] { -1f, 1f }) Box("Side display extrusion", root, new Vector3(side * (width * .5f + .035f), 0, -.005f), new Vector3(.035f, height + .08f, .09f), Metal);
        var face = Primitive("Working image", PrimitiveType.Quad, root, new Vector3(0, 0, -.014f), new Vector3(width, height, 1f), image).GetComponent<Renderer>();
        foreach (float x in new[] { -.32f, -.24f, -.16f }) Box("Visible bezel vent", root, new Vector3(x * width, -height * .5f - .027f, -.064f), new Vector3(.038f, .008f, .008f), Gold);
        if (cursors) { LocalCursors.Add(Cursor("One local cursor", root, Paper, false)); RemoteCursors.Add(Cursor("One responding cursor", root, Mint, true)); }
        return face;
    }
    static bool FontPartsValid(TMP_FontAsset font)
    {
        return font != null && font.material != null && font.material.shader != null && font.atlasTextures != null && font.atlasTextures.Length == 1 && font.atlasTextures[0] != null;
    }
    static void PersistFontChildren(TMP_FontAsset font)
    {
        if (!FontPartsValid(font)) throw new InvalidOperationException("Renewal TMP font has no complete material/atlas pair");
        if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures)
        {
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(atlas);
        }
        font.material.mainTexture = font.atlasTextures[0];
        EditorUtility.SetDirty(font.material); EditorUtility.SetDirty(font);
    }
    static TMP_FontAsset FreshFont()
    {
        var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/OldWebMap/Fonts/FontSources/NotoSansSC-Regular.ttf");
        if (source == null) throw new InvalidOperationException("Existing NotoSansSC source missing");
        var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        if (!FontPartsValid(font)) throw new InvalidOperationException("Fresh Noto TMP font failed to initialize material/atlas");
        font.name = "RenewalLabelsPersisted"; font.isMultiAtlasTexturesEnabled = false;
        // Do not delete a font still referenced by a saved scene, and never touch
        // the inherited V5 font. A broken owned resource gets a new valid path.
        string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Fonts/RenewalLabelsPersisted.asset");
        AssetDatabase.CreateAsset(font, path); PersistFontChildren(font); return font;
    }
    static void FontAsset()
    {
        string path = Root + "/Fonts/RenewalLabels.asset"; Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (!FontPartsValid(Font)) Font = FreshFont();
        PersistFontChildren(Font); Font.ReadFontAssetDefinition();
        string chars = " xxxxSAVED✓_0123456789v.，。！？：·一盏灯同份保存资料刚才的外面还在这里试试看草图模型世界工具翻译字幕校对时间轴原作修改新贡献者一起需要点帮助吗星桥把方法留下来";
        string required = new string(chars.Distinct().Where(c => !Font.HasCharacter(c)).ToArray());
        if (required.Length > 0 && !Font.TryAddCharacters(required, out string missing))
        {
            Font = FreshFont();
            if (!Font.TryAddCharacters(new string(chars.Distinct().ToArray()), out missing)) throw new InvalidOperationException("Renewal font glyphs missing: " + missing);
        }
        PersistFontChildren(Font); Font.ReadFontAssetDefinition(); AssetDatabase.SaveAssets();
        if (!AssetDatabase.Contains(Font.material) || Font.atlasTextures.Any(atlas => !AssetDatabase.Contains(atlas)))
            throw new InvalidOperationException("Renewal material/atlas children were not persisted");
        // Exercise the exact font setter before replacing any existing module.
        // The probe is an active temporary root for the same TMP Awake reason as Label.
        GameObject probe = null;
        try
        {
            probe = new GameObject("Renewal TMP initialization probe"); var text = probe.AddComponent<TextMeshPro>();
            text.font = Font; text.fontSharedMaterial = Font.material; text.text = "xxxx SAVED ✓ 刚才的外面，还在这里"; text.ForceMeshUpdate(true, true);
            if (probe.GetComponent<MeshRenderer>() == null || text.fontSharedMaterial == null) throw new InvalidOperationException("Renewal TMP initialization probe is incomplete");
        }
        finally { if (probe != null) Object.DestroyImmediate(probe); }
    }

    public static object Import()
    {
        Guard(); Folder(Root + "/Textures"); var rows = new List<object>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); var importer = AssetImporter.GetAtPath(path) as TextureImporter; if (importer == null) continue;
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = Path.GetFileName(path).StartsWith("layer-"); importer.mipmapEnabled = true; importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.isReadable = false; importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path); rows.Add(new { path, tex.width, tex.height, alpha = importer.alphaIsTransparency });
        }
        if (rows.Count != 17) throw new InvalidOperationException("Expected all 17 renewal images; found " + rows.Count);
        return Report("import.json", new { imported = rows.Count, rows });
    }

    static Transform ClonePart(Transform source, Transform parent, string name, Vector3 position, float width = 0f, float yaw = 0f)
    {
        if (source == null) return null;
        var go = Object.Instantiate(source.gameObject, parent); go.name = name; go.SetActive(true);
        go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        if (width > 0f)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true); if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
                float factor = width / Mathf.Max(.01f, b.size.x); go.transform.localScale *= factor;
            }
        }
        ReusedAssets.Add(new { name, source = source.name, kind = "existing persisted scene furniture", meshes = go.GetComponentsInChildren<MeshFilter>(true).Select(f => AssetDatabase.GetAssetPath(f.sharedMesh)).Distinct().ToArray() });
        return go.transform;
    }
    static Transform ExistingFurniture(string name)
    {
        if (SourceCast.maintenanceRoot == null) return null;
        return SourceCast.maintenanceRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }
    static void Desk(Transform parent, Vector3 position, float width)
    {
        var desk = ClonePart(ExistingFurniture("Imported maintenance table"), parent, "Existing wooden work table", position, width);
        if (desk != null)
        {
            var renderers = desk.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                float top = bounds.max.y - desk.position.y;
                if (top > .02f) { Vector3 fitted = desk.localScale; fitted.y *= .76f / top; desk.localScale = fitted; }
            }
        }
        if (desk == null)
        {
            // The integrated scene normally supplies the imported table. This fallback
            // is a restrained stand, not a replacement imported furniture package.
            Box("Small work surface", parent, position + new Vector3(0, .74f, 0), new Vector3(width, .08f, .75f), Wood);
            foreach (float x in new[] { -width * .40f, width * .40f }) foreach (float z in new[] { -.25f, .25f })
                Box("Stand leg", parent, position + new Vector3(x, .35f, z), new Vector3(.055f, .70f, .055f), Metal);
        }
        ClonePart(ExistingFurniture("Imported maintenance task lamp"), parent, "Existing task lamp", position + new Vector3(width * .35f, .78f, .1f), .34f);
        ClonePart(ExistingFurniture("Imported maintenance reference books"), parent, "Existing reference volumes", position + new Vector3(-width * .34f, .78f, .15f), .34f);
    }
    static Transform Pad(Transform parent, string name, Vector3 position, float radius, Material material)
    {
        var pad = Primitive(name, PrimitiveType.Cylinder, parent, position + new Vector3(0, .006f, 0), new Vector3(radius, .006f, radius * .72f), material);
        return pad;
    }
    static Transform Book(Transform parent, string name, Vector3 position, float scale = 1f)
    {
        if (SourceCast.archivedCopy == null) throw new InvalidOperationException("The real V4 archivedCopy book is required for continuity");
        var book = ClonePart(SourceCast.archivedCopy.transform, parent, name, position);
        book.localScale *= scale; book.localRotation = Quaternion.identity;
        // This is the source geometry and source acknowledgement. Do not substitute
        // an unrelated disk or generic luminous file icon.
        return book;
    }
    static void Archive(Transform parent)
    {
        var archive = Node("Far warm lamp and the same saved volume", parent, new Vector3(0, 0, 17.5f));
        Run.distantArchive = archive.gameObject;
        Desk(archive, Vector3.zero, 2.0f);
        ClonePart(ExistingFurniture("Imported maintained archive shelf"), archive, "Existing archive shelf", new Vector3(.72f, 0, 1.05f), 1.7f);
        Book(archive, "128-second archivedCopy, distant matched location", new Vector3(-.35f, .89f, -.24f), 1.7f);
        var light = Node("One far warm task light", archive, new Vector3(.22f, 2.2f, -.45f)).gameObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = C("#ffd29a"); light.intensity = 1.6f; light.range = 4.8f;
        light.shadows = LightShadows.None; Run.archiveLight = light;
        Pad(archive, "Small island of warm light", Vector3.zero, 3.1f, Mat("Far warm ground", "#181916", .12f));
        Label("A familiar acknowledgement", "SAVED ✓", archive, new Vector3(0, 1.6f, -.15f), 1.8f, .13f, "#e8cb91");
        var reply = Node("First small response at hand", parent, new Vector3(.75f, 1.12f, .6f));
        Box("Response box depth", reply, Vector3.zero, new Vector3(1.40f, .23f, .07f), Dark);
        Box("Response thin edge", reply, new Vector3(0, -.125f, -.012f), new Vector3(1.44f, .018f, .07f), Mint);
        Label("Ready cursor", "_", reply, new Vector3(-.49f, .015f, -.045f), .24f, .14f, "#a4e6d4"); Run.replyWindow = reply.gameObject;
    }
    static void LayerPreview(Transform parent, Vector3 position, float scale)
    {
        var root = Node("The same layers, genuinely separated in depth", parent, position); root.localScale = Vector3.one * scale;
        Box("Layer preview dark pedestal", root, new Vector3(0, -.49f, .16f), new Vector3(1.42f, .06f, .95f), Metal);
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = new Vector3(i * .10f, i * -.015f, .48f - i * .24f);
            var card = Primitive("Independent transparent picture layer " + i, PrimitiveType.Quad, root, p, new Vector3(1.28f, .85f, 1f), ImageMaterial("layer-" + i, true));
            LayerCards.Add(card); LayerOrigins.Add(p);
        }
    }
    static Renderer Station(Transform parent, string title, Vector3 position, float yaw, float width, Material image, bool cursors)
    {
        var station = Node(title, parent, position); station.localRotation = Quaternion.Euler(0, yaw, 0);
        Pad(station, "Open work island", Vector3.zero, width + 1.0f, Mat("Work island floor", "#182a34", .03f));
        Desk(station, new Vector3(0, 0, .17f), width * .90f);
        float screenCenter = Mathf.Max(1.76f, .82f + width * .625f * .5f);
        var renderer = Screen("Raised work display", station, new Vector3(0, screenCenter, 0f), width, width * .625f, image, cursors);
        Box("Display support", station, new Vector3(0, .91f, .15f), new Vector3(.09f, .26f, .11f), Metal);
        return renderer;
    }
    static void Workspaces(Transform frame)
    {
        var before = Node("Before the window - three parallel jobs", frame, Vector3.zero); Run.preWindowSpace = before;
        var central = Node("Tool work begins", before, Vector3.zero);
        var translation = Node("Translation work begins", before, Vector3.zero);
        var original = Node("Original world work begins", before, Vector3.zero);
        Run.stationRevealRoots = new[] { central, translation, original };
        var tool = Station(central, "An approachable useful tool", new Vector3(0, 0, 3.60f), 0f, 3.1f, Run.toolMaterials[0], true);
        var subs = Station(translation, "Someone is translating and timing", new Vector3(4.65f, 0, 5.05f), -16f, 2.65f, Run.subtitleMaterials[0], false);
        var world = Station(original, "Someone is drawing an original world", new Vector3(-4.6f, 0, 5.25f), 16f, 2.65f, Run.worldMaterials[0], false);
        var nearBook = Node("The saved volume is open beside the new work", central, new Vector3(-1.95f, .81f, 2.45f));
        Book(nearBook, "128-second archivedCopy, near matched location", Vector3.zero, 1.25f); Run.nearArchive = nearBook.gameObject;
        BookRest(central, new Vector3(-1.95f, 0, 2.45f));
        LayerPreview(central, new Vector3(.20f, 1.02f, 1.82f), .85f);
        var question = Node("An answerable shared question", before, new Vector3(2.52f, 1.75f, 2.55f));
        question.localRotation = Quaternion.Euler(0, -12, 0);
        Run.sharedQuestion = question.gameObject; Run.questionRenderer = Screen("Question and a concrete response", question, Vector3.zero, 1.48f, 1.97f, Run.questionMaterials[0]);

        var after = Node("After the window - shared working space", frame, new Vector3(0, 0, Run.transitDistance)); Run.afterWindowSpace = after;
        // The front emphasis changes from a tool to a shared original result. Tool and
        // translation remain visible in their own human-scale workstations.
        var worldResult = Station(after, "Original Star Bridge project and people", new Vector3(0, 0, 4.35f), 0f, 3.5f, Run.worldMaterials[1], false);
        var toolResult = Station(after, "The useful tool and its README remain available", new Vector3(-4.65f, 0, 5.10f), 16f, 2.7f, Run.toolMaterials[2], true);
        var subtitleResult = Station(after, "Translation and timing continue", new Vector3(4.65f, 0, 5.05f), -16f, 2.7f, Run.subtitleMaterials[1], false);
        LayerPreview(after, new Vector3(-4.5f, 1.05f, 3.7f), .60f);
        Run.toolScreens = new[] { tool, toolResult }; Run.subtitleScreens = new[] { subs, subtitleResult }; Run.worldScreens = new[] { world, worldResult };
        Book(after, "The preserved volume continues to be used", new Vector3(-1.95f, .81f, 2.45f), 1.25f);
        BookRest(after, new Vector3(-1.95f, 0, 2.45f));
        var memory = Node("The outside now lives on a small screen inside", after, new Vector3(-3.35f, 1.20f, 1.50f)); memory.localRotation = Quaternion.Euler(0, 22f, 0);
        Screen("A quiet pre-rendered memory of the earlier space", memory, Vector3.zero, 1.35f, .759f, ImageMaterial("memory-v5-archive"));
        Box("Memory screen slender support", memory, new Vector3(0, -.57f, .10f), new Vector3(.04f, .45f, .04f), Metal);
        Label("Inside outside connection", "刚才的外面，还在这里", memory, new Vector3(0, -.49f, -.04f), 2.0f, .080f, "#a8bdbf");
        BuildOriginalWorld(after);
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = i == 0 ? new Vector3(-4.65f, .92f, 4.25f) : i == 1 ? new Vector3(4.65f, .92f, 4.20f) : new Vector3(0, .91f, 3.65f);
            var mark = Node("Result has named contributions " + i, after, p);
            Primitive("Finished result indicator", PrimitiveType.Sphere, mark, Vector3.zero, new Vector3(.065f, .065f, .065f), Mint);
            Published.Add(mark.gameObject);
        }
        var publication = Node("A newcomer publishes the first version", after, new Vector3(-2.15f, 1.72f, 1.60f)); publication.localRotation = Quaternion.Euler(0, 13f, 0);
        Run.newcomerPublication = publication.gameObject; Run.newcomerScreen = Screen("Original author and new contribution together", publication, Vector3.zero, 1.95f, 1.219f, Run.newcomerMaterial);
        var portal = Node("A real thick window becoming an open threshold", frame, new Vector3(0, 1.65f, 5.6f)); Run.crossingWindow = portal;
        foreach (float side in new[] { -1f, 1f })
        {
            Box("Substantial window jamb", portal, new Vector3(side * 2.47f, 0, .02f), new Vector3(.14f, 2.98f, .31f), Metal);
            Box("Copper inner jamb", portal, new Vector3(side * 2.39f, 0, -.15f), new Vector3(.025f, 2.86f, .04f), Rim);
        }
        Box("Deep titlebar", portal, new Vector3(0, 1.49f, .02f), new Vector3(5.08f, .19f, .31f), Metal);
        Box("Deep sill", portal, new Vector3(0, -1.49f, .02f), new Vector3(5.08f, .13f, .31f), Metal);
        Box("Titlebar luminous rule", portal, new Vector3(0, 1.38f, -.15f), new Vector3(4.85f, .018f, .03f), Mint);
        for (int b = 0; b < 3; b++) Box("Raised familiar window control", portal, new Vector3(1.76f + b * .22f, 1.49f, -.16f), new Vector3(.13f, .08f, .04f), Paper);
        var preview = Primitive("Preparation preview removed before the physical crossing", PrimitiveType.Quad, portal, new Vector3(0, 0, .10f), new Vector3(4.70f, 2.77f, 1f), Run.worldMaterials[1]); Run.windowPreview = preview.gameObject;
        var key = Node("One restrained work light", Run.contentRoot.transform, new Vector3(0, 2.7f, .9f)).gameObject.AddComponent<Light>();
        key.type = LightType.Point; key.range = 9f; key.intensity = .9f; key.color = C("#a1caff"); key.shadows = LightShadows.None; Run.workLight = key;
    }
    static void BookRest(Transform parent, Vector3 position)
    {
        var stand = Node("A physical rest for the saved volume", parent, position);
        Box("Reference reading top", stand, new Vector3(0, .77f, 0), new Vector3(.63f, .06f, .49f), Wood);
        Box("Reference reading support", stand, new Vector3(0, .39f, .05f), new Vector3(.04f, .73f, .04f), Metal);
        Box("Reference stand foot", stand, new Vector3(0, .025f, .05f), new Vector3(.49f, .035f, .40f), Metal);
    }
    static void BuildOriginalWorld(Transform parent)
    {
        var result = Node("An original physical Star Bridge world", parent, new Vector3(0, .86f, 1.5f)); Run.originalWorldResult = result.gameObject;
        // A real small model. Sheltered seats, a paper-star sculpture and a clear path
        // are visible from oblique viewpoints as well as from the reference eye.
        Box("Diorama stand top", result, new Vector3(0, -.10f, 0), new Vector3(2.75f, .10f, 1.9f), Metal);
        foreach (float x in new[] { -.98f, .98f }) Box("Diorama stand foot", result, new Vector3(x, -.48f, 0), new Vector3(.065f, .70f, .06f), Metal);
        var stages = new List<GameObject>();
        var platform = Node("A useful open platform", result, Vector3.zero); stages.Add(platform.gameObject);
        Box("Original world foundation", platform, Vector3.zero, new Vector3(2.40f, .10f, 1.55f), Mat("Star Bridge platform", "#376b74", .08f));
        for (int x = -5; x <= 5; x++) Box("Model floor plank", platform, new Vector3(x * .215f, .056f, 0), new Vector3(.006f, .006f, 1.5f), Mat("Star Bridge grain", "#5e9b9d", .05f));
        var shelter = Node("An original shelter", result, new Vector3(-.60f, .05f, .22f)); stages.Add(shelter.gameObject);
        foreach (float x in new[] { -.40f, .40f }) foreach (float z in new[] { -.25f, .25f })
            Box("Small shelter upright", shelter, new Vector3(x, .47f, z), new Vector3(.045f, .94f, .045f), Gold);
        var roof = Box("Folded original shelter roof", shelter, new Vector3(0, 1.0f, 0), new Vector3(1.13f, .07f, .84f), Mat("Star Bridge roof", "#76bcb6", .10f)); roof.localRotation = Quaternion.Euler(0, 0, -9f);
        var sculpture = Node("A shared original paper-star sculpture", result, new Vector3(.58f, .76f, .25f)); stages.Add(sculpture.gameObject); Run.worldSculpture = sculpture;
        Star("Original gold star", sculpture, Vector3.zero, .26f, Gold);
        Box("Sculpture fine hanging line", sculpture, new Vector3(0, .29f, 0), new Vector3(.009f, .30f, .009f), Metal);
        var seats = Node("Seats and lights tested together", result, Vector3.zero); stages.Add(seats.gameObject);
        foreach (float x in new[] { .46f, .87f }) Box("Seat foot", seats, new Vector3(x, .17f, -.30f), new Vector3(.04f, .30f, .04f), Wood);
        Box("A bench for two", seats, new Vector3(.67f, .32f, -.30f), new Vector3(.87f, .07f, .24f), Wood);
        foreach (Vector3 p in new[] { new Vector3(-1.01f, .12f, -.53f), new Vector3(1.01f, .12f, .51f), new Vector3(.13f, .12f, -.52f) })
            Primitive("Small world path light", PrimitiveType.Sphere, seats, p, Vector3.one * .10f, Mint);
        Label("A finished, own small world", "星桥", result, new Vector3(0, -.04f, -.99f), .9f, .085f, "#b7e3db");
        Run.worldDetailStages = stages.ToArray();
    }

    static OWActorRig Actor(Transform parent, int sourceSlot, int index, bool newArrival)
    {
        OWActorRig source = sourceSlot < SourceCast.alternateActors.Length && SourceCast.alternateActors[sourceSlot] != null ? SourceCast.alternateActors[sourceSlot] : SourceCast.actors[sourceSlot];
        // New arrivals use the other already imported body. The anonymity and poses
        // distinguish a role; no unapproved third-party avatar is introduced.
        if (newArrival) source = SourceCast.actors[sourceSlot];
        var go = Object.Instantiate(source.gameObject, parent); go.name = "Renewal humanoid slot " + index; go.SetActive(true);
        var actor = go.GetComponent<OWActorRig>(); actor.actorSlot = "full-renewal-" + index;
        if (actor.visualRoot == null || actor.animator == null) throw new InvalidOperationException("Source Humanoid rig is incomplete");
        actor.visualRoot.gameObject.SetActive(true); actor.animator.gameObject.SetActive(true); actor.animator.enabled = true;
        actor.animator.applyRootMotion = false; actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
        foreach (var child in go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform && (t.name.StartsWith("Carried shared work") || t.name.StartsWith("Readable low contact tray"))).ToArray()) Object.DestroyImmediate(child.gameObject);
        for (int i = 0; i < SourceCast.bodyRenderers.Length; i++)
        {
            var r = SourceCast.bodyRenderers[i]; if (r == null || !r.transform.IsChildOf(source.transform)) continue;
            string relative = AnimationUtility.CalculateTransformPath(r.transform, source.transform); var target = go.transform.Find(relative);
            if (target != null) { var copied = target.GetComponent<Renderer>(); if (copied != null) copied.enabled = SourceCast.bodyBaseEnabled[i]; }
        }
        // The copied V5 actor is already normalized to 1.25 m; never multiply scale
        // again or mutate the imported avatar, its materials or its controller.
        actor.transform.localScale = Vector3.one; actor.animator.Rebind(); actor.animator.Play("V4Idle", 0, 0); actor.animator.Update(0);
        Sync(actor); return actor;
    }
    static GameObject ContactTray(OWActorRig actor)
    {
        actor.animator.Play("V4Type", 0, .5f); actor.animator.Update(0);
        Vector3 hands = actor.transform.InverseTransformPoint((actor.leftHand.position + actor.rightHand.position) * .5f);
        var tray = Node("Renewal contact surface under both hands", actor.transform, new Vector3(0, hands.y - .036f, hands.z));
        Box("Contact tray", tray, Vector3.zero, new Vector3(.59f, .035f, .29f), Wood);
        for (int row = 0; row < 3; row++) for (int col = 0; col < 8; col++) Box("Small actual keyboard key", tray, new Vector3((col - 3.5f) * .055f, .025f, (row - 1) * .058f), new Vector3(.044f, .011f, .045f), Paper);
        Box("Small contact stem", tray, new Vector3(0, -hands.y * .5f, .04f), new Vector3(.025f, hands.y, .025f), Metal);
        Box("Small contact foot", tray, new Vector3(0, -hands.y + .03f, .04f), new Vector3(.30f, .025f, .26f), Metal);
        return tray.gameObject;
    }
    static void BuildActors(Transform parent, JObject data)
    {
        var rows = (JArray)data["actors"]; var castRoot = Node("People keep a stable physical reference", parent, Vector3.zero);
        int count = rows.Count; Run.actors = new OWActorRig[count]; Run.actorNameplates = new Transform[count];
        Run.actorKeyOffsets = new int[count]; Run.actorKeyCounts = new int[count]; Run.actorShowStarts = new float[count]; Run.actorShowEnds = new float[count];
        Run.carriedBooks = new GameObject[count]; Run.contactTrays = new GameObject[count]; Run.sampledActorGestures = new int[count];
        var times = new List<float>(); var positions = new List<Vector3>(); var yaws = new List<float>(); var poses = new List<int>();
        for (int i = 0; i < count; i++)
        {
            var row = rows[i]; var actor = Actor(castRoot, (int)row["source_slot"], i, (bool)row["new_arrival"]); Run.actors[i] = actor;
            var plate = Node("Anonymous role " + i, castRoot, Vector3.zero);
            Box("Subtle anonymous nameplate", plate, new Vector3(0, 0, .012f), new Vector3(.59f, .17f, .022f), Mat("Anonymous plate", "#142d38", .18f));
            Label("No personal identity", "xxxx", plate, new Vector3(0, 0, -.003f), .55f, .086f, "#d5eeee"); Run.actorNameplates[i] = plate;
            Run.actorShowStarts[i] = (float)row["show"][0]; Run.actorShowEnds[i] = (float)row["show"][1];
            var keys = (JArray)row["keys"]; Run.actorKeyOffsets[i] = times.Count; Run.actorKeyCounts[i] = keys.Count;
            foreach (var key in keys) { times.Add((float)key[0]); positions.Add(new Vector3((float)key[1], 0f, (float)key[2])); yaws.Add((float)key[3]); poses.Add((int)key[4]); }
            Run.contactTrays[i] = ContactTray(actor);
            var carried = Node("The saved book as a hand-to-hand method", actor.rightHand == null ? actor.transform : actor.rightHand, new Vector3(.02f, 0, .035f));
            // Carrying copies keep the same real book mesh; acknowledgement labels are
            // not attached to tiny hands. This also avoids inventing a second symbol.
            var volume = SourceCast.archivedCopy.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null);
            if (volume != null)
            {
                Transform sourceVolume = volume.transform;
                while (sourceVolume.parent != null && sourceVolume.parent != SourceCast.archivedCopy.transform) sourceVolume = sourceVolume.parent;
                var piece = ClonePart(sourceVolume, carried, "The exact archived volume mesh and cover", Vector3.zero);
                piece.localRotation = Quaternion.Euler(0, 0, 15f);
                var renderers = piece.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    piece.localScale *= .19f / Mathf.Max(.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                }
            }
            else
            {
                Box("Bound pages with the same book silhouette", carried, Vector3.zero, new Vector3(.12f, .026f, .17f), Paper);
                Box("Readable teal book cover", carried, new Vector3(0, .016f, 0), new Vector3(.13f, .012f, .18f), Mint);
            }
            Run.carriedBooks[i] = carried.gameObject;
        }
        Run.actorKeyTimes = times.ToArray(); Run.actorKeyPositions = positions.ToArray(); Run.actorKeyYaws = yaws.ToArray(); Run.actorKeyGestures = poses.ToArray();
        var accessory = Node("Trying an original star accessory", Run.actors[2].head == null ? Run.actors[2].transform : Run.actors[2].head, new Vector3(.13f, .11f, 0));
        Run.originalAccessory = accessory.gameObject; var stars = new List<Transform>();
        stars.Add(Star("Original accessory large star", accessory, Vector3.zero, .072f, Gold));
        stars.Add(Star("Original accessory small star", accessory, new Vector3(.08f, -.055f, 0), .037f, Mint)); Run.originalAccessoryStars = stars.ToArray();
    }

    public static object RepairProgram()
    {
        Folder(Root + "/Programs"); var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/OldWebMap/Scripts/OWFullRenewal.cs");
        if (script == null) throw new InvalidOperationException("Import OWFullRenewal.cs first");
        var existing = AssetDatabase.FindAssets("t:UdonSharpProgramAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>).FirstOrDefault(p => p != null && p.sourceCsScript == script);
        if (existing == null) { existing = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); existing.sourceCsScript = script; AssetDatabase.CreateAsset(existing, Root + "/Programs/OWFullRenewal.asset"); }
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new InvalidOperationException("Udon compilation failed");
        AssetDatabase.SaveAssets(); return new { program = AssetDatabase.GetAssetPath(existing), compiled = true };
    }
    public static object Build()
    {
        Guard(); if (!File.Exists(DataPath)) throw new InvalidOperationException("Sync renewal data first: " + DataPath);
        SourceCast = Find<OWV4HubCast>(); if (SourceCast == null || SourceCast.actors == null || SourceCast.actors.Length < 4) throw new InvalidOperationException("Preserved V5 cast required");
        foreach (string folder in new[] { "Materials", "Meshes", "Fonts", "Programs", "Textures" }) Folder(Root + "/" + folder);
        Mats.Clear(); ReusedAssets.Clear(); LayerCards.Clear(); LayerOrigins.Clear(); LocalCursors.Clear(); RemoteCursors.Clear(); Published.Clear(); FontAsset();
        var parent = PhysicalParent().transform; var old = parent.Find(ModuleName);
        if (old != null) { if (old.GetComponent<OWFullRenewal>() == null) throw new InvalidOperationException("Refuse to replace an unowned same-name object"); Object.DestroyImmediate(old.gameObject); }
        var root = Node(ModuleName, parent, Vector3.zero); Run = root.gameObject.AddUdonSharpComponent<OWFullRenewal>(); Run.autoSample = false; Run.director = Find<OWEpisodeDirector>();
        var content = Node("Renewal authored content", root, Vector3.zero); Run.contentRoot = content.gameObject;
        Wood = Mat("Warm work wood", "#79644e"); Metal = Mat("Blue dark Metal", "#2f4653"); Rim = Mat("Copper window edge", "#b79365", .12f);
        Paper = Mat("Soft paper and cursor", "#d8e4d9", .17f); Mint = Mat("Mint work signal", "#73d3bc", .45f); Gold = Mat("Warm star gold", "#d9ae6d", .26f);
        Blue = Mat("Blue shared signal", "#7ea5d1", .24f); Dark = Mat("Display charcoal", "#152431");
        Run.toolMaterials = Enumerable.Range(0, 4).Select(i => ImageMaterial("tool-" + i)).ToArray(); Run.subtitleMaterials = Enumerable.Range(0, 3).Select(i => ImageMaterial("subtitles-" + i)).ToArray();
        Run.worldMaterials = Enumerable.Range(0, 3).Select(i => ImageMaterial("world-" + i)).ToArray(); Run.questionMaterials = Enumerable.Range(0, 2).Select(i => ImageMaterial("share-" + i)).ToArray(); Run.newcomerMaterial = ImageMaterial("newcomer");
        var data = JObject.Parse(File.ReadAllText(DataPath)); Run.transitStart = (float)data["travel"]["start"]; Run.transitEnd = (float)data["travel"]["end"]; Run.transitDistance = (float)data["travel"]["distance"];
        Archive(content); var frame = Node("Only the environment crosses the window", content, Vector3.zero); Run.transitFrame = frame; Workspaces(frame); BuildActors(content, data);
        Run.layerCards = LayerCards.ToArray(); Run.layerCardOrigins = LayerOrigins.ToArray(); Run.localCursors = LocalCursors.ToArray(); Run.remoteCursors = RemoteCursors.ToArray(); Run.publishedMarks = Published.ToArray();
        Run.Sample(0f); Sync(Run); foreach (var actor in Run.actors) Sync(actor);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        return Report("build.json", new { scene = ScenePath, parent = ParentName, root = ModuleName, autoSample = Run.autoSample, actors = Run.actors.Length, actorKeys = Run.actorKeyTimes.Length, screens = Run.toolScreens.Length + Run.subtitleScreens.Length + Run.worldScreens.Length, realLights = 2, independentLayerCards = Run.layerCards.Length, audioSources = root.GetComponentsInChildren<AudioSource>(true).Length, sharedAssets = ReusedAssets, sceneSaved = false });
    }

    public static void Pose(float time)
    {
        var run = Find<OWFullRenewal>(); if (run == null) throw new InvalidOperationException("Build renewal first"); run.Sample(time);
        string[] states = { "V4Idle", "V4Type", "V4Carry", "V4Present", "V4Point", "V4Inspect", "V4Walk", "V4Wave", "V4Calibrate", "V4Talk", "V4Adjust", "V4Shelve" };
        foreach (var actor in run.actors.Where(a => a != null && a.animator != null && a.gameObject.activeInHierarchy))
        { actor.animator.Play(states[Mathf.Clamp(actor.animator.GetInteger("V4Pose"), 0, 11)], 0, actor.animator.GetFloat("V4PoseTime")); actor.animator.Update(0f); }
        run.Sample(time); Canvas.ForceUpdateCanvases();
    }
    public static object Validate()
    {
        Guard(); var run = Find<OWFullRenewal>(); if (run == null) throw new InvalidOperationException("Build renewal first");
        var failures = new List<string>(); var rows = new List<object>(); float maxSpeed = 0f, minPair = 1000f, minEye = 1000f;
        var previous = new Vector3[run.actors.Length];
        try
        {
            if (run.autoSample) failures.Add("autoSample must remain false for the common presentation scheduler");
            if (run.GetComponentsInChildren<AudioSource>(true).Length > 0) failures.Add("Renewal may not contain audio sources");
            if (Mathf.Abs(run.transitStart - 217.60f) > .001f || Mathf.Abs(run.transitEnd - 222.72f) > .001f) failures.Add("Travel boundaries differ from the common score");
            for (int n = 0; n <= 864; n++)
            {
                float time = 186.88f + n * .08f; if (time > 256.001f) break; run.Sample(time);
                for (int i = 0; i < run.actors.Length; i++)
                {
                    var a = run.actors[i]; Vector3 p = a.transform.position;
                    if (n > 0) maxSpeed = Mathf.Max(maxSpeed, Vector3.Distance(p, previous[i]) / .08f); previous[i] = p;
                    if (!a.gameObject.activeSelf) continue;
                    minEye = Mathf.Min(minEye, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, -2.8f)));
                    for (int j = i + 1; j < run.actors.Length; j++) if (run.actors[j].gameObject.activeSelf) minPair = Mathf.Min(minPair, Vector3.Distance(p, run.actors[j].transform.position));
                }
            }
            if (maxSpeed > 1.3f) failures.Add("Actor speed exceeds 1.3 m/s: " + maxSpeed);
            if (minPair < .60f) failures.Add("Actors approach closer than .60 m: " + minPair);
            if (minEye < 1.80f) failures.Add("Actor enters the near eye column: " + minEye);
            foreach (float time in new[] { 189.60f, 195.20f, 200.32f, 208.64f, 215.04f, 217.60f, 219.52f, 222.72f, 230.40f, 240.64f, 251.52f })
            {
                run.Sample(time); Vector3 first = run.transitFrame.localPosition; int[] poses = (int[])run.sampledActorGestures.Clone();
                run.Sample(255.2f); run.Sample(186.9f); run.Sample(time);
                bool deterministic = Vector3.Distance(first, run.transitFrame.localPosition) < .00001f && poses.SequenceEqual(run.sampledActorGestures);
                if (!deterministic) failures.Add("Seek differs from direct sample at " + time);
                rows.Add(new { time, deterministic, visibleActors = run.sampledVisibleActors, run.sampledToolState, run.sampledSubtitleState, run.sampledWorldState, run.sampledTransit, offset = new[] { run.sampledWorldOffset.x, run.sampledWorldOffset.y, run.sampledWorldOffset.z } });
            }
            run.Sample(222.72f); float guideDistance = Vector2.Distance(new Vector2(run.actors[3].transform.localPosition.x, run.actors[3].transform.localPosition.z), new Vector2(0, -2.8f));
            if (guideDistance < 1.8f || guideDistance > 2.4f) failures.Add("Near demonstration must be at 1.8–2.4 m");
            var result = new { success = failures.Count == 0, failures, actors = run.actors.Length, maxSpeed, minPair, minEye, guideDistance, sourceBook = SourceCast == null ? "Read from V4 archivedCopy during Build" : SourceCast.archivedCopy.name, travel = new[] { run.transitStart, run.transitEnd }, rows, scope = "Editor numerical/source checks only. Parent must inspect stereo, physical head motion, actual humanoid poses and final integrated frames." };
            string json = Report("validate.json", result); if (!result.success) throw new InvalidOperationException(json); return json;
        }
        finally { run.Sample(0f); }
    }
}
