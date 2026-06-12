using Microsoft.UI.Xaml;
using TabNest.Core.Models;
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
    private readonly SettingsService _settingsService = new();

    public MainViewModel ViewModel { get; } = new(new FileSystemService(), new ShellFileLauncher());

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

        // アプリ終了時に現在のセッション状態を settings.json に保存する(SPEC「設定保存」)
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // 保存単位: WindowWidth/Height は物理ピクセル(AppWindow.Size)、
        // LeftPaneWidth は DIP(ActualWidth)。復元(Task 4-3)も同じ単位で行うこと
        // (ウィンドウは AppWindow.Resize、左カラムは ColumnDefinition.Width)。
        // 左カラム幅はレイアウト未確定などで取得できない場合があるため、既定値にフォールバックする
        var leftPaneWidth = (RootFrame.Content as MainPage)?.LeftPaneWidth ?? 0;
        var settings = ViewModel.CreateAppSettings(
            AppWindow.Size.Width,
            AppWindow.Size.Height,
            leftPaneWidth > 0 ? leftPaneWidth : new AppSettings().LeftPaneWidth);
        _settingsService.Save(settings);
    }

    private void RestoreClosedTabAccelerator_Invoked(
        Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
        Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        // グループ名編集中は VM 側で no-op になる(編集状態を維持)
        ViewModel.RestoreClosedTab();
        args.Handled = true;
    }

    private void AddTabAccelerator_Invoked(
        Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
        Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.AddTabToActiveGroup();
        args.Handled = true;
    }

    private void AddGroupAccelerator_Invoked(
        Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
        Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.AddGroupWithDefaultTab();
        args.Handled = true;
    }
}
