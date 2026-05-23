#!/usr/bin/env python3
"""
Extract slideOK (Just_str/Just_curv) positions from Unity prefab YAML files.

For every prefab in Assets/SlidePrefab/, find the Just_str PrefabInstance or Just_curv GameObject,
extract its m_LocalPosition and m_LocalEulerAnglesHint.

Usage:
    python tools/extract_slideok.py            # writes Assets/Scripts/Notes/Pool/SlideOKTable.cs
    python tools/extract_slideok.py --dry-run  # prints to stdout

The mapping from prefab filename to slide-shape key matches DataLoader.SLIDE_PREFAB_MAP.
"""

from __future__ import annotations
import argparse
import re
import sys
from pathlib import Path
from typing import Dict, Tuple, Optional

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

DOC_RE = re.compile(r"^---\s+!u!(\d+)\s+&(\d+)\s*$", re.MULTILINE)
TRANSFORM_TAG = "4"
GAMEOBJECT_TAG = "1"
PREFAB_INSTANCE_TAG = "1001"


def parse_block(block: str) -> Dict[str, str]:
    """Flatten a YAML doc into a {key: raw_value_string} dict."""
    out: Dict[str, str] = {}
    for line in block.splitlines():
        m = re.match(r"^  (\w[\w]*):\s*(.*)$", line)
        if m:
            out[m.group(1)] = m.group(2).strip()
    return out


def extract_xyz(value: str) -> Tuple[float, float, float]:
    m = re.match(r"\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+),\s*z:\s*(-?[\d.eE+-]+)", value)
    if not m:
        return (0.0, 0.0, 0.0)
    return tuple(float(m.group(i)) for i in (1, 2, 3))  # type: ignore


def parse_prefab_slideok(path: Path) -> Optional[Tuple[float, float, float]]:
    """Extract (localX, localY, eulerZ) for the Just_str/Just_curv object."""
    text = path.read_text(encoding="utf-8")

    # Try PrefabInstance method first (Just_str as nested prefab)
    result = parse_prefab_instance_just(text)
    if result:
        return result

    # Try direct GameObject method (Just_curv as direct child)
    result = parse_gameobject_just(text)
    if result:
        return result

    return None


def parse_prefab_instance_just(text: str) -> Optional[Tuple[float, float, float]]:
    """Extract position from PrefabInstance with name Just_str."""
    # Find the PrefabInstance block that contains "value: Just_str"
    just_str_match = re.search(r'propertyPath:\s*m_Name\s*\n\s*value:\s*Just_str', text)
    if not just_str_match:
        return None

    # Find the start of this PrefabInstance block (search backwards for "--- !u!1001")
    prefab_instance_start = text.rfind('--- !u!1001', 0, just_str_match.start())
    if prefab_instance_start == -1:
        return None

    # Find the end of this PrefabInstance block
    next_block = text.find('--- !u!', prefab_instance_start + 10)
    if next_block == -1:
        next_block = len(text)

    block = text[prefab_instance_start:next_block]

    # Extract position and rotation from m_Modifications
    pos_x = pos_y = 0.0
    euler_z = 0.0

    # Pattern for m_LocalPosition.x/y
    for axis in ['x', 'y']:
        match = re.search(
            rf'propertyPath:\s*m_LocalPosition\.{axis}\s*\n\s*value:\s*(-?[\d.eE+-]+)',
            block
        )
        if match:
            if axis == 'x':
                pos_x = float(match.group(1))
            else:
                pos_y = float(match.group(1))

    # Pattern for m_LocalEulerAnglesHint.z
    euler_match = re.search(
        r'propertyPath:\s*m_LocalEulerAnglesHint\.z\s*\n\s*value:\s*(-?[\d.eE+-]+)',
        block
    )
    if euler_match:
        euler_z = float(euler_match.group(1))

    return (pos_x, pos_y, euler_z)


def parse_gameobject_just(text: str) -> Optional[Tuple[float, float, float]]:
    """Extract position from direct GameObject named Just_curv."""
    # Split into (tag, fileID, body) tuples
    matches = list(DOC_RE.finditer(text))

    transforms: Dict[str, Dict[str, str]] = {}
    gameobjects: Dict[str, Dict[str, str]] = {}
    transform_to_go: Dict[str, str] = {}  # transform fileID -> gameobject fileID

    for i, m in enumerate(matches):
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[start:end]
        if m.group(1) == TRANSFORM_TAG:
            parsed = parse_block(body)
            transforms[m.group(2)] = parsed
            # Extract the GameObject reference
            go_match = re.search(r"fileID:\s*(\d+)", parsed.get("m_GameObject", ""))
            if go_match:
                transform_to_go[m.group(2)] = go_match.group(1)
        elif m.group(1) == GAMEOBJECT_TAG:
            gameobjects[m.group(2)] = parse_block(body)

    # Find Just_curv/Just_wifi GameObject
    just_go_id = None
    for go_id, go in gameobjects.items():
        name = go.get("m_Name", "")
        if "Just_curv" in name or "Just_str" in name or "Just_wifi" in name:
            just_go_id = go_id
            break

    if just_go_id is None:
        return None

    # Find the Transform that belongs to this GameObject
    just_transform_id = None
    for t_id, go_id in transform_to_go.items():
        if go_id == just_go_id:
            just_transform_id = t_id
            break

    if just_transform_id is None:
        return None

    t = transforms[just_transform_id]
    pos = extract_xyz(t.get("m_LocalPosition", "{x: 0, y: 0, z: 0}"))
    euler = extract_xyz(t.get("m_LocalEulerAnglesHint", "{x: 0, y: 0, z: 0}"))
    return (pos[0], pos[1], euler[2])


# ---------------------------------------------------------------------------
# Code generation
# ---------------------------------------------------------------------------
HEADER = """// <auto-generated>
//  Generated by tools/extract_slideok.py — DO NOT EDIT BY HAND.
//  Re-run the script after modifying any Slide_*/Star_* prefab.
// </auto-generated>
#nullable enable

using System.Collections.Generic;

/// <summary>
/// SlideOK (Just_str/Just_curv) 位姿(localX/localY/eulerZ)静态表，按 slide shape 检索。
/// 由 tools/extract_slideok.py 从 Assets/SlidePrefab/*.prefab 一次性抽取。
/// 镜像/翻转在运行时计算（见 SlideDrop.Init），不在表里预存翻转副本。
/// </summary>
public static class SlideOKTable
{
    public readonly struct OKPose
    {
        public readonly float X, Y, RotZ;
        public OKPose(float x, float y, float rotZ) { X = x; Y = y; RotZ = rotZ; }
    }

    private static readonly Dictionary<string, OKPose> _map = new()
    {
"""

FOOTER = """    };

    /// <summary>取出指定形状的 slideOK 位姿。返回 null 表示未知形状。</summary>
    public static OKPose? Get(string shape) => _map.TryGetValue(shape, out var pose) ? pose : null;

    /// <summary>是否包含此 shape。</summary>
    public static bool Contains(string shape) => _map.ContainsKey(shape);
}
"""


def emit_csharp(shapes: Dict[str, Tuple[float, float, float]]) -> str:
    out = [HEADER]
    for shape in sorted(shapes.keys()):
        x, y, rot = shapes[shape]
        out.append(f'        ["{shape}"] = new OKPose({x:.6f}f, {y:.6f}f, {rot:.6f}f),\n')
    out.append(FOOTER)
    return "".join(out)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--prefab-dir", default="Assets/SlidePrefab")
    ap.add_argument("--output", default="Assets/Scripts/Notes/Pool/SlideOKTable.cs")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    prefab_dir = Path(args.prefab_dir)
    if not prefab_dir.is_dir():
        print(f"prefab dir not found: {prefab_dir}", file=sys.stderr)
        return 1

    shapes: Dict[str, Tuple[float, float, float]] = {}
    for prefab in sorted(prefab_dir.glob("*.prefab")):
        stem = prefab.stem
        shape = PREFAB_TO_SHAPE.get(stem)
        if shape is None:
            print(f"  [skip] {stem} (no shape mapping)", file=sys.stderr)
            continue
        pose = parse_prefab_slideok(prefab)
        if pose is None:
            print(f"  [warn] {stem} -> {shape} (no Just found)", file=sys.stderr)
            continue
        shapes[shape] = pose
        print(f"  [ok]   {stem} -> {shape} (pos=({pose[0]:.2f}, {pose[1]:.2f}), rot={pose[2]:.2f})", file=sys.stderr)

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
