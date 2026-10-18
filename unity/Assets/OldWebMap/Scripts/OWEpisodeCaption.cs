using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWEpisodeCaption : UdonSharpBehaviour
    {
        public Vector3 headOffset=new Vector3(0,-.33f,1.65f);
        public Transform fallbackCamera;
        private void LateUpdate()
        {
            if(Utilities.IsValid(Networking.LocalPlayer))
            {
                VRCPlayerApi.TrackingData head=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                transform.SetPositionAndRotation(head.position+head.rotation*headOffset,head.rotation);
            }
            else if(fallbackCamera!=null)transform.SetPositionAndRotation(fallbackCamera.position+fallbackCamera.rotation*headOffset,fallbackCamera.rotation);
        }
    }
}
