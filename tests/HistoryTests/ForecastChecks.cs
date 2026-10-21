using CopilotPulse;
internal static class ForecastChecks
{
    internal static void Run(Action<string,bool> check)
    {
        var now = new DateTimeOffset(2026,10,21,12,0,0,TimeSpan.Zero);
        Account A(decimal cost=80, decimal? limit=100, double percent=80) => new() {Name="Test",Login="demo",Cost=cost,Limit=limit,Percent=percent};
        HistoryFrame F(DateTimeOffset t,decimal cost) => HistoryFrame.From(new Consumption {Ok=true,FetchedAt=t,Accounts=new(){A(cost)},Total=cost},60);
        var history = Enumerable.Range(0,61).Select(i=>F(now.AddMinutes(i-60),70+i/6m)).ToArray();
        QuotaForecast B(Account a, DateTimeOffset? at=null, DateTimeOffset? clock=null, IReadOnlyList<HistoryFrame>? h=null) => QuotaForecasting.Build(h??history,a,at??now,(at??now).AddHours(-1),clock??now);
        var normal=B(A());
        check("forecast numeric remaining and daily allowance",normal.Remaining==20 && normal.DailyAllowance>0 && !normal.PercentEstimate);
        check("forecast realistic numeric depletion in two hours",normal.State==ForecastState.Ready && normal.Depletion==now.AddHours(2));
        check("forecast month end uses remaining hours",normal.ProjectedCost==2600 && normal.ProjectedPercent==2600);
        var standard=B(A(80,null,8));
        check("Standard forecast works without numeric cap",standard.State==ForecastState.Ready && standard.PercentEstimate && standard.Remaining==null && standard.RemainingPercent==92);
        check("Standard prediction is percentage based",standard.ProjectedPercent==260 && standard.Depletion==now.AddHours(92) && standard.DailyAllowance==null && standard.DailyPercentAllowance>0);
        var zero=B(A(0,null,0),h:new[]{F(now.AddHours(-1),0),F(now,0)});
        check("zero Standard reading never invents quota",zero.DepletesBeforeReset==null && zero.ProjectedPercent==null && zero.ProjectedCost==0);
        check("too coarsely rounded Standard percentage not extrapolated",B(A(80,null,.01)).ProjectedPercent==null);
        var tiny=B(A(80,1000000000,0),h:new[]{F(now.AddHours(-1),79.999m),F(now,80)});
        check("tiny rate explicitly safe through reset",tiny.DepletesBeforeReset==false && tiny.Depletion==null);
        var exhausted=B(A(110,100,110));
        check("exceeded quota explicitly exhausted",exhausted.Exhausted && exhausted.Remaining==0 && exhausted.Depletion==now);
        check("zero numeric quota is not unknown",B(A(0,0,0),h:new[]{F(now.AddHours(-1),0)}).Exhausted);
        check("exhaustion visible without history",B(A(110),h:Array.Empty<HistoryFrame>()).Exhausted);
        var flat=B(A(),h:new[]{F(now.AddHours(-1),80),F(now,80)});
        check("flat numeric rate no depletion before reset",flat.DepletesBeforeReset==false && flat.ProjectedCost==80);
        check("stale source suppresses forecasts",B(A(),clock:now.AddMinutes(10)).State==ForecastState.Stale && B(A(),clock:now.AddMinutes(10)).Depletion==null);
        var delay=B(A(),clock:now.AddMinutes(1));
        check("fresh source depletion does not slide with repaint",delay.Depletion==normal.Depletion);
        check("future source timestamp rejected",B(A(),at:now.AddMinutes(1)).State==ForecastState.InvalidReading);
        check("prior UTC month reading rejected",B(A(),at:new DateTimeOffset(2026,9,30,23,59,0,TimeSpan.Zero)).State==ForecastState.WrongMonth);
        check("source current endpoint used without saved sample",B(A(),h:history.Take(60).ToArray()).Depletion==normal.Depletion);
        check("correction has distinct status",B(A(60)).State==ForecastState.Correction);
        check("missing account has distinct status",B(A(),h:new[]{HistoryFrame.From(new Consumption{FetchedAt=now.AddMinutes(-5),Accounts=new(){new Account{Name="Other"}}},60)}).State==ForecastState.MissingAccount);
        check("insufficient samples has distinct status",B(A(),h:Array.Empty<HistoryFrame>()).State==ForecastState.Insufficient);
        var gap=B(A(),h:new[]{F(now.AddHours(-1),70)});
        check("offline gap is disclosed with endpoint average",gap.HasGaps && gap.Rate?.PerHour==10);
        var end=new DateTimeOffset(2026,10,31,23,0,0,TimeSpan.Zero);
        var exact=QuotaForecasting.Build(new[]{F(end.AddHours(-1),60)},A(),end,end.AddHours(-1),end);
        check("exact exhaustion at reset not before reset",exact.DepletesBeforeReset==false && exact.Reset==end.AddHours(1));
        var utcBoundary=new DateTimeOffset(2026,11,1,8,30,0,TimeSpan.FromHours(8));
        var reset=QuotaForecasting.Build(new[]{F(utcBoundary.AddHours(-1),900),F(utcBoundary.AddMinutes(-30),0)},A(5),utcBoundary,utcBoundary.AddHours(-1),utcBoundary);
        check("system offset retains UTC month cutoff",reset.State==ForecastState.Ready && reset.Rate?.PerHour==10 && reset.Reset==new DateTimeOffset(2026,12,1,0,0,0,TimeSpan.Zero));
        check("long sampling cadence freshness respected",QuotaForecasting.Build(history,A(),now,now.AddHours(-1),now.AddMinutes(10),900).State==ForecastState.Ready);
        check("future historical samples ignored",B(A(),h:history.Concat(new[]{F(now.AddHours(1),999)}).ToArray()).Depletion==normal.Depletion);
    }
}
