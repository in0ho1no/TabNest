using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Core.Tests;

/// <summary>タブの複製(右クリックメニュー)のサービス層テスト(Task 6-3)。</summary>
public class TabDuplicateTests
{
    private static TabGroup AddGroup(TabManagerService service, string name)
    {
        var result = service.AddGroup(name);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public void 複製は同一PathとTitleのタブを対象タブの直後に挿入する()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var first = service.AddTab(group.Id, @"C:\a", "a").Value!;
        var target = service.AddTab(group.Id, @"C:\target", "target").Value!;
        var last = service.AddTab(group.Id, @"C:\b", "b").Value!;

        var result = service.DuplicateTab(target.Id);

        Assert.True(result.IsSuccess);
        var duplicate = result.Value!;
        Assert.Equal(target.Path, duplicate.Path);
        Assert.Equal(target.Title, duplicate.Title);
        // 対象タブの直後(インデックス2)に挿入される
        Assert.Equal(
            [first.Id, target.Id, duplicate.Id, last.Id],
            group.Tabs.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void 複製タブには新しいIdが採番される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var target = service.AddTab(group.Id, @"C:\target", "target").Value!;

        var duplicate = service.DuplicateTab(target.Id).Value!;

        Assert.NotEqual(target.Id, duplicate.Id);
        Assert.NotEmpty(duplicate.Id);
    }

    [Fact]
    public void 複製したタブがアクティブになる()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var target = service.AddTab(group.Id, @"C:\target", "target").Value!;

        var duplicate = service.DuplicateTab(target.Id).Value!;

        Assert.Equal(duplicate.Id, service.ActiveTabId);
        Assert.Equal(group.Id, service.ActiveGroupId);
    }

    [Fact]
    public void 上限到達グループでの複製はTabLimitReachedで拒否される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            service.AddTab(group.Id, $@"C:\folder{i}", $"folder{i}");
        }
        var target = group.Tabs[0];

        var result = service.DuplicateTab(target.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.TabLimitReached, result.Error);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, group.Tabs.Count);
    }

    [Fact]
    public void 存在しないタブの複製はTabNotFoundで拒否される()
    {
        var service = new TabManagerService();
        AddGroup(service, "作業1");

        var result = service.DuplicateTab("no-such-tab");

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.TabNotFound, result.Error);
    }
}
