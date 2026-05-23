#!/usr/bin/env python3
"""
Extract slide arrow positions / rotations from Unity prefab YAML files.

For every prefab in Assets/SlidePrefab/, find the root GameObject (Transform with m_Father=fileID:0),
walk its child transforms in the original order (skipping the trailing Just_str PrefabInstance),
and emit a C# static dictionary keyed by slide shape name.

Usage:
    python tools/extract_slide_arrows.py            # writes Assets/Scripts/Notes/Pool/SlideArrowTable.cs
    python tools/extract_slide_arrows.py --dry-run  # prints to stdout

The mapping from prefab filename to slide-shape key matches DataLoader.SLIDE_PREFAB_MAP.
"""

from __future__ import annotations
import argparse
import os
import re
import sys
from pathlib import Path
from typing import Dict, List, Tuple, Optional

# ---------------------------------------------------------------------------
# prefab filename -> shape key (matches DataLoader.SLIDE_PREFAB_MAP)
# ---------------------------------------------------------------------------
PREFAB_TO_SHAPE: Dict[str, str] = {
    "Star_Line_3": "line3", "Star_Line_4": "line4", "Star_Line_5": "line5",
    "Star_Line_6": "line6", "Star_Line_7": "line7",
    "Star_Circle_1": "circle1", "Star_Circle_2": "circle2", "Star_Circle_3": "circle3",
    "Star_Circle_4": "circle4", "Star_Circle_5": "circle5", "Star_Circle_6": "circle6",
    "Star_Circle_7": "circle7", "Star_Circle_8": "circle8",
    "Star_V_1": "v1", "Star_V_2": "v2", "Star_V_3": "v3", "Star_V_4": "v4",
    "Star_V_6": "v6", "Star_V_7": "v7", "Star_V_8": "v8",
    "Star_ppqq_1": "ppqq1", "Star_ppqq_2": "ppqq2", "Star_ppqq_3": "ppqq3",
    "Star_ppqq_4": "ppqq4", "Star_ppqq_5": "ppqq5", "Star_ppqq_6": "ppqq6",
    "Star_ppqq_7": "ppqq7", "Star_ppqq_8": "ppqq8",
    "Star_pq_1": "pq1", "Star_pq_2": "pq2", "Star_pq_3": "pq3", "Star_pq_4": "pq4",
    "Star_pq_5": "pq5", "Star_pq_6": "pq6", "Star_pq_7": "pq7", "Star_pq_8": "pq8",
    "Star_S": "s",
    "Slide_Wifi": "wifi",
    "Star_L_2": "L2", "Star_L_3": "L3", "Star_L_4": "L4", "Star_L_5": "L5",
}

# Match enough YAML to extract Transform records without a full parser.
DOC_RE = re.compile(r"^---\s+!u!(\d+)\s+&(\d+)\s*$", re.MULTILINE)
TRANSFORM_TAG = "4"
GAMEOBJECT_TAG = "1"


def parse_block(block: str) -> Dict[str, str]:
    """Flatten a YAML doc into a {key: raw_value_string} dict.
    Captures fields at the standard 2-space indent level used by Unity prefabs."""
    out: Dict[str, str] = {}
    for line in block.splitlines():
        # Unity uses "  m_FieldName: value" at 2-space indent for component fields.
        m = re.match(r"^  (\w[\w]*):\s*(.*)$", line)
        if m:
            out[m.group(1)] = m.group(2).strip()
    return out


def extract_xyz(value: str) -> Tuple[float, float, float]:
    m = re.match(r"\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)", value)
    if not m:
        return (0.0, 0.0, 0.0)
    return tuple(float(m.group(i)) for i in (1, 2, 3))  # type: ignore


def extract_xyzw(value: str) -> Tuple[float, float, float, float]:
    """Extract quaternion {x, y, z, w}."""
    m = re.match(r"\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+),\s*w:\s*(-?[\d.eE+-]+)", value)
    if not m:
        return (0.0, 0.0, 0.0, 1.0)
    return tuple(float(m.group(i)) for i in (1, 2, 3, 4))  # type: ignore


def quaternion_to_euler_z(qx: float, qy: float, qz: float, qw: float) -> float:
    """Convert quaternion to euler Z angle in degrees.

    For a pure Z rotation, the formula simplifies to: angle = 2 * atan2(z, w)
    """
    import math
    angle_rad = 2 * math.atan2(qz, qw)
    return math.degrees(angle_rad)


def extract_father_id(value: str) -> Optional[int]:
    m = re.search(r"fileID:\s*(-?\d+)", value)
    if not m:
        return None
    fid = int(m.group(1))
    return fid if fid != 0 else None


def parse_prefab(path: Path) -> List[Tuple[float, float, float]]:
    """Return [(localX, localY, eulerZ)] for every direct child arrow of the root,
    in the *original child order* (matches transform.GetChild(i) order)."""
    text = path.read_text(encoding="utf-8")

    # Split into (tag, fileID, body) tuples
    docs: List[Tuple[str, str, str]] = []
    matches = list(DOC_RE.finditer(text))
    for i, m in enumerate(matches):
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[start:end]
        docs.append((m.group(1), m.group(2), body))

    transforms: Dict[str, Dict[str, str]] = {}
    gameobjects: Dict[str, Dict[str, str]] = {}
    for tag, fid, body in docs:
        if tag == TRANSFORM_TAG:
            transforms[fid] = parse_block(body)
        elif tag == GAMEOBJECT_TAG:
            gameobjects[fid] = parse_block(body)

    # Locate root transform (m_Father is fileID:0). There may be several "root-ish"
    # transforms; pick the one with the most children — that's the slide root.
    root_id: Optional[str] = None
    best_children = -1
    for fid, t in transforms.items():
        father = extract_father_id(t.get("m_Father", ""))
        if father is None:
            children_block = read_children_block(t)
            if len(children_block) > best_children:
                best_children = len(children_block)
                root_id = fid
    if root_id is None:
        return []

    children_ids = read_children_block(transforms[root_id])

    poses: List[Tuple[float, float, float]] = []
    for child_fid in children_ids:
        t = transforms.get(child_fid)
        if t is None:
            # PrefabInstance child (e.g. Just_str) — skip
            continue
        # Check the GameObject's name to drop Just_str
        go_id_match = re.search(r"fileID:\s*(\d+)", t.get("m_GameObject", ""))
        if go_id_match:
            go = gameobjects.get(go_id_match.group(1))
            if go and "Just_str" in go.get("m_Name", ""):
                continue
        pos = extract_xyz(t.get("m_LocalPosition", "{x: 0, y: 0, z: 0}"))
        rot = extract_xyzw(t.get("m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}"))
        euler_z = quaternion_to_euler_z(*rot)
        poses.append((pos[0], pos[1], euler_z))
    return poses


def read_children_block(transform: Dict[str, str]) -> List[str]:
    """Children of a Transform appear as 'm_Children:' followed by '- {fileID: N}' lines.
    The flat parse_block doesn't capture those, so re-scan from the raw key."""
    raw = transform.get("m_Children", "")
    if not raw or raw == "[]":
        return []
    return []  # unused; see find_children_in_text


def find_children_in_text(text: str, root_anchor_fid: str) -> List[str]:
    """Find the m_Children list of the transform with anchor fid."""
    # locate the &fid line, then read until next ---
    pat = re.compile(rf"^---\s+!u!4\s+&{root_anchor_fid}\s*$", re.MULTILINE)
    m = pat.search(text)
    if not m:
        return []
    start = m.end()
    end_match = re.search(r"^---\s", text[start:], re.MULTILINE)
    end = start + (end_match.start() if end_match else len(text) - start)
    block = text[start:end]
    # find m_Children: line, capture subsequent indented '- {fileID: N}' lines
    children_match = re.search(r"^\s*m_Children:\s*$", block, re.MULTILINE)
    if not children_match:
        # may be inline 'm_Children: []'
        return []
    sub = block[children_match.end():]
    out: List[str] = []
    for line in sub.splitlines():
        if not line.strip():
            continue
        if not line.startswith("  "):
            break
        m2 = re.search(r"fileID:\s*(\d+)", line)
        if m2:
            out.append(m2.group(1))
        else:
            break
    return out


def parse_prefab_full(path: Path) -> List[Tuple[float, float, float]]:
    """Better implementation that uses raw text to read m_Children lists."""
    text = path.read_text(encoding="utf-8")

    matches = list(DOC_RE.finditer(text))
    transforms: Dict[str, Dict[str, str]] = {}
    gameobjects: Dict[str, Dict[str, str]] = {}
    for i, m in enumerate(matches):
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[start:end]
        if m.group(1) == TRANSFORM_TAG:
            transforms[m.group(2)] = parse_block(body)
        elif m.group(1) == GAMEOBJECT_TAG:
            gameobjects[m.group(2)] = parse_block(body)

    # find root transform
    root_id: Optional[str] = None
    best_children: List[str] = []
    for fid, t in transforms.items():
        father = extract_father_id(t.get("m_Father", ""))
        if father is None:
            children = find_children_in_text(text, fid)
            if len(children) > len(best_children):
                best_children = children
                root_id = fid
    if root_id is None:
        return []

    poses: List[Tuple[float, float, float]] = []
    for child_fid in best_children:
        t = transforms.get(child_fid)
        if t is None:
            # PrefabInstance child (Just_str etc) — skip
            continue
        go_id_match = re.search(r"fileID:\s*(\d+)", t.get("m_GameObject", ""))
        if go_id_match:
            go = gameobjects.get(go_id_match.group(1))
            if go and "Just_str" in go.get("m_Name", ""):
                continue
        pos = extract_xyz(t.get("m_LocalPosition", "{x: 0, y: 0, z: 0}"))
        rot = extract_xyzw(t.get("m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}"))
        euler_z = quaternion_to_euler_z(*rot)
        poses.append((pos[0], pos[1], euler_z))
    return poses


# ---------------------------------------------------------------------------
# Code generation
# ---------------------------------------------------------------------------
HEADER = """// <auto-generated>
//  Generated by tools/extract_slide_arrows.py — DO NOT EDIT BY HAND.
//  Re-run the script after modifying any Slide_*/Star_* prefab.
// </auto-generated>
#nullable enable

using System.Collections.Generic;

/// <summary>
/// Slide 箭头位姿(localX/localY/eulerZ)静态表，按 slide shape 检索。
/// 由 tools/extract_slide_arrows.py 从 Assets/SlidePrefab/*.prefab 一次性抽取。
/// 镜像/翻转在运行时计算（见 SlideDrop.Init），不在表里预存翻转副本。
/// </summary>
public static class SlideArrowTable
{
    public readonly struct ArrowPose
    {
        public readonly float X, Y, RotZ;
        public ArrowPose(float x, float y, float rotZ) { X = x; Y = y; RotZ = rotZ; }
    }

    private static readonly Dictionary<string, ArrowPose[]> _map = new()
    {
"""

FOOTER = """    };

    /// <summary>取出指定形状的 arrow 位姿数组。返回 null 表示未知形状。</summary>
    public static ArrowPose[]? Get(string shape) => _map.TryGetValue(shape, out var arr) ? arr : null;

    /// <summary>是否包含此 shape。</summary>
    public static bool Contains(string shape) => _map.ContainsKey(shape);
}
"""


def emit_csharp(shapes: Dict[str, List[Tuple[float, float, float]]]) -> str:
    out = [HEADER]
    for shape in sorted(shapes.keys()):
        poses = shapes[shape]
        out.append(f'        ["{shape}"] = new ArrowPose[]\n        {{\n')
        for x, y, rot in poses:
            out.append(f"            new ArrowPose({x:.6f}f, {y:.6f}f, {rot:.6f}f),\n")
        out.append("        },\n")
    out.append(FOOTER)
    return "".join(out)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--prefab-dir", default="Assets/SlidePrefab")
    ap.add_argument("--output", default="Assets/Scripts/Notes/Pool/SlideArrowTable.cs")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    prefab_dir = Path(args.prefab_dir)
    if not prefab_dir.is_dir():
        print(f"prefab dir not found: {prefab_dir}", file=sys.stderr)
        return 1

    shapes: Dict[str, List[Tuple[float, float, float]]] = {}
    for prefab in sorted(prefab_dir.glob("*.prefab")):
        stem = prefab.stem
        shape = PREFAB_TO_SHAPE.get(stem)
        if shape is None:
            print(f"  [skip] {stem} (no shape mapping)", file=sys.stderr)
            continue
        poses = parse_prefab_full(prefab)
        shapes[shape] = poses
        print(f"  [ok]   {stem} -> {shape} ({len(poses)} arrows)", file=sys.stderr)

    code = emit_csharp(shapes)
    if args.dry_run:
        sys.stdout.write(code)
    else:
        out_path = Path(args.output)
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(code, encoding="utf-8")
        print(f"wrote {out_path} ({len(shapes)} shapes)", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
