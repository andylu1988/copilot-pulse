using System.Text.Json;
using CopilotPulse;
internal static class ForecastReplay
{
    internal static void Run(string directory, string output, Action<string,bool> check)
    {
        var json = new JsonSerializerOptions { PropertyNameCaseInsensitive=true };
        var reading = JsonSerializer.Deserialize<Consumption>(File.ReadAllText(Path.Combine(directory,"consumption.json")),json)!;
        var frames = new HistoryRepository(Path.Combine(directory,"history")).ReadAll();
        check("real replay valid cache and journal",reading.Ok && frames.Count>2);
        var rows = new List<object>();
        int accountIndex=0;
        foreach(var account in reading.Accounts)
        {
            accountIndex++;
            foreach(int hours in new[]{1,24,0})
            {
                var from=hours==0?HistoryRanges.Start("month",reading.FetchedAt):reading.FetchedAt.AddHours(-hours);
                var f=QuotaForecasting.Build(frames,account,reading.FetchedAt,from,reading.FetchedAt);
                if(f.State==ForecastState.Ready)
                {
                    check($"real replay account {accountIndex} window {hours} rate nonnegative",f.Rate!.PerHour>=0);
                    if(account.Limit==null) check($"real replay account {accountIndex} window {hours} no invented monetary limit",f.Remaining==null && f.DailyAllowance==null);
                    if(f.PercentEstimate)
                    {
                        decimal expected=(decimal)account.Percent+(decimal)account.Percent/account.Cost*f.Rate.PerHour*(decimal)(f.Reset-reading.FetchedAt).TotalHours;
                        check($"real replay account {accountIndex} window {hours} independent percentage projection",Math.Abs(expected-f.ProjectedPercent!.Value)<.000001m);
                    }
                }
                rows.Add(new { Account=accountIndex, WindowHours=hours, State=f.State.ToString(), f.PercentEstimate, f.HasGaps, Samples=f.Rate?.Samples, Hours=f.Rate==null?(double?)null:(f.Rate.Until-f.Rate.From).TotalHours, RatePerHour=f.Rate?.PerHour, f.ProjectedPercent, f.DepletesBeforeReset });
            }
            check($"real replay account {accountIndex} stale cache paused",QuotaForecasting.Build(frames,account,reading.FetchedAt,reading.FetchedAt.AddHours(-24),reading.FetchedAt.AddHours(1)).State==ForecastState.Stale);
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new {Readings=frames.Count,AsOf=reading.FetchedAt,Results=rows},new JsonSerializerOptions {WriteIndented=true}));
    }
}
