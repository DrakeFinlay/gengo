using System.Windows;
namespace Kotoba.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message, "Gengo", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
        base.OnStartup(e);
        if (MainWindow is null) { MainWindow = new MainWindow(); MainWindow.Show(); }
    }
}
