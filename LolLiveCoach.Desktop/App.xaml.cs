using System.Windows;
using Velopack;

namespace LolLiveCoach.Desktop;

public partial class App : Application
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
