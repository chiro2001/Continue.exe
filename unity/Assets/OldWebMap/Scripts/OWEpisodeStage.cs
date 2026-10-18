using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    // All objects, poses and materials are authored by OWEpisodeBuild; this only samples them.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWEpisodeStage : UdonSharpBehaviour
    {
        public Transform[] cueRoots;
        public float[] starts;
        public float[] ends;
        public Vector3[] rootPositions;
        public float[] rootScales;
        public Vector3[] travelOffsets;
        public Transform[] shells;
        public Vector3[] shellPositions;
        public Vector3[] shellExits;
        public Transform[] animated;
        public float[] animOn;
        public float[] animOff;
        public float[] animBegin;
        public float[] animLength;
        public Vector3[] animFrom;
        public Vector3[] animTo;
        public Vector3[] scaleFrom;
        public Vector3[] scaleTo;
        public Vector3[] animWobble;
        public float[] animPhase;
        public OWActorRig[] actors;
        public Vector3[] actorPositions;
        public float[] actorYaws;
        public int[] actorPoses;
        public int[] actorResultPoses;
        public Renderer[] contactScreens;
        public Material[] contactScreenMaterials;
        public TMP_Text chapterText;
        public TMP_Text progressText;
        public TMP_Text captionText;
        public string[] captions;
        public GameObject endCard;
        public float sampledTime;
        public int sampledCue;
        public int activeCueRoots;
        private int _lastCue=-100;
        private int _shownSecond=-1;
        private int _lastChapter=-1;
        private Vector3[] _cachedRootPositions;
        private float[] _cachedRootScales;
        private bool[] _rootWritten;
        private bool[] _animatedVisible;
        private bool[] _animatedWritten;
        private bool[] _animatedHasWobble;
        private float[] _animatedProgress;
        private int[] _wobbleTicks;
        private Vector3[] _cachedActorPositions;
        private float[] _cachedActorYaws;
        private int[] _cachedActorPoses;
        private float _lastWithdrawal=-1;

        private void Start()
        {
            _cachedRootPositions=null;_cachedRootScales=null;_rootWritten=null;
            _animatedVisible=null;_animatedWritten=null;_animatedHasWobble=null;_animatedProgress=null;_wobbleTicks=null;
            _cachedActorPositions=null;_cachedActorYaws=null;_cachedActorPoses=null;
            _lastCue=-100;_lastChapter=-1;_shownSecond=-1;_lastWithdrawal=-1;
        }

        public void Sample(float time)
        {
            if(cueRoots==null || starts==null || starts.Length==0) return;
            sampledTime=time;
            if(_cachedRootPositions==null || _cachedRootPositions.Length!=cueRoots.Length || _cachedRootScales==null || _cachedRootScales.Length!=cueRoots.Length || _rootWritten==null || _rootWritten.Length!=cueRoots.Length)
            {
                _cachedRootPositions=new Vector3[cueRoots.Length];_cachedRootScales=new float[cueRoots.Length];_rootWritten=new bool[cueRoots.Length];
            }
            int cue=0;
            for(int i=0;i<starts.Length;i++) if(time>=starts[i]) cue=i;
            sampledCue=cue;activeCueRoots=0;
            for(int i=0;i<cueRoots.Length;i++)
            {
                if(cueRoots[i]==null) continue;
                bool visible=time>=starts[i]-.65f && time<ends[i]+.65f;
                if(i==cueRoots.Length-1 && time>=ends[i]-.01f) visible=true;
                if(cueRoots[i].gameObject.activeSelf!=visible) cueRoots[i].gameObject.SetActive(visible);
                if(!visible) continue;
                activeCueRoots++;
                float enter=i==0?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((time-starts[i]+.65f)/1.3f));
                float leave=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-ends[i]+.1f)/.75f));
                if(i==cueRoots.Length-1) leave=0;
                float journey=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-starts[i])/Mathf.Max(.1f,ends[i]-starts[i])));
                Vector3 travel=travelOffsets!=null && i<travelOffsets.Length?travelOffsets[i]*journey:Vector3.zero;
                Vector3 position=rootPositions[i]+travel+new Vector3(-leave*7f,0,(1-enter)*8f-leave*2f);
                float scale=rootScales[i]*(.84f+.16f*enter);
                if(!_rootWritten[i] || _cachedRootPositions[i]!=position){cueRoots[i].localPosition=position;_cachedRootPositions[i]=position;}
                if(!_rootWritten[i] || _cachedRootScales[i]!=scale){cueRoots[i].localScale=new Vector3(scale,scale,scale);_cachedRootScales[i]=scale;}
                _rootWritten[i]=true;
            }
            float withdrawal=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-30.72f)/20.48f));
            if(shells!=null && withdrawal!=_lastWithdrawal) for(int i=0;i<shells.Length;i++)
            {
                if(shells[i]==null) continue;
                bool show=withdrawal<.999f;
                if(shells[i].gameObject.activeSelf!=show) shells[i].gameObject.SetActive(show);
                shells[i].localPosition=shellPositions[i]+shellExits[i]*withdrawal;
            }
            _lastWithdrawal=withdrawal;
            if(animated!=null && (_animatedVisible==null || _animatedVisible.Length!=animated.Length || _animatedWritten==null || _animatedWritten.Length!=animated.Length || _animatedHasWobble==null || _animatedHasWobble.Length!=animated.Length || _animatedProgress==null || _animatedProgress.Length!=animated.Length || _wobbleTicks==null || _wobbleTicks.Length!=animated.Length))
            {
                _animatedVisible=new bool[animated.Length];_animatedWritten=new bool[animated.Length];_animatedHasWobble=new bool[animated.Length];_animatedProgress=new float[animated.Length];_wobbleTicks=new int[animated.Length];
                for(int i=0;i<animated.Length;i++){_animatedVisible[i]=animated[i]!=null && animated[i].gameObject.activeSelf;_animatedHasWobble[i]=animWobble[i].sqrMagnitude>.000001f;_wobbleTicks[i]=-1;}
            }
            if(animated!=null) for(int i=0;i<animated.Length;i++)
            {
                if(animated[i]==null) continue;
                bool show=time>=animOn[i] && time<animOff[i];
                bool visibilityChanged=_animatedVisible[i]!=show;
                if(visibilityChanged){animated[i].gameObject.SetActive(show);_animatedVisible[i]=show;}
                if(!show) continue;
                float p=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-animBegin[i])/Mathf.Max(.01f,animLength[i])));
                int tick=Mathf.FloorToInt(time*20);
                bool wobbleUpdate=_animatedHasWobble[i] && _wobbleTicks[i]!=tick;
                if(_animatedWritten[i] && !visibilityChanged && p==_animatedProgress[i] && !wobbleUpdate) continue;
                animated[i].localPosition=Vector3.Lerp(animFrom[i],animTo[i],p)+animWobble[i]*Mathf.Sin(time*.9f+animPhase[i]);
                if(!_animatedWritten[i] || visibilityChanged || scaleFrom[i]!=scaleTo[i])animated[i].localScale=Vector3.Lerp(scaleFrom[i],scaleTo[i],p);
                _animatedWritten[i]=true;_animatedProgress[i]=p;_wobbleTicks[i]=tick;
            }
            if(actors!=null && (_cachedActorPositions==null || _cachedActorPositions.Length!=actors.Length || _cachedActorYaws==null || _cachedActorYaws.Length!=actors.Length || _cachedActorPoses==null || _cachedActorPoses.Length!=actors.Length))
            {
                _cachedActorPositions=new Vector3[actors.Length];_cachedActorYaws=new float[actors.Length];_cachedActorPoses=new int[actors.Length];
                for(int i=0;i<actors.Length;i++)_cachedActorPoses[i]=-100;
            }
            if(actors!=null) for(int i=0;i<actors.Length;i++)
            {
                var actor=actors[i];if(actor==null || actor.animator==null) continue;
                int at=cue*actors.Length+i;
                int before=Mathf.Max(0,cue-1)*actors.Length+i;
                float moveDuration=cue==7 || cue==8?2.56f:1.4f;
                float p=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-starts[cue])/moveDuration));
                Vector3 actorPosition=Vector3.Lerp(actorPositions[before],actorPositions[at],p);
                if(_cachedActorPositions[i]!=actorPosition){actor.transform.localPosition=actorPosition;_cachedActorPositions[i]=actorPosition;}
                float yaw=Mathf.LerpAngle(actorYaws[before],actorYaws[at],p);
                Vector3 delta=actorPositions[at]-actorPositions[before];
                bool walking=delta.sqrMagnitude>.08f && time-starts[cue]<moveDuration;
                if(walking)
                {
                    float direction=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                    float turn=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-starts[cue])/.35f));
                    float settle=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-starts[cue]-moveDuration+.35f)/.35f));
                    yaw=Mathf.LerpAngle(Mathf.LerpAngle(actorYaws[before],direction,turn),actorYaws[at],settle);
                }
                if(_cachedActorYaws[i]!=yaw){actor.transform.localRotation=Quaternion.Euler(0,yaw,0);_cachedActorYaws[i]=yaw;}
                int pose=actorPoses[at];
                bool result=actorResultPoses!=null && at<actorResultPoses.Length && time>=ends[cue]-2.56f;
                if(result) pose=actorResultPoses[at];
                if(walking) pose=6;
                if(_cachedActorPoses[i]!=pose){actor.animator.SetInteger("Pose",pose);_cachedActorPoses[i]=pose;}
                float phase=Mathf.Repeat(time*.24f+i*.23f,1f);
                if(pose==6)phase=Mathf.Repeat((time-starts[cue])/.9f,1);
                else if(pose!=0 && pose!=1)
                {
                    float part=result?(time-ends[cue]+2.56f)/2.56f:(time-starts[cue])/Mathf.Max(1,ends[cue]-starts[cue]-2.56f);
                    phase=Mathf.Clamp01(.05f+part*.75f+i*.025f);
                }
                actor.animator.SetFloat("PoseTime",phase);
            }
            if(_lastCue!=cue && contactScreens!=null && contactScreenMaterials!=null)
                for(int i=0;i<contactScreens.Length;i++)
                    if(contactScreens[i]!=null)contactScreens[i].sharedMaterial=contactScreenMaterials[cue];
            if(captionText!=null && (_lastCue!=cue)) captionText.text=captions[cue];
            _lastCue=cue;
            int chapter=time<30.72f?0:time<102.4f?1:2;
            if(chapterText!=null && chapter!=_lastChapter){chapterText.text=chapter==0?"01  接入":chapter==1?"02  繁荣":"03  条件改变";_lastChapter=chapter;}
            int wholeSecond=Mathf.FloorToInt(time);
            if(progressText!=null && wholeSecond!=_shownSecond)
            {
                _shownSecond=wholeSecond;
                int sec=wholeSecond;string s=(sec%60).ToString();if(sec%60<10) s="0"+s;
                progressText.text=(sec/60).ToString()+":"+s+" / 2:13";
            }
            if(endCard!=null && endCard.activeSelf!=(time>=133.11f)) endCard.SetActive(time>=133.11f);
        }
    }
}
