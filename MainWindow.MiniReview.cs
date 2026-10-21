using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace CopilotPulse;

public partial class MainWindow
{
    private async Task ReviewMiniAsync(string directory)
    {
        if(!App.Demo)throw new InvalidOperationException("Visual review requires isolated demo mode.");
        Directory.CreateDirectory(directory);
        await Task.Delay(250); // Let initial native SizeToContent sizing settle before testing a mode switch.
        var checks=new List<string>();
        void Check(string name,bool pass){if(!pass)throw new InvalidOperationException(name);checks.Add(name);}
        Check("legacy compact setting preserved",DisplayModes.Normalize(null,true)=="compact");
        Check("legacy full setting preserved",DisplayModes.Normalize(null,false)=="full");
        var oldSettings=JsonSerializer.Deserialize<Preferences>("{\"SkinId\":\"cyber\"}",Store.Json)!;
        Check("existing preferences get normal intensity and speed",oldSettings.EffectIntensity==1&&oldSettings.EffectSpeed==1);
        var invalid=new Preferences {EffectIntensity=double.NaN,EffectSpeed=double.PositiveInfinity};
        SkinTheme.Normalize(invalid);
        Check("nonfinite effects normalize",invalid.EffectIntensity==1&&invalid.EffectSpeed==1);
        invalid.EffectIntensity=-12;invalid.EffectSpeed=50;SkinTheme.Normalize(invalid);
        Check("effect bounds enforced",invalid.EffectIntensity==.35&&invalid.EffectSpeed==1.5);
        preferences.DisplayMode="mini";preferences.ReduceMotion=false;
        var persisted=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences,Store.Json),Store.Json)!;
        Check("mini and motion options serialize",persisted.DisplayMode=="mini"&&!persisted.ReduceMotion);
        ApplyDisplayMode();
        foreach(var skin in SkinTheme.Catalog)
        {
            if(skin.Id=="custom")continue;
            preferences.SkinId=skin.Id;SkinEdited();MiniDisplay.PaintReviewFrame(1.5);await Task.Delay(80);
            Capture(Path.Combine(directory,"mini-"+skin.Id+".png"));
            File.AppendAllText(Path.Combine(directory,"dimensions.txt"),$"{skin.Id}: {ActualWidth} x {ActualHeight}, value={MiniDisplay.Value}, expected={data!.Total}\n");
            Check("mini fits "+skin.Id,Math.Abs(ActualWidth-244)<1&&ActualHeight<=114&&MiniDisplay.Value==data!.Total);
        }
        foreach(string style in new[]{"lcd","terminal","nixie","cyber","eink","radar","mecha","matrix","shell","amber","scope","debug"})
        {
            preferences.SkinId=style;SkinEdited();
            foreach(decimal amount in new[]{0m,0.01m,1234567.89m})
            {
                MiniDisplay.Update(amount,"demo",data!.FetchedAt);
                Capture(Path.Combine(directory,$"amount-{style}-{amount.ToString("0.00",DisplayArt.Us)}.png"));
            }
        }
        foreach(var state in new[]{"login","network","paused"})
        {
            MiniDisplay.Update(data!.Total,state,data.FetchedAt);Capture(Path.Combine(directory,"state-"+state+".png"));
            Check("cached amount retained in "+state,MiniDisplay.Value==data.Total);
        }
        MiniDisplay.Update(null,"connecting",null);Capture(Path.Combine(directory,"state-empty.png"));
        Check("not connected is not a zero amount",MiniDisplay.Value==null);
        foreach (string geek in new[]{"shell","amber","scope","debug","matrix"})
        {
            preferences.SkinId=geek;SkinEdited();
            foreach (string state in new[]{"live","syncing","login","network","paused","connecting"})
            {
                MiniDisplay.Update(state=="connecting"?null:data!.Total,state,state=="connecting"?null:data!.FetchedAt);
                Capture(Path.Combine(directory,$"geek-state-{geek}-{state}.png"));
                if(state=="network")Check("geek cached state explicit "+geek,MiniDisplay.GeekStateText=="CACHED / STALE"&&MiniDisplay.Value==data!.Total);
                if(state=="connecting")Check("geek pending state explicit "+geek,MiniDisplay.GeekStateText=="WAITING"&&MiniDisplay.Value==null);
            }
            MiniDisplay.Update(-12.34m,"demo",null);Capture(Path.Combine(directory,$"geek-negative-{geek}.png"));
        }
        RefreshNumericState();
        preferences.SkinId="cyber";SkinEdited();MiniDisplay.ReviewTime=null;await Task.Delay(100);
        preferences.ReduceMotion=true;SkinEdited();
        Check("reduced motion stops all main effects",!MiniDisplay.AnimationRunning&&!FullAmount.AnimationRunning&&!FullAtmosphere.AnimationRunning);
        preferences.ReduceMotion=false;SkinEdited();Hide();await Task.Delay(80);
        Check("hidden widget has no animation timers",!MiniDisplay.AnimationRunning&&!FullAmount.AnimationRunning&&!FullAtmosphere.AnimationRunning);
        Show();await Task.Delay(80);
        SetDisplayMode("full");await Task.Delay(80);
        Check("full mode can be restored",FullView.IsVisible&&!MiniHost.IsVisible&&ActualWidth==408);
        foreach(string id in new[]{"lcd","cyber","terminal","nixie","eink","radar","mecha","synthwave","warp","plasma","matrix","shell","amber","scope","debug"})
        {
            preferences.SkinId=id;SkinEdited();FullAmount.PaintReviewFrame(1.5);FullAtmosphere.PaintReviewFrame(1.5);await Task.Delay(70);
            Capture(Path.Combine(directory,"full-"+id+".png"));
        }
        preferences.SkinId="lcd";SkinEdited();await ReviewMenusAsync(Path.Combine(directory,"lcd-menus"));
        var historyPreview=new HistoryWindow(null,demoHistory);historyPreview.Show();await historyPreview.ReloadAsync();await Task.Delay(100);
        CaptureControl(historyPreview,Path.Combine(directory,"lcd-history.png"));historyPreview.Close();
        preferences.SkinId="matrix";SkinEdited();
        var matrixHistory=new HistoryWindow(null,demoHistory);matrixHistory.Show();await matrixHistory.ReloadAsync();await Task.Delay(100);
        CaptureControl(matrixHistory,Path.Combine(directory,"matrix-history.png"));matrixHistory.Close();
        SetDisplayMode("mini");preferences.SkinId="matrix";SkinEdited();
        OpenSkins(this,new RoutedEventArgs());await Task.Delay(150);
        skinWindow!.SetReviewEffects(.55,1.25);
        Check("picker sliders update shared effects",preferences.EffectIntensity==.55&&preferences.EffectSpeed==1.25);
        persisted=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences,Store.Json),Store.Json)!;
        Check("effect settings survive serialization",persisted.EffectIntensity==.55&&persisted.EffectSpeed==1.25);
        bool canAnimate=SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
        Check("selected preview animates when allowed",skinWindow.PreviewAnimationRunning==canAnimate);
        preferences.ReduceMotion=true;SkinEdited();skinWindow.RefreshSelection();
        Check("reduced motion stops selected preview",!skinWindow.PreviewAnimationRunning);
        preferences.ReduceMotion=false;SkinEdited();skinWindow.RefreshSelection();
        settingsWindow!.Hide();Check("hidden picker stops preview",!skinWindow.PreviewAnimationRunning);
        settingsWindow!.Show();skinWindow.SetReviewEffects(1,1);skinWindow.PaintPreviewFrame(1.5);
        await Task.Delay(100);CaptureControl(settingsWindow!,Path.Combine(directory,"picker.png"));settingsWindow!.Close();
        await Task.Delay(1200); // Let the numeric update glow settle before comparing static frames.
        string Signature(string name)
        {
            string file=Path.Combine(directory,name+".png");CaptureControl(MiniDisplay,file);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
        }
        foreach(string id in new[]{"radar","mecha","synthwave","warp","plasma","matrix","cyber","shell","amber","scope","debug"})
        {
            preferences.SkinId=id;preferences.EffectSpeed=1;preferences.EffectIntensity=.35;SkinEdited();MiniDisplay.PaintReviewFrame(1.5);
            string soft=Signature("soft-"+id);
            preferences.EffectIntensity=1.5;SkinEdited();
            Check("intensity changes appearance "+id,soft!=Signature("vivid-"+id));
            Check("animation active "+id,MiniDisplay.AnimationRunning==canAnimate);
            preferences.ReduceMotion=true;SkinEdited();MiniDisplay.PaintReviewFrame(1);
            string frozen=Signature("frozen-a-"+id);MiniDisplay.PaintReviewFrame(2);
            Check("reduced motion freezes pixels "+id,frozen==Signature("frozen-b-"+id)&&!MiniDisplay.AnimationRunning);
            preferences.ReduceMotion=false;preferences.EffectIntensity=1;SkinEdited();
        }
        preferences.SkinId="radar";preferences.EffectSpeed=.5;SkinEdited();MiniDisplay.PaintReviewFrame(2);
        string slow=Signature("speed-half");
        preferences.EffectSpeed=1;SkinEdited();MiniDisplay.PaintReviewFrame(1);
        Check("speed scales animation phase",slow==Signature("speed-normal"));
        foreach(string id in new[]{"radar","mecha","synthwave","warp","plasma","matrix","cyber","terminal","aurora","arcade","nixie","shell","amber","scope","debug"})
        {
            string frames=Path.Combine(directory,"animation-"+id);Directory.CreateDirectory(frames);
            preferences.SkinId=id;SkinEdited();
            for(int i=0;i<48;i++) {MiniDisplay.PaintReviewFrame(i*.12);Capture(Path.Combine(frames,$"frame-{i:000}.png"));}
        }
        File.WriteAllText(Path.Combine(directory,"checks.json"),JsonSerializer.Serialize(new {passed=checks.Count,systemAnimationEnabled=SystemParameters.ClientAreaAnimation,checks},Store.Json));
    }
}
