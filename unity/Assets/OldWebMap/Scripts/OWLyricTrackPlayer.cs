using TMPro;
using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>
    /// 下缘歌词带：按段落切句、逐字高亮、句末保持、下一句淡出接上。
    /// 不演唱，只做 KTV 式逐字显示；时间全部来自 OWTimelineDirector，本地推算。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWLyricTrackPlayer : UdonSharpBehaviour
    {
        [Header("数据")]
        public OWTimelineDirector director;
        public OWLyricData lyrics;

        [Header("显示")]
        public TextMeshProUGUI lineField;
        public OWGreetzRoll greetzRoll;

        [Header("打字机")]
        public float charsPerSecond = 11f;
        public float fadeFraction = 0.18f;
        public string cursorGlyph = "_";
        public Color lineColor = new Color(1f, 0.69f, 0f, 1f);

        private string _segmentId = "";
        private string _shownText = "";
        private float _shownAlpha = -1f;
        private int _greetzIndex = -2;
        private bool _visible = true;

        private void Update()
        {
            if (director == null || lyrics == null || lineField == null) return;

            string segmentId = director.GetSegmentId();
            int count = lyrics.CountLinesInSegment(segmentId);
            if (count <= 0)
            {
                SetLine("", 0f);
                TickGreetz();
                return;
            }

            float segmentElapsed = director.GetSegmentElapsed();
            float slot = director.GetSegmentDuration() / count;
            int ordinal = (int)(segmentElapsed / slot);
            if (ordinal < 0) ordinal = 0;
            if (ordinal >= count) ordinal = count - 1;

            if (segmentId != _segmentId)
            {
                _segmentId = segmentId;
                _shownText = "";
                _shownAlpha = -1f;
            }

            string text = lyrics.GetLineText(lyrics.GetLineIndexInSegment(segmentId, ordinal));
            int total = text.Length;
            float lineElapsed = segmentElapsed - ordinal * slot;

            float revealWindow = total / Mathf.Max(1f, charsPerSecond);
            float maxReveal = slot * 0.75f;
            if (revealWindow > maxReveal) revealWindow = maxReveal;
            if (revealWindow < 0.05f) revealWindow = 0.05f;

            int shownChars = (int)(total * Mathf.Clamp01(lineElapsed / revealWindow));
            if (shownChars > total) shownChars = total;

            string line = text.Substring(0, shownChars);
            if (shownChars < total) line = line + cursorGlyph;

            float alpha = 1f;
            float fadeSpan = slot * fadeFraction;
            float fadeStart = slot - fadeSpan;
            if (fadeSpan > 0.01f && lineElapsed > fadeStart)
            {
                alpha = Mathf.Clamp01((slot - lineElapsed) / fadeSpan);
            }
            if (director.HasFinished() && ordinal == count - 1)
            {
                // 全片结束后最后一句保持可见，交给片尾黑屏收尾。
                alpha = 1f;
                line = text;
            }

            SetLine(line, alpha);
            TickGreetz();
        }

        private void TickGreetz()
        {
            if (greetzRoll == null) return;
            int index = lyrics.FindGreetzIndex(director.GetElapsed());
            if (index == _greetzIndex) return;
            _greetzIndex = index;
            greetzRoll.SetEntry(index);
        }

        private void SetLine(string text, float alpha)
        {
            if (lineField == null) return;
            bool visible = text.Length > 0 && alpha > 0.01f;
            if (visible != _visible)
            {
                _visible = visible;
                lineField.gameObject.SetActive(visible);
            }
            if (!visible) return;

            if (text != _shownText)
            {
                _shownText = text;
                lineField.text = text;
            }
            if (Mathf.Abs(alpha - _shownAlpha) > 0.02f || _shownAlpha < 0f)
            {
                _shownAlpha = alpha;
                lineField.color = new Color(lineColor.r, lineColor.g, lineColor.b, alpha);
            }
        }

        /// <summary>外部（导演面板/事件）可用：立刻只显示某一句。</summary>
        public void ShowLine(int lineIndex)
        {
            if (lyrics == null) return;
            SetLine(lyrics.GetLineText(lineIndex), 1f);
        }
    }
}
