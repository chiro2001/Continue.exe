using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Source and destination authoring must execute through Pipeline run_script.
// This file intentionally has no dependency on the Avatar SDK or NDMF assemblies.
public static class OWAvatarTransfer
{
    [Serializable] public class BoneEntry { public string bone; public string path; }
    [Serializable] public class ExportReport
    {
        public string actor;
        public string sourceProject;
        public string sourceScene;
        public string sourceObject;
        public string sourceModifiedUtc;
        public string sourceSha256;
        public string prefab;
        public string package;
        public bool humanoid;
        public int renderers;
        public long triangles;
        public int meshes;
        public int materials;
        public int textures;
        public BoneEntry[] bones;
        public string[] shaders;
        public string[] packageDependencies;
        public string[] components;
    }

    public static object Inspect(string scenePath)
    {
        bool opened;
        var scene = OpenReadOnly(scenePath, out opened);
        try
        {
            return new {
                project = Directory.GetCurrentDirectory(), scene = scenePath,
                candidates = Candidates(scene).Select(g => new {
                    name = g.name, path = HierarchyPath(g.transform), active = g.activeInHierarchy,
                    renderers = g.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    visibleRenderers = g.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy),
                    animator = g.GetComponent<Animator>() != null,
                    humanoid = g.GetComponent<Animator>() != null && g.GetComponent<Animator>().avatar != null && g.GetComponent<Animator>().avatar.isHuman,
                    ndmf = g.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Namespace != null && c.GetType().Namespace.StartsWith("nadena")).Select(c => c.GetType().FullName).Distinct().ToArray(),
                    shaders = g.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.shader == null ? "MISSING" : m.shader.name).Distinct().ToArray()
                }).ToArray()
            };
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    public static object Export(string scenePath, string sourcePath, string actorId, string packagePath)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be stopped and compiled");
        var sceneHash = Sha256(scenePath);
        string root = "Assets/OldWebActors/Imported/" + actorId;
        string prefabPath = root + "/" + actorId + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) throw new InvalidOperationException("Export exists: " + prefabPath);
        bool opened;
        var scene = OpenReadOnly(scenePath, out opened);
        Scene preview = default(Scene);
        GameObject clone = null;
        try
        {
            var source = Candidates(scene).Single(g => HierarchyPath(g.transform) == sourcePath);
            preview = EditorSceneManager.NewPreviewScene();
            clone = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(clone, preview);
            clone.SetActive(true);
            if (PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Bake MA armature merges and default appearance on the isolated clone.
            var processor = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("nadena.dev.ndmf.AvatarProcessor", false)).FirstOrDefault(t => t != null);
            if (processor == null) throw new InvalidOperationException("NDMF is required in the source project");
            var process = processor.GetMethod("ProcessAvatar", BindingFlags.Public | BindingFlags.Static, null, new [] { typeof(GameObject) }, null);
            if (process == null) throw new InvalidOperationException("Cannot find NDMF ProcessAvatar(GameObject)");
            try { process.Invoke(null, new object[] { clone }); }
            catch (TargetInvocationException error) { throw new InvalidOperationException("NDMF bake failed: " + error.InnerException, error.InnerException); }

            clone.name = actorId;
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            var animator = clone.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException("Export requires a valid Humanoid Avatar");
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Keep the current visible appearance, not every disabled wardrobe/toy.
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy) Object.DestroyImmediate(r);
            foreach (var a in clone.GetComponentsInChildren<Animator>(true))
                if (a != animator) Object.DestroyImmediate(a);
            foreach (var t in clone.GetComponentsInChildren<Transform>(true).Reverse())
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var c in t.GetComponents<Component>().Reverse())
                {
                    if (c == null || c is Transform || c is SkinnedMeshRenderer || c is MeshRenderer || c is MeshFilter || c == animator) continue;
                    Object.DestroyImmediate(c);
                }
            }

            EnsureFolder(root + "/Meshes");
            EnsureFolder(root + "/Materials");
            EnsureFolder(root + "/Textures");
            var meshes = new Dictionary<Mesh, Mesh>();
            var materials = new Dictionary<Material, Material>();
            var textures = new Dictionary<Texture, Texture>();
            foreach (var r in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) throw new InvalidOperationException("Missing mesh: " + r.name);
                r.sharedMesh = CopyMesh(r.sharedMesh, root, meshes);
                r.updateWhenOffscreen = true;
            }
            foreach (var f in clone.GetComponentsInChildren<MeshFilter>(true))
                if (f.GetComponent<MeshRenderer>() != null && f.sharedMesh != null) f.sharedMesh = CopyMesh(f.sharedMesh, root, meshes);
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                var mapped = r.sharedMaterials.Select(m => CopyMaterial(m, root, materials, textures)).ToArray();
                if (mapped.Any(m => m == null)) throw new InvalidOperationException("Missing material: " + r.name);
                r.sharedMaterials = mapped;
            }
            var avatar = Object.Instantiate(animator.avatar);
            avatar.name = actorId + " Humanoid";
            AssetDatabase.CreateAsset(avatar, root + "/Humanoid.asset");
            animator.avatar = avatar;
            var boneMap = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => new { id = (HumanBodyBones)i, transform = animator.GetBoneTransform((HumanBodyBones)i) }).Where(b => b.transform != null).Select(b => new BoneEntry { bone = b.id.ToString(), path = AnimationUtility.CalculateTransformPath(b.transform, clone.transform) }).ToArray();
            foreach (var bone in new [] { "Hips", "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot" })
                if (!boneMap.Any(b => b.bone == bone)) throw new InvalidOperationException("Required bone missing: " + bone);
            PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            AssetDatabase.SaveAssets();
            var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            var unsafeDependencies = dependencies.Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (unsafeDependencies.Length > 0) throw new InvalidOperationException("Unexpected script dependencies: " + string.Join(", ", unsafeDependencies));
            var report = new ExportReport {
                actor = actorId, sourceProject = Directory.GetCurrentDirectory(), sourceScene = scenePath,
                sourceObject = sourcePath, sourceModifiedUtc = File.GetLastWriteTimeUtc(scenePath).ToString("O"), sourceSha256 = sceneHash,
                prefab = prefabPath, package = packagePath, humanoid = true,
                renderers = clone.GetComponentsInChildren<Renderer>(true).Length,
                triangles = meshes.Values.Sum(m => (long)m.triangles.Length / 3),
                meshes = meshes.Count, materials = materials.Count, textures = textures.Count,
                bones = boneMap, shaders = materials.Values.Select(m => m.shader.name).Distinct().ToArray(),
                packageDependencies = dependencies.Where(p => p.StartsWith("Packages/")).ToArray(),
                components = clone.GetComponentsInChildren<Component>(true).Where(c => c != null).Select(c => c.GetType().FullName).Distinct().OrderBy(n => n).ToArray()
            };
            File.WriteAllText(root + "/source.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.ImportAsset(root + "/source.json");
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath));
            AssetDatabase.ExportPackage(new [] { root }, packagePath, ExportPackageOptions.Recurse);
            if (Sha256(scenePath) != sceneHash) throw new InvalidOperationException("Source scene unexpectedly changed");
            return report;
        }
        finally
        {
            if (clone != null) Object.DestroyImmediate(clone);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static object Repackage(string actorId, string packagePath)
    {
        string root = "Assets/OldWebActors/Imported/" + actorId;
        var report = Newtonsoft.Json.JsonConvert.DeserializeObject<ExportReport>(File.ReadAllText(root + "/source.json"));
        var prefab = PrefabUtility.LoadPrefabContents(root + "/" + actorId + ".prefab");
        try
        {
            var animator = prefab.GetComponent<Animator>();
            report.bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => new { id = (HumanBodyBones)i, transform = animator.GetBoneTransform((HumanBodyBones)i) }).Where(b => b.transform != null).Select(b => new BoneEntry { bone = b.id.ToString(), path = AnimationUtility.CalculateTransformPath(b.transform, prefab.transform) }).ToArray();
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        if (Sha256(report.sourceScene) != report.sourceSha256) throw new InvalidOperationException("Source scene changed");
        File.WriteAllText(root + "/source.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.ImportAsset(root + "/source.json");
        AssetDatabase.ExportPackage(new [] { root }, packagePath, ExportPackageOptions.Recurse);
        return new { actor = actorId, package = packagePath, bytes = new FileInfo(packagePath).Length, bones = report.bones.Length, sourceUnchanged = true };
    }

    static Mesh CopyMesh(Mesh source, string root, Dictionary<Mesh, Mesh> copies)
    {
        Mesh result;
        if (copies.TryGetValue(source, out result)) return result;
        result = Object.Instantiate(source);
        AssetDatabase.CreateAsset(result, AssetDatabase.GenerateUniqueAssetPath(root + "/Meshes/" + SafeName(source.name) + ".asset"));
        copies.Add(source, result);
        return result;
    }

    static Material CopyMaterial(Material source, string root, Dictionary<Material, Material> copies, Dictionary<Texture, Texture> textures)
    {
        if (source == null) return null;
        Material result;
        if (copies.TryGetValue(source, out result)) return result;
        if (source.shader == null) throw new InvalidOperationException("Missing shader: " + source.name);
        result = new Material(source);
        copies.Add(source, result);
        foreach (var property in result.GetTexturePropertyNames())
        {
            var tex = result.GetTexture(property);
            if (tex == null) continue;
            Texture copy;
            if (!textures.TryGetValue(tex, out copy))
            {
                string path = AssetDatabase.GetAssetPath(tex);
                string ext = Path.GetExtension(path);
                if (AssetDatabase.IsMainAsset(tex) && !string.IsNullOrEmpty(ext) && ext != ".asset" && (path.StartsWith("Assets/") || path.StartsWith("Packages/")))
                {
                    string target = AssetDatabase.GenerateUniqueAssetPath(root + "/Textures/" + SafeName(tex.name) + ext);
                    if (!AssetDatabase.CopyAsset(path, target)) throw new InvalidOperationException("Cannot copy texture: " + path);
                    copy = AssetDatabase.LoadAssetAtPath<Texture>(target);
                }
                else
                {
                    copy = Object.Instantiate(tex);
                    AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(root + "/Textures/" + SafeName(tex.name) + ".asset"));
                }
                if (copy == null) throw new InvalidOperationException("Cannot persist texture: " + tex.name);
                textures.Add(tex, copy);
            }
            result.SetTexture(property, copy);
        }
        AssetDatabase.CreateAsset(result, AssetDatabase.GenerateUniqueAssetPath(root + "/Materials/" + SafeName(source.name) + ".mat"));
        return result;
    }

    static GameObject[] Candidates(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null && c.GetType().FullName == "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor").Select(c => c.gameObject).ToArray();
    }
    static Scene OpenReadOnly(string path, out bool opened)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.path == path) { opened = false; return scene; }
        }
        opened = true;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }
    static string HierarchyPath(Transform t) { return t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name; }
    static string SafeName(string name) { var result = Regex.Replace(name, "[^a-zA-Z0-9_\\-]", "_"); return result.Length > 64 ? result.Substring(0, 64) : result; }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    static string Sha256(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
}
