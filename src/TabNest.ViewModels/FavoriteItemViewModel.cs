using TabNest.Core.Models;

namespace TabNest.ViewModels;

/// <summary>
/// お気に入り一覧の1項目を表す ViewModel。SavedTabGroup(永続化用 DTO)をラップする。
/// v0.1 ではリネーム・並び替えがないため不変(変更通知なし)。
/// </summary>
public sealed class FavoriteItemViewModel
{
    private readonly SavedTabGroup _model;

    public FavoriteItemViewModel(SavedTabGroup model)
    {
        _model = model;
    }

    public string Id => _model.Id;

    /// <summary>表示名(お気に入りの名前)。</summary>
    public string Name => _model.Name;

    /// <summary>タブのフォルダパス一覧(並び順含む)。</summary>
    public IReadOnlyList<string> Paths => _model.Paths;

    public DateTime SavedAt => _model.SavedAt;
}
