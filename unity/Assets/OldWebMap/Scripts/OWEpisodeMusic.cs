using UdonSharp;
using UnityEngine;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWEpisodeMusic : UdonSharpBehaviour
    {
        public AudioSource[] sources;
        // V3 opts in; existing v1/v2 position sampling remains the default.
        public bool externalVoicePositionDriver;
        public Transform[] voiceTransforms;
        public Vector3[] normalPositions;
        public Vector3[] expandedPositions;
        public float[] notes0; public float[] notes1; public float[] notes2; public float[] notes3;
        public float[] envelope0; public float[] envelope1; public float[] envelope2; public float[] envelope3;
        public Transform[] pulseGlyphs;
        public float[] pulseBaseScale;
        public Transform[] meterBars;
        public Vector3[] meterSizes;
        public Vector3[] meterBottoms;
        public float baseVolume=.8f;
        public float performanceEnd=133.12f;
        public float endFadeSeconds=.3f;
        private int _revision=-100;
        private bool _active;
        private float _checkAt;
        private int _n0; private int _n1; private int _n2; private int _n3;
        private float _lastOpen=-1;
        private float _lastVolume=-1;
        private int[] _meterPixels;
        public float lastScheduleDsp;
        public float lastScheduledTime;
        public int scheduleCount;
        public float measuredMaxIntertrackDrift;

        private void Start()
        {
            _revision=-100;_active=false;_lastOpen=-1;_lastVolume=-1;_meterPixels=null;
            _n0=0;_n1=0;_n2=0;_n3=0;_checkAt=0;
        }

        public void SyncPlayback(float time,bool playing,int revision,float untilAnchor)
        {
            if(sources==null || sources.Length!=4) return;
            float endGain=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((time-performanceEnd+endFadeSeconds)/Mathf.Max(.01f,endFadeSeconds)));
            float volume=baseVolume*endGain;
            if(volume!=_lastVolume){for(int i=0;i<4;i++)sources[i].volume=volume;_lastVolume=volume;}
            if(revision!=_revision || playing!=_active)
            {
                _revision=revision;_active=playing;
                for(int i=0;i<4;i++) sources[i].Stop();
                if(playing) Schedule(time,untilAnchor);
            }
            if(playing && Time.realtimeSinceStartup>_checkAt && untilAnchor<=0)
            {
                _checkAt=Time.realtimeSinceStartup+1f;
                float minTime=10000,maxTime=-10000;
                for(int i=0;i<4;i++) { float v=sources[i].time;minTime=Mathf.Min(minTime,v);maxTime=Mathf.Max(maxTime,v); }
                measuredMaxIntertrackDrift=Mathf.Max(measuredMaxIntertrackDrift,maxTime-minTime);
                if(sources[0].isPlaying && Mathf.Abs(sources[0].time-time)>.12f) Schedule(time,0);
            }
            SampleVisual(time);
        }

        public void SampleVisual(float time)
        {
            if(voiceTransforms==null) return;
            float open=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-81.92f)/2.56f));
            open*=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((time-89.6f)/2.56f));
            if(!externalVoicePositionDriver && open!=_lastOpen)
            {
                for(int i=0;i<voiceTransforms.Length;i++) voiceTransforms[i].localPosition=Vector3.Lerp(normalPositions[i],expandedPositions[i],open);
                _lastOpen=open;
            }
            _n0=NoteIndex(notes0,time,_n0);_n1=NoteIndex(notes1,time,_n1);_n2=NoteIndex(notes2,time,_n2);_n3=NoteIndex(notes3,time,_n3);
            Pulse(0,time,notes0,_n0,envelope0);Pulse(1,time,notes1,_n1,envelope1);Pulse(2,time,notes2,_n2,envelope2);Pulse(3,time,notes3,_n3,envelope3);
        }

        private void Schedule(float time,float untilAnchor)
        {
            float lead=untilAnchor>.02f?untilAnchor:.06f;
            double dsp=AudioSettings.dspTime+lead;
            float clipPosition=untilAnchor>.02f?time:time+lead;
            for(int i=0;i<4;i++)
            {
                sources[i].Stop();
                if(sources[i].clip==null) continue;
                sources[i].timeSamples=Mathf.Clamp(Mathf.RoundToInt(clipPosition*sources[i].clip.frequency),0,sources[i].clip.samples-1);
                sources[i].PlayScheduled(dsp);
            }
            lastScheduleDsp=(float)dsp;lastScheduledTime=clipPosition;scheduleCount++;
        }

        private int NoteIndex(float[] notes,float time,int index)
        {
            if(notes==null || notes.Length==0) return -1;
            if(index<0 || index>=notes.Length || notes[index]>time) index=0;
            while(index+1<notes.Length && notes[index+1]<=time) index++;
            return index;
        }

        private void Pulse(int voice,float time,float[] notes,int index,float[] envelope)
        {
            if(pulseGlyphs==null || voice>=pulseGlyphs.Length || pulseGlyphs[voice]==null) return;
            float note=0;
            if(notes!=null && index>=0 && index<notes.Length && time>=notes[index]) note=Mathf.Exp(-(time-notes[index])*12);
            float amp=0;
            int sample=Mathf.FloorToInt(time/.08f);
            if(envelope!=null && sample>=0 && sample<envelope.Length) amp=envelope[sample];
            if(time<=10.25f && meterBars!=null && voice<meterBars.Length && meterBars[voice]!=null)
            {
                if(_meterPixels==null || _meterPixels.Length!=4){_meterPixels=new int[4];for(int i=0;i<4;i++)_meterPixels[i]=-1;}
                int pixels=Mathf.Max(1,Mathf.RoundToInt(amp*65));
                if(_meterPixels[voice]!=pixels)
                {
                    float height=meterSizes[voice].y*pixels/65f;
                    meterBars[voice].localScale=new Vector3(meterSizes[voice].x,height,meterSizes[voice].z);
                    meterBars[voice].localPosition=meterBottoms[voice]+Vector3.up*(height*.5f);
                    _meterPixels[voice]=pixels;
                }
            }
            float size=pulseBaseScale[voice]*(1+note*.28f+amp*.22f);
            pulseGlyphs[voice].localScale=new Vector3(size,size,size);
        }
    }
}
