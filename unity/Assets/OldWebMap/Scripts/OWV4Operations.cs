using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    // All working parts are persistent scene objects authored by Operations.Build.
    // This module never moves an AudioSource or creates objects/materials at runtime.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4Operations : UdonSharpBehaviour
    {
        public OWV4Journey journey;
        public OWEpisodeDirector director;
        public OWEpisodeMusic music;
        public bool autoSample=true;
        public Transform[] groupRoots;
        public Transform[] explorerParts;
        public Vector3[] explorerPositions;
        public Transform fileTile;
        public Vector3 fileHome=new Vector3(-.12f,-.12f,-.12f);
        public Vector3 fileDrop=new Vector3(1.02f,.16f,-.85f);
        public Transform filePointer;
        public GameObject selection;
        public Transform clickRing;
        public Material clickMaterial;
        public Transform readmePage;
        public TMP_Text readmeBody;
        public string readmeContent;
        public GameObject readmeResult;
        public Transform[] subtitleParts;
        public Vector3[] subtitlePositions;
        public Transform[] miniWaves;
        public Material[] miniWaveMaterials;
        public int atlasHeight=4096;
        public int atlasFrames=3995;
        public float atlasFps=30;
        public Transform audioCursor;
        public Transform subtitleRow;
        public Transform subtitleSelection;
        public TMP_Text sourceLine;
        public TMP_Text translationLine;
        public TMP_Text correctionLine;
        public string[] sourceLines;
        public string[] translatedLines;
        public string[] correctedLines;
        public Transform subtitlePointer;
        public Transform subtitleActions;
        public Vector2 subtitleActionUV;
        public TMP_Text timingValue;
        public string[] timeCodes;
        public string[] correctedTimeCodes;
        public TMP_Text[] timelineLines;
        public Vector3 filePointerTarget;
        public Vector3 subtitlePointerTarget;
        public Transform subtitleOutput;
        public GameObject subtitleResult;
        public Material subtitleSelectionMaterial;
        public Transform[] nodes;
        public Material[] nodeMaterials;
        public TMP_Text[] nodeLabels;
        public Vector3[] nodePositions;
        public Transform[] pieceFills;
        public Vector3[] piecePositions;
        public Material[] pieceMaterials;
        public Transform[] receivePackets;
        public float[] receiveTimes;
        public float[] arrivalTimes;
        public int[] receiveChannels;
        public int[] receiveNodes;
        public Transform[] uploadPackets;
        public float[] uploadTimes;
        public int[] uploadChannels;
        public int[] uploadNodes;
        public Material fileMaterial;
        public Transform hashCursor;
        public TMP_Text torrentProgress;
        public TMP_Text torrentResult;
        public float completeAt;
        public float seedAt;
        public float sampledTime;
        public int sampledGroup=-1;
        public int readmeCharacters;
        public int editingLine;
        public int correctedCount;
        public int receivedPieces;
        public int uploadsStarted;
        public int uploadsCompleted;
        public int activePackets;
        public int sampled80msTick;
        private int _tick=-1;
        private int _group=-2;
        private int _readmeChars=-1;
        private int _subtitleTick=-1;
        private int _progress=-1;
        private int _resultPhase=-1;
        private int _uploadState=-1;
        private int _n0;private int _n1;private int _n2;private int _n3;

        private void Start(){_tick=-1;_group=-2;_readmeChars=-1;_subtitleTick=-1;_progress=-1;_resultPhase=-1;_uploadState=-1;}
        private void LateUpdate(){if(autoSample && director!=null)Sample(director.Elapsed());}
        public void Seek(float time){_tick=-1;_group=-2;_readmeChars=-1;_subtitleTick=-1;_progress=-1;_resultPhase=-1;_uploadState=-1;Sample(time);}
        public void Sample(float time)
        {
            if(groupRoots==null || groupRoots.Length!=3)return;
            sampledTime=time;
            int group=time>=10.24f && time<20.48f?0:time>=61.44f && time<71.68f?1:time>=40.96f && time<51.2f?2:-1;
            sampledGroup=group;
            if(group!=_group)
            {
                for(int i=0;i<3;i++)groupRoots[i].gameObject.SetActive(i==group);
                _group=group;_tick=-1;
            }
            int tick=Mathf.FloorToInt(time*60);
            if(tick==_tick)return;_tick=tick;
            if(music!=null)
            {
                _n0=NoteIndex(music.notes0,time,_n0);_n1=NoteIndex(music.notes1,time,_n1);
                _n2=NoteIndex(music.notes2,time,_n2);_n3=NoteIndex(music.notes3,time,_n3);
            }
            if(group==0)SampleExplorer(time);
            if(group==1)SampleSubtitles(time);
            if(group==2)SampleTorrent(time);
        }
        private void SampleExplorer(float time)
        {
            float age=time-10.24f;
            float separate=Ease((age-.5f)/1.2f);
            float open=Ease((time-15.56f)/1.08f);
            for(int i=0;i<explorerParts.Length;i++)
            {
                Vector3 p=explorerPositions[i];
                p.z+=(i%3-1)*.11f*separate;
                p.x-=open*.32f;
                explorerParts[i].localPosition=p;
            }
            selection.SetActive(time>=11.52f && time<15.56f);
            float drag=Ease((time-12.8f)/2.56f);
            Vector3 tile=Vector3.Lerp(fileHome,fileDrop,drag)+Vector3.up*(Mathf.Sin(drag*Mathf.PI)*.2f);
            fileTile.localPosition=tile;
            fileTile.localRotation=Quaternion.Euler(0,Mathf.Sin(drag*Mathf.PI)*-17,Mathf.Sin(drag*Mathf.PI)*7);
            fileTile.localScale=Vector3.one*(1-.9f*open);
            fileTile.gameObject.SetActive(open<.98f);
            Vector3 iconTarget=tile+fileTile.localRotation*new Vector3(0,.05f*(1-.9f*open),-.015f*(1-.9f*open)-.006f);
            Vector3 before=new Vector3(-1.05f,.53f,iconTarget.z);
            float select=Ease((time-10.88f)/.64f);
            Vector3 target=Vector3.Lerp(before,fileHome+new Vector3(0,.05f,-.021f),select);
            Quaternion cursorRotation=Quaternion.identity;
            if(time>=12.8f){target=iconTarget;cursorRotation=fileTile.localRotation;}
            if(open>0)
            {
                Vector3 pagePosition=Vector3.Lerp(fileDrop,new Vector3(.94f,-.04f,-.98f),open);
                Vector3 pageScale=new Vector3(.12f+.88f*open,.06f+.94f*open,1);
                Vector3 pagePoint=Vector3.Lerp(new Vector3(.6f,.51f,-.06f),new Vector3(.48f,-.30f,-.065f),Ease((time-16.64f)/.64f));
                target=pagePosition+Vector3.Scale(pagePoint,pageScale);cursorRotation=Quaternion.identity;
            }
            float click=Click(time,11.52f)+Click(time,15.36f)+Click(time,15.56f)+Click(time,18.88f);
            filePointerTarget=target;PlaceCursor(filePointer,target,cursorRotation,.095f-click*.011f);
            clickRing.localPosition=target+cursorRotation*new Vector3(0,0,-.002f);clickRing.localRotation=cursorRotation;
            clickRing.localScale=Vector3.one*(.14f+(1-Mathf.Clamp01(click))*.18f);
            clickMaterial.SetFloat("_Opacity",Mathf.Clamp01(click)*.8f);
            readmePage.gameObject.SetActive(time>=15.56f);
            readmePage.localPosition=Vector3.Lerp(fileDrop,new Vector3(.94f,-.04f,-.98f),open);
            readmePage.localScale=new Vector3(.12f+.88f*open,.06f+.94f*open,1);
            int characters=Mathf.Clamp(Mathf.FloorToInt((time-16.64f)/.045f),0,readmeContent.Length);
            readmeCharacters=characters;
            if(characters!=_readmeChars){readmeBody.text=readmeContent.Substring(0,characters);_readmeChars=characters;}
            readmeResult.SetActive(time>=18.88f);
        }
        private void SampleSubtitles(float time)
        {
            float age=time-61.44f;
            float unfold=Ease(age/.72f);
            for(int i=0;i<subtitleParts.Length;i++)
            {
                Vector3 p=subtitlePositions[i];p.z+=(i%3-1)*.14f*unfold;
                subtitleParts[i].localPosition=p;
                subtitleParts[i].localRotation=Quaternion.Euler(0,(i%2==0?-1:1)*(1-unfold)*8,0);
            }
            int frame=Mathf.Clamp(Mathf.FloorToInt(time*atlasFps),0,atlasFrames-1);
            for(int ch=0;ch<miniWaveMaterials.Length;ch++)
            {
                miniWaveMaterials[ch].SetFloat("_Row",1-(frame+.5f)/atlasHeight);
                miniWaveMaterials[ch].SetFloat("_Timeline",time);
                miniWaveMaterials[ch].SetFloat("_Energy",Envelope(ch,time));
            }
            int tick=Mathf.Max(0,Mathf.FloorToInt(age/.08f));sampled80msTick=tick;
            int line=Mathf.Min(translatedLines.Length-1,tick/16);editingLine=line;
            float progress=(tick%16)/15f;
            audioCursor.localPosition=new Vector3(-.23f+progress*1.7f,.625f,-.32f);
            int firstVisible=Mathf.Clamp(line-3,0,Mathf.Max(0,translatedLines.Length-6));
            subtitleRow.localPosition=new Vector3(.62f,-.45f-(line-firstVisible)*.07f,-.48f);
            subtitleSelection.localPosition=new Vector3(-.18f+Mathf.Clamp01(progress)*1.6f,-.035f,-.47f);
            subtitleSelectionMaterial.SetFloat("_Opacity",.18f+VoicePulse(2,time)*.16f);
            Vector3 inputTarget=subtitleSelection.localPosition+new Vector3(0,0,-.008f);
            Vector3 actionsTarget=subtitleActions.localPosition+subtitleActions.localRotation*new Vector3((subtitleActionUV.x-.5f)*subtitleActions.localScale.x,(.5f-subtitleActionUV.y)*subtitleActions.localScale.y,-.008f);
            float preview=Ease((age-7.05f)/.35f)*(1-Ease((age-8.2f)/.4f));
            subtitlePointerTarget=Vector3.Lerp(inputTarget,actionsTarget,preview);
            PlaceCursor(subtitlePointer,subtitlePointerTarget,Quaternion.Slerp(Quaternion.identity,subtitleActions.localRotation,preview),.085f-Click(age,7.6f)*.009f);
            if(tick!=_subtitleTick)
            {
                int characters=Mathf.Min(translatedLines[line].Length,tick%16+1);
                sourceLine.text=sourceLines[line];
                bool checkedLine=tick%16>=11 || age>=9.6f;
                string translated=checkedLine?correctedLines[line]:translatedLines[line].Substring(0,characters);
                translationLine.text=translated;
                correctedCount=Mathf.Min(translatedLines.Length,line+(checkedLine?1:0));
                correctionLine.text=checkedLine?"已确认":"当前行 "+(line+1)+" / "+translatedLines.Length;
                timingValue.text=checkedLine?correctedTimeCodes[line]:timeCodes[line];
                for(int row=0;row<timelineLines.Length;row++)
                {
                    int number=firstVisible+row;bool confirmed=number<line || number==line && checkedLine;
                    timelineLines[row].text=(number+1)+"  "+(confirmed?correctedLines[number]:sourceLines[number]);
                }
                _subtitleTick=tick;
            }
            float output=Ease((age-8.8f)/.65f);
            subtitleOutput.gameObject.SetActive(output>0);
            subtitleOutput.localPosition=Vector3.Lerp(new Vector3(.2f,-.22f,-.35f),new Vector3(1.25f,.12f,-.88f),output);
            subtitleOutput.localRotation=Quaternion.Euler(0,Mathf.Sin(output*Mathf.PI)*-13,0);
            subtitleOutput.localScale=Vector3.one*(.3f+.7f*output);
            subtitleResult.SetActive(age>=9.5f);
        }
        private void SampleTorrent(float time)
        {
            int complete=0,inflight=0,uploaded=0,delivered=0;
            for(int i=0;i<pieceFills.Length;i++)
            {
                bool arrived=time>=arrivalTimes[i];pieceFills[i].gameObject.SetActive(arrived);
                if(arrived)
                {
                    complete++;float settle=Ease((time-arrivalTimes[i])/.24f);
                    float merge=Ease((time-completeAt)/.45f);
                    Vector3 assembled=new Vector3((i%4-1.5f)*.22f,(1.5f-i/4)*.11f,-.24f);
                    pieceFills[i].localPosition=Vector3.Lerp(piecePositions[i],assembled,merge)+new Vector3(0,(1-settle)*.12f,-(1-settle)*.14f);
                    pieceFills[i].localScale=new Vector3(.22f,.11f,.055f)*(.3f+.7f*settle);
                }
                bool moving=time>=receiveTimes[i] && time<arrivalTimes[i];receivePackets[i].gameObject.SetActive(moving);
                if(moving)
                {
                    inflight++;float p=Mathf.Clamp01((time-receiveTimes[i])/(arrivalTimes[i]-receiveTimes[i]));
                    Vector3 start=nodePositions[receiveNodes[i]]+new Vector3(0,0,-.16f);
                    receivePackets[i].localPosition=Vector3.Lerp(start,piecePositions[i],p)+new Vector3(0,Mathf.Sin(p*Mathf.PI)*.2f,-Mathf.Sin(p*Mathf.PI)*.2f);
                    receivePackets[i].localRotation=Quaternion.Euler(p*100,p*180,p*70);
                }
            }
            for(int i=0;i<uploadPackets.Length;i++)
            {
                float age=time-uploadTimes[i];bool moving=age>=0 && age<.65f;uploadPackets[i].gameObject.SetActive(moving);
                if(age>=0)uploaded++;if(age>=.65f)delivered++;
                if(moving)
                {
                    inflight++;float p=age/.65f;
                    uploadPackets[i].localPosition=Vector3.Lerp(new Vector3(0,.02f,-.28f),nodePositions[uploadNodes[i]]+new Vector3(0,0,-.15f),p)+new Vector3(0,Mathf.Sin(p*Mathf.PI)*.22f,-Mathf.Sin(p*Mathf.PI)*.18f);
                    uploadPackets[i].localRotation=Quaternion.Euler(p*-160,p*170,p*60);
                }
            }
            receivedPieces=complete;uploadsStarted=uploaded;uploadsCompleted=delivered;activePackets=inflight;
            for(int ch=0;ch<4;ch++)
            {
                float pulse=VoicePulse(ch,time),rms=Envelope(ch,time);
                nodes[ch].localScale=Vector3.one*(1+pulse*.05f);
                nodeMaterials[ch].SetFloat("_Highlight",.1f+rms*.16f+pulse*.7f);
            }
            int phase=time>=seedAt?2:complete==pieceFills.Length?1:0;
            if(complete!=_progress || phase!=_resultPhase || delivered!=_uploadState)
            {
                torrentProgress.text=complete+" / "+pieceFills.Length+" 分片";
                torrentResult.text=phase==0?"下载中":phase==1?"下载完成 · 校验通过":"做种中 · 已上传 "+delivered+" 个分片";
                for(int node=0;node<4;node++)
                {
                    int pieces=0;for(int i=0;i<receiveNodes.Length;i++)if(receiveNodes[i]==node && time>=arrivalTimes[i])pieces++;
                    int received=0;for(int i=0;i<uploadNodes.Length;i++)if(uploadNodes[i]==node && time>=uploadTimes[i]+.65f)received++;
                    nodeLabels[node].text=phase==2?"节点 "+(node+1)+"  收到 "+received+" 包":"节点 "+(node+1)+"  提供 "+pieces+" 片";
                }
                _progress=complete;_resultPhase=phase;_uploadState=delivered;
            }
            fileMaterial.SetFloat("_Opacity",Ease((time-completeAt)/.38f));
            bool hash=time>=completeAt && time<seedAt;hashCursor.gameObject.SetActive(hash);
            if(hash)hashCursor.localPosition=new Vector3(-.55f+Mathf.Clamp01((time-completeAt)/Mathf.Max(.1f,seedAt-completeAt))*1.1f,0,-.34f);
        }
        private float Envelope(int ch,float time)
        {
            if(music==null)return 0;int index=Mathf.Max(0,Mathf.FloorToInt(time/.08f));
            if(ch==0)return music.envelope0!=null && index<music.envelope0.Length?music.envelope0[index]:0;
            if(ch==1)return music.envelope1!=null && index<music.envelope1.Length?music.envelope1[index]:0;
            if(ch==2)return music.envelope2!=null && index<music.envelope2.Length?music.envelope2[index]:0;
            return music.envelope3!=null && index<music.envelope3.Length?music.envelope3[index]:0;
        }
        private int NoteIndex(float[] notes,float time,int index)
        {
            if(notes==null || notes.Length==0)return -1;
            if(index<0 || index>=notes.Length || notes[index]>time)
            {int lo=0,hi=notes.Length-1;index=-1;while(lo<=hi){int mid=(lo+hi)/2;if(notes[mid]<=time){index=mid;lo=mid+1;}else hi=mid-1;}}
            while(index+1<notes.Length && notes[index+1]<=time)index++;return index;
        }
        private float VoicePulse(int ch,float time)
        {
            if(music==null)return 0;
            if(ch==0)return _n0>=0 && music.notes0!=null?Mathf.Exp(-(time-music.notes0[_n0])*15):0;
            if(ch==1)return _n1>=0 && music.notes1!=null?Mathf.Exp(-(time-music.notes1[_n1])*15):0;
            if(ch==2)return _n2>=0 && music.notes2!=null?Mathf.Exp(-(time-music.notes2[_n2])*15):0;
            return _n3>=0 && music.notes3!=null?Mathf.Exp(-(time-music.notes3[_n3])*15):0;
        }
        private void PlaceCursor(Transform pointer,Vector3 target,Quaternion rotation,float size)
        {
            pointer.localRotation=rotation;pointer.localScale=Vector3.one*size;
            // Pointer mesh tip is (-.3,.5); place that vertex at the real target.
            pointer.localPosition=target-rotation*(new Vector3(-.3f,.5f,0)*size);
        }
        private float Click(float time,float at){return time>=at?Mathf.Exp(-(time-at)*18):0;}
        private float Ease(float value){return Mathf.SmoothStep(0,1,Mathf.Clamp01(value));}
    }
}
