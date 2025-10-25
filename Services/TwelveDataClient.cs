using System.Globalization;
using System.Text.Json;
using XAUUSDAnalyzerApi.Models;

namespace XAUUSDAnalyzerApi.Services;

public class TwelveDataClient
{
    private readonly HttpClient _http = new();
    // 🔑 کلید API شما (ثابت در کد)
    private readonly string _apiKey = "21e5f10920d14e2cbb1b33220760a438";

    public async Task<List<Candle>> GetHistoricAsync(string symbol, string interval = "5min", int outputSize = 300)
    {
        string url = $"https://api.twelvedata.com/time_series?symbol={symbol}&interval={interval}&outputsize={outputSize}&apikey={_apiKey}";
        try
        {
            var response = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            if (!doc.RootElement.TryGetProperty("values", out var arr))
                return new List<Candle>();

            var candles = new List<Candle>();
            foreach (var d in arr.EnumerateArray().Reverse())
            {
                candles.Add(new Candle
                {
                    TimeUnix = DateTimeOffset.Parse(d.GetProperty("datetime").GetString()!).ToUnixTimeSeconds(),
                    Open     = decimal.Parse(d.GetProperty("open").GetString()!, CultureInfo.InvariantCulture),
                    High     = decimal.Parse(d.GetProperty("high").GetString()!, CultureInfo.InvariantCulture),
                    Low      = decimal.Parse(d.GetProperty("low").GetString()!, CultureInfo.InvariantCulture),
                    Close    = decimal.Parse(d.GetProperty("close").GetString()!, CultureInfo.InvariantCulture)
                });
            }
            return candles;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TwelveDataClient] Error: {ex.Message}");
            return new List<Candle>();
        }
    }
}
