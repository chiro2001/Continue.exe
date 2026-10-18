using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // Mesh, materials, particles and all references are authored by the V3 builder.
    // This behaviour allocates no sample arrays and creates no runtime scene assets.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV3Oscilloscope : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWEpisodeMusic music;
        public Transform[] waves;
        public Renderer[] waveRenderers;
        public Material[] waveMaterials;
        public ParticleSystem[] particles;
        public Transform[] particleAnchors;
        public float[] energy0;
        public float[] energy1;
        public float[] energy2;
        public float[] energy3;
        public Vector3 fusedCenter = new Vector3(0, 2.55f, 5.8f);
        public Vector3 orbitCenter = new Vector3(0, 1.65f, -1.4f);
        public float orbitRadius = 4.2f;
        public float frameRate = 30f;
        public int frameCount = 3995;
        public int atlasHeight = 4096;
        public float duration = 133.12f;
        public float masterGain = 1f;
        public bool autoSample = true;
        public float sampledTime;
        public float sampledOpen;
        public int sampledFrame;
        public int particleReseeks;
        public float maxVoiceAnchorError;
        private int _materialFrame = -1;
        private int _particleFrame = -1;
        private float _lastTime = -1f;

        private void Start()
        {
            _materialFrame = -1; _particleFrame = -1; _lastTime = -1f;
            if (music != null) music.externalVoicePositionDriver = true;
        }

        private void LateUpdate()
        {
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        // Deterministic public offline/late-join entry. Reset forces simulation from
        // absolute zero; steady playback advances authored particles in fixed ticks.
        public void Seek(float time)
        {
            _particleFrame = -1;
            _materialFrame = -1;
            Sample(time);
        }

        public void Sample(float time)
        {
            if (waves == null || waves.Length != 4 || waveMaterials == null || waveMaterials.Length != 4) return;
            time = Mathf.Clamp(time, 0, duration);
            sampledTime = time;
            int frame = Mathf.Clamp(Mathf.FloorToInt(time * frameRate), 0, frameCount - 1);
            sampledFrame = frame;
            float open = Ease((time - 81.92f) / 2.56f) * (1f - Ease((time - 89.6f) / 2.56f));
            sampledOpen = open;
            float create = Ease((time - 30.72f) / 8f);
            float constrain = Ease((time - 102.4f) / 10.24f);
            float fade = 1f - Ease((time - duration + .8f) / .8f);
            bool visible = fade > .001f;
            for (int ch = 0; ch < 4; ch++)
            {
                float energy = Energy(ch, frame);
                float phase = ch * 1.57079633f;
                // Common front centre, four distinct braided voices. Geometry is
                // never attached to the listener; looking and walking remain free.
                Vector3 front = fusedCenter + new Vector3(Mathf.Sin(time * .31f + phase) * .22f,
                    (ch - 1.5f) * .13f + Mathf.Sin(time * .53f + phase) * .11f,
                    (ch - 1.5f) * .13f);
                float angle = phase + (time - 84.48f) * .46f;
                Vector3 surround = orbitCenter + new Vector3(Mathf.Sin(angle) * orbitRadius,
                    .38f + Mathf.Sin(angle * 1.7f + phase) * .4f,
                    Mathf.Cos(angle) * orbitRadius);
                Vector3 position = Vector3.Lerp(front, surround, open);
                waves[ch].position = position;
                float frontRoll = (ch - 1.5f) * 6f + Mathf.Sin(time * .26f + phase) * (3f + create * 6f);
                Quaternion normal = Quaternion.Euler(Mathf.Sin(time * .31f + phase) * 4f,
                    Mathf.Sin(time * .19f + phase) * 11f, frontRoll);
                Quaternion expanded = Quaternion.Euler(Mathf.Sin(time * .6f + phase) * 19f,
                    angle * Mathf.Rad2Deg, Mathf.Sin(time * .47f + phase) * 21f);
                waves[ch].rotation = Quaternion.Slerp(normal, expanded, open);
                if (particleAnchors != null && ch < particleAnchors.Length && particleAnchors[ch] != null)
                    particleAnchors[ch].SetPositionAndRotation(position, waves[ch].rotation);
                if (music != null && music.sources != null && ch < music.sources.Length && music.sources[ch] != null)
                {
                    // Position is exactly shared by waveform and its real stem.
                    // Zero Doppler protects pitch while movement changes localization.
                    music.sources[ch].transform.position = position;
                    music.sources[ch].spatialBlend = Mathf.Lerp(.55f, 1f, open);
                    maxVoiceAnchorError = Mathf.Max(maxVoiceAnchorError, Vector3.Distance(music.sources[ch].transform.position, position));
                }
                if (waveRenderers != null && ch < waveRenderers.Length && waveRenderers[ch] != null)
                    waveRenderers[ch].enabled = visible;
                if (_materialFrame != frame || _lastTime < 0)
                {
                    Material mat = waveMaterials[ch];
                    if (mat != null)
                    {
                        mat.SetFloat("_Row", 1f - (frame + .5f) / atlasHeight);
                        mat.SetFloat("_Timeline", time);
                        mat.SetFloat("_Gain", masterGain * fade * Mathf.Clamp01(time / .4f));
                        mat.SetFloat("_Energy", energy);
                        mat.SetFloat("_Length", Mathf.Lerp(5.8f + create * 1.2f, 2.8f, open) * (1f - constrain * .16f));
                        mat.SetFloat("_Amplitude", (1.6f + .45f * create + .15f * ch) * (1f + .35f * open));
                        mat.SetFloat("_Bend", Mathf.Sin(time * .44f + phase) * (.12f + .35f * create + open * .65f));
                        mat.SetFloat("_Curl", Mathf.Sin(time * .37f + phase) * (.16f + .45f * create + open * .8f));
                        mat.SetFloat("_Twist", Mathf.Sin(time * .33f + phase) * (create * .24f + open * .8f));
                        mat.SetFloat("_Constrain", constrain);
                    }
                }
            }
            _materialFrame = frame;
            SampleParticles(frame, time < _lastTime || time - _lastTime > .35f);
            _lastTime = time;
        }

        private void SampleParticles(int frame, bool discontinuity)
        {
            if (particles == null || frame == _particleFrame) return;
            bool restart = _particleFrame < 0 || frame < _particleFrame || discontinuity;
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem ps = particles[i];
                if (ps == null) continue;
                if (restart)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Simulate(frame / frameRate, true, true, true);
                }
                else
                {
                    // Same absolute tick grid in playback and offline rendering.
                    int steps = frame - _particleFrame;
                    for (int step = 0; step < steps; step++) ps.Simulate(1f / frameRate, true, false, true);
                }
                ps.Pause(true);
            }
            if (restart) particleReseeks++;
            _particleFrame = frame;
        }

        private float Energy(int ch, int frame)
        {
            if (ch == 0) return energy0 != null && frame < energy0.Length ? energy0[frame] : 0;
            if (ch == 1) return energy1 != null && frame < energy1.Length ? energy1[frame] : 0;
            if (ch == 2) return energy2 != null && frame < energy2.Length ? energy2[frame] : 0;
            return energy3 != null && frame < energy3.Length ? energy3[frame] : 0;
        }
        private float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
    }
}
