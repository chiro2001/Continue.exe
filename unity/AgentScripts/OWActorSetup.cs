using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OldWebMap;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class OWActorSetup
{
    const string Root = "Assets/OldWebActors";
    const string PreviewScene = "Assets/Scenes/OWM_Actors.unity";
    const string ClipPath = Root + "/Motion/ActorReach.anim";
    const string ControllerPath = Root + "/Motion/ActorMotion.controller";

    public static async Task<object> Import(string packageDirectory)
    {
        foreach (var actor in new [] { "Kipfel", "Nemesis" })
        {
            string asset = Root + "/Imported/" + actor + "/" + actor + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(asset) != null) continue;
            string package = Path.Combine(packageDirectory, actor + ".unitypackage");
            if (!File.Exists(package)) throw new InvalidOperationException("Missing package: " + package);
            var completion = new TaskCompletionSource<bool>();
            AssetDatabase.ImportPackageCallback completed = name => completion.TrySetResult(true);
            AssetDatabase.ImportPackageCallback cancelled = name => completion.TrySetException(new InvalidOperationException("Import cancelled: " + name));
            AssetDatabase.ImportPackageFailedCallback failed = (name, error) => completion.TrySetException(new InvalidOperationException(name + ": " + error));
            AssetDatabase.importPackageCompleted += completed;
            AssetDatabase.importPackageCancelled += cancelled;
            AssetDatabase.importPackageFailed += failed;
            try { AssetDatabase.ImportPackage(package, false); await completion.Task; }
            finally
            {
                AssetDatabase.importPackageCompleted -= completed;
                AssetDatabase.importPackageCancelled -= cancelled;
                AssetDatabase.importPackageFailed -= failed;
            }
        }
        AssetDatabase.Refresh();
        return InspectImported();
    }

    public static object InspectImported()
    {
        return new [] { "Kipfel", "Nemesis" }.Select(actor => {
            string path = Root + "/Imported/" + actor + "/" + actor + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Prefab not imported: " + path);
            var animator = prefab.GetComponent<Animator>();
            var components = prefab.GetComponentsInChildren<Component>(true);
            var invalid = components.Where(c => c != null && !(c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is Animator)).Select(c => c.GetType().FullName).Distinct().ToArray();
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            var badMaterials = materials.Where(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader").Select(m => m == null ? "MISSING" : m.name).ToArray();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman || invalid.Length != 0 || badMaterials.Length != 0 || components.Any(c => c == null))
                throw new InvalidOperationException("Invalid actor: " + actor + "; components=" + string.Join(",", invalid) + "; materials=" + string.Join(",", badMaterials));
            return new { actor, path, humanoid = true, renderers = prefab.GetComponentsInChildren<Renderer>(true).Length, materials = materials.Length, shaders = materials.Select(m => m.shader.name).Distinct().ToArray(), missingScripts = 0 };
        }).ToArray();
    }

    public static object BuildPreview()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Stop Play Mode and finish compilation");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve the existing unsaved scene before switching");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene) != null) throw new InvalidOperationException("Preview already exists; use targeted edits");
        InspectImported();
        Folder(Root + "/Cast");
        Folder(Root + "/Motion");
        Folder(Root + "/Materials");
        RepairProgram();
        var controller = Motion();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rigs = new List<OWActorRig>();
        rigs.Add(CreateActor("actor-a", "Kipfel", "Actor_A", -0.65f, controller));
        rigs.Add(CreateActor("actor-b", "Nemesis", "Actor_B", 0.65f, controller));
        float height = rigs.Max(r => r.modelHeight);

        var floorMat = new Material(Shader.Find("Standard")) { name = "Actor Preview Floor", color = new Color(.20f, .22f, .24f) };
        AssetDatabase.CreateAsset(floorMat, Root + "/Materials/PreviewFloor.mat");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Preview Floor"; floor.transform.position = new Vector3(0, -.04f, 0); floor.transform.localScale = new Vector3(5, .08f, 4);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        var camera = new GameObject("Actor Preview Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, height * .63f, -Mathf.Max(2.8f, height * 2.7f));
        camera.transform.LookAt(new Vector3(0, height * .48f, 0));
        camera.fieldOfView = 36; camera.nearClipPlane = .02f; camera.farClipPlane = 30;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .15f, .18f);
        camera.gameObject.AddComponent<AudioListener>();
        var light = new GameObject("Actor Key Light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = .9f; light.transform.rotation = Quaternion.Euler(32, -25, 0);
        RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/OldWebMap/Fonts/OWM_Chinese-SDF.asset");
        foreach (var rig in rigs)
        {
            var label = new GameObject(rig.appearanceId + " Label").AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = rig.appearanceId.ToUpperInvariant(); label.fontSize = 1.6f; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(1.2f, .25f); label.transform.position = new Vector3(rig.transform.position.x, .04f, -.5f);
            label.transform.rotation = Quaternion.Euler(65, 0, 0); label.color = Color.white;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, PreviewScene);
        AssetDatabase.SaveAssets();
        return new { scene = PreviewScene, actors = rigs.Select(r => new { slot = r.actorSlot, appearance = r.appearanceId, height = r.modelHeight, bones = r.humanoidBones.Count(b => b != null) }).ToArray(), clip = ClipPath, humanoidClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath).humanMotion };
    }

    public static object ValidateMotion()
    {
        var rigs = Object.FindObjectsOfType<OWActorRig>();
        if (rigs.Length != 2) throw new InvalidOperationException("Open the actor preview scene");
        var results = new List<object>();
        foreach (var rig in rigs)
        {
            var animator = rig.animator;
            animator.Rebind(); animator.SetFloat("PerformanceTime", 0); animator.Update(0);
            Vector3 rest = rig.rightHand.position;
            animator.SetFloat("PerformanceTime", .5f); animator.Update(0);
            Vector3 reach = rig.rightHand.position;
            float distance = Vector3.Distance(rest, reach);
            animator.SetFloat("PerformanceTime", 0); animator.Update(0);
            if (distance < .02f) throw new InvalidOperationException("Humanoid motion did not move the hand: " + rig.appearanceId);
            results.Add(new { actor = rig.appearanceId, handTravelMeters = distance, bones = rig.humanoidBones.Count(b => b != null), modelHeight = rig.modelHeight, commonController = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) });
        }
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        return new { success = true, actors = results.ToArray() };
    }

    public static object ValidateRuntime()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode");
        var motion = ValidateMotion();
        var rigs = Object.FindObjectsOfType<OWActorRig>();
        foreach (var rig in rigs)
        {
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(rig);
            if (backing == null || (Animator)backing.GetProgramVariable("animator") != rig.animator) throw new InvalidOperationException("Missing Udon binding: " + rig.appearanceId);
            rig.animator.SetFloat("PerformanceTime", .5f);
            backing.SendCustomEvent("ResetPreview");
            if (Mathf.Abs(rig.animator.GetFloat("PerformanceTime")) > .0001f) throw new InvalidOperationException("Udon reset event failed: " + rig.appearanceId);
        }
        return new { success = true, motion, udonEvents = rigs.Length };
    }

    public static object Pose(float normalizedTime)
    {
        foreach (var rig in Object.FindObjectsOfType<OWActorRig>())
        { rig.animator.Rebind(); rig.animator.SetFloat("PerformanceTime", normalizedTime); rig.animator.Update(0); }
        return new { time = normalizedTime };
    }

    public static object FramePreview()
    {
        if (SceneManager.GetActiveScene().path != PreviewScene || EditorApplication.isPlaying) throw new InvalidOperationException("Open the stopped actor preview scene");
        var rigs = Object.FindObjectsOfType<OWActorRig>();
        foreach (var rig in rigs)
        {
            rig.transform.rotation = Quaternion.Euler(0, 180, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.transform);
        }
        float height = rigs.Max(r => r.modelHeight);
        var camera = GameObject.Find("Actor Preview Camera").GetComponent<Camera>();
        camera.transform.position = new Vector3(0, height * .63f, -Mathf.Max(2.8f, height * 2.7f));
        camera.transform.LookAt(new Vector3(0, height * .48f, 0));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return new { framed = true };
    }

    public static object CalibratePreview()
    {
        if (SceneManager.GetActiveScene().path != PreviewScene || EditorApplication.isPlaying) throw new InvalidOperationException("Open the stopped actor preview scene");
        // Use the imported reference stance, rather than zeroing every muscle (which bends knees).
        var source = PrefabUtility.LoadPrefabContents(Root + "/Imported/Kipfel/Kipfel.prefab");
        var pose = new HumanPose();
        try
        {
            var animator = source.GetComponent<Animator>();
            using (var handler = new HumanPoseHandler(animator.avatar, source.transform)) handler.GetHumanPose(ref pose);
        }
        finally { PrefabUtility.UnloadPrefabContents(source); }
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        for (int i = 0; i < HumanTrait.MuscleName.Length; i++)
        {
            string muscle = HumanTrait.MuscleName[i];
            float rest = pose.muscles[i];
            if (muscle.Contains("Arm Down-Up")) rest = -.72f;
            if (muscle.Contains("Forearm Stretch")) rest = .75f;
            float reach = rest;
            if (muscle == "Right Arm Down-Up") reach = -.25f;
            if (muscle == "Right Arm Front-Back") reach = .60f;
            if (muscle == "Right Forearm Stretch") reach = .15f;
            if (muscle == "Head Turn Left-Right") reach = -.12f;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), new AnimationCurve(new Keyframe(0, rest), new Keyframe(1, rest), new Keyframe(2, reach), new Keyframe(3, reach), new Keyframe(4, rest)));
        }
        EditorUtility.SetDirty(clip);
        var results = new List<object>();
        foreach (var rig in Object.FindObjectsOfType<OWActorRig>())
        {
            rig.animator.Rebind(); rig.animator.SetFloat("PerformanceTime", 0); rig.animator.Update(0);
            float lowest = float.PositiveInfinity;
            var footBones = new HashSet<Transform> { rig.leftFoot, rig.rightFoot, rig.animator.GetBoneTransform(HumanBodyBones.LeftToes), rig.animator.GetBoneTransform(HumanBodyBones.RightToes) };
            footBones.Remove(null);
            foreach (var renderer in rig.visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var indices = new HashSet<int>(renderer.bones.Select((bone, index) => new { bone, index }).Where(b => footBones.Contains(b.bone)).Select(b => b.index));
                if (indices.Count == 0) continue;
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    var vertices = baked.vertices; var weights = renderer.sharedMesh.boneWeights;
                    for (int i = 0; i < Math.Min(vertices.Length, weights.Length); i++)
                    {
                        var w = weights[i];
                        if ((indices.Contains(w.boneIndex0) && w.weight0 > .2f) || (indices.Contains(w.boneIndex1) && w.weight1 > .2f) || (indices.Contains(w.boneIndex2) && w.weight2 > .2f) || (indices.Contains(w.boneIndex3) && w.weight3 > .2f))
                            lowest = Mathf.Min(lowest, renderer.transform.TransformPoint(vertices[i]).y);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
            }
            if (float.IsInfinity(lowest)) throw new InvalidOperationException("Cannot determine foot contact: " + rig.appearanceId);
            rig.visualRoot.localPosition -= Vector3.up * lowest;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.visualRoot);
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject);
            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var assetRig = contents.GetComponent<OWActorRig>();
                assetRig.visualRoot.localPosition = rig.visualRoot.localPosition;
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            results.Add(new { actor = rig.appearanceId, footAdjustment = -lowest });
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return results.ToArray();
    }

    static OWActorRig CreateActor(string slot, string appearance, string fileName, float x, RuntimeAnimatorController controller)
    {
        var root = new GameObject(fileName);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Imported/" + appearance + "/" + appearance + ".prefab");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
        visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.SetFloat("PerformanceTime", 0); animator.Update(0);
        var bounds = visual.GetComponentsInChildren<Renderer>().First().bounds;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        visual.transform.localPosition -= Vector3.up * bounds.min.y;
        var rig = root.AddUdonSharpComponent<OWActorRig>();
        rig.actorSlot = slot; rig.appearanceId = appearance; rig.visualRoot = visual.transform; rig.animator = animator;
        rig.humanoidBones = new Transform[(int)HumanBodyBones.LastBone];
        for (int i = 0; i < rig.humanoidBones.Length; i++) rig.humanoidBones[i] = animator.GetBoneTransform((HumanBodyBones)i);
        rig.head = animator.GetBoneTransform(HumanBodyBones.Head); rig.leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rig.rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rig.leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot); rig.rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot); rig.modelHeight = bounds.size.y;
        UdonSharpEditorUtility.CopyProxyToUdon(rig);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(rig));
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, Root + "/Cast/" + fileName + ".prefab", InteractionMode.AutomatedAction);
        root.transform.position = new Vector3(x, 0, 0);
        root.transform.rotation = Quaternion.Euler(0, 180, 0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        return rig;
    }

    static RuntimeAnimatorController Motion()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (existing != null && AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null) return existing;
        var clip = new AnimationClip { name = "ActorReach", frameRate = 30, wrapMode = WrapMode.ClampForever };
        foreach (var muscle in HumanTrait.MuscleName)
        {
            float rest = muscle.Contains("Arm Down-Up") ? -.72f : 0f;
            if (muscle.Contains("Forearm Stretch")) rest = .75f;
            float reach = rest;
            if (muscle == "Right Arm Down-Up") reach = -.25f;
            if (muscle == "Right Arm Front-Back") reach = .60f;
            if (muscle == "Right Forearm Stretch") reach = .15f;
            if (muscle == "Head Turn Left-Right") reach = -.12f;
            var curve = new AnimationCurve(new Keyframe(0, rest), new Keyframe(1, rest), new Keyframe(2, reach), new Keyframe(3, reach), new Keyframe(4, rest));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
        }
        AssetDatabase.CreateAsset(clip, ClipPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("PerformanceTime", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var state = machine.AddState("ActorMotion"); state.motion = clip; state.timeParameterActive = true; state.timeParameter = "PerformanceTime";
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void RepairProgram()
    {
        Folder("Assets/OldWebMap/Programs");
        string path = "Assets/OldWebMap/Programs/OWActorRig.asset";
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if (program == null) { program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); AssetDatabase.CreateAsset(program, path); }
        program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/OldWebMap/Scripts/OWActorRig.cs");
        EditorUtility.SetDirty(program); AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new InvalidOperationException("UdonSharp compilation failed");
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
