using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TabNest.ViewModels;
using Windows.System;

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

    /// <summary>x:Bind 用: 文字列が空でなければ true。</summary>
    public static bool HasText(string? value) => !string.IsNullOrEmpty(value);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is MainViewModel viewModel)
        {
            ViewModel = viewModel;
            Bindings.Update();
        }
    }

    private void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel?.Folder.NavigateToAddressCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is Microsoft.UI.Xaml.FrameworkElement { DataContext: FileItemViewModel item })
        {
            ViewModel?.Folder.OpenItemCommand.Execute(item);
        }
    }
}
