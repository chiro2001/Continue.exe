using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using TMPro;
using OldWebMap;
using Object = UnityEngine.Object;

public static class OWV2Review
{
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v2");
    static T F<T>() where T : Component => Object.FindObjectsOfType<T>(true).First(b => b.gameObject.scene == SceneManager.GetActiveScene());
    static OWV2Presentation Run() { if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != "Assets/Scenes/OWM_FullFilm_v2.unity") throw new Exception("Stopped compiled V2 required"); return F<OWV2Presentation>(); }
    static float[] V(Vector3 v) => new[]{v.x,v.y,v.z};
    static string Report(string file, object value) { Directory.CreateDirectory(Tmp + "/reports"); var json = JsonConvert.SerializeObject(value, Formatting.Indented); File.WriteAllText(Tmp + "/reports/" + file + ".json", json); return json; }
    public static object Pose(float time)
    {
        var run = Run(); run.Preview(time);
        string[] names = { "V4Idle", "V4Type", "V4Carry", "V4Present", "V4Point", "V4Inspect", "V4Walk", "V4Wave", "V4Calibrate", "V4Talk", "V4Adjust", "V4Shelve" };
        foreach (var actor in Object.FindObjectsOfType<OWActorRig>(true))
        {
            var a = actor.animator; if (a == null || !a.gameObject.activeInHierarchy || !a.enabled || a.runtimeAnimatorController == null) continue;
            a.Play(names[Mathf.Clamp(a.GetInteger("V4Pose"), 0, 11)], 0, a.GetFloat("V4PoseTime")); a.Update(0);
        }
        run.Preview(time); Canvas.ForceUpdateCanvases(); foreach (var t in Object.FindObjectsOfType<TMP_Text>()) if (t.havePropertiesChanged) t.ForceMeshUpdate(false, false);
        if (run.channelParticles != null) foreach (var ps in run.channelParticles) if (ps != null && ps.gameObject.activeInHierarchy) ps.Simulate(Mathf.Repeat(time, 4f), true, true, true);
        UnityEditor.SceneView.RepaintAll();
        return new { success = true, time, run.sampledNative, run.sampledImage, run.visibleActors, run.sampledWorldScale };
    }
    static void Frame(Camera camera, string path, int width = 1600, int height = 900)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); var previous = camera.targetTexture; var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
    }
    public static object Capture(string label, float[] times)
    {
        var run = Run(); var rows = new List<object>();
        try
        {
            run.Waiting(); Frame(Camera.main, Tmp + "/" + label + "/waiting.png");
            foreach (float time in times) { Pose(time); string path = Tmp + "/" + label + "/v2_" + time.ToString("000.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"; Frame(Camera.main, path); rows.Add(new { time, path }); }
            run.Finished(); Frame(Camera.main, Tmp + "/" + label + "/finished.png");
            return Report("capture-" + label, new { success = true, rows, scope = "Editor camera preview, not live Udon or HMD" });
        }
        finally { run.Waiting(); run.offlinePreview = false; }
    }
    public static object Inspect()
    {
        var run = Run();
        return Report("inspect", new { success = true, scene = SceneManager.GetActiveScene().path, dirty = SceneManager.GetActiveScene().isDirty, camera = Camera.main == null ? null : new { position = V(Camera.main.transform.position), rotation = V(Camera.main.transform.eulerAngles) }, workspace = run.workspace.GetComponentsInChildren<MeshFilter>(true).Select(m => new { m.name, vertices = m.sharedMesh.vertexCount }).ToArray(), native = run.nativeStates.Length, frontActors = run.frontActors.Length, laterActors = run.laterActors.Length });
    }
    public static object Models(float time)
    {
        var run = Run(); Pose(time);
        return Report("model-poses", new { time, stage = run.editorStage.activeInHierarchy, root = V(run.transform.position), models = run.editorStage.GetComponentsInChildren<Renderer>(true).Select(r => new { r.name, active = r.gameObject.activeInHierarchy, r.enabled, position = V(r.transform.position), scale = V(r.transform.lossyScale), bounds = new { center = V(r.bounds.center), size = V(r.bounds.size) }, material = r.sharedMaterial == null ? null : r.sharedMaterial.name }).ToArray() });
    }
    public static object Ground(float time)
    {
        var run=Run();if(time>=256)run.Finished();else Pose(time);Physics.SyncTransforms();
        return Report("ground",new{time,hits=Physics.RaycastAll(new Vector3(0,3,-2.8f),Vector3.down,12).OrderBy(h=>h.distance).Select(h=>new{h.collider.name,h.distance,point=V(h.point),active=h.collider.gameObject.activeInHierarchy}).ToArray()});
    }
    public static object SceneView(float time)
    {
        Pose(time); var view = UnityEditor.SceneView.lastActiveSceneView;
        if (view != null) { view.LookAt(new Vector3(0f, 1.7f, 4.5f), Quaternion.Euler(10f, -18f, 0f), 7f); view.Focus(); view.Repaint(); }
        Selection.activeGameObject = F<OWV2Presentation>().workspace.gameObject;
        return new { success = true, time };
    }
}
