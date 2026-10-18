using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // One bounded, fused contribution preserves audibility while the original
    // four physical emitters keep their distance and motion. No new music or
    // note gating is introduced: each layer reads the same original mono PCM.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV2AudioFloor : UdonSharpBehaviour
    {
        public OWEpisodeMusic music;
        public AudioSource[] drySources;
        public float retreatFloor = .30f;
        public float sampledFloor;
        public int synchronizedSchedules;
        public double lastCommonDsp;
        private int _schedule = -1;
        private bool _playing;

        public void Sync(float time, bool playing)
        {
            if (music == null || music.sources == null || music.sources.Length != 4 || drySources == null || drySources.Length != 4) return;
            float retreat = Ease((time - 186.88f) / 2.56f) * (1f - Ease((time - 194.56f) / 12.80f));
            float end = 1f - Ease((time - 255.84f) / .16f);
            sampledFloor = retreatFloor * retreat;
            for (int ch = 0; ch < 4; ch++)
            {
                music.sources[ch].volume = music.baseVolume * (1f - sampledFloor) * end;
                drySources[ch].volume = music.baseVolume * sampledFloor * end;
            }
            if (!playing)
            {
                if (_playing) for (int ch = 0; ch < 4; ch++) drySources[ch].Stop();
                _playing = false; return;
            }
            if (!_playing || _schedule != music.scheduleCount)
            {
                // The legacy public DSP diagnostic is a float. Re-schedule all
                // eight components on this one double DSP target, preventing a
                // rounded timestamp from creating comb filtering in the overlap.
                double dsp = System.Math.Max(AudioSettings.dspTime + .04, (double)music.lastScheduleDsp);
                float position = music.lastScheduledTime + (float)System.Math.Max(0.0, dsp - (double)music.lastScheduleDsp);
                for (int ch = 0; ch < 4; ch++)
                {
                    var main = music.sources[ch]; var dry = drySources[ch];
                    int sample = Mathf.Clamp(Mathf.RoundToInt(position * main.clip.frequency), 0, main.clip.samples - 1);
                    main.Stop(); dry.Stop(); main.timeSamples = sample; dry.timeSamples = sample;
                    main.PlayScheduled(dsp); dry.PlayScheduled(dsp);
                }
                lastCommonDsp = dsp; _schedule = music.scheduleCount; synchronizedSchedules++;
            }
            _playing = true;
        }
        private float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
    }
}
