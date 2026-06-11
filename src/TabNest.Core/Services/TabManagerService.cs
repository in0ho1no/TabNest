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
