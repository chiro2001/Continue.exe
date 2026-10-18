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

public static class OWFilmV4Review
{
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v4");
    static T F<T>() where T : Component => Object.FindObjectsOfType<T>(true).First(b => b.gameObject.scene == SceneManager.GetActiveScene());
    static OWFilmV4Presentation Run() { if (EditorApplication.isPlaying || EditorApplication.isCompiling || SceneManager.GetActiveScene().path != "Assets/Scenes/OWM_FullFilm_v4.unity") throw new Exception("Stopped compiled full-film V4 required"); return F<OWFilmV4Presentation>(); }
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
        Directory.CreateDirectory(Path.GetDirectoryName(path)); var previous = camera.targetTexture; var active = RenderTexture.active;float captureAspect=camera.aspect;
        var rt = RenderTexture.GetTemporary(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        bool hasInfo=GizmoUtility.TryGetGizmoInfo(typeof(VRC.Udon.UdonBehaviour),out GizmoInfo info);bool icons=hasInfo&&info.iconEnabled;
        if(hasInfo)GizmoUtility.SetIconEnabled(typeof(VRC.Udon.UdonBehaviour),false);
        try { camera.targetTexture = rt;camera.aspect=width/(float)height; camera.Render(); RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { camera.targetTexture = previous;camera.aspect=captureAspect; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image);if(hasInfo)GizmoUtility.SetIconEnabled(typeof(VRC.Udon.UdonBehaviour),icons); }
    }
    public static object Capture(string label, float[] times)
    {
        var run = Run(); var rows = new List<object>();
        try
        {
            run.Waiting(); Frame(Camera.main, Tmp + "/" + label + "/waiting.png");
            foreach (float time in times) { Pose(time); string path = Tmp + "/" + label + "/v4_" + time.ToString("000.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"; Frame(Camera.main, path); rows.Add(new { time, path }); }
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
    public static object Cover()
    {
        var run=Run();var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView,aspect=camera.aspect;string path=Tmp+"/map-cover-1200x900.png";
        try
        {
            run.Waiting();camera.transform.SetPositionAndRotation(new Vector3(0,1.30f,-3.18f),Quaternion.identity);camera.fieldOfView=50;camera.aspect=4f/3;
            Canvas.ForceUpdateCanvases();foreach(var text in Object.FindObjectsOfType<TMP_Text>())text.ForceMeshUpdate(false,false);
            Frame(camera,path,1200,900);
            return Report("cover",new{success=true,path,width=1200,height=900,format="PNG",source="Actual V4 waiting scene Camera.Render",scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path});
        }
        finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.aspect=aspect;run.Waiting();run.offlinePreview=false;}
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
        Selection.activeGameObject = F<OWFilmV4Presentation>().workspace.gameObject;
        return new { success = true, time };
    }
    public static object Heights()
    {
        var run=Run();var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;var rows=new List<object>();
        try
        {
            foreach(float eye in new[]{.65f,1f,1.35f})
            {
                cam.transform.SetPositionAndRotation(new Vector3(0,eye,-2.8f),Quaternion.identity);
                run.Waiting();Frame(cam,Tmp+"/heights/waiting-"+eye.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png");
                Pose(205f);Frame(cam,Tmp+"/heights/workspace-"+eye.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png");
                run.Finished();Frame(cam,Tmp+"/heights/world-"+eye.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png");
                cam.transform.rotation=Quaternion.identity;Frame(cam,Tmp+"/heights/replay-"+eye.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png");
                rows.Add(new{eye});
            }
            cam.transform.SetPositionAndRotation(new Vector3(0,1.35f,-2.8f),Quaternion.Euler(0,180,0));run.Preview(1);Frame(cam,Tmp+"/heights/opening-rear.png");
            cam.transform.SetPositionAndRotation(new Vector3(0,1.35f,-.2f),Quaternion.Euler(-18,0,0));run.Preview(.12f);Frame(cam,Tmp+"/heights/cylinder-close.png");
            return Report("heights",new{success=true,rows,scope="Offline fixed eye-height composition checks, camera restored; no player head control"});
        }
        finally{cam.transform.SetPositionAndRotation(pos,rot);run.Waiting();run.offlinePreview=false;}
    }
    public static object RuntimeFrame(string label)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Actual Play Mode required");
        Frame(Camera.main,Tmp+"/runtime/"+label+".png");return new{success=true,label,scope="Actual ClientSim camera pixels, no head pose override"};
    }
    public static object GizmoApi()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GizmoUtility",false)).FirstOrDefault(t=>t!=null);
        return new{type=type==null?null:type.FullName,methods=type==null?null:type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Select(m=>m.ToString()).ToArray()};
    }
    public static object SoftwareScene()
    {
        var run=Run();Pose(225);Selection.activeGameObject=run.finalWorld.gameObject;
        var type=typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");if(type!=null){var inspector=EditorWindow.GetWindow(type);inspector.Show();inspector.Focus();}
        var view=UnityEditor.SceneView.lastActiveSceneView;if(view!=null){view.showGrid=false;view.drawGizmos=false;view.LookAt(new Vector3(0,1.2f,.5f),Quaternion.Euler(25,-25,0),10f);view.Focus();view.Repaint();}
        return new{success=true,scene="OWM_FullFilm_v4",selectedModel=run.finalWorld.name};
    }
}
