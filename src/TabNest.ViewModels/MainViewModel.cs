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
    private bool _isFolderTreeVisible = true;

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
        // フォルダツリーの表示状態を復元する(保存値が無い場合は既定で表示する)
        _isFolderTreeVisible = session?.IsFolderTreeVisible ?? true;

        // お気に入りはタブ状態の復元成否と独立して復元する
        // (タブ状態が無く初期起動状態になる場合でも、保存済みのお気に入りは保持する)
        if (session is not null)
        {
            _favorites.RestoreSavedGroups(session.SavedGroups);
            foreach (var favorite in _favorites.SavedGroups)
            {
                Favorites.Add(CreateFavoriteItem(favorite));
            }
        }

        if (session is not null && _tabManager.RestoreSession(session))
        {
            foreach (var group in _tabManager.Groups)
            {
                Groups.Add(CreateGroupViewModel(group));
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

    /// <summary>
    /// 左カラムのフォルダツリーを表示するか(Task 6-5)。トグルで切り替え、settings.json に保存・復元する。
    /// false のとき View 側でフォルダツリー領域を畳む(お気に入り領域は残す)。
    /// </summary>
    public bool IsFolderTreeVisible
    {
        get => _isFolderTreeVisible;
        set => SetProperty(ref _isFolderTreeVisible, value);
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
    /// 新しいアクティブタブのフォルダ内容を表示する。閉じた結果グループが空になった場合は
    /// Core 側で空グループが自動クローズされるため、対応するグループ表示も除去する(Task 6-6)。
    /// アプリ内の最後の1タブは閉じられず、状態を変更せず false を返す。
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

        // Core 側でグループが自動クローズされた場合は ViewModel 側のグループ表示も除去する
        if (groupVm is not null && _tabManager.Groups.All(g => g.Id != groupVm.Id))
        {
            Groups.Remove(groupVm);
        }

        ApplyActiveStates();

        if (wasActive && _tabManager.ActiveTabId is string newActiveId
            && FindTabViewModel(newActiveId) is { } newActiveVm)
        {
            Folder.AttachHistory(newActiveVm.History);
            Folder.ShowFolder(newActiveVm.Path);
        }

        return true;
    }

    /// <summary>
    /// タブを複製する(タブの右クリックメニュー)。元タブと同じ Path / Title の新規タブを
    /// 元タブの直後に挿入し、複製したタブをアクティブにしてその内容を表示する。
    /// 複製タブの戻る・進む履歴は引き継がず、新規(空)の履歴で開始する。
    /// グループのタブ上限(20)到達時は複製せず OperationError を設定して false を返す。
    /// </summary>
    public bool DuplicateTab(FolderTabViewModel tab)
    {
        var groupVm = Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        if (groupVm is null)
        {
            return false;
        }

        var result = _tabManager.DuplicateTab(tab.Id);
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
            return false;
        }

        var index = groupVm.Tabs.IndexOf(tab);
        var duplicateVm = new FolderTabViewModel(result.Value!);
        groupVm.Tabs.Insert(index + 1, duplicateVm);
        ApplyActiveStates();
        // 複製タブはアクティブになるため、新規(空)の履歴を接続して内容を表示する
        Folder.AttachHistory(duplicateVm.History);
        Folder.ShowFolder(duplicateVm.Path);
        OperationError = null;
        return true;
    }

    /// <summary>
    /// アクティブタブを閉じる(Ctrl+W)。中クリックでの閉じると同一の経路(CloseTab)を通り、
    /// ClosedTab 履歴へ積む。最後のタブを閉じた場合の挙動も中クリックに統一する。
    /// グループ名編集中は何も実行しない(編集状態を維持する)。
    /// </summary>
    public bool CloseActiveTab()
    {
        if (IsRenameInProgress)
        {
            return false;
        }

        if (_tabManager.ActiveTabId is not string activeId
            || FindTabViewModel(activeId) is not { } activeVm)
        {
            return false;
        }

        return CloseTab(activeVm);
    }

    /// <summary>
    /// グループを削除する(グループ名の右クリックメニュー)。最後の1グループは削除できず、
    /// その場合は OperationError を設定して false を返す。削除対象がアクティブグループの場合は
    /// 隣接グループが新しいアクティブになり、そのアクティブタブのフォルダ内容を表示する。
    /// 削除されたグループ内のタブは ClosedTab 履歴へ積まない(明示的なグループ破棄操作のため)。
    /// 確認ダイアログ(タブを持つグループの削除時)は View 側で表示してから本メソッドを呼ぶ。
    /// </summary>
    public bool RemoveGroup(string groupId)
    {
        var wasActiveGroup = _tabManager.ActiveGroupId == groupId;
        var result = _tabManager.RemoveGroup(groupId);
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
            return false;
        }

        if (Groups.FirstOrDefault(g => g.Id == groupId) is { } groupVm)
        {
            Groups.Remove(groupVm);
        }

        ApplyActiveStates();

        // アクティブグループを削除した場合は新しいアクティブタブのフォルダ内容を表示する
        if (wasActiveGroup && _tabManager.ActiveTabId is string newActiveId
            && FindTabViewModel(newActiveId) is { } newActiveVm)
        {
            Folder.AttachHistory(newActiveVm.History);
            Folder.ShowFolder(newActiveVm.Path);
        }

        OperationError = null;
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
        Groups.Add(CreateGroupViewModel(group));
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

    /// <summary>
    /// お気に入り(保存済みタブグループ)一覧。保存順(SavedAt 昇順)。
    /// FavoritesService の一覧と同期して UI へ変更通知する。
    /// </summary>
    public ObservableCollection<FavoriteItemViewModel> Favorites { get; } = [];

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

        Favorites.Add(CreateFavoriteItem(result.Value!));
        OperationError = null;
        return true;
    }

    /// <summary>
    /// お気に入り項目 ViewModel を生成し、リネーム処理(同名衝突解決)を親 ViewModel に接続する。
    /// </summary>
    private FavoriteItemViewModel CreateFavoriteItem(SavedTabGroup model)
        => new(model, newName => RenameFavorite(model.Id, newName));

    /// <summary>
    /// お気に入りをリネームする(右クリックメニュー「名前の変更」。Task 6-4)。
    /// 前後の空白を除去し、空文字なら元の名前を維持して何もしない。
    /// 同名衝突は保存時と同じ規則(完全一致で連番付与・上書きしない)で FavoritesService が解決する。
    /// </summary>
    public bool RenameFavorite(string favoriteId, string newName)
    {
        var trimmed = newName.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var result = _favorites.RenameFavorite(favoriteId, trimmed);
        if (!result.IsSuccess)
        {
            OperationError = result.ErrorMessage;
            return false;
        }

        if (Favorites.FirstOrDefault(f => f.Id == favoriteId) is { } itemVm)
        {
            itemVm.RefreshName();
        }

        OperationError = null;
        return true;
    }

    /// <summary>
    /// お気に入りを指定された Id 順に並べ替える(行内 D&amp;D。Task 6-4)。
    /// FavoritesService の保存順と Favorites コレクションの表示順を同じ順序へ同期する
    /// (順序は次回起動時に settings.json から復元される)。
    /// </summary>
    public void ReorderFavorites(IReadOnlyList<string> orderedIds)
    {
        _favorites.ReorderFavorites(orderedIds);

        for (var target = 0; target < orderedIds.Count; target++)
        {
            var id = orderedIds[target];
            var current = -1;
            for (var i = target; i < Favorites.Count; i++)
            {
                if (Favorites[i].Id == id)
                {
                    current = i;
                    break;
                }
            }

            if (current >= 0 && current != target)
            {
                Favorites.Move(current, target);
            }
        }
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

        var groupVm = CreateGroupViewModel(result.Value!);
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

    /// <summary>お気に入りを削除する(右クリックメニュー)。存在しない場合は false。</summary>
    public bool RemoveFavorite(string favoriteId)
    {
        if (!_favorites.RemoveFavorite(favoriteId))
        {
            return false;
        }

        if (Favorites.FirstOrDefault(f => f.Id == favoriteId) is { } itemVm)
        {
            Favorites.Remove(itemVm);
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

    /// <summary>戻る(Alt+左)。アクティブタブの履歴で戻る操作を行う。</summary>
    public bool NavigateBack() => InvokeFolderNavigation(Folder.BackCommand);

    /// <summary>進む(Alt+右)。アクティブタブの履歴で進む操作を行う。</summary>
    public bool NavigateForward() => InvokeFolderNavigation(Folder.ForwardCommand);

    /// <summary>上の階層へ移動する(Alt+上)。</summary>
    public bool NavigateUp() => InvokeFolderNavigation(Folder.NavigateUpCommand);

    /// <summary>
    /// 既存のナビゲーションコマンド(戻る/進む/上へ)を Alt 系ショートカットから実行する。
    /// グループ名編集中は何も実行しない(編集状態を維持する)。コマンド不可(戻る/進む/上へ不可)の
    /// 状態では実行せず、状態を変更しない。実行した場合のみ true を返す。
    /// </summary>
    private bool InvokeFolderNavigation(RelayCommand command)
    {
        if (IsRenameInProgress || !command.CanExecute(null))
        {
            return false;
        }

        command.Execute(null);
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
            IsFolderTreeVisible = _isFolderTreeVisible,
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
        Groups.Add(CreateGroupViewModel(group));
        ApplyActiveStates();
    }

    /// <summary>
    /// タブグループの ViewModel を生成する(タブ選択・クローズ・お気に入り保存のコールバックを接続)。
    /// </summary>
    private TabGroupViewModel CreateGroupViewModel(TabGroup group)
        => new(
            group,
            tab => SelectTab(tab),
            tab => CloseTab(tab),
            () => SaveGroupAsFavorite(group.Id),
            () => RemoveGroup(group.Id),
            tab => DuplicateTab(tab));

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
