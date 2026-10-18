using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // Drives the two tracker side panels (CH1|CH2 left, CH3|CH4 right). Materials, panel quads and the
    // baked note-on tables are authored by OWEpisodeV5Signal. Everything is a pure function of the
    // absolute performance time; no allocation, no string building.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV5Tracker : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWV4Oscilloscope oscilloscope;
        public Material[] materials;
        public Renderer[] renderers;
        public float[] noteTimes0;
        public float[] noteTimes1;
        public float[] noteTimes2;
        public float[] noteTimes3;
        public float rowSeconds = .08f;
        public float flashDecay = .14f;
        public float duration = 133.12f;
        public float levelGain = 1f;
        public bool autoSample = true;
        public float sampledTime;
        public float sampledRow;
        public float sampledOpacity;
        public Vector4 sampledFlash;
        public Vector4 sampledLevels;

        private void LateUpdate()
        {
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        public void Seek(float time)
        {
            Sample(time);
        }

        public void Sample(float time)
        {
            time = Mathf.Clamp(time, 0f, duration);
            sampledTime = time;
            float row = time / rowSeconds;
            sampledRow = row;
            float opacity = Opacity(time);
            sampledOpacity = opacity;
            sampledFlash = new Vector4(FlashFor(noteTimes0, time), FlashFor(noteTimes1, time), FlashFor(noteTimes2, time), FlashFor(noteTimes3, time));
            sampledLevels = Levels(time);
            bool visible = opacity > .002f;
            if (materials == null) return;
            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;
                mat.SetFloat("_RowPosition", row);
                mat.SetVector("_Flash", sampledFlash);
                mat.SetVector("_Levels", sampledLevels);
                mat.SetFloat("_Opacity", opacity);
                mat.SetFloat("_Timeline", time);
            }
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }

        // Full strength for the opening and the VRC climax, quiet behind the content in between.
        // Transitions start at the section boundary and take 1.28 s (smoothstep).
        public float Opacity(float time)
        {
            float level = 1f;
            level = Mathf.Lerp(level, .28f, Ease((time - 30.72f) / 1.28f));
            level = Mathf.Lerp(level, 1f, Ease((time - 79.36f) / 1.28f));
            level = Mathf.Lerp(level, .28f, Ease((time - 92.16f) / 1.28f));
            level = Mathf.Lerp(level, .6f, Ease((time - 122.88f) / 1.28f));
            float fadeIn = Ease(time / .4f);
            float fadeOut = 1f - Ease((time - duration + .8f) / .8f);
            return level * fadeIn * fadeOut;
        }

        // Exponential flash from the latest note-on at or before time (binary search over baked seconds).
        public float FlashFor(float[] times, float time)
        {
            if (times == null || times.Length == 0) return 0f;
            int lo = 0;
            int hi = times.Length - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (times[mid] <= time + .00001f) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            if (found < 0) return 0f;
            float age = Mathf.Max(0f, time - times[found]);
            if (age > flashDecay * 8f) return 0f;
            return Mathf.Exp(-age / flashDecay);
        }

        private Vector4 Levels(float time)
        {
            if (oscilloscope == null) return Vector4.zero;
            int frame = Mathf.FloorToInt(time * oscilloscope.frameRate);
            return new Vector4(LevelOf(oscilloscope.energy0, frame), LevelOf(oscilloscope.energy1, frame), LevelOf(oscilloscope.energy2, frame), LevelOf(oscilloscope.energy3, frame));
        }

        // Peak of the last four 30 Hz RMS frames with a fast fall, like a meter ballistics.
        private float LevelOf(float[] energy, int frame)
        {
            if (energy == null || energy.Length == 0) return 0f;
            float peak = 0f;
            float weight = 1f;
            for (int k = 0; k < 4; k++)
            {
                int at = Mathf.Clamp(frame - k, 0, energy.Length - 1);
                peak = Mathf.Max(peak, energy[at] * weight);
                weight *= .7f;
            }
            return Mathf.Clamp01(peak * levelGain);
        }

        private float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
    }
}
