"""Validate hand-calculated population contracts, not a replacement generator.

The actual planner is exercised by SetPopulationTests.cs. Mixed cases here only
validate the specification until a generator adapter can consume them.
"""
import json
import math
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class PopulationContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads((ROOT / "tests/fixtures/set-population.v1.json").read_text())
        cls.cases = {c["id"]: c for c in cls.data["scenarios"]}

    def test_fixture_arithmetic_and_envelopes(self):
        self.assertEqual(len(self.cases), len(self.data["scenarios"]))
        for case in self.cases.values():
            self.assertEqual(set(case["expected"]), set(case["speciesDensityPercent"]))
            for species, percent in case["speciesDensityPercent"].items():
                profile = self.data["profiles"][species]
                # Independent dimensional derivation: hectares times opportunities/ha.
                mean = case["areaM2"] / 10000 * profile["opportunitiesPerHectare"]
                mean *= case["overallDensityPercent"] / 100 * percent / 100
                if case["stage"] == "uncontestedPlacement":
                    mean *= case["eligibleAreaFraction"] * case["uniformSuitability"]
                band = case["expected"][species]
                self.assertAlmostEqual(band["mean"], mean, places=7)
                tolerance = max(mean * .1, 4 * math.sqrt(mean))
                self.assertEqual(band["minimum"], max(0, math.floor(mean - tolerance)))
                self.assertEqual(band["maximum"], math.ceil(mean + tolerance))

    def test_adding_species_does_not_normalize_existing_requests(self):
        two = self.cases["maple-birch-candidates"]["expected"]
        three = self.cases["three-species-candidates"]["expected"]
        for species in two:
            self.assertEqual(two[species], three[species])
        self.assertEqual(two["maple"], self.cases["disabled-birch-does-not-redistribute"]["expected"]["maple"])

    def test_overall_multiplier_scales_every_species(self):
        full = self.cases["three-species-candidates"]["expected"]
        half = self.cases["three-species-half-overall"]["expected"]
        for species in full:
            self.assertAlmostEqual(half[species]["mean"], full[species]["mean"] / 2)

    def test_reference_units_and_area_scaling(self):
        for profile in self.data["profiles"].values():
            self.assertAlmostEqual(profile["opportunitiesPerHectare"], 10000 / profile["referencePitchM"] ** 2)
        # The small plot stays below the cap; scaling to 1 km² would exceed it.
        requested = sum(v["mean"] for v in self.cases["three-species-candidates"]["expected"].values())
        self.assertLess(requested, 10000)
        self.assertGreater(requested * 25, 10000)


if __name__ == "__main__":
    unittest.main()
