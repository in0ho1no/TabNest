namespace TabNest.Core.Models;

/// <summary>
/// お気に入り(保存済みタブグループ)。settings.json 永続化用 DTO(SPEC「データモデル」準拠)。
/// 選択タブは保持しない(お気に入りから開いた段は常に先頭タブをアクティブにするため、
/// SelectedTabId 等のプロパティを追加しないこと)。
/// 同名判定のキーは Name であり、Id は将来のリネーム機能用の内部識別子。
/// </summary>
public sealed class SavedTabGroup
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>タブのフォルダパス一覧(並び順含む)。保存時のスナップショット。</summary>
    public List<string> Paths { get; set; } = new();

    public DateTime SavedAt { get; set; }
}
