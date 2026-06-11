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

    /// <summary>初期表示フォルダ(%UserProfile%)を読み込む。</summary>
    public bool LoadInitialFolder()
        => Folder.LoadFolder(UserProfilePath);

    private static string UserProfilePath
        => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// タブを選択してアクティブにし、そのタブのフォルダ内容を表示する。
    /// フォルダの読み込みに失敗してもタブ選択は維持される(エラーは Folder.ErrorMessage に表示)。
    /// </summary>
    public bool SelectTab(FolderTabViewModel tab)
    {
        if (!_tabManager.SetActiveTab(tab.Id))
        {
            return false;
        }

        ApplyActiveStates();
        Folder.LoadFolder(tab.Path);
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
        Groups.Add(new TabGroupViewModel(group, tab => SelectTab(tab)));
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

    /// <summary>
    /// パスからタブタイトルを生成する。通常はフォルダ名、ドライブルートは "C:\" 形式。
    /// </summary>
    public static string GetTabTitle(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return name.Length > 0 ? name : path;
    }
}
