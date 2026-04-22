using OpenNDOF.App.ViewModels;
using System.Windows.Controls;

namespace OpenNDOF.App.Views;

public partial class TestInputView : UserControl
{
    public TestInputView(TestInputViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
