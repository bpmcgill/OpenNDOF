using OpenNDOF.App.ViewModels;
using System.Windows.Controls;

namespace OpenNDOF.App.Views;

public partial class LcdView : UserControl
{
    public LcdView(LcdViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
