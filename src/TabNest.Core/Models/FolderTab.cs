namespace TabNest.Core.Models;

/// <summary>
/// 1つのタブ。settings.json 永続化用 DTO(SPEC「データモデル」準拠)。
/// 戻る・進む履歴は ViewModel 層が保持し、本クラスには含めない。
/// </summary>
public sealed class FolderTab
{
    public string Id { get; set; } = "";

    /// <summary>「現在表示中」のパス。フォルダ移動のたびに更新される。</summary>
    public string Path { get; set; } = "";

    /// <summary>現在表示中のフォルダ名。Path 更新と同時に更新される。</summary>
    public string Title { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}
