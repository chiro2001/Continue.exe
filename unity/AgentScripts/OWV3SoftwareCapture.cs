using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class OWV3SoftwareCapture
{
    struct WinRect { public int left, top, right, bottom; }
    delegate bool EnumerateWindow(IntPtr handle, IntPtr unused);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumerateWindow callback, IntPtr param);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out WinRect rect);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr handle,StringBuilder text,int count);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr bitmap);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr item);
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo { public uint size; public int width, height; public ushort planes, bits; public uint compression, imageSize; public int xppm, yppm; public uint colors, important; }
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint rows, [Out] byte[] pixels, ref BitmapInfo info, uint usage);
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v3");
    static IntPtr Window(int pid)
    {
        IntPtr best = IntPtr.Zero; long area = 0;
        EnumWindows((handle, unused) =>
        {
            GetWindowThreadProcessId(handle, out uint found); if (found != pid || !IsWindowVisible(handle)) return true;
            if (GetWindowRect(handle, out WinRect r)) { long size = (long)(r.right - r.left) * (r.bottom - r.top); if (size > area) { area = size; best = handle; } } return true;
        }, IntPtr.Zero);
        if (best == IntPtr.Zero) throw new Exception("No visible software window for PID " + pid); return best;
    }
    public static async Task<object> CaptureWindow(string label, int pid)
    {
        IntPtr window = Window(pid); ShowWindow(window, 3); SetForegroundWindow(window); await Task.Delay(650);
        IntPtr priorDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try {
        if (!GetWindowRect(window, out WinRect r)) throw new Exception("Window bounds unavailable");
        int x = Mathf.Max(0, r.left), y = Mathf.Max(0, r.top), width = Mathf.Min(3840, r.right - x), height = Mathf.Min(2160, r.bottom - y);
        if (width < 400 || height < 300) throw new Exception("Software window too small");
        var bytes = new byte[width * height * 4];
        IntPtr screen = GetDC(IntPtr.Zero), dc = CreateCompatibleDC(screen), bitmap = CreateCompatibleBitmap(screen, width, height), previous = SelectObject(dc, bitmap);
        bool foregroundVerified=GetForegroundWindow()==window;string method="PrintWindow window surface / PW_RENDERFULLCONTENT";
        try {
            if (!PrintWindow(window,dc,2))
            {
                if(!foregroundVerified)throw new Exception("Target window was not foreground; refusing to capture another application's desktop pixels");
                if (!BitBlt(dc, 0, 0, width, height, screen, x, y, 0x00CC0020)) throw new Exception("Screen pixel copy failed");method="Foreground-verified desktop window pixels";
            }
            SelectObject(dc, previous);
            var info = new BitmapInfo { size = 40, width = width, height = height, planes = 1, bits = 32, imageSize = (uint)bytes.Length };
            if (GetDIBits(dc, bitmap, 0, (uint)height, bytes, ref info, 0) != height) throw new Exception("Screen bitmap read failed");
        } finally { DeleteObject(bitmap); DeleteDC(dc); ReleaseDC(IntPtr.Zero, screen); }
        for(int i=3;i<bytes.Length;i+=4)bytes[i]=255;
        var texture = new Texture2D(width, height, TextureFormat.BGRA32, false); texture.LoadRawTextureData(bytes); texture.Apply();
        string folder = Tmp + "/software"; Directory.CreateDirectory(folder); string path = folder + "/" + label + ".png";
        try { File.WriteAllBytes(path, texture.EncodeToPNG()); } finally { UnityEngine.Object.DestroyImmediate(texture); }
        var title=new StringBuilder(1024);GetWindowText(window,title,title.Capacity);GetWindowThreadProcessId(window,out uint actualPid);
        var report = new { success = true, path, pid, actualPid, windowTitle=title.ToString(), windowVerified=actualPid==(uint)pid, foregroundVerified, method, width, height, capturedAtUtc = DateTime.UtcNow.ToString("o"), kind = "Actual target application window pixels; DPI-aware, window-specific, not a reconstructed software screenshot", project = SceneManager.GetActiveScene().path };
        string json = JsonConvert.SerializeObject(report, Formatting.Indented); File.WriteAllText(folder + "/" + label + ".json", json); return json;
        } finally { SetThreadDpiAwarenessContext(priorDpi); }
    }
    public static async Task<object> UnityWindow(string label)
    {
        var view = SceneView.lastActiveSceneView;
        if (view != null) { view.Focus(); view.Repaint(); }
        return await CaptureWindow(label, System.Diagnostics.Process.GetCurrentProcess().Id);
    }
    public static object OpenBlender()
    {
        string file = Tmp + "/modeling/fullfilm_v3_scene_block.blend";
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "D:/Software/Blender 4.2/blender.exe", Arguments = "--window-geometry 50 50 1600 1000 \"" + file + "\"",
            UseShellExecute = false, CreateNoWindow = false
        };
        var process = System.Diagnostics.Process.Start(start);
        string json = JsonConvert.SerializeObject(new { success = true, pid = process.Id, file, version = "4.2.0", software = "Blender" }, Formatting.Indented);
        Directory.CreateDirectory(Tmp + "/software"); File.WriteAllText(Tmp + "/software/blender-session.json", json); return json;
    }
    public static object OpenBlenderMode(string mode)
    {
        if (mode != "wire" && mode != "material") throw new Exception("Mode must be wire or material");
        string folder = Tmp + "/software"; Directory.CreateDirectory(folder);
        string setup = "import bpy\nfrom mathutils import Quaternion\ndef prepare():\n    meshes=[o for o in bpy.data.objects if o.type=='MESH']\n    for o in bpy.data.objects:\n        o.select_set(False)\n        if o.name.startswith('SOURCE_PREVIEW_'): o.hide_set(True)\n    for a in bpy.context.screen.areas:\n        if a.type=='VIEW_3D':\n            s=a.spaces.active\n            s.shading.type='SOLID'\n            s.shading.color_type='MATERIAL'\n            s.overlay.show_overlays=True\n            s.overlay.show_extras=False\n            s.region_3d.view_distance=10.5\n            s.region_3d.view_location=(0,0,1.0)\n            s.region_3d.view_rotation=Quaternion((0.866,0.38,-0.10,-0.30)).normalized()\n            s.region_3d.view_perspective='PERSP'\n    if meshes:\n        bpy.context.view_layer.objects.active=meshes[0]\n";
        if (mode == "wire") setup += "        for o in meshes: o.select_set(True)\n        bpy.ops.object.mode_set(mode='EDIT')\n";
        setup += "    return None\nbpy.app.timers.register(prepare,first_interval=1.0)\n";
        string script = folder + "/blender-" + mode + "-setup.py"; File.WriteAllText(script, setup);
        string file = Tmp + "/modeling/fullfilm_v3_scene_block.blend";
        var start = new System.Diagnostics.ProcessStartInfo { FileName = "D:/Software/Blender 4.2/blender.exe", Arguments = "\"" + file + "\" --python \"" + script + "\"", UseShellExecute = false, CreateNoWindow = false };
        var process = System.Diagnostics.Process.Start(start);
        string json = JsonConvert.SerializeObject(new { success = true, pid = process.Id, file, mode, geometryEdited = false, sourceSaved = false }, Formatting.Indented); File.WriteAllText(folder + "/blender-" + mode + "-session.json", json); return json;
    }
    public static async Task<object> PlayStages()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Actual Play Mode required");
        var run=UnityEngine.Object.FindObjectOfType<OldWebMap.OWV3Presentation>(true);var director=UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(run.director);var rows=new List<object>();
        float[] times={213f,220f,239f};string[] labels={"unity-play-miniature-actual","unity-play-expansion-actual","unity-play-world-actual"};
        try
        {
            for(int i=0;i<times.Length;i++)
            {
                director.SetProgramVariable("_offset",times[i]);director.SetProgramVariable("_anchor",VRC.SDKBase.Networking.GetServerTimeInSeconds()+30d);director.SetProgramVariable("_playing",true);director.SetProgramVariable("_revision",Convert.ToInt32(director.GetProgramVariable("_revision"))+1);director.SendCustomEvent("_onDeserialization");await Task.Delay(400);
                Selection.activeGameObject=(times[i]<217.6f?run.workspace:run.finalWorld).gameObject;
                var type=typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");if(type!=null){var inspector=EditorWindow.GetWindow(type);inspector.Show();}
                var view=SceneView.lastActiveSceneView;if(view!=null){view.showGrid=false;view.drawGizmos=false;view.LookAt(new Vector3(0,1.3f,2),Quaternion.Euler(17,-24,0),times[i]<217.6f?7:9);view.Focus();view.Repaint();}await Task.Delay(250);
                rows.Add(JsonConvert.DeserializeObject((string)await CaptureWindow(labels[i],System.Diagnostics.Process.GetCurrentProcess().Id)));
            }
            return new{success=true,rows,scope="Actual V3 ClientSim and original Windows Unity window; future test anchor holds each visual instant"};
        }
        finally{director.SendCustomEvent("StopAtStart");await Task.Delay(100);}
    }
}
