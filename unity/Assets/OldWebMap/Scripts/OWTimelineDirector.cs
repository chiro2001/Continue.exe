using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace OldWebMap
{
    /// <summary>
    /// 演出时间轴总控。
    ///
    /// 同步模型：只同步「是否在播放 + 播放锚点」两件事——
    /// 锚点是 (服务端时间, 时间轴位置)，所有客户端都用同一个服务端时钟推导进度，
    /// 因此新玩家入房不需要补帧，落点天然对齐；逐帧参数、粒子、后处理全部本地推算。
    ///
    /// 段落切换与事件由每个客户端本地推导，误差只受服务端时间精度影响。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class OWTimelineDirector : UdonSharpBehaviour
    {
        [Header("数据")]
        public OWTimelineData data;

        [Header("消费者")]
        public OWStageBeacon[] beacons;
        public OWVisitorCounter visitorCounter;
        public UdonBehaviour[] segmentListeners;

        [Header("播放")]
        public bool autoStart = true;
        public float autoStartDelay = 3f;
        public bool loop = false;

        [UdonSynced] private bool _playing;
        [UdonSynced] private double _startServerTime;
        [UdonSynced] private float _elapsedOffset;

        private float _elapsed;
        private float _lastEventTime;
        private int _segmentIndex = -2;
        private bool _finished;
        private string _lastEventType = "";
        private string _lastEventId = "";

        private void Start()
        {
            _lastEventTime = 0f;
            if (autoStart && Networking.IsOwner(gameObject))
            {
                SendCustomEventDelayedSeconds("DirectorPlay", autoStartDelay);
            }
        }

        private void Update()
        {
            if (data == null) return;
            _elapsed = ComputeElapsed();

            float duration = data.durationSeconds;
            if (duration > 0f && _elapsed > duration)
            {
                _elapsed = duration;
                if (Networking.IsOwner(gameObject) && _playing && !loop)
                {
                    _elapsedOffset = duration;
                    _playing = false;
                    _finished = true;
                    RequestSerialization();
                }
            }

            int segment = data.FindSegmentIndex(_elapsed);
            if (segment != _segmentIndex)
            {
                _segmentIndex = segment;
                // 迟到的客户端不补放已经过去的事件，只对齐段落状态。
                _lastEventTime = _elapsed;
                NotifySegmentChanged();
            }
            else
            {
                FireDueEvents(segment);
            }

            _lastEventTime = _elapsed;
        }

        private void FireDueEvents(int segment)
        {
            if (segment < 0) return;
            int count = data.GetEventCount(segment);
            for (int i = 0; i < count; i++)
            {
                float when = data.GetEventTime(segment, i);
                if (when <= _lastEventTime || when > _elapsed) continue;
                _lastEventType = data.GetEventType(segment, i);
                _lastEventId = data.GetEventId(segment, i);
                NotifyEvent();
            }
        }

        private void NotifySegmentChanged()
        {
            if (beacons != null)
            {
                for (int i = 0; i < beacons.Length; i++)
                {
                    if (beacons[i] != null) beacons[i].SetSegment(GetSegmentId());
                }
            }
            if (visitorCounter != null) visitorCounter.SetSegment(GetSegmentId());
            if (segmentListeners != null)
            {
                for (int i = 0; i < segmentListeners.Length; i++)
                {
                    if (segmentListeners[i] != null) segmentListeners[i].SendCustomEvent("OnOWSegmentChanged");
                }
            }
        }

        private void NotifyEvent()
        {
            if (segmentListeners == null) return;
            for (int i = 0; i < segmentListeners.Length; i++)
            {
                if (segmentListeners[i] != null) segmentListeners[i].SendCustomEvent("OnOWEvent");
            }
        }

        private float ComputeElapsed()
        {
            if (!_playing) return _elapsedOffset;
            double now = Networking.GetServerTimeInSeconds();
            double delta = now - _startServerTime;
            if (delta < 0.0) delta = 0.0;
            return _elapsedOffset + (float)delta;
        }

        // ---------------- 导演接口（仅实例主可用） ----------------

        public void DirectorPlay()
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (_finished && !loop)
            {
                _elapsedOffset = 0f;
                _finished = false;
            }
            _startServerTime = Networking.GetServerTimeInSeconds();
            _playing = true;
            RequestSerialization();
        }

        public void DirectorPause()
        {
            if (!Networking.IsOwner(gameObject)) return;
            _elapsedOffset = ComputeElapsed();
            _playing = false;
            RequestSerialization();
        }

        public void DirectorRestart()
        {
            DirectorSeekTo(0f);
            DirectorPlay();
        }

        public void DirectorSeekTo(float seconds)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (seconds < 0f) seconds = 0f;
            if (data != null && seconds > data.durationSeconds) seconds = data.durationSeconds;
            _elapsedOffset = seconds;
            _startServerTime = Networking.GetServerTimeInSeconds();
            _finished = false;
            RequestSerialization();
        }

        public void DirectorGotoSegment(int segmentIndex)
        {
            if (data == null) return;
            int count = data.GetSegmentCount();
            if (segmentIndex < 0) segmentIndex = 0;
            if (segmentIndex >= count) segmentIndex = count - 1;
            DirectorSeekTo(data.GetSegmentStart(segmentIndex));
        }

        public void DirectorNextSegment()
        {
            DirectorGotoSegment(GetSegmentIndex() + 1);
        }

        public void DirectorPreviousSegment()
        {
            DirectorGotoSegment(GetSegmentIndex() - 1);
        }

        public override void OnDeserialization()
        {
            // 同步状态到达后立刻按新的锚点重算，避免新玩家看到上一帧的旧值。
            _elapsed = ComputeElapsed();
            _segmentIndex = -2;
            _lastEventTime = _elapsed;
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject)) RequestSerialization();
        }

        // ---------------- 只读查询 ----------------

        public float GetElapsed() { return _elapsed; }

        public float GetTotalDuration()
        {
            if (data == null) return 0f;
            return data.durationSeconds;
        }

        public bool IsPlaying() { return _playing; }

        public bool HasFinished() { return _finished; }

        public int GetSegmentIndex() { return _segmentIndex; }

        public string GetSegmentId()
        {
            if (data == null || _segmentIndex < 0) return "";
            return data.GetSegmentId(_segmentIndex);
        }

        public string GetSegmentName()
        {
            if (data == null || _segmentIndex < 0) return "";
            return data.GetSegmentName(_segmentIndex);
        }

        public float GetSegmentElapsed()
        {
            if (data == null || _segmentIndex < 0) return 0f;
            float start = data.GetSegmentStart(_segmentIndex);
            float value = _elapsed - start;
            if (value < 0f) value = 0f;
            return value;
        }

        public float GetSegmentDuration()
        {
            if (data == null || _segmentIndex < 0) return 1f;
            float length = data.GetSegmentEnd(_segmentIndex) - data.GetSegmentStart(_segmentIndex);
            if (length <= 0f) length = 1f;
            return length;
        }

        public float GetSegmentProgress()
        {
            float length = GetSegmentDuration();
            if (length <= 0f) return 0f;
            return GetSegmentElapsed() / length;
        }

        public string GetLastEventType() { return _lastEventType; }

        public string GetLastEventId() { return _lastEventId; }
    }
}
