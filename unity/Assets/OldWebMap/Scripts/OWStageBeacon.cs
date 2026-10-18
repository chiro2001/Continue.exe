using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>
    /// 舞台信标：某个段落进入时点亮对应区域（换材质 + 开关子物体）。
    /// 只在段落切换时改状态，不在 Update 里逐帧写材质。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWStageBeacon : UdonSharpBehaviour
    {
        [Header("生效段落 id（属于这些段落时点亮）")]
        public string[] activeSegments;

        [Header("材质切换")]
        public Renderer[] renderers;
        public Material activeMaterial;
        public Material idleMaterial;

        [Header("物体开关")]
        public GameObject[] activeObjects;
        public GameObject[] idleObjects;

        private bool _initialized;
        private bool _active;

        public void SetSegment(string segmentId)
        {
            bool active = IsActiveSegment(segmentId);
            if (_initialized && active == _active) return;
            _initialized = true;
            _active = active;
            Apply(active);
        }

        private bool IsActiveSegment(string segmentId)
        {
            if (activeSegments == null || segmentId == null) return false;
            for (int i = 0; i < activeSegments.Length; i++)
            {
                if (activeSegments[i] == segmentId) return true;
            }
            return false;
        }

        private void Apply(bool active)
        {
            Material target = active ? activeMaterial : idleMaterial;
            if (renderers != null && target != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) renderers[i].sharedMaterial = target;
                }
            }
            Toggle(activeObjects, active);
            Toggle(idleObjects, !active);
        }

        private void Toggle(GameObject[] objects, bool state)
        {
            if (objects == null) return;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null) objects[i].SetActive(state);
            }
        }
    }
}
