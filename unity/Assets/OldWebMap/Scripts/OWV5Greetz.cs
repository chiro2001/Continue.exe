using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // Greetz ring: fixed physical sine scroller. Everything is a pure function of the performance time:
    // the baked piecewise-linear curve gives the text pixel at the arc front, the shader does the rest.
    // Mesh, material and atlas are persisted assets authored by OWEpisodeV5Identity.BuildGreetz.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV5Greetz : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public Renderer ringRenderer;
        public Material ringMaterial;
        public float[] curvePx;
        public float curveStart = 30.72f;
        public float curveStep = .32f;
        public float fadeInStart = 30.72f;
        public float fadeInLength = 1.28f;
        public float dimStart = 81.92f;
        public float dimEnd = 92.16f;
        public float dimOpacity = .55f;
        public float dimRamp = .64f;
        public float fadeOutLength = .8f;
        public float duration = 133.12f;
        public bool autoSample = true;
        public float sampledTime;
        public float sampledScroll;
        public float sampledOpacity;

        private void LateUpdate()
        {
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        public void Seek(float time) { Sample(time); }

        public void Sample(float time)
        {
            sampledTime = time;
            float scroll = 0f;
            int count = curvePx == null ? 0 : curvePx.Length;
            if (count > 0)
            {
                float u = (time - curveStart) / curveStep;
                if (u <= 0f) scroll = curvePx[0];
                else if (u >= count - 1) scroll = curvePx[count - 1];
                else
                {
                    int i = Mathf.FloorToInt(u);
                    scroll = Mathf.Lerp(curvePx[i], curvePx[i + 1], u - i);
                }
            }
            float opacity = 0f;
            if (time >= fadeInStart)
            {
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - fadeInStart) / fadeInLength));
                float climax = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - dimStart) / dimRamp)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - dimEnd) / dimRamp)));
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - (duration - fadeOutLength)) / fadeOutLength));
                opacity = fadeIn * (1f - (1f - dimOpacity) * climax) * fadeOut;
            }
            sampledScroll = scroll;
            sampledOpacity = opacity;
            if (ringRenderer != null) ringRenderer.enabled = opacity > .002f;
            if (ringMaterial != null)
            {
                ringMaterial.SetFloat("_Scroll", scroll);
                ringMaterial.SetFloat("_Timeline", time);
                ringMaterial.SetFloat("_Opacity", opacity);
            }
        }
    }
}
