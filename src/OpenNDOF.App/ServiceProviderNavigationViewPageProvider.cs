using Wpf.Ui.Abstractions;

namespace OpenNDOF.App;

internal sealed class ServiceProviderNavigationViewPageProvider(IServiceProvider serviceProvider)
    : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => serviceProvider.GetService(pageType);
}
