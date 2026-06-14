using OpenQA.Selenium;
using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>
/// Task 6-2: Alt+左/右/上(戻る・進む・上へ)ショートカットの UI テスト。
/// 各テストは settings.json を削除した初期起動状態から開始し、
/// ナビゲーションは TestFixtures/SampleFolder(SubFolder を含む)のみ参照する。
/// </summary>
public class NavigationShortcutUiTests
{
    private static (SettingsFileScope Settings, AppSession Session) LaunchAtInitialState()
    {
        var settings = new SettingsFileScope();
        settings.DeleteSettings();
        return (settings, new AppSession());
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void AltArrows_Should_Navigate_Back_Forward_Up()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.WaitForTabCount(session, 1);
            var subFolderPath = Path.Combine(UiTestEnvironment.SampleFolderPath, "SubFolder");

            // SampleFolder -> SubFolder と移動して戻る履歴を作る
            UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);
            UiActions.NavigateTo(session, subFolderPath);
            Assert.Equal("SubFolder", UiActions.FindTabs(session)[0].Text);

            // タブをクリックしてフォーカスをアドレスバーからタブ(コンテンツ側)へ移す。
            // NavigateTo 直後はアドレスバーの TextBox がフォーカスを保持しており、
            // WinAppDriver の合成 Alt+矢印が編集コントロールに吸われてルートのアクセラレータへ
            // 届かないため(Ctrl+W は影響を受けず発火する。Alt+矢印のみの WinAppDriver 固有事象)。
            UiActions.FindTabs(session)[0].Click();

            // Alt+左: 戻る -> SampleFolder
            UiActions.SendShortcut(session, Keys.Alt, Keys.Left);
            Assert.True(
                UiActions.WaitUntil(() => UiActions.FindTabs(session)[0].Text == "SampleFolder"),
                $"Alt+左で戻れませんでした(タブ: {UiActions.FindTabs(session)[0].Text})。");

            // Alt+右: 進む -> SubFolder
            UiActions.SendShortcut(session, Keys.Alt, Keys.Right);
            Assert.True(
                UiActions.WaitUntil(() => UiActions.FindTabs(session)[0].Text == "SubFolder"),
                $"Alt+右で進めませんでした(タブ: {UiActions.FindTabs(session)[0].Text})。");

            // Alt+上: 上の階層へ -> SampleFolder
            UiActions.SendShortcut(session, Keys.Alt, Keys.Up);
            Assert.True(
                UiActions.WaitUntil(() => UiActions.FindTabs(session)[0].Text == "SampleFolder"),
                $"Alt+上で上の階層へ移動できませんでした(タブ: {UiActions.FindTabs(session)[0].Text})。");
        }
    }
}
