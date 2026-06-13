namespace TabNest.Core.Models;

/// <summary>
/// settings.json に保存するアプリ設定(SPEC「データモデル」準拠)。
/// タブごとの戻る・進む履歴はセッション保存の対象外。
/// </summary>
public sealed class AppSettings
{
    public List<TabGroup> TabGroups { get; set; } = new();

    public List<ClosedTab> ClosedTabs { get; set; } = new();

    public List<SavedTabGroup> SavedGroups { get; set; } = new();

    public string? ActiveGroupId { get; set; }

    public string? ActiveTabId { get; set; }

    public double WindowWidth { get; set; }

    public double WindowHeight { get; set; }

    public double LeftPaneWidth { get; set; } = 220;

    /// <summary>左カラムのフォルダツリーを表示するか(既定 true)。Task 6-5 で追加。</summary>
    public bool IsFolderTreeVisible { get; set; } = true;
}
