#!/usr/bin/env python3
"""V5 performed text: eight keylines and non-repeating period danmaku snapped to real FC14 onsets."""
import csv, json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "data/episode123-v5-text.json"

# Keylines: (start, end, text). Region follows the shared journey; styles alternate.
KEYLINES = [
    (1.60, 5.90, "这些界面，你认得吗？"),
    (23.20, 27.60, "我们先把它，做出来。"),
    (33.20, 37.70, "有人把它译成了，我们读得懂的样子。"),
    (46.30, 50.50, "下载完成以后，仍有人留着灯。"),
    (66.00, 70.30, "一条字幕，许多双手"),
    (89.50, 94.10, "原来，我们能在这里见面"),
    (105.20, 109.40, "联系人还在，选择却变了"),
    (128.50, 132.70, "把可用的方法，留给后来的人"),
]

# Danmaku by period. Forum replies before video danmaku; Hub chatter; sparse conditions.
COMMENTS = [
    # 2007–2010 forums, posts, early social networks
    (53.20, "沙发！"), (53.90, "板凳"), (54.60, "前排围观"), (55.40, "楼主好人一生平安"),
    (56.20, "mark 一下"), (57.00, "顶上去"), (57.60, "这个页面我存过"), (58.30, "打酱油路过"),
    (58.90, "求种，谢楼主"), (59.50, "楼主还在吗"), (60.10, "贾君鹏，你妈妈喊你回家吃饭"),
    (60.70, "神马都是浮云"), (61.10, "给力！"),
    # 2011–2014 subtitles and video danmaku
    (61.70, "空降成功"), (62.30, "字幕组辛苦了"), (62.90, "熟肉来了"), (63.50, "这句我会背"),
    (64.10, "23333"), (64.70, "时间轴好准"), (65.30, "谢谢翻译"), (65.90, "<color=#7FE7FF>弹幕护体</color>"),
    (67.00, "生肉也看懂了"), (67.60, "这里对上了"), (68.20, "求压制版"), (68.90, "校对辛苦"),
    (69.60, "第一次见这么准的轴"), (70.40, "收藏了"), (71.00, "字幕君威武"),
    # 2014–2017 tutorials, modelling, first worlds
    (72.00, "求教程"), (72.60, "原来还可以这样"), (73.30, "UP 主更新了"), (73.90, "我也想试试"),
    (74.50, "骨骼又歪了……"), (75.20, "贴图终于对上了"), (75.90, "收藏 = 学会"), (76.60, "建模好难"),
    (77.30, "学会了！"), (78.00, "下一期呢"), (80.64, "<color=#FFD24A>前方高能预警</color>"),
    # 2017/2019 Hubs: being there together
    (82.24, "听得见吗？"), (82.90, "你也来了"), (83.60, "麦有点炸"), (84.30, "新来的？"),
    (85.00, "跟我来"), (85.70, "我带你过去"), (88.60, "这边坐"), (89.30, "晚上好"),
    (90.00, "你的模型好可爱"), (90.80, "原来你在这里"), (91.50, "一起拍张照"), (92.30, "镜子前集合"),
    (93.10, "手势怎么做？"), (93.90, "像这样——"), (94.70, "学会了"), (95.50, "换个地方聊"),
    (96.40, "我卡住了，拉我一下"), (97.30, "重进一下试试"), (98.20, "好了！"), (99.10, "谢谢你"),
    (100.00, "下次见"), (101.00, "晚安"),
    # Conditions: fewer, slower
    (105.90, "要登录才能看？"), (107.20, "<color=#FFB0A0>艰难的决定……</color>"), (108.60, "二选一？"),
    (110.00, "换个入口试试"), (112.80, "Mod 不能用了"), (114.20, "辅助功能呢？"),
    (115.70, "有人找到办法了吗"), (118.00, "先导出一份"), (120.30, "我的收藏夹打不开了"),
    (123.40, "我这里还有备份"), (125.20, "这个方法能用"), (127.00, "谢谢，还在维护"),
    (129.60, "收到了"), (131.20, "我也留一份"),
]

JOURNEYS = ((79.36, 81.92), (86.40, 88.32), (102.40, 104.96))


def region(t):
    return 0 if t < 79.36 else 1 if t < 86.40 else 2 if t < 102.40 else 3


def onsets():
    rows = []
    with open(ROOT / "audio/keil-keygen-2008/tracks.csv") as f:
        for r in csv.DictReader(f):
            t = float(r["time_s"])
            if t <= 133.12:
                rows.append((t, int(r["channel"]) - 1))
    return sorted(rows)


def visible_length(text):
    out, skip = 0, False
    for ch in text:
        if ch == "<":
            skip = True
        elif ch == ">":
            skip = False
        elif not skip:
            out += 1
    return out


def main():
    notes = onsets()
    comments, used = [], set()
    for want, text in COMMENTS:
        candidates = [(abs(t - want), t, ch) for t, ch in notes if abs(t - want) <= 0.24 and t not in used]
        if not candidates:
            raise SystemExit(f"No FC14 onset within 0.24 s of {want} for {text}")
        _, t, ch = min(candidates)
        used.add(t)
        for a, b in JOURNEYS:
            if a - 0.3 < t < b and text != "<color=#FFD24A>前方高能预警</color>":
                raise SystemExit(f"Comment inside journey {a}-{b}: {text}")
        n = visible_length(text)
        comments.append({"time": round(t, 4), "text": text, "region": region(t), "channel": ch,
                         "duration": 4.6 if n <= 8 else 5.4})
    times = [c["time"] for c in comments]
    assert times == sorted(times), "comments must be sorted"
    assert len({c["text"] for c in comments}) == len(comments), "comment texts must not repeat"
    keylines = []
    for i, (a, b, text) in enumerate(KEYLINES):
        assert visible_length(text) <= 18, text
        keylines.append({"start": a, "end": b, "text": text, "region": region(a), "style": i % 2})
    data = {"schema": "episode123-v5-text/1", "keylines": keylines, "comments": comments,
            "notes": "Keylines 15→8; danmaku rewritten by period, unique texts, times snapped to actual FC14 onsets."}
    OUT.write_text(json.dumps(data, ensure_ascii=False, indent=1))
    print(f"{len(keylines)} keylines, {len(comments)} comments → {OUT}")


if __name__ == "__main__":
    main()
