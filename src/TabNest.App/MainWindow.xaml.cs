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

    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        // 起動時に settings.json から前回セッションを復元する(無い・壊れている場合は初期起動状態)
        ViewModel = new MainViewModel(
            new FileSystemService(), new ShellFileLauncher(), _settingsService.Load());

        Title = ViewModel.Title;
        AppTitleBar.Title = ViewModel.Title;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // 前回終了時のウィンドウサイズ(物理px)を復元する。保存値が無ければ既定サイズで起動する
        if (ViewModel.RestoredWindowWidth > 0 && ViewModel.RestoredWindowHeight > 0)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)ViewModel.RestoredWindowWidth,
                (int)ViewModel.RestoredWindowHeight));
        }

        // 初期表示フォルダ(前回のアクティブタブ、初回は %UserProfile%)を読み込んでから遷移する。
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
