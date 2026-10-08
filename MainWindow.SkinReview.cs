using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CopilotPulse;

public partial class MainWindow
{
    private async Task ReviewSkinsAsync(string directory)
    {
        if (!App.Demo) throw new InvalidOperationException("Skin review is an isolated demo.");
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(string name, bool passed) { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
        var oldPhoto = new Preferences { PhotoBackground = true }; SkinTheme.Normalize(oldPhoto);
        Check("legacy image preference migrates to bayi", oldPhoto.SkinId == "bayi");
        var oldPlain = new Preferences { PhotoBackground = false }; SkinTheme.Normalize(oldPlain);
        Check("legacy plain preference migrates to midnight", oldPlain.SkinId == "midnight");
        var invalid = new Preferences { SkinId = "unknown", PhotoDim = double.NaN, CustomFocusX = 5 }; SkinTheme.Normalize(invalid);
        Check("invalid preferences are normalized", invalid.SkinId == "midnight" && invalid.PhotoDim == 0.65 && invalid.CustomFocusX == 1);

        preferences.SkinId = "bayi"; SkinEdited();
        string original = Path.Combine(directory, "custom-source.png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(SkinTheme.CurrentImage!));
        using (var stream = File.Create(original)) encoder.Save(stream);
        string imported = SkinImages.Import(original, Path.Combine(directory, "imported"));
        Check("custom image is an independent managed copy", imported != original && File.Exists(imported));
        Check("same imported image is deduplicated", SkinImages.Import(original, Path.Combine(directory, "imported")) == imported);
        Check("managed image decodes after import", SkinImages.LoadLocal(imported).PixelWidth > 0);
        File.WriteAllText(imported, "damaged-copy");
        Check("reimport repairs a damaged managed copy", SkinImages.Import(original, Path.Combine(directory, "imported")) == imported && SkinImages.LoadLocal(imported).PixelWidth > 0);
        foreach (string extension in new[] { "jpg", "bmp" })
        {
            BitmapEncoder alternative = extension == "jpg" ? new JpegBitmapEncoder() : new BmpBitmapEncoder();
            alternative.Frames.Add(BitmapFrame.Create(SkinTheme.CurrentImage!));
            string input = Path.Combine(directory, "test-image." + extension);
            using (var stream = File.Create(input)) alternative.Save(stream);
            string output = SkinImages.Import(input, Path.Combine(directory, "imported"));
            Check(extension + " import decodes and creates a copy", File.Exists(output) && SkinImages.LoadLocal(output).PixelWidth > 0);
        }
        string bad = Path.Combine(directory, "invalid.png"); File.WriteAllText(bad, "not an image");
        bool rejected = false; try { SkinImages.Import(bad, Path.Combine(directory, "imported")); } catch (Exception e) when (SkinImages.IsImageError(e)) { rejected = true; }
        Check("invalid images rejected before changing preference", rejected && preferences.SkinId == "bayi");
        preferences.CustomBackgroundPath = Path.Combine(directory, "missing.png"); preferences.SkinId = "custom"; SkinEdited();
        Check("missing custom image falls back without crashing", SkinTheme.CurrentImage == null && SkinTheme.ImageError != null);
        preferences.CustomBackgroundPath = imported;
        preferences.PhotoDim = 0.69; preferences.CustomFocusX = 0.56;
        var restored = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences, Store.Json), Store.Json)!;
        SkinTheme.Normalize(restored);
        Check("skin and photo settings survive serialization", restored.SkinId == "custom" && restored.CustomBackgroundPath == imported && restored.PhotoDim == 0.69 && restored.CustomFocusX == 0.56);

        historyWindow = new HistoryWindow(null, demoHistory);
        historyWindow.Show(); await historyWindow.ReloadAsync();
        foreach (var skin in SkinTheme.Catalog)
        {
            preferences.SkinId = skin.Id; SkinEdited();
            await Task.Delay(140);
            Capture(Path.Combine(directory, skin.Id + "-widget.png"));
            CaptureControl(historyWindow, Path.Combine(directory, skin.Id + "-history.png"));
            Check("applied " + skin.Id, SkinTheme.Current.Id == skin.Id);
        }
        preferences.PhotoDim = 0.65;
        preferences.CustomBackgroundPath = "";
        preferences.SkinId = "forest"; SkinEdited();
        await ReviewMenusAsync(Path.Combine(directory, "forest-menus"));
        OpenSkins(this, new RoutedEventArgs());
        await Task.Delay(180);
        CaptureControl(skinWindow!, Path.Combine(directory, "skin-picker.png"));
        skinWindow!.Close(); skinWindow = null;
        preferences.SkinId = "bayi"; SkinEdited();
        OpenSkins(this, new RoutedEventArgs());
        await Task.Delay(180);
        CaptureControl(skinWindow!, Path.Combine(directory, "skin-picker-photo.png"));
        skinWindow!.Close(); skinWindow = null;
        historyWindow.Close(); historyWindow = null;
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks }, Store.Json));
    }
}
