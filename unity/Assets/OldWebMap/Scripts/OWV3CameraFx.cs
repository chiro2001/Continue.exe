using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace OldWebMap
{
    // A locally followed full-screen rendering surface, never a player/camera rig.
    // Each eye projects itself in the shader. The head pose only selects the local
    // primary view and keeps CPU culling bounds beside the current player.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV3CameraFx : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public Renderer overlayRenderer;
        public Material effectMaterial;
        public Transform fallbackCamera;
        public bool autoSample = true;
        public float masterIntensity = 1f;
        public float sampledTime;
        public float sampledIntensity;
        private int _tick = -1;

        private void LateUpdate()
        {
            FollowHead();
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        public void FollowHead()
        {
            Vector3 position; Quaternion rotation;
            if (Utilities.IsValid(Networking.LocalPlayer))
            {
                VRCPlayerApi.TrackingData head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                position = head.position; rotation = head.rotation;
            }
            else if (fallbackCamera != null) { position = fallbackCamera.position; rotation = fallbackCamera.rotation; }
            else return;
            transform.SetPositionAndRotation(position + rotation * new Vector3(0, 0, .4f), rotation);
            if (effectMaterial != null)
            {
                effectMaterial.SetVector("_LocalHeadPosition", new Vector4(position.x, position.y, position.z, 1));
                Vector3 forward = rotation * Vector3.forward;
                effectMaterial.SetVector("_LocalHeadForward", new Vector4(forward.x, forward.y, forward.z, 0));
            }
        }

        public void Sample(float time)
        {
            FollowHead();
            sampledTime = time;
            int tick = Mathf.FloorToInt(time * 30f);
            if (_tick == tick || effectMaterial == null || overlayRenderer == null) return;
            _tick = tick;
            float enter = Ease(time / 1.4f);
            float leave = 1f - Ease((time - 131.84f) / 1.28f);
            float create = Ease((time - 30.72f) / 5.12f);
            float constrain = Ease((time - 102.4f) / 2.56f);
            float bloom = Ease((time - 81.92f) / 2.56f) * (1f - Ease((time - 89.6f) / 2.56f));
            // Localized dropouts rather than permanent flicker; exact timeline so
            // seeking and replay choose the same effect. Total alpha stays modest.
            float lossA = Ease((time - 111.36f) / .32f) * (1f - Ease((time - 112.64f) / .8f));
            float lossB = Ease((time - 121.6f) / .25f) * (1f - Ease((time - 122.88f) / .7f));
            float dropout = Mathf.Max(lossA, lossB);
            float intensity = masterIntensity * enter * leave;
            sampledIntensity = intensity;
            overlayRenderer.enabled = intensity > .002f;
            effectMaterial.SetFloat("_Intensity", intensity);
            effectMaterial.SetFloat("_Timeline", time);
            effectMaterial.SetFloat("_CRT", .35f * (1f - create) + .12f * create);
            effectMaterial.SetFloat("_Chroma", .00016f + bloom * .00065f + dropout * .0011f);
            effectMaterial.SetFloat("_Warp", bloom * .0035f + dropout * .002f);
            effectMaterial.SetFloat("_Vignette", .10f + constrain * .13f - bloom * .06f);
            effectMaterial.SetFloat("_Desaturate", constrain * .32f);
            effectMaterial.SetFloat("_Dropout", dropout * .45f);
            effectMaterial.SetFloat("_Warmth", create * (1f - constrain) * .12f);
        }
        private float Ease(float value) { return Mathf.SmoothStep(0, 1, Mathf.Clamp01(value)); }
    }
}
