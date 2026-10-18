using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    /// <summary>Samples the persisted V3 sets and replaceable Humanoid performance slots.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV3HubCast : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public Transform worldRoot;
        public Transform workshop;
        public Transform hub2017;
        public Transform hub2019;
        public Transform home2019;
        public Transform conditionsRoot;
        public Transform maintenanceRoot;
        public Transform[] conditionFrames;
        public Vector3[] conditionFrameOrigins;
        public Transform[] conditionLeftJambs;
        public Transform[] conditionRightJambs;
        public Transform[] conditionLintels;
        public GameObject[] conditionReadyMarks;
        public Transform[] conditionChoiceArrows;
        public TMP_Text conditionStatus;
        public TMP_Text maintenanceStatus;
        public Transform archiveDrawer;
        public Transform savedDocument;
        public GameObject archivedCopy;
        public Light maintenanceLight;
        public float maintenanceWorkHeight=.8f;
        public int conditionsPhase;
        public GameObject uiRoot;
        public GameObject[] legacyWorkstations;
        public GameObject[] legacyContactDiscs;
        public GameObject mirror2017;
        public GameObject mirror2019;
        public OWActorRig[] actors;
        public OWActorRig[] alternateActors;
        public Renderer[] bodyRenderers;
        public int[] bodySlots;
        public bool[] bodyAlternates;
        public bool[] bodyBaseEnabled;
        public GameObject[] workTrays;
        public bool[] externalMotion;
        public Transform[] roleBadges;
        public GameObject[] carriedWorks;
        public GameObject[] alternateCarriedWorks;
        public Quaternion[] headRestRotations;
        public Quaternion[] alternateHeadRestRotations;
        public float[] keyTimes;
        public Vector3[] positions;
        public float[] yaws;
        public int[] gestures;
        public Vector3[] gazePositions;
        public TMP_Text epochLabel;
        public TMP_Text avatarSelection;
        public TMP_Text worldSelection;
        public TMP_Text presence;
        public Transform avatarCursor;
        public Transform worldCursor;
        public Transform[] travelDots;
        public Transform[] portalCores;
        public Transform[] portalRims;
        public Transform[] fireFlames;
        public Light fireLight;
        public float sampledTime;
        public int sampledKey;
        public int visibleEra;
        public bool appearanceChanged;
        public bool worldChosen;
        private int _lastEra = -1;
        private int _lastAction = -1;
        private int _lastConditionPhase = -1;

        // The integrated Stage has released its cast. Sample once after the director
        // advances the shared clock, also making the renderer swap deterministic.
        private void LateUpdate()
        {
            if (director != null) Sample(director.Elapsed());
        }

        private float Ease(float time, float start, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time-start)/Mathf.Max(.01f,end-start)));
        }

        private void Visible(GameObject target, bool show)
        {
            if (target != null && target.activeSelf != show) target.SetActive(show);
        }

        public void Sample(float time)
        {
            sampledTime = time;
            bool oldHub = time >= 79.36f && time < 87.68f;
            bool newHub = time >= 86.40f && time < 102.40f;
            Visible(uiRoot,time >= 79.36f && time < 102.40f);
            if(legacyWorkstations!=null) for(int i=0;i<legacyWorkstations.Length;i++) Visible(legacyWorkstations[i],false);
            if(legacyContactDiscs!=null) for(int i=0;i<legacyContactDiscs.Length;i++) Visible(legacyContactDiscs[i],false);
            Visible(hub2017 == null ? null : hub2017.gameObject, oldHub);
            Visible(hub2019 == null ? null : hub2019.gameObject, newHub);
            Visible(home2019 == null ? null : home2019.gameObject, newHub);
            Visible(workshop == null ? null : workshop.gameObject, time < 81.92f);
            // The SDK mirror is enabled only during its short visiting beat.
            Visible(mirror2017, time >= 81.92f && time < 86.40f);
            Visible(mirror2019, time >= 89.60f && time < 97.28f);
            if (hub2017 != null)
            {
                float enter = Ease(time,79.36f,81.92f);
                float leave = Ease(time,86.40f,87.68f);
                hub2017.localPosition = new Vector3(-leave*12f,-.045f,Mathf.Lerp(14f,4.1f,enter)-Ease(time,81.92f,86.4f)*1.2f);
                hub2017.localRotation = Quaternion.Euler(0f,Mathf.Lerp(-7f,0f,enter)-leave*15f,0f);
            }
            if (hub2019 != null)
            {
                float enter = Ease(time,86.40f,88.32f);
                hub2019.localPosition = new Vector3((1f-enter)*8f,-.04f,Mathf.Lerp(13f,5.1f,enter));
                hub2019.localRotation = Quaternion.Euler(0f,(1f-enter)*12f,0f);
            }
            if (home2019 != null)
            {
                home2019.localPosition = new Vector3(-7.8f,-.04f,6.2f+(1f-Ease(time,86.4f,88.32f))*7f);
                home2019.localRotation = Quaternion.Euler(0f,24f,0f);
            }
            visibleEra = newHub ? 2019 : oldHub ? 2017 : 0;
            if (visibleEra != _lastEra && epochLabel != null)
                epochLabel.text = visibleEra == 2017 ? "VRChat  /  NEW HUB  /  2017.11" : visibleEra == 2019 ? "VRChat  /  HOME + HUB  /  2019.05" : "";
            _lastEra = visibleEra;
            appearanceChanged = time >= 84.48f;
            SampleBodyVisibility();
            SampleActors(time);
            SampleConditions(time);
            worldChosen = time >= 85.76f && time < 87.68f;
            int action = time < 83.2f ? 0 : time < 84.48f ? 1 : time < 85.76f ? 2 : time < 87.04f ? 3 : time < 89.6f ? 4 : time < 92.16f ? 5 : 6;
            if (action != _lastAction)
            {
                if (avatarSelection != null) avatarSelection.text = action < 2 ? "AVATARS  /  SELECT" : "AVATARS  /  APPLIED ✓";
                if (worldSelection != null) worldSelection.text = action < 3 ? "WORLDS  /  SELECT" : "WORLDS  /  READY →";
                if (presence != null) presence.text = action < 4 ? "选择身体" : action < 5 ? "进入世界" : "同一个实例";
                _lastAction = action;
            }
            if (avatarCursor != null) avatarCursor.localPosition = new Vector3(appearanceChanged?.34f:-.34f,-.02f,-.045f);
            if (worldCursor != null) worldCursor.localPosition = new Vector3(worldChosen?.34f:-.34f,-.02f,-.045f);
            if (travelDots != null && actors != null)
                for (int i=0;i<travelDots.Length;i++)
                    if (travelDots[i]!=null && i<actors.Length && actors[i]!=null)
                    {
                        Vector3 p=actors[i].transform.localPosition;
                        travelDots[i].localPosition = new Vector3(Mathf.Clamp(p.x*.055f,-.38f,.38f),Mathf.Clamp((p.z-4f)*.055f,-.24f,.24f),-.035f);
                    }
            if (portalCores != null)
                for (int i=0;i<portalCores.Length;i++)
                    if (portalCores[i]!=null) portalCores[i].localRotation=Quaternion.Euler(0f,0f,time*(i%2==0?11f:-8f));
            if (portalRims != null)
                for (int i=0;i<portalRims.Length;i++)
                    if (portalRims[i]!=null) portalRims[i].localRotation=Quaternion.Euler(0f,0f,Mathf.Sin(time*.7f+i)*1.2f);
            if (fireFlames != null)
                for (int i=0;i<fireFlames.Length;i++)
                    if (fireFlames[i]!=null)
                    {
                        float beat=.92f+.1f*Mathf.Sin(time*6.3f+i*2.1f)+.04f*Mathf.Sin(time*15.7f+i);
                        fireFlames[i].localScale=new Vector3(1f+i*.08f,(.80f-i*.12f)*beat,1f+i*.08f);
                    }
            if (fireLight!=null) fireLight.intensity=1.1f+.15f*Mathf.Sin(time*8f)+.07f*Mathf.Sin(time*17f);
        }

        private void SampleBodyVisibility()
        {
            if(bodyRenderers==null || bodySlots==null || bodyAlternates==null || bodyBaseEnabled==null)return;
            for(int i=0;i<bodyRenderers.Length;i++)
            {
                if(bodyRenderers[i]==null || i>=bodySlots.Length || i>=bodyAlternates.Length || i>=bodyBaseEnabled.Length)continue;
                int slot=bodySlots[i];bool switched=appearanceChanged && alternateActors!=null && slot<alternateActors.Length && alternateActors[slot]!=null;
                bool show=bodyBaseEnabled[i] && (bodyAlternates[i]?switched:!switched);
                if(bodyRenderers[i].enabled!=show)bodyRenderers[i].enabled=show;
            }
        }

        private void SampleConditions(float time)
        {
            bool active=time>=102.40f;
            Visible(conditionsRoot==null?null:conditionsRoot.gameObject,active);
            Visible(maintenanceRoot==null?null:maintenanceRoot.gameObject,active);
            float enter=Ease(time,102.40f,105.00f);
            float narrow=Ease(time,107.52f,117.76f);
            float resolve=Ease(time,117.76f,122.88f);
            float retreat=Ease(time,122.88f,130.56f);
            if(conditionsRoot!=null)conditionsRoot.localPosition=new Vector3(0f,0f,(1f-enter)*8f+retreat*16f);
            if(conditionFrames!=null && conditionFrameOrigins!=null)
                for(int i=0;i<conditionFrames.Length;i++)
                {
                    if(conditionFrames[i]==null || i>=conditionFrameOrigins.Length)continue;
                    float baseWidth=7.8f-i*.72f;
                    float targetWidth=i==2?2.7f:i==3?3.4f:5.6f-i*.50f;
                    float width=Mathf.Lerp(baseWidth,targetWidth,narrow);
                    // Checking safety restores a route; accessibility adjustments widen
                    // the following layer. The sequence is not an irreversible closure.
                    if(i==2)width+=Ease(time,115.20f,119.04f)*.90f;
                    if(i==3)width+=resolve*1.10f;
                    float sign=i%2==0?1f:-1f;
                    float shift=sign*(.30f+i*.17f)*narrow*(1f-resolve*.55f);
                    conditionFrames[i].localPosition=conditionFrameOrigins[i]+new Vector3(shift+retreat*sign*(1f+i*.35f),0f,retreat*i*.55f);
                    conditionFrames[i].localRotation=Quaternion.Euler(0f,sign*narrow*(4f+i)*(.35f+retreat*.65f),0f);
                    if(conditionLeftJambs!=null && i<conditionLeftJambs.Length && conditionLeftJambs[i]!=null)conditionLeftJambs[i].localPosition=new Vector3(-width*.5f,1.55f,0f);
                    if(conditionRightJambs!=null && i<conditionRightJambs.Length && conditionRightJambs[i]!=null)conditionRightJambs[i].localPosition=new Vector3(width*.5f,1.55f,0f);
                    if(conditionLintels!=null && i<conditionLintels.Length && conditionLintels[i]!=null)conditionLintels[i].localScale=new Vector3(width+.24f,.18f,.32f);
                    if(conditionReadyMarks!=null && i<conditionReadyMarks.Length)Visible(conditionReadyMarks[i],time>=112.64f+i*1.28f);
                }
            if(conditionChoiceArrows!=null)
                for(int i=0;i<conditionChoiceArrows.Length;i++)
                    if(conditionChoiceArrows[i]!=null)conditionChoiceArrows[i].localPosition=new Vector3((i==0?-1f:1f)*(3.5f+narrow*.6f),.10f,8.4f+retreat*5f);
            if(maintenanceRoot!=null)maintenanceRoot.localPosition=new Vector3(4.65f,0f,4.9f+(1f-enter)*3f);
            if(maintenanceLight!=null)maintenanceLight.intensity=.75f+Ease(time,122.88f,125.44f)*.65f;
            float drawerOpen=Ease(time,123.52f,125.44f)*(1f-Ease(time,128.00f,130.56f));
            if(archiveDrawer!=null)archiveDrawer.localPosition=new Vector3(-.48f,maintenanceWorkHeight*.56f,-.14f-drawerOpen*.28f);
            if(savedDocument!=null)savedDocument.localPosition=Vector3.Lerp(new Vector3(-.60f,maintenanceWorkHeight+.055f,-.12f),new Vector3(.30f,maintenanceWorkHeight+.055f,.27f),Ease(time,125.44f,128.00f));
            Visible(archivedCopy,time>=128f);
            conditionsPhase=!active?0:time<107.52f?1:time<115.20f?2:time<122.88f?3:time<128f?4:5;
            if(conditionsPhase!=_lastConditionPhase)
            {
                if(conditionStatus!=null)conditionStatus.text=conditionsPhase<2?"同一套工具\n使用条件正在变化":conditionsPhase==2?"安全验证  /  使用便利\n辅助功能也需要被看见":conditionsPhase==3?"检查与调整\n仍有可以继续的路径":"入口退远\n维护仍在继续";
                if(maintenanceStatus!=null)maintenanceStatus.text=conditionsPhase<4?"CHECK  /  COMPATIBILITY\n安全 · 便利 · 可访问性":conditionsPhase==4?"检查  →  保存\n记录问题与可用的方法":"已保存  ✓\n把可用的方法留给后来的人";
                _lastConditionPhase=conditionsPhase;
            }
        }

        private void SampleActors(float time)
        {
            if (actors==null || keyTimes==null || keyTimes.Length<2 || positions==null) return;
            int key=0;
            for(int k=0;k<keyTimes.Length-1;k++) if(time>=keyTimes[k]) key=k;
            sampledKey=key;
            int next=Mathf.Min(key+1,keyTimes.Length-1);
            float span=Mathf.Max(.01f,keyTimes[next]-keyTimes[key]);
            float part=Mathf.Clamp01((time-keyTimes[key])/span);
            float move=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(part/.72f));
            for(int i=0;i<actors.Length;i++)
            {
                OWActorRig actor=actors[i];
                if(actor==null) continue;
                if(externalMotion!=null && i<externalMotion.Length && externalMotion[i]) continue;
                OWActorRig original=actor;
                OWActorRig alternate=alternateActors!=null && i<alternateActors.Length?alternateActors[i]:null;
                Visible(original.gameObject,true);
                Visible(original.visualRoot==null?null:original.visualRoot.gameObject,true);
                if(alternate!=null)
                {
                    // Both Animator hierarchies stay active. Swap renderer visibility,
                    // so deterministic editor capture never evaluates an inactive Animator.
                    Visible(alternate.gameObject,true);
                    Visible(alternate.visualRoot==null?null:alternate.visualRoot.gameObject,true);
                    if(appearanceChanged) actor=alternate;
                }
                int a=key*actors.Length+i;
                int b=next*actors.Length+i;
                if(a>=positions.Length || b>=positions.Length) continue;
                Vector3 delta=positions[b]-positions[a];
                bool walking=delta.sqrMagnitude>.06f && part<.72f;
                Vector3 position=Vector3.Lerp(positions[a],positions[b],move);
                if(walking) position.y+=Mathf.Sin(part*span*9f)*.009f;
                actor.transform.localPosition=position;
                if(actor!=original) original.transform.localPosition=position;
                float yaw=Mathf.LerpAngle(yaws[a],yaws[b],move);
                if(walking)
                {
                    float facing=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                    float turn=Ease(part,0f,.14f);
                    float settle=Ease(part,.56f,.72f);
                    yaw=Mathf.LerpAngle(Mathf.LerpAngle(yaws[a],facing,turn),yaws[b],settle);
                }
                actor.transform.localRotation=Quaternion.Euler(0f,yaw,0f);
                if(actor!=original) original.transform.localRotation=Quaternion.Euler(0f,yaw,0f);
                int gesture=walking?6:gestures[part<.72f?a:b];
                float phase=walking?Mathf.Repeat((time-keyTimes[key])*1.25f+i*.18f,1f):.25f+.4f*(.5f+.5f*Mathf.Sin(time*1.8f+i));
                if(actor.animator!=null && actor.animator.gameObject.activeInHierarchy && actor.animator.enabled)
                {
                    actor.animator.SetInteger("V3Pose",gesture);
                    actor.animator.SetFloat("V3PoseTime",phase);
                }
                if(roleBadges!=null && i<roleBadges.Length && roleBadges[i]!=null)
                {
                    roleBadges[i].localRotation=Quaternion.Euler(0f,-yaw,0f);
                    roleBadges[i].localPosition=new Vector3(0f,actor.modelHeight+.12f,0f);
                }
                if(carriedWorks!=null && i<carriedWorks.Length) Visible(carriedWorks[i],actor==original && !walking && (gesture==2 || gesture==3 || gesture==11));
                if(alternateCarriedWorks!=null && i<alternateCarriedWorks.Length) Visible(alternateCarriedWorks[i],appearanceChanged && !walking && (gesture==2 || gesture==3 || gesture==11));
                if(workTrays!=null && i<workTrays.Length)Visible(workTrays[i],time<79.36f && !walking && (gesture==1 || gesture==5 || gesture==10));
                if(actor.head!=null && headRestRotations!=null && i<headRestRotations.Length && gazePositions!=null && b<gazePositions.Length)
                {
                    Vector3 target=Vector3.Lerp(gazePositions[a],gazePositions[b],move);
                    Vector3 direction=actor.transform.InverseTransformDirection(target-actor.head.position);
                    float lookYaw=Mathf.Clamp(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,-30f,30f);
                    float lookPitch=-Mathf.Clamp(Mathf.Atan2(direction.y,Mathf.Sqrt(direction.x*direction.x+direction.z*direction.z))*Mathf.Rad2Deg,-18f,22f);
                    Quaternion rest=actor!=original && alternateHeadRestRotations!=null && i<alternateHeadRestRotations.Length?alternateHeadRestRotations[i]:headRestRotations[i];
                    actor.head.localRotation=rest*Quaternion.Euler(lookPitch,lookYaw,0f);
                }
            }
        }
    }
}
