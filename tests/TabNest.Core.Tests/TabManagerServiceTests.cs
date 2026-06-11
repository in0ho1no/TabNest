using TabNest.Core.Services;

namespace TabNest.Core.Tests;

public class TabManagerServiceTests
{
    private const string UserProfile = @"C:\Users\test";

    [Fact]
    public void AddGroup_グループが追加され最初のグループがアクティブになる()
    {
        var service = new TabManagerService();

        var group = service.AddGroup("作業1");

        Assert.Single(service.Groups);
        Assert.Equal("作業1", group.Name);
        Assert.NotEmpty(group.Id);
        Assert.Equal(group.Id, service.ActiveGroupId);
    }

    [Fact]
    public void AddGroup_2つ目のグループ追加ではアクティブグループが変わらない()
    {
        var service = new TabManagerService();
        var first = service.AddGroup("作業1");

        service.AddGroup("作業2");

        Assert.Equal(2, service.Groups.Count);
        Assert.Equal(first.Id, service.ActiveGroupId);
    }

    [Fact]
    public void AddTab_グループ末尾に追加されアクティブタブになる()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var first = service.AddTab(group.Id, UserProfile, "test");

        var second = service.AddTab(group.Id, @"C:\work", "work");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal([first!.Id, second!.Id], group.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal(second.Id, service.ActiveTabId);
        Assert.Equal(second.Id, group.SelectedTabId);
        Assert.Equal(@"C:\work", second.Path);
        Assert.Equal("work", second.Title);
    }

    [Fact]
    public void AddTab_存在しないグループにはnullを返す()
    {
        var service = new TabManagerService();
        service.AddGroup("作業1");

        var tab = service.AddTab("no-such-group", UserProfile, "test");

        Assert.Null(tab);
        Assert.Empty(service.Groups[0].Tabs);
    }

    [Fact]
    public void CloseTab_タブが削除される()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var tab = service.AddTab(group.Id, UserProfile, "test")!;

        var ok = service.CloseTab(tab.Id);

        Assert.True(ok);
        Assert.Empty(group.Tabs);
        Assert.Null(service.ActiveTabId);
        Assert.Null(group.SelectedTabId);
    }

    [Fact]
    public void CloseTab_アクティブタブを閉じると次のタブがアクティブになる()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var a = service.AddTab(group.Id, @"C:\a", "a")!;
        var b = service.AddTab(group.Id, @"C:\b", "b")!;
        var c = service.AddTab(group.Id, @"C:\c", "c")!;
        service.SetActiveTab(b.Id);

        service.CloseTab(b.Id);

        Assert.Equal([a.Id, c.Id], group.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal(c.Id, service.ActiveTabId);
        Assert.Equal(c.Id, group.SelectedTabId);
    }

    [Fact]
    public void CloseTab_末尾のアクティブタブを閉じると前のタブがアクティブになる()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var a = service.AddTab(group.Id, @"C:\a", "a")!;
        var b = service.AddTab(group.Id, @"C:\b", "b")!;

        service.CloseTab(b.Id);

        Assert.Equal(a.Id, service.ActiveTabId);
    }

    [Fact]
    public void CloseTab_非アクティブタブを閉じてもアクティブタブは変わらない()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var a = service.AddTab(group.Id, @"C:\a", "a")!;
        var b = service.AddTab(group.Id, @"C:\b", "b")!;
        service.SetActiveTab(b.Id);

        service.CloseTab(a.Id);

        Assert.Equal(b.Id, service.ActiveTabId);
        Assert.Single(group.Tabs);
    }

    [Fact]
    public void CloseTab_存在しないタブはfalse()
    {
        var service = new TabManagerService();
        service.AddGroup("作業1");

        Assert.False(service.CloseTab("no-such-tab"));
    }

    [Fact]
    public void SetActiveTab_タブと所属グループがアクティブになる()
    {
        var service = new TabManagerService();
        var group1 = service.AddGroup("作業1");
        var group2 = service.AddGroup("作業2");
        var tab1 = service.AddTab(group1.Id, @"C:\a", "a")!;
        var tab2 = service.AddTab(group2.Id, @"C:\b", "b")!;
        service.SetActiveTab(tab1.Id);

        var ok = service.SetActiveTab(tab2.Id);

        Assert.True(ok);
        Assert.Equal(group2.Id, service.ActiveGroupId);
        Assert.Equal(tab2.Id, service.ActiveTabId);
        Assert.Equal(tab2.Id, group2.SelectedTabId);
        // 他グループの SelectedTabId は維持される(タブを切り替えても履歴は混ざらない)
        Assert.Equal(tab1.Id, group1.SelectedTabId);
    }

    [Fact]
    public void SetActiveTab_存在しないタブはfalseで状態を変更しない()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var tab = service.AddTab(group.Id, @"C:\a", "a")!;

        var ok = service.SetActiveTab("no-such-tab");

        Assert.False(ok);
        Assert.Equal(tab.Id, service.ActiveTabId);
        Assert.Equal(group.Id, service.ActiveGroupId);
    }

    [Fact]
    public void ActiveTabとActiveGroupが現在の状態を返す()
    {
        var service = new TabManagerService();
        var group = service.AddGroup("作業1");
        var tab = service.AddTab(group.Id, @"C:\a", "a")!;

        Assert.Same(group, service.ActiveGroup);
        Assert.Same(tab, service.ActiveTab);
    }
}
