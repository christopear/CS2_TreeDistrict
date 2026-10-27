using System.Globalization;
using System.Text;
using System.Text.Json;
using TreeDistrict.Core.Foliage;

// A standalone demonstration adapter. The library itself performs no file I/O.
try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var flags = new HashSet<string>(StringComparer.Ordinal);
    var valueOptions = new[] { "--profiles", "--size", "--seed", "--density", "--species", "--temperature", "--output", "--svg",
        "--grade-percent", "--elevation", "--reference-elevation", "--cooling-per-km" };
    for (int i = 0; i < args.Length; i++)
    {
        string key = args[i];
        if (key == "--help")
        {
            Console.WriteLine("Standalone foliage planner: --profiles FILE --size METRES --seed UINT64 --density PERCENT --species maple,birch,oak --temperature C --grade-percent GRADE --elevation METRES --reference-elevation METRES --cooling-per-km C --no-competition --no-shade --output PLAN.json --svg PLOT.svg");
            return 0;
        }
        if (key == "--no-competition" || key == "--no-shade")
        { if (!flags.Add(key)) throw new ArgumentException("Duplicate flag: " + key); continue; }
        if (!valueOptions.Contains(key) || i + 1 >= args.Length || !options.TryAdd(key, args[++i]))
            throw new ArgumentException("Unknown, duplicate or incomplete option: " + key);
    }
    string Option(string key, string fallback) => options.TryGetValue(key, out var value) ? value : fallback;
    double Number(string key, double fallback) => options.TryGetValue(key, out var value)
        ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;
    var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
    string profilePath = Option("--profiles", Path.Combine(AppContext.BaseDirectory, "foliage-model.v1.json"));
    using var document = JsonDocument.Parse(File.ReadAllText(profilePath));
    if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1
        || document.RootElement.GetProperty("algorithmVersion").GetString() != FoliageGenerator.AlgorithmVersion)
        throw new ArgumentException("Unsupported profile schema or algorithm version.");
    var profiles = JsonSerializer.Deserialize<SpeciesProfile[]>(document.RootElement.GetProperty("profiles").GetRawText(), json)
        ?? throw new ArgumentException("Missing profiles.");
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
    double slopeDegrees = Math.Atan(Math.Abs(grade) / 100) * 180 / Math.PI;
    var request = new GenerationRequest
    {
        Seed = ulong.Parse(Option("--seed", "42"), CultureInfo.InvariantCulture),
        Species = profiles,
        OverallDensityPercent = Number("--density", 100),
        Climate = new ClimateProfile(temperature, Number("--reference-elevation", 0), Number("--cooling-per-km", 6.5)),
        ResolveCompetition = !flags.Contains("--no-competition"),
        ResolveShade = !flags.Contains("--no-shade"),
        Boundary = new[] { new Position2(0, 0), new Position2(side, 0), new Position2(side, side), new Position2(0, side) }
    };
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var result = new FoliageGenerator().Generate(request,
        p => new GroundSample(elevation + p.X * grade / 100, slopeDegrees), cancellation.Token);
    Console.WriteLine($"{result.AlgorithmVersion}; seed={request.Seed}; area={result.AreaM2:F0} m²");
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
    if (options.TryGetValue("--output", out var output))
        Save(output, JsonSerializer.Serialize(new { schemaVersion = 1, request, terrain = new { baseElevationM = elevation, gradePercent = grade, slopeDegrees }, result }, json));
    if (options.TryGetValue("--svg", out var svgPath))
    {
        string F(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        string Escape(string text) => System.Security.SecurityElement.Escape(text) ?? "";
        string[] palette = { "#43a047", "#d18b28", "#487fc1", "#a05da8", "#a6ab36" };
        var ordered = profiles.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        var colors = ordered.Select((p, i) => (p.Id, Color: palette[i % palette.Length])).ToDictionary(p => p.Id, p => p.Color);
        var profilesById = profiles.ToDictionary(p => p.Id);
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"960\" viewBox=\"0 0 900 960\"><rect width=\"900\" height=\"960\" fill=\"#f5f3ed\"/><text x=\"30\" y=\"28\" font-family=\"sans-serif\" font-size=\"18\">Foliage plan · {F(side)} m square · seed {request.Seed}</text>");
        double scale = 840 / side;
        svg.Append("<defs><clipPath id=\"plot\"><rect x=\"30\" y=\"50\" width=\"840\" height=\"840\"/></clipPath></defs><g clip-path=\"url(#plot)\">");
        foreach (var p in result.Placements.OrderBy(p => profilesById[p.SpeciesId].MatureHeightM))
        {
            double x = 30 + p.Position.X * scale, y = 50 + p.Position.Z * scale;
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
    || error is JsonException || error is FormatException || error is OverflowException || error is OperationCanceledException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
