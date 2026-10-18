using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWEpisodeMontage : UdonSharpBehaviour
    {
        public Material[] imageMaterials;
        public int[] imageWidths;
        public int[] imageHeights;
        public float[] cutTimes;
        public int[] cutImages;
        public int[] cutLayouts;
        public string[] cutWords;
        public string[] cutLabels;
        public Renderer[] imageSlots;
        public Transform[] slotRoots;
        public Transform[] slotPlanes;
        public TMP_Text focusWord;
        public Transform wordTransform;
        public TMP_Text imageLabel;
        public TMP_Text greeting;
        public Transform greetingRoot;
        public Transform[] spectrumBars;
        public Vector3[] spectrumOrigins;
        public float[] spectrum0;
        public float[] spectrum1;
        public float[] spectrum2;
        public float[] spectrum3;
        public int bandCount=16;
        public float spectrumStep=.04f;
        public int spectrumFrames;
        public Transform[] energyObjects;
        public Vector3[] energyOrigins;
        public Light[] channelLights;
        public Transform[] letterBlocks;
        public Vector3[] letterOrigins;
        public Transform logoRoot;
        public Transform[] starLayers;
        public OWEpisodeMusic music;
        public int sampledCut=-1;
        public int cutChanges;
        public float sampledTime;
        public float[] channelEnergy;
        private int _cut=-1;
        private int _tick=-1;
        private int _beat=-1;
        private int _half=-1;
        private float _lastTime=-1;
        private float[] _barHeights;

        private void Start()
        {
            _cut=-1;_tick=-1;_beat=-1;_half=-1;_lastTime=-1;_barHeights=null;sampledCut=-1;cutChanges=0;
            channelEnergy=new float[4];
        }
        public void Sample(float time)
        {
            sampledTime=time;
            if(cutTimes==null || cutTimes.Length==0) return;
            if(_cut<0 || _cut>=cutTimes.Length || time<cutTimes[_cut]) _cut=0;
            while(_cut+1<cutTimes.Length && cutTimes[_cut+1]<=time) _cut++;
            if(sampledCut!=_cut || time<_lastTime)
            {
                sampledCut=_cut;cutChanges++;
                if(focusWord!=null) focusWord.text=cutWords[_cut];
                if(imageLabel!=null) imageLabel.text=cutLabels[_cut];
                int layout=cutLayouts[_cut];
                for(int slot=0;slot<imageSlots.Length;slot++)
                {
                    int use=_cut-slot;
                    if(slot==1) use=_cut-2;
                    if(slot==2) use=_cut-5;
                    if(slot==3) use=_cut-8;
                    if(use<0) use=0;
                    int image=cutImages[use];
                    imageSlots[slot].sharedMaterial=imageMaterials[image];
                    float width=slot==0?4.8f:2.45f;
                    if(slot==0 && layout==2) width=3.55f;
                    float height=width*imageHeights[image]/Mathf.Max(1f,imageWidths[image]);
                    if(height>3.25f){width*=3.25f/height;height=3.25f;}
                    slotPlanes[slot].localScale=new Vector3(width,height,1);
                }
            }
            int half=Mathf.FloorToInt(time/.32f);
            if(half!=_half && imageSlots.Length>3)
            {
                int back=Mathf.Max(0,_cut-(half%2==0?3:9));int image=cutImages[back];imageSlots[3].sharedMaterial=imageMaterials[image];
                float h=2.45f*imageHeights[image]/Mathf.Max(1f,imageWidths[image]);slotPlanes[3].localScale=new Vector3(2.45f,Mathf.Min(3.25f,h),1);_half=half;
            }
            float age=Mathf.Max(0,time-cutTimes[_cut]);
            float settle=1-Mathf.Exp(-age*21f);
            int form=cutLayouts[_cut];
            bool hub=time>=81.92f && time<92.16f;
            slotRoots[0].localPosition=new Vector3(hub?2.8f:form==2?-.7f:0,hub?2.7f:2.25f,hub?7.2f:4.15f+(1-settle)*.45f);
            slotRoots[0].localRotation=Quaternion.Euler(0,form==1?Mathf.Lerp(-7,0,settle):form==2?7:0,form==3?Mathf.Lerp(-3,0,settle):0);
            slotRoots[0].localScale=Vector3.one*(.94f+.06f*settle)*(hub?.68f:1f);
            for(int slot=1;slot<slotRoots.Length;slot++)
            {
                float side=slot%2==0?1:-1;
                float distance=time<30.72f?7.2f:6.3f;
                float x=side*(3.5f+(slot-1)*.35f);
                slotRoots[slot].localPosition=new Vector3(x,1.8f+(slot%3)*.6f,distance+(slot-1)*.5f);
                slotRoots[slot].localRotation=Quaternion.Euler(0,-side*(10+slot*4),side*(slot==3?5:0));
                slotRoots[slot].gameObject.SetActive(time>=10.24f || slot==1);
            }
            if(focusWord!=null)
            {
                float wordScale=1+.08f*Mathf.Exp(-age*13);
                wordTransform.localScale=Vector3.one*wordScale;
                focusWord.color=new Color(.94f,.88f,.57f,form==3?1f:.92f);
            }
            int beat=Mathf.FloorToInt(time/.64f);
            if(greetingRoot!=null) greetingRoot.localPosition=new Vector3(5.8f-Mathf.Repeat(time*.65f,15.6f),.52f,2.25f);
            if(greeting!=null && beat!=_beat)
                greeting.color=beat%4==0?new Color(.45f,.9f,.95f):beat%4==1?new Color(1,.79f,.42f):beat%4==2?new Color(.82f,.58f,1):new Color(.61f,.94f,.57f);
            _beat=beat;
            int tick=Mathf.FloorToInt(time*25);
            if(tick!=_tick)
            {
                if(channelEnergy==null || channelEnergy.Length!=4)channelEnergy=new float[4];
                if(_barHeights==null || _barHeights.Length!=spectrumBars.Length)_barHeights=new float[spectrumBars.Length];
                float open=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-81.92f)/1.28f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((time-90.24f)/1.92f)));
                float sample=Mathf.Clamp(time/spectrumStep,0,spectrumFrames-1);
                int frame=Mathf.FloorToInt(sample);int next=Mathf.Min(frame+1,spectrumFrames-1);float frac=sample-frame;
                for(int ch=0;ch<4;ch++)
                {
                    float energy=0;
                    for(int b=0;b<bandCount;b++)
                    {
                        int a=frame*bandCount+b,n=next*bandCount+b;
                        float value=ch==0?Mathf.Lerp(spectrum0[a],spectrum0[n],frac):ch==1?Mathf.Lerp(spectrum1[a],spectrum1[n],frac):ch==2?Mathf.Lerp(spectrum2[a],spectrum2[n],frac):Mathf.Lerp(spectrum3[a],spectrum3[n],frac);
                        energy+=value;
                        int index=ch*bandCount+b;
                        float height=.025f+value*(.32f+open*.32f);
                        _barHeights[index]=height;
                        if(spectrumBars[index]!=null)
                        {
                            spectrumBars[index].localScale=new Vector3(.037f,height,.03f);
                            spectrumBars[index].localPosition=spectrumOrigins[index]+new Vector3(0,height*.5f,0);
                        }
                    }
                    energy/=bandCount;channelEnergy[ch]=energy;
                    if(energyObjects!=null && ch<energyObjects.Length && energyObjects[ch]!=null)
                    {
                        energyObjects[ch].localPosition=energyOrigins[ch]+new Vector3(0,Mathf.Sin(time*1.6f+ch)*(.05f+open*.45f),-open*(ch%2==0?.6f:.1f));
                        energyObjects[ch].localScale=Vector3.one*(.08f+energy*(.16f+open*.35f));
                        energyObjects[ch].localRotation=Quaternion.Euler(time*(18+ch*4),time*(30+ch*3),time*12);
                    }
                    if(channelLights!=null && ch<channelLights.Length && channelLights[ch]!=null)channelLights[ch].intensity=energy*(.08f+open*.45f);
                }
                if(logoRoot!=null)
                {
                    logoRoot.gameObject.SetActive(time<10.24f || time>=81.92f && time<92.16f || time>=128f);
                    logoRoot.localRotation=Quaternion.Euler(Mathf.Sin(time*.8f)*10,Mathf.Sin(time*.61f)*24,Mathf.Sin(time*.42f)*4);
                    for(int i=0;i<letterBlocks.Length;i++)
                        letterBlocks[i].localPosition=letterOrigins[i]+new Vector3(0,Mathf.Sin(time*2.4f+letterOrigins[i].x*3)*.065f,Mathf.Sin(time*1.8f+letterOrigins[i].x*2+letterOrigins[i].y*4)*.22f);
                }
                if(starLayers!=null)for(int i=0;i<starLayers.Length;i++)starLayers[i].localRotation=Quaternion.Euler(0,0,time*(i+1)*.6f);
                _tick=tick;
            }
            _lastTime=time;
        }
    }
}
