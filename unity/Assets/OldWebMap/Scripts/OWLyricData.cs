using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>
    /// 歌词与 greetz 数据。只读容器，由 AgentScripts/OWMImportData.cs 从 data/lyrics.json 生成。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWLyricData : UdonSharpBehaviour
    {
        [Header("歌词（并行数组）")]
        public string[] lineTexts;
        public string[] lineSegments;
        public int[] lineNumbers;

        [Header("Greetz")]
        public string[] greetzEntries;
        public float[] greetzSwitchTimes;
        public int[] greetzSwitchIndex;

        public int GetLineCount()
        {
            if (lineTexts == null) return 0;
            return lineTexts.Length;
        }

        public string GetLineText(int index)
        {
            if (lineTexts == null || index < 0 || index >= lineTexts.Length) return "";
            return lineTexts[index];
        }

        public string GetLineSegment(int index)
        {
            if (lineSegments == null || index < 0 || index >= lineSegments.Length) return "";
            return lineSegments[index];
        }

        public int GetLineNumber(int index)
        {
            if (lineNumbers == null || index < 0 || index >= lineNumbers.Length) return index + 1;
            return lineNumbers[index];
        }

        /// <summary>某段落内第 ordinal 句歌词在整体歌词数组中的下标；越界返回 -1。</summary>
        public int GetLineIndexInSegment(string segmentId, int ordinal)
        {
            if (lineSegments == null || ordinal < 0) return -1;
            int found = 0;
            for (int i = 0; i < lineSegments.Length; i++)
            {
                if (lineSegments[i] != segmentId) continue;
                if (found == ordinal) return i;
                found = found + 1;
            }
            return -1;
        }

        public int CountLinesInSegment(string segmentId)
        {
            if (lineSegments == null) return 0;
            int count = 0;
            for (int i = 0; i < lineSegments.Length; i++)
            {
                if (lineSegments[i] == segmentId) count = count + 1;
            }
            return count;
        }

        public int GetGreetzEntryCount()
        {
            if (greetzEntries == null) return 0;
            return greetzEntries.Length;
        }

        public string GetGreetzEntry(int index)
        {
            if (greetzEntries == null || index < 0 || index >= greetzEntries.Length) return "";
            return greetzEntries[index];
        }

        /// <summary>给定全局时间，返回应显示的 greetz 序号；尚未到第一次切换时返回 -1。</summary>
        public int FindGreetzIndex(float time)
        {
            if (greetzSwitchTimes == null || greetzSwitchIndex == null) return -1;
            int result = -1;
            int count = greetzSwitchTimes.Length;
            if (greetzSwitchIndex.Length < count) count = greetzSwitchIndex.Length;
            for (int i = 0; i < count; i++)
            {
                if (time >= greetzSwitchTimes[i]) result = greetzSwitchIndex[i];
            }
            return result;
        }
    }
}
