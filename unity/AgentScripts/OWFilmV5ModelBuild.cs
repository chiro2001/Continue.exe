using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

// V5-only static asset import and material recovery. This file does not add or
// modify runtime behaviours. The source Room01 FBX and V4 materials stay intact.
public static class OWFilmV5ModelBuild
{
    const string ScenePath = "Assets/Scenes/OWM_FullFilm_v5.unity";
    const string Root = "Assets/OldWebMap/FullFilm/Revision5/ModelAssets";
    const string Materials = Root + "/Materials";
    const string Textures = Root + "/Textures";
    const string Meshes = Root + "/Meshes";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v5");

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash <= 0) throw new InvalidOperationException("Cannot create asset folder: " + path);
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static void EnsureFolders()
    {
        foreach (string path in new[] { Root, Materials, Textures, Meshes }) Folder(path);
    }

    static object Report(string name, object data)
    {
        Directory.CreateDirectory(Tmp + "/reports");
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(Tmp + "/reports/" + name, json);
        return json;
    }

    static Texture2D Texture(string file, TextureImporterType type, bool srgb, bool repeat, bool dataTexture = false)
    {
        string path = Textures + "/" + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Copy the V5 recovery texture into " + path, path);
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.textureCompression = dataTexture ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (result == null) throw new InvalidOperationException("Texture import produced no asset: " + path);
        return result;
    }

    static Material OwnedMaterial(string name, string shaderName = "Standard")
    {
        string path = Materials + "/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Built-in shader is unavailable: " + shaderName);
        if (result == null)
        {
            result = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(result, path);
        }
        else result.shader = shader;
        if (result.HasProperty("_Color")) result.SetColor("_Color", Color.white);
        if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", 0f);
        if (result.HasProperty("_Glossiness")) result.SetFloat("_Glossiness", .18f);
        return result;
    }

    static Material ShelfMaterial()
    {
        var albedo = Texture("archive-shelf-wood017-albedo.jpg", TextureImporterType.Default, true, true);
        var normal = Texture("archive-shelf-wood017-normal-gl.jpg", TextureImporterType.NormalMap, false, true);
        var smoothness = Texture("archive-shelf-wood017-metallic-smoothness.png", TextureImporterType.Default, false, true, true);
        var result = OwnedMaterial("ArchiveShelf_Wood017_V5");
        result.SetTexture("_MainTex", albedo);
        result.SetTexture("_BumpMap", normal);
        result.SetFloat("_BumpScale", .3f);
        result.SetTexture("_MetallicGlossMap", smoothness);
        result.SetFloat("_GlossMapScale", .34f);
        result.DisableKeyword("_SPECULAR_SETUP");
        result.DisableKeyword("_SPECGLOSSMAP");
        result.EnableKeyword("_NORMALMAP");
        result.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(result);
        return result;
    }

    static Material BookMaterial(string name, string textureFile)
    {
        var albedo = Texture(textureFile, TextureImporterType.Default, true, false);
        var result = OwnedMaterial(name);
        result.SetTexture("_MainTex", albedo);
        result.SetFloat("_Glossiness", .12f);
        result.DisableKeyword("_NORMALMAP");
        result.DisableKeyword("_METALLICGLOSSMAP");
        result.DisableKeyword("_SPECULAR_SETUP");
        result.DisableKeyword("_SPECGLOSSMAP");
        EditorUtility.SetDirty(result);
        return result;
    }

    static Material Room01Wood021Material()
    {
        var albedo = Texture("room01-wood021-color.jpg", TextureImporterType.Default, true, true);
        var normal = Texture("room01-wood021-normal-gl.jpg", TextureImporterType.NormalMap, false, true);
        var smoothness = Texture("room01-wood021-metallic-smoothness.png", TextureImporterType.Default, false, true, true);
        var result = OwnedMaterial("Room01_Wood021_V5");
        result.SetColor("_Color", Color.white);
        result.SetFloat("_Metallic", 0f);
        result.SetFloat("_Glossiness", 1f);
        result.SetTexture("_MainTex", albedo);
        result.SetTexture("_BumpMap", normal);
        result.SetFloat("_BumpScale", 1f);
        result.SetTexture("_MetallicGlossMap", smoothness);
        result.SetFloat("_GlossMapScale", 1f);
        result.DisableKeyword("_SPECULAR_SETUP");
        result.DisableKeyword("_SPECGLOSSMAP");
        result.EnableKeyword("_NORMALMAP");
        result.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(result);
        return result;
    }

    static Material Room01Wood032Material()
    {
        var normal = Texture("room01-wood032-normal-gl.jpg", TextureImporterType.NormalMap, false, true);
        var smoothness = Texture("room01-wood032-specular-smoothness.png", TextureImporterType.Default, false, true, true);
        var result = OwnedMaterial("Room01_Wood032_V5", "Standard (Specular setup)");
        // FBX Principled Base Color is an untextured scalar for wood.004.
        result.SetColor("_Color", new Color(.800457835f, .301460505f, .072322369f, 1f));
        result.SetFloat("_Glossiness", 1f);
        result.SetTexture("_MainTex", null);
        result.SetTexture("_BumpMap", normal);
        result.SetFloat("_BumpScale", 1f);
        // FBX Principled Specular IOR Level is exactly 0; Unity's separate
        // Standard specular-workflow shader carries that F0 correctly.
        result.EnableKeyword("_SPECGLOSSMAP");
        result.DisableKeyword("_METALLICGLOSSMAP");
        result.EnableKeyword("_NORMALMAP");
        result.SetColor("_SpecColor", Color.black);
        result.SetTexture("_SpecGlossMap", smoothness);
        result.SetFloat("_GlossMapScale", 1f);
        EditorUtility.SetDirty(result);
        return result;
    }

    public static object Folders()
    {
        EnsureFolders();
        return new { success = true, root = Root, materials = Materials, textures = Textures, meshes = Meshes };
    }

    // Run after copying the files from art/export/fullfilm-v5/model-textures.
    // Materials and imported textures are owned by the V5-only project root.
    public static object PrepareRecoveryMaterials()
    {
        EnsureFolders();
        var shelf = ShelfMaterial();
        var book4 = BookMaterial("ArchiveBookCover04_V5", "archive-books-book4.png");
        var book5 = BookMaterial("ArchiveBookCover05_V5", "archive-books-book5.png");
        var wood021 = Room01Wood021Material();
        var wood032 = Room01Wood032Material();
        AssetDatabase.SaveAssets();
        return Report("model-materials.json", new
        {
            success = true,
            root = Root,
            materials = new object[]
            {
                new { name = shelf.name, path = AssetDatabase.GetAssetPath(shelf), sourceMaterial = "shelf", target = "Living_Shelf", slot = 0, colorMap = AssetDatabase.GetAssetPath(shelf.GetTexture("_MainTex")), normalMap = AssetDatabase.GetAssetPath(shelf.GetTexture("_BumpMap")), smoothnessMap = AssetDatabase.GetAssetPath(shelf.GetTexture("_MetallicGlossMap")), baseColor = "#FFFFFF", metallic = 0f, specularIorLevel = .5f, wrap = "Repeat", shader = shelf.shader.name, shaderMode = "Standard Metallic" },
                new { name = book4.name, path = AssetDatabase.GetAssetPath(book4), sourceMaterial = "book.008", target = "Books", slot = 1, colorMap = AssetDatabase.GetAssetPath(book4.GetTexture("_MainTex")), normalMap = "", smoothnessMap = "", baseColor = "#FFFFFF", metallic = 0f, specularIorLevel = .5f, wrap = "Clamp", shader = book4.shader.name, shaderMode = "Standard Metallic" },
                new { name = book5.name, path = AssetDatabase.GetAssetPath(book5), sourceMaterial = "book.009", target = "Books", slot = 2, colorMap = AssetDatabase.GetAssetPath(book5.GetTexture("_MainTex")), normalMap = "", smoothnessMap = "", baseColor = "#FFFFFF", metallic = 0f, specularIorLevel = .5f, wrap = "Clamp", shader = book5.shader.name, shaderMode = "Standard Metallic" },
                new { name = wood021.name, path = AssetDatabase.GetAssetPath(wood021), sourceMaterial = "wood.001", target = "Sofa / Wooden_Table", slot = -1, colorMap = AssetDatabase.GetAssetPath(wood021.GetTexture("_MainTex")), normalMap = AssetDatabase.GetAssetPath(wood021.GetTexture("_BumpMap")), smoothnessMap = AssetDatabase.GetAssetPath(wood021.GetTexture("_MetallicGlossMap")), baseColor = "#FFFFFF (source albedo map linked)", metallic = 0f, specularIorLevel = .5f, wrap = "Repeat", shader = wood021.shader.name, shaderMode = "Standard Metallic" },
                new { name = wood032.name, path = AssetDatabase.GetAssetPath(wood032), sourceMaterial = "wood.004", target = "Desk_Lamp / LowTable_wood", slot = -1, colorMap = "", normalMap = AssetDatabase.GetAssetPath(wood032.GetTexture("_BumpMap")), smoothnessMap = AssetDatabase.GetAssetPath(wood032.GetTexture("_SpecGlossMap")), baseColor = "0.800457835,0.301460505,0.072322369", metallic = 0f, specularIorLevel = 0f, wrap = "Repeat", shader = wood032.shader.name, shaderMode = "Standard (Specular setup)" }
            },
            sourceObjects = new[] { "Room01.fbx/Living_Shelf", "Room01.fbx/Books", "Room01.fbx/Desk_Lamp", "Room01.fbx/Wooden_Table", "Room01.fbx/LowTable_wood", "Room01.fbx/Sofa" },
            runtimeScriptsChanged = false
        });
    }

    static string ObjectPath(Transform t)
    {
        return t.parent == null ? t.name : ObjectPath(t.parent) + "/" + t.name;
    }

    static bool UnderNamedAncestor(Transform t, params string[] names)
    {
        for (var p = t.parent; p != null; p = p.parent)
            if (names.Any(name => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    static void RequireV5()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || scene.path != ScenePath)
            throw new InvalidOperationException("Stopped OWM_FullFilm_v5 scene required; active=" + scene.path);
    }

    public static object RestoreShelfAndBooksMaterials()
    {
        RequireV5();
        var shelfMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/ArchiveShelf_Wood017_V5.mat");
        var book4Material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/ArchiveBookCover04_V5.mat");
        var book5Material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/ArchiveBookCover05_V5.mat");
        if (shelfMaterial == null || book4Material == null || book5Material == null)
            throw new InvalidOperationException("Run PrepareRecoveryMaterials after importing the V5 texture batch first");

        var scene = SceneManager.GetActiveScene();
        var shelfRoots = Object.FindObjectsOfType<Transform>(true).Where(t => t.gameObject.scene == scene &&
            string.Equals(t.name, "Living_Shelf", StringComparison.OrdinalIgnoreCase) &&
            UnderNamedAncestor(t, "Imported archive shelf", "Imported maintained archive shelf", "Existing archive shelf")).ToArray();
        if (shelfRoots.Length != 4) throw new InvalidOperationException("Expected 4 affected Living_Shelf nodes in V5 scene, found " + shelfRoots.Length);
        var shelfRows = new List<object>();
        foreach (var t in shelfRoots)
        {
            var renderer = t.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException("Living_Shelf has no renderer: " + ObjectPath(t));
            var slots = renderer.sharedMaterials;
            if (slots.Length != 1) throw new InvalidOperationException("Living_Shelf source slot count changed at " + ObjectPath(t) + ": " + slots.Length);
            string old = slots[0] == null ? "<null>" : slots[0].name;
            slots[0] = shelfMaterial;
            renderer.sharedMaterials = slots;
            EditorUtility.SetDirty(renderer);
            shelfRows.Add(new { path = ObjectPath(t), slot = 0, previous = old, material = shelfMaterial.name });
        }

        var bookRoots = Object.FindObjectsOfType<Transform>(true).Where(t => t.gameObject.scene == scene &&
            string.Equals(t.name, "Books", StringComparison.OrdinalIgnoreCase) &&
            UnderNamedAncestor(t, "Imported reference books", "Imported maintenance reference books", "Existing reference volumes")).ToArray();
        if (bookRoots.Length != 10) throw new InvalidOperationException("Expected 10 affected plural Books nodes in V5 scene, found " + bookRoots.Length);
        var bookRows = new List<object>();
        foreach (var t in bookRoots)
        {
            var renderer = t.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException("Books has no renderer: " + ObjectPath(t));
            var slots = renderer.sharedMaterials;
            if (slots.Length != 3) throw new InvalidOperationException("Books source slot count changed at " + ObjectPath(t) + ": " + slots.Length);
            string paper = slots[0] == null ? "<null>" : slots[0].name;
            string old1 = slots[1] == null ? "<null>" : slots[1].name;
            string old2 = slots[2] == null ? "<null>" : slots[2].name;
            slots[1] = book4Material;
            slots[2] = book5Material;
            renderer.sharedMaterials = slots;
            EditorUtility.SetDirty(renderer);
            bookRows.Add(new { path = ObjectPath(t), slots = 3, preservedPaperSlot0 = paper,
                previousSlot1 = old1, previousSlot2 = old2, slot1 = book4Material.name, slot2 = book5Material.name });
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return Report("model-material-bindings.json", new
        {
            success = true,
            scene = scene.path,
            shelfInstances = shelfRows,
            pluralBookInstances = bookRows,
            shelfTextureWrap = "Repeat for authored UV range U -2.3039..1.9892, V -1.0036..1.9964",
            booksTextureWrap = "Clamp; active UV range is approximately 0..1",
            singleBookNodesTouched = false,
            v4FilesTouched = false
        });
    }

    // Configure the standalone FBX after the main agent copies it into V5/ModelAssets/Meshes.
    public static object PrepareCanopyImport()
    {
        EnsureFolders();
        string path = Meshes + "/fullfilm_v5_branch_canopy.fbx";
        if (!File.Exists(path)) throw new FileNotFoundException("Copy the V5 branch canopy FBX into " + path, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Canopy FBX has no ModelImporter: " + path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.preserveHierarchy = true;
        importer.SaveAndReimport();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Canopy FBX did not import as a prefab");
        return Report("canopy-import.json", new
        {
            success = true,
            path,
            root = prefab.name,
            rootLocalScale = prefab.transform.localScale.ToString(),
            rootLocalRotation = prefab.transform.localRotation.eulerAngles.ToString(),
            meshCount = prefab.GetComponentsInChildren<MeshFilter>(true).Count(x => x.sharedMesh != null),
            sourceAxes = "Blender Z-up; FBX converted once through Unity ModelImporter; do not add a manual second axis conversion",
            target = "Replace only final-world BranchCanopy geometry; keep its existing planter, outer scene and all unrelated scene references"
        });
    }

    public static object RestoreRoom01WoodMaterials()
    {
        RequireV5();
        var wood021 = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Room01_Wood021_V5.mat");
        var wood032 = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/Room01_Wood032_V5.mat");
        if (wood021 == null || wood032 == null)
            throw new InvalidOperationException("Run PrepareRecoveryMaterials before binding Room01 wood slots");
        var scene = SceneManager.GetActiveScene();
        var renderers = Object.FindObjectsOfType<Renderer>(true).Where(r => r.gameObject.scene == scene).ToArray();
        var rows = new List<object>();
        int changed = 0;
        Action<string,int,int,Material,string> bind = (meshName, slot, expectedCount, replacement, sourceMaterial) =>
        {
            var targets = renderers.Where(r => string.Equals(r.name, meshName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (targets.Length != expectedCount)
                throw new InvalidOperationException(meshName + " affected instance count changed: expected " + expectedCount + ", found " + targets.Length);
            foreach (var renderer in targets)
            {
                var slots = renderer.sharedMaterials;
                if (slot < 0 || slot >= slots.Length)
                    throw new InvalidOperationException(meshName + " slot " + slot + " is absent at " + ObjectPath(renderer.transform));
                var prior = slots[slot];
                rows.Add(new { path = ObjectPath(renderer.transform), mesh = meshName, slot,
                    sourceMaterial, previousMaterial = prior == null ? "<null>" : prior.name,
                    previousTexture = prior == null || prior.mainTexture == null ? "" : AssetDatabase.GetAssetPath(prior.mainTexture),
                    material = replacement.name });
                slots[slot] = replacement;
                renderer.sharedMaterials = slots;
                EditorUtility.SetDirty(renderer);
                changed++;
            }
        };
        // These exact slots are the 20 current V4 baseline collisions. Every
        // other slot, including Desk_Lamp's white/lamp and Sofa's fabric mats,
        // stays on its existing external material.
        bind("Desk_Lamp", 1, 10, wood032, "wood.004");
        bind("Wooden_Table", 0, 8, wood021, "wood.001");
        bind("LowTable_wood", 0, 1, wood032, "wood.004");
        bind("Sofa", 1, 1, wood021, "wood.001");
        EditorSceneManager.MarkSceneDirty(scene);
        return Report("room01-wood-material-bindings.json", new
        {
            success = changed == 20,
            scene = scene.path,
            changedSlots = changed,
            bindings = rows,
            otherRendererSlotsPreserved = true,
            source = "Room01.fbx embedded material node graphs; exact packed maps and material scalar values recorded in data/fullfilm-v5-room01-material-recovery.json"
        });
    }

    public static object IntegrateRecovery()
    {
        PrepareCanopyImport();
        PrepareRecoveryMaterials();
        var shelfAndBooks = RestoreShelfAndBooksMaterials();
        var furnitureWood = RestoreRoom01WoodMaterials();
        return Report("integrated-model-material-recovery.json", new
        {
            success = true,
            shelfAndBooks,
            furnitureWood
        });
    }

}
