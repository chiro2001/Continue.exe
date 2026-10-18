using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4Journey : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public Transform worldFrame;
        public Transform[] openingShells;
        public Vector3[] shellPositions;
        public Vector3[] shellExits;
        public bool autoSample=true;
        public float sampledTime;
        public Vector3 sampledVirtualPosition;
        private void LateUpdate(){if(autoSample && director!=null)Sample(director.Elapsed());}
        public Vector3 RegionOffset(int region)
        {
            return new Vector3(0,0,region==1?24f:region==2?52f:region==3?80f:0f);
        }
        public int Region(float time){return time<79.36f?0:time<86.40f?1:time<102.40f?2:3;}
        public Vector3 VirtualPosition(float time)
        {
            float z=24*Ease((time-79.36f)/2.56f)+28*Ease((time-86.40f)/1.92f)+28*Ease((time-102.40f)/2.56f);
            return new Vector3(0,0,z);
        }
        public void Sample(float time)
        {
            sampledTime=time;sampledVirtualPosition=VirtualPosition(time);
            if(worldFrame!=null){worldFrame.localPosition=-sampledVirtualPosition;worldFrame.localRotation=Quaternion.identity;}
            float withdrawal=Ease((time-30.72f)/15.36f);
            if(openingShells!=null)for(int i=0;i<openingShells.Length;i++)
            {
                if(openingShells[i]==null)continue;
                openingShells[i].gameObject.SetActive(withdrawal<.999f);
                openingShells[i].localPosition=shellPositions[i]+shellExits[i]*withdrawal;
            }
        }
        private float Ease(float t){return Mathf.SmoothStep(0,1,Mathf.Clamp01(t));}
    }
}
