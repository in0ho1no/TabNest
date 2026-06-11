using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TabNest.ViewModels;

namespace TabNest.App;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel? ViewModel { get; private set; }

    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is MainViewModel viewModel)
        {
            ViewModel = viewModel;
            Bindings.Update();
        }
    }
}
