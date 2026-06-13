using OpenQA.Selenium;
using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>
/// Task 5-2: 主要 UI 要素を AutomationId で検索できることを検証する。
/// (RestoreClosedTabButton は SPEC 画面レイアウトに存在しないため対象外。
/// MainWindow はウィンドウタイトルで識別する)
/// </summary>
public class ElementDiscoveryTests
{
    /// <summary>SPEC Task 5-2 の対象例のうち、実装済み要素の AutomationId。</summary>
    private static readonly string[] RequiredAutomationIds =
    [
        "AddTabButton",
        "AddGroupButton",
        "BackButton",
        "ForwardButton",
        "UpButton",
        "PathTextBox",
        "FileListView",
        "FolderTreeView",
        "FavoritesListView",
        "TabGroupRow",
        "FolderTabItem",
        "GroupNameText",
    ];

    [UiFact]
    [Trait("Category", "UITest")]
    public void 主要要素をAutomationIdで検索できる()
    {
        using var session = new AppSession();

        // MainWindow: セッションのルートウィンドウとして識別できる(タイトルは "TabNest")
        Assert.Equal("TabNest", session.Driver.Title);

        var missing = new List<string>();
        foreach (var automationId in RequiredAutomationIds)
        {
            try
            {
                session.Driver.FindElementByAccessibilityId(automationId);
            }
            catch (WebDriverException)
            {
                missing.Add(automationId);
            }
        }

        Assert.True(missing.Count == 0, $"AutomationId が見つかりません: {string.Join(", ", missing)}");
    }
}
