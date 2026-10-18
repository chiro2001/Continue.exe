using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4Foley : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWV4Journey journey;
        public AudioSource[] sources;
        public float[] eventTimes;
        public int[] eventClips;
        public int[] eventEmitters;
        public Transform[] emitters;
        // Content/UI Foley has one physical point throughout the performance.
        // Keyboard events use the real keyboard emitter at the onset instead.
        // Sources live outside worldFrame and keep that onset position while a
        // one-shot plays; virtual travel never transports a playing sound.
        public Vector3 contentWorldPosition = new Vector3(0, 2.1f, 5.6f);
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
                        sources[clip].transform.position=EmitterWorldPosition(emitter);
                        sources[clip].PlayOneShot(sources[clip].clip);
                    }
                }
                _cursor++;
            }
            _last=time;
        }
        public Vector3 EmitterWorldPosition(int emitter)
        {
            if(emitter==0)return contentWorldPosition;
            if(emitters!=null && emitter>=0 && emitter<emitters.Length && emitters[emitter]!=null)return emitters[emitter].position;
            return contentWorldPosition;
        }
    }
}
