using System.Collections.ObjectModel;
using TabNest.Core.Interfaces;
using TabNest.Core.Services;

namespace TabNest.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly TabManagerService _tabManager = new();
    private string _title = "TabNest";

    public MainViewModel(IFileSystemService fileSystemService, IFileLauncher fileLauncher)
    {
        Folder = new FolderViewModel(fileSystemService, fileLauncher);
        // フォルダ移動のたびにアクティブタブの Path とタイトルを移動先に更新する
        Folder.Navigated += (_, path) => UpdateActiveTabLocation(path);
        InitializeDefaultTabs();
    }

    /// <summary>ウィンドウタイトル。初期値は "TabNest"。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>表示中フォルダのファイル一覧 ViewModel。</summary>
    public FolderViewModel Folder { get; }

    /// <summary>タブグループ(表示順)。</summary>
    public ObservableCollection<TabGroupViewModel> Groups { get; } = [];

    /// <summary>初期タブを選択して初期表示フォルダ(%UserProfile%)を読み込む。</summary>
    public bool LoadInitialFolder()
        => Groups.Count > 0 && Groups[0].Tabs.Count > 0 && SelectTab(Groups[0].Tabs[0]);

    private static string UserProfilePath
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// タブを選択してアクティブにし、そのタブの履歴を接続してフォルダ内容を表示する。
    /// タブ切替は履歴に記録しない(各タブの履歴は独立)。
    /// フォルダの読み込みに失敗してもタブ選択は維持される(エラーは Folder.ErrorMessage に表示)。
    /// </summary>
    public bool SelectTab(FolderTabViewModel tab)
    {
        if (!_tabManager.SetActiveTab(tab.Id))
        {
            return false;
        }

        ApplyActiveStates();
        Folder.AttachHistory(tab.History);
        Folder.ShowFolder(tab.Path);
        return true;
    }

    /// <summary>
    /// タブを閉じる(ホイールクリック)。アクティブタブを閉じた場合は
    /// 新しいアクティブタブのフォルダ内容を表示する。
    /// </summary>
    public bool CloseTab(FolderTabViewModel tab)
    {
        var wasActive = tab.IsActive;
        if (!_tabManager.CloseTab(tab.Id))
        {
            return false;
        }

        var groupVm = Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        groupVm?.Tabs.Remove(tab);
        ApplyActiveStates();

        if (wasActive && _tabManager.ActiveTabId is string newActiveId
            && FindTabViewModel(newActiveId) is { } newActiveVm)
        {
            Folder.AttachHistory(newActiveVm.History);
            Folder.ShowFolder(newActiveVm.Path);
        }

        return true;
    }

    /// <summary>グループ名のインライン編集中かどうか(編集中はショートカットを無効にする)。</summary>
    public bool IsRenameInProgress => Groups.Any(g => g.IsEditingName);

    /// <summary>
    /// 最後に閉じたタブを復元する(Ctrl+Shift+T)。
    /// グループ名編集中は何も実行しない(編集状態を維持する)。
    /// 復元したタブはアクティブになり、そのフォルダ内容を表示する。
    /// </summary>
    public bool RestoreClosedTab()
    {
        if (IsRenameInProgress)
        {
            return false;
        }

        var result = _tabManager.RestoreClosedTab();
        if (!result.IsSuccess)
        {
            return false;
        }

        var tab = result.Value!;
        var group = _tabManager.Groups.First(g => g.Tabs.Contains(tab));
        var groupVm = Groups.FirstOrDefault(g => g.Id == group.Id);
        FolderTabViewModel? tabVm = null;
        if (groupVm is not null)
        {
            var index = group.Tabs.IndexOf(tab);
            tabVm = new FolderTabViewModel(tab);
            groupVm.Tabs.Insert(index, tabVm);
        }

        ApplyActiveStates();
        if (tabVm is not null)
        {
            // 復元したタブの履歴(新規)を接続して内容を表示する
            Folder.AttachHistory(tabVm.History);
            Folder.ShowFolder(tabVm.Path);
        }

        return true;
    }

    /// <summary>
    /// 指定グループの末尾に新規タブを追加する(追加されたタブはアクティブになる)。
    /// UI(ボタン・Ctrl+T)への接続は Task 3-10 で行う。
    /// </summary>
    internal FolderTabViewModel? AddTab(string groupId, string path)
    {
        var result = _tabManager.AddTab(groupId, path, GetTabTitle(path));
        if (!result.IsSuccess)
        {
            return null;
        }

        var groupVm = Groups.FirstOrDefault(g => g.Id == groupId);
        if (groupVm is null)
        {
            return null;
        }

        var tabVm = new FolderTabViewModel(result.Value!);
        groupVm.Tabs.Add(tabVm);
        ApplyActiveStates();
        // 追加されたタブはアクティブになるため、履歴を接続して内容を表示する
        Folder.AttachHistory(tabVm.History);
        Folder.ShowFolder(tabVm.Path);
        return tabVm;
    }

    /// <summary>
    /// SPEC「初期起動状態」: グループ「作業1」とタブ1個(%UserProfile%)を作成する。
    /// (Step 4 の永続化実装前は常にこの初期状態で起動する)
    /// </summary>
    private void InitializeDefaultTabs()
    {
        var groupResult = _tabManager.AddGroup("作業1");
        if (!groupResult.IsSuccess)
        {
            return;
        }

        var group = groupResult.Value!;
        _tabManager.AddTab(group.Id, UserProfilePath, GetTabTitle(UserProfilePath));
        Groups.Add(new TabGroupViewModel(group, tab => SelectTab(tab), tab => CloseTab(tab)));
        ApplyActiveStates();
    }

    /// <summary>
    /// TabManagerService のアクティブ状態を各タブ ViewModel の IsActive に反映する(一元管理)。
    /// </summary>
    private void ApplyActiveStates()
    {
        var activeId = _tabManager.ActiveTabId;
        foreach (var tab in Groups.SelectMany(g => g.Tabs))
        {
            tab.IsActive = tab.Id == activeId;
        }
    }

    /// <summary>フォルダ移動に追従してアクティブタブの Path とタイトルを更新する。</summary>
    private void UpdateActiveTabLocation(string path)
    {
        if (_tabManager.ActiveTabId is string activeId && FindTabViewModel(activeId) is { } tabVm)
        {
            tabVm.UpdateLocation(path, GetTabTitle(path));
        }
    }

    private FolderTabViewModel? FindTabViewModel(string tabId)
        => Groups.SelectMany(g => g.Tabs).FirstOrDefault(t => t.Id == tabId);

    /// <summary>
    /// パスからタブタイトルを生成する。通常はフォルダ名、ドライブルートは "C:\" 形式。
    /// </summary>
    public static string GetTabTitle(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return name.Length > 0 ? name : path;
    }
}
