using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Core.Tests;

/// <summary>タブ数・グループ数の上限/下限制御のテスト(Task 3-2)。</summary>
public class TabLimitTests
{
    private static TabGroup AddGroup(TabManagerService service, string name)
    {
        var result = service.AddGroup(name);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public void タブは1グループに20個まで追加できる()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");

        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            var result = service.AddTab(group.Id, $@"C:\folder{i}", $"folder{i}");
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(20, group.Tabs.Count);
    }

    [Fact]
    public void 同一グループへの21個目はTabLimitReachedで拒否される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            service.AddTab(group.Id, $@"C:\folder{i}", $"folder{i}");
        }

        var result = service.AddTab(group.Id, @"C:\over", "over");

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.TabLimitReached, result.Error);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(20, group.Tabs.Count);
    }

    [Fact]
    public void 上限到達グループがあっても他のグループへは追加できる()
    {
        var service = new TabManagerService();
        var full = AddGroup(service, "作業1");
        var other = AddGroup(service, "作業2");
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            service.AddTab(full.Id, $@"C:\folder{i}", $"folder{i}");
        }

        var result = service.AddTab(other.Id, @"C:\ok", "ok");

        Assert.True(result.IsSuccess);
        Assert.Single(other.Tabs);
    }

    [Fact]
    public void 上限で拒否された後も同グループのタブを閉じれば追加できる()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            service.AddTab(group.Id, $@"C:\folder{i}", $"folder{i}");
        }
        Assert.False(service.AddTab(group.Id, @"C:\over", "over").IsSuccess);

        service.CloseTab(group.Tabs[0].Id);
        var result = service.AddTab(group.Id, @"C:\retry", "retry");

        Assert.True(result.IsSuccess);
        Assert.Equal(20, group.Tabs.Count);
    }

    [Fact]
    public void グループは5つまで追加できる()
    {
        var service = new TabManagerService();

        for (var i = 1; i <= TabManagerService.MaxGroups; i++)
        {
            var result = service.AddGroup($"作業{i}");
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(5, service.Groups.Count);
    }

    [Fact]
    public void グループ6つ目はGroupLimitReachedで拒否される()
    {
        var service = new TabManagerService();
        for (var i = 1; i <= TabManagerService.MaxGroups; i++)
        {
            service.AddGroup($"作業{i}");
        }

        var result = service.AddGroup("作業6");

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.GroupLimitReached, result.Error);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(5, service.Groups.Count);
    }

    [Fact]
    public void 最後の1グループの削除はLastGroupProtectedで拒否される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");

        var result = service.RemoveGroup(group.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.LastGroupProtected, result.Error);
        Assert.Single(service.Groups);
    }

    [Fact]
    public void グループが2つあれば削除できる()
    {
        var service = new TabManagerService();
        var first = AddGroup(service, "作業1");
        var second = AddGroup(service, "作業2");

        var result = service.RemoveGroup(second.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal([first.Id], service.Groups.Select(g => g.Id).ToArray());
    }

    [Fact]
    public void アクティブグループを削除すると先頭の残存グループがアクティブになる()
    {
        var service = new TabManagerService();
        var first = AddGroup(service, "作業1");
        var second = AddGroup(service, "作業2");
        var firstTab = service.AddTab(first.Id, @"C:\a", "a").Value!;
        var secondTab = service.AddTab(second.Id, @"C:\b", "b").Value!;
        Assert.Equal(second.Id, service.ActiveGroupId);

        var result = service.RemoveGroup(second.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(first.Id, service.ActiveGroupId);
        Assert.Equal(firstTab.Id, service.ActiveTabId);
    }

    [Fact]
    public void 存在しないグループの削除はGroupNotFound()
    {
        var service = new TabManagerService();
        AddGroup(service, "作業1");
        AddGroup(service, "作業2");

        var result = service.RemoveGroup("no-such-group");

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.GroupNotFound, result.Error);
        Assert.Equal(2, service.Groups.Count);
    }
}
