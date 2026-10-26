"""Check research provenance, unit conversions and stable inventory links."""
import copy
import json
import unittest

from test_vegetation_measurements import ROOT, measure


class BotanicalCatalogue(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalogue = json.loads((ROOT / "data/real-world-vegetation.v1.json").read_text(encoding="utf-8"))
        cls.inventory = json.loads((ROOT / "data/base-game-vegetation.v1.json").read_text(encoding="utf-8"))

    def test_named_assets_have_references(self):
        profiles = self.catalogue["profiles"]
        self.assertEqual(len(profiles), 25)
        self.assertEqual(sum(p["kind"] == "tree" for p in profiles), 15)
        self.assertEqual(sum(p["kind"] == "shrub" for p in profiles), 10)
        self.assertEqual({p["id"] for p in profiles}, {
            a["id"] for a in self.inventory["assets"] if a["nameConfirmedInLocale"]})
        for profile in profiles:
            self.assertFalse(profile["identity"]["gameSpeciesConfirmed"])
            if profile["kind"] == "shrub":
                self.assertEqual(profile["identity"]["mappingStatus"], "representative")

    def test_provenance_and_conversions(self):
        sources = self.catalogue["sources"]

        def check_references(value):
            if isinstance(value, dict):
                if "sourceIds" in value:
                    self.assertTrue(value["sourceIds"])
                    for source in value["sourceIds"]:
                        self.assertIn(source, sources)
                        self.assertTrue(sources[source]["url"].startswith("https://"))
                for child in value.values():
                    check_references(child)
            elif isinstance(value, list):
                for child in value:
                    check_references(child)

        check_references(self.catalogue["profiles"])
        for profile in self.catalogue["profiles"]:
            self.assertTrue(profile["sizeObservations"])
            for observation in profile["sizeObservations"]:
                original = observation["original"]
                self.assertIn(original["unit"], ("ft", "m"))
                factor = .3048 if original["unit"] == "ft" else 1
                for metric, field in (("heightM", "height"), ("crownSpreadM", "spread")):
                    self.assertEqual(observation[metric], [round(v * factor, 3) for v in original[field]])
                    self.assertGreater(observation[metric][0], 0)
                    self.assertLessEqual(*observation[metric])
            self.assertTrue(all(v is None for v in profile["quantitativeModelInputs"].values()))

    def test_mapping_measurements_are_traceable(self):
        assets = {a["id"]: a for a in self.inventory["assets"]}
        for profile in self.catalogue["profiles"]:
            selection = profile["identity"]["selectionMeasurement"]
            meshes = assets[profile["id"]]["baseGameMeasurements"]
            match = next(m for m in meshes if m["meshName"] == selection["meshName"]
                         and m["archive"] == selection["archive"])
            for dimension in ("heightM", "widthM", "depthM"):
                self.assertEqual(match[dimension], selection[dimension])

    def test_refresh_rejoins_research_without_changing_geometry(self):
        refreshed = copy.deepcopy(self.inventory)
        for asset in refreshed["assets"]:
            if asset["nameConfirmedInLocale"]:
                asset["botanicalProfileRef"] = None
                asset.pop("botanicalProfileId", None)
                asset["botanicalResearchStatus"] = "notResearched"
        measure.link_botanical_references(refreshed, self.catalogue)
        self.assertEqual(refreshed, self.inventory)
        measure.link_botanical_references(refreshed, self.catalogue)
        self.assertEqual(refreshed, self.inventory)


if __name__ == "__main__":
    unittest.main()
