using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWFilmV4Presentation : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWFilmV4Details details;
        public OWFilmV4BasePresentation basis;
        public OWFilmV4AudioFloor audioFloor;
        public OWFilmV4Media media;
        public Transform workspaceScreen;
        public Transform workspacePlayControl;
        public bool offlinePreview;
        public GameObject[] frontActors;
        public GameObject[] laterActors;
        public Transform cylinder;
        public Transform[] cylinderBands;
        public GameObject editorStage;
        public Transform editorFrame;
        public Transform workspace;
        public Transform miniature;
        public Transform finalWorld;
        public Material[] screenshotMaterials;
        public Texture2D[] screenshotStates;
        public float[] screenshotTimes;
        public Material fieldMaterial;
        public Material openingMaterial;
        public Material[] openingMaterials;
        public GameObject overloadField;
        public Transform archiveFocus;
        public Transform archiveMain;
        public Transform archiveDetail;
        public Material archiveMainMaterial;
        public Material archiveDetailMaterial;
        public Texture2D[] archiveTextures;
        public float[] archiveAspects;
        public string[] archiveNames;
        public int[] archiveSequence;
        public int[] archiveCompanionSequence;
        public TMP_Text archiveFile;
        public Transform archivePointer;
        public Texture2D[] failureStates;
        public Texture2D[] failureDetails;
        public float[] failureDetailAspects;
        public int sampledFailure = -1;
        public Transform barrierRoot;
        public Transform[] barrierSlats;
        public GameObject climaxEffects;
        public Material contourMaterial;
        public Transform[] channelEffects;
        public ParticleSystem[] channelParticles;
        public Material[] channelParticleMaterials;
        public Light[] channelLights;
        public Transform hubDetails;
        public Transform[] layerPanels;
        public Vector3[] layerHomes;
        public TMP_Text sourceStatus;
        public GameObject sourceStatusRoot;
        public TMP_Text finalWord;
        public GameObject finalWordRoot;
        public GameObject nativeRoot;
        public Renderer nativeMain;
        public Renderer nativeSecondary;
        public Material nativeMainMaterial;
        public Material nativeSecondaryMaterial;
        public Texture2D[] nativeStates;
        public Texture2D[] nativeCompanions;
        public float[] nativeStarts;
        public float[] nativeEnds;
        public int[] nativeOffsets;
        public TMP_Text nativeStatus;
        public Transform nativePointer;
        public Vector3[] nativePointerTargets;
        public float[] nativeCompanionAspects;
        public Texture2D[] accessHistory;
        public float[] accessHistoryAspects;
        public string[] accessHistoryLabels;
        public Material editorToolbarMaterial;
        public Texture2D editorToolbarIdle;
        public Texture2D editorToolbarPlaying;
        public Transform editorPlayPointer;
        public Vector3 editorPlayTarget;
        public Material worldTopologyMaterial;
        public Material[] worldColorMaterials;
        public Color[] worldOriginalColors;
        public Material[] workflowMaterials;
        public Texture2D[] workflowStates;
        public float sampledTime;
        public int sampledState;
        public int sampledNative = -1;
        public int sampledImage = -1;
        public float sampledWorldScale;
        public int visibleActors;
        public int sampledArchive = -1;
        public Vector4 sampledAccents;

        private void Start() { offlinePreview = false; if (basis != null) basis.drivenExternally = true; }
        private void LateUpdate()
        {
            if (offlinePreview || basis == null || director == null) return;
            Apply(director.Elapsed(), director.IsPlaying());
            if (audioFloor != null) audioFloor.Sync(director.Elapsed(), director.IsPlaying());
        }
        public void Preview(float time) { offlinePreview = true; Apply(time, true); }
        public void Waiting() { offlinePreview = true; Apply(0f, false); }
        public void Finished() { offlinePreview = true; Apply(director == null ? 256f : director.duration, false); }
        public void Apply(float time, bool playing)
        {
            sampledTime = time; float duration=director==null?256f:director.duration; bool show = playing && time < duration;
            sampledState = show ? 1 : time >= duration - .01f ? 2 : 0;
            basis.Apply(time, show);
            var legacy = basis.legacy;
            Show(legacy.captionRoot, false);
            if (!show)
            {
                if (legacy.menuTitle != null) legacy.menuTitle.text = sampledState == 2 ? "继续" : "FC14";
                if (legacy.menuBody != null) legacy.menuBody.text = sampledState == 2 ? "" : "Future Composer 1.4";
                if (legacy.playLabel != null) legacy.playLabel.text = sampledState == 2 ? "重播" : "开始";
            }
            if (legacy.montage.keyRoots != null) for (int i = 0; i < legacy.montage.keyRoots.Length; i++) Show(legacy.montage.keyRoots[i].gameObject, false);
            Show(basis.keyRoot, false);
            if (basis.commentRoots != null) for (int i = 0; i < basis.commentRoots.Length; i++) Show(basis.commentRoots[i].gameObject, false);
            if (legacy.montage.typeRegions != null) for (int i = 0; i < legacy.montage.typeRegions.Length; i++) Show(legacy.montage.typeRegions[i].gameObject, false);
            visibleActors = 0; bool vrc = show && time >= 77.44f && time < 102.4f;
            if (frontActors != null) for (int i = 0; i < frontActors.Length; i++) { Show(frontActors[i], vrc); if (vrc) visibleActors++; }
            if (laterActors != null) for (int i = 0; i < laterActors.Length; i++) Show(laterActors[i], false);
            if (!vrc && legacy.nameplateRoot != null) Show(legacy.nameplateRoot, false);
            if (basis.renewal != null) Show(basis.renewal.contentRoot, false);

            SampleWorld(time, show);
            if (vrc && legacy.cast != null && legacy.cast.nameplates != null) legacy.cast.nameplates.Sample(time);
            // The old category greetz is a quiet cracktro layer. Source documents
            // carry the foreground typography in the rest of the performance.
            var sig = basis.signal;
            if (sig != null)
            {
                float credit = show && time < 30.72f ? .40f : 0f;
                if (sig.greetz != null && sig.greetz.ringMaterial != null) sig.greetz.ringMaterial.SetFloat("_Opacity", credit);
                if (sig.continuationGreetzMaterial != null) sig.continuationGreetzMaterial.SetFloat("_Opacity", credit);
                if (credit < .001f) { if (sig.greetz != null && sig.greetz.ringRenderer != null) sig.greetz.ringRenderer.enabled = false; if (sig.continuationGreetzRenderer != null) sig.continuationGreetzRenderer.enabled = false; }
                if (sig.tracker != null) sampledAccents = sig.tracker.sampledFlash;
            }

            Show(cylinder == null ? null : cylinder.gameObject, show && (time < 30.72f || time >= 122.88f && time < 133.12f || time >= 235.52f));
            if (cylinder != null) cylinder.localRotation = Quaternion.Euler(0f, 0f, 2.5f * Mathf.Sin(time * .4f));
            if (cylinderBands != null) for (int i = 0; i < cylinderBands.Length; i++)
                if (cylinderBands[i] != null) cylinderBands[i].localRotation = Quaternion.Euler(0f, i * 11f + Mathf.Sin(time * .32f + i) * 5f, 0f);
            if (legacy.montage.typeMaterial != null) legacy.montage.typeMaterial.SetFloat("_Pulse", (sampledAccents.x + sampledAccents.z) * .35f);
            if (openingMaterial != null) openingMaterial.SetFloat("_Timeline", Time.time);
            if (openingMaterials != null) for (int i = 0; i < openingMaterials.Length; i++) if (openingMaterials[i] != null) openingMaterials[i].SetFloat("_Timeline", Time.time);

            bool overload = show && time >= 133.12f && time < 186.88f;
            Show(overloadField, overload);
            if (fieldMaterial != null)
            {
                fieldMaterial.SetFloat("_Timeline", time);
                fieldMaterial.SetFloat("_Density", Ease((time - 133.12f) / 17.92f));
                fieldMaterial.SetFloat("_Pressure", Ease((time - 143.36f) / 2.56f) * (1f - Ease((time - 168.96f) / 5.12f)));
                fieldMaterial.SetFloat("_Fade", overload ? 1f - Ease((time - 185.6f) / 1.28f) : 0f);
                fieldMaterial.SetVector("_Pulse", sampledAccents);
            }

            bool editing = (show && time >= 197.12f) || sampledState == 2;
            Show(editorStage, editing);
            float unfold = Ease((time - 217.6f) / 5.12f);
            if (workspace != null)
            {
                workspace.localScale = Vector3.one * (2.4f * Ease((time - 197.12f) / 2.56f));
                workspace.localPosition = Vector3.Lerp(new Vector3(0f, 0f, 4.6f), new Vector3(-4.8f, 0f, 3.6f), unfold);
                workspace.localRotation = Quaternion.Euler(0f, -22f * unfold, 0f);
            }
            Vector3 screenOrigin=workspaceScreen==null?new Vector3(0,1.20f,4.6f):editorStage.transform.InverseTransformPoint(workspaceScreen.position);
            if (editorFrame != null)
            {
                Show(editorFrame.gameObject, show && time>=217.6f && time < 224f);
                editorFrame.localPosition = Vector3.Lerp(screenOrigin, new Vector3(0f, 2.1f, 1.3f), unfold);
                editorFrame.localScale = Vector3.one * Mathf.Lerp(.22f, 6.8f, unfold);
                editorFrame.localRotation = Quaternion.Euler(-8f * Mathf.Sin(unfold * Mathf.PI), -24f * (1f - unfold), 3f * Mathf.Sin(unfold * Mathf.PI));
            }
            sampledWorldScale = Mathf.Lerp(.004f, 1.6f, unfold);
            if (miniature != null) Show(miniature.gameObject,false);
            if (finalWorld != null)
            {
                Show(finalWorld.gameObject, editing && time >= 217.6f);
                finalWorld.localScale = Vector3.one * sampledWorldScale;
                finalWorld.localPosition = Vector3.Lerp(screenOrigin, new Vector3(0f, -.5484f, .5f), unfold);
                finalWorld.localRotation = Quaternion.Euler(-12f*Mathf.Sin(unfold*Mathf.PI), Mathf.Lerp(32f, -8f, unfold), 9f*Mathf.Sin(unfold*Mathf.PI));
            }
            if (worldColorMaterials != null && worldOriginalColors != null) for (int i = 0; i < worldColorMaterials.Length; i++) if (worldColorMaterials[i] != null && i < worldOriginalColors.Length)
            {
                Color original = worldOriginalColors[i]; float gray = original.r * .299f + original.g * .587f + original.b * .114f;
                Color blueprint = new Color(gray * .65f, gray * .85f, gray + .08f, original.a);
                worldColorMaterials[i].color = Color.Lerp(blueprint, original, editing ? unfold : 1f);
            }
            if (worldTopologyMaterial != null)
            {
                float wire = Ease((time - 216.32f) / 1.28f) * (1f - Ease((time - 222.72f) / 1.28f));
                worldTopologyMaterial.SetFloat("_Opacity", show ? .78f * wire : 0f); worldTopologyMaterial.SetFloat("_Timeline", time); worldTopologyMaterial.SetVector("_Pulse", sampledAccents);
            }
            sampledImage = -1;
            if (screenshotTimes != null) for (int i = 0; i < screenshotTimes.Length; i++) if (time >= screenshotTimes[i]) sampledImage = i;
            if (sampledImage >= 0 && screenshotStates != null && sampledImage < screenshotStates.Length && screenshotMaterials != null)
                for (int i = 0; i < screenshotMaterials.Length; i++) if (screenshotMaterials[i] != null) screenshotMaterials[i].mainTexture = screenshotStates[sampledImage];
            if (workflowMaterials != null && workflowStates != null && workflowStates.Length > 0)
                for (int i = 0; i < workflowMaterials.Length; i++) if (workflowMaterials[i] != null)
                {
                    int state = time >= 235.52f ? Mathf.FloorToInt((time - 235.52f) / .64f) + i * 2 : i;
                    workflowMaterials[i].mainTexture = workflowStates[Mathf.Max(0, state) % workflowStates.Length];
                }
            if (layerPanels != null) for (int i = 0; i < layerPanels.Length; i++)
            {
                var panel = layerPanels[i]; if (panel == null || layerHomes == null || i >= layerHomes.Length) continue;
                Show(panel.gameObject, editing && time >= 199.68f + i * .64f);
                Vector3 expanded = i == 0 ? new Vector3(-7f,4.1f,3f) : i == 1 ? new Vector3(0f,5.4f,9f) : new Vector3(7f,3.9f,3f);
                panel.localPosition = Vector3.Lerp(layerHomes[i], expanded, unfold) + new Vector3(Mathf.Sin(time * .7f + i) * .06f, Mathf.Sin(time * .9f + i) * .05f, .10f * Mathf.Sin(time * .5f + i));
                panel.localRotation = Quaternion.Euler(0f, (i - 1f) * -40f * unfold + Mathf.Sin(time * .4f + i) * 4f, (i - 1f) * 2f);
            }
            if (editorToolbarMaterial != null) editorToolbarMaterial.mainTexture = time >= 217.6f ? editorToolbarPlaying : editorToolbarIdle;
            if (editorPlayPointer != null)
            {
                Show(editorPlayPointer.gameObject, show && time >= 215.68f && time < 218.24f);
                if(workspacePlayControl!=null)editorPlayPointer.position=workspacePlayControl.position+new Vector3(.28f,-.18f,-.02f)*(1f-Ease((time-215.68f)/1.6f))+Vector3.back*.015f;
                editorPlayPointer.rotation=workspace==null?Quaternion.identity:workspace.rotation;
                editorPlayPointer.localScale = Vector3.one * (.09f * (1f - .18f * Mathf.Exp(-Mathf.Abs(time - 217.6f) * 22f)));
            }
            Show(sourceStatusRoot, show && time >= 197.12f && time < 222.72f);
            if (sourceStatus != null && time >= 197.12f) sourceStatus.text = time < 202.24f ? "Sources" : time < 207.36f ? "scene_block.blend" : time < 212.48f ? "OWM_FullFilm_v4.unity" : time < 217.6f ? "Scene   |   Game" : "▶";
            Show(finalWordRoot, show && time >= 250.88f && time < 253.44f);
            if (finalWord != null) finalWord.text = "继续";
            SampleNative(time, show);
            SampleArchive(time, show);
            SampleClimax(time, show);
            if (show && legacy.cameraFx != null && legacy.cameraFx.effectMaterial != null && time >= 133.12f)
            {
                float fault = Window(time,166.4f,166.72f) + Window(time,177.92f,178.24f) + Window(time,217.6f,217.76f);
                legacy.cameraFx.effectMaterial.SetFloat("_Dropout", fault * .38f);
                legacy.cameraFx.effectMaterial.SetFloat("_Chroma", .00012f + fault * .0012f);
            }
            if(details!=null)details.Sample(time,show,sampledState);
            if(media!=null)media.Sample(time,show,sampledState);
        }
        private void SampleNative(float time, bool show)
        {
            sampledNative = -1;
            if (show && nativeStarts != null && nativeEnds != null) for (int i = 0; i < nativeStarts.Length; i++)
                if (time >= nativeStarts[i] && time < nativeEnds[i]) sampledNative = i;
            Show(nativeRoot, sampledNative >= 0);
            if (sampledNative < 0 || nativeOffsets == null || nativeStates == null) return;
            int region = basis.legacy.journey.Region(time);
            nativeRoot.transform.localPosition = basis.legacy.journey.RegionOffset(region);
            var montage = basis.legacy.montage;
            for (int i = 0; i < montage.windows.Length; i++)
            {
                int at = montage.slotFirst[i]; int count = montage.slotCount[i];
                for (int j = at; j < at + count; j++)
                    if (time >= montage.starts[j] && time < montage.ends[j] && montage.roles[j] == 1) Show(montage.windows[i].gameObject, false);
            }
            if (basis.legacy.operations.groupRoots != null) for (int i = 0; i < basis.legacy.operations.groupRoots.Length; i++) Show(basis.legacy.operations.groupRoots[i].gameObject, false);
            int state = Mathf.Clamp(Mathf.FloorToInt((time - nativeStarts[sampledNative]) / 2.56f), 0, 3);
            int index = nativeOffsets[sampledNative] + state;
            if (index < nativeStates.Length && nativeMainMaterial != null)
            {
                nativeMainMaterial.mainTexture = nativeStates[index];
                nativeMainMaterial.SetTexture("_PreviousTex", nativeStates[nativeOffsets[sampledNative] + Mathf.Max(0, state - 1)]);
                nativeMainMaterial.SetFloat("_Blend", Mathf.Clamp01((time - nativeStarts[sampledNative] - state * 2.56f) / .42f));
                nativeMainMaterial.SetFloat("_Timeline", time);
                nativeMainMaterial.SetFloat("_Activity", .6f);
            }
            if (nativeCompanions != null && index < nativeCompanions.Length && nativeSecondaryMaterial != null)
            {
                nativeSecondaryMaterial.mainTexture = nativeCompanions[index]; nativeSecondaryMaterial.SetTexture("_PreviousTex", nativeCompanions[index]);
                float progress = Ease(Mathf.Repeat(time - nativeStarts[sampledNative], 1.28f) / .68f);
                nativeSecondaryMaterial.SetVector("_Rect", new Vector4(0f, .26f * progress, 1f, .74f));
                nativeSecondaryMaterial.SetFloat("_Activity", .75f); nativeSecondaryMaterial.SetFloat("_Timeline", time);
            }
            if (nativeSecondary != null)
            {
                float aspect = nativeCompanionAspects != null && index < nativeCompanionAspects.Length ? nativeCompanionAspects[index] : 1.6f;
                float width = Mathf.Min(2.1f, 1.8f * aspect);
                nativeSecondary.transform.localScale = new Vector3(width, width / aspect * .74f, 1f);
                nativeSecondary.transform.localPosition = new Vector3(2.38f, 2.35f + .025f * Mathf.Sin(time * 3f), 5.45f);
            }
            if (nativePointer != null && nativePointerTargets != null && sampledNative < nativePointerTargets.Length)
            {
                float action = Mathf.Repeat(time - nativeStarts[sampledNative], 2.56f);
                nativePointer.localPosition = Vector3.Lerp(new Vector3(1.1f, 1.8f, 5.39f), nativePointerTargets[sampledNative], Ease(action / .68f));
                nativePointer.localScale = Vector3.one * (.22f * (1f - .13f * Mathf.Exp(-Mathf.Abs(action - .80f) * 26f)));
            }
            if (sampledNative == 5 && accessHistory != null && accessHistory.Length > 0)
            {
                int page = Mathf.Clamp(Mathf.FloorToInt((time - 104.96f) / 3.584f), 0, accessHistory.Length - 1);
                nativeSecondaryMaterial.mainTexture = accessHistory[page]; nativeSecondaryMaterial.SetTexture("_PreviousTex", accessHistory[page]); nativeSecondaryMaterial.SetVector("_Rect", new Vector4(0,0,1,1));
                nativeSecondary.transform.localScale = new Vector3(2.18f, Mathf.Min(2.15f, 2.18f / accessHistoryAspects[page]), 1f);
            }
            if (nativeStatus != null)
            {
                int tick = Mathf.FloorToInt((time - nativeStarts[sampledNative]) / .32f);
                nativeStatus.text = sampledNative == 0 ? "README.TXT" : sampledNative == 1 ? "Layer " + (tick / 4 % 4 + 1) + " / 4" : sampledNative == 2 ? "Pieces " + Mathf.Min(16, tick + 1) + "/16" : sampledNative == 3 ? "Re: " + (tick + 1) : sampledNative == 4 ? "00:00:" + (10 + tick) + "." + (tick % 8) + "0" : "";
                if (sampledNative == 5 && accessHistoryLabels != null && accessHistoryLabels.Length > 0) nativeStatus.text = accessHistoryLabels[Mathf.Clamp(Mathf.FloorToInt((time - 104.96f) / 3.584f), 0, accessHistoryLabels.Length - 1)];
            }
        }
        private void SampleWorld(float time, bool show)
        {
            var journey = basis.legacy.journey; if (journey == null || journey.worldFrame == null) return;
            float a = Window(time, 79.36f, 81.92f), b = Window(time, 86.4f, 88.32f), c = Window(time, 102.4f, 104.96f);
            float yaw = -9f * a + 5.5f * b - 7f * c;
            float roll = 2.6f * b - 1.7f * c;
            float scale = 1f - .11f * a + .05f * c;
            var rotation = Quaternion.Euler(2.4f * a, yaw, roll);
            Vector3 eye = new Vector3(0f, 1.35f, -2.8f);
            journey.worldFrame.localRotation = rotation; journey.worldFrame.localScale = Vector3.one * scale;
            journey.worldFrame.localPosition = eye - rotation * ((eye + journey.sampledVirtualPosition) * scale);
            var signal = basis.signal;
            if (signal != null && signal.movingStarsRoot != null) signal.movingStarsRoot.SetPositionAndRotation(journey.worldFrame.position, journey.worldFrame.rotation);
            if (signal != null && signal.oscilloscope != null)
            {
                var osc = signal.oscilloscope;
                for (int ch = 0; ch < 4; ch++)
                {
                    Vector3 physical = signal.SourceWorldPosition(time, ch);
                    if (time < 133.12f && osc.waves != null && ch < osc.waves.Length && osc.waves[ch] != null) osc.waves[ch].position = physical;
                    if (osc.music != null && osc.music.sources != null && ch < osc.music.sources.Length) osc.music.sources[ch].transform.position = physical;
                    if (osc.particleAnchors != null && ch < osc.particleAnchors.Length && osc.particleAnchors[ch] != null && time < 133.12f) osc.particleAnchors[ch].position = physical;
                }
            }
            Show(hubDetails == null ? null : hubDetails.gameObject, show && time >= 79.36f && time < 102.4f);
            Show(barrierRoot == null ? null : barrierRoot.gameObject, show && time >= 104.96f && time < 133.12f);
            if (barrierRoot != null)
            {
                barrierRoot.localPosition = journey.RegionOffset(3);
                float closed = Ease((time - 104.96f) / 12.8f);
                if (barrierSlats != null) for (int i = 0; i < barrierSlats.Length; i++) if (barrierSlats[i] != null)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    barrierSlats[i].localPosition = new Vector3(side * Mathf.Lerp(5.5f, 3.1f + (i / 2) * .32f, closed), 2.1f, 6.2f + (i / 2) * 1.4f);
                    barrierSlats[i].localRotation = Quaternion.Euler(0f, side * Mathf.Lerp(84f, 32f, closed), side * 1.8f * Mathf.Sin(time + i));
                }
            }
        }
        private void SampleArchive(float time, bool show)
        {
            bool active = show && time >= 133.12f && time < 186.88f;
            Show(archiveFocus == null ? null : archiveFocus.gameObject, active);
            sampledArchive = -1;
            sampledFailure = -1;
            if (!active || archiveSequence == null || archiveSequence.Length == 0 || archiveTextures == null) return;
            if (basis.overload != null) Show(basis.overload.contentRoot == null ? null : basis.overload.contentRoot.gameObject, false);
            if (time >= 168.96f && failureStates != null && failureStates.Length == 4) { SampleFailure(time); return; }
            int eventIndex = Mathf.Clamp(Mathf.FloorToInt((time - 133.12f) / 3.84f), 0, archiveSequence.Length - 1);
            int image = archiveSequence[eventIndex], companion = archiveCompanionSequence[eventIndex]; sampledArchive = image;
            if (image < 0 || image >= archiveTextures.Length || companion < 0 || companion >= archiveTextures.Length) return;
            float age = time - 133.12f - eventIndex * 3.84f;
            float envelope = Ease(age / .30f) * (1f - Ease((age - 3.55f) / .29f));
            if (archiveMainMaterial != null)
            {
                archiveMainMaterial.mainTexture = archiveTextures[image]; archiveMainMaterial.SetTexture("_PreviousTex", archiveTextures[image]);
                archiveMainMaterial.SetVector("_Rect", new Vector4(0f, 0f, 1f, 1f)); archiveMainMaterial.SetColor("_Tint", Color.white);
                archiveMainMaterial.SetFloat("_Opacity", envelope); archiveMainMaterial.SetFloat("_Activity", .6f); archiveMainMaterial.SetFloat("_Timeline", time);
            }
            float aspect = archiveAspects[image], width = Mathf.Min(5.1f, aspect * 3.30f);
            if (archiveMain != null) { archiveMain.localPosition = new Vector3(-.78f, 2.15f, 5.2f + .18f * (1f - Ease(age / .5f))); archiveMain.localScale = new Vector3(width, width / aspect, 1f); archiveMain.localRotation = Quaternion.Euler(0f, 1.8f * Mathf.Sin(age * .32f), 0f); }
            if (archiveDetailMaterial != null)
            {
                archiveDetailMaterial.mainTexture = archiveTextures[companion]; archiveDetailMaterial.SetTexture("_PreviousTex", archiveTextures[companion]);
                float scroll = Ease(Mathf.Repeat(age, 1.28f) / 1.0f);
                float crop = archiveAspects[companion] > 3f ? 1f : .52f;
                archiveDetailMaterial.SetVector("_Rect", new Vector4(0f, crop > .9f ? 0f : .28f + .20f * scroll, 1f, crop));
                archiveDetailMaterial.SetFloat("_Opacity", envelope); archiveDetailMaterial.SetFloat("_Activity", .8f); archiveDetailMaterial.SetFloat("_Timeline", time);
            }
            if (archiveDetail != null) { archiveDetail.localPosition = new Vector3(2.5f, 2.55f, 5.05f); archiveDetail.localScale = new Vector3(2.0f, Mathf.Min(1.6f, (archiveAspects[companion] > 3f ? 2f : 1.04f) / archiveAspects[companion]), 1f); }
            if (archiveFile != null && archiveNames != null && image < archiveNames.Length) archiveFile.text = archiveNames[image];
            if (archivePointer != null)
            {
                float enter = Ease((time - 143.36f) / 5.12f);
                archivePointer.localPosition = new Vector3(-.4f + Mathf.Sin(time * .9f) * 1.0f, 2.0f + Mathf.Cos(time * 1.2f) * .45f, Mathf.Lerp(5.12f, 3.9f, enter));
                archivePointer.localRotation = Quaternion.Euler(0f, enter * 20f * Mathf.Sin(time), enter * 9f * Mathf.Sin(time * .7f));
            }
        }
        private void SampleFailure(float time)
        {
            int state = Mathf.Clamp(Mathf.FloorToInt((time - 168.96f) / 4.48f), 0, 3); sampledFailure = state;
            float age = time - 168.96f - state * 4.48f;
            float opacity = Ease(age / .25f) * (1f - Ease((age - 4.08f) / .40f));
            if (archiveMainMaterial != null)
            {
                archiveMainMaterial.mainTexture = failureStates[state]; archiveMainMaterial.SetTexture("_PreviousTex", failureStates[state]);
                archiveMainMaterial.SetVector("_Rect", new Vector4(0,0,1,1)); archiveMainMaterial.SetFloat("_Opacity", opacity);
                archiveMainMaterial.SetColor("_Tint", Color.Lerp(Color.white, new Color(.68f,.71f,.74f,1f), state == 3 ? Ease(age / 4f) : 0f));
            }
            if (archiveMain != null) { archiveMain.localPosition = new Vector3(-.65f,2.1f,5.2f + (state == 3 ? Ease(age / 4.48f)*.7f : 0f)); archiveMain.localScale = new Vector3(4.3f,2.86667f,1); archiveMain.localRotation = Quaternion.identity; }
            if (archiveDetailMaterial != null)
            {
                archiveDetailMaterial.mainTexture = failureDetails[state]; archiveDetailMaterial.SetTexture("_PreviousTex", failureDetails[state]);
                archiveDetailMaterial.SetVector("_Rect", new Vector4(0f,.12f*Ease(age/2.56f),1f,.88f)); archiveDetailMaterial.SetFloat("_Opacity",opacity);
            }
            if (archiveDetail != null) { archiveDetail.localPosition = new Vector3(2.65f,2.5f,5.0f); archiveDetail.localScale = new Vector3(1.9f,1.9f/failureDetailAspects[state]*.88f,1); }
            if (archiveFile != null) archiveFile.text = state == 0 ? age < .55f ? "正在连接..." : "无法显示该网页" : state == 1 ? age < .55f ? "37.5%" : "连接已中断" : state == 2 ? age < .55f ? "正在读取缓存..." : "完成" : age < .55f ? "正在查找..." : "404 Not Found";
            if (archivePointer != null) { archivePointer.localPosition = new Vector3(.5f+Mathf.Sin(time*.2f)*.03f,1.85f,5.12f); archivePointer.localRotation = Quaternion.identity; }
        }
        private void SampleClimax(float time, bool show)
        {
            float first = Ease((time - 76.8f) / 2.56f) * (1f - Ease((time - 94.72f) / 2.56f));
            float last = Ease((time - 215.04f) / 5.12f) * (1f - Ease((time - 254.72f) / 1.28f));
            Show(climaxEffects, show && (first > .001f || last > .001f));
            if (contourMaterial != null) { contourMaterial.SetFloat("_Timeline", time); contourMaterial.SetFloat("_Opacity", .48f * Mathf.Max(first, last)); contourMaterial.SetVector("_Pulse", sampledAccents); }
            if (channelEffects != null) for (int ch = 0; ch < channelEffects.Length; ch++) if (channelEffects[ch] != null)
            {
                channelEffects[ch].position = basis.signal.SourceWorldPosition(time, ch);
                channelEffects[ch].rotation = Quaternion.Euler(0f, time * (ch % 2 == 0 ? 7f : -7f), ch * 36f);
                channelEffects[ch].localScale = Vector3.one * (1f + .4f * last);
            }
            if (channelParticles != null) for (int ch = 0; ch < channelParticles.Length; ch++) if (channelParticles[ch] != null)
            {
                Show(channelParticles[ch].gameObject, show && (first > .001f || last > .001f));
                channelParticles[ch].transform.position = basis.signal.SourceWorldPosition(time, ch);
            }
            if (channelParticleMaterials != null) for (int ch = 0; ch < channelParticleMaterials.Length; ch++) if (channelParticleMaterials[ch] != null)
            { var tint = channelParticleMaterials[ch].GetColor("_Tint"); tint.a = show ? Mathf.Max(first, last) * .45f : 0f; channelParticleMaterials[ch].SetColor("_Tint", tint); }
            if (channelLights != null) for (int ch = 0; ch < channelLights.Length; ch++) if (channelLights[ch] != null)
            {
                channelLights[ch].enabled = show && (first > .001f || last > .001f);
                channelLights[ch].transform.position = basis.signal.SourceWorldPosition(time, ch);
                float onset = ch == 0 ? sampledAccents.x : ch == 1 ? sampledAccents.y : ch == 2 ? sampledAccents.z : sampledAccents.w;
                channelLights[ch].intensity = Mathf.Max(first, last) * (.15f + onset * .65f);
            }
        }
        private float Window(float time, float start, float end) { if (time <= start || time >= end) return 0f; return Mathf.Sin(Mathf.Clamp01((time - start) / (end - start)) * Mathf.PI); }
        private void Show(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }
        private float Ease(float value) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value)); }
    }
}
