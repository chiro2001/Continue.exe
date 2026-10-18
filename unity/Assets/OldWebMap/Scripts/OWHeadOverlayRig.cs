using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace OldWebMap
{
    /// <summary>
    /// 头部全屏气氛层：把一块面片固定在本地玩家视野正前方，并驱动它的着色器参数。
    ///
    /// 用途：CRT 老屏、扫描线、颗粒、四角压暗（OWHeadAtmosphere），
    ///       以及「信号丢失」的雪花与丢行（OWHeadSignalNoise）。
    ///
    /// 约束与做法：
    ///   - VRChat 世界拿不到玩家摄像机，只能跟随本地玩家的头部追踪数据；
    ///     面片每个客户端各摆各的，不占同步量，其他玩家看不到你眼前这层效果。
    ///   - 面片本身必须在场景里预先做好（施工规范禁止运行时新建 GameObject），
    ///     本脚本只负责移动、缩放与写材质参数。
    ///   - 强度接近 0 且没有脉冲时自动关掉渲染器，省下全屏填充。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWHeadOverlayRig : UdonSharpBehaviour
    {
        [Header("场景里预先做好的面片（挂本脚本的物体）")]
        public Renderer overlayRenderer;

        [Header("跟随")]
        public float distance = 0.35f;
        public float verticalFieldOfView = 90f;
        public float coverageBoost = 1.15f;
        [Tooltip("横向补偿：桌面约 1.6~1.8，VR 单眼约 1.0")]
        public float aspect = 1.6f;
        public float followSpeed = 0f;   // 0 = 硬跟随，与 OWHeadHud 一致

        [Header("材质参数名")]
        public string intensityProperty = "_Intensity";
        public string pulseProperty = "_Pulse";

        [Header("状态")]
        [Range(0f, 2f)] public float intensity = 1f;
        [Range(0f, 1f)] public float pulse = 0f;
        [Tooltip("脉冲每秒衰减量：2.5 表示 0.4 秒衰减完")]
        public float pulseDecayPerSecond = 2.5f;

        [Header("性能")]
        [Tooltip("强度与脉冲都低于该值时关闭渲染器")]
        public float hideThreshold = 0.003f;

        private Material _material;
        private VRCPlayerApi _player;
        private bool _inEditor;
        private float _appliedIntensity = -1f;
        private float _appliedPulse = -1f;
        private bool _appliedVisible = true;

        private void Start()
        {
            _player = Networking.LocalPlayer;
            _inEditor = _player == null;
            if (overlayRenderer == null) overlayRenderer = GetComponent<Renderer>();
            if (overlayRenderer != null) _material = overlayRenderer.material;
            ApplyScale();
            ApplyMaterial(true);
        }

        /// <summary>时间轴：设置常态强度（0 = 完全关闭）。</summary>
        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>时间轴：设置脉冲强度（会按 pulseDecayPerSecond 自动衰减）。</summary>
        public void SetPulse(float value)
        {
            pulse = Mathf.Clamp01(value);
        }

        /// <summary>事件：打一次满强度脉冲（卡点、失效瞬间、开机）。</summary>
        public void FirePulse()
        {
            pulse = 1f;
        }

        /// <summary>按视野大小重算面片尺寸；改过 FOV/距离后调用。</summary>
        public void ApplyScale()
        {
            float fov = Mathf.Clamp(verticalFieldOfView, 20f, 150f);
            float halfFov = fov * 0.5f * Mathf.Deg2Rad;
            float height = 2f * distance * Mathf.Tan(halfFov) * Mathf.Max(coverageBoost, 1f);
            float width = height * Mathf.Max(aspect, 0.1f);
            transform.localScale = new Vector3(width, height, 1f);
        }

        private void LateUpdate()
        {
            FollowHead();
            DecayPulse();
            ApplyMaterial(false);
        }

        private void FollowHead()
        {
            if (_inEditor) return;
            if (_player == null)
            {
                _player = Networking.LocalPlayer;
                if (_player == null) return;
            }

            VRCPlayerApi.TrackingData head = _player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 targetPosition = head.position + head.rotation * new Vector3(0f, 0f, distance);
            Quaternion targetRotation = head.rotation;

            if (followSpeed <= 0f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        private void DecayPulse()
        {
            if (pulse <= 0f) return;
            pulse = Mathf.Max(0f, pulse - pulseDecayPerSecond * Time.deltaTime);
        }

        private void ApplyMaterial(bool force)
        {
            if (_material == null) return;

            bool visible = (intensity > hideThreshold) || (pulse > hideThreshold);
            if (force || visible != _appliedVisible)
            {
                overlayRenderer.enabled = visible;
                _appliedVisible = visible;
            }
            if (!visible) return;

            if (force || !Mathf.Approximately(intensity, _appliedIntensity))
            {
                _material.SetFloat(intensityProperty, intensity);
                _appliedIntensity = intensity;
            }
            if (force || !Mathf.Approximately(pulse, _appliedPulse))
            {
                _material.SetFloat(pulseProperty, pulse);
                _appliedPulse = pulse;
            }
        }
    }
}
