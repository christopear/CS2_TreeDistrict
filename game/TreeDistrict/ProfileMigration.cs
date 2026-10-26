using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TreeDistrict
{
    public static class ProfileMigration
    {
        public static JObject Catalog(JObject manifest, JObject species)
        {
            var result = (JObject)manifest.DeepClone();
            var asset = species["asset"];
            result["assets"] = new JArray(asset.DeepClone());
            foreach (var entry in result["sets"])
            {
                var set = (JObject)entry["set"];
                set["assets"] = new JArray(set["assetIds"].Select(id => (string)id == (string)asset["id"]
                    ? asset.DeepClone() : throw new ArgumentException("Unknown default asset.")));
                set.Remove("assetIds");
            }
            return result;
        }
        // Only load-time migration: preserve legacy sets, using their mature profile once.
        public static void Set(JToken set)
        {
            if (!(set is JObject)) return;
            foreach (JObject asset in set["assets"] ?? new JArray())
            {
                if (asset["profile"] != null) continue;
                var ages = (JArray)asset["ages"];
                var mature = ages.FirstOrDefault(a => (string)a["id"] == "mature") ?? ages[0];
                var profile = (JObject)mature.DeepClone();
                foreach (string key in new[] { "id", "name", "enabled", "densityPercent", "footprintRadiusM" }) profile.Remove(key);
                profile["matureSpacingM"] = Math.Max(1, (double)mature["canopyRadiusM"] * 2);
                asset["profile"] = profile;
                double total = ages.Sum(a => (bool)a["enabled"] ? (double)a["densityPercent"] : 0);
                asset["ages"] = new JArray(ages.Select(a => new JObject {
                    ["id"] = a["id"], ["name"] = a["name"],
                    ["proportionPercent"] = total > 0 && (bool)a["enabled"] ? (double)a["densityPercent"] * 100 / total : 0
                }));
            }
        }
        public static void Working(JToken target)
        {
            Set(target["working"]); Set(target["baseline"]);
        }
    }
}
