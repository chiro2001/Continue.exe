using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace OldWebMap
{
    /// <summary>
    /// 把世界空间 HUD 固定在本地玩家的视野下缘。
    /// 每个客户端各算各的，不占同步量；离开演出区也不会丢失歌词带。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class OWHeadHud : UdonSharpBehaviour
    {
        [Header("相对头显的偏移（本地坐标系）")]
        public Vector3 offset = new Vector3(0f, -0.34f, 1.45f);
        public Vector3 eulerOffset = new Vector3(-8f, 0f, 0f);

        [Header("跟随")]
        public float followSpeed = 0f;   // 0 = 硬跟随，>0 = 平滑跟随

        private VRCPlayerApi _player;
        private bool _inEditor;

        private void Start()
        {
            _player = Networking.LocalPlayer;
            _inEditor = _player == null;
        }

        private void LateUpdate()
        {
            if (_inEditor) return;
            if (_player == null)
            {
                _player = Networking.LocalPlayer;
                if (_player == null) return;
            }

            VRCPlayerApi.TrackingData head = _player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 targetPosition = head.position + head.rotation * offset;
            Quaternion targetRotation = head.rotation * Quaternion.Euler(eulerOffset);

            if (followSpeed <= 0f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
    }
}
