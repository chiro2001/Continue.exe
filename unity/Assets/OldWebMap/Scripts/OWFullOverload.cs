using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    // Persistent geometry/materials are authored by OWFullOverloadBuild. The full-film
    // presentation owns the clock. Seeking and reverse sampling require no history.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWFullOverload : UdonSharpBehaviour
    {
        public bool autoSample = false;
        public OWEpisodeDirector director;
        public Transform contentRoot;
        public Transform[] stageRoots;
        public Transform[] focusRoots;
        public Material[] focusMaterials;
        public Material[] fieldMaterials;
        public Transform sourceCredit;
        public Material sourceCreditMaterial;
        public Transform cursor;
        public Material cursorMaterial;
        public Transform clickRing;
        public Material clickMaterial;
        public Transform busySpinner;
        public Material busyMaterial;
        public Transform maintenanceLamp;
        public Material lampGlowMaterial;
        public Material[] furnitureMaterials;
        public OWActorRig[] actors;
        public Vector3[] actorPositions;
        public float[] actorYaws;
        public Transform[] actorNameplates;
        public Material[] actorScreenMaterials;
        public int[] sampledActorGestures;
        public int sampledVisibleActors;
        public float startTime = 133.12f;
        public float endTime = 186.88f;
        public Vector3 focusHome = new Vector3(0f, 2.2f, 5.4f);
        public float sampledTime;
        public int sampledStage = -1;
        public int sampledFocus = -1;
        public int sampledFailure = -1;
        public float sampledOpacity;
        public float sampledMotionTime;
        public bool sampledRepairComplete;
        public bool sampledQueueFrozen;

        private void LateUpdate()
        {
            if (autoSample && director != null) Sample(director.Elapsed());
        }

        public void Seek(float time) { Sample(time); }

        public void Sample(float time)
        {
            sampledTime = time;
            bool active = time >= startTime && time < endTime;
            if (contentRoot != null) contentRoot.gameObject.SetActive(active);
            int stage = !active ? -1 : time < 143.36f ? 0 : time < 153.60f ? 1 : time < 168.96f ? 2 : 3;
            sampledStage = stage;
            sampledFailure = stage != 3 ? -1 : time < 173.44f ? 0 : time < 177.92f ? 1 : time < 182.40f ? 2 : 3;
            sampledRepairComplete = time >= 159.36f && time < 163.84f;
            sampledQueueFrozen = time >= 163.84f && time < 166.40f;
            sampledFocus = stage < 0 ? -1 : stage == 0 ? 0 : stage == 1 ? 1 : stage == 2 ? time >= 166.40f ? 8 : time >= 159.36f ? 3 : 2 : 4 + sampledFailure;
            sampledOpacity = active ? Window(time, startTime, endTime, .48f, .64f) : 0f;
            float motionTime = time;
            if (stage == 2 && time >= 163.84f) motionTime = time < 166.40f ? 163.84f : time - 2.56f;
            sampledMotionTime = motionTime;
            if (stageRoots != null)
                for (int i = 0; i < stageRoots.Length; i++)
                    if (stageRoots[i] != null) stageRoots[i].gameObject.SetActive(i == stage);
            SampleActors(time, active);
            if (stage < 0)
            {
                if (cursor != null) { cursor.gameObject.SetActive(false); cursor.localPosition = Vector3.zero; }
                if (clickRing != null) clickRing.gameObject.SetActive(false);
                if (busySpinner != null) busySpinner.gameObject.SetActive(false);
                if (sourceCredit != null) sourceCredit.gameObject.SetActive(false);
                if (maintenanceLamp != null) maintenanceLamp.gameObject.SetActive(false);
                return;
            }
            if (focusRoots == null || focusRoots.Length < 4 || focusMaterials == null || focusMaterials.Length < 4) return;

            float stageStart = stage == 0 ? 133.12f : stage == 1 ? 143.36f : stage == 2 ? 153.60f : 168.96f;
            float stageEnd = stage == 0 ? 143.36f : stage == 1 ? 153.60f : stage == 2 ? 168.96f : 186.88f;
            float envelope = Window(time, stageStart, stageEnd, .4f, .4f);
            float focusOpacity = envelope;
            Vector3 position = focusHome;
            Quaternion rotation = Quaternion.identity;
            if (stage == 0)
            {
                position.z += .65f * (1f - Ease((time - 133.12f) / .96f));
                rotation = Quaternion.Euler(0f, -2f * (1f - Ease((time - 133.12f) / 1.28f)), 0f);
            }
            if (stage == 1)
            {
                position.z -= .9f * Ease((time - 143.36f) / 7.68f);
                // The readable panel remains stable while the side results keep advancing.
                rotation = Quaternion.Euler(0f, 0f, .14f * Mathf.Sin((time - 143.36f) * 2.454369f));
            }
            if (stage == 2)
            {
                if (sampledQueueFrozen) focusOpacity *= .055f;
                if (time >= 166.40f) focusOpacity *= Ease((time - 166.40f) / .48f);
            }
            if (stage == 3)
            {
                float failureStart = 168.96f + sampledFailure * 4.48f;
                focusOpacity = Window(time, failureStart, failureStart + 4.48f, .32f, .40f);
                float retreat = Ease((time - 185.60f) / 1.28f);
                position.z += retreat * 1.4f;
                position.y += retreat * .14f;
            }
            Transform focus = focusRoots[stage];
            if (focus != null)
            {
                focus.localPosition = position;
                focus.localRotation = rotation;
                focus.localScale = Vector3.one;
            }
            Material focusMaterial = focusMaterials[stage];
            if (focusMaterial != null)
            {
                focusMaterial.SetVector("_Rect", AtlasRect(sampledFocus, 2, 5));
                focusMaterial.SetFloat("_Opacity", focusOpacity);
                focusMaterial.SetFloat("_Timeline", time);
                focusMaterial.SetFloat("_Activity", stage == 1 ? .5f : sampledRepairComplete ? .16f : .08f);
            }
            if (fieldMaterials != null)
                for (int i = 0; i < fieldMaterials.Length; i++)
                {
                    Material material = fieldMaterials[i];
                    if (material == null) continue;
                    float opacity = i == stage ? envelope : 0f;
                    if (i == 2 && sampledQueueFrozen) opacity *= .018f;
                    if (i == 3) opacity *= 1f - Ease((time - 181.76f) / 4.48f);
                    material.SetFloat("_Opacity", opacity);
                    material.SetFloat("_Timeline", time);
                    material.SetFloat("_MotionTime", motionTime);
                }
            if (furnitureMaterials != null)
                for (int i = 0; i < furnitureMaterials.Length; i++)
                    if (furnitureMaterials[i] != null) furnitureMaterials[i].SetFloat("_Opacity", stage == 2 && sampledQueueFrozen ? .10f : 1f);
            SampleCredit(time, envelope);
            SampleCursor(time, stage, position, rotation, focusOpacity);
            if (maintenanceLamp != null) maintenanceLamp.gameObject.SetActive(stage == 2);
            if (lampGlowMaterial != null)
            {
                lampGlowMaterial.SetFloat("_Opacity", stage == 2 ? envelope * (sampledQueueFrozen ? .84f : .45f) : 0f);
                lampGlowMaterial.SetFloat("_Timeline", time);
            }
        }

        private void SampleActors(float time, bool active)
        {
            sampledVisibleActors = 0;
            if (actors == null || actorPositions == null || actorYaws == null) return;
            for (int i = 0; i < actors.Length; i++)
            {
                OWActorRig actor = actors[i];
                if (actor == null || i >= actorPositions.Length || i >= actorYaws.Length) continue;
                float leave = i == 0 ? 182.40f : i == 1 ? 184f : 186.24f;
                bool visible = active && time < leave && (!sampledQueueFrozen || i == 2);
                actor.gameObject.SetActive(visible);
                actor.transform.localPosition = actorPositions[i];
                actor.transform.localRotation = Quaternion.Euler(0f, actorYaws[i], 0f);
                if (visible) sampledVisibleActors++;
                int gesture = active ? ActorGesture(time, i) : 0;
                // Every motion is sampled from the same absolute clock, including a
                // genuinely held pose during the local queue freeze and final pause.
                float phase = sampledQueueFrozen || time >= 182.40f || !active ? .38f :
                    .28f + .27f * (.5f + .5f * Mathf.Sin(time * 1.12f + i * 1.4f));
                if (sampledActorGestures != null && i < sampledActorGestures.Length) sampledActorGestures[i] = gesture;
                if (actor.animator != null)
                {
                    actor.animator.SetInteger("V4Pose", gesture);
                    actor.animator.SetFloat("V4PoseTime", phase);
                }
                if (actorNameplates != null && i < actorNameplates.Length && actorNameplates[i] != null)
                {
                    Transform plate = actorNameplates[i];
                    plate.gameObject.SetActive(visible);
                    plate.localPosition = actorPositions[i] + new Vector3(0f, 1.48f, 0f);
                    Vector3 away = plate.position - new Vector3(0f, 1.35f, -2.8f);
                    away.y = 0f;
                    if (away.sqrMagnitude > .001f) plate.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
                if (actorScreenMaterials != null && i < actorScreenMaterials.Length && actorScreenMaterials[i] != null)
                {
                    int screen = sampledStage == 3 ? 4 + sampledFailure : i == 2 && sampledStage == 2 ?
                        time >= 166.40f ? 8 : time >= 159.36f ? 3 : 2 : sampledStage == 1 ? 1 : i == 2 ? 2 : 0;
                    actorScreenMaterials[i].SetVector("_Rect", AtlasRect(screen, 2, 5));
                    actorScreenMaterials[i].SetFloat("_Opacity", visible ? sampledQueueFrozen ? .08f : .96f : 0f);
                    actorScreenMaterials[i].SetFloat("_Timeline", time);
                }
            }
        }

        private int ActorGesture(float time, int actor)
        {
            if (sampledQueueFrozen || time >= 182.40f) return 0;
            if (actor == 0)
            {
                if (time < 136.32f) return 1;
                if (time < 139.52f) return 10;
                if (time < 143.36f) return 3;
                if (time < 148.48f) return 4;
                if (time < 153.60f) return 5;
                if (time < 161.92f) return 1;
                if (time < 168.96f) return 5;
                return time < 177.92f ? 10 : 5;
            }
            if (actor == 1)
            {
                if (time < 138.24f) return 1;
                if (time < 143.36f) return 5;
                if (time < 146.56f) return 4;
                if (time < 153.60f) return 8;
                if (time < 163.84f) return 1;
                if (time < 168.96f) return 4;
                if (time < 173.44f) return 5;
                return time < 177.92f ? 4 : 0;
            }
            if (time < 143.36f) return 5;
            if (time < 153.60f) return 1;
            if (time < 154.88f) return 5;
            if (time < 158.88f) return 10;
            if (time < 159.36f) return 1;
            if (time < 161.28f) return 3;
            if (time < 163.84f) return 1;
            if (time < 168.96f) return 5;
            if (time < 173.44f) return 1;
            return time < 177.92f ? 10 : 5;
        }

        private void SampleCredit(float time, float envelope)
        {
            if (sourceCredit == null) return;
            float drop = Ease((time - 135.68f) / 2.56f);
            bool visible = sampledStage == 0 && drop < .999f;
            sourceCredit.gameObject.SetActive(visible);
            sourceCredit.localPosition = new Vector3(-.12f + drop * .42f, -1.24f - 1.5f * drop * drop, -.03f - drop * .3f);
            sourceCredit.localRotation = Quaternion.Euler(drop * 18f, drop * -9f, drop * 12f);
            if (sourceCreditMaterial != null)
            {
                sourceCreditMaterial.SetFloat("_Opacity", envelope * (1f - drop));
                sourceCreditMaterial.SetFloat("_Timeline", time);
            }
        }

        private void SampleCursor(float time, int stage, Vector3 position, Quaternion rotation, float opacity)
        {
            if (cursor == null) return;
            Vector3 point = new Vector3(1.82f, -1.12f, -.07f);
            float cycleStart = stage == 0 ? 133.12f : stage == 1 ? 143.36f : stage == 2 ? 153.60f : 168.96f + sampledFailure * 4.48f;
            float cycle = Mathf.Repeat(time - cycleStart, 2.56f);
            float click = 1f - Ease((cycle - .96f) / .32f);
            click *= Ease((cycle - .88f) / .08f);
            bool busy = false;
            if (stage == 1)
            {
                int result = Mathf.FloorToInt((time - 143.36f) / 2.56f) % 3;
                point = Vector3.Lerp(new Vector3(2.12f, 1.02f, -.07f), new Vector3(2.03f, .32f - result * .49f, -.07f), Ease(cycle / .8f));
                busy = cycle > 1.12f;
            }
            if (stage == 2)
            {
                point = Vector3.Lerp(new Vector3(.85f, -.12f, -.07f), new Vector3(1.8f, -.38f, -.07f), Ease((time - 154.88f) / 2.56f));
                click = 1f - Ease((time - 158.88f) / .32f);
                click *= Ease((time - 158.80f) / .08f);
                if (time >= 159.36f) point = new Vector3(1.65f, -.80f, -.07f);
                if (time >= 166.40f) { point = new Vector3(.02f, .16f, -.07f); busy = true; }
            }
            if (stage == 3)
            {
                point = new Vector3(sampledFailure == 2 ? -1.42f : 1.1f, sampledFailure == 2 ? -.72f : -.35f, -.07f);
                busy = cycle > 1.12f;
            }
            bool shown = !sampledQueueFrozen;
            cursor.gameObject.SetActive(shown);
            cursor.localPosition = position + rotation * point;
            cursor.localRotation = rotation;
            cursor.localScale = Vector3.one * (1f - click * .08f);
            if (cursorMaterial != null) cursorMaterial.SetFloat("_Opacity", opacity * .98f);
            if (clickRing != null)
            {
                clickRing.gameObject.SetActive(shown && click > .001f);
                clickRing.localPosition = position + rotation * (point + new Vector3(-.045f, .045f, -.006f));
                clickRing.localRotation = rotation;
                clickRing.localScale = Vector3.one * (.55f + (1f - click) * 1.25f);
            }
            if (clickMaterial != null) clickMaterial.SetFloat("_Opacity", opacity * click * .8f);
            if (busySpinner != null)
            {
                busySpinner.gameObject.SetActive(shown && busy);
                busySpinner.localPosition = position + rotation * (point + new Vector3(.22f, .02f, -.01f));
                busySpinner.localRotation = rotation * Quaternion.Euler(0f, 0f, -Mathf.Floor((time - cycleStart) / .08f) * 30f);
            }
            if (busyMaterial != null) busyMaterial.SetFloat("_Opacity", opacity * .85f);
        }

        private Vector4 AtlasRect(int index, int columns, int rows)
        {
            return new Vector4((index % columns) / (float)columns, 1f - (index / columns + 1) / (float)rows, 1f / columns, 1f / rows);
        }
        private float Window(float time, float begin, float end, float fadeIn, float fadeOut)
        {
            return Ease((time - begin) / fadeIn) * (1f - Ease((time - end + fadeOut) / fadeOut));
        }
        private float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
    }
}
