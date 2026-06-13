using TabNest.Core.Models;

namespace TabNest.ViewModels;

/// <summary>
/// お気に入り一覧の1項目を表す ViewModel。SavedTabGroup(永続化用 DTO)をラップする。
/// Task 6-4 でインラインのリネーム編集状態を持つ(実際のリネーム処理は親 ViewModel に委譲し、
/// 同名衝突の連番付与は FavoritesService 側で解決する)。
/// </summary>
public sealed class FavoriteItemViewModel : ViewModelBase
{
    private readonly SavedTabGroup _model;
    private readonly Func<string, bool>? _rename;
    private bool _isEditingName;
    private string _editingName = "";

    public FavoriteItemViewModel(SavedTabGroup model, Func<string, bool>? rename = null)
    {
        _model = model;
        _rename = rename;
    }

    public string Id => _model.Id;

    /// <summary>表示名(お気に入りの名前)。リネーム確定後に <see cref="RefreshName"/> で更新通知する。</summary>
    public string Name => _model.Name;

    /// <summary>タブのフォルダパス一覧(並び順含む)。</summary>
    public IReadOnlyList<string> Paths => _model.Paths;

    public DateTime SavedAt => _model.SavedAt;

    /// <summary>名前のインライン編集中かどうか。</summary>
    public bool IsEditingName
    {
        get => _isEditingName;
        private set => SetProperty(ref _isEditingName, value);
    }

    /// <summary>編集中の入力文字列。</summary>
    public string EditingName
    {
        get => _editingName;
        set => SetProperty(ref _editingName, value);
    }

    /// <summary>インライン編集を開始する(右クリックメニュー「名前の変更」)。</summary>
    public void BeginRename()
    {
        EditingName = Name;
        IsEditingName = true;
    }

    /// <summary>
    /// 編集を確定する(Enter / フォーカス喪失)。実際のリネーム(トリム・同名衝突解決)は
    /// 親 ViewModel のコールバックに委譲する。空白のみの場合は親側で無視され元の名前を維持する。
    /// </summary>
    public void CommitRename()
    {
        if (!IsEditingName)
        {
            return;
        }

        _rename?.Invoke(EditingName);
        IsEditingName = false;
    }

    /// <summary>編集をキャンセルする(Esc)。元の名前を維持する。</summary>
    public void CancelRename()
    {
        IsEditingName = false;
    }

    /// <summary>モデルの名前変更後に表示名の更新通知を送る(リネーム確定時に親 ViewModel が呼ぶ)。</summary>
    public void RefreshName() => OnPropertyChanged(nameof(Name));
}
