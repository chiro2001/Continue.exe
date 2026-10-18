using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OWEpisodeDirector : UdonSharpBehaviour
    {
        public OWEpisodeStage stage;
        public OWEpisodeMusic music;
        public float duration = 133.12f;
        public bool autoStart;
        public bool continuousPerformance;
        // V4 owns the visible world and captions through its independent shell.
        // Earlier scenes retain the legacy stage sampler by default.
        public bool externalStageSampler;
        public GameObject[] audiencePanels;
        public OWEpisodeMontage montage;
        public float lastMontageSampleMilliseconds;
        [UdonSynced] private bool _playing;
        [UdonSynced] private double _anchor;
        [UdonSynced] private float _offset;
        [UdonSynced] private int _revision;
        private float _elapsed;
        public float lastStageSampleMilliseconds;
        public float lastAudioSyncMilliseconds;

        private void Start()
        {
            if (autoStart && IsAuthority()) SendCustomEventDelayedSeconds("PlayFromConnect", .8f);
        }

        private double Clock()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return (double)Time.realtimeSinceStartup;
            return Networking.GetServerTimeInSeconds();
        }

        private bool IsAuthority() { return !Utilities.IsValid(Networking.LocalPlayer) || Networking.IsOwner(gameObject); }

        private void Acquire()
        {
            if (Utilities.IsValid(Networking.LocalPlayer) && !Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer,gameObject);
        }

        public float Elapsed()
        {
            if (!_playing) return _offset;
            return Mathf.Clamp(_offset + (float)System.Math.Max(0.0,Clock()-_anchor),0f,duration);
        }

        private void Update()
        {
            _elapsed=Elapsed();
            if (_playing && _elapsed>=duration && IsAuthority())
            {
                _offset=duration;_playing=false;_revision++;RequestSerialization();
            }
            float audioStart=Time.realtimeSinceStartup;
            if (music!=null) music.SyncPlayback(_elapsed,_playing,_revision,(float)System.Math.Max(0.0,_anchor-Clock()));
            lastAudioSyncMilliseconds=(Time.realtimeSinceStartup-audioStart)*1000f;
            float stageStart=Time.realtimeSinceStartup;
            if (!externalStageSampler && stage!=null) stage.Sample(_elapsed);
            lastStageSampleMilliseconds=(Time.realtimeSinceStartup-stageStart)*1000f;
            float montageStart=Time.realtimeSinceStartup;
            if(montage!=null) montage.Sample(_elapsed);
            lastMontageSampleMilliseconds=(Time.realtimeSinceStartup-montageStart)*1000f;
            Present();
            if (Input.GetKeyDown(KeyCode.Return) && (!Utilities.IsValid(Networking.LocalPlayer) || !Networking.LocalPlayer.IsUserInVR())) Toggle();
        }

        public void Toggle() { if (continuousPerformance) { if (!_playing) Play(); return; } if (_playing) Pause(); else Play(); }
        private void Present()
        {
            if(!continuousPerformance || audiencePanels==null) return;
            for(int i=0;i<audiencePanels.Length;i++)
                if(audiencePanels[i]!=null && audiencePanels[i].activeSelf==_playing) audiencePanels[i].SetActive(!_playing);
        }
        public void Play()
        {
            if(continuousPerformance && _playing) return;
            Acquire();if (_offset>=duration-.01f) _offset=0;
            _anchor=Clock()+.25;_playing=true;_revision++;RequestSerialization();Present();
        }
        public void Pause()
        {
            if(continuousPerformance) return;
            Acquire();_offset=Elapsed();_playing=false;_revision++;RequestSerialization();
        }
        public void Restart() { if(continuousPerformance && _playing) return; Seek(0);Play(); }
        public void PlayFromConnect() { Seek(0);Play(); }
        public void PlayFromCreate() { Seek(30.72f);Play(); }
        public void PlayFromConstrain() { Seek(102.4f);Play(); }
        public void Seek(float seconds)
        {
            Acquire();_offset=Mathf.Clamp(seconds,0,duration);_anchor=Clock()+.25;_revision++;RequestSerialization();
        }
        public void StopAtStart() { Acquire();_playing=false;_offset=0;_revision++;RequestSerialization(); }
        public bool IsPlaying() { return _playing; }
        public int Revision() { return _revision; }
        public override void OnPlayerJoined(VRCPlayerApi player) { if(IsAuthority()) RequestSerialization(); }
        public override void OnDeserialization() { _elapsed=Elapsed();Present(); if(!externalStageSampler && stage!=null) stage.Sample(_elapsed);if(montage!=null) montage.Sample(_elapsed); }
        public override void Interact() { Toggle(); }
    }
}
