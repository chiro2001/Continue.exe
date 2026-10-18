using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    // Twenty authored window layers. Each slot walks only its own sorted events;
    // no per-frame scan over the complete score and no runtime object creation.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV3Montage : UdonSharpBehaviour
    {
        public OWEpisodeDirector director;
        public OWEpisodeMusic music;
        public bool autoSample = true;
        public Transform[] windowRoots;
        public Transform[] surfaces;
        public Transform[] backings;
        public Transform[] headerBars;
        public Transform[] pointers;
        public Transform[] actionPads;
        public TMP_Text[] windowTitles;
        public TMP_Text[] operationTexts;
        // TMP inherited component accessors are not exposed to Udon. The builder
        // serializes base-component references explicitly for runtime sampling.
        public Transform[] windowTitleTransforms;
        public RectTransform[] windowTitleRects;
        public Transform[] operationTextTransforms;
        public RectTransform[] operationTextRects;
        public Material[] slotMaterials;
        public Texture2D[] imageTextures;
        public string[] imageTitles;
        public int[] slotFirst;
        public int[] slotCount;
        public float[] starts;
        public float[] ends;
        public Vector3[] from;
        public Vector3[] to;
        public float[] widths;
        public float[] heights;
        public int[] images;
        public int[] motions;
        public int[] channels;
        public string[] labels;
        public string[] results;
        public float[] sceneStarts;
        public float[] sceneEnds;
        public string[] sceneTitles;
        public string[] sceneCaptions;
        public TMP_Text sceneTitle;
        public TMP_Text sceneCaption;
        public Transform[] commentRoots;
        public TMP_Text[] commentTexts;
        public float[] commentStarts;
        public string[] commentLabels;
        public int[] commentChannels;
        public Transform matrixRoot;
        public Material matrixMaterial;
        public Vector3 matrixCenter = new Vector3(0,2.6f,8.8f);
        public Vector3 matrixHubCenter = new Vector3(-3.2f,3.1f,8.8f);
        public Vector3 matrixOpeningCenter = new Vector3(0,2.6f,5.4f);
        public float matrixSize = 1f;
        public float sampledTime;
        public int activeWindows;
        public int activeComments;
        public int sampledScene = -1;
        public int eventTransitions;
        public int binarySearches;
        private int[] _cursor;
        private int[] _written;
        private bool[] _visible;
        private int[] _resultState;
        private int[] _commentActive;
        private int _nextComment;
        private int _scene = -1;
        private int _n0; private int _n1; private int _n2; private int _n3;
        private float _lastTime = -1;
        private int _tick = -1;

        private void Start()
        {
            _cursor=null;_written=null;_visible=null;_resultState=null;_commentActive=null;
            _lastTime=-1;_scene=-1;_tick=-1;_nextComment=0;
        }
        private void LateUpdate() { if(autoSample && director!=null)Sample(director.Elapsed()); }
        public void Seek(float time) { _lastTime=-1;_tick=-1;Sample(time); }
        private void Init()
        {
            int count=windowRoots.Length;
            _cursor=new int[count];_written=new int[count];_visible=new bool[count];_resultState=new int[count];
            for(int i=0;i<count;i++){_cursor[i]=-1;_written[i]=-2;_resultState[i]=-1;}
            int comments=commentRoots==null?0:commentRoots.Length;_commentActive=new int[comments];
            for(int i=0;i<comments;i++)_commentActive[i]=-1;
        }
        public void Sample(float time)
        {
            if(windowRoots==null || slotFirst==null || starts==null)return;
            if(_cursor==null || _cursor.Length!=windowRoots.Length)Init();
            sampledTime=time;
            bool seek=_lastTime<0 || time<_lastTime || time-_lastTime>.5f;
            int tick=Mathf.FloorToInt(time*30);
            bool newTick=tick!=_tick || seek;
            if(music!=null)
            {
                _n0=NoteIndex(music.notes0,time,_n0);_n1=NoteIndex(music.notes1,time,_n1);
                _n2=NoteIndex(music.notes2,time,_n2);_n3=NoteIndex(music.notes3,time,_n3);
            }
            activeWindows=0;
            for(int slot=0;slot<windowRoots.Length;slot++)
            {
                int index;
                if(seek)index=FindEvent(slot,time);
                else
                {
                    index=_cursor[slot];int first=slotFirst[slot],limit=first+slotCount[slot];
                    if(index<first)index=first-1;
                    while(index+1<limit && starts[index+1]<=time)index++;
                }
                _cursor[slot]=index;
                bool show=index>=slotFirst[slot] && index<slotFirst[slot]+slotCount[slot] && time>=starts[index] && time<ends[index];
                if(_visible[slot]!=show || seek){windowRoots[slot].gameObject.SetActive(show);_visible[slot]=show;}
                if(!show)continue;
                activeWindows++;
                if(_written[slot]!=index || seek){WriteEvent(slot,index);_written[slot]=index;eventTransitions++;}
                if(!newTick)continue;
                float age=time-starts[index],length=Mathf.Max(.05f,ends[index]-starts[index]);
                float progress=Mathf.Clamp01(age/length),ease=Ease(progress);
                float attack=Ease(age/Mathf.Min(.42f,length*.22f));
                float release=1-Ease((age-length+.3f)/.3f);
                float note=VoicePulse(channels[index],time);
                Vector3 position=Vector3.Lerp(from[index],to[index],ease);
                int motion=motions[index];float phase=slot*1.91f;
                if(motion==0)position+=new Vector3(Mathf.Sin(time*.53f+phase)*.055f,Mathf.Sin(time*.67f+phase)*.065f,Mathf.Sin(time*.31f+phase)*.08f);
                if(motion==1)position+=new Vector3((1-attack)*(slot%2==0?-.4f:.4f),0,0);
                if(motion==3)position+=new Vector3(0,0,Mathf.Sin(progress*Mathf.PI)*-.7f);
                if(motion==6)position+=new Vector3(0,Mathf.Sin(progress*Mathf.PI)*.22f,-Mathf.Sin(progress*Mathf.PI)*.2f);
                windowRoots[slot].position=position;
                float yaw=motion==2?Mathf.Sin(time*.43f+phase)*14f:motion==3?Mathf.Sin(progress*Mathf.PI)*8f:(slot%2==0?1:-1)*3f;
                float roll=motion==2?Mathf.Sin(time*.53f+phase)*4f:0;
                windowRoots[slot].rotation=Quaternion.Euler(0,yaw,roll);
                float stamp=motion==5?1+Mathf.Exp(-age*10)*.12f:1;
                float scale=(.87f+.13f*attack)*(.97f+.03f*release)*stamp*(1+note*.018f);
                windowRoots[slot].localScale=Vector3.one*scale;
                Material mat=slotMaterials[slot];
                mat.SetFloat("_Reveal",attack*release);mat.SetFloat("_Pulse",note*.22f);
                mat.SetFloat("_Scroll",motion==4?.001f+progress*.999f:0);
                // Cursor visibly travels from the title area into the action pad;
                // the exact arrival is the operation's trigger/result boundary.
                float actionDuration=Mathf.Min(.72f,length*.38f);
                float action=Ease(age/Mathf.Max(.12f,actionDuration));
                Vector3 cursorFrom=new Vector3(-widths[index]*.27f,heights[index]*.23f,-.075f);
                Vector3 cursorTo=new Vector3(widths[index]*.25f,-heights[index]*.32f,-.075f);
                pointers[slot].localPosition=Vector3.Lerp(cursorFrom,cursorTo,action);
                float click=Mathf.Exp(-Mathf.Max(0,age-actionDuration)*15f)*(age>=actionDuration?1:0);
                pointers[slot].localScale=Vector3.one*(.085f+click*.023f);
                actionPads[slot].localScale=new Vector3(widths[index]*.29f,.12f,.012f)*(1+click*.09f);
                int result=age>=actionDuration?1:0;
                if(_resultState[slot]!=result)
                {
                    operationTexts[slot].text=result==1?results[index]:labels[index];
                    operationTexts[slot].color=result==1?new Color(.54f,.95f,.7f):new Color(.89f,.94f,1);
                    _resultState[slot]=result;
                }
            }
            SampleScene(time,seek);
            SampleComments(time,seek,newTick);
            if(matrixRoot!=null && newTick)
            {
                float hub=Ease((time-81.92f)/1.28f)*(1-Ease((time-90.88f)/1.28f));
                bool show=time<30.72f || time>=81.92f && time<92.16f || time>=128f && time<133.12f;
                matrixRoot.gameObject.SetActive(show);
                float opening=1-Ease((time-2.56f)/2.56f);
                matrixRoot.position=Vector3.Lerp(Vector3.Lerp(matrixCenter,matrixOpeningCenter,opening),matrixHubCenter,hub);
                matrixRoot.rotation=Quaternion.Euler(time*11f,time*19f,time*7f);
                float beat=(VoicePulse(0,time)+VoicePulse(1,time)+VoicePulse(2,time)+VoicePulse(3,time))*.25f;
                matrixRoot.localScale=Vector3.one*matrixSize*(1+opening*.35f)*(1-hub*.18f)*(1+beat*.035f);
                if(matrixMaterial!=null)matrixMaterial.SetFloat("_Pulse",beat*.25f);
            }
            _tick=tick;_lastTime=time;
        }
        private void WriteEvent(int slot,int index)
        {
            int image=images[index];float width=widths[index],height=heights[index];
            slotMaterials[slot].mainTexture=imageTextures[image];
            surfaces[slot].localScale=new Vector3(width,height,1);
            backings[slot].localScale=new Vector3(width+.07f,height+.22f,.035f);
            backings[slot].localPosition=new Vector3(0,.035f,.04f);
            headerBars[slot].localScale=new Vector3(width+.065f,.15f,.022f);
            headerBars[slot].localPosition=new Vector3(0,height*.5f+.08f,-.012f);
            windowTitles[slot].text=imageTitles[image];
            windowTitleTransforms[slot].localPosition=new Vector3(0,height*.5f+.08f,-.04f);
            windowTitleRects[slot].sizeDelta=new Vector2(width-.07f,.14f);
            operationTextTransforms[slot].localPosition=new Vector3(0,-height*.5f-.09f,-.045f);
            operationTextRects[slot].sizeDelta=new Vector2(width,.14f);
            actionPads[slot].localPosition=new Vector3(width*.25f,-height*.32f,-.045f);
            _resultState[slot]=-1;
        }
        private int FindEvent(int slot,float time)
        {
            binarySearches++;
            int lo=slotFirst[slot],hi=lo+slotCount[slot]-1,answer=lo-1;
            while(lo<=hi){int mid=(lo+hi)/2;if(starts[mid]<=time){answer=mid;lo=mid+1;}else hi=mid-1;}
            return answer;
        }
        private void SampleScene(float time,bool seek)
        {
            if(sceneStarts==null || sceneStarts.Length==0)return;
            int scene=_scene;
            if(seek || scene<0 || scene>=sceneStarts.Length)
            {
                int lo=0,hi=sceneStarts.Length-1;scene=-1;
                while(lo<=hi){int mid=(lo+hi)/2;if(sceneStarts[mid]<=time){scene=mid;lo=mid+1;}else hi=mid-1;}
            }
            else while(scene+1<sceneStarts.Length && sceneStarts[scene+1]<=time)scene++;
            if(scene!=_scene || seek)
            {
                _scene=scene;sampledScene=scene;
                if(sceneTitle!=null)sceneTitle.text=scene>=0?sceneTitles[scene]:"";
                if(sceneCaption!=null)sceneCaption.text=scene>=0?sceneCaptions[scene]:"";
            }
        }
        private void SampleComments(float time,bool seek,bool newTick)
        {
            if(commentRoots==null || commentRoots.Length==0 || commentStarts==null)return;
            if(seek)
            {
                for(int slot=0;slot<_commentActive.Length;slot++)_commentActive[slot]=-1;
                int lo=0,hi=commentStarts.Length-1;_nextComment=0;
                while(lo<=hi){int mid=(lo+hi)/2;if(commentStarts[mid]<=time){_nextComment=mid+1;lo=mid+1;}else hi=mid-1;}
                for(int i=Mathf.Max(0,_nextComment-_commentActive.Length);i<_nextComment;i++)ActivateComment(i);
            }
            else while(_nextComment<commentStarts.Length && commentStarts[_nextComment]<=time){ActivateComment(_nextComment);_nextComment++;}
            if(!newTick)return;
            activeComments=0;
            for(int slot=0;slot<commentRoots.Length;slot++)
            {
                int index=_commentActive[slot];float age=index<0?10:time-commentStarts[index];bool show=age>=0 && age<3.6f;
                commentRoots[slot].gameObject.SetActive(show);if(!show)continue;activeComments++;
                int lane=index%5;float x=7.1f-age*3.95f;
                commentRoots[slot].position=new Vector3(x,3.65f-lane*.24f,5.3f+lane*.13f);
                commentRoots[slot].rotation=Quaternion.identity;
            }
        }
        private void ActivateComment(int index)
        {
            int slot=index%commentRoots.Length;_commentActive[slot]=index;commentTexts[slot].text=commentLabels[index];
            int ch=commentChannels[index];commentTexts[slot].color=ch==0?new Color(.4f,.88f,.95f):ch==1?new Color(1,.82f,.52f):ch==2?new Color(.81f,.65f,1):new Color(.64f,.93f,.72f);
        }
        private int NoteIndex(float[] notes,float time,int index)
        {
            if(notes==null || notes.Length==0)return -1;
            if(index<0 || index>=notes.Length || notes[index]>time)
            {
                int lo=0,hi=notes.Length-1;index=-1;
                while(lo<=hi){int mid=(lo+hi)/2;if(notes[mid]<=time){index=mid;lo=mid+1;}else hi=mid-1;}
            }
            while(index+1<notes.Length && notes[index+1]<=time)index++;
            return index;
        }
        private float VoicePulse(int ch,float time)
        {
            if(music==null || ch<0)return 0;
            if(ch==0)return _n0>=0 && music.notes0!=null?Mathf.Exp(-(time-music.notes0[_n0])*14):0;
            if(ch==1)return _n1>=0 && music.notes1!=null?Mathf.Exp(-(time-music.notes1[_n1])*14):0;
            if(ch==2)return _n2>=0 && music.notes2!=null?Mathf.Exp(-(time-music.notes2[_n2])*14):0;
            return _n3>=0 && music.notes3!=null?Mathf.Exp(-(time-music.notes3[_n3])*14):0;
        }
        private float Ease(float value){return Mathf.SmoothStep(0,1,Mathf.Clamp01(value));}
    }
}
