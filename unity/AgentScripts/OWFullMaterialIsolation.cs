using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using OldWebMap;
using Object = UnityEngine.Object;

public static class OWFullMaterialIsolation
{
    const string ScenePath = "Assets/Scenes/OWM_FullFilm_v1.unity";
    const string Root = "Assets/OldWebMap/FullFilm/Revision1";
    const string FolderPath = Root + "/MutableLegacyMaterials";
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v1");
    static T Find<T>() where T : Object => Object.FindObjectsOfType<T>(true).First();
    static void Guard()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Stopped compiled full-film scene required");
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    static Material[] Mutable()
    {
        var m = Find<OWV4Montage>(); var o = Find<OWV4Operations>();
        return m.windowMaterials.Concat(m.supportMaterials).Concat(new[] { m.typeMaterial, o.clickMaterial,
            o.subtitleSelectionMaterial, o.fileMaterial }).Concat(o.miniWaveMaterials).Concat(o.nodeMaterials).Distinct().ToArray();
    }
    static string Report(object data)
    {
        Directory.CreateDirectory(Tmp + "/reports");
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(Tmp + "/reports/material-isolation.json", json); return json;
    }
    static void Sync(UdonSharpBehaviour b)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (vm != null) EditorUtility.SetDirty(vm); EditorUtility.SetDirty(b);
    }
    static void SameProperties(Material source, Material copy)
    {
        if (source.shader != copy.shader || source.renderQueue != copy.renderQueue ||
            !source.shaderKeywords.OrderBy(x => x).SequenceEqual(copy.shaderKeywords.OrderBy(x => x)))
            throw new Exception("Material shader/queue/keywords changed: " + source.name);
        for (int i = 0; i < ShaderUtil.GetPropertyCount(source.shader); i++)
        {
            string prop = ShaderUtil.GetPropertyName(source.shader, i);
            var kind = ShaderUtil.GetPropertyType(source.shader, i);
            bool same = true;
            if (kind == ShaderUtil.ShaderPropertyType.Color) same = source.GetColor(prop) == copy.GetColor(prop);
            else if (kind == ShaderUtil.ShaderPropertyType.Vector) same = source.GetVector(prop) == copy.GetVector(prop);
            else if (kind == ShaderUtil.ShaderPropertyType.TexEnv) same = source.GetTexture(prop) == copy.GetTexture(prop) &&
                source.GetTextureScale(prop) == copy.GetTextureScale(prop) && source.GetTextureOffset(prop) == copy.GetTextureOffset(prop);
            else same = source.GetFloat(prop) == copy.GetFloat(prop);
            if (!same) throw new Exception("Material property changed: " + source.name + "/" + prop);
        }
    }
    public static object Apply()
    {
        Guard(); Folder(FolderPath);
        var map = new Dictionary<Material, Material>(); var rows = new List<object>();
        foreach (var source in Mutable())
        {
            if (source == null) throw new Exception("Missing animated material");
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath.StartsWith(Root + "/")) continue;
            string guid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(guid)) throw new Exception("Animated material is not persistent: " + source.name);
            string dest = FolderPath + "/" + guid + ".mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(dest);
            if (copy == null)
            {
                copy = new Material(source) { name = "FullFilm " + source.name };
                AssetDatabase.CreateAsset(copy, dest);
            }
            SameProperties(source, copy); map[source] = copy;
            rows.Add(new { source = sourcePath, destination = dest });
        }
        int renderers = 0, fields = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>(true).Where(r => r.gameObject.scene == SceneManager.GetActiveScene()))
        {
            bool changed = false; var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && map.TryGetValue(materials[i], out var copy)) { materials[i] = copy; changed = true; }
            if (changed) { r.sharedMaterials = materials; EditorUtility.SetDirty(r); renderers++; }
        }
        foreach (var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b => b.GetType().Name.StartsWith("OW")))
        {
            bool changed = false;
            foreach (var field in b.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(Material))
                {
                    var source = (Material)field.GetValue(b);
                    if (source != null && map.TryGetValue(source, out var copy)) { field.SetValue(b, copy); changed = true; fields++; }
                }
                else if (field.FieldType == typeof(Material[]))
                {
                    var original = (Material[])field.GetValue(b); if (original == null) continue;
                    var copied = (Material[])original.Clone(); bool arrayChanged = false;
                    for (int i = 0; i < copied.Length; i++)
                        if (copied[i] != null && map.TryGetValue(copied[i], out var copy)) { copied[i] = copy; arrayChanged = true; }
                    if (arrayChanged) { field.SetValue(b, copied); changed = true; fields++; }
                }
            }
            if (changed) Sync(b);
        }
        ValidateBindings();
        foreach (var material in map.Values) { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report(new { success = true, isolated = map.Count, renderers, fields, rows,
            propertyEquivalence = true, scope = "Copies and rebinds legacy animated materials; source shader properties unchanged. Does not certify historical source-asset byte identity." });
    }
    static void ValidateBindings()
    {
        if (Mutable().Any(m => m == null || !AssetDatabase.GetAssetPath(m).StartsWith(Root + "/")))
            throw new Exception("Legacy animated material still outside full-film asset directory");
        var m = Find<OWV4Montage>();
        for (int i = 0; i < m.windowMaterials.Length; i++)
            if (!m.imageRenderers[i].sharedMaterials.Contains(m.windowMaterials[i])) throw new Exception("Window material binding mismatch " + i);
        foreach (var b in Object.FindObjectsOfType<UdonSharpBehaviour>(true).Where(b => b.GetType().Name.StartsWith("OW")))
            foreach (var field in b.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Material[] materials = field.FieldType == typeof(Material) ? new[] { (Material)field.GetValue(b) } :
                    field.FieldType == typeof(Material[]) ? (Material[])field.GetValue(b) : null;
                if (materials == null) continue;
                foreach (var material in materials.Where(x => x != null && AssetDatabase.GetAssetPath(x).StartsWith(FolderPath + "/")))
                {
                    var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
                    if (vm == null) throw new Exception("Missing Udon backing " + b.name);
                    if (!vm.publicVariables.TryGetVariableValue(field.Name, out object value)) throw new Exception("Missing Udon material field " + field.Name);
                    bool bound = value is Material mat ? mat == material : value is Material[] arr && arr.Contains(material);
                    if (!bound) throw new Exception("Udon material not copied: " + b.GetType().Name + "." + field.Name);
                }
            }
    }
    public static object Validate() { Guard(); ValidateBindings(); return new { success = true, animatedMaterials = Mutable().Length }; }
}
