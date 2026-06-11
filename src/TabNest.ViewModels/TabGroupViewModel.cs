using System.Collections.ObjectModel;
using TabNest.Core.Models;

namespace TabNest.ViewModels;

/// <summary>
/// タブグループ(1段)を表す ViewModel。グループ名のインライン編集(リネーム)を担う。
/// </summary>
public sealed class TabGroupViewModel : ViewModelBase
{
    private readonly TabGroup _model;
    private string _name;
    private string _editingName = "";
    private bool _isEditingName;

    public TabGroupViewModel(TabGroup model)
    {
        _model = model;
        _name = model.Name;
        Tabs = new ObservableCollection<FolderTabViewModel>(
            model.Tabs.Select(t => new FolderTabViewModel(t)));
        BeginRenameCommand = new RelayCommand(_ => BeginRename());
        CommitRenameCommand = new RelayCommand(_ => CommitRename());
        CancelRenameCommand = new RelayCommand(_ => CancelRename());
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
