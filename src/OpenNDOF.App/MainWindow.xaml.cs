using OpenNDOF.App.ViewModels;
using OpenNDOF.App.Views;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Forms = System.Windows.Forms;

namespace OpenNDOF.App;

public partial class MainWindow : FluentWindow
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _runInBackgroundMenuItem;
    private bool _exitRequested;

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

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "OpenNDOF Bridge",
            Icon = SystemIcons.Application,
            Visible = false,
        };

        var openMenuItem = new Forms.ToolStripMenuItem("Open", null, (_, _) => RestoreFromTray());
        _runInBackgroundMenuItem = new Forms.ToolStripMenuItem("Run in background")
        {
            CheckOnClick = true,
            Checked = true,
        };
        var exitMenuItem = new Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add(openMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(_runInBackgroundMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();

        StateChanged += OnWindowStateChanged;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;

        // Navigate to Dashboard on startup
        Loaded += (_, _) => navigationService.Navigate(typeof(DashboardView));
    }

    private bool RunInBackground => _runInBackgroundMenuItem.Checked;

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && RunInBackground)
        {
            HideToTray();
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested || !RunInBackground)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _notifyIcon.Visible = true;
    }

    private void RestoreFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            ShowInTaskbar = true;
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
        });
    }

    private void ExitApplication()
    {
        Dispatcher.Invoke(() =>
        {
            _exitRequested = true;
            _notifyIcon.Visible = false;
            Close();
        });
    }
}
