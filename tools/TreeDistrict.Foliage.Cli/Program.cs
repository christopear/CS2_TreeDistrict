using System.Globalization;
using System.Text;
using System.Text.Json;
using TreeDistrict.Core.Foliage;

// A standalone demonstration adapter. The library itself performs no file I/O.
try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var flags = new HashSet<string>(StringComparer.Ordinal);
    var valueOptions = new[] { "--workspace", "--district", "--vegetation-set", "--sets", "--asset-catalog", "--botanical-catalog", "--adapter-settings", "--inspect-inputs", "--profiles", "--size", "--seed", "--density", "--species", "--temperature", "--output", "--svg",
        "--heightmap", "--terrain-smoothing", "--terrain-shape", "--environment-cell-size", "--generator", "--scatter-settings", "--spacing", "--attempts-per-point", "--grade-percent", "--elevation", "--reference-elevation", "--cooling-per-km" };
    for (int i = 0; i < args.Length; i++)
    {
        string key = args[i];
        if (key == "--help")
        {
            Console.WriteLine("Standalone foliage planner (defaults: Proton + authored environment profiles): --workspace EDITOR.json --district ID; --vegetation-set ID --sets FILE --asset-catalog FILE --botanical-catalog FILE --adapter-settings FILE --inspect-inputs REPORT.json --generator original|proton --heightmap FILE --terrain-smoothing METRES --terrain-shape plane|plateau --environment-cell-size METRES --scatter-settings FILE --spacing METRES --attempts-per-point N --profiles FILE --size METRES --seed UINT64 --density PERCENT --species maple,birch,oak --temperature C --grade-percent GRADE --elevation METRES --reference-elevation METRES --cooling-per-km C --no-competition --no-shade --post-suitability --no-composition --abundance | --no-abundance --output PLAN.json --svg PLOT.svg");
            return 0;
        }
        if (key == "--no-competition" || key == "--no-shade" || key == "--post-suitability" || key == "--no-composition" || key == "--no-abundance" || key == "--abundance")
        { if (!flags.Add(key)) throw new ArgumentException("Duplicate flag: " + key); continue; }
        if (!valueOptions.Contains(key) || i + 1 >= args.Length || !options.TryAdd(key, args[++i]))
            throw new ArgumentException("Unknown, duplicate or incomplete option: " + key);
    }
    if (flags.Contains("--abundance") && flags.Contains("--no-abundance"))
        throw new ArgumentException("Choose either --abundance or --no-abundance.");
    string Option(string key, string fallback) => options.TryGetValue(key, out var value) ? value : fallback;
    double Number(string key, double fallback) => options.TryGetValue(key, out var value)
        ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;
    var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
    IReadOnlyList<VegetationCatalogAdapter.Resolved>? resolved = null;
    SpeciesProfile[] profiles;
    if (options.TryGetValue("--asset-catalog", out var assetCatalog))
    {
        if (options.ContainsKey("--profiles")) throw new ArgumentException("Choose --asset-catalog or --profiles.");
        resolved = VegetationCatalogAdapter.Load(assetCatalog,
            Option("--botanical-catalog", Path.Combine(AppContext.BaseDirectory, "real-world-vegetation.v1.json")),
            Option("--adapter-settings", Path.Combine(AppContext.BaseDirectory, "vegetation-adapter.v1.json")));
        profiles = resolved.Select(p => p.Profile).ToArray();
    }
    else
    {
        string profilePath = Option("--profiles", Path.Combine(AppContext.BaseDirectory, "vegetation-environment.v1.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(profilePath));
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1
            || document.RootElement.GetProperty("algorithmVersion").GetString() != FoliageGenerator.AlgorithmVersion)
            throw new ArgumentException("Unsupported profile schema or algorithm version.");
        profiles = JsonSerializer.Deserialize<SpeciesProfile[]>(document.RootElement.GetProperty("profiles").GetRawText(), json)
            ?? throw new ArgumentException("Missing profiles.");
    }
    VegetationSet? vegetationSet = null;
    if (options.TryGetValue("--vegetation-set", out var setId))
    {

        if (options.ContainsKey("--species")) throw new ArgumentException("Choose --vegetation-set or --species.");
        vegetationSet = VegetationSetCatalog.Load(Option("--sets", Path.Combine(AppContext.BaseDirectory, "vegetation-sets.v1.json")), setId);
        profiles = vegetationSet.Apply(profiles);
    }
    if (options.TryGetValue("--species", out var selected))
    {
        var ids = selected.Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.Ordinal);
        if (ids.Any(id => !profiles.Any(p => p.Id == id))) throw new ArgumentException("Unknown species selection.");
        profiles = profiles.Where(p => ids.Contains(p.Id)).ToArray();
    }
    double side = Number("--size", 200);
    if (!double.IsFinite(side) || side <= 0) throw new ArgumentException("Size must be positive and finite.");
    double temperature = Number("--temperature", 18);
    if (!double.IsFinite(temperature)) throw new ArgumentException("Temperature must be finite.");
    double grade = Number("--grade-percent", 0), elevation = Number("--elevation", 0);
    if (!double.IsFinite(grade) || !double.IsFinite(elevation)) throw new ArgumentException("Grade and elevation must be finite.");
    string terrainShape = Option("--terrain-shape", "plane");
    if (terrainShape != "plane" && terrainShape != "plateau") throw new ArgumentException("Unknown terrain shape.");
    double smoothingM = Number("--terrain-smoothing", 0);
    if (!double.IsFinite(smoothingM) || smoothingM < 0) throw new ArgumentException("Terrain smoothing must be nonnegative and finite.");
    double rampStartM = side * .35, rampEndM = side * .65;
    double slopeDegrees = Math.Atan(Math.Abs(grade) / 100) * 180 / Math.PI;
    var request = new GenerationRequest
    {
        Seed = ulong.Parse(Option("--seed", "42"), CultureInfo.InvariantCulture),
        Species = profiles,
        OverallDensityPercent = Number("--density", vegetationSet?.OverallDensityPercent ?? 100),
        Climate = new ClimateProfile(temperature, Number("--reference-elevation", 0), Number("--cooling-per-km", 6.5)),
        ResolveCompetition = !flags.Contains("--no-competition"),
        ResolveShade = !flags.Contains("--no-shade"),
        Boundary = new[] { new Position2(0, 0), new Position2(side, 0), new Position2(side, side), new Position2(0, side) }
    };
    if (options.TryGetValue("--workspace", out var workspacePath))
    {
        if (!options.TryGetValue("--district", out var districtId)) throw new ArgumentException("--workspace requires --district ID.");
        var conflicting = new[] { "--profiles", "--asset-catalog", "--botanical-catalog", "--adapter-settings", "--vegetation-set", "--sets", "--species", "--density", "--temperature", "--reference-elevation", "--cooling-per-km", "--seed", "--size" };
        if (conflicting.Any(options.ContainsKey)) throw new ArgumentException("Workspace supplies profiles, boundary, seed, density and climate; remove conflicting options.");
        if (options.ContainsKey("--generator") && options["--generator"] != "proton") throw new ArgumentException("Workspace planning uses Proton.");
        var workspace = TreeDistrict.SettingsModel.Parse(File.ReadAllText(workspacePath));
        if ((int?)workspace["schemaVersion"] != 1) throw new ArgumentException("Unsupported workspace schema.");
        var district = workspace["districts"]?.OfType<Newtonsoft.Json.Linq.JObject>().SingleOrDefault(d => (string?)d["id"] == districtId)
            ?? throw new ArgumentException("District not found: " + districtId);
        request = TreeDistrict.DistrictGenerationAdapter.Create(district, (Newtonsoft.Json.Linq.JObject)workspace["city"]!, (double?)workspace["gameTemperatureC"]);
        profiles = request.Species.ToArray();
    }
    else if (options.ContainsKey("--district")) throw new ArgumentException("--district requires --workspace.");
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    string generator = Option("--generator", "proton");
    if (generator != "original" && generator != "proton") throw new ArgumentException("Unknown generator.");
    ProtonScatterSettings? scatterSettings = null;
    if (generator == "proton")
    {
        scatterSettings = JsonSerializer.Deserialize<ProtonScatterSettings>(File.ReadAllText(
            Option("--scatter-settings", Path.Combine(AppContext.BaseDirectory, "proton-scatter.v1.json"))), json)
            ?? throw new ArgumentException("Missing scatter settings.");
        if (flags.Contains("--post-suitability")) scatterSettings.UsePrePlacementSuitability = false;
        if (flags.Contains("--no-composition")) scatterSettings.EnableLocalComposition = false;
        if (flags.Contains("--abundance")) scatterSettings.EnableLocalAbundance = true;
        if (flags.Contains("--no-abundance")) scatterSettings.EnableLocalAbundance = false;
        scatterSettings.SpacingM = Number("--spacing", scatterSettings.SpacingM);
        scatterSettings.AttemptsPerPoint = int.Parse(Option("--attempts-per-point",
            scatterSettings.AttemptsPerPoint.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
    }
    var heightmapPath = options.TryGetValue("--heightmap", out var inputHeightmap) ? inputHeightmap : null;
    var heightmap = heightmapPath == null ? null : HeightmapTerrain.Load(heightmapPath);
    if (heightmap != null)
    {
        if (options.ContainsKey("--terrain-shape") || options.ContainsKey("--grade-percent")
            || options.ContainsKey("--terrain-smoothing") || options.ContainsKey("--elevation"))
            throw new ArgumentException("Heightmap cannot be combined with synthetic terrain options.");
        foreach (var corner in request.Boundary) heightmap.Sample(corner);
    }
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    GroundSample Sample(Position2 p)
    {
        if (heightmap != null) return heightmap.Sample(p);
        if (terrainShape == "plane") return new GroundSample(elevation + p.X * grade / 100, slopeDegrees);
        double run = Math.Clamp(p.X - rampStartM, 0, rampEndM - rampStartM);
        double derivative = p.X >= rampStartM && p.X < rampEndM ? 1 : 0;
        if (smoothingM > 0)
        {
            // Exact centred box-filter convolution of the piecewise-linear heightmap.
            double PositiveSquare(double v) => Math.Pow(Math.Max(0, v), 2);
            double Integral(double x) => .5 * (PositiveSquare(x - rampStartM) - PositiveSquare(x - rampEndM));
            double left = p.X - smoothingM / 2, right = p.X + smoothingM / 2;
            run = (Integral(right) - Integral(left)) / smoothingM;
            derivative = (Math.Clamp(right - rampStartM, 0, rampEndM - rampStartM)
                - Math.Clamp(left - rampStartM, 0, rampEndM - rampStartM)) / smoothingM;
        }
        return new GroundSample(elevation + run * grade / 100,
            Math.Atan(Math.Abs(derivative * grade / 100)) * 180 / Math.PI);
    }
    var coordinator = generator == "proton"
        ? new FoliageGenerationCoordinator(request, Sample,
            Number("--environment-cell-size", LocalEnvironmentGrid.DefaultCellSizeM), cancellationToken: cancellation.Token)
        : null;
    var environment = coordinator?.Environment;
    var result = coordinator != null
        ? coordinator.Generate(scatterSettings!, cancellation.Token)
        : new FoliageGenerator().Generate(request, Sample, cancellation.Token);
    stopwatch.Stop();
    Console.WriteLine($"Planning: {stopwatch.Elapsed.TotalMilliseconds:F1} ms (includes first-call/JIT overhead)");
    Console.WriteLine($"{result.AlgorithmVersion}; seed={request.Seed}; area={result.AreaM2:F0} m²");
    if (scatterSettings?.UsePrePlacementSuitability == true)
        Console.WriteLine($"Opportunities={result.SamplingOpportunities}; environmentally empty={result.EmptyEnvironmentSamples}; spacing/obstacle rejected={result.SpacingRejectedSamples}");
    Console.WriteLine("Species  Candidates  Terrain  Environment  Existing  Competition  Shade  Accepted");
    foreach (var c in result.Counts)
        Console.WriteLine($"{c.SpeciesId,-8} {c.Candidates,10} {c.TerrainRejected,8} {c.EnvironmentRejected,12} {c.ExistingPlantRejected,9} {c.CompetitionRejected,12} {c.ShadeRejected,6} {c.Accepted,9}");
    void Save(string path, string content)
    {
        string absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, content);
        Console.WriteLine("Wrote " + absolute);
    }
    if (options.TryGetValue("--inspect-inputs", out var inspectionPath))
    {
        var provider = new PlacementEnvironmentProvider(Sample, request.Climate, environment);
        var evaluator = new VegetationSuitabilityEvaluator();
        var samples = new[] { new Position2(side * .2, side / 2), new Position2(side / 2, side / 2), new Position2(side * .8, side / 2) };
        Save(inspectionPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            vegetationSet,
            request.Climate,
            request.Terrain,
            note = "Three proposed locations, not existing plants. Unknown inputs remain visible in source records; neutral scores are not proven tolerance.",
            vegetation = profiles.Select(profile => new
            {
                profile,
                source = resolved?.Single(p => p.Profile.Id == profile.Id),
                samples = samples.Select(position => new
                {
                    position,
                    environment = provider.Sample(position),
                    suitability = evaluator.Evaluate(profile, provider.Sample(position), request.Terrain)
                })
            })
        }, json));
    }
    if (options.TryGetValue("--output", out var output))
        Save(output, JsonSerializer.Serialize(new { schemaVersion = 1, generator, vegetationSet, scatterSettings, environmentGrid = environment == null ? null : new { environment.CellSizeM, environment.Columns, environment.Rows, environment.SampleCount }, request, terrain = new { shape = heightmap == null ? terrainShape : "heightmap", heightmapPath, baseElevationM = elevation, gradePercent = grade, slopeDegrees, rampStartM, rampEndM, smoothingM }, result }, json));
    if (options.TryGetValue("--svg", out var svgPath))
    {
        string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        string Escape(string text) => System.Security.SecurityElement.Escape(text) ?? "";
        string[] palette = { "#43a047", "#d18b28", "#487fc1", "#a05da8", "#a6ab36" };
        var ordered = profiles.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        var colors = ordered.Select((p, i) => (p.Id, Color: palette[i % palette.Length])).ToDictionary(p => p.Id, p => p.Color);
        var profilesById = profiles.ToDictionary(p => p.Id);
        double minX = request.Boundary.Min(p => p.X), minZ = request.Boundary.Min(p => p.Z);
        double width = request.Boundary.Max(p => p.X) - minX, depth = request.Boundary.Max(p => p.Z) - minZ;
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"960\" viewBox=\"0 0 900 960\"><rect width=\"900\" height=\"960\" fill=\"#f5f3ed\"/><text x=\"30\" y=\"28\" font-family=\"sans-serif\" font-size=\"18\">{Escape(generator == "proton" ? "ProtonScatter port" : "Original generator")} · {F(width)} × {F(depth)} m bounds · seed {request.Seed}</text>");
        double scale = 840 / Math.Max(width, depth);
        svg.Append("<defs><clipPath id=\"plot\"><rect x=\"30\" y=\"50\" width=\"840\" height=\"840\"/></clipPath></defs><g clip-path=\"url(#plot)\">");
        foreach (var p in result.Placements.OrderBy(p => profilesById[p.SpeciesId].MatureHeightM))
        {
            double x = 30 + (p.Position.X - minX) * scale, y = 50 + (p.Position.Z - minZ) * scale;
            svg.Append($"<circle cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"{F(profilesById[p.SpeciesId].CrownRadiusM * scale)}\" fill=\"{colors[p.SpeciesId]}\" fill-opacity=\"0.25\" stroke=\"{colors[p.SpeciesId]}\" stroke-width=\"0.5\"/><circle cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"1.5\" fill=\"{colors[p.SpeciesId]}\"/>");
        }
        svg.Append("</g><rect x=\"30\" y=\"50\" width=\"840\" height=\"840\" fill=\"none\" stroke=\"#555\"/>");
        svg.Append($"<text x=\"30\" y=\"920\" font-family=\"sans-serif\" font-size=\"15\">{Escape(string.Join(" · ", result.Counts.Select(c => c.SpeciesId + ": " + c.Accepted)))}</text>");
        svg.Append("<text x=\"30\" y=\"945\" font-family=\"sans-serif\" font-size=\"12\">Dots: tree positions. Circles: model crowns; overlap is allowed.</text></svg>");
        Save(svgPath, svg.ToString());
    }
    return 0;
}
catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is IOException
    || error is Newtonsoft.Json.JsonException || error is JsonException || error is FormatException || error is OverflowException || error is OperationCanceledException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
