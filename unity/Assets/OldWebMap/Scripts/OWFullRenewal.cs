using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    /// <summary>
    /// Persisted 186.88–256 s ensemble. Sample is entirely determined by the supplied
    /// score time: no local clock, random state, audio, player movement or spawned objects.
    /// The parent presentation calls this after its common clock has advanced.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWFullRenewal : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public bool autoSample = false;
        public GameObject contentRoot;
        public Transform transitFrame;
        public Transform preWindowSpace;
        public Transform afterWindowSpace;
        public Transform crossingWindow;
        public GameObject windowPreview;
        public GameObject replyWindow;
        public GameObject distantArchive;
        public GameObject nearArchive;
        public Light archiveLight;
        public Light workLight;
        public Transform[] stationRevealRoots;
        public GameObject sharedQuestion;
        public Renderer questionRenderer;
        public Material[] questionMaterials;
        public Renderer[] toolScreens;
        public Renderer[] subtitleScreens;
        public Renderer[] worldScreens;
        public Material[] toolMaterials;
        public Material[] subtitleMaterials;
        public Material[] worldMaterials;
        public Material newcomerMaterial;
        public GameObject newcomerPublication;
        public Renderer newcomerScreen;
        public Transform[] localCursors;
        public Transform[] remoteCursors;
        public Transform[] layerCards;
        public Vector3[] layerCardOrigins;
        public GameObject originalWorldResult;
        public Transform worldSculpture;
        public GameObject[] worldDetailStages;
        public GameObject originalAccessory;
        public Transform[] originalAccessoryStars;
        public GameObject[] publishedMarks;
        public OWActorRig[] actors;
        public Transform[] actorNameplates;
        public int[] actorKeyOffsets;
        public int[] actorKeyCounts;
        public float[] actorShowStarts;
        public float[] actorShowEnds;
        public float[] actorKeyTimes;
        public Vector3[] actorKeyPositions;
        public float[] actorKeyYaws;
        public int[] actorKeyGestures;
        public int[] sampledActorGestures;
        public GameObject[] carriedBooks;
        public GameObject[] contactTrays;
        public float transitStart = 217.60f;
        public float transitEnd = 222.72f;
        public float transitDistance = 16f;
        public float duration = 256f;
        public float sampledTime;
        public float sampledTransit;
        public Vector3 sampledWorldOffset;
        public bool sampledInTransit;
        public int sampledToolState;
        public int sampledSubtitleState;
        public int sampledWorldState;
        public int sampledVisibleActors;

        private void LateUpdate()
        {
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        public void Seek(float time) { Sample(time); }

        private float Ease(float time, float start, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - start) / Mathf.Max(.01f, end - start)));
        }

        private void Show(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }

        private void ScreenMaterial(Renderer[] screens, Material[] materials, int index)
        {
            if (screens == null || materials == null || materials.Length == 0) return;
            Material material = materials[Mathf.Clamp(index, 0, materials.Length - 1)];
            for (int i = 0; i < screens.Length; i++)
                if (screens[i] != null && screens[i].sharedMaterial != material) screens[i].sharedMaterial = material;
        }

        public void Sample(float time)
        {
            sampledTime = Mathf.Clamp(time, 0f, duration);
            bool active = time >= 186.88f && time <= duration;
            Show(contentRoot, active);
            // Always resolve every field, including while hidden, so a seek backwards
            // and a forward run give the same scene state.
            sampledTransit = Ease(time, transitStart, transitEnd);
            sampledInTransit = time >= transitStart && time < transitEnd;
            sampledWorldOffset = new Vector3(0f, 0f, -transitDistance * sampledTransit);
            if (transitFrame != null) transitFrame.localPosition = sampledWorldOffset;
            if (preWindowSpace != null)
            {
                preWindowSpace.localPosition = new Vector3(-2.4f * Ease(time, 215.04f, transitStart) - 4.1f * sampledTransit, 0f, 0f);
                Show(preWindowSpace.gameObject, time >= 197.12f && time < 219.52f);
            }
            if (afterWindowSpace != null)
            {
                afterWindowSpace.localPosition = new Vector3(0f, 0f, transitDistance);
                Show(afterWindowSpace.gameObject, time >= 215.04f);
            }
            if (crossingWindow != null)
            {
                float prepare = Ease(time, 215.04f, transitStart);
                float scale = Mathf.Lerp(.38f, 1f, prepare) + 2.4f * sampledTransit;
                crossingWindow.localPosition = new Vector3(0f, 1.65f, 5.6f);
                crossingWindow.localScale = Vector3.one * scale;
                Show(crossingWindow.gameObject, time >= 215.04f && time < 224f);
            }
            // This plane is a preparation image only. During the passage the opening
            // contains actual geometry with stereo depth and unrestricted head tracking.
            Show(windowPreview, time < transitStart);
            Show(replyWindow, time >= 194.56f && time < 199.68f);
            Show(distantArchive, time < 198.40f);
            Show(nearArchive, time >= 197.12f);
            if (archiveLight != null)
            {
                archiveLight.enabled = active && time < 198.40f;
                archiveLight.intensity = 1.6f * (1f - Ease(time, 197.12f, 198.40f));
            }
            if (workLight != null)
            {
                workLight.enabled = active && time >= 197.12f;
                workLight.intensity = .9f * Ease(time, 197.12f, 199.68f);
                workLight.transform.localPosition = new Vector3(0f, 2.7f, time < transitStart ? .9f : 1.1f);
                workLight.color = Color.Lerp(new Color(.50f, .78f, 1f), new Color(1f, .80f, .61f), Ease(time, 215.04f, 235.52f));
            }
            if (stationRevealRoots != null)
                for (int i = 0; i < stationRevealRoots.Length; i++)
                {
                    if (stationRevealRoots[i] == null) continue;
                    float reveal = Ease(time, 197.12f + i * .64f, 198.40f + i * .64f);
                    stationRevealRoots[i].localScale = Vector3.one * Mathf.Lerp(.001f, 1f, reveal);
                }

            sampledToolState = time < 202.24f ? 0 : time < 210.56f ? 1 : time < 230.40f ? 2 : 3;
            sampledSubtitleState = time < 207.36f ? 0 : time < 229.12f ? 1 : 2;
            sampledWorldState = time < 212.48f ? 0 : time < 230.40f ? 1 : 2;
            ScreenMaterial(toolScreens, toolMaterials, sampledToolState);
            ScreenMaterial(subtitleScreens, subtitleMaterials, sampledSubtitleState);
            ScreenMaterial(worldScreens, worldMaterials, sampledWorldState);
            Show(sharedQuestion, time >= 207.36f && time < 215.04f);
            if (questionRenderer != null && questionMaterials != null && questionMaterials.Length > 1)
                questionRenderer.sharedMaterial = questionMaterials[time < 211.20f ? 0 : 1];
            Show(newcomerPublication, time >= 240.64f);
            if (newcomerScreen != null && newcomerMaterial != null) newcomerScreen.sharedMaterial = newcomerMaterial;

            SampleCursors(time);
            SampleLayerCards(time);
            Show(originalWorldResult, time >= 212.48f);
            if (worldSculpture != null)
                worldSculpture.localRotation = Quaternion.Euler(0f, 18f + Mathf.Sin(time * .36f) * 7f, 0f);
            if (worldDetailStages != null)
                for (int i = 0; i < worldDetailStages.Length; i++)
                    Show(worldDetailStages[i], time >= 212.48f + i * 5.12f);
            Show(originalAccessory, time >= 230.40f);
            if (originalAccessoryStars != null)
                for (int i = 0; i < originalAccessoryStars.Length; i++)
                    if (originalAccessoryStars[i] != null)
                        originalAccessoryStars[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.2f + i) * 9f);
            if (publishedMarks != null)
                for (int i = 0; i < publishedMarks.Length; i++) Show(publishedMarks[i], time >= 230.40f + i * .64f);
            SampleActors(time, active);
        }

        private void SampleCursors(float time)
        {
            if (localCursors != null)
                for (int i = 0; i < localCursors.Length; i++)
                {
                    Transform cursor = localCursors[i];
                    if (cursor == null) continue;
                    bool visible = time >= 197.12f;
                    Show(cursor.gameObject, visible);
                    float u = Ease(time, 202.24f, 204.80f);
                    float corrected = Ease(time, 210.56f, 212.48f);
                    cursor.localPosition = new Vector3(Mathf.Lerp(.45f, -.11f, corrected), Mathf.Lerp(-.32f, -.04f, u), -.058f);
                }
            if (remoteCursors != null)
                for (int i = 0; i < remoteCursors.Length; i++)
                {
                    Transform cursor = remoteCursors[i];
                    if (cursor == null) continue;
                    Show(cursor.gameObject, time >= 207.36f);
                    float u = Ease(time, 208f, 212.48f);
                    cursor.localPosition = new Vector3(Mathf.Lerp(-.56f, -.22f, u), Mathf.Lerp(.30f, .03f, u), -.062f);
                }
        }

        private void SampleLayerCards(float time)
        {
            if (layerCards == null || layerCardOrigins == null) return;
            float bad = Ease(time, 202.24f, 204.80f) * (1f - Ease(time, 210.56f, 213.12f));
            for (int i = 0; i < layerCards.Length; i++)
            {
                Transform card = layerCards[i];
                if (card == null || i >= layerCardOrigins.Length) continue;
                Vector3 p = layerCardOrigins[i];
                if (i % 3 == 2) p.z += .48f * bad;
                card.localPosition = p;
                card.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * .65f) * 2.1f, 0f);
            }
        }

        private void SampleActors(float time, bool active)
        {
            sampledVisibleActors = 0;
            if (actors == null || actorKeyOffsets == null || actorKeyCounts == null || actorKeyTimes == null) return;
            for (int i = 0; i < actors.Length; i++)
            {
                OWActorRig actor = actors[i];
                if (actor == null || i >= actorKeyOffsets.Length || i >= actorKeyCounts.Length) continue;
                bool visible = active && i < actorShowStarts.Length && time >= actorShowStarts[i] && time <= actorShowEnds[i];
                Show(actor.gameObject, visible);
                if (actorNameplates != null && i < actorNameplates.Length && actorNameplates[i] != null)
                    Show(actorNameplates[i].gameObject, visible);
                if (visible) sampledVisibleActors++;
                int first = actorKeyOffsets[i];
                int last = first + actorKeyCounts[i] - 1;
                int key = first;
                for (int k = first; k < last; k++) if (time >= actorKeyTimes[k]) key = k;
                int next = Mathf.Min(key + 1, last);
                float part = Mathf.Clamp01((time - actorKeyTimes[key]) / Mathf.Max(.01f, actorKeyTimes[next] - actorKeyTimes[key]));
                float move = Mathf.SmoothStep(0f, 1f, part);
                Vector3 delta = actorKeyPositions[next] - actorKeyPositions[key];
                Vector3 position = Vector3.Lerp(actorKeyPositions[key], actorKeyPositions[next], move);
                bool walking = delta.sqrMagnitude > .025f && part > .015f && part < .985f;
                actor.transform.localPosition = position;
                float yaw = Mathf.LerpAngle(actorKeyYaws[key], actorKeyYaws[next], move);
                if (walking)
                {
                    float facing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    float turn = Ease(part, 0f, .15f) * (1f - Ease(part, .78f, 1f));
                    yaw = Mathf.LerpAngle(yaw, facing, turn);
                }
                actor.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                int gesture = walking ? 6 : actorKeyGestures[part < .85f ? key : next];
                float phase = walking ? Mathf.Repeat(time * .95f + i * .17f, 1f) : .28f + .27f * (.5f + .5f * Mathf.Sin(time * 1.12f + i * 1.4f));
                if (sampledActorGestures != null && i < sampledActorGestures.Length) sampledActorGestures[i] = gesture;
                if (actor.animator != null && actor.animator.enabled)
                {
                    actor.animator.SetInteger("V4Pose", gesture);
                    actor.animator.SetFloat("V4PoseTime", phase);
                }
                if (actorNameplates != null && i < actorNameplates.Length && actorNameplates[i] != null)
                {
                    Transform plate = actorNameplates[i];
                    // Stable anonymous plate, matching the 1.25 m normalized V5 bodies.
                    plate.localPosition = position + new Vector3(0f, 1.48f, 0f);
                    Vector3 toward = plate.position - new Vector3(0f, 1.35f, -2.8f);
                    if (toward.sqrMagnitude > .01f) plate.rotation = Quaternion.LookRotation(toward, Vector3.up);
                }
                if (carriedBooks != null && i < carriedBooks.Length)
                    Show(carriedBooks[i], visible && !walking && (gesture == 2 || gesture == 3 || gesture == 11));
                if (contactTrays != null && i < contactTrays.Length)
                    Show(contactTrays[i], visible && !walking && (gesture == 1 || gesture == 5 || gesture == 10));
            }
        }
    }
}
