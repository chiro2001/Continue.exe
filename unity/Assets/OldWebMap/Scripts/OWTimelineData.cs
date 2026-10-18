using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>
    /// 时间轴数据：段落与事件。
    /// 只读容器，由 AgentScripts/OWMImportData.cs 从 data/timeline.json 生成；
    /// 运行时不做解析，也不参与同步。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWTimelineData : UdonSharpBehaviour
    {
        [Header("标题")]
        public string title = "";
        public string subtitle = "";
        public float durationSeconds = 180f;

        [Header("段落（并行数组）")]
        public string[] segmentIds;
        public string[] segmentNames;
        public string[] segmentSpaces;
        public float[] segmentStarts;
        public float[] segmentEnds;

        [Header("事件（并行数组，按段落划分区间）")]
        public int[] eventStartIndex;
        public int[] eventCount;
        public float[] eventTimes;
        public string[] eventTypes;
        public string[] eventIds;

        public int GetSegmentCount()
        {
            if (segmentIds == null) return 0;
            return segmentIds.Length;
        }

        public string GetSegmentId(int index)
        {
            if (segmentIds == null || index < 0 || index >= segmentIds.Length) return "";
            return segmentIds[index];
        }

        public string GetSegmentName(int index)
        {
            if (segmentNames == null || index < 0 || index >= segmentNames.Length) return "";
            return segmentNames[index];
        }

        public string GetSegmentSpace(int index)
        {
            if (segmentSpaces == null || index < 0 || index >= segmentSpaces.Length) return "";
            return segmentSpaces[index];
        }

        public float GetSegmentStart(int index)
        {
            if (segmentStarts == null || index < 0 || index >= segmentStarts.Length) return 0f;
            return segmentStarts[index];
        }

        public float GetSegmentEnd(int index)
        {
            if (segmentEnds == null || index < 0 || index >= segmentEnds.Length) return 0f;
            return segmentEnds[index];
        }

        /// <summary>返回时间点所属段落下标；早于第一段返回 0，晚于最后一段返回最后一段。</summary>
        public int FindSegmentIndex(float time)
        {
            int count = GetSegmentCount();
            if (count <= 0) return -1;
            if (time < segmentStarts[0]) return 0;
            for (int i = 0; i < count; i++)
            {
                if (time < segmentEnds[i]) return i;
            }
            return count - 1;
        }

        public int GetEventCount(int segmentIndex)
        {
            if (eventCount == null || segmentIndex < 0 || segmentIndex >= eventCount.Length) return 0;
            return eventCount[segmentIndex];
        }

        public int GetEventStartIndex(int segmentIndex)
        {
            if (eventStartIndex == null || segmentIndex < 0 || segmentIndex >= eventStartIndex.Length) return 0;
            return eventStartIndex[segmentIndex];
        }

        public float GetEventTime(int segmentIndex, int ordinal)
        {
            int index = GetEventStartIndex(segmentIndex) + ordinal;
            if (eventTimes == null || index < 0 || index >= eventTimes.Length) return -1f;
            return eventTimes[index];
        }

        public string GetEventType(int segmentIndex, int ordinal)
        {
            int index = GetEventStartIndex(segmentIndex) + ordinal;
            if (eventTypes == null || index < 0 || index >= eventTypes.Length) return "";
            return eventTypes[index];
        }

        public string GetEventId(int segmentIndex, int ordinal)
        {
            int index = GetEventStartIndex(segmentIndex) + ordinal;
            if (eventIds == null || index < 0 || index >= eventIds.Length) return "";
            return eventIds[index];
        }
    }
}
