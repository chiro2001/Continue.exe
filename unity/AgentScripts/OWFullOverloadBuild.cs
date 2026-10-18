using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object = UnityEngine.Object;

// Main agent runs these entries through Pipeline, stopped in the full-film scene.
// Input PNGs: art/export/fullfilm/overload/* -> Root/Textures/* (new files only).
// Build authors only this module's geometry and assets; it does not save the scene.
public static class OWFullOverloadBuild
{
    const string ScenePath = "Assets/Scenes/OWM_FullFilm_v1.unity";
    const string Root = "Assets/OldWebMap/FullFilm/Revision1/Overload";
    const string ParentName = "FullFilm Continuation";
    const string ModuleName = "FullFilm Overload";
    const string SurfaceShader = "OldWebMap/FullFilm/Overload Surface";
    const string FieldShader = "OldWebMap/FullFilm/Overload Field";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-overload");

    static void Require()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Stopped full-film scene required: " + ScenePath);
        var parent = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ParentName);
        if (parent == null) throw new InvalidOperationException("Main integration must create " + ParentName);
        if (parent.transform.position.sqrMagnitude > .000001f || Quaternion.Angle(parent.transform.rotation, Quaternion.identity) > .001f || (parent.transform.lossyScale-Vector3.one).sqrMagnitude > .000001f)
            throw new InvalidOperationException(ParentName + " needs identity world transform");
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int k = path.LastIndexOf('/'); Folder(path.Substring(0, k)); AssetDatabase.CreateFolder(path.Substring(0, k), path.Substring(k + 1));
    }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var g = new GameObject(name); Undo.RegisterCreatedObjectUndo(g, "Build overload module");
        g.transform.SetParent(parent, false); g.transform.localPosition = position; return g.transform;
    }
    static void Style(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
    static Texture2D Texture(string file)
    {
        string path = Root + "/Textures/" + file;
        if (!File.Exists(path)) throw new InvalidOperationException("Copy art/export/fullfilm/overload/" + file + " to " + path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Texture importer unavailable: " + path);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true; importer.streamingMipmaps = false; importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
        importer.maxTextureSize = 4096; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.compressionQuality = 100;
        importer.crunchedCompression = false; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("Failed to import " + path);
        return texture;
    }
    static Material Material(string name, string shaderName, int queue, Texture texture, Color tint)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Missing or unsupported shader " + shaderName);
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        mat.name = name; mat.renderQueue = queue; mat.enableInstancing = true;
        mat.SetTexture("_MainTex", texture); mat.SetColor("_Tint", tint); mat.SetFloat("_Opacity", 1f);
        if (shaderName == SurfaceShader) { mat.SetVector("_Rect", new Vector4(0, 0, 1, 1)); mat.SetFloat("_ZWrite", 0f); }
        EditorUtility.SetDirty(mat); return mat;
    }
    static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Vector4> meta = null, List<Vector4> rects = null, List<Color> colors = null)
    {
        string path = Root + "/Meshes/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); } else mesh.Clear();
        mesh.name = name; mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uvs);
        if (meta != null) mesh.SetUVs(1, meta); if (rects != null) mesh.SetUVs(2, rects); if (colors != null) mesh.SetColors(colors);
        mesh.SetNormals(Enumerable.Repeat(Vector3.back, vertices.Count).ToList()); mesh.RecalculateBounds();
        if (meta != null) mesh.bounds = new Bounds(new Vector3(0, 11, 21), new Vector3(118, 83, 58));
        EditorUtility.SetDirty(mesh); return mesh;
    }
    static Mesh Quad(string name, float width, float height)
    {
        return SaveMesh(name, new List<Vector3> { new Vector3(-width/2,-height/2,0), new Vector3(-width/2,height/2,0), new Vector3(width/2,height/2,0), new Vector3(width/2,-height/2,0) },
            new List<int> { 0, 1, 2, 0, 2, 3 }, new List<Vector2> { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
    }
    static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position)
    {
        var p = Child(parent, name, position); p.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = p.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; Style(renderer); return p;
    }
    static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        Undo.RegisterCreatedObjectUndo(g, "Build overload furniture");
        g.transform.SetParent(parent, false); g.transform.localPosition = position; g.transform.localScale = scale;
        Object.DestroyImmediate(g.GetComponent<Collider>()); var renderer = g.GetComponent<Renderer>(); renderer.sharedMaterial = material; Style(renderer); return g.transform;
    }
    static Vector4 Rect(int index, int columns, int rows)
    {
        return new Vector4((index % columns) / (float)columns, 1f - (index / columns + 1) / (float)rows, 1f / columns, 1f / rows);
    }
    static Vector4 CreditRect() { return new Vector4(1536f / 2048f, 1f - 225f / 1280f, 512f / 2048f, 130f / 1280f); }

    sealed class FieldMesh
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> triangles = new List<int>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Vector4> meta = new List<Vector4>();
        public readonly List<Vector4> rect = new List<Vector4>();
        public readonly List<Color> colors = new List<Color>();
        public void Add(float width, float height, Vector3 local, Vector4 info, Vector4 tile, float role)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { local+new Vector3(-width/2,-height/2,0), local+new Vector3(-width/2,height/2,0), local+new Vector3(width/2,height/2,0), local+new Vector3(width/2,-height/2,0) });
            triangles.AddRange(new[] { first, first+1, first+2, first, first+2, first+3 });
            uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            for (int k = 0; k < 4; k++) { meta.Add(info); rect.Add(tile); colors.Add(new Color(role, 0, 0, 1)); }
        }
    }
    static Mesh Field(int stage)
    {
        var data = new FieldMesh(); float[] lanes = { -1.13f, -.91f, -.70f, .70f, .91f, 1.13f };
        float[] rows = { -.045f, .155f, .355f, .555f };
        for (int lane = 0; lane < lanes.Length; lane++)
            for (int row = 0; row < rows.Length; row++)
                for (int depth = 0; depth < 6; depth++)
                {
                    int i = lane * 24 + row * 6 + depth;
                    float seed = ((i * 43 + 17) % 139) / 139f;
                    float phase = (depth + (lane % 3) * .21f + row * .07f) / 6f;
                    var info = new Vector4(lanes[lane], rows[row], phase, seed);
                    int tile = stage == 0 ? i % 3 : stage == 1 ? i % 7 == 0 ? 9 : i % 5 == 0 ? 8 : 4 + i % 2 : stage == 2 ? i % 5 == 0 ? 13 : 6 + i % 2 : i % 5 == 0 ? 15 : 7;
                    data.Add(2.6f, 1.625f, Vector3.zero, info, Rect(tile, 4, 4), 0f);
                    if (stage == 0) data.Add(2.35f, .60f, new Vector3(0, -.69f, -.015f), info, CreditRect(), 1f / 3f);
                }
        for (int i = 0; i < 32; i++)
        {
            float sign = i % 2 == 0 ? -1f : 1f;
            var info = new Vector4(sign * (.59f + (i % 4) * .11f), -.015f + (i % 5) * .13f, ((i * 19) % 37) / 37f, ((i * 11) % 31) / 31f);
            data.Add(.80f, .50f, new Vector3(.10f, -.12f, -.10f), info, Rect(10, 4, 4), 2f / 3f);
            data.Add(.70f, .4375f, new Vector3(0, 0, -.11f), info, Rect(12, 4, 4), 1f);
        }
        return SaveMesh("OverloadField" + stage, data.vertices, data.triangles, data.uv, data.meta, data.rect, data.colors);
    }
    static void Sync(UdonSharpBehaviour behaviour)
    {
        EditorUtility.SetDirty(behaviour); UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour); if (backing != null) EditorUtility.SetDirty(backing);
    }
    static string Report(string file, object result)
    {
        Directory.CreateDirectory(Tmp + "/reports"); string json = JsonConvert.SerializeObject(result, Formatting.Indented);
        File.WriteAllText(Tmp + "/reports/" + file, json); return json;
    }

    public static object Build()
    {
        Require(); Folder(Root + "/Textures"); Folder(Root + "/Materials"); Folder(Root + "/Meshes");
        var focusTexture = Texture("overload-focus-atlas.png"); var fieldTexture = Texture("overload-field-atlas.png");
        var parent = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ParentName).transform; var previous = parent.Find(ModuleName);
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        var moduleRoot = Child(parent, ModuleName, Vector3.zero);
        var module = moduleRoot.gameObject.AddUdonSharpComponent<OWFullOverload>();
        module.autoSample = false; module.director = null;
        module.contentRoot = Child(moduleRoot, "Overload content 133.12-186.88", Vector3.zero);
        module.stageRoots = new Transform[4]; module.focusRoots = new Transform[4]; module.focusMaterials = new Material[4]; module.fieldMaterials = new Material[4];
        var focusQuad = Quad("OverloadFocusQuad", 5.4f, 3.0375f);
        var black = Material("OverloadCase", SurfaceShader, 2980, Texture2D.whiteTexture, new Color(.075f,.115f,.13f,1)); black.SetFloat("_Shade", 1);
        var metal = Material("OverloadMetal", SurfaceShader, 2980, Texture2D.whiteTexture, new Color(.24f,.32f,.34f,1)); metal.SetFloat("_Shade", 1);
        var desk = Material("OverloadDesk", SurfaceShader, 2980, Texture2D.whiteTexture, new Color(.13f,.18f,.19f,1)); desk.SetFloat("_Shade", 1);
        module.furnitureMaterials = new[] { black, metal, desk };
        string[] labels = { "14 Copies and missing names", "15 Search pressure", "16 Maintenance and queue", "17 Four lost conditions" };
        float[] starts = { 133.12f, 143.36f, 153.60f, 168.96f };
        Color[] tints = { new Color(.94f,.98f,1f,1), new Color(1f,.73f,.65f,1), new Color(.61f,.79f,.80f,1), new Color(.58f,.63f,.64f,1) };
        for (int stage = 0; stage < 4; stage++)
        {
            var group = Child(module.contentRoot, labels[stage], Vector3.zero); module.stageRoots[stage] = group;
            var focus = Child(group, "Readable focus", module.focusHome); module.focusRoots[stage] = focus;
            Box(focus, "Monitor casing", new Vector3(0,0,.09f), new Vector3(5.57f,3.21f,.18f), metal);
            Box(focus, "Inset bezel", new Vector3(0,0,-.015f), new Vector3(5.48f,3.12f,.04f), black);
            var mainMaterial = Material("OverloadFocus"+stage, SurfaceShader, 3000, focusTexture, Color.white);
            mainMaterial.SetVector("_Rect", Rect(stage == 0 ? 0 : stage == 1 ? 1 : stage == 2 ? 2 : 4, 2, 5));
            mainMaterial.SetFloat("_ZWrite", 1f);
            if (stage == 3) mainMaterial.SetFloat("_Desaturate", .28f);
            module.focusMaterials[stage] = mainMaterial;
            Part(focus, "Authored screen", focusQuad, mainMaterial, new Vector3(0,0,-.04f));
            var fieldMaterial = Material("OverloadField"+stage, FieldShader, 2960, fieldTexture, tints[stage]);
            fieldMaterial.SetFloat("_Start", starts[stage]); fieldMaterial.SetFloat("_Mode", stage);
            fieldMaterial.SetFloat("_Timeline", starts[stage]); fieldMaterial.SetFloat("_MotionTime", starts[stage]); module.fieldMaterials[stage] = fieldMaterial;
            Part(group, "Merged cards names cursors and clicks", Field(stage), fieldMaterial, Vector3.zero);
        }
        module.sourceCreditMaterial = Material("OverloadFallingCredit", SurfaceShader, 3013, fieldTexture, Color.white);
        module.sourceCreditMaterial.SetVector("_Rect", CreditRect());
        module.sourceCredit = Part(module.focusRoots[0], "One readable credit falls", Quad("OverloadCentralCredit", 3.5f, .889f), module.sourceCreditMaterial, new Vector3(-.12f,-1.24f,-.03f));
        module.cursorMaterial = Material("OverloadCentralCursor", SurfaceShader, 3015, fieldTexture, Color.white);
        module.cursorMaterial.SetVector("_Rect", Rect(10, 4, 4));
        module.cursor = Part(module.contentRoot, "One attempted operation", Quad("OverloadCursorQuad", .70f, .4375f), module.cursorMaterial, Vector3.zero);
        module.clickMaterial = Material("OverloadCentralClick", SurfaceShader, 3014, fieldTexture, Color.white);
        module.clickMaterial.SetVector("_Rect", Rect(12, 4, 4));
        module.clickRing = Part(module.contentRoot, "Attempt pulse", Quad("OverloadClickQuad", .42f, .2625f), module.clickMaterial, Vector3.zero);
        module.busyMaterial = Material("OverloadCentralWait", SurfaceShader, 3016, fieldTexture, new Color(.82f,.90f,.88f,1));
        module.busyMaterial.SetVector("_Rect", Rect(11, 4, 4));
        module.busySpinner = Part(module.contentRoot, "Only a waiting response", Quad("OverloadWaitQuad", .48f, .30f), module.busyMaterial, Vector3.zero);
        BuildMaintenanceFurniture(module, desk, metal, black);
        BuildActors(module, focusTexture, desk, metal, black);
        module.Sample(0f); Sync(module);
        foreach (var t in moduleRoot.GetComponentsInChildren<Transform>(true)) EditorUtility.SetDirty(t);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("build.json", new { success=true, scene=ScenePath, parent=ParentName, root=ModuleName, autoSample=module.autoSample,
            fieldCards=144, sourceNameTags=144, cursorsPerField=32, mergedFields=4, focusFrames=9,
            actors=module.actors.Length, actorAppearances=module.actors.Select(a=>a.appearanceId).ToArray(),
            renderers=moduleRoot.GetComponentsInChildren<Renderer>(true).Length, audioSources=moduleRoot.GetComponentsInChildren<AudioSource>(true).Length,
            textures=new[]{AssetDatabase.GetAssetPath(focusTexture),AssetDatabase.GetAssetPath(fieldTexture)}, sceneSaved=false });
    }

    static void BuildMaintenanceFurniture(OWFullOverload module, Material desk, Material metal, Material black)
    {
        var stage = module.stageRoots[2]; var furniture = Child(stage, "One maintained workstation", Vector3.zero);
        Box(furniture, "Desk top", new Vector3(0,.56f,5.75f), new Vector3(6.4f,.16f,1.5f), desk);
        Box(furniture, "Desk front lip", new Vector3(0,.59f,4.99f), new Vector3(6.4f,.075f,.055f), metal);
        for (int side = -1; side <= 1; side += 2)
            for (int back = 0; back < 2; back++)
                Box(furniture, "Desk leg " + side + " " + back, new Vector3(side*2.7f,.26f,5.2f+back*1.1f), new Vector3(.11f,.52f,.11f), black);
        Box(furniture, "Monitor foot", new Vector3(0,.69f,5.72f), new Vector3(1.08f,.08f,.63f), black);
        Box(furniture, "Monitor stand", new Vector3(0,.94f,5.79f), new Vector3(.22f,.56f,.19f), metal);
        Box(furniture, "Keyboard bed", new Vector3(0,.685f,5.05f), new Vector3(1.67f,.07f,.45f), black);
        // Four merged key banks give the desk a recognisable keyboard without hundreds of objects.
        for (int row = 0; row < 4; row++) Box(furniture, "Key bank " + row, new Vector3(0,.733f,4.89f+row*.09f), new Vector3(1.48f,.025f,.058f), metal);
        var lamp = Child(stage, "Maintenance lamp remains", new Vector3(-3.48f,0,5.65f)); module.maintenanceLamp = lamp;
        var warm = Material("OverloadLampMetal", SurfaceShader, 2980, Texture2D.whiteTexture, new Color(.46f,.38f,.21f,1)); warm.SetFloat("_Shade", .8f);
        var lit = Material("OverloadLampBulb", SurfaceShader, 3001, Texture2D.whiteTexture, new Color(1f,.79f,.38f,1));
        Box(lamp, "Lamp foot", new Vector3(0,.67f,0), new Vector3(.58f,.075f,.48f), warm);
        var lower = Box(lamp, "Lower articulated arm", new Vector3(.10f,1.07f,.05f), new Vector3(.065f,.86f,.065f), metal); lower.localRotation=Quaternion.Euler(0,0,-16);
        var upper = Box(lamp, "Upper articulated arm", new Vector3(.37f,1.65f,.05f), new Vector3(.065f,.60f,.065f), metal); upper.localRotation=Quaternion.Euler(0,0,-42);
        var shade = Box(lamp, "Lamp shade", new Vector3(.53f,1.92f,-.04f), new Vector3(.58f,.16f,.44f), warm); shade.localRotation=Quaternion.Euler(0,0,-10);
        Box(lamp, "Warm bulb", new Vector3(.53f,1.82f,-.05f), new Vector3(.43f,.035f,.32f), lit);
        var glow = Material("OverloadMaintenanceGlow", SurfaceShader, 2990, Texture2D.whiteTexture, new Color(1f,.68f,.29f,.80f)); glow.SetFloat("_Glow",1f);
        module.lampGlowMaterial=glow;
        var pool = Part(lamp,"Small lamp pool",Quad("OverloadLampPool",2.2f,1.4f),glow,new Vector3(.60f,.65f,-.15f)); pool.localRotation=Quaternion.Euler(90,0,0);
        Part(lamp,"Warm local halo",Quad("OverloadLampHalo",1.8f,1.8f),glow,new Vector3(.53f,1.83f,.14f));
    }

    static OWActorRig SourceActor(OWV4HubCast cast, int slot, bool alternate)
    {
        if (alternate && cast.alternateActors != null && slot < cast.alternateActors.Length && cast.alternateActors[slot] != null) return cast.alternateActors[slot];
        if (cast.actors == null || slot >= cast.actors.Length || cast.actors[slot] == null) throw new InvalidOperationException("V5 actor slot missing: " + slot);
        return cast.actors[slot];
    }
    static RuntimeAnimatorController OwnActorController(OWActorRig source)
    {
        if (source.animator == null || source.animator.runtimeAnimatorController == null) throw new InvalidOperationException("V5 actor needs the V4 motion controller");
        Folder(Root + "/Motion"); string path = Root + "/Motion/OverloadV4Motions.controller";
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
        if (controller == null)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source.animator.runtimeAnimatorController);
            if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, path)) throw new InvalidOperationException("Cannot persist a copy of the V4 actor controller: " + sourcePath);
            controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
        }
        return controller;
    }
    static OWActorRig CloneActor(OWV4HubCast cast, OWActorRig source, Transform preview, int index, RuntimeAnimatorController controller)
    {
        var go = Object.Instantiate(source.gameObject, preview); go.name = "Overload humanoid " + index;
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one; go.SetActive(true);
        var actor = go.GetComponent<OWActorRig>(); actor.actorSlot = "full-overload-" + index;
        if (actor.visualRoot == null || actor.animator == null || actor.leftHand == null || actor.rightHand == null) throw new InvalidOperationException("V5 Humanoid wrapper incomplete: " + source.actorSlot);
        actor.visualRoot.gameObject.SetActive(true);
        for (var t=actor.animator.transform; t!=null && t.IsChildOf(actor.transform); t=t.parent) t.gameObject.SetActive(true);
        actor.animator.enabled=true; actor.animator.runtimeAnimatorController=controller;
        actor.animator.applyRootMotion=false; actor.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
        foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var child in go.GetComponentsInChildren<Transform>(true).Where(t=>t!=go.transform && (t.name.StartsWith("Carried shared work") || t.name.StartsWith("Readable low contact tray"))).ToArray()) Object.DestroyImmediate(child.gameObject);
        if (cast.bodyRenderers != null && cast.bodyBaseEnabled != null)
            for (int i=0; i<cast.bodyRenderers.Length && i<cast.bodyBaseEnabled.Length; i++)
            {
                Renderer sourceRenderer=cast.bodyRenderers[i]; if(sourceRenderer==null || !sourceRenderer.transform.IsChildOf(source.transform))continue;
                string relative=AnimationUtility.CalculateTransformPath(sourceRenderer.transform,source.transform); var copiedTransform=go.transform.Find(relative);
                if(copiedTransform!=null) { var copied=copiedTransform.GetComponent<Renderer>(); if(copied!=null)copied.enabled=cast.bodyBaseEnabled[i]; }
            }
        // The saved V5 visual already carries its 1.25 m normalization and sole
        // correction. Only the wrapper is reset; never scale the imported visual twice.
        actor.animator.Rebind(); actor.animator.SetInteger("V4Pose",1); actor.animator.SetFloat("V4PoseTime",.5f);
        actor.animator.Play("V4Type",0,.5f); actor.animator.Update(0f);
        return actor;
    }
    static void ActorWorkstation(OWActorRig actor, Material screen, Material desk, Material metal, Material black)
    {
        Vector3 hands=actor.transform.InverseTransformPoint((actor.leftHand.position+actor.rightHand.position)*.5f);
        var tray=Child(actor.transform,"Overload persistent working surface",new Vector3(0,hands.y-.037f,hands.z+.01f));
        Box(tray,"Hands meet the keyboard surface",Vector3.zero,new Vector3(.68f,.035f,.38f),desk);
        Box(tray,"A floor supported stand",new Vector3(0,-tray.localPosition.y*.5f,.04f),new Vector3(.035f,tray.localPosition.y,.035f),metal);
        Box(tray,"A stable floor foot",new Vector3(0,-tray.localPosition.y+.02f,.04f),new Vector3(.40f,.035f,.33f),black);
        for(int row=0;row<3;row++)Box(tray,"Keyboard bank "+row,new Vector3(0,.026f,-.09f+row*.06f),new Vector3(.53f,.016f,.038f),metal);
        var display=Part(tray,"The work remains on the screen",Quad("OverloadActorScreen",.56f,.315f),screen,new Vector3(0,.09f,.15f));
        display.localRotation=Quaternion.Euler(65f,0f,0f);
    }
    static void BuildActors(OWFullOverload module, Texture2D texture, Material desk, Material metal, Material black)
    {
        var sourceCast=Object.FindObjectsOfType<OWV4HubCast>(true).FirstOrDefault();
        if(sourceCast==null)throw new InvalidOperationException("The copied V5 cast is required to author overload people");
        var sources=new[]{SourceActor(sourceCast,0,true),SourceActor(sourceCast,1,false),SourceActor(sourceCast,3,true)};
        if(sources.Select(a=>a.appearanceId).Distinct().Count()<2)
        {
            var other=sourceCast.actors.Concat(sourceCast.alternateActors??new OWActorRig[0]).FirstOrDefault(a=>a!=null && a.appearanceId!=sources[0].appearanceId);
            if(other!=null)sources[1]=other;
        }
        TMP_FontAsset font=sourceCast.nameplates==null || sourceCast.nameplates.actorLabels==null ? null : sourceCast.nameplates.actorLabels.Where(t=>t!=null).Select(t=>t.font).FirstOrDefault(f=>f!=null);
        if(font==null)font=Object.FindObjectsOfType<TMP_Text>(true).Where(t=>t.font!=null).Select(t=>t.font).FirstOrDefault();
        if(font==null)throw new InvalidOperationException("Existing V5 font needed for anonymous actor plates");
        var controller=OwnActorController(sources[0]); var castRoot=Child(module.contentRoot,"Three people keep working",Vector3.zero);
        module.actors=new OWActorRig[3]; module.actorNameplates=new Transform[3]; module.actorScreenMaterials=new Material[3]; module.sampledActorGestures=new int[3];
        module.actorPositions=new[]{new Vector3(-5.6f,0f,5f),new Vector3(3.7f,0f,3.7f),new Vector3(-3.4f,0f,3.5f)};
        module.actorYaws=new[]{28f,-32f,65f};
        // This temporary new root allows deterministic hand contact authoring even
        // while the main Waiting state has the continuation parent disabled. It
        // never activates that parent or changes an existing actor's pose.
        var preview=new GameObject("Overload owned actor authoring preview");
        try
        {
            for(int i=0;i<3;i++)
            {
                var actor=CloneActor(sourceCast,sources[i],preview.transform,i,controller);
                var screen=Material("OverloadActorScreen"+i,SurfaceShader,3000,texture,Color.white);
                screen.SetVector("_Rect",Rect(i==2?2:0,2,5)); module.actorScreenMaterials[i]=screen;
                ActorWorkstation(actor,screen,desk,metal,black);
                actor.animator.SetInteger("V4Pose",0); actor.animator.SetFloat("V4PoseTime",.38f); actor.animator.Play("V4Idle",0,.38f); actor.animator.Update(0f);
                actor.transform.SetParent(castRoot,false); actor.transform.localPosition=module.actorPositions[i]; actor.transform.localRotation=Quaternion.Euler(0,module.actorYaws[i],0);
                Undo.RegisterCreatedObjectUndo(actor.gameObject,"Build overload performer"); module.actors[i]=actor; Sync(actor);
                var plate=Child(castRoot,"Anonymous overload role "+i,module.actorPositions[i]+new Vector3(0,1.48f,0));
                Box(plate,"Small anonymous plate",new Vector3(0,0,.012f),new Vector3(.59f,.16f,.022f),black);
                // TMP must Awake while active before assigning its font; the final
                // continuation parent can legitimately be inactive in Waiting.
                var labelObject=new GameObject("xxxx"); Undo.RegisterCreatedObjectUndo(labelObject,"Build overload anonymous label");
                var label=labelObject.AddComponent<TextMeshPro>();
                label.font=font; label.fontSharedMaterial=font.material; label.text="xxxx"; label.richText=false; label.fontSize=.82f;
                label.alignment=TextAlignmentOptions.Center; label.enableWordWrapping=false; label.color=new Color(.83f,.94f,.94f,1);
                label.rectTransform.sizeDelta=new Vector2(.55f,.24f); Style(label.GetComponent<Renderer>());
                labelObject.transform.SetParent(plate,false); labelObject.transform.localPosition=new Vector3(0,0,-.004f); module.actorNameplates[i]=plate;
            }
        }
        finally { Object.DestroyImmediate(preview); }
    }

    static OWFullOverload Module()
    {
        var parent=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ParentName); var root=parent==null?null:parent.transform.Find(ModuleName);
        var module=root==null?null:root.GetComponent<OWFullOverload>();
        if(module==null)throw new InvalidOperationException("Build overload module first"); return module;
    }
    static string State(OWFullOverload module)
    {
        int i=module.sampledStage; var focus=i<0?null:module.focusRoots[i];
        return JsonConvert.SerializeObject(new { module.sampledStage,module.sampledFocus,module.sampledFailure,module.sampledQueueFrozen,module.sampledRepairComplete,module.sampledMotionTime,
            module.sampledVisibleActors, actorGestures=module.sampledActorGestures,
            visible=module.contentRoot.gameObject.activeSelf,position=V(focus==null?Vector3.zero:focus.localPosition),
            opacity=i<0?0:module.focusMaterials[i].GetFloat("_Opacity"),cursor=module.cursor.gameObject.activeSelf,spinner=module.busySpinner.gameObject.activeSelf,
            cursorPosition=V(module.cursor.localPosition),credit=module.sourceCredit.gameObject.activeSelf });
    }
    static float[] V(Vector3 value) { return new[] { value.x, value.y, value.z }; }
    static bool Finite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
    static object ProbeActor(OWActorRig source, int index, List<string> failures)
    {
        var preview=new GameObject("Overload isolated active validation "+index);
        var evidence=new List<object>(); int before=failures.Count;
        try
        {
            var clone=Object.Instantiate(source.gameObject,preview.transform); clone.SetActive(true);
            var probe=clone.GetComponent<OWActorRig>();
            clone.transform.localPosition=Vector3.zero; clone.transform.localRotation=Quaternion.identity; clone.transform.localScale=Vector3.one;
            if(probe==null || probe.animator==null || probe.visualRoot==null || probe.leftHand==null || probe.rightHand==null)
            {
                failures.Add("active Humanoid probe incomplete at "+index); return new{actor=index,success=false};
            }
            probe.visualRoot.gameObject.SetActive(true);
            for(var t=probe.animator.transform;t!=null && t.IsChildOf(probe.transform);t=t.parent)t.gameObject.SetActive(true);
            Animator animator=probe.animator; animator.enabled=true; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0f);
            if(!animator.gameObject.activeInHierarchy || !animator.isHuman || animator.runtimeAnimatorController==null)
                failures.Add("active Humanoid/controller probe failed at "+index);
            var parameters=animator.parameters;
            bool integerParameter=parameters.Any(p=>p.name=="V4Pose" && p.type==AnimatorControllerParameterType.Int);
            bool timeParameter=parameters.Any(p=>p.name=="V4PoseTime" && p.type==AnimatorControllerParameterType.Float);
            if(!integerParameter || !timeParameter)
            {
                failures.Add("active Animator V4 parameter probe failed at "+index);
                return new{actor=index,success=false,activeParameterNames=parameters.Select(p=>p.name).ToArray()};
            }
            string[] names={"V4Idle","V4Type","V4Present","V4Point","V4Inspect","V4Calibrate","V4Adjust"};
            int[] gestures={0,1,3,4,5,8,10};
            foreach(float phase in new[]{.28f,.43f,.55f})
                for(int k=0;k<names.Length;k++)
                {
                    animator.SetInteger("V4Pose",gestures[k]); animator.SetFloat("V4PoseTime",phase);
                    animator.Play(names[k],0,phase); animator.Update(.02f); animator.Update(0f);
                    bool roundTrip=animator.GetInteger("V4Pose")==gestures[k] && Mathf.Abs(animator.GetFloat("V4PoseTime")-phase)<.0001f;
                    bool state=animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(names[k]);
                    Vector3 left=probe.transform.InverseTransformPoint(probe.leftHand.position),right=probe.transform.InverseTransformPoint(probe.rightHand.position);
                    bool bones=Finite(left)&&Finite(right); bool separated=bones && left.x<right.x-.015f;
                    if(!roundTrip || !state || !bones || !separated)failures.Add("active pose probe failed at "+index+" "+names[k]+" "+phase);
                    evidence.Add(new{state=names[k],phase,roundTrip,stateSelected=state,finiteBones=bones,handsSeparated=separated,left=V(left),right=V(right)});
                }
            return new{actor=index,success=failures.Count==before,activeInHierarchy=animator.gameObject.activeInHierarchy,human=animator.isHuman,
                controller=AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),activeParameterNames=parameters.Select(p=>p.name).ToArray(),poses=evidence};
        }
        catch(Exception error)
        {
            failures.Add("active actor probe exception at "+index+": "+error.Message); return new{actor=index,success=false,error=error.ToString(),poses=evidence};
        }
        finally { Object.DestroyImmediate(preview); }
    }
    public static object Validate()
    {
        Require(); var module=Module(); var failures=new List<string>(); var rows=new List<object>(); var actorProbes=new List<object>();
        if(module.autoSample)failures.Add("autoSample must be false");
        if(module.stageRoots.Length!=4||module.focusRoots.Length!=4||module.focusMaterials.Length!=4||module.fieldMaterials.Length!=4)failures.Add("four authored sections required");
        if(module.GetComponentsInChildren<AudioSource>(true).Length!=0)failures.Add("module must not author audio");
        if(module.GetComponentsInChildren<Collider>(true).Length!=0)failures.Add("furniture and screens must not add collision");
        if(module.actors==null || module.actors.Length!=3)failures.Add("three persistent actors required");
        else
            for(int i=0;i<module.actors.Length;i++)
            {
                var actor=module.actors[i];
                if(actor==null || actor.animator==null || !actor.animator.isHuman || actor.animator.runtimeAnimatorController==null)failures.Add("valid copied Humanoid and controller required at "+i);
                else
                {
                    string controllerPath=AssetDatabase.GetAssetPath(actor.animator.runtimeAnimatorController);
                    var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    bool declared=controller!=null && controller.parameters.Any(p=>p.name=="V4Pose" && p.type==AnimatorControllerParameterType.Int) && controller.parameters.Any(p=>p.name=="V4PoseTime" && p.type==AnimatorControllerParameterType.Float);
                    if(!declared)failures.Add("persisted V4 controller parameters missing or wrongly typed at "+i+": "+controllerPath);
                    else actorProbes.Add(ProbeActor(actor,i,failures));
                }
                if(actor!=null && Mathf.Abs(actor.modelHeight-1.25f)>.02f)failures.Add("copied V5 body scale changed at "+i);
                if(i<module.actorPositions.Length)
                {
                    Vector3 p=module.actorPositions[i]; float edge=Mathf.Atan((Mathf.Abs(p.x)-.45f)/(p.z+2.8f))*Mathf.Rad2Deg;
                    if(edge<20f)failures.Add("actor enters central reading cone at "+i);
                }
            }
        float[] times={0,133.11f,133.12f,134.4f,136.96f,140.8f,143.36f,145.92f,150.4f,153.60f,156.16f,159.36f,162.56f,163.84f,165.12f,166.40f,167.68f,168.96f,171.2f,173.44f,175.68f,177.92f,180.16f,182.40f,184.64f,186.80f,186.88f,197.12f,256f};
        var states=new Dictionary<float,string>();
        try
        {
            foreach(float time in times)
            {
                module.Sample(time); states[time]=State(module);
                bool active=time>=133.12f&&time<186.88f;
                if(module.contentRoot.gameObject.activeSelf!=active)failures.Add("visibility at "+time);
                if(active&&module.stageRoots.Count(t=>t.gameObject.activeSelf)!=1)failures.Add("exactly one stage at "+time);
                if(active&&time<182.40f && !module.sampledQueueFrozen && module.sampledVisibleActors!=3)failures.Add("working ensemble missing at "+time);
                if(module.sampledQueueFrozen&&module.sampledVisibleActors!=1)failures.Add("one paused maintainer should remain at "+time);
                rows.Add(new{time,module.sampledStage,module.sampledFocus,module.sampledFailure,module.sampledRepairComplete,module.sampledQueueFrozen,module.sampledVisibleActors,gestures=module.sampledActorGestures==null?null:(int[])module.sampledActorGestures.Clone()});
            }
            for(int i=times.Length-1;i>=0;i--)
            {
                module.Seek(times[i]);
                if(State(module)!=states[times[i]])failures.Add("non-deterministic reverse seek at "+times[i]);
            }
            // Inner edges of advancing cards remain outside 20 degrees from the reference eye.
            // Worst case is the nearest pressure card, including its 18 percent approach scale.
            float depth=4.5f,width=2.6f*(1+depth*.032f)*1.18f;
            float clearAngle=Mathf.Atan(.70f-width*.5f/(depth+2.8f))*Mathf.Rad2Deg;
            if(clearAngle<20f)failures.Add("central reading cone too narrow");
            var result=new { success=failures.Count==0, samples=times.Length, reverseSeekSamples=times.Length, centralClearHalfAngle=clearAngle,
                failures,rows,actorProbes,sceneSaved=false,renderedValidationRequired=true };
            string report=Report("validate.json",result); if(failures.Count>0)throw new InvalidOperationException(report); return report;
        }
        finally { module.Sample(0); }
    }
}
