using System.Windows;
using System.Windows.Media;
using System.IO;

namespace Optimizer.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Respect the user's contrast theme without making network or settings changes.
        if (SystemParameters.HighContrast)
        {
            Resources["CanvasBrush"] = SystemColors.WindowBrush;
            Resources["SurfaceBrush"] = SystemColors.WindowBrush;
            Resources["InkBrush"] = SystemColors.WindowTextBrush;
            Resources["MutedBrush"] = SystemColors.WindowTextBrush;
            Resources["LineBrush"] = SystemColors.WindowTextBrush;
            Resources["AccentBrush"] = SystemColors.HighlightBrush;
            Resources["RailBrush"] = SystemColors.WindowBrush;
            Resources["RailInkBrush"] = SystemColors.WindowTextBrush;
            Resources["TintBrush"] = SystemColors.WindowBrush;
        }
        try
        {
            var window = new MainWindow(e.Args);
            MainWindow = window;
            window.Show();
        }
        catch (Exception error)
        {
            var smokeIndex = Array.IndexOf(e.Args, "--smoke-ui");
            if (smokeIndex >= 0 && smokeIndex + 1 < e.Args.Length)
            {
                var failurePath = Path.GetFullPath(e.Args[smokeIndex + 1]) + ".error.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(failurePath)!);
                File.WriteAllText(failurePath, error.ToString());
            }
            else MessageBox.Show(error.Message, "Windows Workbench could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
