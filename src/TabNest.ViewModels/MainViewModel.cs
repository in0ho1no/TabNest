using System.Collections.ObjectModel;
using TabNest.Core.Interfaces;
using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly TabManagerService _tabManager = new();
    private readonly FavoritesService _favorites = new();
    private string _title = "TabNest";
    private string? _operationError;

    /// <param name="session">
    /// 復元するセッション(起動時に settings.json から読み込んだ AppSettings)。
    /// null または復元できる内容が無い場合は SPEC「初期起動状態」で開始する。
    /// </param>
    public MainViewModel(
        IFileSystemService fileSystemService,
        IFileLauncher fileLauncher,
        AppSettings? session = null)
    {
        Folder = new FolderViewModel(fileSystemService, fileLauncher);
        Tree = new FolderTreeViewModel(fileSystemService, path => Folder.LoadFolder(path));
        // フォルダ移動のたびにアクティブタブの Path/タイトル更新とツリー選択の追従を行う
        Folder.Navigated += (_, path) =>
        {
            UpdateActiveTabLocation(path);
            Tree.RevealPath(path);
        };
        AddTabCommand = new RelayCommand(_ => AddTabToActiveGroup());
        AddGroupCommand = new RelayCommand(_ => AddGroupWithDefaultTab());

        RestoredWindowWidth = NormalizeLength(session?.WindowWidth ?? 0, fallback: 0);
        RestoredWindowHeight = NormalizeLength(session?.WindowHeight ?? 0, fallback: 0);
        LeftPaneWidth = Math.Max(
            MinLeftPaneWidth,
            NormalizeLength(session?.LeftPaneWidth ?? 0, fallback: DefaultLeftPaneWidth));

        // お気に入りはタブ状態の復元成否と独立して復元する
        // (タブ状態が無く初期起動状態になる場合でも、保存済みのお気に入りは保持する)
        if (session is not null)
        {
            _favorites.RestoreSavedGroups(session.SavedGroups);
        }

        if (session is not null && _tabManager.RestoreSession(session))
        {
            foreach (var group in _tabManager.Groups)
            {
                Groups.Add(new TabGroupViewModel(group, tab => SelectTab(tab), tab => CloseTab(tab)));
            }

            ApplyActiveStates();
        }
        else
        {
            InitializeDefaultTabs();
        }
    }

    /// <summary>左カラム幅の既定値(px)。SPEC「画面レイアウト」準拠。</summary>
    public const double DefaultLeftPaneWidth = 220;

    /// <summary>左カラム幅の最小値(px)。SPEC「画面レイアウト」準拠。</summary>
    public const double MinLeftPaneWidth = 150;

    /// <summary>
    /// 復元するウィンドウ幅(物理px)。0 は保存値なし・不正値で、復元しない(既定サイズで起動)。
    /// </summary>
    public double RestoredWindowWidth { get; }

    /// <summary>
    /// 復元するウィンドウ高さ(物理px)。0 は保存値なし・不正値で、復元しない(既定サイズで起動)。
    /// </summary>
    public double RestoredWindowHeight { get; }

    /// <summary>
    /// 左カラムの幅(DIP)。保存値が無い・不正な場合は既定 220、最小 150 に補正される。
    /// </summary>
    public double LeftPaneWidth { get; }

    /// <summary>保存値が正の有限値ならそのまま、それ以外は fallback を返す。</summary>
    private static double NormalizeLength(double value, double fallback)
        => double.IsFinite(value) && value > 0 ? value : fallback;

    /// <summary>ウィンドウタイトル。初期値は "TabNest"。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>表示中フォルダのファイル一覧 ViewModel。</summary>
    public FolderViewModel Folder { get; }

    /// <summary>左カラムのフォルダツリー ViewModel。</summary>
    public FolderTreeViewModel Tree { get; }

    /// <summary>タブグループ(表示順)。</summary>
    public ObservableCollection<TabGroupViewModel> Groups { get; } = [];

    /// <summary>
    /// 起動時の初期表示としてアクティブタブを選択し、そのフォルダを読み込む
    /// (初期起動状態では %UserProfile%、セッション復元時は前回のアクティブタブ)。
    /// アクティブタブが無い場合は先頭グループの先頭タブにフォールバックする。
    /// </summary>
    public bool LoadInitialFolder()
    {
        var tab = _tabManager.ActiveTabId is string activeId ? FindTabViewModel(activeId) : null;
        tab ??= Groups.SelectMany(g => g.Tabs).FirstOrDefault();
        return tab is not null && SelectTab(tab);
    }

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

    /// <summary>タブ・グループ操作のエラーメッセージ(上限到達など)。成功時は null に戻る。</summary>
    public string? OperationError
    {
        get => _operationError;
        private set => SetProperty(ref _operationError, value);
    }

    /// <summary>アクティブグループの末尾に新規タブ(%UserProfile%)を追加する。</summary>
    public RelayCommand AddTabCommand { get; }

    /// <summary>「作業N」グループを1段追加する(%UserProfile% の初期タブ付き)。</summary>
    public RelayCommand AddGroupCommand { get; }

    /// <summary>
    /// アクティブグループの末尾に %UserProfile% を開く新規タブを追加する(ボタン / Ctrl+T)。
    /// グループ名編集中は何も実行しない。上限到達時はエラーを表示して追加しない。
    /// </summary>
    public bool AddTabToActiveGroup()
    {
        if (IsRenameInProgress)
        {
            return false;
        }

        var groupId = _tabManager.ActiveGroupId;
        return groupId is not null && AddTab(groupId, UserProfilePath) is not null;
    }

    /// <summary>
    /// 「作業N」という名前の新規グループを1段追加し、%UserProfile% を開く初期タブを作成する
    /// (ボタン / Ctrl+G)。グループ名編集中は何も実行しない。5段到達時はエラーを表示して追加しない。
    /// </summary>
    public bool AddGroupWithDefaultTab()
    {
        if (IsRenameInProgress)
        {
            return false;
        }

        var result = _tabManager.AddGroup(GenerateGroupName());
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
            return false;
        }

        var group = result.Value!;
        Groups.Add(new TabGroupViewModel(group, tab => SelectTab(tab), tab => CloseTab(tab)));
        AddTab(group.Id, UserProfilePath);
        OperationError = null;
        return true;
    }

    /// <summary>
    /// 新規グループ名「作業N」を採番する。現在表示中の全グループ名のうち
    /// 「作業&lt;整数&gt;」形式に完全一致する名前の最大値+1(マッチがなければ 1)。
    /// 「作業1 (2)」のような完全一致しない名前は判定対象に含めない。
    /// </summary>
    public string GenerateGroupName()
    {
        var max = 0L;
        foreach (var group in Groups)
        {
            var name = group.Name;
            if (name.StartsWith("作業", StringComparison.Ordinal)
                && long.TryParse(name.AsSpan(2), out var number)
                && number > 0
                && name == $"作業{number}")
            {
                max = Math.Max(max, number);
            }
        }

        return $"作業{max + 1}";
    }

    /// <summary>お気に入り(保存済みタブグループ)一覧。保存順(SavedAt 昇順)。</summary>
    public IReadOnlyList<SavedTabGroup> Favorites => _favorites.SavedGroups;

    /// <summary>
    /// 指定されたグループ(右クリック対象。アクティブグループとは限らない)を
    /// お気に入りとして保存する。名前はグループ名をそのまま使い、同名時は連番を付ける。
    /// タブ0個・上限50件到達時は保存せずエラーを表示する。
    /// </summary>
    public bool SaveGroupAsFavorite(string groupId)
    {
        var group = _tabManager.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            OperationError = "指定されたグループが見つかりません。";
            return false;
        }

        var result = _favorites.SaveFavorite(group);
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
            return false;
        }

        OperationError = null;
        return true;
    }

    /// <summary>
    /// お気に入りを新しい段として開く。開いたグループの名前はお気に入りの名前を引き継ぎ、
    /// 先頭のタブをアクティブにしてそのフォルダを表示する(存在しないパスはエラー表示で開く)。
    /// 5段上限到達時は開かずエラーを表示する。グループ名編集中は何も実行しない。
    /// </summary>
    public bool OpenFavorite(string favoriteId)
    {
        if (IsRenameInProgress)
        {
            return false;
        }

        var favorite = _favorites.FindFavorite(favoriteId);
        if (favorite is null)
        {
            OperationError = "指定されたお気に入りが見つかりません。";
            return false;
        }

        var result = _tabManager.OpenSavedGroup(favorite, GetTabTitle);
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
            return false;
        }

        var groupVm = new TabGroupViewModel(result.Value!, tab => SelectTab(tab), tab => CloseTab(tab));
        Groups.Add(groupVm);
        ApplyActiveStates();
        if (groupVm.Tabs.FirstOrDefault() is { } firstTab)
        {
            Folder.AttachHistory(firstTab.History);
            Folder.ShowFolder(firstTab.Path);
        }

        OperationError = null;
        return true;
    }

    /// <summary>お気に入りを削除する。存在しない場合は false。</summary>
    public bool RemoveFavorite(string favoriteId)
    {
        if (!_favorites.RemoveFavorite(favoriteId))
        {
            return false;
        }

        OperationError = null;
        return true;
    }

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
    /// 上限到達などの失敗時は OperationError を設定して null を返す。
    /// </summary>
    internal FolderTabViewModel? AddTab(string groupId, string path)
    {
        var result = _tabManager.AddTab(groupId, path, GetTabTitle(path));
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
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
        OperationError = null;
        return tabVm;
    }

    /// <summary>
    /// アプリ終了時のセッション保存用に、現在のタブ状態(タブグループ・アクティブグループ/タブ・
    /// 閉じたタブ履歴・お気に入り)とウィンドウサイズ・左カラム幅から AppSettings を生成する
    /// (SPEC「設定保存」)。タブごとの戻る・進む履歴は保存対象外。
    /// ウィンドウサイズ・左カラム幅は WinUI 依存のため View 側で取得して渡す。
    /// </summary>
    public AppSettings CreateAppSettings(double windowWidth, double windowHeight, double leftPaneWidth)
        => new()
        {
            TabGroups = _tabManager.Groups.ToList(),
            ClosedTabs = _tabManager.ClosedTabs.ToList(),
            SavedGroups = _favorites.SavedGroups.ToList(),
            ActiveGroupId = _tabManager.ActiveGroupId,
            ActiveTabId = _tabManager.ActiveTabId,
            WindowWidth = windowWidth,
            WindowHeight = windowHeight,
            LeftPaneWidth = leftPaneWidth,
        };

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
