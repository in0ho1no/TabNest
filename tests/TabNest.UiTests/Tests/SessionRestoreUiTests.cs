using System.Text.Json;
using TabNest.Core.Models;
using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>
/// Task 5-7: テスト用 settings.json を配置してアプリを起動し、
/// 前回セッション(タブグループ・アクティブタブ・ウィンドウサイズ)の復元を検証する UI テスト。
/// </summary>
public class SessionRestoreUiTests
{
    /// <summary>
    /// 2グループ・3タブ(WorkA: SampleFolder + SubFolder、WorkB: SampleFolder)の
    /// テスト用セッション。アクティブは WorkA の SubFolder タブ。
    /// </summary>
    private static AppSettings CreateTestSession(double windowWidth, double windowHeight)
    {
        var samplePath = UiTestEnvironment.SampleFolderPath;
        var subFolderPath = Path.Combine(samplePath, "SubFolder");
        return new AppSettings
        {
            TabGroups =
            [
                new TabGroup
                {
                    Id = "g1",
                    Name = "WorkA",
                    SelectedTabId = "t2",
                    Tabs =
                    [
                        new FolderTab { Id = "t1", Path = samplePath, Title = "SampleFolder" },
                        new FolderTab { Id = "t2", Path = subFolderPath, Title = "SubFolder" },
                    ],
                },
                new TabGroup
                {
                    Id = "g2",
                    Name = "WorkB",
                    SelectedTabId = "t3",
                    Tabs = [new FolderTab { Id = "t3", Path = samplePath, Title = "SampleFolder" }],
                },
            ],
            ActiveGroupId = "g1",
            ActiveTabId = "t2",
            WindowWidth = windowWidth,
            WindowHeight = windowHeight,
            LeftPaneWidth = 220,
        };
    }

    private static void WriteTestSession(SettingsFileScope scope, AppSettings session)
        => scope.WriteSettings(JsonSerializer.Serialize(session));

    [UiFact]
    [Trait("Category", "UITest")]
    public void App_Should_Restore_Previous_Session()
    {
        using var settings = new SettingsFileScope();
        WriteTestSession(settings, CreateTestSession(1200, 760));

        using var session = new AppSession();

        // タブグループの復元(2段・名前・段ごとのタブ数)
        UiActions.WaitForGroupCount(session, 2);
        var groupNames = session.Driver.FindElementsByAccessibilityId("GroupNameText")
            .Select(e => e.Text).ToList();
        Assert.Equal(["WorkA", "WorkB"], groupNames);

        var rows = UiActions.FindGroupRows(session);
        Assert.Equal(2, rows[0].FindElementsByAccessibilityId("FolderTabItem").Count);
        Assert.Single(rows[1].FindElementsByAccessibilityId("FolderTabItem"));

        // アクティブタブ(WorkA の SubFolder)の復元: そのフォルダが表示される
        var subFolderPath = Path.Combine(UiTestEnvironment.SampleFolderPath, "SubFolder");
        var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
        Assert.True(
            UiActions.WaitUntil(() => addressBar.Text == subFolderPath),
            $"アクティブタブが復元されませんでした(アドレスバー: {addressBar.Text})。");

        // ファイル一覧もアクティブタブの内容(note.txt)を表示している
        var fileList = session.Driver.FindElementByAccessibilityId("FileListView");
        Assert.Single(fileList.FindElementsByName("note.txt"));
    }

    [UiFact]
    [Trait("Category", "UITest")]
    public void App_Should_Restore_Window_Size()
    {
        using var settings = new SettingsFileScope();
        WriteTestSession(settings, CreateTestSession(1080, 720));

        using var session = new AppSession();

        Assert.True(NativeMethods.GetWindowRect(session.MainWindowHandle, out var rect));
        Assert.Equal(1080, rect.Width);
        Assert.Equal(720, rect.Height);
    }
}
