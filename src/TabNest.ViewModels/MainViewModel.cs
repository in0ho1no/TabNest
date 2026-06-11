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
        var groupVm = new TabGroupViewModel(group);
        if (groupVm.Tabs.Count > 0)
        {
            groupVm.Tabs[0].IsActive = true;
        }

        Groups.Add(groupVm);
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
