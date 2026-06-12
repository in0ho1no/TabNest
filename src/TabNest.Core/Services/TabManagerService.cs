using TabNest.Core.Models;

namespace TabNest.Core.Services;

/// <summary>
/// タブとタブグループの状態を管理するサービス。
/// グループ・タブの追加/削除/アクティブ変更を担う(上限制御は Task 3-2 で追加)。
/// </summary>
public sealed class TabManagerService
{
    /// <summary>各グループが保持できるタブ数の上限。</summary>
    public const int MaxTabsPerGroup = 20;

    /// <summary>タブグループ数の上限。</summary>
    public const int MaxGroups = 5;

    /// <summary>タブグループ数の下限(0段になる操作は拒否する)。</summary>
    public const int MinGroups = 1;

    /// <summary>閉じたタブ履歴の最大件数(超過時は古いものから破棄)。</summary>
    public const int MaxClosedTabs = 20;

    private readonly List<TabGroup> _groups = new();
    private readonly List<ClosedTab> _closedTabs = new();

    /// <summary>全タブグループ(表示順)。</summary>
    public IReadOnlyList<TabGroup> Groups => _groups;

    /// <summary>閉じたタブ履歴(古い順。末尾が最後に閉じたタブ)。</summary>
    public IReadOnlyList<ClosedTab> ClosedTabs => _closedTabs;

    /// <summary>アクティブなグループの Id。グループが無い場合は null。</summary>
    public string? ActiveGroupId { get; private set; }

    /// <summary>アクティブなタブの Id。タブが無い場合は null。</summary>
    public string? ActiveTabId { get; private set; }

    /// <summary>アクティブなグループ。無ければ null。</summary>
    public TabGroup? ActiveGroup => FindGroup(ActiveGroupId);

    /// <summary>アクティブなタブ。無ければ null。</summary>
    public FolderTab? ActiveTab
        => ActiveTabId is null ? null : _groups.SelectMany(g => g.Tabs).FirstOrDefault(t => t.Id == ActiveTabId);

    /// <summary>
    /// 保存済みセッション(settings.json)からタブ状態を復元する(SPEC「設定保存」)。
    /// グループ数・グループ毎タブ数・閉じたタブ履歴は上限を超える分を切り捨てる
    /// (グループ・タブは先頭優先、閉じたタブ履歴は新しいもの優先)。
    /// Id が空の要素には新しい Id を採番する。アクティブタブは保存値が実在しない場合、
    /// アクティブグループ(無効なら先頭グループ)の SelectedTabId → 先頭タブの順でフォールバックする。
    /// タブ0個のグループも復元する(全タブを閉じたグループは実行時に存在しうる正規の状態のため)。
    /// 復元できるグループが1つも無い場合は何も変更せず false を返す
    /// (呼び出し側は初期起動状態で開始する)。
    /// </summary>
    public bool RestoreSession(AppSettings settings)
    {
        var groups = settings.TabGroups.Take(MaxGroups).ToList();
        if (groups.Count == 0)
        {
            return false;
        }

        foreach (var group in groups)
        {
            if (string.IsNullOrEmpty(group.Id))
            {
                group.Id = Guid.NewGuid().ToString();
            }

            if (group.Tabs.Count > MaxTabsPerGroup)
            {
                group.Tabs.RemoveRange(MaxTabsPerGroup, group.Tabs.Count - MaxTabsPerGroup);
            }

            foreach (var tab in group.Tabs)
            {
                if (string.IsNullOrEmpty(tab.Id))
                {
                    tab.Id = Guid.NewGuid().ToString();
                }
            }

            // SelectedTabId がグループ内に実在しなければ先頭タブに補正する
            if (group.Tabs.All(t => t.Id != group.SelectedTabId))
            {
                group.SelectedTabId = group.Tabs.FirstOrDefault()?.Id;
            }
        }

        _groups.Clear();
        _groups.AddRange(groups);

        _closedTabs.Clear();
        _closedTabs.AddRange(settings.ClosedTabs.TakeLast(MaxClosedTabs));

        // アクティブの決定: 保存されたアクティブタブ → アクティブグループの選択タブ → 先頭グループの選択タブ
        if (settings.ActiveTabId is string savedTabId && SetActiveTab(savedTabId))
        {
            return true;
        }

        var activeGroup = FindGroup(settings.ActiveGroupId) ?? _groups[0];
        ActiveGroupId = activeGroup.Id;
        ActiveTabId = null;
        if (activeGroup.SelectedTabId is string selectedTabId)
        {
            SetActiveTab(selectedTabId);
        }

        return true;
    }

    /// <summary>
    /// グループを末尾に追加する。最初のグループは自動的にアクティブになる。
    /// 上限(5)到達時は追加せず失敗結果を返す。
    /// </summary>
    public TabOperationResult<TabGroup> AddGroup(string name)
    {
        if (_groups.Count >= MaxGroups)
        {
            return TabOperationResult<TabGroup>.Failure(
                TabOperationError.GroupLimitReached,
                $"タブグループは最大 {MaxGroups} 段までです。");
        }

        var group = new TabGroup
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
        };
        _groups.Add(group);
        ActiveGroupId ??= group.Id;
        return TabOperationResult<TabGroup>.Success(group);
    }

    /// <summary>
    /// お気に入り(保存済みタブグループ)を新しい段として開く(SPEC「お気に入り」準拠)。
    /// 開いたグループの名前はお気に入りの名前をそのまま使い、先頭のタブをアクティブにする。
    /// 5段上限到達時は開かず失敗結果を返す。パスの実在チェックは行わない
    /// (存在しないパスのタブは表示時にエラーとなるが、タブ自体は開く)。
    /// </summary>
    /// <param name="favorite">開くお気に入り。</param>
    /// <param name="titleForPath">パスからタブタイトルを生成する関数。</param>
    public TabOperationResult<TabGroup> OpenSavedGroup(
        SavedTabGroup favorite, Func<string, string> titleForPath)
    {
        if (_groups.Count >= MaxGroups)
        {
            return TabOperationResult<TabGroup>.Failure(
                TabOperationError.GroupLimitReached,
                $"タブグループは最大 {MaxGroups} 段までです。");
        }

        var group = new TabGroup
        {
            Id = Guid.NewGuid().ToString(),
            Name = favorite.Name,
        };
        foreach (var path in favorite.Paths.Take(MaxTabsPerGroup))
        {
            group.Tabs.Add(new FolderTab
            {
                Id = Guid.NewGuid().ToString(),
                Path = path,
                Title = titleForPath(path),
                CreatedAt = DateTime.Now,
            });
        }

        _groups.Add(group);
        if (group.Tabs.Count > 0)
        {
            SetActiveTab(group.Tabs[0].Id);
        }

        return TabOperationResult<TabGroup>.Success(group);
    }

    /// <summary>
    /// グループを削除する。最後の1グループ(下限1)は削除できない。
    /// 削除したグループがアクティブだった場合は先頭の残存グループをアクティブにする。
    /// (グループ削除 UI は v0.2 以降。本メソッドは下限制御のためのサービス操作)
    /// </summary>
    public TabOperationResult<TabGroup> RemoveGroup(string groupId)
    {
        if (FindGroup(groupId) is not TabGroup group)
        {
            return TabOperationResult<TabGroup>.Failure(
                TabOperationError.GroupNotFound,
                "指定されたグループが見つかりません。");
        }

        if (_groups.Count <= MinGroups)
        {
            return TabOperationResult<TabGroup>.Failure(
                TabOperationError.LastGroupProtected,
                "最後のタブグループは削除できません。");
        }

        _groups.Remove(group);

        if (ActiveGroupId == group.Id)
        {
            var fallback = _groups[0];
            ActiveGroupId = fallback.Id;
            ActiveTabId = null;
            if (fallback.SelectedTabId is string selectedTabId)
            {
                // SetActiveTab 経由でタブの実在を検証して整合を保つ
                SetActiveTab(selectedTabId);
            }
        }

        return TabOperationResult<TabGroup>.Success(group);
    }

    /// <summary>
    /// 指定グループの末尾にタブを追加し、アクティブタブにする。
    /// グループ毎のタブ数上限(20)到達時は追加せず失敗結果を返す。
    /// </summary>
    public TabOperationResult<FolderTab> AddTab(string groupId, string path, string title)
    {
        if (FindGroup(groupId) is not TabGroup group)
        {
            return TabOperationResult<FolderTab>.Failure(
                TabOperationError.GroupNotFound,
                "指定されたグループが見つかりません。");
        }

        if (group.Tabs.Count >= MaxTabsPerGroup)
        {
            return TabOperationResult<FolderTab>.Failure(
                TabOperationError.TabLimitReached,
                $"1グループのタブは最大 {MaxTabsPerGroup} 個までです。");
        }

        var tab = new FolderTab
        {
            Id = Guid.NewGuid().ToString(),
            Path = path,
            Title = title,
            CreatedAt = DateTime.Now,
        };
        group.Tabs.Add(tab);
        SetActiveTab(tab.Id);
        return TabOperationResult<FolderTab>.Success(tab);
    }

    /// <summary>
    /// タブを閉じる。アクティブタブを閉じた場合は同グループ内の隣のタブ
    /// (次を優先、無ければ前)をアクティブにする。グループが空になった場合は
    /// アクティブタブなし(null)となる。タブが存在しない場合は false。
    /// </summary>
    public bool CloseTab(string tabId)
    {
        var group = _groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == tabId));
        if (group is null)
        {
            return false;
        }

        var index = group.Tabs.FindIndex(t => t.Id == tabId);
        var closing = group.Tabs[index];
        group.Tabs.RemoveAt(index);

        _closedTabs.Add(new ClosedTab
        {
            Path = closing.Path,
            Title = closing.Title,
            GroupId = group.Id,
            TabIndex = index,
            ClosedAt = DateTime.Now,
        });
        while (_closedTabs.Count > MaxClosedTabs)
        {
            _closedTabs.RemoveAt(0);
        }

        if (group.SelectedTabId == tabId)
        {
            var neighbor = index < group.Tabs.Count
                ? group.Tabs[index]
                : group.Tabs.LastOrDefault();
            group.SelectedTabId = neighbor?.Id;
        }

        if (ActiveTabId == tabId)
        {
            if (group.SelectedTabId is string neighborId)
            {
                SetActiveTab(neighborId);
            }
            else
            {
                ActiveTabId = null;
            }
        }

        return true;
    }

    /// <summary>
    /// アクティブタブを変更する。所属グループもアクティブになり、
    /// グループの SelectedTabId も更新される。タブが存在しない場合は false。
    /// </summary>
    public bool SetActiveTab(string tabId)
    {
        var group = _groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == tabId));
        if (group is null)
        {
            return false;
        }

        ActiveGroupId = group.Id;
        ActiveTabId = tabId;
        group.SelectedTabId = tabId;
        return true;
    }

    /// <summary>
    /// 最後に閉じたタブを復元する(SPEC「閉じたタブの復元ルール」準拠)。
    /// 復元先は元のグループを優先し、消滅・タブ上限到達時はアクティブグループの末尾とする。
    /// 元のグループへは TabIndex が範囲内なら同位置へ挿入し、範囲外なら末尾に追加する。
    /// 復元成功時のみ履歴から削除し、復元したタブをアクティブにする。
    /// </summary>
    public TabOperationResult<FolderTab> RestoreClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            return TabOperationResult<FolderTab>.Failure(
                TabOperationError.NoClosedTab,
                "復元できるタブがありません。");
        }

        var closed = _closedTabs[^1];

        // 復元先の決定: 元グループ優先、消滅・上限時はアクティブグループ
        var originalGroup = FindGroup(closed.GroupId);
        var targetGroup = originalGroup is not null && originalGroup.Tabs.Count < MaxTabsPerGroup
            ? originalGroup
            : ActiveGroup;

        if (targetGroup is null)
        {
            return TabOperationResult<FolderTab>.Failure(
                TabOperationError.GroupNotFound,
                "復元先のグループがありません。");
        }

        if (targetGroup.Tabs.Count >= MaxTabsPerGroup)
        {
            return TabOperationResult<FolderTab>.Failure(
                TabOperationError.TabLimitReached,
                $"復元先グループのタブが上限({MaxTabsPerGroup})に達しています。");
        }

        // 挿入位置の決定: 元グループに戻す場合のみ TabIndex を考慮、それ以外は末尾
        var insertIndex = ReferenceEquals(targetGroup, originalGroup)
            && closed.TabIndex >= 0
            && closed.TabIndex <= targetGroup.Tabs.Count
                ? closed.TabIndex
                : targetGroup.Tabs.Count;

        var tab = new FolderTab
        {
            Id = Guid.NewGuid().ToString(),
            Path = closed.Path,
            Title = closed.Title,
            CreatedAt = DateTime.Now,
        };
        targetGroup.Tabs.Insert(insertIndex, tab);
        _closedTabs.Remove(closed);
        SetActiveTab(tab.Id);
        return TabOperationResult<FolderTab>.Success(tab);
    }

    private TabGroup? FindGroup(string? groupId)
        => groupId is null ? null : _groups.FirstOrDefault(g => g.Id == groupId);
}
