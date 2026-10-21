using System.Text.Json;
using CopilotPulse;
internal static class InsightsChecks
{
    internal static void Run(Action<string,bool> check)
    {
        var now = new DateTimeOffset(2026,10,21,12,0,0,TimeSpan.Zero);
        HistoryFrame F(DateTimeOffset at, decimal n, string login = "demo") => HistoryFrame.From(new Consumption { Ok=true, FetchedAt=at, Total=n, Accounts=new() { new Account {Name="Test",Login=login,Cost=n} } },3600);
        var frames = new[] { F(now.AddHours(-2),10),F(now.AddHours(-1),14), F(now,20) };
        const string key = "Test\ndemo";
        var rate = UsageInsights.Rate(frames,key,now.AddDays(-1),now);
        check("rate uses observed coverage and samples",rate is {PerHour:5,Samples:3} && rate.From==now.AddHours(-2));
        check("hour rate excludes older sample",UsageInsights.Rate(frames,key,now.AddHours(-1),now)?.PerHour==6);
        check("missing account does not invent rate",UsageInsights.Rate(frames,"missing",now.AddDays(-1),now)==null);
        check("account change suppresses forecast",UsageInsights.Rate(new[]{frames[0],F(now,20,"other")},key,now.AddDays(-1),now)==null);
        check("downward correction suppresses forecast",UsageInsights.Rate(new[]{frames[0],F(now.AddHours(-1),9),frames[2]},key,now.AddDays(-1),now)==null);
        check("one sample not a forecast",UsageInsights.Rate(new[]{frames[2]},key,now.AddDays(-1),now)==null);
        check("short interval not a forecast",UsageInsights.Rate(new[]{F(now.AddSeconds(-30),10),frames[2]},key,now.AddDays(-1),now)==null);
        check("future sample excluded",UsageInsights.Rate(new[]{frames[2],F(now.AddHours(1),30)},key,now.AddDays(-1),now)==null);
        var boundary = new DateTimeOffset(2026,11,1,0,30,0,TimeSpan.Zero);
        check("UTC reset excludes preceding month",UsageInsights.Rate(new[]{F(boundary.AddHours(-1),200),F(boundary.AddMinutes(-30),1),F(boundary,4)},key,boundary.AddDays(-1),boundary)?.PerHour==6);
        check("depletion calculated from remaining quota",UsageInsights.Depletion(10,rate,now)==now.AddHours(2));
        check("already exhausted is now",UsageInsights.Depletion(0,null,now)==now);
        check("unknown rate means unknown depletion",UsageInsights.Depletion(10,null,now)==null);
        check("zero consumption is not an exhaustion date",UsageInsights.Depletion(10,new(0,now.AddHours(-1),now,2),now)==null);
        check("tiny rate overflow safely suppressed",UsageInsights.Depletion(decimal.MaxValue,new(.000001m,now.AddHours(-1),now,2),now)==null);
        check("very distant exhaustion safely suppressed",UsageInsights.Depletion(decimal.MaxValue,new(1,now.AddHours(-1),now,2),now)==null);
        var interval = UsageInsights.Interval(new MeasurementTask {StartUtc=now.AddHours(-2),EndUtc=now.AddHours(-1)},frames,now);
        check("comparison uses inclusive bounded interval",interval.Delta==4 && interval.Samples==2 && interval.Duration==TimeSpan.FromHours(1) && interval.PeakChange==4);
        var p = new Preferences { ReminderRepeatMinutes=60 };
        var state = new ReminderState();
        check("first reminder due",ReminderPolicy.Due(state,"event",p,now));
        check("cooldown survives restart",!ReminderPolicy.Due(JsonSerializer.Deserialize<ReminderState>(JsonSerializer.Serialize(state))!,"event",p,now.AddMinutes(59)));
        check("hour boundary allows repeat",ReminderPolicy.Due(state,"event",p,now.AddHours(1)));
        check("once event never repeats",!ReminderPolicy.Due(state,"event",p,now.AddDays(2),true));
        p.NotificationsSilent=true;
        check("silent does not consume future reminder",!ReminderPolicy.Due(state,"muted",p,now) && !state.Sent.ContainsKey("muted"));
        p.NotificationsSilent=false;p.QuietHoursEnabled=true;p.QuietStartHour=22;p.QuietEndHour=8;
        var local = new DateTimeOffset(2026,10,21,23,0,0,TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026,10,21,23,0,0)));
        check("overnight quiet hours suppress",ReminderPolicy.Muted(p,local));
        check("quiet end boundary resumes",!ReminderPolicy.Muted(p,local.AddHours(9)));
        p.QuietStartHour=9;p.QuietEndHour=17;
        check("daytime quiet hours suppress",ReminderPolicy.Muted(p,local.AddHours(-11)));
        check("outside daytime quiet hours resumes",!ReminderPolicy.Muted(p,local));
        p.QuietStartHour=p.QuietEndHour=0;
        check("equal quiet boundaries mute all day",ReminderPolicy.Muted(p,local));
        p.QuietHoursEnabled=false;p.ReminderRepeatMinutes=0;
        check("single notification preference persists",!ReminderPolicy.Due(state,"event",p,now.AddDays(30)));
        p.QuietStartHour=-1;p.QuietEndHour=99;p.ResetReminderHours=0;p.SamplingReminderMinutes=500;p.ReminderRepeatMinutes=7;
        SettingsPolicy.Normalize(p);
        check("reminder settings normalize safely",p.QuietStartHour==0 && p.QuietEndHour==23 && p.ResetReminderHours==1 && p.SamplingReminderMinutes==120 && p.ReminderRepeatMinutes==60);
    }
}
