using OpenNDOF.App.ViewModels;
using OpenNDOF.App.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace OpenNDOF.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(
        MainViewModel      vm,
        INavigationService navigationService,
        ISnackbarService   snackbarService)
    {
        InitializeComponent();
        DataContext = vm;

        // Wire WPF-UI services to the named controls in XAML
        navigationService.SetNavigationControl(RootNavigation);
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        // Allow navigation service to resolve pages from the DI container
        RootNavigation.SetServiceProvider(App.Services);

        // Navigate to Dashboard on startup
        Loaded += (_, _) => navigationService.Navigate(typeof(DashboardView));
    }
}
