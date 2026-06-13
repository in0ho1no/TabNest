using System.Collections.ObjectModel;
using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.ViewModels;

/// <summary>
/// タブグループ(1段)を表す ViewModel。グループ名のインライン編集(リネーム)を担う。
/// </summary>
public sealed class TabGroupViewModel : ViewModelBase
{
    private readonly TabGroup _model;
    private readonly Action<FolderTabViewModel>? _selectTab;
    private readonly Action<FolderTabViewModel>? _closeTab;
    private readonly Action<FolderTabViewModel>? _duplicateTab;
    private readonly Action? _saveAsFavorite;
    private readonly Action? _removeGroup;
    private readonly Action<IReadOnlyList<string>>? _reorderTabs;
    private readonly Func<FolderTabViewModel, int, bool>? _moveTabIntoGroup;
    private string _name;
    private string _editingName = "";
    private bool _isEditingName;

    public TabGroupViewModel(
        TabGroup model,
        Action<FolderTabViewModel>? selectTab = null,
        Action<FolderTabViewModel>? closeTab = null,
        Action? saveAsFavorite = null,
        Action? removeGroup = null,
        Action<FolderTabViewModel>? duplicateTab = null,
        Action<IReadOnlyList<string>>? reorderTabs = null,
        Func<FolderTabViewModel, int, bool>? moveTabIntoGroup = null)
    {
        _model = model;
        _selectTab = selectTab;
        _closeTab = closeTab;
        _saveAsFavorite = saveAsFavorite;
        _removeGroup = removeGroup;
        _duplicateTab = duplicateTab;
        _reorderTabs = reorderTabs;
        _moveTabIntoGroup = moveTabIntoGroup;
        _name = model.Name;
        Tabs = new ObservableCollection<FolderTabViewModel>(
            model.Tabs.Select(t => new FolderTabViewModel(t)));
        BeginRenameCommand = new RelayCommand(_ => BeginRename());
        CommitRenameCommand = new RelayCommand(_ => CommitRename());
        CancelRenameCommand = new RelayCommand(_ => CancelRename());
    }

    /// <summary>タブを左クリックで選択する(親 ViewModel に委譲)。</summary>
    public void SelectTab(FolderTabViewModel tab) => _selectTab?.Invoke(tab);

    /// <summary>タブを中クリックで閉じる(親 ViewModel に委譲)。</summary>
    public void CloseTab(FolderTabViewModel tab) => _closeTab?.Invoke(tab);

    /// <summary>タブを複製する(タブの右クリックメニュー。親 ViewModel に委譲)。</summary>
    public void DuplicateTab(FolderTabViewModel tab) => _duplicateTab?.Invoke(tab);

    /// <summary>このグループをお気に入りに保存する(グループ名の右クリックメニュー。親 ViewModel に委譲)。</summary>
    public void SaveAsFavorite() => _saveAsFavorite?.Invoke();

    /// <summary>このグループを削除する(グループ名の右クリックメニュー。親 ViewModel に委譲)。</summary>
    public void RemoveGroup() => _removeGroup?.Invoke();

    /// <summary>このグループがタブを1個以上持つか(削除時の確認ダイアログ要否の判定に使う)。</summary>
    public bool HasTabs => Tabs.Count > 0;

    /// <summary>
    /// グループ内 D&amp;D でタブを並べ替える(Task 7-1)。<paramref name="insertIndex"/> は
    /// 移動前のリスト座標における挿入位置(その位置にあるタブの直前へ挿入する。Count なら末尾)。
    /// 表示順(Tabs)とモデル(TabGroup.Tabs)の双方を同じ順序へ更新する。
    /// タブの同一性は変わらないためアクティブタブ・選択状態は保持される。
    /// 並べ替えが発生した場合は true、対象外・位置変化なしの場合は false を返す。
    /// </summary>
    public bool MoveTab(FolderTabViewModel source, int insertIndex)
    {
        ClearDropIndicators();

        var oldIndex = Tabs.IndexOf(source);
        if (oldIndex < 0)
        {
            return false;
        }

        // 挿入位置を移動前座標で受け取り、source 自身を取り除いた後の座標へ補正する
        var newIndex = insertIndex > oldIndex ? insertIndex - 1 : insertIndex;
        newIndex = Math.Clamp(newIndex, 0, Tabs.Count - 1);
        if (newIndex == oldIndex)
        {
            return false;
        }

        Tabs.Move(oldIndex, newIndex);
        _reorderTabs?.Invoke(Tabs.Select(t => t.Id).ToList());
        return true;
    }

    /// <summary>このグループのタブ数が上限(20)に達しているか(グループ間 D&amp;D の受け入れ判定。Task 7-2)。</summary>
    public bool IsTabLimitReached => Tabs.Count >= TabManagerService.MaxTabsPerGroup;

    /// <summary>
    /// 別グループのタブを、このグループの <paramref name="insertIndex"/> の位置へ移動して受け入れる
    /// (グループ間 D&amp;D。Task 7-2)。実際のモデル更新と表示順同期は親 ViewModel に委譲する。
    /// 受け入れに成功した場合は true、上限到達などで移動しなかった場合は false を返す。
    /// </summary>
    public bool MoveTabFromOtherGroup(FolderTabViewModel source, int insertIndex)
    {
        ClearDropIndicators();
        return _moveTabIntoGroup?.Invoke(source, insertIndex) ?? false;
    }

    /// <summary>
    /// グループ内 D&amp;D 中の挿入位置インジケータを設定する(Task 7-1)。
    /// 指定タブの直前(<paramref name="after"/> が false)または直後(true)に1か所だけ表示し、
    /// 他のタブのインジケータはすべて消す。<paramref name="target"/> が null なら全消去する。
    /// </summary>
    public void SetDropIndicator(FolderTabViewModel? target, bool after)
    {
        foreach (var tab in Tabs)
        {
            tab.IsDropBefore = !after && ReferenceEquals(tab, target);
            tab.IsDropAfter = after && ReferenceEquals(tab, target);
        }
    }

    /// <summary>グループ内 D&amp;D 終了時に挿入位置インジケータをすべて消す(Task 7-1)。</summary>
    public void ClearDropIndicators()
    {
        foreach (var tab in Tabs)
        {
            tab.IsDropBefore = false;
            tab.IsDropAfter = false;
        }
    }

    public string Id => _model.Id;

    /// <summary>グループ名。リネーム確定時に TabGroup.Name と同期する。</summary>
    public string Name
    {
        get => _name;
        private set
        {
            if (SetProperty(ref _name, value))
            {
                _model.Name = value;
            }
        }
    }

    /// <summary>このグループのタブ(横並び表示順)。</summary>
    public ObservableCollection<FolderTabViewModel> Tabs { get; }

    /// <summary>グループ名のインライン編集中かどうか。</summary>
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

    public RelayCommand BeginRenameCommand { get; }

    public RelayCommand CommitRenameCommand { get; }

    public RelayCommand CancelRenameCommand { get; }

    /// <summary>インライン編集を開始する(ダブルクリック)。</summary>
    public void BeginRename()
    {
        EditingName = Name;
        IsEditingName = true;
    }

    /// <summary>
    /// 編集を確定する(Enter / フォーカス喪失)。空白のみの場合は元の名前を維持する。
    /// </summary>
    public void CommitRename()
    {
        if (!IsEditingName)
        {
            return;
        }

        var newName = EditingName.Trim();
        if (newName.Length > 0)
        {
            Name = newName;
        }

        IsEditingName = false;
    }

    /// <summary>編集をキャンセルする(Esc)。元の名前を維持する。</summary>
    public void CancelRename()
    {
        IsEditingName = false;
    }
}
