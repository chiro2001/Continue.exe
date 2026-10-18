#!/usr/bin/env python3
"""Build performed text and restrained action sounds for the complete performance."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent

def main():
    beats=json.loads((ROOT/'handoff/full-film-beats.json').read_text())
    keys=[
        {'start':136.0,'end':140.48,'text':'副本越来越多，留下的名字越来越少。','style':0},
        {'start':189.6,'end':194.0,'text':'文件还在，打开它的路不见了。','style':0},
        {'start':202.0,'end':206.4,'text':'原来，我也能做出来。','style':1},
        {'start':250.88,'end':253.44,'text':'那就一起做下去。','style':1},
    ]
    comments=[]
    def series(start,end,step,phrases,duration):
        i=0
        while start+i*step<end-1e-4:
            comments.append({'time':round(start+i*step,2),'duration':duration,'text':phrases[i%len(phrases)]});i+=1
    series(133.28,153.6,.64,['已转','求资源','链接在评论区','搬运自网络','原地址失效','已阅','这个版本呢？','来源：网络'],4.48)
    series(154.24,167.68,1.28,['加载中……','有没有人试过？','同一个问题','还在排队','这个链接也失效了'],4.0)
    series(207.36,215.04,1.28,['这个思路好','第三层顺序反了','我试一下','好了，这次对了','源文件收到了','谢谢，已经复现'],4.0)
    series(224.0,235.52,1.92,['我帮你校一下','这个角度好看','新场景可以进了','把做法也贴出来','字幕对上了','我也想试试'],4.0)
    series(236.16,245.12,1.92,['沙发！','学会了！','感谢分享','我也做了一个','下次一起'],3.6)
    foley=json.loads((ROOT/'data/episode123-v5-foley.json').read_text())
    names={c['id']:i for i,c in enumerate(foley['clips'])}
    extra=[]
    def event(t,clip,emitter=0):
        assert abs(t/.08-round(t/.08))<1e-4
        extra.append({'time':round(t,2),'clip':names[clip],'emitter':emitter})
    for i in range(18):event(133.12+i*.80,'mouse_click_old' if i%3 else 'notification')
    for i in range(6):event(155.20+i*.32,'keyboard_laptop_'+['a','b','c'][i%3],8)
    event(158.08,'verify_ok',8)
    for t in [169.28,173.76,178.24,182.72]:event(t,'verify_wait')
    for base in [198.40,201.28,209.92,224.0,228.48]:
        for i in range(3):event(base+i*.24,'keyboard_laptop_'+['a','b','c'][i],8+(int(base)%3))
    event(203.52,'verify_ok',8);event(207.68,'msg_pop',8);event(211.20,'mouse_click_old',8)
    event(225.28,'subtitle_snap',9);event(229.12,'subtitle_snap',9);event(234.24,'bt_complete',10)
    event(238.08,'mouse_click_old',11);event(239.36,'bt_complete',11);event(243.20,'forum_post',11)
    all_events=sorted(foley['events']+extra,key=lambda x:x['time'])
    doc={'schema':'oldwebmap.fullfilm.presentation/1','duration':256,'scene':'Assets/Scenes/OWM_FullFilm_v1.unity',
         'spec':'spec/49-fullfilm-production.md','music_note':'四轨原音符和持续声保留；空行不代表静音。',
         'scene_starts':[s['start'] for s in beats['scenes']],
         'scenes':[{'id':s['id'],'start':s['start'],'end':s['end'],'title':s['title'],'segment':s['segment']} for s in beats['scenes']],
         'keylines':keys,'comments':comments,'foley_events':all_events,'new_foley_events':len(extra),
         'new_emitters':[[0,.85,4.8],[-5,.95,6.0],[5,.95,6.0],[1.8,.95,3.6]],
         'travel_freeze':[[79.36,81.92],[86.40,88.32],[102.40,104.96],[217.60,222.72]],
         'reviewer_boundaries':{'hmd_tested':False,'real_multiplayer_tested':False,'human_listening':False},
         'playback_output':'Windows default outputs muted; source audio and exported film retain music'}
    path=ROOT/'data/fullfilm-presentation.json'
    if path.exists():
        previous=json.loads(path.read_text())
        if 'history_anchors' in previous:doc['history_anchors']=previous['history_anchors']
    path.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
    assert len({s['id'] for s in doc['scenes']})==24
    assert all(c['time']<186.88 or c['time']>=207.36 for c in comments)
    assert all(not 217.60<=e['time']<222.72 for e in extra)
    print(path,'keys',len(keys),'comments',len(comments),'foley',len(all_events))

if __name__=='__main__':main()
