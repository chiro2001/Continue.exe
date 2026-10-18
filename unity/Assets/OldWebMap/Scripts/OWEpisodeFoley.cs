using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWEpisodeFoley : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWEpisodeStage stage;
        public AudioSource[] sources;
        public float[] eventTimes;
        public int[] eventClips;
        public int[] eventEmitters;
        public Transform[] emitters;
        private int _cursor;
        private int _revision=-100;
        private float _last;
        private void Update()
        {
            if(director==null || eventTimes==null || sources==null) return;
            float time=director.Elapsed();int revision=director.Revision();
            if(revision!=_revision || time<_last || time-_last>.4f)
            {
                _revision=revision;_cursor=0;
                while(_cursor<eventTimes.Length && eventTimes[_cursor]<=time) _cursor++;
                for(int i=0;i<sources.Length;i++) sources[i].Stop();
            }
            if(director.IsPlaying())while(_cursor<eventTimes.Length && eventTimes[_cursor]<=time)
            {
                if(time-eventTimes[_cursor]<.18f)
                {
                    int clip=eventClips[_cursor],emitter=eventEmitters[_cursor];
                    if(clip>=0 && clip<sources.Length && sources[clip]!=null)
                    {
                        if(emitter==0 && stage!=null && stage.cueRoots!=null)
                            sources[clip].transform.position=stage.cueRoots[stage.sampledCue].TransformPoint(new Vector3(0,2.1f,5.6f));
                        else if(emitter>=0 && emitter<emitters.Length && emitters[emitter]!=null) sources[clip].transform.position=emitters[emitter].position;
                        sources[clip].PlayOneShot(sources[clip].clip);
                    }
                }
                _cursor++;
            }
            _last=time;
        }
    }
}
