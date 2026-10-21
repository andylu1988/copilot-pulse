using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CopilotPulse;
public partial class MainWindow
{
    private async Task ReviewQuotaScenarios(string directory)
    {
        var now = DateTimeOffset.UtcNow;
        foreach(string language in new[]{"zh-CN","en"})
        {
            UiText.Language=language;
            OpenInsights(1); await Task.Delay(350);
            for(int index=0;index<3;index++) { insightsWindow!.SelectForecastRangeForReview(index); await Task.Delay(50); }
            insightsWindow!.SelectForecastRangeForReview(1);
            foreach(string scenario in new[]{"standard","risk","stale"})
            {
                var reading=new Consumption {Ok=true,FetchedAt=now,Total=577.14m,Accounts=new(){new Account{Name=UiText.T("演示账户 · Standard","Demo account · Standard"),Login="demo",Cost=577.14m,Percent=5.77,LimitLabel="Standard"}}};
                if(scenario=="risk") {reading.Accounts[0].Name=UiText.T("演示账户 · 数值额度","Demo account · Numeric quota");reading.Accounts[0].Cost=95;reading.Accounts[0].Limit=100;reading.Accounts[0].Percent=95;}
                var h=Enumerable.Range(0,25).Select(i=>HistoryFrame.From(new Consumption {Ok=true,FetchedAt=now.AddHours(i-24),Accounts=new(){new Account{Name=reading.Accounts[0].Name,Login="demo",Cost=reading.Accounts[0].Cost-(24-i)*(scenario=="risk"?2m:1.5m)} }},3600)).ToArray();
                insightsWindow!.ReviewForecast(reading,h,scenario=="stale"?now.AddHours(3):now);
                await Task.Delay(120);
                CaptureControl(insightsWindow,Path.Combine(directory,$"quota-{scenario}-{language}.png"));
                string text=insightsWindow.ForecastReviewText();
                string required=scenario=="standard"?UiText.T("百分比外推","Percentage extrapolation"):scenario=="risk"?UiText.T("预计在","Estimated depletion"):UiText.T("源读数已延迟","Source reading is stale");
                if(!text.Contains(required)) throw new InvalidOperationException("Quota scenario UI missing state: "+scenario+" "+language);
                if(scenario=="stale" && text.Contains(UiText.T("月末累计约","Projected month-end"))) throw new InvalidOperationException("Stale UI still predicts");
                if (scenario != "stale")
                {
                    int projection = text.IndexOf(UiText.T("预计月末累计用量", "Projected month-end usage"), StringComparison.Ordinal);
                    int stats = text.IndexOf(UiText.T("当前已用", "Currently used"), StringComparison.Ordinal);
                    if (projection < 0 || stats <= projection) throw new InvalidOperationException("Forecast emphasis order invalid");
                }
                File.WriteAllText(Path.Combine(directory,$"quota-{scenario}-{language}.txt"),text);
            }
            insightsWindow!.Close();
        }
        UiText.Language=preferences.Language;
    }
}
