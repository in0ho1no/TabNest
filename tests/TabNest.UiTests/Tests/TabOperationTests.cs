using OpenQA.Selenium;
using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>
/// Task 5-4: タブ操作(追加・選択・ホイールクリックで閉じる・復元)の UI テスト。
/// 各テストは settings.json を削除した初期起動状態(グループ1段・%UserProfile% タブ1個)から開始し、
/// ナビゲーションは TestFixtures/SampleFolder のみ参照する(サンドボックスポリシー)。
/// </summary>
public class TabOperationTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static (SettingsFileScope Settings, AppSession Session) LaunchAtInitialState()
    {
        var settings = new SettingsFileScope();
        settings.DeleteSettings();
        return (settings, new AppSession());
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void AddTab_Should_Create_New_Tab()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.WaitForTabCount(session, 1);

            session.Driver.FindElementByAccessibilityId("AddTabButton").Click();

            UiActions.WaitForTabCount(session, 2);
            // 追加されたタブは %UserProfile% を表示する
            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
            Assert.Equal(UserProfile, addressBar.Text);
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void CtrlT_Should_Add_Tab_With_UserProfile()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.WaitForTabCount(session, 1);

            UiActions.SendShortcut(session, Keys.Control, "t");

            UiActions.WaitForTabCount(session, 2);
            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
            Assert.Equal(UserProfile, addressBar.Text);
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void Tab_Select_Should_Show_Selected_Tab_Folder()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // 2個目のタブを追加し(アクティブになる)、サンプルフォルダへ移動して2タブを区別する
            UiActions.SendShortcut(session, Keys.Control, "t");
            UiActions.WaitForTabCount(session, 2);
            var samplePath = UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);
            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");

            // 1個目のタブ(%UserProfile%)をクリックで選択する
            UiActions.FindTabs(session)[0].Click();

            Assert.True(
                UiActions.WaitUntil(() => addressBar.Text == UserProfile),
                $"タブ選択でフォルダが切り替わりませんでした(アドレスバー: {addressBar.Text})。");

            // 2個目のタブへ戻るとサンプルフォルダが表示される
            UiActions.FindTabs(session)[1].Click();
            Assert.True(
                UiActions.WaitUntil(() => addressBar.Text == samplePath),
                $"タブ再選択でフォルダが戻りませんでした(アドレスバー: {addressBar.Text})。");
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void MiddleClick_Tab_Should_Close_Tab()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.SendShortcut(session, Keys.Control, "t");
            UiActions.WaitForTabCount(session, 2);

            UiActions.MiddleClick(session, UiActions.FindTabs(session)[1]);

            UiActions.WaitForTabCount(session, 1);
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void MiddleClick_Last_Tab_Of_Group_Should_Close_Group()
    {
        // Task 6-6: グループ内の最後のタブを閉じると、空になったグループも自動的に閉じる
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // 作業2 を追加(タブ1個)。アプリ全体ではタブ2個になる
            UiActions.SendShortcut(session, Keys.Control, "g");
            UiActions.WaitForGroupCount(session, 2);
            UiActions.WaitForTabCount(session, 2);

            // 作業2 段の唯一のタブを中クリックで閉じる → 作業2 が空になり自動クローズされる
            var work2Tab = UiActions.FindGroupRows(session)[1]
                .FindElementsByAccessibilityId("FolderTabItem").First();
            UiActions.MiddleClick(session, work2Tab);

            UiActions.WaitForGroupCount(session, 1);
            UiActions.WaitForTabCount(session, 1);
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void MiddleClick_App_Last_Tab_Should_Be_Rejected()
    {
        // Task 6-6: アプリ内の最後の1タブは閉じられない(常にタブ1個以上・グループ1段以上を保持する)
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.WaitForTabCount(session, 1);

            UiActions.MiddleClick(session, UiActions.FindTabs(session)[0]);

            // 閉じる操作は拒否され、タブ・グループは維持される。
            // 状態が変わらないことを確認するため、一定回数連続でタブ1個を確認する
            for (var i = 0; i < 5; i++)
            {
                Assert.Single(UiActions.FindTabs(session));
                Assert.Single(UiActions.FindGroupRows(session));
            }
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void CtrlShiftT_Should_Restore_Closed_Tab()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // タブを追加してサンプルフォルダへ移動し、閉じてから復元する
            UiActions.SendShortcut(session, Keys.Control, "t");
            UiActions.WaitForTabCount(session, 2);
            var samplePath = UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);
            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");

            UiActions.MiddleClick(session, UiActions.FindTabs(session)[1]);
            UiActions.WaitForTabCount(session, 1);

            UiActions.SendShortcut(session, Keys.Control + Keys.Shift, "t");

            // 復元されたタブはアクティブになり、閉じる前のパスを表示する
            UiActions.WaitForTabCount(session, 2);
            Assert.True(
                UiActions.WaitUntil(() => addressBar.Text == samplePath),
                $"復元タブのフォルダが表示されませんでした(アドレスバー: {addressBar.Text})。");
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void CtrlW_Should_Close_Active_Tab_And_Restore_With_CtrlShiftT()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // タブを追加し(アクティブになる)、サンプルフォルダへ移動する
            UiActions.SendShortcut(session, Keys.Control, "t");
            UiActions.WaitForTabCount(session, 2);
            var samplePath = UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);
            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");

            // Ctrl+W でアクティブタブを閉じる(中クリックと同一経路で ClosedTab 履歴へ積む)
            UiActions.SendShortcut(session, Keys.Control, "w");
            UiActions.WaitForTabCount(session, 1);

            // Ctrl+Shift+T で復元でき、閉じる前のパスが表示される
            UiActions.SendShortcut(session, Keys.Control + Keys.Shift, "t");
            UiActions.WaitForTabCount(session, 2);
            Assert.True(
                UiActions.WaitUntil(() => addressBar.Text == samplePath),
                $"Ctrl+W で閉じたタブが Ctrl+Shift+T で復元されませんでした(アドレスバー: {addressBar.Text})。");
        }
    }
}
