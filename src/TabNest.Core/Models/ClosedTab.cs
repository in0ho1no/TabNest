namespace TabNest.Core.Models;

/// <summary>
/// 閉じたタブの履歴1件。settings.json 永続化用 DTO(SPEC「データモデル」準拠)。
/// </summary>
public sealed class ClosedTab
{
    public string Path { get; set; } = "";

    public string Title { get; set; } = "";

    /// <summary>閉じた時点で所属していたグループの Id(復元先の優先候補)。</summary>
    public string GroupId { get; set; } = "";

    /// <summary>閉じた時点でのグループ内位置。</summary>
    public int TabIndex { get; set; }

    public DateTime ClosedAt { get; set; }
}
