using System.Text;
using XAUUSDAnalyzerApi.Models;

namespace XAUUSDAnalyzerApi.Services;

public class AnalyzerService
{
    private readonly TwelveDataClient _client = new();

    private static string? lastSignal = null;

    public async Task<(AnalysisResult result5, AnalysisResult result15, string finalSignal)> AnalyzeAsync(string symbol = "XAU/USD")
    {
        // گرفتن داده‌های 5 دقیقه‌ای و 15 دقیقه‌ای
        var data5 = await _client.GetHistoricAsync(symbol, "5min", 300);
        var data15 = await _client.GetHistoricAsync(symbol, "15min", 300);

        // تحلیل هر تایم‌فریم
        var result5 = AnalyzeSingleTimeframe(symbol, "5m", data5);
        var result15 = AnalyzeSingleTimeframe(symbol, "15m", data15);

        // تعیین سیگنال نهایی
        string finalSignal = "🤔 بدون قطعیت (HOLD)";
        bool short5 = result5.Signal.Contains("SHORT");
        bool short15 = result15.Signal.Contains("SHORT");
        bool long5 = result5.Signal.Contains("LONG");
        bool long15 = result15.Signal.Contains("LONG");

        if (long5 && long15)
            finalSignal = "✅ خرید (LONG)";
        else if (short5 && short15)
            finalSignal = "🚨 فروش (SHORT)";

        return (result5, result15, finalSignal);
    }
    private AnalysisResult AnalyzeSingleTimeframe(string symbol, string tf, List<Candle> candles)
    {
        var res = new AnalysisResult
        {
            Symbol = symbol.ToUpper(),
            Timeframe = tf,
            Signal = "🤝 HOLD",
            Verdict = "🤝 خنثی"
        };

        if (candles == null || candles.Count < 50)
        {
            res.Signal = "⚠️ داده کافی نیست";
            return res;
        }

        var closes = candles.Select(c => c.Close).ToList();
        var rsiList = IndicatorService.CalculateRSI(closes, 14);
        var ema14List = IndicatorService.CalculateEMA(closes, 14);
        var ema50List = IndicatorService.CalculateEMA(closes, 50);
        var macdTuple = IndicatorService.CalculateMACD(closes);

        if (!rsiList.Any() || !ema14List.Any() || !ema50List.Any() || !macdTuple.macd.Any() || !macdTuple.signal.Any())
        {
            res.Signal = "⚠️ داده کافی نیست";
            return res;
        }

        decimal rsi = rsiList.Last();
        decimal ema14 = ema14List.Last();
        decimal ema50 = ema50List.Last();
        decimal macd = macdTuple.macd.Last();
        decimal macdSignal = macdTuple.signal.Last();

        res.RSI = rsi;
        res.EMA14 = ema14;
        res.EMA50 = ema50;
        res.MACD = macd;
        res.MACDSignal = macdSignal;

        int score = IndicatorService.ScoreConfluence(rsi, ema14, ema50, macd, macdSignal);
        res.Signal = score >= 2 ? "📈 LONG" : (score <= -2 ? "📉 SHORT" : "🤝 HOLD");

        decimal atr = IndicatorService.CalculateATR(candles);
        res.ATR = atr;
        decimal price = closes.Last();
        res.Entry = price;
        bool isLong = res.Signal.Contains("LONG");
        bool isShort = res.Signal.Contains("SHORT");

        if (isLong)
        {
            res.SL = price - atr;
            res.TP1 = price + atr * 2;
            res.TP2 = price + atr * 3;
        }
        else if (isShort)
        {
            res.SL = price + atr;
            res.TP1 = price - atr * 2;
            res.TP2 = price - atr * 3;
        }

        if (lastSignal == res.Signal && (isLong || isShort))
            res.Warnings.Add("سیگنال تکراری صادر شد، اما TP/SL دوباره محاسبه شد.");
        lastSignal = res.Signal;

        res.RiskPerUnit = res.SL == 0 ? 0 : Math.Abs(res.Entry - res.SL);
        var reward1 = res.TP1 == 0 ? 0 : Math.Abs(res.TP1 - res.Entry);
        res.RRR = res.RiskPerUnit > 0 ? reward1 / res.RiskPerUnit : 0;

        res.Warnings.AddRange(BuildWarnings(res.Signal, rsi, ema50, atr, res.Entry, res.SL, res.TP1));

        bool hasCritical = res.Warnings.Any(w => w.Contains("اشباع") || w.Contains("SL") || w.Contains("R/R"));
        if (isLong)
            res.Verdict = hasCritical ? "⛔ اسکپ (ورود نکن)" : "✅ ورود LONG";
        else if (isShort)
            res.Verdict = hasCritical ? "⛔ اسکپ (ورود نکن)" : "✅ ورود SHORT";

        return res;
    }

    private static List<string> BuildWarnings(string finalSignal, decimal rsi, decimal ema50, decimal atr, decimal entry, decimal sl, decimal tp1)
    {
        var warnings = new List<string>();
        bool isLong = finalSignal.Contains("LONG");
        bool isShort = finalSignal.Contains("SHORT");

        decimal risk = Math.Abs(entry - sl);
        decimal reward = Math.Abs(tp1 - entry);
        decimal rrr = risk > 0 ? reward / risk : 0;

        if (isLong && rsi >= 65) warnings.Add("RSI نزدیک اشباع خرید است → احتمال اصلاح کوتاه‌مدت.");
        if (isShort && rsi <= 35) warnings.Add("RSI نزدیک اشباع فروش است → احتمال اصلاح کوتاه‌مدت.");

        if (isLong && sl > ema50) warnings.Add("SL بهتر است زیر EMA50 باشد.");
        if (isShort && sl < ema50) warnings.Add("SL بهتر است بالای EMA50 باشد.");

        if (risk < atr * 0.8m) warnings.Add("فاصله SL کمتر از 0.8×ATR است → احتمال خوردن SL زیاد.");
        if (rrr < 1.8m) warnings.Add($"نسبت R/R پایین است ({rrr:N2}).");

        return warnings;
    }

    // خروجی متنی
    public string ToText(AnalysisResult result5, AnalysisResult result15, string finalSignal)
{
    var sb = new StringBuilder();

    sb.AppendLine("برنامه بررسی سیگنال طلا در بازار فارکس");
    sb.AppendLine("-----------------------------------");
    sb.AppendLine($"نقطه بررسی سیگنال: {FormatUtils.ToSlashDecimal(result5.Entry)}");
    sb.AppendLine("-----------------------------------");

    sb.AppendLine($"📊 تایم‌فریم {result5.Timeframe}");
    sb.AppendLine($"   RSI: {FormatUtils.ToSlashDecimal(result5.RSI)}");
    sb.AppendLine($"   EMA14/50: {FormatUtils.ToSlashDecimal(result5.EMA14)} / {FormatUtils.ToSlashDecimal(result5.EMA50)}");
    sb.AppendLine($"   MACD: {FormatUtils.ToSlashDecimal(result5.MACD)} / {FormatUtils.ToSlashDecimal(result5.MACDSignal)}");
    sb.AppendLine($"   سیگنال: {result5.Signal}");
    sb.AppendLine("-----------------------------------");

    sb.AppendLine($"📊 تایم‌فریم {result15.Timeframe}");
    sb.AppendLine($"   RSI: {FormatUtils.ToSlashDecimal(result15.RSI)}");
    sb.AppendLine($"   EMA14/50: {FormatUtils.ToSlashDecimal(result15.EMA14)} / {FormatUtils.ToSlashDecimal(result15.EMA50)}");
    sb.AppendLine($"   MACD: {FormatUtils.ToSlashDecimal(result15.MACD)} / {FormatUtils.ToSlashDecimal(result15.MACDSignal)}");
    sb.AppendLine($"   سیگنال: {result15.Signal}");
    sb.AppendLine("-----------------------------------");

    sb.AppendLine($"📌 نتیجه نهایی: {finalSignal}");
    sb.AppendLine($"🎯 Entry: {FormatUtils.ToSlashDecimal(result5.Entry)}");
    sb.AppendLine($"🎯 TP1: {FormatUtils.ToSlashDecimal(result5.TP1)}");
    sb.AppendLine($"🎯 TP2: {FormatUtils.ToSlashDecimal(result5.TP2)}");
    sb.AppendLine($"🛑 SL: {FormatUtils.ToSlashDecimal(result5.SL)}");

    if (result5.Warnings.Any())
    {
        sb.AppendLine("⚠️ هشدارها:");
        foreach (var w in result5.Warnings)
            sb.AppendLine("   - " + w);
    }
    sb.AppendLine("-----------------------------------");
    sb.AppendLine("ساخته شده توسط: مهدی خسروآبادی و امید رحیم زاده");

    return sb.ToString();
}


    // خروجی HTML رنگی
    public string ToHtml(AnalysisResult result5, AnalysisResult result15, string finalSignal)
{
    string style = @"
    <style>
        body { font-family: Tahoma, sans-serif; background:#f9f9f9; color:#222; padding:20px; }
        h1 { color:#333; }
        h2 { margin-top:20px; color:#444; }
        .section { border:1px solid #ccc; padding:10px; margin:10px 0; background:#fff; border-radius:6px; }
        .signal-long { color:green; font-weight:bold; }
        .signal-short { color:red; font-weight:bold; }
        .signal-hold { color:orange; font-weight:bold; }
        .warn { color:#b00; margin-left:15px; }
        .sep { border-bottom:1px solid #ddd; margin:10px 0; }
        .entry { color:#0066cc; font-weight:bold; }
    </style>";

    var sb = new StringBuilder();
    sb.AppendLine("<html><head><meta charset='utf-8'>" + style + "</head><body>");
    sb.AppendLine("<h1>برنامه بررسی سیگنال طلا در بازار فارکس</h1>");
    sb.AppendLine("<h2>کاری از مهدی خسروآبادی و امید رحیم زاده</h2>");
    sb.AppendLine("<div class='sep'></div>");
    sb.AppendLine($"<p><b>نقطه بررسی سیگنال:</b> <span class='entry'>{FormatUtils.ToSlashDecimal(result5.Entry)}</span></p>");
    sb.AppendLine("<div class='sep'></div>");

    sb.AppendLine("<div class='section'>");
    sb.AppendLine($"<h3>📊 تایم‌فریم {result5.Timeframe}</h3>");
    sb.AppendLine($"<p>RSI: {FormatUtils.ToSlashDecimal(result5.RSI)}</p>");
    sb.AppendLine($"<p>EMA14/50: {FormatUtils.ToSlashDecimal(result5.EMA14)} / {FormatUtils.ToSlashDecimal(result5.EMA50)}</p>");
    sb.AppendLine($"<p>MACD: {FormatUtils.ToSlashDecimal(result5.MACD)} / {FormatUtils.ToSlashDecimal(result5.MACDSignal)}</p>");
    sb.AppendLine($"<p>سیگنال: {result5.Signal}</p>");
    sb.AppendLine("</div>");

    sb.AppendLine("<div class='section'>");
    sb.AppendLine($"<h3>📊 تایم‌فریم {result15.Timeframe}</h3>");
    sb.AppendLine($"<p>RSI: {FormatUtils.ToSlashDecimal(result15.RSI)}</p>");
    sb.AppendLine($"<p>EMA14/50: {FormatUtils.ToSlashDecimal(result15.EMA14)} / {FormatUtils.ToSlashDecimal(result15.EMA50)}</p>");
    sb.AppendLine($"<p>MACD: {FormatUtils.ToSlashDecimal(result15.MACD)} / {FormatUtils.ToSlashDecimal(result15.MACDSignal)}</p>");
    sb.AppendLine($"<p>سیگنال: {result15.Signal}</p>");
    sb.AppendLine("</div>");

    sb.AppendLine("<div class='section'>");
    sb.AppendLine($"<h3>📌 نتیجه نهایی: {finalSignal}</h3>");
    sb.AppendLine($"<p>🎯 Entry: {FormatUtils.ToSlashDecimal(result5.Entry)}</p>");
    sb.AppendLine($"<p>🎯 TP1: {FormatUtils.ToSlashDecimal(result5.TP1)}</p>");
    sb.AppendLine($"<p>🎯 TP2: {FormatUtils.ToSlashDecimal(result5.TP2)}</p>");
    sb.AppendLine($"<p>🛑 SL: {FormatUtils.ToSlashDecimal(result5.SL)}</p>");
    sb.AppendLine("</div>");

    if (result5.Warnings.Any())
    {
        sb.AppendLine("<div class='section'>");
        sb.AppendLine("<h3>⚠️ هشدارها:</h3>");
        foreach (var w in result5.Warnings)
            sb.AppendLine($"<p class='warn'>- {w}</p>");
        sb.AppendLine("</div>");
    }

    sb.AppendLine("</body></html>");
    return sb.ToString();
}

}
