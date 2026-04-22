using OpenNDOF.App.ViewModels;
using System.Windows.Controls;

namespace OpenNDOF.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
