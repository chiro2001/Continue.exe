using TMPro;
using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>
    /// 彩色滚动 greetz：位置由「时间轴时间 × 速度」推导，所有客户端天然一致，
    /// 不需要逐帧同步，也不依赖本地帧率。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWGreetzRoll : UdonSharpBehaviour
    {
        [Header("数据")]
        public OWTimelineDirector director;
        public OWLyricData lyrics;

        [Header("显示")]
        public TextMeshProUGUI textField;
        public RectTransform track;
        public float scrollSpeed = 60f;
        public float cycleLength = 2400f;
        public float startX = 0f;

        private int _entryIndex = -2;
        private float _startY;
        private bool _captured;

        private void Start()
        {
            if (track != null)
            {
                _startY = track.anchoredPosition.y;
                _captured = true;
            }
        }

        private void Update()
        {
            if (director == null || track == null) return;
            if (!_captured)
            {
                _startY = track.anchoredPosition.y;
                _captured = true;
            }
            float offset = Mathf.Repeat(director.GetElapsed() * scrollSpeed, cycleLength);
            track.anchoredPosition = new Vector2(startX - offset, _startY);
        }

        public void SetEntry(int index)
        {
            if (index == _entryIndex) return;
            _entryIndex = index;
            if (textField == null) return;

            if (index < 0)
            {
                textField.text = "";
                return;
            }
            string entry = "";
            if (lyrics != null) entry = lyrics.GetGreetzEntry(index);
            textField.text = entry;
        }
    }
}
