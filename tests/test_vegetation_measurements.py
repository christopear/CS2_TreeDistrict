"""Synthetic binary format tests plus checks on the committed stock inventory."""
import importlib.util
import json
from pathlib import Path
import struct
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("measure", ROOT / "scripts/measure-vegetation.py")
measure = importlib.util.module_from_spec(spec)
spec.loader.exec_module(measure)


def geometry(bounds):
    header = bytearray(88)
    struct.pack_into("<i", header, 84, len(bounds))
    return struct.pack("<HII", 1, 1, 0) + header + b"".join(
        bytes(16) + struct.pack("<6f", *values) for values in bounds)


class Measurements(unittest.TestCase):
    def test_union_includes_offset_submeshes(self):
        result = measure.measure(geometry([(0, 2, 0, 1, 2, 1), (4, 5, -2, 2, 1, 3)]))
        self.assertEqual(result["boundsMinM"], [-1, 0, -5])
        self.assertEqual(result["boundsMaxM"], [6, 6, 1])
        self.assertEqual((result["widthM"], result["heightM"], result["depthM"]), (7, 6, 6))

    def test_invalid_headers_and_truncation_fail(self):
        data = geometry([(0, 0, 0, 1, 1, 1)])
        for bad in (b"", data[:9], data[:70], data[:-1], b"\x02\x00" + data[2:]):
            with self.assertRaises(ValueError):
                measure.measure(bad)

    def test_invalid_bounds_fail(self):
        for values in ((0, 0, 0, -1, 1, 1), (float("nan"), 0, 0, 1, 1, 1)):
            with self.assertRaises(ValueError):
                measure.measure(geometry([values]))

    def test_inventory_coverage_and_identity(self):
        data = json.loads((ROOT / "data/base-game-vegetation.v1.json").read_text(encoding="utf-8"))
        self.assertFalse(data["coverage"]["namedAssetsWithoutMeasurements"])
        self.assertFalse(data["rejectedMeasurements"])
        self.assertEqual(len(data["assets"]), len({r["id"] for r in data["assets"]}))
        for asset in data["assets"]:
            self.assertTrue(asset["baseGameMeasurements"])
            for mesh in asset["baseGameMeasurements"]:
                self.assertGreater(mesh["heightM"], 0)
                self.assertGreater(mesh["widthM"], 0)
                self.assertGreater(mesh["depthM"], 0)
                self.assertNotIn("_LOD", mesh["meshName"])

    def test_london_plane_keeps_pack_variants(self):
        data = json.loads((ROOT / "data/base-game-vegetation.v1.json").read_text(encoding="utf-8"))
        asset = next(a for a in data["assets"] if a["id"] == "cs2:NA_LondonPlaneTree01")
        mature = [m for m in asset["baseGameMeasurements"] if m["stage"] == "mature"]
        self.assertEqual({m["archive"] for m in mature}, {"Game/Blob.cok", "Game/Blob_Environment.cok"})
        for mesh in mature:
            self.assertAlmostEqual(mesh["heightM"], 7.7058, places=3)


if __name__ == "__main__":
    unittest.main()
