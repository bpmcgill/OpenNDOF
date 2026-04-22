using Microsoft.Extensions.DependencyInjection;
using OpenNDOF.App.ViewModels;
using OpenNDOF.Core.Devices;
using OpenNDOF.Core.Profiles;
using OpenNDOF.HID;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace OpenNDOF.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        Services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.GetService<SpaceDevice>()?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => HidController.Instance);
        services.AddSingleton<ProfileManager>();
        services.AddSingleton<SpaceDevice>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<INavigationViewPageProvider, ServiceProviderNavigationViewPageProvider>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ConfigurationViewModel>();
        services.AddTransient<TestInputViewModel>();
        services.AddTransient<LcdViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<Views.DashboardView>();
        services.AddTransient<Views.ConfigurationView>();
        services.AddTransient<Views.TestInputView>();
        services.AddTransient<Views.LcdView>();
        services.AddTransient<Views.AboutView>();
    }
}

