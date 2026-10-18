using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // One absolute clock for the inherited V5 signal layer and its full-film continuation.
    // Assets are persisted by OWFullSignalBuild. No audio is played by this component.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWFilmV5Signal : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWV4Oscilloscope oscilloscope;
        public OWV5Tracker tracker;
        public OWV5Greetz greetz;
        public OWV5Atmosphere atmosphere;
        public Transform worldFrame;
        public Transform movingStarsRoot;
        public Renderer farStarsRenderer;
        public Renderer midStarsRenderer;
        public Renderer nearStarsRenderer;
        public Renderer gridRenderer;
        public Transform[] continuationWaves;
        public Renderer[] continuationRenderers;
        public Renderer continuationGreetzRenderer;
        public Material continuationGreetzMaterial;
        public float[] journeyStarts;
        public float[] journeyEnds;
        public float[] journeyDistances;
        public float[] basePhosphorIntensities;
        public float overloadFirst;
        public float overloadLast;
        public float overloadMin;
        public float overloadMax;
        public float cursorPx;
        public float cursorMin;
        public float cursorMax;
        public float renewalFirst;
        public float renewalLast;
        public float renewalMin;
        public float renewalMax;
        public float duration = 256f;
        public bool autoSample;
        public float sampledTime;
        public float sampledMotionTime;
        public float sampledPressure;
        public float sampledRetreat;
        public float sampledStarPresence;
        public float sampledTrackerOpacity;
        public float sampledGreetzOpacity;
        public float sampledGreetzScroll;
        public float maxVoiceAnchorError;

        private void LateUpdate()
        {
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        public void Seek(float time)
        {
            if (oscilloscope != null) oscilloscope.Seek(time);
            Sample(time);
        }

        public void Waiting() { Sample(0f); }
        public void Finished() { Sample(duration); }

        public void Sample(float time)
        {
            time = Mathf.Clamp(time, 0f, duration);
            sampledTime = time;
            sampledMotionTime = MotionClock(time);
            sampledPressure = Pressure(time);
            sampledRetreat = Retreat(time);
            bool performing = time > .0001f && time < duration - .0001f;
            bool continuation = time >= 133.12f && performing;

            // These copies are detached from the hidden historical scenery. Their
            // fixed physical parent follows the old frame only for its star layer.
            if (movingStarsRoot != null && worldFrame != null)
                movingStarsRoot.SetPositionAndRotation(worldFrame.position, worldFrame.rotation);
            if (atmosphere != null) atmosphere.Sample(time);
            SamplePresence(time, performing);
            if (time >= duration && atmosphere != null && atmosphere.skyTop != null && atmosphere.skyTop.Length > 0)
            {
                // The completed shared workspace retains its last gentle daylight.
                int last = atmosphere.skyTop.Length - 1;
                Color horizon = atmosphere.skyHorizon[last];
                if (atmosphere.skyMaterial != null)
                {
                    atmosphere.skyMaterial.SetColor("_TopColor", atmosphere.skyTop[last]);
                    atmosphere.skyMaterial.SetColor("_HorizonColor", horizon);
                    atmosphere.skyMaterial.SetColor("_GroundColor", new Color(horizon.r * atmosphere.groundDarken, horizon.g * atmosphere.groundDarken, horizon.b * atmosphere.groundDarken, 1f));
                }
                if (atmosphere.keyLight != null) { atmosphere.keyLight.color = atmosphere.keyColors[last]; atmosphere.keyLight.intensity = atmosphere.keyIntensities[last] * .75f; }
                if (atmosphere.fillLight != null) { atmosphere.fillLight.color = atmosphere.fillColors[last]; atmosphere.fillLight.intensity = atmosphere.fillIntensities[last] * .75f; }
            }

            if (oscilloscope != null)
            {
                oscilloscope.Sample(time);
                SampleWaves(time, continuation, performing);
            }
            if (tracker != null)
            {
                tracker.Sample(time);
                sampledTrackerOpacity = TrackerOpacity(time);
                tracker.sampledOpacity = sampledTrackerOpacity;
                if (tracker.materials != null) for (int i = 0; i < tracker.materials.Length; i++)
                    if (tracker.materials[i] != null) tracker.materials[i].SetFloat("_Opacity", sampledTrackerOpacity);
                if (tracker.renderers != null) for (int i = 0; i < tracker.renderers.Length; i++)
                    if (tracker.renderers[i] != null) tracker.renderers[i].enabled = sampledTrackerOpacity > .002f;
            }
            SampleGreetz(time, performing);
        }

        private void SampleWaves(float time, bool continuation, bool performing)
        {
            int frame = Mathf.Clamp(Mathf.FloorToInt(time * oscilloscope.frameRate), 0, oscilloscope.frameCount - 1);
            // Modest local attenuation preserves V5 phosphor and the musical hierarchy.
            float busy = Ease((time - 51.20f) / 2.56f) * (1f - Ease((time - 79.36f) / 2.56f));
            float release = Ease((time - 81.92f) / 1.28f) * (1f - Ease((time - 92.16f) / 1.28f));
            float editorialGain = 1f - .22f * busy - .16f * release;
            float outro = 1f - Ease((time - duration + .8f) / .8f);
            for (int ch = 0; ch < 4; ch++)
            {
                float energy = Energy(ch, frame);
                float nearPressure = sampledPressure * (ch >= 2 ? 1f : 0f);
                if (oscilloscope.waveMaterials != null && ch < oscilloscope.waveMaterials.Length)
                {
                    Material mat = oscilloscope.waveMaterials[ch];
                    if (mat != null)
                    {
                        // Activity is measured from real AC RMS. An empty tracker
                        // row never extinguishes a sustained waveform or meter.
                        float activity = continuation ? Ease(energy / .015f) : 1f;
                        float gain = oscilloscope.masterGain * editorialGain * outro * Ease(time / .4f);
                        float finale = FinaleOpen(time);
                        if (continuation) gain *= Mathf.Lerp(.42f + finale * .20f, .18f, sampledRetreat) * Mathf.Lerp(1f, .72f, nearPressure) * activity;
                        mat.SetFloat("_Gain", gain);
                        mat.SetFloat("_Timeline", time);
                        mat.SetFloat("_Row", 1f - (frame + .5f) / oscilloscope.atlasHeight);
                        mat.SetFloat("_Energy", energy);
                        mat.SetFloat("_NearFadeStart", continuation ? 1.1f : 2.5f);
                        mat.SetFloat("_NearFadeEnd", continuation ? 2.5f : 5f);
                        if (basePhosphorIntensities != null && ch < basePhosphorIntensities.Length)
                            mat.SetFloat("_Intensity", basePhosphorIntensities[ch]);
                        if (continuation)
                        {
                            // Short, quiet traces leave the work itself readable;
                            // the two nearby pressure voices turn into side strokes.
                            mat.SetFloat("_Length", Mathf.Lerp(4.6f - finale * 1.7f, .86f, nearPressure));
                            mat.SetFloat("_Amplitude", Mathf.Lerp(.80f + .025f * ch, .48f + .02f * (ch - 2), nearPressure));
                            mat.SetFloat("_Constrain", Mathf.Lerp(.65f, .12f, Ease((time - 197.12f) / 17.92f)));
                            mat.SetFloat("_Bend", Mathf.Sin(time * .31f + ch) * Mathf.Lerp(.10f, .035f, nearPressure));
                            mat.SetFloat("_Curl", Mathf.Sin(time * .27f + ch) * Mathf.Lerp(.12f + finale * .35f, .045f, nearPressure));
                            mat.SetFloat("_Twist", Mathf.Sin(time * .23f + ch) * Mathf.Lerp(.08f, .025f, nearPressure));
                        }
                    }
                }
                if (continuation)
                {
                    Vector3 physical = SourceWorldPosition(time, ch);
                    if (continuationWaves != null && ch < continuationWaves.Length && continuationWaves[ch] != null)
                    {
                        continuationWaves[ch].position = physical;
                        float roll = Mathf.Lerp((ch - 1.5f) * 4f, ch == 2 ? 75f : -75f, nearPressure);
                        continuationWaves[ch].rotation = Quaternion.Euler(Mathf.Sin(time * .2f + ch) * 3f,
                            Mathf.Sin(time * .17f + ch) * 7f, roll);
                    }
                    if (oscilloscope.music != null && oscilloscope.music.sources != null && ch < oscilloscope.music.sources.Length)
                    {
                        AudioSource source = oscilloscope.music.sources[ch];
                        if (source != null)
                        {
                            source.transform.position = physical;
                            source.spatialBlend = Mathf.Lerp(.55f, 1f, sampledPressure * (ch >= 2 ? 1f : 0f));
                            source.spatialBlend = Mathf.Lerp(source.spatialBlend, 1f, FinaleOpen(time));
                            source.spatialBlend = Mathf.Lerp(source.spatialBlend, .85f, sampledRetreat);
                            source.dopplerLevel = 0f;
                            if (continuationWaves != null && ch < continuationWaves.Length && continuationWaves[ch] != null)
                                maxVoiceAnchorError = Mathf.Max(maxVoiceAnchorError, Vector3.Distance(source.transform.position, continuationWaves[ch].position));
                        }
                    }
                }
                if (continuationRenderers != null && ch < continuationRenderers.Length && continuationRenderers[ch] != null)
                    continuationRenderers[ch].enabled = continuation && outro > .001f;
            }
            // The legacy regional meshes remain exactly as authored for 0..133.12.
            // Their detached successors survive the historical world's withdrawal.
            if (time >= 133.12f || !performing)
            {
                if (oscilloscope.regionalRenderers != null) for (int i = 0; i < oscilloscope.regionalRenderers.Length; i++)
                    if (oscilloscope.regionalRenderers[i] != null) oscilloscope.regionalRenderers[i].enabled = false;
                if (oscilloscope.particles != null) for (int i = 0; i < oscilloscope.particles.Length; i++)
                    if (oscilloscope.particles[i] != null) oscilloscope.particles[i].gameObject.SetActive(false);
            }
            else if (oscilloscope.particles != null) for (int i = 0; i < oscilloscope.particles.Length; i++)
                if (oscilloscope.particles[i] != null) oscilloscope.particles[i].gameObject.SetActive(true);
            // One physical quartet only. Incoming/outgoing regional scenery does
            // not carry additional, prematurely visible copies of this signal.
            if (time < 133.12f && performing && oscilloscope.regionalRenderers != null)
            {
                int activeRegion = oscilloscope.journey == null ? 0 : oscilloscope.journey.Region(time);
                for (int i = 0; i < oscilloscope.regionalRenderers.Length; i++)
                    if (oscilloscope.regionalRenderers[i] != null)
                        oscilloscope.regionalRenderers[i].enabled = i / 4 == activeRegion;
            }
        }

        // Musical time advances everywhere; spatial time has exact plateaux for all journeys.
        public float MotionClock(float time)
        {
            time = Mathf.Clamp(time, 0f, duration);
            float paused = 0f;
            if (journeyStarts != null && journeyEnds != null)
                for (int i = 0; i < journeyStarts.Length; i++)
                {
                    if (time < journeyStarts[i]) break;
                    if (time <= journeyEnds[i]) return journeyStarts[i] - paused;
                    paused += journeyEnds[i] - journeyStarts[i];
                }
            return time - paused;
        }

        public Vector3 SourceWorldPosition(float time, int ch)
        {
            if (oscilloscope == null) return Vector3.zero;
            if (time < 133.12f) return oscilloscope.SourceWorldPosition(time, ch);
            float m = MotionClock(time), phase = ch * 1.57079633f;
            Vector3 front = oscilloscope.fusedCenter + new Vector3((-1.8f + ch * 1.2f) + Mathf.Sin(m * .31f + phase) * .07f,
                (ch - 1.5f) * .13f + Mathf.Sin(m * .53f + phase) * .11f, (ch - 1.5f) * .13f);
            float reborn = Ease((time - 207.36f) / 7.68f);
            front += new Vector3(0, -.15f * reborn, -1.0f * reborn);
            Vector3 near = new Vector3(ch == 2 ? -1.45f : 1.45f, 1.75f + .10f * Mathf.Sin(m * .4f + phase), -.70f);
            if (ch >= 2) front = Vector3.Lerp(front, near, Pressure(time));
            float orbit = phase + (m - MotionClock(235.52f)) * .43f;
            Vector3 expanded = new Vector3(Mathf.Sin(orbit) * 4.1f, 1.8f + .42f * Mathf.Sin(orbit * .7f + ch), -2.8f + Mathf.Cos(orbit) * 4.1f);
            front = Vector3.Lerp(front, expanded, FinaleOpen(time));
            Vector3 far = new Vector3((ch - 1.5f) * .45f, 1.95f + (ch - 1.5f) * .10f, 16.2f + (ch - 1.5f) * .15f);
            return Vector3.Lerp(front, far, Retreat(time));
        }

        public float Pressure(float time)
        {
            return Ease((time - 143.36f) / 2.56f) * (1f - Ease((time - 153.60f) / 2.56f));
        }

        public float FinaleOpen(float time)
        {
            return Ease((time - 235.52f) / 2.56f) * (1f - Ease((time - 246.40f) / 4.48f));
        }

        public float Retreat(float time)
        {
            return Ease((time - 186.88f) / 2.56f) * (1f - Ease((time - 194.56f) / 12.80f));
        }

        public float TrackerOpacity(float time)
        {
            if (tracker == null) return 0f;
            if (time < 133.12f) return tracker.Opacity(time);
            float level = .60f;
            level = Mathf.Lerp(level, .26f, Ease((time - 133.12f) / 1.28f));
            level = Mathf.Lerp(level, .38f, Ease((time - 153.60f) / 1.28f));
            // The void keeps the actual PCM alive, while the large side panels
            // withdraw completely so only the saved light and distant traces remain.
            level = Mathf.Lerp(level, 0f, Ease((time - 184.32f) / 2.56f));
            level = Mathf.Lerp(level, .42f, Ease((time - 197.12f) / 2.56f));
            level = Mathf.Lerp(level, .30f, Ease((time - 207.36f) / 1.28f));
            level = Mathf.Lerp(level, .68f, Ease((time - 235.52f) / 1.28f));
            level = Mathf.Lerp(level, .36f, Ease((time - 245.76f) / 1.28f));
            return level * (1f - Ease((time - duration + .8f) / .8f));
        }

        private void SamplePresence(float time, bool performing)
        {
            float presence = 1f;
            if (time >= 133.12f)
            {
                presence = 1f - .98f * Ease((time - 168.96f) / 17.92f);
                presence = Mathf.Lerp(presence, 1f, Ease((time - 197.12f) / 28.16f));
            }
            sampledStarPresence = presence;
            if (atmosphere == null) return;
            if (performing && time >= 133.12f)
            {
                if (atmosphere.farStarsMaterial != null) atmosphere.farStarsMaterial.SetFloat("_Intensity", atmosphere.farShowing * presence);
                if (atmosphere.midStarsMaterial != null) atmosphere.midStarsMaterial.SetFloat("_Intensity", atmosphere.midShowing * presence);
                if (atmosphere.nearStarsMaterial != null) atmosphere.nearStarsMaterial.SetFloat("_Intensity", atmosphere.nearShowing * presence);
            }
            // At the void, remove the geometry's visible layers as well as dimming its colour.
            bool stars = !performing || time < 133.12f || presence > .025f;
            if (farStarsRenderer != null) farStarsRenderer.enabled = stars;
            if (midStarsRenderer != null) midStarsRenderer.enabled = stars;
            if (nearStarsRenderer != null) nearStarsRenderer.enabled = stars;
            if (gridRenderer != null) gridRenderer.enabled = !performing || time < 133.12f || time < 186.88f || time > 197.12f;
        }

        private void SampleGreetz(float time, bool performing)
        {
            if (greetz != null)
            {
                greetz.Sample(time);
                if (time >= 133.12f || !performing)
                {
                    float oldOpacity = performing ? 1f - Ease((time - 133.12f) / 1.28f) : 0f;
                    greetz.sampledOpacity = oldOpacity;
                    if (greetz.ringMaterial != null) greetz.ringMaterial.SetFloat("_Opacity", oldOpacity);
                    if (greetz.ringRenderer != null) greetz.ringRenderer.enabled = oldOpacity > .002f;
                }
            }
            float scroll = overloadFirst, opacity = 0f, drop = 0f, cursorOnly = 0f;
            float spanMin = overloadMin, spanMax = overloadMax;
            float clock = time;
            if (performing && time >= 133.12f && time < 197.12f)
            {
                // Integrating (1-u)^2 gives a smooth slowdown whose derivative is zero at 184.32.
                float u = Mathf.Clamp01((time - 133.12f) / 51.20f);
                float progress = 1f - (1f - u) * (1f - u) * (1f - u);
                scroll = Mathf.Lerp(overloadFirst, overloadLast, progress);
                drop = Ease((time - 133.12f) / 30.72f);
                opacity = Ease((time - 133.12f) / 1.28f) * (1f - Ease((time - 184.32f) / 2.56f)) * .72f;
                clock = Mathf.Min(time, 184.32f);
            }
            else if (performing && time >= 197.12f && time < 207.36f)
            {
                scroll = cursorPx; cursorOnly = 1f;
                spanMin = cursorMin; spanMax = cursorMax;
                opacity = Ease((time - 197.12f) / 1.28f) * (.55f + .45f * Mathf.Sin(time * 3.14159265f) * Mathf.Sin(time * 3.14159265f));
            }
            else if (performing && time >= 207.36f)
            {
                scroll = Mathf.Lerp(renewalFirst, renewalLast, Mathf.Clamp01((time - 207.36f) / 43.52f));
                spanMin = renewalMin; spanMax = renewalMax;
                opacity = Ease((time - 207.36f) / 1.28f) * (1f - Ease((time - 254.72f) / 1.28f)) * .74f;
            }
            sampledGreetzOpacity = opacity; sampledGreetzScroll = scroll;
            if (continuationGreetzRenderer != null) continuationGreetzRenderer.enabled = opacity > .002f;
            if (continuationGreetzMaterial != null)
            {
                continuationGreetzMaterial.SetFloat("_Scroll", scroll);
                continuationGreetzMaterial.SetFloat("_Timeline", clock);
                continuationGreetzMaterial.SetFloat("_Opacity", opacity);
                continuationGreetzMaterial.SetFloat("_Drop", drop);
                continuationGreetzMaterial.SetFloat("_CursorOnly", cursorOnly);
                continuationGreetzMaterial.SetVector("_VisibleSpan", new Vector4(spanMin, spanMax, 0f, 0f));
            }
        }

        private float Energy(int ch, int frame)
        {
            if (ch == 0) return oscilloscope.energy0 != null && frame < oscilloscope.energy0.Length ? oscilloscope.energy0[frame] : 0f;
            if (ch == 1) return oscilloscope.energy1 != null && frame < oscilloscope.energy1.Length ? oscilloscope.energy1[frame] : 0f;
            if (ch == 2) return oscilloscope.energy2 != null && frame < oscilloscope.energy2.Length ? oscilloscope.energy2[frame] : 0f;
            return oscilloscope.energy3 != null && frame < oscilloscope.energy3.Length ? oscilloscope.energy3[frame] : 0f;
        }
        private float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
    }
}
