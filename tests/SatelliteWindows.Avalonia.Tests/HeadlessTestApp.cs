using Avalonia;
using Avalonia.Headless;
using SatelliteWindows.Avalonia.Tests;

// Tell Avalonia.Headless.XUnit how to spin up a UI thread for [AvaloniaFact] / [AvaloniaTheory].
[assembly: AvaloniaTestApplication(typeof(HeadlessTestAppBuilder))]

namespace SatelliteWindows.Avalonia.Tests;

public static class HeadlessTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

public sealed class HeadlessTestApp : Application
{
    public override void Initialize() { /* no XAML / styles — tests don't render */ }
}
