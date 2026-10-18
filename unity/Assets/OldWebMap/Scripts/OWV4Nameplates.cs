using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;

namespace OldWebMap
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OWV4Nameplates : UdonSharpBehaviour
    {
        public OWV4HubCast cast;
        public bool autoSample=true;
        public Transform observer;
        public Transform[] actorPlates;
        public TMP_Text[] actorLabels;
        public string[] actorNicknames;
        public string anonymousLabel="xxxx";
        public Transform[] playerPlates;
        public TMP_Text[] playerLabels;
        public Renderer[] plateBackgrounds;
        public Renderer[] plateBorders;
        public Material[] eraBackgrounds;
        public Material[] eraBorders;
        public int[] playerIds;
        public Vector3[] previewWorldHeads;
        public bool previewMode;
        public int sampledEra;
        public int activePlayerCount;
        public int visiblePlayerCount;
        public int overflowCount;
        public float headGap=.23f;
        public float maxDistance=24f;
        public float playerPoseInterval=.125f;
        public float rosterInterval=1f;
        public float lastSampledTime;
        private VRCPlayerApi[] _players;
        private bool _dirty=true;
        private float _nextRoster;
        private float _nextPose;
        private int _lastEra=-1;

        private void Start(){EnsurePool();_dirty=true;}
        private void LateUpdate(){if(autoSample && cast!=null && cast.director!=null)Sample(cast.director.Elapsed());}

        private void EnsurePool()
        {
            if(playerPlates==null)return;
            int count=playerPlates.Length;
            if(_players==null || _players.Length!=count)_players=new VRCPlayerApi[count];
            if(playerIds==null || playerIds.Length!=count)
            {
                playerIds=new int[count];for(int i=0;i<count;i++)playerIds[i]=-1;
            }
            if(previewWorldHeads==null || previewWorldHeads.Length!=count)previewWorldHeads=new Vector3[count];
        }

        public override void OnPlayerJoined(VRCPlayerApi player){_dirty=true;}
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if(player!=null && playerIds!=null)
                for(int i=0;i<playerIds.Length;i++)if(playerIds[i]==player.playerId){playerIds[i]=-1;Show(playerPlates[i],false);}
            _dirty=true;
        }

        public void RefreshRoster()
        {
            EnsurePool();if(previewMode || _players==null || !Utilities.IsValid(Networking.LocalPlayer))return;
            for(int i=0;i<playerIds.Length;i++)
                if(playerIds[i]>=0 && !Utilities.IsValid(VRCPlayerApi.GetPlayerById(playerIds[i])))
                {playerIds[i]=-1;Show(playerPlates[i],false);}
            VRCPlayerApi.GetPlayers(_players);overflowCount=Mathf.Max(0,VRCPlayerApi.GetPlayerCount()-playerPlates.Length);
            for(int p=0;p<_players.Length;p++)
            {
                VRCPlayerApi player=_players[p];if(!Utilities.IsValid(player))continue;
                int slot=-1;
                for(int i=0;i<playerIds.Length;i++)if(playerIds[i]==player.playerId)slot=i;
                if(slot<0)for(int i=0;i<playerIds.Length;i++)if(slot<0 && playerIds[i]<0)slot=i;
                if(slot<0)continue;
                playerIds[slot]=player.playerId;
                if(playerLabels!=null && slot<playerLabels.Length && playerLabels[slot]!=null && playerLabels[slot].text!=anonymousLabel)playerLabels[slot].text=anonymousLabel;
            }
            activePlayerCount=0;for(int i=0;i<playerIds.Length;i++)if(playerIds[i]>=0)activePlayerCount++;
            _dirty=false;
        }

        private void Show(Transform target,bool visible)
        {if(target!=null && target.gameObject.activeSelf!=visible)target.gameObject.SetActive(visible);}

        private Vector3 ViewerPosition()
        {
            if(Utilities.IsValid(Networking.LocalPlayer))return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            return observer==null?new Vector3(0,1.35f,-2.8f):observer.position;
        }

        private void Place(Transform plate,Vector3 headWorld,Vector3 viewer)
        {
            if(plate==null)return;
            Vector3 target=headWorld+Vector3.up*headGap;
            plate.localPosition=plate.parent==null?target:plate.parent.InverseTransformPoint(target);
            Vector3 away=target-viewer;away.y=0f;
            if(away.sqrMagnitude<.0001f)away=Vector3.forward;
            Quaternion worldRotation=Quaternion.LookRotation(away,Vector3.up);
            plate.localRotation=plate.parent==null?worldRotation:Quaternion.Inverse(plate.parent.rotation)*worldRotation;
        }

        public void Sample(float time)
        {
            EnsurePool();lastSampledTime=time;
            sampledEra=time<79.36f || time>=102.40f?0:time<86.40f?2017:2019;
            Vector3 viewer=ViewerPosition();
            if(sampledEra!=_lastEra)
            {
                int style=sampledEra==2019?1:0;
                if(plateBackgrounds!=null && eraBackgrounds!=null && style<eraBackgrounds.Length)
                    for(int i=0;i<plateBackgrounds.Length;i++)if(plateBackgrounds[i]!=null)plateBackgrounds[i].sharedMaterial=eraBackgrounds[style];
                if(plateBorders!=null && eraBorders!=null && style<eraBorders.Length)
                    for(int i=0;i<plateBorders.Length;i++)if(plateBorders[i]!=null)plateBorders[i].sharedMaterial=eraBorders[style];
                if(sampledEra==0 && playerPlates!=null)for(int i=0;i<playerPlates.Length;i++)Show(playerPlates[i],false);
                _lastEra=sampledEra;_nextPose=0f;
            }
            if(actorPlates!=null)
                for(int i=0;i<actorPlates.Length;i++)
                {
                    if(actorLabels!=null && i<actorLabels.Length && actorLabels[i]!=null && actorLabels[i].text!=anonymousLabel)actorLabels[i].text=anonymousLabel;
                    OWActorRig actor=cast!=null && cast.actors!=null && i<cast.actors.Length?cast.actors[i]:null;
                    if(cast!=null && cast.appearanceChanged && cast.alternateActors!=null && i<cast.alternateActors.Length && cast.alternateActors[i]!=null)actor=cast.alternateActors[i];
                    bool visible=sampledEra!=0 && actor!=null && actor.head!=null;Show(actorPlates[i],visible);
                    if(visible)Place(actorPlates[i],actor.head.position,viewer);
                }
            float now=Time.realtimeSinceStartup;
            if(!previewMode && (_dirty || now>=_nextRoster)){RefreshRoster();_nextRoster=now+rosterInterval;}
            if(sampledEra==0){visiblePlayerCount=0;return;}
            if(now<_nextPose && !previewMode)return;
            _nextPose=now+playerPoseInterval;activePlayerCount=0;visiblePlayerCount=0;
            if(playerPlates==null || playerIds==null)return;
            for(int i=0;i<playerPlates.Length;i++)
            {
                bool valid=false;Vector3 head=Vector3.zero;
                if(previewMode && playerIds[i]<-1){head=previewWorldHeads[i];valid=true;}
                else if(playerIds[i]>=0)
                {
                    VRCPlayerApi player=VRCPlayerApi.GetPlayerById(playerIds[i]);
                    if(Utilities.IsValid(player))
                    {
                        head=player.GetBonePosition(HumanBodyBones.Head);
                        if(head==Vector3.zero)head=player.GetPosition()+Vector3.up*1.4f;
                        valid=true;
                    }
                }
                if(valid)activePlayerCount++;
                bool visible=valid && sampledEra!=0 && (head-viewer).sqrMagnitude<=maxDistance*maxDistance;Show(playerPlates[i],visible);
                if(visible){Place(playerPlates[i],head,viewer);visiblePlayerCount++;}
            }
        }

        public void SetPreviewPlayer(int slot,string displayName,Vector3 headWorld)
        {
            EnsurePool();if(playerIds==null || slot<0 || slot>=playerIds.Length)return;
            previewMode=true;playerIds[slot]=-slot-2;previewWorldHeads[slot]=headWorld;
            if(playerLabels!=null && slot<playerLabels.Length && playerLabels[slot]!=null)playerLabels[slot].text=anonymousLabel;
        }
        public void ClearPreviewPlayer(int slot)
        {
            if(playerIds==null || slot<0 || slot>=playerIds.Length)return;
            playerIds[slot]=-1;Show(playerPlates[slot],false);
        }
        public void EndPreview()
        {
            if(playerIds!=null)for(int i=0;i<playerIds.Length;i++){playerIds[i]=-1;Show(playerPlates[i],false);}
            activePlayerCount=0;visiblePlayerCount=0;previewMode=false;_dirty=true;_nextPose=0f;
        }
    }
}
