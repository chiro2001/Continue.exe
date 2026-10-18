using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWEpisodeButton : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public int action;
        public override void Interact() { Press(); }
        public void Press()
        {
            if (director==null) return;
            if(action==0) director.Toggle();
            else if(action==1) director.Restart();
            else if(action==2) director.PlayFromConnect();
            else if(action==3) director.PlayFromCreate();
            else if(action==4) director.PlayFromConstrain();
            else director.StopAtStart();
        }
    }
}
