using CopilotPulse;
using System.Text.Json;
internal static class WorkspaceChecks
{
    internal static void Run(string directory, Action<string,bool> check)
    {
        var now=new DateTimeOffset(2026,10,21,12,0,0,TimeSpan.Zero);
        string path=Path.Combine(directory,"workspace-tasks.json");
        var repo=new TaskRepository(path); var task=repo.Start("Task A",now,60);
        repo.AddMarker(task.Id,"Run tests",now.AddMinutes(1)); repo.RequestSettlement(task.Id,now.AddMinutes(2));
        repo=new TaskRepository(path);
        check("settlement and marker survive restart",repo.Tasks[0].SettlementRequestedUtc==now.AddMinutes(2)&&repo.Tasks[0].Markers.Single().Name=="Run tests");
        repo.RequestSettlement(task.Id,now.AddMinutes(3));
        check("second end click cannot reset settlement clock",repo.Tasks[0].SettlementRequestedUtc==now.AddMinutes(2));
        check("old sample ignored",!repo.AdvanceSettlement(now.AddMinutes(3),now.AddMinutes(1))&&repo.Tasks[0].SettlementSamples==0);
        check("first new sample keeps pending",!repo.AdvanceSettlement(now.AddMinutes(3),now.AddMinutes(3))&&repo.Tasks[0].SettlementSamples==1);
        repo.AdvanceSettlement(now.AddMinutes(4),now.AddMinutes(3)); repo.AdvanceSettlement(now.AddMinutes(4),now.AddMinutes(5));
        check("duplicate and future samples ignored",repo.Tasks[0].SettlementSamples==1);
        repo.Rename(task.Id,"Renamed"); check("rename retains pending and marker",repo.Tasks[0].SettlementSamples==1&&repo.Tasks[0].Markers.Count==1);
        repo.AdvanceSettlement(now.AddMinutes(4),now.AddMinutes(4));
        check("third distinct successful sample confirms settlement",repo.AdvanceSettlement(now.AddMinutes(5),now.AddMinutes(5))&&repo.Tasks[0].SettlementResult=="observed"&&repo.Tasks[0].EndUtc==now.AddMinutes(5));
        check("confirmed state survives restart",new TaskRepository(path).Tasks[0].SettlementSamples==3);
        task=repo.Start("Timeout",now.AddHours(1),60); repo.RequestSettlement(task.Id,now.AddHours(1));
        check("not timed out before ten minutes",!repo.AdvanceSettlement(now.AddHours(1).AddMinutes(9)));
        check("timeout explicitly unconfirmed",repo.AdvanceSettlement(now.AddHours(1).AddMinutes(10))&&repo.Tasks.Last().SettlementResult=="timeout");
        task=repo.Start("Manual",now.AddHours(2),60);repo.AddMarker(task.Id,"Stage",now.AddHours(2));repo.RequestSettlement(task.Id,now.AddHours(2));repo.End(task.Id,now.AddHours(2).AddMinutes(1));
        check("manual ending preserves stage and marks manual",repo.Tasks.Last().SettlementResult=="manual"&&repo.Tasks.Last().Markers.Count==1);
        try{repo.AddMarker(task.Id,"late",now.AddHours(3));check("ended task rejects new markers",false);}catch(InvalidOperationException){check("ended task rejects new markers",true);}
        HistoryFrame F(DateTimeOffset time,decimal value)=>HistoryFrame.From(new Consumption{Ok=true,FetchedAt=time,Total=value,Accounts=new(){new Account{Name="Example",Login="demo",Cost=value}}},60);
        var frames=new[]{F(now,100),F(now.AddMinutes(1),105),F(now.AddMinutes(2),105),F(now.AddMinutes(3),103),F(now.AddHours(1),110)};
        var daily=ActivitySummary.Days(frames,now.Date,now.Date.AddDays(1),TimeZoneInfo.Utc);
        check("heatmap sums observed positive changes without negative corrections",daily[0].Delta==5);
        check("heatmap missing day stays unknown",daily[1].Delta==null&&daily[1].Samples==0);
        check("heatmap gaps and corrections excluded from coverage",daily[0].CoveredSeconds==120);
        check("flat valid samples distinguish zero from unknown",ActivitySummary.Days(new[]{F(now,10),F(now.AddMinutes(1),10)},now.Date,now.Date,TimeZoneInfo.Utc)[0].Delta==0);
        check("single reading cannot imply zero consumption",ActivitySummary.Days(new[]{F(now,10)},now.Date,now.Date,TimeZoneInfo.Utc)[0].Delta==null);
        var boundary=new DateTimeOffset(2026,11,1,0,0,0,TimeSpan.Zero);
        var reset=new[]{F(boundary.AddMinutes(-1),100),F(boundary,1),F(boundary.AddMinutes(1),3)};
        var china=TimeZoneInfo.CreateCustomTimeZone("Test+8",TimeSpan.FromHours(8),"Test+8","Test+8");
        var west=TimeZoneInfo.CreateCustomTimeZone("Test-7",TimeSpan.FromHours(-7),"Test-7","Test-7");
        check("UTC reset not counted as consumption",ActivitySummary.Days(reset,boundary.Date,boundary.Date,china)[0].Delta==2);
        check("heatmap labels local calendar date",ActivitySummary.Days(reset,boundary.Date.AddDays(-1),boundary.Date,west)[0].Samples==3);
        var month=ActivitySummary.Month("2026-10",frames,repo.Tasks,china);
        check("monthly archive selects UTC month latest observation",month.LastObserved==110&&month.LastSampleUtc==now.AddHours(1));
        check("monthly coverage uses full UTC month",Math.Abs(month.CoveragePercent-120d/(31*86400)*100)<.000001);
        check("monthly archive counts saved tasks",month.SavedTasks==3);
        check("archive local dates cover UTC month endpoints",month.Days.First().Date==new DateTime(2026,10,1)&&month.Days.Last().Date==new DateTime(2026,11,1));
        var empty=ActivitySummary.Month("2026-09",frames,repo.Tasks,china);
        check("empty archive does not invent monthly values",empty.LastObserved==null&&empty.MostActiveDate==null&&empty.CoveragePercent==0);
        var events=new[]{new SamplingEvent(now,"live",60)};
        string csv=TaskExport.Csv(repo.Tasks,frames,events,now.AddDays(1),true);
        check("export has marker records and settlement metadata",csv.Contains("settlement_result")&&csv.Contains("\"marker\""));
        check("anonymous CSV removes task and marker names",!csv.Contains("Run tests")&&!csv.Contains("Renamed")&&!csv.Contains("Stage"));
        check("all CSV rows retain seventeen columns",csv.Split("\r\n",StringSplitOptions.RemoveEmptyEntries).All(row=>row.Split(',').Length==17));
        string json=TaskExport.Json(repo.Tasks,frames,events,now.AddDays(1),true);
        using var doc=JsonDocument.Parse(json);
        check("JSON v2 exports markers and settlement",doc.RootElement.GetProperty("schemaVersion").GetInt32()==2&&json.Contains("observed")&&json.Contains("Marker"));
        check("anonymous JSON removes marker descriptions",!json.Contains("Run tests")&&!json.Contains("Renamed"));
    }
}
