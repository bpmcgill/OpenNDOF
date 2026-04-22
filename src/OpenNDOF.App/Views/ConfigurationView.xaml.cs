using OpenNDOF.App.ViewModels;
using System.Windows.Controls;

namespace OpenNDOF.App.Views;

public partial class ConfigurationView : UserControl
{
    public ConfigurationView(ConfigurationViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
