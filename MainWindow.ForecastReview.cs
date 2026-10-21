using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CopilotPulse;
public partial class MainWindow
{
    private async Task ReviewQuotaScenarios(string directory)
    {
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        foreach (string language in new[] { "zh-CN", "en" })
        {
            UiText.Language = language;
            var reading = new Consumption { Ok = true, FetchedAt = now, Total = 500,
                Accounts = new() { new Account { Name = "Hidden account A", Cost = 200 }, new Account { Name = "Hidden account B", Cost = 300 } } };
            var window = new InsightsWindow(null,
                () => throw new InvalidOperationException("Forecast must not read history"), () => reading,
                () => throw new InvalidOperationException("Forecast must not run diagnostics"), 1) { Owner = this };
            window.Show(); await window.ReloadAsync(); await Task.Delay(150);
            var settings = new Preferences { WarningMode = "amount", WarningAmount = 2000, WarningEnabled = true };
            foreach (string scenario in new[] { "standard", "risk", "reached", "boundary", "percent", "stale" })
            {
                settings.WarningMode = scenario == "percent" ? "percent" : "amount";
                settings.WarningAmount = scenario == "risk" ? 600 : scenario == "reached" ? 500 : scenario == "boundary" ? 1000 : 2000;
                window.ReviewForecast(reading, Array.Empty<HistoryFrame>(), scenario == "stale" ? now.AddHours(3) : now, settings);
                await Task.Delay(100);
                CaptureControl(window, Path.Combine(directory, $"quota-{scenario}-{language}.png"));
                string text = window.ForecastReviewText();
                string required = scenario == "standard" ? UiText.T("月末预计不超出提醒阈值", "stay below the alert threshold")
                    : scenario == "risk" || scenario == "boundary" ? UiText.T("按本月平均速度预计达到提醒阈值", "Expected to reach alert threshold")
                    : scenario == "reached" ? UiText.T("已达到提醒阈值", "Alert threshold reached")
                    : scenario == "percent" ? UiText.T("已读取百分比阈值", "Percentage threshold loaded")
                    : UiText.T("源读数已延迟", "Source reading is stale");
                if (!text.Contains(required) || text.Contains("Hidden account") || text.Contains("24" + UiText.T("小时", " hours")))
                    throw new InvalidOperationException("Overall forecast UI invalid: " + scenario + " " + language);
                if (scenario == "stale" && text.Contains("≈ $")) throw new InvalidOperationException("Stale UI still predicts");
                if (scenario != "stale" && !text.Contains("1,000.00")) throw new InvalidOperationException("Monthly projection must be $1,000");
                if (scenario == "standard" && (!text.Contains("2,000.00") || !text.Contains("25%"))) throw new InvalidOperationException("Configured $2000 threshold missing");
                File.WriteAllText(Path.Combine(directory, $"quota-{scenario}-{language}.txt"), text);
            }
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) window.ReviewForecast(reading, Array.Empty<HistoryFrame>(), now);
            File.WriteAllText(Path.Combine(directory, "forecast-timing-" + language + ".txt"), $"100 Overall forecast renders: {watch.Elapsed.TotalMilliseconds:0.0} ms. No history or diagnostic calls.");
            window.Close();
        }
        UiText.Language = preferences.Language;
    }
}
