using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OldWebMap;
using UdonSharpEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

/// <summary>Apply only the reviewed front-cast table in the new full-film scene.</summary>
public static class OWFullFrontTuning
{
    const string ScenePath = "Assets/Scenes/OWM_FullFilm_v1.unity";
    const string DataPath = "AgentScripts/data/fullfilm-front-cast.json";
    const float MoveFraction = .72f;
    sealed class Table
    {
        public float[] times, yaws;
        public Vector3[] positions, gaze;
        public int[] gestures;
    }
    static string Tmp => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmp/vrc-old-web-map/fullfilm-v1/reports");
    static OWV4HubCast Cast()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Stopped full-film scene required; inherited V5 is never modified");
        var scene = SceneManager.GetActiveScene();
        var cast = Object.FindObjectsOfType<OWV4HubCast>(true).Where(c => c.gameObject.scene == scene).SingleOrDefault();
        if (cast == null || cast.actors == null || cast.actors.Length != 4) throw new InvalidOperationException("Exactly one four-slot V5 cast is required");
        return cast;
    }
    static JObject Document()
    {
        if (!File.Exists(DataPath)) throw new InvalidOperationException("Sync the independently reviewed data first: " + DataPath);
        var doc = JObject.Parse(File.ReadAllText(DataPath));
        if ((string)doc["scene"] != ScenePath || Mathf.Abs((float)doc["runtime_move_fraction"] - MoveFraction) > .00001f)
            throw new InvalidOperationException("Scene/runtime contract differs from the reviewed data");
        return doc;
    }
    static Vector3 Vec(JToken token) => new Vector3((float)token[0], (float)token[1], (float)token[2]);
    static float[] Floats(Vector3 p) => new[] { p.x, p.y, p.z };
    static Table Read(JToken row)
    {
        return new Table { times = row["times"].Select(v => (float)v).ToArray(), positions = row["positions"].Select(Vec).ToArray(), yaws = row["yaws"].Select(v => (float)v).ToArray(), gestures = row["gestures"].Select(v => (int)v).ToArray(), gaze = row["gaze"].Select(Vec).ToArray() };
    }
    static Table Read(OWV4HubCast cast) => new Table { times = cast.keyTimes, positions = cast.positions, yaws = cast.yaws, gestures = cast.gestures, gaze = cast.gazePositions };
    static string Difference(Table a, Table b)
    {
        if (a.times == null || a.times.Length != b.times.Length) return "time count";
        int count = b.times.Length * 4;
        if (a.positions == null || a.yaws == null || a.gestures == null || a.gaze == null || a.positions.Length != count || a.yaws.Length != count || a.gestures.Length != count || a.gaze.Length != count) return "slot array length";
        for (int k = 0; k < b.times.Length; k++)
        {
            if (Mathf.Abs(a.times[k] - b.times[k]) > .00001f) return "time " + k;
            for (int s = 0; s < 4; s++)
            {
                int i = k * 4 + s;
                if (Vector3.Distance(a.positions[i], b.positions[i]) > .0001f || Mathf.Abs(Mathf.DeltaAngle(a.yaws[i], b.yaws[i])) > .001f || a.gestures[i] != b.gestures[i] || Vector3.Distance(a.gaze[i], b.gaze[i]) > .0001f)
                    return "time " + b.times[k] + " slot " + s;
            }
        }
        return null;
    }
    static string Report(string filename, object result)
    {
        Directory.CreateDirectory(Tmp); string json = JsonConvert.SerializeObject(result, Formatting.Indented);
        File.WriteAllText(Path.Combine(Tmp, filename), json); return json;
    }
    public static object Apply()
    {
        var cast = Cast(); var doc = Document(); var original = Read(doc["original"]); var tuned = Read(doc["tuned"]); var current = Read(cast);
        bool alreadyApplied = Difference(current, tuned) == null;
        bool reviewedPrevious = doc["previous_tuned"] != null && Difference(current, Read(doc["previous_tuned"])) == null;
        if (!alreadyApplied && !reviewedPrevious && Difference(current, original) != null)
            throw new InvalidOperationException("Scene cast is neither the measured V5 source nor the reviewed tuning; refuse to overwrite: " + Difference(current, original));
        CheckData(doc, original, tuned);
        Undo.RecordObject(cast, "Smooth the reviewed full-film front cast");
        cast.keyTimes = (float[])tuned.times.Clone(); cast.positions = (Vector3[])tuned.positions.Clone(); cast.yaws = (float[])tuned.yaws.Clone();
        cast.gestures = (int[])tuned.gestures.Clone(); cast.gazePositions = (Vector3[])tuned.gaze.Clone();
        UdonSharpEditorUtility.CopyProxyToUdon(cast); EditorUtility.SetDirty(cast);
        var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(cast); if (vm != null) EditorUtility.SetDirty(vm);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return Report("front-cast-apply.json", new { applied = true, alreadyApplied, upgradedReviewedPrevious = reviewedPrevious && !alreadyApplied, scene = ScenePath, sourceSha256 = (string)doc["source_sha256"], keys = tuned.times.Length, editedStances = ((JArray)doc["edits"]).Count, newSharedKeys = 0, sceneSaved = false });
    }
    static void CheckData(JObject doc, Table original, Table tuned)
    {
        if (!original.times.SequenceEqual(tuned.times)) throw new InvalidOperationException("Shared keys were changed");
        if (!original.gestures.SequenceEqual(tuned.gestures)) throw new InvalidOperationException("Authored gesture timing must remain unchanged");
        for (int i = 0; i < original.gaze.Length; i++) if (Vector3.Distance(original.gaze[i], tuned.gaze[i]) > .000001f) throw new InvalidOperationException("An original gaze target changed");
        foreach (var window in doc["protected"])
        {
            int slot = (int)window["slot"]; float start = (float)window["from"], end = (float)window["to"];
            var keys = new HashSet<int>();
            for (int k = 0; k < original.times.Length - 1; k++) if (original.times[k] < end && original.times[k + 1] > start) { keys.Add(k); keys.Add(k + 1); }
            foreach (int k in keys)
            {
                int at = k * 4 + slot;
                if (Vector3.Distance(original.positions[at], tuned.positions[at]) > .000001f || Mathf.Abs(Mathf.DeltaAngle(original.yaws[at], tuned.yaws[at])) > .00001f || original.gestures[at] != tuned.gestures[at] || Vector3.Distance(original.gaze[at], tuned.gaze[at]) > .000001f)
                    throw new InvalidOperationException("Protected full interpolation span changed at " + original.times[k] + " slot " + slot);
            }
        }
    }
    static Vector3 Sample(Table table, float time, int slot, bool bob = false)
    {
        int key = 0; for (int k = 0; k < table.times.Length - 1; k++) if (time >= table.times[k]) key = k;
        int next = Mathf.Min(key + 1, table.times.Length - 1); float span = Mathf.Max(.01f, table.times[next] - table.times[key]);
        float part = Mathf.Clamp01((time - table.times[key]) / span); float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(part / MoveFraction));
        Vector3 delta = table.positions[next * 4 + slot] - table.positions[key * 4 + slot];
        Vector3 p = Vector3.Lerp(table.positions[key * 4 + slot], table.positions[next * 4 + slot], move);
        if (bob && delta.sqrMagnitude > .06f && part < MoveFraction) p.y += Mathf.Sin(part * span * 9f) * .009f;
        return p;
    }
    static double InverseSmooth(double u)
    {
        double lo = 0, hi = 1;
        for (int n = 0; n < 48; n++) { double q = (lo + hi) * .5; if (q * q * (3 - 2 * q) < u) lo = q; else hi = q; }
        return (lo + hi) * .5;
    }
    public static object Validate()
    {
        var cast = Cast(); var doc = Document(); var original = Read(doc["original"]); var tuned = Read(doc["tuned"]);
        CheckData(doc, original, tuned); var failures = new List<string>(); string diff = Difference(Read(cast), tuned);
        if (diff != null) failures.Add("Scene table differs from reviewed tuning: " + diff);
        float speedLimit = (float)doc["limits"]["analytic_peak_speed_mps"], separationLimit = (float)doc["limits"]["horizontal_separation_m"];
        float maxPeak = 0f, oldPeak = 0f, minSeparation = 100f, sampledMinimum = 100f, minimumEye = 100f;
        object fastest = null, closest = null; var intervals = new List<object>();
        for (int k = 0; k < tuned.times.Length - 1; k++)
        {
            float start = tuned.times[k], end = tuned.times[k + 1], duration = end - start;
            for (int slot = 0; slot < 4; slot++)
            {
                int a = k * 4 + slot, b = (k + 1) * 4 + slot;
                float peak = 1.5f * Vector3.Distance(tuned.positions[a], tuned.positions[b]) / (MoveFraction * duration);
                float before = 1.5f * Vector3.Distance(original.positions[a], original.positions[b]) / (MoveFraction * duration);
                oldPeak = Mathf.Max(oldPeak, before);
                if (peak > maxPeak) { maxPeak = peak; fastest = new { slot, start, end, peak, from = Floats(tuned.positions[a]), to = Floats(tuned.positions[b]) }; }
                if (peak > speedLimit + .00001f) failures.Add("Analytic speed " + peak + " at " + start + "–" + end + " slot " + slot);
                if (Mathf.Abs(peak - before) > .00001f) intervals.Add(new { slot, start, end, previousPeak = before, tunedPeak = peak });
            }
            // All four slots share exactly the same scalar smoothstep in an interval.
            // Their horizontal relative path is r(u)=r0+u*dr, so its closest point is
            // analytic; dense time sampling alone cannot miss an intervening crossing.
            for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++)
            {
                Vector3 r0 = tuned.positions[k * 4 + a] - tuned.positions[k * 4 + b]; r0.y = 0f;
                Vector3 r1 = tuned.positions[(k + 1) * 4 + a] - tuned.positions[(k + 1) * 4 + b]; r1.y = 0f;
                Vector3 delta = r1 - r0; float u = delta.sqrMagnitude > .000000001f ? Mathf.Clamp01(-Vector3.Dot(r0, delta) / delta.sqrMagnitude) : 0f;
                float distance = (r0 + u * delta).magnitude;
                if (distance < minSeparation) { minSeparation = distance; closest = new { slotA = a, slotB = b, start, end, distance, interpolation = u, time = start + MoveFraction * duration * InverseSmooth(u) }; }
                if (distance < separationLimit - .00001f) failures.Add("Body clearance " + distance + " at " + start + "–" + end + " slots " + a + "/" + b);
            }
        }
        int bandHits = 0, campfireHits = 0; float step = (float)doc["limits"]["sample_step_seconds"];
        for (int n = 0; n <= Mathf.RoundToInt(133.12f / step); n++)
        {
            float time = Mathf.Min(n * step, 133.12f); var points = new Vector3[4];
            for (int s = 0; s < 4; s++)
            {
                points[s] = Sample(tuned, time, s); minimumEye = Mathf.Min(minimumEye, Vector2.Distance(new Vector2(points[s].x, points[s].z), new Vector2(0, -2.8f)));
                if (Mathf.Abs(points[s].x) < 1.92f && points[s].z > .38f && points[s].z < 1.62f && points[s].y < .6f) bandHits++;
                if (time >= 88.32f && time < 102.40f && Vector2.Distance(new Vector2(points[s].x, points[s].z), new Vector2(0, 4.55f)) < 1.25f) campfireHits++;
                if (s == 2 && time >= 102.40f && time < 117.76f && points[s].z >= 5.6f) failures.Add("Slot2 crossed the first closed gate at " + time);
                if (s == 2 && time >= 112.64f && time < 120.32f && points[s].z >= 9.6f) failures.Add("Slot2 crossed the second closed gate at " + time);
            }
            for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++) sampledMinimum = Mathf.Min(sampledMinimum, Vector2.Distance(new Vector2(points[a].x, points[a].z), new Vector2(points[b].x, points[b].z)));
        }
        if (bandHits > 0) failures.Add("Central keyline band samples: " + bandHits);
        if (campfireHits > 0) failures.Add("Existing campfire keep-out samples: " + campfireHits);
        if (minimumEye < (float)doc["limits"]["eye_clearance_m"]) failures.Add("Eye clearance " + minimumEye);
        // Real cast.Sample probes verify that the existing 72% runtime executes
        // these persisted arrays, even while the parent presentation is hidden.
        float previousTime = cast.sampledTime, actualError = 0f; var actualRows = new List<object>();
        try
        {
            foreach (float time in new[] { 11.72f, 20.8f, 23.2f, 33.2f, 53.1f, 63.9f, 76.8f, 80.26f, 85.12f, 89.5f, 120.8f, 124.7f, 128.5f, 131.5f })
            {
                cast.Sample(time); var positions = new List<float[]>();
                for (int s = 0; s < 4; s++)
                {
                    Vector3 actual = cast.actors[s].transform.localPosition - cast.ActorRegionOffset(time), expected = Sample(tuned, time, s, true);
                    actualError = Mathf.Max(actualError, Vector3.Distance(actual, expected)); positions.Add(Floats(actual));
                }
                actualRows.Add(new { time, positions });
            }
        }
        finally { cast.Sample(previousTime); }
        if (actualError > .001f) failures.Add("Existing runtime differs from the table sampler: " + actualError);
        var result = new { success = failures.Count == 0, failures, scene = ScenePath, keys = tuned.times.Length, newSharedKeys = 0, editedStances = ((JArray)doc["edits"]).Count, oldMaxAnalyticSpeed = oldPeak, maxAnalyticSpeed = maxPeak, minAnalyticHorizontalSeparation = minSeparation, denseSampleMinimum = sampledMinimum, minimumEyeDistance = minimumEye, bandHits, campfireHits, fastest, closest, actualRuntimePositionError = actualError, protectedWindowsAndSlot3Exact = true, intervals, actualRows, sceneSaved = false, scope = "Full-table analytic speed and body clearance; dense keyline/gate/campfire checks; actual stopped-editor cast.Sample position probes. Pose/contact/headset visual checks remain separate." };
        string json = Report("front-cast-validate.json", result); if (failures.Count > 0) throw new InvalidOperationException(json); return json;
    }
}
