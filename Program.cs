using XAUUSDAnalyzerApi.Services;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://*:{port}");

app.MapGet("/", () => "XAUUSD Analyzer API آماده است ✅");

// JSON
app.MapGet("/analyze", async (string symbol = "XAU/USD") =>
{
    var service = new AnalyzerService();
    var (r5, r15, finalSignal) = await service.AnalyzeAsync(symbol);
    return Results.Json(new { result5 = r5, result15 = r15, finalSignal });
});

// متن
app.MapGet("/analyzeText", async (string symbol = "XAU/USD") =>
{
    var service = new AnalyzerService();
    var (r5, r15, finalSignal) = await service.AnalyzeAsync(symbol);
    var text = service.ToText(r5, r15, finalSignal);
    return Results.Text(text);
});

// HTML
app.MapGet("/analyzeHtml", async (string symbol = "XAU/USD") =>
{
    var service = new AnalyzerService();
    var (r5, r15, finalSignal) = await service.AnalyzeAsync(symbol);
    var html = service.ToHtml(r5, r15, finalSignal);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.Run();
