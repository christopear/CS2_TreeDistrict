#nullable disable
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TreeDistrict
{
    // Pure host-side contract. Never accept a whole workspace from the UI.
    public static class SettingsModel
    {
        public static JObject Parse(string json)
        {
            if (json == null || json.Length > 5000000) throw new ArgumentException("Settings document is too large.");
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        public static JObject Initial(JObject catalog)
        {
            var first = catalog["sets"][0];
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["city"] = catalog["cityDefaults"].DeepClone(),
                ["gameTemperatureC"] = null,
                ["library"] = catalog["sets"].DeepClone(),
                ["districts"] = new JArray(),
                ["draft"] = new JObject { ["libraryId"] = first["id"].DeepClone(), ["baseline"] = first["set"].DeepClone(), ["working"] = first["set"].DeepClone() }
            };
        }
        public static JObject Target(JObject state, string id)
        {
            if (id == null) return (JObject)state["draft"];
            return state["districts"].OfType<JObject>().FirstOrDefault(d => (string)d["id"] == id)
                ?? throw new ArgumentException("Tree Area no longer exists.");
        }
        private static JObject Library(JObject state, string id) => state["library"].OfType<JObject>().FirstOrDefault(e => (string)e["id"] == id)
            ?? throw new ArgumentException("Vegetation set no longer exists.");
        private static void Choose(JObject state, JObject target, string id)
        {
            var entry = Library(state, id);
            target["libraryId"] = id;
            target["working"] = entry["set"].DeepClone();
            target["baseline"] = entry["set"].DeepClone();
        }
        private static void NewSet(JObject target)
        {
            target["libraryId"] = null; target["baseline"] = null;
            target["working"]["name"] = "New vegetation set"; target["working"]["assets"] = new JArray();
        }
        public static JObject Apply(JObject current, JObject command)
        {
            var next = (JObject)current.DeepClone();
            string type = Text(command["type"], 40);
            if (type == "city") { ValidateClimate(command["city"], false); next["city"] = command["city"].DeepClone(); return next; }
            if (command.Property("districtId") == null) throw new ArgumentException("Missing edit target.");
            string id = command["districtId"].Type == JTokenType.Null ? null : Text(command["districtId"], 160);
            var target = Target(next, id);
            switch (type)
            {
                case "editSet": ValidateSet(command["set"]); target["working"] = command["set"].DeepClone(); break;
                case "chooseSet": Choose(next, target, Text(command["libraryId"], 160)); break;
                case "newSet": NewSet(target); break;
                case "saveSet":
                    var saved = (JObject)target["working"].DeepClone(); saved["name"] = Text(command["name"], 80); ValidateSet(saved);
                    string newId = command["newId"] == null ? null : Text(command["newId"], 160);
                    if (newId != null)
                    {
                        var library = (JArray)next["library"];
                        if (library.Count >= 256 || library.Any(e => (string)e["id"] == newId)) throw new ArgumentException("Library is full or identifier already exists.");
                        library.Add(new JObject { ["id"] = newId, ["builtIn"] = false, ["set"] = saved.DeepClone() });
                        target["libraryId"] = newId;
                    }
                    else
                    {
                        var entry = Library(next, (string)target["libraryId"]);
                        if ((bool)entry["builtIn"]) throw new ArgumentException("Use Save as for a built-in set.");
                        entry["set"] = saved.DeepClone();
                    }
                    target["working"] = saved.DeepClone(); target["baseline"] = saved.DeepClone();
                    if (command["afterSaveLibraryId"] != null)
                    {
                        string after = Text(command["afterSaveLibraryId"], 160);
                        if (after == "__new") NewSet(target); else Choose(next, target, after);
                    }
                    break;
                case "district":
                    if (id == null) throw new ArgumentException("Select a Tree Area first.");
                    if (command["name"] != null) target["name"] = Text(command["name"], 80);
                    if (command["generationMode"] != null)
                    {
                        string mode = Text(command["generationMode"], 20);
                        if (mode != "auto" && mode != "onDemand") throw new ArgumentException("Invalid generation mode.");
                        target["generationMode"] = mode;
                    }
                    break;
                default: throw new ArgumentException("Unsupported settings command.");
            }
            if (id != null) target["revision"] = checked((int)target["revision"] + 1);
            return next;
        }
        public static string Text(JToken token, int max)
        {
            if (token?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token) || ((string)token).Length > max)
                throw new ArgumentException("Invalid or missing text value.");
            return ((string)token).Trim();
        }
        public static double Number(JToken value, double min, double max)
        {
            if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)) throw new ArgumentException("Expected a number.");
            double n = (double)value;
            if (double.IsNaN(n) || double.IsInfinity(n) || n < min || n > max) throw new ArgumentException("Number is outside the allowed range.");
            return n;
        }
        private static void Boolean(JToken value) { if (value?.Type != JTokenType.Boolean) throw new ArgumentException("Expected a boolean."); }
        private static JObject Object(JToken value) => value as JObject ?? throw new ArgumentException("Expected an object.");
        private static JArray Array(JToken value, int min, int max)
        {
            var array = value as JArray ?? throw new ArgumentException("Expected a list.");
            if (array.Count < min || array.Count > max) throw new ArgumentException("Invalid list length.");
            return array;
        }
        private static void Unique(JArray values)
        {
            var ids = values.Select(v => Text(v["id"], 160)).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Duplicate identifier.");
        }
        public static void ValidateClimate(JToken value, bool partial)
        {
            var obj = Object(value);
            foreach (var key in new[] { "referenceTemperatureC", "referenceElevationM", "coolingCPerKm" })
            {
                if (partial && obj[key] == null) continue;
                Number(obj[key], key == "referenceTemperatureC" ? -100 : key == "referenceElevationM" ? -2000 : 0,
                    key == "referenceTemperatureC" ? 100 : key == "referenceElevationM" ? 20000 : 20);
            }
            if (!partial && (string)obj["source"] != "manual" && (string)obj["source"] != "game") throw new ArgumentException("Invalid climate source.");
        }
        private static void Limits(JToken value)
        {
            var obj = Object(value);
            if (Number(obj["minElevationM"], -10000, 20000) > Number(obj["maxElevationM"], -10000, 20000)) throw new ArgumentException("Inverted elevation limits.");
            Number(obj["maxSlopeDegrees"], 0, 90);
        }
        private static void Response(JToken value, bool slope)
        {
            var obj = Object(value);
            double min = Number(obj["min"], slope ? 0 : -double.MaxValue, slope ? 90 : double.MaxValue);
            double mid = Number(obj["optimum"], slope ? 0 : -double.MaxValue, slope ? 90 : double.MaxValue);
            double max = Number(obj["max"], slope ? 0 : -double.MaxValue, slope ? 90 : double.MaxValue);
            if (!(min < mid && mid < max)) throw new ArgumentException("Response must have minimum < optimum < maximum.");
        }
        public static void ValidateSet(JToken value)
        {
            var set = Object(value); Text(set["name"], 80);
            Number(set["overallDensityPercent"], 0, 100); Number(set["clumpingPercent"], 0, 100);
            ValidateClimate(set["climate"], true); Limits(set["limits"]);
            var assets = Array(set["assets"], 0, 128); Unique(assets);
            foreach (var asset in assets)
            {
                Text(asset["name"], 80); Boolean(asset["placeholder"]); Number(asset["densityPercent"], 0, 100);
                var profile = Object(asset["profile"]);
                Number(profile["matureSpacingM"], 1, 200);
                if (Number(profile["heightM"], 0, 200) <= 0) throw new ArgumentException("Height must be positive.");
                Number(profile["canopyRadiusM"], 0, 100);
                Number(profile["canopyOpacityPercent"], 0, 100); Number(profile["shadeTolerancePercent"], 0, 100);
                Response(profile["temperature"], false); Response(profile["slope"], true); Limits(profile["limits"]);
                var ages = Array(asset["ages"], 1, 16); Unique(ages);
                foreach (var age in ages)
                {
                    Text(age["name"], 80); Number(age["proportionPercent"], 0, 100);
                }
            }
        }
        public static void ValidateRecord(JObject record)
        {
            Text(record["id"], 160); Text(record["name"], 80);
            ValidateWorking(record);
            if ((string)record["generationMode"] != "onDemand" && (string)record["generationMode"] != "auto") throw new ArgumentException("Invalid mode.");
            foreach (var key in new[] { "seed", "revision" })
                if (record[key]?.Type != JTokenType.Integer || Number(record[key], 0, int.MaxValue) < 0) throw new ArgumentException("Invalid integer.");
        }
        public static void ValidateWorking(JObject target)
        {
            ValidateSet(target["working"]);
            if (target["baseline"] == null || target["libraryId"] == null) throw new ArgumentException("Missing set baseline.");
            if (target["baseline"].Type != JTokenType.Null) ValidateSet(target["baseline"]);
            if (target["libraryId"].Type != JTokenType.Null) Text(target["libraryId"], 160);
        }
    }
}
