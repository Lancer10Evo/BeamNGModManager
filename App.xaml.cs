using System.Windows;
using BeamNGModManager.Services;

namespace BeamNGModManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--self-test"))
        {
            var (passed, report) = SecurityScannerService.RunSelfTestSuite();
            Console.WriteLine(report);
            Environment.Exit(passed ? 0 : 1);
            return;
        }
        base.OnStartup(e);
    }
}
