using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    /// <summary>A performance slot whose visual can be rebound to any valid Humanoid.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWActorRig : UdonSharpBehaviour
    {
        public string actorSlot;
        public string appearanceId;
        public Transform visualRoot;
        public Animator animator;
        public Transform[] humanoidBones;
        public Transform head;
        public Transform leftHand;
        public Transform rightHand;
        public Transform leftFoot;
        public Transform rightFoot;
        public float modelHeight;
        public float previewDuration = 4f;

        public Transform GetBone(int humanoidBone)
        {
            if (humanoidBones == null || humanoidBone < 0 || humanoidBone >= humanoidBones.Length) return null;
            return humanoidBones[humanoidBone];
        }

        // The director supplies time; the Animator handles Humanoid retargeting locally.
        public void SeekPreview(float seconds)
        {
            if (animator == null || previewDuration <= 0f) return;
            animator.SetFloat("PerformanceTime", Mathf.Repeat(seconds, previewDuration) / previewDuration);
        }

        public void ResetPreview()
        {
            if (animator != null) animator.SetFloat("PerformanceTime", 0f);
        }

        public void Show()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }
    }
}
