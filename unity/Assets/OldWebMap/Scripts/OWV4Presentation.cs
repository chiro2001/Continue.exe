using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

namespace OldWebMap
{
    // Lives outside the performed world. Inactive presentation roots cannot
    // override this lifecycle by enabling individual children during sampling.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4Presentation : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWV4Journey journey;
        public OWV4HubCast cast;
        public OWV4Montage montage;
        public OWV4Oscilloscope oscilloscope;
        public OWV4Operations operations;
        public OWV4CameraFx cameraFx;
        public GameObject performanceRoot;
        public GameObject nameplateRoot;
        public GameObject overlayRoot;
        public GameObject captionRoot;
        public TMP_Text caption;
        public GameObject menuRoot;
        public TMP_Text menuTitle;
        public TMP_Text menuBody;
        public TMP_Text playLabel;
        public Transform startupObject;
        public Transform fallbackCamera;
        public bool offlinePreview;
        public bool sampledPlaying;
        public int sampledState;
        public float sampledTime;
        private int _state=-1;
        private void Start(){_state=-1;offlinePreview=false;Sample();}
        private void LateUpdate()
        {
            if(offlinePreview)return;
            Sample();
            // One clock and one explicit order. Nameplates use the head positions
            // after the shared frame has travelled, rather than last frame's pose.
            if(journey!=null)journey.Sample(sampledTime);
            if(!sampledPlaying || sampledTime<.08f || sampledTime>=director.duration)return;
            if(cast!=null)cast.Sample(sampledTime);
            if(montage!=null)montage.Sample(sampledTime);
            if(oscilloscope!=null)oscilloscope.Sample(sampledTime);
            if(operations!=null)operations.Sample(sampledTime);
            if(cameraFx!=null)cameraFx.Sample(sampledTime);
        }
        public void Sample()
        {
            if(director==null)return;
            Apply(director.Elapsed(),director.IsPlaying());
        }
        public void Apply(float time,bool playing)
        {
            sampledTime=time;sampledPlaying=playing;
            bool showing=playing && time<director.duration-.0001f;
            int state=showing?1:time>=director.duration-.01f?2:0;sampledState=state;
            Show(performanceRoot,showing&&time>=.08f);Show(nameplateRoot,showing);Show(overlayRoot,showing);Show(captionRoot,showing&&time>=1.28f);
            Show(menuRoot,!showing);
            if(startupObject!=null)startupObject.gameObject.SetActive(!showing);
            if(state!=_state)
            {
                if(!showing)PlaceMenu();
                if(menuTitle!=null)menuTitle.text=state==2?"这一段，先到这里。":"连接尚未开始";
                if(menuBody!=null)menuBody.text=state==2?"想再看一次，就从这里接通。":"按下开始，让第一扇窗口打开。";
                if(playLabel!=null)playLabel.text=state==2?"重新接通":"开始";
                if(!showing && caption!=null)caption.text="";
                _state=state;
            }
        }
        public void Preview(float time){offlinePreview=true;Apply(time,true);}
        public void Waiting(){offlinePreview=true;Apply(0,false);}
        public void Finished(){offlinePreview=true;Apply(director.duration,false);}
        public void PlaceMenu()
        {
            Vector3 head;Quaternion rotation;
            if(Utilities.IsValid(Networking.LocalPlayer))
            {VRCPlayerApi.TrackingData data=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);head=data.position;rotation=data.rotation;}
            else if(fallbackCamera!=null){head=fallbackCamera.position;rotation=fallbackCamera.rotation;}
            else return;
            Vector3 forward=rotation*Vector3.forward;forward.y=0;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;forward=forward.normalized;
            Vector3 position=head+forward*2.1f+Vector3.down*.2f;Quaternion face=Quaternion.LookRotation(forward,Vector3.up);
            if(menuRoot!=null)menuRoot.transform.SetPositionAndRotation(position,face);
            if(startupObject!=null)startupObject.SetPositionAndRotation(position+forward*.22f,face);
        }
        private void Show(GameObject go,bool on){if(go!=null&&go.activeSelf!=on)go.SetActive(on);}
    }
}
