using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>Task 5-3: アプリ起動と起動時のセッション復元(ウィンドウサイズ)の UI テスト。</summary>
public class LaunchTests
{
    [UiFact]
    [Trait("Category", "UITest")]
    public void Launch_App_Should_Show_MainWindow()
    {
        using var settings = new SettingsFileScope();
        settings.DeleteSettings(); // 初期起動状態(SPEC「主要機能 > 初期起動状態」)で起動させる

        using var session = new AppSession();

        // MainWindow の検出(タイトルで識別)
        Assert.Equal("TabNest", session.Driver.Title);

        // 初期表示の検証: グループ「作業1」とタブ1個(%UserProfile%)
        var groupName = session.Driver.FindElementByAccessibilityId("GroupNameText");
        Assert.Equal("作業1", groupName.Text);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tab = session.Driver.FindElementByAccessibilityId("FolderTabItem");
        Assert.Equal(Path.GetFileName(userProfile), tab.Text);

        var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
        Assert.Equal(userProfile, addressBar.Text);
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void Launch_App_Should_Restore_Window_Size()
    {
        using var settings = new SettingsFileScope();
        // テスト用 settings.json を配置する(既定と異なる識別しやすいサイズ。単位は物理px)
        settings.WriteSettings("""
            {
              "TabGroups": [],
              "ClosedTabs": [],
              "SavedGroups": [],
              "WindowWidth": 1000,
              "WindowHeight": 650,
              "LeftPaneWidth": 220
            }
            """);

        using var session = new AppSession();

        // GetWindowRect は AppWindow.Resize と同じ物理ピクセルの外接矩形を返す
        // (WinAppDriver の Window.Size は見えないリサイズ枠を除くため使わない)
        Assert.True(NativeMethods.GetWindowRect(session.MainWindowHandle, out var rect));
        Assert.Equal(1000, rect.Width);
        Assert.Equal(650, rect.Height);
    }
}
