using Microsoft.UI.Xaml;
using TabNest.Core.Services;
using TabNest.ViewModels;

namespace TabNest.App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new(new FileSystemService());

    public MainWindow()
    {
        InitializeComponent();

        Title = ViewModel.Title;
        AppTitleBar.Title = ViewModel.Title;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // 初期表示フォルダ(%UserProfile%)を読み込んでからメインページへ遷移する。
        ViewModel.LoadInitialFolder();
        RootFrame.Navigate(typeof(MainPage), ViewModel);
    }
}
