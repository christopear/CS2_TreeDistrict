using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TreeDistrict
{
    // Pure placement planning, independently testable without game assemblies.
    public static class PlacementPlanner
    {
        public struct Point { public double X, Z; public Point(double x, double z) { X = x; Z = z; } }
        public struct Ground { public double Height, Slope; }
        public sealed class Placement { public double X, Y, Z, Rotation; public string Age; }
        public const int MaxTrees = 10000;
        public const int MaxCandidates = 250000;
        public static bool Contains(IReadOnlyList<Point> nodes, double x, double z)
        {
            bool inside = false;
            for (int i = 0, j = nodes.Count - 1; i < nodes.Count; j = i++)
            {
                var a = nodes[i]; var b = nodes[j];
                if ((a.Z > z) != (b.Z > z) && x < (b.X - a.X) * (z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
            }
            return inside;
        }
        private static bool Allowed(JToken limits, Ground ground) => ground.Height >= (double)limits["minElevationM"]
            && ground.Height <= (double)limits["maxElevationM"] && ground.Slope <= (double)limits["maxSlopeDegrees"];
        public static double Response(JToken curve, double value, bool slope = false)
        {
            double min = (double)curve["min"], mid = (double)curve["optimum"], max = (double)curve["max"];
            // Flat ground remains suitable; slope is a penalty, not a demand for a hillside.
            if (slope && value <= mid) return 1;
            return Math.Max(0, Math.Min(1, value <= mid ? (value - min) / (mid - min) : (max - value) / (max - mid)));
        }
        public static List<Placement> Plan(IReadOnlyList<Point> nodes, JObject set, JObject city, int seed, Func<double, double, Ground> sample)
        {
            SettingsModel.ValidateSet(set);
            if (nodes.Count < 3) throw new ArgumentException("The native area has no completed boundary.");
            var assets = set["assets"].Where(a => (double)a["densityPercent"] > 0).ToArray();
            if (assets.Length != 1 || (string)assets[0]["id"] != "cs2:NA_LondonPlaneTree01")
                throw new ArgumentException("This version generates the London Plane asset only. Choose the London Plane set.");
            var asset = assets[0]; var profile = asset["profile"];
            var ages = asset["ages"].ToArray();
            if (ages.Any(a => !new[] { "young", "teen", "mature", "old" }.Contains((string)a["id"])) ||
                Math.Abs(ages.Sum(a => (double)a["proportionPercent"]) - 100) > 0.001)
                throw new ArgumentException("London Plane age proportions must total 100% and use its supported ages.");
            double spacing = (double)profile["matureSpacingM"], step = spacing * 1.25;
            double minX = nodes.Min(p => p.X), maxX = nodes.Max(p => p.X), minZ = nodes.Min(p => p.Z), maxZ = nodes.Max(p => p.Z);
            double columns = Math.Ceiling((maxX - minX) / step) + 1, rows = Math.Ceiling((maxZ - minZ) / step) + 1;
            if (columns * rows > MaxCandidates) throw new ArgumentException("Area is too large for this spacing. Increase spacing or use smaller areas (250,000 candidate limit).");
            var random = new Random(seed); var result = new List<Placement>();
            var climate = set["climate"];
            double reference = (double)(climate["referenceTemperatureC"] ?? city["referenceTemperatureC"]);
            double elevation = (double)(climate["referenceElevationM"] ?? city["referenceElevationM"]);
            double cooling = (double)(climate["coolingCPerKm"] ?? city["coolingCPerKm"]);
            double density = (double)set["overallDensityPercent"] * (double)asset["densityPercent"] / 10000;
            for (double x = minX; x <= maxX; x += step)
                for (double z = minZ; z <= maxZ; z += step)
                {
                    double px = x + (random.NextDouble() - 0.5) * spacing * 0.2;
                    double pz = z + (random.NextDouble() - 0.5) * spacing * 0.2;
                    if (!Contains(nodes, px, pz)) continue;
                    var ground = sample(px, pz);
                    if (double.IsNaN(ground.Height) || double.IsInfinity(ground.Height) || double.IsNaN(ground.Slope) ||
                        !Allowed(set["limits"], ground) || !Allowed(profile["limits"], ground)) continue;
                    double temperature = reference - (ground.Height - elevation) * cooling / 1000;
                    double suitability = Response(profile["temperature"], temperature) * Response(profile["slope"], ground.Slope, true);
                    double cluster = 0.5 + 0.5 * Math.Sin(px / (step * 3) + seed) * Math.Cos(pz / (step * 3) - seed);
                    double clumping = (double)set["clumpingPercent"] / 100;
                    if (random.NextDouble() >= density * suitability * (1 - clumping + clumping * cluster)) continue;
                    if (result.Count >= MaxTrees) throw new ArgumentException("Generation would exceed 10,000 trees. Reduce density or split the area. Existing trees were retained.");
                    result.Add(new Placement { X = px, Y = ground.Height, Z = pz, Rotation = random.NextDouble() * Math.PI * 2 });
                }
            // Largest-remainder apportionment, then shuffle: closely match percentages even in small areas.
            var counts = ages.Select(a => (int)Math.Floor(result.Count * (double)a["proportionPercent"] / 100)).ToArray();
            foreach (int i in Enumerable.Range(0, ages.Length).OrderByDescending(i => result.Count * (double)ages[i]["proportionPercent"] / 100 - counts[i]).Take(result.Count - counts.Sum())) counts[i]++;
            var labels = new List<string>();
            for (int i = 0; i < counts.Length; i++) for (int j = 0; j < counts[i]; j++) labels.Add((string)ages[i]["id"]);
            for (int i = labels.Count - 1; i > 0; i--) { int j = random.Next(i + 1); string label = labels[i]; labels[i] = labels[j]; labels[j] = label; }
            for (int i = 0; i < result.Count; i++) result[i].Age = labels[i];
            return result;
        }
    }
}
