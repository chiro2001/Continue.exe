using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    /// <summary>Samples the persisted V4 sets and replaceable Humanoid performance slots.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4HubCast : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public bool autoSample=true;
        public OWV4Journey journey;
        public OWV4Nameplates nameplates;
        public Vector3[] regionOffsets;
        public GameObject[] eraUiRoots;
        public TMP_Text[] eraEpochLabels;
        public TMP_Text[] eraAvatarLabels;
        public TMP_Text[] eraWorldLabels;
        public TMP_Text[] eraPresenceLabels;
        public Transform[] eraAvatarCursors;
        public Transform[] eraWorldCursors;
        public Transform[] barrierLeftLeaves;
        public Transform[] barrierRightLeaves;
        public GameObject[] barrierLockedSigns;
        public GameObject[] barrierOpenSigns;
        public Transform[] verificationArms;
        public TMP_Text[] barrierMessages;
        public float[] sampledGateAngles;
        public bool[] sampledGateLocked;
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
            if (autoSample && director != null) Sample(director.Elapsed());
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
            bool oldHub=time>=77.44f;
            bool newHub=time>=83.20f;
            if(legacyWorkstations!=null) for(int i=0;i<legacyWorkstations.Length;i++) Visible(legacyWorkstations[i],false);
            if(legacyContactDiscs!=null) for(int i=0;i<legacyContactDiscs.Length;i++) Visible(legacyContactDiscs[i],false);
            Visible(hub2017==null?null:hub2017.gameObject,oldHub);
            Visible(hub2019==null?null:hub2019.gameObject,newHub);
            Visible(home2019==null?null:home2019.gameObject,newHub);
            Visible(workshop==null?null:workshop.gameObject,true);
            Visible(mirror2017,time>=81.92f && time<86.40f);
            Visible(mirror2019,time>=89.60f && time<97.28f);
            // Every set stays at its authored destination under the common worldFrame.
            // Only the journey moves that common frame; no set cancels the camera travel.
            visibleEra=time<79.36f || time>=102.40f?0:time<86.40f?2017:2019;
            if(eraUiRoots!=null)for(int i=0;i<eraUiRoots.Length;i++)Visible(eraUiRoots[i],visibleEra==(i==0?2017:2019));
            if(visibleEra!=_lastEra && eraEpochLabels!=null)
                for(int i=0;i<eraEpochLabels.Length;i++)if(eraEpochLabels[i]!=null)eraEpochLabels[i].text=i==0?"VRChat  /  NEW HUB  /  2017":"VRChat  /  HOME + HUB  /  2019";
            _lastEra=visibleEra;
            appearanceChanged = time >= 84.48f;
            SampleBodyVisibility();
            SampleActors(time);
            SampleConditions(time);
            worldChosen = time >= 85.76f && time < 87.68f;
            int action = time < 83.2f ? 0 : time < 84.48f ? 1 : time < 85.76f ? 2 : time < 87.04f ? 3 : time < 89.6f ? 4 : time < 92.16f ? 5 : 6;
            if(action!=_lastAction)
            {
                if(eraAvatarLabels!=null)for(int i=0;i<eraAvatarLabels.Length;i++)if(eraAvatarLabels[i]!=null)eraAvatarLabels[i].text=action<2?"AVATARS  /  SELECT":"AVATARS  /  APPLIED ✓";
                if(eraWorldLabels!=null)for(int i=0;i<eraWorldLabels.Length;i++)if(eraWorldLabels[i]!=null)eraWorldLabels[i].text=action<3?"WORLDS  /  SELECT":"WORLDS  /  READY →";
                if(eraPresenceLabels!=null)for(int i=0;i<eraPresenceLabels.Length;i++)if(eraPresenceLabels[i]!=null)eraPresenceLabels[i].text=action<4?"选择身体":action<5?"进入世界":"同一个实例";
                _lastAction=action;
            }
            if(eraAvatarCursors!=null)for(int i=0;i<eraAvatarCursors.Length;i++)if(eraAvatarCursors[i]!=null)eraAvatarCursors[i].localPosition=new Vector3(appearanceChanged?.34f:-.34f,-.02f,-.045f);
            if(eraWorldCursors!=null)for(int i=0;i<eraWorldCursors.Length;i++)if(eraWorldCursors[i]!=null)eraWorldCursors[i].localPosition=new Vector3(worldChosen?.34f:-.34f,-.02f,-.045f);
            if (travelDots != null && actors != null)
                for (int i=0;i<travelDots.Length;i++)
                    if (travelDots[i]!=null && i<actors.Length && actors[i]!=null)
                    {
                        Vector3 p=actors[i].transform.localPosition-ActorRegionOffset(time);
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
            if(nameplates!=null)nameplates.Sample(time);
        }

        public Vector3 RegionOffset(int region)
        {
            if(regionOffsets!=null && region>=0 && region<regionOffsets.Length)return regionOffsets[region];
            return region==1?new Vector3(0,0,24):region==2?new Vector3(0,0,52):region==3?new Vector3(0,0,80):Vector3.zero;
        }
        public Vector3 ActorRegionOffset(float time)
        {
            if(time<79.36f)return RegionOffset(0);
            if(time<81.92f)return Vector3.Lerp(RegionOffset(0),RegionOffset(1),Ease(time,79.36f,81.92f));
            if(time<86.40f)return RegionOffset(1);
            if(time<88.32f)return Vector3.Lerp(RegionOffset(1),RegionOffset(2),Ease(time,86.40f,88.32f));
            if(time<102.40f)return RegionOffset(2);
            if(time<104.96f)return Vector3.Lerp(RegionOffset(2),RegionOffset(3),Ease(time,102.40f,104.96f));
            return RegionOffset(3);
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
            bool active=time>=100.00f;
            Visible(conditionsRoot==null?null:conditionsRoot.gameObject,active);
            Visible(maintenanceRoot==null?null:maintenanceRoot.gameObject,active);
            float enter=Ease(time,102.40f,105.00f);
            float narrow=Ease(time,107.52f,117.76f);
            float resolve=Ease(time,117.76f,122.88f);
            float retreat=Ease(time,122.88f,130.56f);
            if(conditionsRoot!=null)conditionsRoot.localPosition=RegionOffset(3)+new Vector3(0f,0f,retreat*16f);
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
            if(maintenanceRoot!=null)maintenanceRoot.localPosition=RegionOffset(3)+new Vector3(4.65f,0f,4.9f);
            if(maintenanceLight!=null)maintenanceLight.intensity=.75f+Ease(time,122.88f,125.44f)*.65f;
            if(barrierLeftLeaves!=null)
                for(int i=0;i<barrierLeftLeaves.Length;i++)
                {
                    float angle=i==0?90f*(1f-Ease(time,104.96f,109.00f))+90f*Ease(time,115.20f,117.76f):i==1?90f*(1f-Ease(time,109.00f,112.64f))+90f*Ease(time,118.40f,120.32f):90f*(1f-Ease(time,112.64f,117.76f))+95f*Ease(time,120.32f,122.88f);
                    angle=Mathf.Clamp(angle,0f,95f);
                    if(barrierLeftLeaves[i]!=null)barrierLeftLeaves[i].localRotation=Quaternion.Euler(0f,-angle,0f);
                    if(barrierRightLeaves!=null && i<barrierRightLeaves.Length && barrierRightLeaves[i]!=null)barrierRightLeaves[i].localRotation=Quaternion.Euler(0f,angle,0f);
                    bool locked=i==0?time>=107.52f && time<117.76f:i==1?time>=112.64f && time<120.32f:time>=117.76f && time<122.88f;
                    if(sampledGateAngles!=null && i<sampledGateAngles.Length)sampledGateAngles[i]=angle;
                    if(sampledGateLocked!=null && i<sampledGateLocked.Length)sampledGateLocked[i]=locked;
                    if(barrierLockedSigns!=null && i<barrierLockedSigns.Length)Visible(barrierLockedSigns[i],locked);
                    if(barrierOpenSigns!=null && i<barrierOpenSigns.Length)Visible(barrierOpenSigns[i],!locked);
                    if(verificationArms!=null && i<verificationArms.Length && verificationArms[i]!=null)verificationArms[i].localRotation=Quaternion.Euler(0f,0f,locked?0f:88f);
                    if(barrierMessages!=null && i<barrierMessages.Length && barrierMessages[i]!=null)
                        barrierMessages[i].text=i==0?(locked?"无法同时运行":"兼容性检查通过"):i==1?(locked?"正在验证…":"验证通过"):locked?"当前功能不可用":"已恢复连接";
                }
            float drawerOpen=Ease(time,123.52f,125.44f)*(1f-Ease(time,128.00f,130.56f));
            if(archiveDrawer!=null)archiveDrawer.localPosition=new Vector3(-.48f,maintenanceWorkHeight*.56f,-.14f-drawerOpen*.28f);
            if(savedDocument!=null)savedDocument.localPosition=Vector3.Lerp(new Vector3(-.60f,maintenanceWorkHeight+.055f,-.12f),new Vector3(.30f,maintenanceWorkHeight+.055f,.27f),Ease(time,125.44f,128.00f));
            Visible(archivedCopy,time>=128f);
            conditionsPhase=!active?0:time<107.52f?1:time<115.20f?2:time<122.88f?3:time<128f?4:5;
            if(conditionsPhase!=_lastConditionPhase)
            {
                if(conditionStatus!=null)conditionStatus.text=conditionsPhase<2?"此程序无法运行\n请选择其他程序":conditionsPhase==2?"正在连接…\n请稍候":conditionsPhase==3?"正在检查更新…\n返回后重新连接":"已保存本地副本";
                if(maintenanceStatus!=null)maintenanceStatus.text=conditionsPhase<4?"未保存":conditionsPhase==4?"正在保存…":"已保存  ✓";
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
                Vector3 position=Vector3.Lerp(positions[a],positions[b],move)+ActorRegionOffset(time);
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
                    actor.animator.SetInteger("V4Pose",gesture);
                    actor.animator.SetFloat("V4PoseTime",phase);
                }
                if(carriedWorks!=null && i<carriedWorks.Length) Visible(carriedWorks[i],actor==original && !walking && (gesture==2 || gesture==3 || gesture==11));
                if(alternateCarriedWorks!=null && i<alternateCarriedWorks.Length) Visible(alternateCarriedWorks[i],appearanceChanged && !walking && (gesture==2 || gesture==3 || gesture==11));
                if(workTrays!=null && i<workTrays.Length)Visible(workTrays[i],time<79.36f && !walking && (gesture==1 || gesture==5 || gesture==10));
                if(actor.head!=null && headRestRotations!=null && i<headRestRotations.Length && gazePositions!=null && b<gazePositions.Length)
                {
                    Vector3 localTarget=Vector3.Lerp(gazePositions[a],gazePositions[b],move)+ActorRegionOffset(time);
                    Vector3 target=worldRoot==null?localTarget:worldRoot.TransformPoint(localTarget);
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
