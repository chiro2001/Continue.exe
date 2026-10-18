#!/usr/bin/env python3
"""校验 data/ 下的创意规格是否自洽。

检查项：
- JSON 全部可解析
- 时间轴分段连续，总时长等于 duration_seconds，事件落在所属分段区间内
- 事件 id 能在 stage.json 的节点/道具、quotes.json 或 lyrics.json 的 greetz 开关中找到
- 歌词编号唯一、所属分段存在且事件顺序与时间轴一致
- 引文引用存在，引用状态字段完整
- stage.json 分区指向存在的分段；节点 label 指向存在的标签；连线的对端存在
- 相机路径时间单调、所属分段存在
- budget 与实际统计一致

用法：python3 tools/validate_spec.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "data"
STATES = {"online", "throttled", "dead", "archived", "exported", "revived"}


def load(name: str):
    path = DATA / name
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    def check(condition: bool, message: str) -> None:
        if not condition:
            errors.append(message)

    timeline = load("timeline.json")
    lyrics = load("lyrics.json")
    quotes = load("quotes.json")
    stage = load("stage.json")
    labels = load("labels.json")

    # ---------- 时间轴 ----------
    segments = timeline["segments"]
    cursor = 0
    segment_by_id = {}
    for segment in segments:
        check(segment["start"] == cursor, f"分段 {segment['id']} 起点 {segment['start']} 与上一段终点 {cursor} 不连续")
        check(segment["end"] > segment["start"], f"分段 {segment['id']} 时长非正")
        cursor = segment["end"]
        segment_by_id[segment["id"]] = segment
        for event in segment["events"]:
            check(
                segment["start"] <= event["t"] <= segment["end"],
                f"事件 {event['id']} 的时间 {event['t']} 不在分段 {segment['id']} 区间内",
            )
            check(event["beat"] > 0, f"事件 {event['id']} 的 beat 非法")
    check(cursor == timeline["duration_seconds"], f"分段总时长 {cursor} != duration_seconds {timeline['duration_seconds']}")

    # ---------- 歌词 ----------
    lyric_lines = lyrics["lines"]
    indices = [line["index"] for line in lyric_lines]
    check(len(indices) == len(set(indices)), "歌词编号有重复")
    check(indices == list(range(1, len(indices) + 1)), "歌词编号不是从 1 开始的连续整数")
    lyric_by_index = {line["index"]: line for line in lyric_lines}
    for line in lyric_lines:
        check(line["segment"] in segment_by_id, f"歌词 {line['index']} 所属分段 {line['segment']} 不存在")
        check(bool(line["text"].strip()), f"歌词 {line['index']} 文本为空")
    for segment in segments:
        for index in segment["lyrics"]:
            check(index in lyric_by_index, f"分段 {segment['id']} 引用了不存在的歌词 {index}")
            if index in lyric_by_index:
                check(
                    lyric_by_index[index]["segment"] == segment["id"],
                    f"歌词 {index} 的所属分段与时间轴不一致",
                )

    greetz_ids = {switch.split("@")[0] for switch in lyrics["greetz"]["switches"]}
    check(bool(lyrics["greetz"]["entries"]), "greetz 名单为空")

    # ---------- 引文 ----------
    quote_ids = set()
    for quote in quotes["quotes"]:
        check(quote["id"] not in quote_ids, f"引文 id 重复：{quote['id']}")
        quote_ids.add(quote["id"])
        for field in ("date", "text", "source", "status"):
            check(bool(str(quote.get(field, "")).strip()), f"引文 {quote['id']} 缺少字段 {field}")

    # ---------- 空间 ----------
    zone_ids = set()
    for zone in stage["zones"]:
        check(zone["id"] not in zone_ids, f"分区 id 重复：{zone['id']}")
        zone_ids.add(zone["id"])
        check(zone["segment"] in segment_by_id, f"分区 {zone['id']} 指向不存在的分段 {zone['segment']}")
        check(zone["z_end"] > zone["z_start"], f"分区 {zone['id']} 的 z 区间非法")

    node_ids = set()
    for node in stage["nodes"]:
        check(node["id"] not in node_ids, f"节点 id 重复：{node['id']}")
        node_ids.add(node["id"])
        check(node["zone"] in zone_ids, f"节点 {node['id']} 的分区 {node['zone']} 不存在")
        check(node["label_id"] in labels["node_labels"], f"节点 {node['id']} 的标签 {node['label_id']} 不存在")
        check(node["state"] in STATES, f"节点 {node['id']} 的初始状态 {node['state']} 非法")
        for step in node.get("state_sequence", []):
            check(step["state"] in STATES, f"节点 {node['id']} 的状态序列含非法状态 {step['state']}")
            check(
                0 < step["t"] <= timeline["duration_seconds"],
                f"节点 {node['id']} 的状态切换时间 {step['t']} 越界",
            )
        times = [step["t"] for step in node.get("state_sequence", [])]
        check(times == sorted(times), f"节点 {node['id']} 的状态序列时间不是递增的")

    for node in stage["nodes"]:
        for link in node["links"]:
            check(link in node_ids, f"节点 {node['id']} 连接了不存在的节点 {link}")
            check(link != node["id"], f"节点 {node['id']} 连接了自己")

    prop_ids = set()
    for prop in stage["props"]:
        check(prop["id"] not in prop_ids, f"道具 id 重复：{prop['id']}")
        prop_ids.add(prop["id"])
        check(prop["zone"] in zone_ids, f"道具 {prop['id']} 的分区 {prop['zone']} 不存在")

    camera_segments = {key["segment"] for key in stage["camera_path"]}
    for key in stage["camera_path"]:
        check(key["segment"] in segment_by_id, f"相机关键帧在 {key['t']}s 指向不存在的分段 {key['segment']}")
    camera_times = [key["t"] for key in stage["camera_path"]]
    check(camera_times == sorted(camera_times), "相机路径时间不是递增的")
    check(camera_times[0] == 0, "相机路径没有从 0s 开始")
    missing_segments = set(segment_by_id) - camera_segments
    if missing_segments:
        warnings.append(f"这些分段没有相机关键帧：{sorted(missing_segments)}")

    # ---------- 事件 id 可解析 ----------
    resolvable = node_ids | prop_ids | quote_ids | greetz_ids
    for segment in segments:
        for event in segment["events"]:
            if event["id"] not in resolvable:
                errors.append(f"事件 {event['id']}（分段 {segment['id']}）找不到对应的节点/道具/引文/greetz")

    # ---------- 预算核对 ----------
    edges = set()
    for node in stage["nodes"]:
        for link in node["links"]:
            edges.add(tuple(sorted((node["id"], link))))
    budget = stage["budget"]
    check(budget["node_count"] == len(stage["nodes"]), f"budget.node_count {budget['node_count']} != 实际 {len(stage['nodes'])}")
    check(budget["link_count"] == len(edges), f"budget.link_count {budget['link_count']} != 实际 {len(edges)}")
    popup_actual = sum(
        prop["params"].get("instances", 0) for prop in stage["props"] if prop["kind"] == "popup_wall"
    )
    check(budget["popup_instances"] == popup_actual, f"budget.popup_instances {budget['popup_instances']} != 实际 {popup_actual}")
    particle_actual = sum(
        prop["params"].get("particles", 0) for prop in stage["props"] if prop["kind"] == "particle_flood"
    )
    check(budget["particle_count"] == particle_actual, f"budget.particle_count {budget['particle_count']} != 实际 {particle_actual}")
    tile_actual = sum(
        prop["params"].get("tiles", 0) for prop in stage["props"] if prop["kind"] == "case_wall"
    )
    check(budget["case_tiles"] == tile_actual, f"budget.case_tiles {budget['case_tiles']} != 实际 {tile_actual}")

    # ---------- 统计输出 ----------
    lyric_total = len(lyric_lines)
    print(f"分段 {len(segments)} 个，总时长 {cursor} 秒")
    print(f"节点 {len(stage['nodes'])} 个，连线 {len(edges)} 条，道具 {len(stage['props'])} 个")
    print(f"歌词 {lyric_total} 句，引文 {len(quote_ids)} 条，greetz {len(lyrics['greetz']['entries'])} 条")
    print(f"相机关键帧 {len(stage['camera_path'])} 个，覆盖分段 {len(camera_segments)}/{len(segment_by_id)}")
    for warning in warnings:
        print(f"[warn] {warning}")
    for error in errors:
        print(f"[error] {error}")
    print("OK" if not errors else f"FAILED：{len(errors)} 个问题")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
