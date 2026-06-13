using OpenQA.Selenium;
using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>
/// Task 5-5: タブグループ操作(追加・複数段表示・グループ内タブ表示・リネーム・お気に入り)の UI テスト。
/// 各テストは settings.json を削除した初期起動状態(「作業1」1段・タブ1個)から開始する。
/// </summary>
public class GroupOperationTests
{
    private static (SettingsFileScope Settings, AppSession Session) LaunchAtInitialState()
    {
        var settings = new SettingsFileScope();
        settings.DeleteSettings();
        return (settings, new AppSession());
    }

    private static IReadOnlyList<string> GroupNames(AppSession session)
        => session.Driver.FindElementsByAccessibilityId("GroupNameText").Select(e => e.Text).ToList();

    [UiFact]
    [Trait("Category", "UITest")]
    public void AddGroup_Should_Create_New_Group_Row()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.WaitForGroupCount(session, 1);

            session.Driver.FindElementByAccessibilityId("AddGroupButton").Click();

            UiActions.WaitForGroupCount(session, 2);
            Assert.Equal(["作業1", "作業2"], GroupNames(session));
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void CtrlG_Should_Add_New_Group_Row()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.WaitForGroupCount(session, 1);

            UiActions.SendShortcut(session, Keys.Control, "g");

            UiActions.WaitForGroupCount(session, 2);
            Assert.Equal(["作業1", "作業2"], GroupNames(session));
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void Group_Should_Display_Tabs_In_One_Row()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // 作業1 にタブを1個追加(計2個)してから 作業2 を追加(タブ1個)する
            UiActions.SendShortcut(session, Keys.Control, "t");
            UiActions.WaitForTabCount(session, 2);
            UiActions.SendShortcut(session, Keys.Control, "g");
            UiActions.WaitForGroupCount(session, 2);
            UiActions.WaitForTabCount(session, 3);

            // 各段に属するタブ数を段スコープの検索で検証する(グループごとにタブが表示される)
            var rows = UiActions.FindGroupRows(session);
            Assert.Equal(2, rows[0].FindElementsByAccessibilityId("FolderTabItem").Count);
            Assert.Single(rows[1].FindElementsByAccessibilityId("FolderTabItem"));
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void Group_Rename_Should_Update_Name()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            var groupName = session.Driver.FindElementByAccessibilityId("GroupNameText");
            Assert.Equal("作業1", groupName.Text);

            // ダブルクリックでインライン編集を開始し、新しい名前(JIS 配列の影響を受けない ASCII)を入力する
            UiActions.DoubleClick(session, groupName);
            var editBox = session.Driver.FindElementByAccessibilityId("GroupNameEditBox");
            // 編集ボックスの可視化はディスパッチャ経由の非同期のため、表示を明示的に待つ
            Assert.True(
                UiActions.WaitUntil(() => editBox.Displayed),
                "グループ名の編集ボックスが表示されませんでした。");
            editBox.SendKeys(Keys.Control + "a" + Keys.Control);
            editBox.SendKeys("WorkA");
            editBox.SendKeys(Keys.Enter);

            Assert.True(
                UiActions.WaitUntil(() => GroupNames(session) is ["WorkA"]),
                $"リネームが反映されませんでした(グループ名: {string.Join(", ", GroupNames(session))})。");
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void RemoveGroup_With_Tabs_Should_Confirm_And_Delete()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // 作業2 を追加(削除対象。最後の1グループは削除できないため2段にする)
            UiActions.SendShortcut(session, Keys.Control, "g");
            UiActions.WaitForGroupCount(session, 2);

            // 作業2 のグループ名を右クリックし、2番目の項目「グループを削除」を実行する
            var work2 = session.Driver.FindElementsByAccessibilityId("GroupNameText").ElementAt(1);
            UiActions.InvokeContextMenuItem(session, work2, index: 1);

            // タブを持つグループなので確認ダイアログが出る。「削除」(PrimaryButton)を押す
            Assert.True(
                UiActions.WaitUntil(
                    () => session.Driver.FindElementsByAccessibilityId("RemoveGroupConfirmDialog").Count == 1),
                "削除確認ダイアログが表示されませんでした。");
            session.Driver.FindElementByAccessibilityId("PrimaryButton").Click();

            UiActions.WaitForGroupCount(session, 1);
            Assert.Equal(["作業1"], GroupNames(session));
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void Favorite_Save_And_Open_Should_Restore_Group()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // グループ名の右クリックメニュー(先頭項目=「お気に入りに保存」)で保存する
            var groupName = session.Driver.FindElementByAccessibilityId("GroupNameText");
            UiActions.InvokeFirstContextMenuItem(session, groupName);

            Assert.True(
                UiActions.WaitUntil(
                    () => session.Driver.FindElementsByAccessibilityId("FavoriteItem").Count == 1),
                "お気に入りが保存されませんでした。");

            // お気に入りクリックで新しい段として開く(名前は「作業1」を引き継ぐ)
            session.Driver.FindElementByAccessibilityId("FavoriteItem").Click();

            UiActions.WaitForGroupCount(session, 2);
            Assert.Equal(["作業1", "作業1"], GroupNames(session));
            // 開いた段の先頭タブがアクティブになり %UserProfile% を表示する
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
            Assert.Equal(userProfile, addressBar.Text);
        }
    }
}
