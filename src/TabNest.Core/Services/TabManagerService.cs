using TabNest.Core.Models;

namespace TabNest.Core.Services;

/// <summary>
/// タブとタブグループの状態を管理するサービス。
/// グループ・タブの追加/削除/アクティブ変更を担う(上限制御は Task 3-2 で追加)。
/// </summary>
public sealed class TabManagerService
{
    private readonly List<TabGroup> _groups = new();

    /// <summary>全タブグループ(表示順)。</summary>
    public IReadOnlyList<TabGroup> Groups => _groups;

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
    /// </summary>
    public TabGroup AddGroup(string name)
    {
        var group = new TabGroup
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
        };
        _groups.Add(group);
        ActiveGroupId ??= group.Id;
        return group;
    }

    /// <summary>
    /// 指定グループの末尾にタブを追加し、アクティブタブにする。
    /// グループが存在しない場合は null を返す。
    /// </summary>
    public FolderTab? AddTab(string groupId, string path, string title)
    {
        if (FindGroup(groupId) is not TabGroup group)
        {
            return null;
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
        return tab;
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
        group.Tabs.RemoveAt(index);

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

    private TabGroup? FindGroup(string? groupId)
        => groupId is null ? null : _groups.FirstOrDefault(g => g.Id == groupId);
}
