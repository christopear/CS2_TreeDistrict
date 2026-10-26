"""Read CS2 stock geometry bounds; never extract meshes or modify game files.

Usage: py -3 scripts/measure-vegetation.py --content PATH --game-version VERSION
Geometry format v1: 10-byte prefix, 88-byte mesh headers, 40-byte submesh
records. Bounds are Unity centre/extent float3 pairs after four int32 fields.
Only numeric measurements and identifiers are written to the repository.
"""
import argparse
import io
import json
import math
from pathlib import Path
import re
import struct
import zipfile

STAGES = {"child": "young", "teen": "medium", "adult": "mature",
          "elderly": "old", "dead": "dead", "stump": "stump"}


def measure(data):
    if len(data) < 10:
        raise ValueError("Truncated geometry prefix")
    version, count, flags = struct.unpack_from("<HII", data)
    if version != 1 or not 1 <= count <= 1000:
        raise ValueError(f"Unsupported geometry header: {version}, {count}")
    offset = 10 + count * 88
    if len(data) < offset:
        raise ValueError("Truncated mesh headers")
    minimum = [math.inf] * 3
    maximum = [-math.inf] * 3
    for mesh in range(count):
        sub_count = struct.unpack_from("<i", data, 10 + mesh * 88 + 84)[0]
        if not 1 <= sub_count <= 1000:
            raise ValueError("Invalid submesh count")
        for _ in range(sub_count):
            if offset + 40 > len(data):
                raise ValueError("Truncated submesh bounds")
            values = struct.unpack_from("<6f", data, offset + 16)
            if not all(math.isfinite(v) for v in values) or any(v < 0 for v in values[3:]):
                raise ValueError("Invalid bounds")
            for axis in range(3):
                minimum[axis] = min(minimum[axis], values[axis] - values[axis + 3])
                maximum[axis] = max(maximum[axis], values[axis] + values[axis + 3])
            offset += 40
    size = [maximum[i] - minimum[i] for i in range(3)]
    return {"heightM": round(size[1], 4), "widthM": round(size[0], 4),
            "depthM": round(size[2], 4),
            "boundsMinM": [round(v, 4) for v in minimum],
            "boundsMaxM": [round(v, 4) for v in maximum]}


def read_string(stream):
    length = 0
    for shift in range(0, 35, 7):
        raw = stream.read(1)
        if not raw:
            raise ValueError("Truncated locale string")
        byte = raw[0]
        length |= (byte & 127) << shift
        if not byte & 128:
            raw = stream.read(length)
            if len(raw) != length:
                raise ValueError("Truncated locale text")
            return raw.decode("utf-8")
    raise ValueError("Invalid locale length")


def asset_names(content):
    names = {}
    for path in sorted(content.rglob("Locale.cok")):
        with zipfile.ZipFile(path) as archive:
            if "en-US.loc" not in archive.namelist():
                continue
            stream = io.BytesIO(archive.read("en-US.loc"))
            if stream.read(2) != b"\x01\x00":
                raise ValueError("Unsupported locale version")
            for _ in range(3):
                read_string(stream)
            count, = struct.unpack("<I", stream.read(4))
            for _ in range(count):
                key, value = read_string(stream), read_string(stream)
                match = re.fullmatch(r"Assets.NAME\[(.+)\]", key)
                if match:
                    names[match[1].casefold()] = (match[1], value.strip())
    return names


def collect(content, game_version):
    names = asset_names(content)
    records, scanned, rejected = {}, [], []
    for path in sorted(content.rglob("Blob*.cok")):
        pack = path.relative_to(content).as_posix()
        scanned.append(pack)
        with zipfile.ZipFile(path) as archive:
            for entry in sorted(archive.infolist(), key=lambda e: e.filename):
                match = re.fullmatch(r"(.+)_([a-f0-9]+)\.Geometry", entry.filename)
                if not match:
                    continue
                mesh = match[1]
                if re.search(r"_LOD\d+", mesh, re.I):
                    continue
                # Token matching excludes StreetSign, Streetlight, TreePlanter, etc.
                if not re.search(r"(?:Tree|Bush|Shrub)(?:Child|Teen|Adult|Elderly|Dead|Stump|City|Wild|\d)", mesh, re.I):
                    continue
                stem = re.sub(r"_Geom$", "", mesh, flags=re.I)
                stage_match = re.search(r"(Child|Teen|Adult|Elderly|Dead|Stump)(?=\d+$)", stem, re.I)
                stage = STAGES[stage_match[1].lower()] if stage_match else "static"
                family = re.sub(r"(Child|Teen|Adult|Elderly|Dead|Stump)(?=\d+$)", "", stem, flags=re.I)
                prefab, display = names.get(family.casefold(), (family, None))
                key = prefab.casefold()
                record = records.setdefault(key, {
                    "id": "cs2:" + prefab, "name": display or prefab,
                    "prefabNameCandidate": prefab,
                    "nameConfirmedInLocale": display is not None,
                    "runtimeMeshLinkVerified": False,
                    "kind": "tree" if "tree" in family.lower() else "shrub",
                    "botanicalProfileRef": "london-plane.json" if prefab == "NA_LondonPlaneTree01" else None,
                    "botanicalResearchStatus": "partial" if prefab == "NA_LondonPlaneTree01" else "notResearched",
                    "placementDefaults": None,
                    "baseGameMeasurements": []})
                try:
                    if entry.file_size > 32 * 1024 * 1024:
                        raise ValueError("Geometry exceeds safety limit")
                    bounds = measure(archive.read(entry))
                except (ValueError, struct.error) as error:
                    rejected.append({"archive": pack, "entry": entry.filename, "reason": str(error)})
                    continue
                record["baseGameMeasurements"].append({
                    "stage": stage, "meshName": mesh, "geometryId": match[2],
                    "archive": pack, **bounds})
    named_vegetation = {key for key in names if re.search(r"(?:tree|bush(?:city|wild)?|shrub)\d+$", key)}
    missing = sorted(names[key][0] for key in named_vegetation - records.keys())
    return {"schemaVersion": 1, "gameVersion": game_version,
            "measurementMethod": "Union of highest-detail geometry submesh axis-aligned bounds, in model-space metres; includes trunk and foliage.",
            "limitations": [
                "Stock installed content only; ownership or availability of a DLC is not inferred from installed files.",
                "Groups use mesh naming conventions and English asset labels; active prefab-to-mesh references are not verified.",
                "All matching mesh variants are retained, including older and environment-pack versions. No active variant is guessed.",
                "Bounds exclude runtime scaling, rotation, wind and seasonal changes. Width/depth are not measured foliage cover or opacity.",
                "Placement, shade and botanical defaults are not derived automatically from bounding boxes."],
            "archivesScanned": scanned,
            "coverage": {"namedVegetationAssets": len(named_vegetation),
                         "namedAssetsWithoutMeasurements": missing,
                         "unconfirmedMeshGroups": sorted(r["id"] for r in records.values() if not r["nameConfirmedInLocale"])},
            "rejectedMeasurements": rejected,
            "assets": sorted(records.values(), key=lambda r: r["id"])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--content", type=Path, required=True)
    parser.add_argument("--game-version", required=True)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] / "data/base-game-vegetation.v1.json")
    args = parser.parse_args()
    if not args.content.is_dir():
        parser.error("Content directory does not exist")
    result = collect(args.content, args.game_version)
    if not result["assets"] or result["rejectedMeasurements"]:
        raise ValueError(f"Incomplete extraction: {result['rejectedMeasurements']}")
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Measured {len(result['assets'])} asset groups / {sum(len(r['baseGameMeasurements']) for r in result['assets'])} meshes across {len(result['archivesScanned'])} archives")


if __name__ == "__main__":
    main()
