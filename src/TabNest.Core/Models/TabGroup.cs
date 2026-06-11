namespace TabNest.Core.Models;

/// <summary>
/// タブグループ(1段)。settings.json 永続化用 DTO(SPEC「データモデル」準拠)。
/// </summary>
public sealed class TabGroup
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public List<FolderTab> Tabs { get; set; } = new();

    public string? SelectedTabId { get; set; }
}
