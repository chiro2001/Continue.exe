using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    // One absolute clock owns both the retained V5 and the completed continuation.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV2BasePresentation : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWV4Presentation legacy;
        public OWV2Signal signal;
        public OWFullOverload overload;
        public OWFullRenewal renewal;
        public GameObject continuation;
        public GameObject[] legacyContents;
        public Renderer[] transitWalls;
        public bool[] transitWallEnabled;
        public Transform referenceEye;
        public GameObject keyRoot;
        public TMP_Text keyText;
        public Renderer keyPlate;
        public Material keyDark;
        public Material keyLight;
        public float[] keyStarts;
        public float[] keyEnds;
        public string[] keyStrings;
        public int[] keyStyles;
        public Transform[] commentRoots;
        public TMP_Text[] commentTexts;
        public float[] commentTimes;
        public float[] commentDurations;
        public string[] commentStrings;
        public bool offlinePreview;
        public bool drivenExternally = true;
        public float sampledTime;
        public int sampledState;
        public int sampledKey=-1;
        public int activeComments;
        public int sampledScene;
        public float[] sceneStarts;
        public GameObject[] historyPanels;
        public float[] historyStarts;
        public float[] historyEnds;

        private void Start(){offlinePreview=false;if(legacy!=null)legacy.offlinePreview=true;}
        private void LateUpdate()
        {
            if(drivenExternally||offlinePreview||director==null)return;
            Apply(director.Elapsed(),director.IsPlaying());
            // After the scored performance the work remains alive while visitors
            // browse. This local ambient motion never changes the musical clock.
            if(sampledState==2&&renewal!=null&&renewal.actors!=null)
                for(int i=0;i<renewal.actors.Length;i++)
                {
                    OWActorRig actor=renewal.actors[i];
                    if(actor!=null&&actor.animator!=null&&actor.animator.gameObject.activeInHierarchy)
                        actor.animator.SetFloat("V4PoseTime",.25f+.4f*(.5f+.5f*Mathf.Sin(Time.time*1.8f+i)));
                }
        }
        public void Preview(float time){offlinePreview=true;Apply(time,true);}
        public void Seek(float time){Preview(time);}
        public void Waiting(){offlinePreview=true;Apply(0,false);}
        public void Finished(){offlinePreview=true;Apply(256,false);}
        public void Apply(float time,bool playing)
        {
            sampledTime=time;bool show=playing&&time<256f;
            sampledState=show?1:time>=255.99f?2:0;
            bool front=show&&time<133.12f;
            if(legacy!=null)
            {
                legacy.offlinePreview=true;legacy.Apply(time,show);
                if(legacyContents!=null)for(int i=0;i<legacyContents.Length;i++)Show(legacyContents[i],front);
                if(front)
                {
                    if(legacy.journey!=null)legacy.journey.Sample(time);
                    if(legacy.cast!=null)
                    {
                        // Keep architectural/UI state deterministic on replay;
                        // release only the unused actor animation outside VRC.
                        if(legacy.cast.externalMotion!=null)for(int i=0;i<legacy.cast.externalMotion.Length;i++)legacy.cast.externalMotion[i]=time<77.44f||time>=102.4f;
                        legacy.cast.Sample(time);
                    }
                    if(legacy.montage!=null)legacy.montage.Sample(time);
                    if(legacy.operations!=null)legacy.operations.Sample(time);
                }
                else
                {
                    if(legacy.journey!=null)legacy.journey.Sample(show?133.12f:0f);
                    if(legacy.nameplateRoot!=null)legacy.nameplateRoot.SetActive(false);
                    if(legacy.captionRoot!=null)legacy.captionRoot.SetActive(false);
                }
                SampleCamera(time,show);
                if(!show)
                {
                    if(legacy.menuTitle!=null)legacy.menuTitle.text=sampledState==2?"连接仍在继续":"连接尚未开始";
                    if(legacy.menuBody!=null)legacy.menuBody.text=sampledState==2?"那就一起做下去。":"按下开始，让第一扇窗口打开。";
                }
            }
            // All occlusion changes are reversible on seek/replay.
            if(transitWalls!=null)for(int i=0;i<transitWalls.Length;i++)
            {
                Renderer r=transitWalls[i];if(r==null)continue;
                bool baseOn=transitWallEnabled==null||i>=transitWallEnabled.Length||transitWallEnabled[i];
                bool transit=time>=86.40f&&time<88.32f;
                Vector3 eye=referenceEye==null?new Vector3(0,1.35f,-2.8f):referenceEye.position;
                bool near=transit&&r.bounds.SqrDistance(eye)<2.25f;
                r.enabled=baseOn&&!near;
            }
            bool after=(show&&time>=133.12f)||sampledState==2;
            Show(continuation,after);
            if(signal!=null){if(show)signal.Sample(time);else if(sampledState==2)signal.Finished();else signal.Waiting();}
            // V2 replaces these stages with archival sources and the actual
            // production workspace. Hidden V1 actors must not run their old poses.
            if(overload!=null&&overload.contentRoot!=null)Show(overload.contentRoot.gameObject,false);
            if(renewal!=null&&renewal.contentRoot!=null)Show(renewal.contentRoot,false);
            if(historyPanels!=null)for(int i=0;i<historyPanels.Length;i++)Show(historyPanels[i],(show||sampledState==2)&&time>=historyStarts[i]&&time<historyEnds[i]);
            SampleText(show?time:-1f);
            sampledScene=-1;if(show&&sceneStarts!=null)for(int i=0;i<sceneStarts.Length;i++)if(time>=sceneStarts[i])sampledScene=i;
        }
        private void SampleCamera(float time,bool show)
        {
            OWV4CameraFx fx=legacy.cameraFx;if(fx==null)return;
            if(time<133.12f){fx.Sample(time);if(!show&&fx.overlayRenderer!=null)fx.overlayRenderer.enabled=false;return;}
            fx.FollowHead();Material mat=fx.effectMaterial;if(mat==null)return;
            float pressure=Ease((time-140.8f)/2.56f)*(1f-Ease((time-153.6f)/2.56f));
            float fade=show?1f:0f;
            if(fx.overlayRenderer!=null)fx.overlayRenderer.enabled=show;
            mat.SetFloat("_Timeline",time);mat.SetFloat("_Intensity",fade*.45f);
            mat.SetFloat("_CRT",.06f);mat.SetFloat("_Chroma",.00012f+pressure*.0008f);
            mat.SetFloat("_Warp",pressure*.0018f);mat.SetFloat("_Vignette",time<197.12f?.19f:.06f);
            mat.SetFloat("_Desaturate",time<186.88f?.25f:0f);mat.SetFloat("_Dropout",0f);mat.SetFloat("_Warmth",time>215.04f?.10f:0f);
        }
        private void SampleText(float time)
        {
            sampledKey=-1;
            if(keyStarts!=null)for(int i=0;i<keyStarts.Length;i++)if(time>=keyStarts[i]&&time<keyEnds[i]){sampledKey=i;break;}
            Show(keyRoot,sampledKey>=0);
            if(sampledKey>=0&&keyText!=null)
            {
                keyText.text=keyStrings[sampledKey];bool dark=keyStyles[sampledKey]==0;
                keyText.color=dark?Color.white:Color.black;
                if(keyPlate!=null)keyPlate.sharedMaterial=dark?keyDark:keyLight;
            }
            activeComments=0;
            if(commentRoots==null||commentTexts==null)return;
            for(int slot=0;slot<commentRoots.Length;slot++)
            {
                int chosen=-1;
                if(time>=133.12f&&commentTimes!=null)for(int i=slot;i<commentTimes.Length;i+=commentRoots.Length)
                    if(time>=commentTimes[i]&&time<commentTimes[i]+commentDurations[i])chosen=i;
                Transform root=commentRoots[slot];if(root==null)continue;
                Show(root.gameObject,chosen>=0);if(chosen<0)continue;
                float age=time-commentTimes[chosen],p=age/commentDurations[chosen];int lane=slot%5;
                root.localPosition=new Vector3(Mathf.Lerp(7.2f,-7.2f,p),3.35f+lane*.25f,7.6f+(slot%2)*.4f);
                if(commentTexts[slot].text!=commentStrings[chosen])commentTexts[slot].text=commentStrings[chosen];
                activeComments++;
            }
        }
        private float Ease(float v){return Mathf.SmoothStep(0,1,Mathf.Clamp01(v));}
        private void Show(GameObject go,bool on){if(go!=null&&go.activeSelf!=on)go.SetActive(on);}
    }
}
