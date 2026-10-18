using TMPro;
using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>
    /// 访客计数器：平时缓慢累加，片尾段落归零。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWVisitorCounter : UdonSharpBehaviour
    {
        [Header("数据")]
        public OWTimelineDirector director;
        public TextMeshProUGUI digits;
        public string resetSegmentId = "outro";

        [Header("数值")]
        public int startValue = 1048576;
        public int digitCount = 7;
        public float idlePerSecond = 3f;
        public float resetDuration = 12f;

        private int _lastShown = -1;
        private bool _resetting;
        private float _resetStartElapsed;
        private bool _finishedReset;

        public void SetSegment(string segmentId)
        {
            if (segmentId == resetSegmentId)
            {
                if (_resetting) return;
                _resetting = true;
                _finishedReset = false;
                _resetStartElapsed = director != null ? director.GetElapsed() : 0f;
            }
            else
            {
                _resetting = false;
                _finishedReset = false;
            }
        }

        private void Update()
        {
            if (digits == null) return;
            int value;
            if (_resetting && !_finishedReset)
            {
                float elapsed = director != null ? director.GetElapsed() : 0f;
                float t = resetDuration > 0.01f ? (elapsed - _resetStartElapsed) / resetDuration : 1f;
                t = Mathf.Clamp01(t);
                value = Mathf.RoundToInt(Mathf.Lerp((float)startValue, 0f, t));
                if (t >= 1f) _finishedReset = true;
            }
            else if (_resetting)
            {
                value = 0;
            }
            else
            {
                float elapsed = director != null ? director.GetElapsed() : 0f;
                value = startValue + Mathf.RoundToInt(elapsed * idlePerSecond);
            }
            if (value == _lastShown) return;
            _lastShown = value;
            digits.text = Format(value);
        }

        private string Format(int value)
        {
            string text = value.ToString();
            while (text.Length < digitCount) text = "0" + text;
            return text;
        }
    }
}
