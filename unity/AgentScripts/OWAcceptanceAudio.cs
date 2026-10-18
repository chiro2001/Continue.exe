using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using OldWebMap;
using UdonSharpEditor;
using Object = UnityEngine.Object;

public static class OWAcceptanceAudio
{
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v1/acceptance");
    static string Name(Transform t) => t == null ? null : t.parent == null ? t.name : Name(t.parent) + "/" + t.name;
    static object[] EditorFlags()
    {
        var rows = new List<object>();
        foreach (var type in new[] { typeof(EditorUtility), typeof(EditorUtility).Assembly.GetType("UnityEditor.AudioUtil"), typeof(EditorUtility).Assembly.GetType("UnityEditorInternal.InternalEditorUtility") }.Where(t => t != null))
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(p => p.PropertyType == typeof(bool) && (p.Name.ToLowerInvariant().Contains("mute") || p.Name.ToLowerInvariant().Contains("audio")) && p.GetIndexParameters().Length == 0))
        {
            try { rows.Add(new { type = type.FullName, property = p.Name, value = p.GetValue(null), writable = p.CanWrite }); }
            catch (Exception e) { rows.Add(new { type = type.FullName, property = p.Name, error = e.GetType().Name }); }
        }
        return rows.ToArray();
    }
    static string Write(string name, object value)
    {
        Directory.CreateDirectory(Tmp); string json = JsonConvert.SerializeObject(value, Formatting.Indented);
        File.WriteAllText(Tmp + "/" + name + ".json", json); return json;
    }
    public static object Inspect()
    {
        var listeners = Object.FindObjectsOfType<AudioListener>(true);
        var sources = Object.FindObjectsOfType<AudioSource>(true);
        var configuration = AudioSettings.GetConfiguration();
        var full = Object.FindObjectsOfType<OWFullPresentation>(true).FirstOrDefault();
        var director = full == null ? null : UdonSharpEditorUtility.GetBackingUdonBehaviour(full.director);
        var audio = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset").FirstOrDefault();
        var settings = new List<object>();
        if (audio != null)
        {
            var so = new SerializedObject(audio); var iterator = so.GetIterator();
            while (iterator.Next(true))
                if (iterator.propertyType == SerializedPropertyType.Boolean) settings.Add(new { name = iterator.propertyPath, value = (object)iterator.boolValue });
                else if (iterator.propertyType == SerializedPropertyType.Float) settings.Add(new { name = iterator.propertyPath, value = (object)iterator.floatValue });
                else if (iterator.propertyType == SerializedPropertyType.Integer) settings.Add(new { name = iterator.propertyPath, value = (object)iterator.intValue });
        }
        var sample = new float[1024]; if (EditorApplication.isPlaying) AudioListener.GetOutputData(sample, 0);
        return Write("unity-audio-inspect", new
        {
            success = true, scene = SceneManager.GetActiveScene().path, dirty = SceneManager.GetActiveScene().isDirty,
            processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
            editorFlags = EditorFlags(), listenerVolume = AudioListener.volume, listenerPaused = AudioListener.pause,
            dspTime = AudioSettings.dspTime, configuration.sampleRate, configuration.dspBufferSize, configuration.speakerMode,
            activeListenerCount = listeners.Count(l => l.enabled && l.gameObject.activeInHierarchy),
            listeners = listeners.Select(l => new { path = Name(l.transform), l.enabled, active = l.gameObject.activeInHierarchy, position = new[] { l.transform.position.x, l.transform.position.y, l.transform.position.z } }).ToArray(),
            sourceCount = sources.Length,
            musicSources = sources.Where(s => s.clip != null && s.clip.name.StartsWith("FC14")).Select(s => new
            {
                path = Name(s.transform), clip = s.clip.name, s.enabled, active = s.gameObject.activeInHierarchy,
                s.isPlaying, s.mute, s.volume, s.timeSamples, s.spatialBlend, s.minDistance, s.maxDistance, s.rolloffMode,
                s.ignoreListenerPause, s.ignoreListenerVolume, mixer = s.outputAudioMixerGroup == null ? null : s.outputAudioMixerGroup.name,
                position = new[] { s.transform.position.x, s.transform.position.y, s.transform.position.z }
            }).ToArray(),
            directorPlaying = director == null ? null : director.GetProgramVariable("_playing"),
            directorOffset = director == null ? null : director.GetProgramVariable("_offset"),
            settings, listenerPeak = sample.Max(v => Mathf.Abs(v)), listenerRms = Math.Sqrt(sample.Select(v => (double)v * v).Average())
        });
    }
    public static object EnableEditorAudio()
    {
        var type = typeof(EditorUtility); var property = type.GetProperty("audioMasterMute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite) throw new Exception("Editor master-mute property unavailable");
        bool before = (bool)property.GetValue(null); property.SetValue(null, false);
        return Write("unity-audio-restored", new { success = !(bool)property.GetValue(null), editorWasMuted = before, editorMuted = (bool)property.GetValue(null) });
    }
    public static async Task<object> PlayProbe()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var full = Object.FindObjectsOfType<OWFullPresentation>(true).First();
        var director = UdonSharpEditorUtility.GetBackingUdonBehaviour(full.director);
        if (!Convert.ToBoolean(director.GetProgramVariable("_playing"))) director.SendCustomEvent("Play");
        await Task.Delay(2500);
        return Inspect();
    }
    public static async Task<object> OutputProbe()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var sources = Object.FindObjectsOfType<AudioSource>(true).Where(s => s.clip != null && s.clip.name.StartsWith("FC14")).ToArray();
        var rows = new List<object>(); var data = new float[1024];
        for (int i = 0; i < 10; i++)
        {
            AudioListener.GetOutputData(data, 0); float listener = data.Max(v => Mathf.Abs(v));
            var peaks = new List<float>(); foreach (var s in sources) { s.GetOutputData(data, 0); peaks.Add(data.Max(v => Mathf.Abs(v))); }
            rows.Add(new { listenerPeak = listener, sourcePeaks = peaks, time = Time.realtimeSinceStartup });
            await Task.Delay(120);
        }
        var result = Write("unity-audio-signal", new { success = true, rows }); Inspect(); return result;
    }
    public static async Task<object> WindowsSessions()
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/powershell.exe"),
            Arguments = "-NoProfile -NonInteractive -Mta -ExecutionPolicy Bypass -File \"" + Tmp + "/unity-sessions.ps1\"",
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        using (var process = System.Diagnostics.Process.Start(start))
        {
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!process.HasExited && timer.Elapsed.TotalSeconds < 30) await Task.Delay(100);
            if (!process.HasExited) { process.Kill(); throw new Exception("Audio-session helper timed out"); }
            string stdout = await output; string stderr = await errors;
            if (process.ExitCode != 0) throw new Exception("Audio-session helper failed: " + stderr);
            var result = Newtonsoft.Json.Linq.JObject.Parse(stdout.Trim('\uFEFF', '\r', '\n', ' '));
            return Write("windows-unity-session-context", new { success = true, unitySessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId, result });
        }
    }
    public static async Task<object> ResetOutput()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var full = Object.FindObjectsOfType<OWFullPresentation>(true).First();
        var director = UdonSharpEditorUtility.GetBackingUdonBehaviour(full.director);
        director.SendCustomEvent("StopAtStart"); await Task.Delay(150);
        var configuration = AudioSettings.GetConfiguration(); double oldDsp = AudioSettings.dspTime;
        bool reset = AudioSettings.Reset(configuration); if (!reset) throw new Exception("Audio output reset failed");
        await Task.Delay(300); director.SendCustomEvent("Play"); await Task.Delay(1500);
        Write("unity-output-reset", new { success = reset, oldDsp, newDsp = AudioSettings.dspTime });
        return await OutputProbe();
    }
}
