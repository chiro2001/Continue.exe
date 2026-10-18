using UdonSharp;
using UnityEngine;
using TMPro;

namespace OldWebMap
{
    // All geometry/materials are persisted by the V4 builder. Every scene pose is
    // local to Journey.worldFrame; no camera-offset compensation is performed.
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4Montage : UdonSharpBehaviour
    {
        public OWV4Journey journey;
        public OWEpisodeDirector director;
        public OWEpisodeMusic music;
        public bool autoSample=true;
        public Transform[] windows;
        public Transform[] imagePlanes;
        public Renderer[] imageRenderers;
        public Material[] windowMaterials;
        public Transform[] cursors;
        public TMP_Text[] dateTexts;
        public Transform[] dateTransforms;
        public Texture2D[] imageTextures;
        public int[] slotFirst;
        public int[] slotCount;
        public float[] starts;
        public float[] ends;
        public float[] enters;
        public float[] exits;
        public float[] holds;
        public int[] roles;
        public int[] regions;
        public int[] images;
        public int[] resultImages;
        public int[] channels;
        public Vector3[] from;
        public Vector3[] to;
        public Vector3[] away;
        public float[] widths;
        public float[] heights;
        public string[] years;
        public string[] webTitles;
        public bool[] hasCursor;
        public Vector2[] cursorFrom;
        public Vector2[] cursorUV;
        public float[] cursorAt;
        public bool[] cursorDetach;
        public float overloadAt=61.44f;
        public Transform[] ambientRegions;
        public Transform[] ambientWindows;
        public Vector3[] ambientPositions;
        public Vector3[] ambientScales;
        public float[] ambientReveals;
        public int[] ambientChannels;
        public int ambientPerRegion=48;
        // Authored by ApplyPresentationLayout. The four extra quads reuse the
        // existing texture library; their rows never replace the primary image.
        public bool usePresentationLayout;
        public Vector3 layoutReferenceEye=new Vector3(0,1.35f,-2.8f);
        public float layoutViewerRadius=.3f;
        public Quaternion[] eventRotations;
        public Vector4[] authoredAmbientBounds;
        public Vector4[] authoredEventBounds;
        public Transform[] supportPlanes;
        public Material[] supportMaterials;
        public float[] supportStarts;
        public float[] supportEnds;
        public int[] supportCounts;
        public int[] supportKinds;
        public int[] supportFocusEvents;
        public int[] supportImages;
        public Vector3[] supportPositions;
        public Vector3[] supportScales;
        public Vector3[] supportProtectedPositions;
        public Vector3[] supportProtectedScales;
        public int activeSupport;
        public int sampledSupport=-1;
        public int spacingSuppressedAmbient;
        public Transform[] typeRegions;
        public Transform[] typeWords;
        public Material typeMaterial;
        public Transform[] commentRoots;
        public TMP_Text[] commentTexts;
        public float[] commentTimes;
        public float[] commentDurations;
        public string[] commentStrings;
        public int[] commentRegions;
        public int[] commentChannels;
        public int commentsPerRegion=16;
        public Transform[] keyRoots;
        public TMP_Text[] keyTexts;
        public Renderer[] keyPanels;
        public Material blackPanel;
        public Material whitePanel;
        public float[] keyStarts;
        public float[] keyEnds;
        public string[] keyStrings;
        public int[] keyRegions;
        public int[] keyStyles;
        public float sampledTime;
        public int activeFocus;
        public int activePeriphery;
        public int activeAmbient;
        public int activeComments;
        public int binarySearches;
        public int focusPhase=-1;
        public int sampledKey=-1;
        private int[] _cursor;
        private int[] _written;
        private int[] _shownImage;
        private bool[] _visible;
        private bool[] _ambientVisible;
        private int[] _commentActive;
        private int _nextComment;
        private int _key=-1;
        private float _lastTime=-1;
        private int _tick=-1;
        private int _n0;private int _n1;private int _n2;private int _n3;
        private Vector4[] _ambientBoxes;
        private Vector4[] _priorityBoxes;
        private Vector4[] _fixedAmbientBoxes;
        private Vector4[] _fixedEventBoxes;
        private int _priorityCount;
        private int _acceptedAmbient;
        private int _supportWritten=-2;

        private void Start(){_cursor=null;_lastTime=-1;_tick=-1;_key=-1;_nextComment=0;}
        private void LateUpdate(){if(autoSample && director!=null)Sample(director.Elapsed());}
        public void Seek(float time){_lastTime=-1;_tick=-1;Sample(time);}
        public void ResetPresentationLayoutCache(){_fixedAmbientBoxes=null;_fixedEventBoxes=null;_lastTime=-1;_tick=-1;}
        private void Init()
        {
            _cursor=new int[windows.Length];_written=new int[windows.Length];_shownImage=new int[windows.Length];_visible=new bool[windows.Length];
            for(int i=0;i<windows.Length;i++){_cursor[i]=-1;_written[i]=-2;windows[i].gameObject.SetActive(false);}
            _ambientVisible=new bool[ambientWindows.Length];
            _ambientBoxes=new Vector4[48];_priorityBoxes=new Vector4[32];_supportWritten=-2;
            if(supportPlanes!=null)for(int i=0;i<supportPlanes.Length;i++)if(supportPlanes[i]!=null)supportPlanes[i].gameObject.SetActive(false);
            // The saved/editor preview can leave future-region children active
            // beneath a hidden parent. Align real visibility with the fresh cache
            // before a later journey opens that parent for the first time.
            for(int i=0;i<ambientWindows.Length;i++)ambientWindows[i].gameObject.SetActive(false);
            _commentActive=new int[commentRoots.Length];
            for(int i=0;i<_commentActive.Length;i++)_commentActive[i]=-1;
            _fixedAmbientBoxes=new Vector4[ambientPerRegion];_fixedEventBoxes=new Vector4[starts.Length];
            if(usePresentationLayout && authoredAmbientBounds!=null && authoredAmbientBounds.Length==ambientPerRegion && authoredEventBounds!=null && authoredEventBounds.Length==starts.Length)
            {_fixedAmbientBoxes=authoredAmbientBounds;_fixedEventBoxes=authoredEventBounds;}
            else if(usePresentationLayout)
            {
                for(int i=0;i<ambientPerRegion;i++)_fixedAmbientBoxes[i]=ProjectedBox(ambientPositions[i],ambientWindows[i].localRotation,ambientScales[i]*1.015f,layoutReferenceEye,layoutViewerRadius+.065f);
                for(int e=0;e<starts.Length;e++)if(roles[e]==0)_fixedEventBoxes[e]=ProjectedBox(to[e],eventRotations[e],new Vector3(widths[e],heights[e]+.3f,1)*1.02f,layoutReferenceEye+journey.RegionOffset(regions[e]),layoutViewerRadius+.08f);
            }
        }
        public void Sample(float time)
        {
            if(windows==null || starts==null)return;
            // Recompile/editor deserialization may restore empty private arrays.
            if(_cursor==null || _cursor.Length!=windows.Length || _written==null || _written.Length!=windows.Length || _shownImage==null || _shownImage.Length!=windows.Length || _visible==null || _visible.Length!=windows.Length || _ambientVisible==null || _ambientVisible.Length!=ambientWindows.Length || _commentActive==null || _commentActive.Length!=commentRoots.Length || _ambientBoxes==null || _ambientBoxes.Length!=48 || _priorityBoxes==null || _priorityBoxes.Length!=32 || _fixedAmbientBoxes==null || _fixedAmbientBoxes.Length!=ambientPerRegion || _fixedEventBoxes==null || _fixedEventBoxes.Length!=starts.Length)Init();
            bool seek=_lastTime<0 || time<_lastTime || time-_lastTime>.5f;
            int tick=Mathf.FloorToInt(time*30);if(tick==_tick && !seek)return;
            sampledTime=time;activeFocus=0;activePeriphery=0;focusPhase=-1;
            if(music!=null)
            {
                _n0=NoteIndex(music.notes0,time,_n0);_n1=NoteIndex(music.notes1,time,_n1);
                _n2=NoteIndex(music.notes2,time,_n2);_n3=NoteIndex(music.notes3,time,_n3);
            }
            for(int slot=0;slot<windows.Length;slot++)
            {
                int index=seek?FindEvent(slot,time):_cursor[slot];
                int first=slotFirst[slot],limit=first+slotCount[slot];
                if(index<first)index=first-1;
                if(!seek)while(index+1<limit && starts[index+1]<=time)index++;
                _cursor[slot]=index;
                bool visible=index>=first && index<limit && time>=starts[index] && time<ends[index];
                if(_visible[slot]!=visible || seek){windows[slot].gameObject.SetActive(visible);_visible[slot]=visible;}
                if(!visible)continue;
                if(_written[slot]!=index || seek){WriteEvent(slot,index);_written[slot]=index;}
                float age=time-starts[index],duration=ends[index]-starts[index];
                float note=VoicePulse(channels[index],time);
                Vector3 position;float scale;float yaw=0,roll=0;
                if(roles[index]==1)
                {
                    activeFocus++;float enter=Mathf.Max(.05f,enters[index]),exit=Mathf.Max(.05f,exits[index]);
                    float leaveAt=duration-exit;
                    if(age<enter)
                    {
                        float p=Ease(age/enter);position=Vector3.Lerp(from[index],to[index],p);
                        scale=.84f+.16f*p;yaw=(1-p)*(from[index].x>to[index].x?10:-10);focusPhase=0;
                    }
                    else if(age<leaveAt)
                    {
                        // A recognizable anchor stays within centimetres of its
                        // destination, instead of drifting across the entire screen.
                        float holdAge=age-enter;
                        position=to[index]+new Vector3(Mathf.Sin(holdAge*.5f)*.025f,Mathf.Sin(holdAge*.63f)*.018f,0);
                        scale=1;yaw=Mathf.Sin(holdAge*.27f)*1.2f;focusPhase=1;
                    }
                    else
                    {
                        float p=Ease((age-leaveAt)/exit);position=Vector3.Lerp(to[index],away[index],p);
                        scale=1-.16f*p;yaw=p*(away[index].x>to[index].x?-10:10);focusPhase=2;
                    }
                }
                else
                {
                    activePeriphery++;float p=Ease(age/Mathf.Max(.04f,duration));
                    position=Vector3.Lerp(from[index],to[index],p);
                    float lane=channels[index]>=0?-1.8f+channels[index]*1.2f:position.x;
                    position.x+=Mathf.Clamp(lane-position.x,-1,1)*note*.045f;
                    position.y+=Mathf.Sin(age*1.7f+slot)*.035f;
                    scale=.94f+.06f*Mathf.Sin(p*Mathf.PI);yaw=(slot%2==0?-1:1)*4;
                }
                windows[slot].localPosition=position;windows[slot].localRotation=usePresentationLayout && roles[index]==0 && eventRotations!=null && eventRotations.Length==starts.Length?eventRotations[index]:Quaternion.Euler(0,yaw,roll);windows[slot].localScale=Vector3.one*scale;
                bool showCursor=hasCursor[index] && age>=Mathf.Min(enters[index],.45f)+.12f;
                cursors[slot].gameObject.SetActive(showCursor);
                if(showCursor)
                {
                    float at=cursorAt[index],approach=Ease((age-at+.38f)/.38f);
                    Vector2 uv=Vector2.Lerp(cursorFrom[index],cursorUV[index],approach);
                    float detach=time>=overloadAt && cursorDetach[index]?Ease((age-at-.3f)/.8f):0;
                    cursors[slot].localPosition=new Vector3((uv.x-.5f)*widths[index],(.5f-uv.y)*heights[index],-.018f-detach*.22f);
                    cursors[slot].localRotation=Quaternion.Euler(detach*Mathf.Sin(time*.8f)*12,detach*Mathf.Sin(time*.7f)*16,0);
                    float click=age>=at?Mathf.Exp(-(age-at)*22):0;
                    cursors[slot].localScale=Vector3.one*(.06f-click*.008f);
                    if(resultImages[index]>=0 && age>=at+.14f && _shownImage[slot]!=resultImages[index]){windowMaterials[slot].mainTexture=imageTextures[resultImages[index]];_shownImage[slot]=resultImages[index];}
                }
            }
            SampleSupport(time,seek);SampleKeyline(time,seek);
            SampleAmbient(time);SampleTypography(time);SampleComments(time,seek);
            _lastTime=time;_tick=tick;
        }
        private void WriteEvent(int slot,int index)
        {
            windowMaterials[slot].mainTexture=imageTextures[images[index]];_shownImage[slot]=images[index];
            imagePlanes[slot].localScale=new Vector3(widths[index],heights[index],1);
            string meta=years[index];if(!string.IsNullOrEmpty(webTitles[index]))meta=string.IsNullOrEmpty(meta)?webTitles[index]:meta+" · "+webTitles[index];
            dateTexts[slot].text=meta;dateTransforms[slot].gameObject.SetActive(!string.IsNullOrEmpty(meta));
            dateTransforms[slot].localPosition=new Vector3(0,heights[index]*.5f+.14f,-.025f);
        }
        private void SampleAmbient(float time)
        {
            activeAmbient=0;spacingSuppressedAmbient=0;_acceptedAmbient=0;
            if(usePresentationLayout)PrepareProtection(time);
            int stableRegion=StableProjectionRegion(time);
            int total=Mathf.Clamp(Mathf.RoundToInt(8+40*Ease((time-5.12f)/97.28f)),8,ambientPerRegion);
            for(int region=0;region<4;region++)
            {
                int budget=RegionBudget(region,time,total);ambientRegions[region].gameObject.SetActive(budget>0);
                for(int i=0;i<ambientPerRegion;i++)
                {
                    int index=region*ambientPerRegion+i;bool visible=i<budget && time>=ambientReveals[i];
                    if(visible && usePresentationLayout)
                    {
                        // Test the complete pulse/movement envelope, rather than
                        // the current quiet frame. Rejection only affects retained
                        // history; every scored fast event keeps its authored lane.
                        Vector3 eye=layoutReferenceEye+(journey!=null?journey.VirtualPosition(time):Vector3.zero);
                        Vector3 p=ambientRegions[region].localPosition+ambientPositions[index];
                        Vector4 box=stableRegion==region?_fixedAmbientBoxes[i]:ProjectedBox(p,ambientWindows[index].localRotation,ambientScales[index]*1.015f,eye,layoutViewerRadius+.065f);
                        bool blocked=ProtectedBox(box);
                        for(int accepted=0;accepted<_acceptedAmbient && !blocked;accepted++)if(BoxesOverlap(box,_ambientBoxes[accepted]))blocked=true;
                        if(blocked){visible=false;spacingSuppressedAmbient++;}
                        else if(_acceptedAmbient<48){_ambientBoxes[_acceptedAmbient]=box;_acceptedAmbient++;}
                    }
                    if(_ambientVisible[index]!=visible){ambientWindows[index].gameObject.SetActive(visible);_ambientVisible[index]=visible;}
                    if(!visible)continue;activeAmbient++;
                    float note=VoicePulse(ambientChannels[i],time);
                    float arrive=Ease((time-ambientReveals[i])/.7f);
                    float lane=-1.8f+ambientChannels[i]*1.2f;
                    ambientWindows[index].localPosition=ambientPositions[index]+new Vector3(Mathf.Clamp(lane-ambientPositions[index].x,-1,1)*note*.035f,note*.025f,0);
                    ambientWindows[index].localScale=ambientScales[index]*(.86f+.14f*arrive)*(1+note*.015f);
                }
            }
        }
        private void SampleSupport(float time,bool seek)
        {
            activeSupport=0;sampledSupport=-1;
            if(supportPlanes==null || supportStarts==null || supportEnds==null || supportCounts==null)return;
            int row=-1;
            for(int i=0;i<supportStarts.Length;i++){if(time<supportStarts[i])break;if(time<supportEnds[i])row=i;}
            sampledSupport=row;
            for(int slot=0;slot<supportPlanes.Length;slot++)
            {
                bool visible=row>=0 && slot<supportCounts[row];supportPlanes[slot].gameObject.SetActive(visible);if(!visible)continue;
                int index=row*4+slot;activeSupport++;
                if(_supportWritten!=row || seek)supportMaterials[slot].mainTexture=imageTextures[supportImages[index]];
                float envelope=Ease((time-supportStarts[row])/.25f)*Ease((supportEnds[row]-time)/.3f);
                supportPlanes[slot].localPosition=supportPositions[index]+new Vector3(0,Mathf.Sin((time-supportStarts[row])*.6f+slot)*.012f,(1-envelope)*.12f);
                supportPlanes[slot].localRotation=Quaternion.identity;
                supportPlanes[slot].localScale=supportScales[index]*(.94f+.06f*envelope);
            }
            _supportWritten=row;
        }
        // Cylindrical angular bounds are independent of viewing direction, so a
        // player may turn around. The authored eye position may move only inside
        // layoutViewerRadius; arbitrary room-scale walking needs its own review.
        private Vector2 ProjectedPoint(Vector3 p)
        {
            return new Vector2(Mathf.Atan2(p.x,p.z)*Mathf.Rad2Deg,Mathf.Atan2(p.y,Mathf.Sqrt(p.x*p.x+p.z*p.z))*Mathf.Rad2Deg);
        }
        private Vector4 ProjectedBox(Vector3 position,Quaternion rotation,Vector3 size,Vector3 eye,float expand)
        {
            Vector3 delta=position-eye;Vector2 center=ProjectedPoint(delta);
            Vector3 u=rotation*Vector3.right*(size.x*.5f),v=rotation*Vector3.up*(size.y*.5f);
            Vector2 a=ProjectedPoint(delta-u-v),b=ProjectedPoint(delta-u+v),c=ProjectedPoint(delta+u-v),d=ProjectedPoint(delta+u+v);
            float yaw=Mathf.Max(Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(center.x,a.x)),Mathf.Abs(Mathf.DeltaAngle(center.x,b.x))),Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(center.x,c.x)),Mathf.Abs(Mathf.DeltaAngle(center.x,d.x))));
            float pitch=Mathf.Max(Mathf.Max(Mathf.Abs(center.y-a.y),Mathf.Abs(center.y-b.y)),Mathf.Max(Mathf.Abs(center.y-c.y),Mathf.Abs(center.y-d.y)));
            // Minimum distance to any quad point, including the viewer envelope,
            // is conservative for every point of the rectangular face.
            float distance=Mathf.Max(.08f,delta.magnitude-Mathf.Sqrt(size.x*size.x+size.y*size.y)*.5f-expand);
            float yawDistance=Mathf.Max(.08f,Mathf.Sqrt(delta.x*delta.x+delta.z*delta.z)-Mathf.Sqrt(size.x*size.x+size.y*size.y)*.5f-expand);
            float yawMargin=Mathf.Atan2(expand,yawDistance)*Mathf.Rad2Deg,pitchMargin=Mathf.Atan2(expand,distance)*Mathf.Rad2Deg;
            return new Vector4(center.x,yaw+yawMargin,center.y,pitch+pitchMargin);
        }
        private bool BoxesOverlap(Vector4 a,Vector4 b)
        {
            float yaw=a.x-b.x;if(yaw<0)yaw=-yaw;if(yaw>180)yaw=360-yaw;
            if(yaw>=a.y+b.y+.25f)return false;
            float pitch=a.z-b.z;if(pitch<0)pitch=-pitch;return pitch<a.w+b.w+.25f;
        }
        private void PrepareProtection(float time)
        {
            _priorityCount=0;int stableRegion=StableProjectionRegion(time);Vector3 eye=layoutReferenceEye+(journey!=null?journey.VirtualPosition(time):Vector3.zero);
            // Project each protected face once per tick. History pair checks then
            // compare cached rectangles instead of repeating trigonometry.
            for(int slot=0;slot<windows.Length;slot++)if(_visible[slot])
            {
                int e=_cursor[slot];Vector3 size=new Vector3(widths[e],heights[e]+(roles[e]==1?.35f:.3f),1)*1.02f;
                _priorityBoxes[_priorityCount]=roles[e]==0 && stableRegion==regions[e]?_fixedEventBoxes[e]:ProjectedBox(windows[slot].localPosition,windows[slot].localRotation,size,eye,layoutViewerRadius+.08f);_priorityCount++;
            }
            if(sampledSupport>=0)
            {
                _priorityBoxes[_priorityCount]=ProjectedBox(supportProtectedPositions[sampledSupport],Quaternion.identity,supportProtectedScales[sampledSupport],eye,layoutViewerRadius+.08f);_priorityCount++;
                for(int i=0;i<activeSupport;i++){_priorityBoxes[_priorityCount]=ProjectedBox(supportPositions[sampledSupport*4+i],Quaternion.identity,supportScales[sampledSupport*4+i],eye,layoutViewerRadius+.14f);_priorityCount++;}
            }
            if(sampledKey>=0)
            {
                int region=keyRegions[sampledKey];Transform plate=keyPanels[region].transform;
                Vector3 position=keyRoots[region].localPosition+plate.localPosition;
                _priorityBoxes[_priorityCount]=ProjectedBox(position,Quaternion.identity,plate.localScale,eye,layoutViewerRadius+.03f);_priorityCount++;
            }
        }
        private bool ProtectedBox(Vector4 box)
        {
            for(int i=0;i<_priorityCount;i++)if(BoxesOverlap(box,_priorityBoxes[i]))return true;
            return false;
        }
        private int StableProjectionRegion(float time)
        {
            if(time<79.36f)return 0;if(time<81.92f)return -1;
            if(time<86.4f)return 1;if(time<88.32f)return -1;
            if(time<102.4f)return 2;if(time<104.96f)return -1;return 3;
        }
        private int RegionBudget(int region,float time,int total)
        {
            int fromRegion=0,toRegion=0;float progress=0;
            if(time>=104.96f)fromRegion=toRegion=3;
            else if(time>=102.4f){fromRegion=2;toRegion=3;progress=Ease((time-102.4f)/2.56f);}
            else if(time>=88.32f)fromRegion=toRegion=2;
            else if(time>=86.4f){fromRegion=1;toRegion=2;progress=Ease((time-86.4f)/1.92f);}
            else if(time>=81.92f)fromRegion=toRegion=1;
            else if(time>=79.36f){fromRegion=0;toRegion=1;progress=Ease((time-79.36f)/2.56f);}
            if(fromRegion==toRegion)return region==fromRegion?total:0;
            int incoming=Mathf.RoundToInt(total*progress);return region==fromRegion?total-incoming:region==toRegion?incoming:0;
        }
        private void SampleTypography(float time)
        {
            bool show=time<25.6f || time>=128 && time<133.12f;
            float age=time<25.6f?time:time<92.16f?time-81.92f:time-128;
            for(int region=0;region<4;region++)
            {
                bool visible=show && RegionBudget(region,time,48)>0;typeRegions[region].gameObject.SetActive(visible);
                if(!visible)continue;
                for(int word=0;word<3;word++)
                {
                    int index=region*3+word;
                    // Stop front-on, then complete one full cylindrical rotation.
                    // Credit lettering stays readable for most of its longer cycle.
                    float hold=word==2?7.2f:word==1?2.2f:2.0f;
                    float turn=word==2?1.4f:word==1?3.4f:3.6f;
                    float clock=Mathf.Max(0,age-word*.65f);
                    float phase=Mathf.Repeat(clock,hold+turn);
                    float yaw=phase<hold?0:360f*Ease((phase-hold)/turn);
                    typeWords[index].localRotation=Quaternion.Euler(word==2?0:Mathf.Sin(age*.25f)*3,yaw,0);
                }
            }
            if(typeMaterial!=null)typeMaterial.SetFloat("_Pulse",(VoicePulse(0,time)+VoicePulse(2,time))*.2f);
        }
        private void SampleComments(float time,bool seek)
        {
            if(seek)
            {
                for(int i=0;i<_commentActive.Length;i++)_commentActive[i]=-1;
                int lo=0,hi=commentTimes.Length-1;_nextComment=0;
                while(lo<=hi){int mid=(lo+hi)/2;if(commentTimes[mid]<=time){_nextComment=mid+1;lo=mid+1;}else hi=mid-1;}
                for(int i=Mathf.Max(0,_nextComment-commentsPerRegion*2);i<_nextComment;i++)ActivateComment(i);
            }
            else while(_nextComment<commentTimes.Length && commentTimes[_nextComment]<=time){ActivateComment(_nextComment);_nextComment++;}
            activeComments=0;
            for(int slot=0;slot<commentRoots.Length;slot++)
            {
                int index=_commentActive[slot];float age=index<0?999:time-commentTimes[index];bool visible=index>=0 && age>=0 && age<commentDurations[index];
                commentRoots[slot].gameObject.SetActive(visible);if(!visible)continue;activeComments++;
                float p=age/commentDurations[index];int lane=index%5;
                commentRoots[slot].localPosition=new Vector3(5.8f-p*11.6f,3.95f-lane*.21f,4.6f+lane*.18f);
                commentRoots[slot].localRotation=Quaternion.identity;
            }
        }
        private void ActivateComment(int index)
        {
            int slot=commentRegions[index]*commentsPerRegion+index%commentsPerRegion;_commentActive[slot]=index;commentTexts[slot].text=commentStrings[index];
        }
        private void SampleKeyline(float time,bool seek)
        {
            int index=_key;
            if(seek || index<0)
            {
                int lo=0,hi=keyStarts.Length-1;index=-1;
                while(lo<=hi){int mid=(lo+hi)/2;if(keyStarts[mid]<=time){index=mid;lo=mid+1;}else hi=mid-1;}
            }
            else while(index+1<keyStarts.Length && keyStarts[index+1]<=time)index++;
            bool visible=index>=0 && time<keyEnds[index];
            for(int region=0;region<4;region++)keyRoots[region].gameObject.SetActive(visible && keyRegions[index]==region);
            if(visible && (index!=_key || seek))
            {
                int region=keyRegions[index];bool dark=keyStyles[index]==0;
                keyTexts[region].text=keyStrings[index];keyTexts[region].color=dark?Color.white:Color.black;
                keyPanels[region].sharedMaterial=dark?blackPanel:whitePanel;
            }
            _key=index;sampledKey=visible?index:-1;
        }
        private int FindEvent(int slot,float time)
        {
            binarySearches++;int lo=slotFirst[slot],hi=lo+slotCount[slot]-1,answer=lo-1;
            while(lo<=hi){int mid=(lo+hi)/2;if(starts[mid]<=time){answer=mid;lo=mid+1;}else hi=mid-1;}return answer;
        }
        private int NoteIndex(float[] notes,float time,int index)
        {
            if(notes==null || notes.Length==0)return -1;
            if(index<0 || index>=notes.Length || notes[index]>time){int lo=0,hi=notes.Length-1;index=-1;while(lo<=hi){int mid=(lo+hi)/2;if(notes[mid]<=time){index=mid;lo=mid+1;}else hi=mid-1;}}
            while(index+1<notes.Length && notes[index+1]<=time)index++;return index;
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
