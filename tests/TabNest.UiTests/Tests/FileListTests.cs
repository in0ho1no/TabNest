using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>
/// Task 5-6: ファイル一覧(区別表示・ツリー連動・タイトル更新・ソート・列幅自動調整・
/// ダブルクォートパス)の UI テスト。ナビゲーションは TestFixtures/SampleFolder
/// (SubFolder/・sample.txt・zebra.txt)を使用する。
/// </summary>
public class FileListTests
{
    private static (SettingsFileScope Settings, AppSession Session) LaunchAtInitialState()
    {
        var settings = new SettingsFileScope();
        settings.DeleteSettings();
        return (settings, new AppSession());
    }

    /// <summary>ファイル一覧の名前列の表示文字列を行順で取得する。</summary>
    private static IReadOnlyList<string> FileListNames(AppSession session)
    {
        var fileList = session.Driver.FindElementByAccessibilityId("FileListView");
        return fileList.FindElementsByClassName("ListViewItem")
            .Select(row => row.FindElementsByClassName("TextBlock").First().Text)
            .ToList();
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void FileList_Should_Distinguish_Files_And_Folders()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);

            // 種別列でフォルダ(「フォルダ」)とファイル(「TXT ファイル」)が区別表示される
            var fileList = session.Driver.FindElementByAccessibilityId("FileListView");
            Assert.Single(fileList.FindElementsByName("フォルダ"));
            Assert.Equal(2, fileList.FindElementsByName("TXT ファイル").Count);
            // フォルダ先頭・名前昇順(初期ソート)
            Assert.Equal(["SubFolder", "sample.txt", "zebra.txt"], FileListNames(session));
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void FolderTree_Select_Should_Navigate_Active_Tab()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // フィクスチャのあるドライブのルートをツリーから選択する(読み取りのみ)
            var driveRoot = Path.GetPathRoot(UiTestEnvironment.SampleFolderPath)!;
            var tree = session.Driver.FindElementByAccessibilityId("FolderTreeView");
            var node = tree.FindElementByName(driveRoot);

            node.Click();

            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
            Assert.True(
                UiActions.WaitUntil(() => addressBar.Text == driveRoot),
                $"ツリー選択でアクティブタブが移動しませんでした(アドレスバー: {addressBar.Text})。");
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void Navigate_Should_Update_Tab_Title()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            var userProfileName = Path.GetFileName(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            Assert.Equal(userProfileName, UiActions.FindTabs(session)[0].Text);

            UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);

            Assert.Equal("SampleFolder", UiActions.FindTabs(session)[0].Text);
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void FileList_HeaderClick_Should_Toggle_Sort_Direction()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);
            Assert.Equal(["SubFolder", "sample.txt", "zebra.txt"], FileListNames(session));

            // 「名前」ヘッダークリックで降順(フォルダ先頭は維持)
            session.Driver.FindElementByAccessibilityId("NameColumnHeader").Click();
            Assert.True(
                UiActions.WaitUntil(
                    () => FileListNames(session) is ["SubFolder", "zebra.txt", "sample.txt"]),
                $"降順に切り替わりませんでした(一覧: {string.Join(", ", FileListNames(session))})。");

            // もう一度クリックで昇順に戻る
            session.Driver.FindElementByAccessibilityId("NameColumnHeader").Click();
            Assert.True(
                UiActions.WaitUntil(
                    () => FileListNames(session) is ["SubFolder", "sample.txt", "zebra.txt"]),
                $"昇順に戻りませんでした(一覧: {string.Join(", ", FileListNames(session))})。");
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void FileList_ColumnDividerDoubleClick_Should_AutoFit_Within_Window()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            UiActions.NavigateTo(session, UiTestEnvironment.SampleFolderPath);
            var header = session.Driver.FindElementByAccessibilityId("NameColumnHeader");
            var initialWidth = header.Size.Width;

            var separator = session.Driver.FindElementByAccessibilityId("NameColumnSeparator");
            UiActions.DoubleClick(session, separator);

            // 文字列数に応じて列幅が変わり、最小 40px 以上かつウィンドウ内に収まる
            Assert.True(
                UiActions.WaitUntil(() => header.Size.Width != initialWidth),
                $"列幅が変化しませんでした(幅: {header.Size.Width})。");
            var newWidth = header.Size.Width;
            Assert.True(newWidth >= 40, $"列幅が最小 40px を下回りました({newWidth}px)。");

            var visible = NativeMethods.GetVisibleWindowRect(session.MainWindowHandle);
            var headerRightEdge = header.Location.X + newWidth;
            Assert.True(
                headerRightEdge <= visible.Width,
                $"列幅がウィンドウ内に収まっていません(右端: {headerRightEdge}px、ウィンドウ幅: {visible.Width}px)。");
        }
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void PathTextBox_Should_Navigate_Quoted_Path()
    {
        var (settings, session) = LaunchAtInitialState();
        using (settings)
        using (session)
        {
            // 「"」は JIS 配列で SendKeys が化けるため、クリップボード経由で貼り付ける
            var quotedPath = $"\"{UiTestEnvironment.SampleFolderPath}\"";
            NativeMethods.SetClipboardText(quotedPath);

            var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
            addressBar.Click();
            addressBar.SendKeys(Keys.Control + "av" + Keys.Control); // 全選択して貼り付け
            Assert.True(
                UiActions.WaitUntil(() => addressBar.Text == quotedPath),
                $"貼り付けに失敗しました(アドレスバー: {addressBar.Text})。");
            addressBar.SendKeys(Keys.Enter);

            // 外側のダブルクォートが除去されて移動する(タブタイトルで完了判定)
            Assert.True(
                UiActions.WaitUntil(() => UiActions.FindTabs(session)[0].Text == "SampleFolder"),
                $"引用符付きパスへの移動に失敗しました(タブ: {UiActions.FindTabs(session)[0].Text})。");
            Assert.Equal(UiTestEnvironment.SampleFolderPath, addressBar.Text);
        }
    }
}
