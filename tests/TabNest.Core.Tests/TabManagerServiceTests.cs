using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Core.Tests;

public class TabManagerServiceTests
{
    private const string UserProfile = @"C:\Users\test";

    private static TabGroup AddGroup(TabManagerService service, string name)
    {
        var result = service.AddGroup(name);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static FolderTab AddTab(TabManagerService service, string groupId, string path, string title)
    {
        var result = service.AddTab(groupId, path, title);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public void AddGroup_グループが追加され最初のグループがアクティブになる()
    {
        var service = new TabManagerService();

        var group = AddGroup(service, "作業1");

        Assert.Single(service.Groups);
        Assert.Equal("作業1", group.Name);
        Assert.NotEmpty(group.Id);
        Assert.Equal(group.Id, service.ActiveGroupId);
    }

    [Fact]
    public void AddGroup_2つ目のグループ追加ではアクティブグループが変わらない()
    {
        var service = new TabManagerService();
        var first = AddGroup(service, "作業1");

        AddGroup(service, "作業2");

        Assert.Equal(2, service.Groups.Count);
        Assert.Equal(first.Id, service.ActiveGroupId);
    }

    [Fact]
    public void AddTab_グループ末尾に追加されアクティブタブになる()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var first = AddTab(service, group.Id, UserProfile, "test");

        var second = AddTab(service, group.Id, @"C:\work", "work");

        Assert.Equal([first.Id, second.Id], group.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal(second.Id, service.ActiveTabId);
        Assert.Equal(second.Id, group.SelectedTabId);
        Assert.Equal(@"C:\work", second.Path);
        Assert.Equal("work", second.Title);
    }

    [Fact]
    public void AddTab_存在しないグループはGroupNotFound()
    {
        var service = new TabManagerService();
        AddGroup(service, "作業1");

        var result = service.AddTab("no-such-group", UserProfile, "test");

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.GroupNotFound, result.Error);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(service.Groups[0].Tabs);
    }

    [Fact]
    public void CloseTab_タブが削除される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var keep = AddTab(service, group.Id, UserProfile, "keep");
        var tab = AddTab(service, group.Id, @"C:\x", "test");

        var ok = service.CloseTab(tab.Id);

        Assert.True(ok);
        Assert.Equal([keep.Id], group.Tabs.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void CloseTab_アクティブタブを閉じると次のタブがアクティブになる()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var a = AddTab(service, group.Id, @"C:\a", "a");
        var b = AddTab(service, group.Id, @"C:\b", "b");
        var c = AddTab(service, group.Id, @"C:\c", "c");
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
        var group = AddGroup(service, "作業1");
        var a = AddTab(service, group.Id, @"C:\a", "a");
        var b = AddTab(service, group.Id, @"C:\b", "b");

        service.CloseTab(b.Id);

        Assert.Equal(a.Id, service.ActiveTabId);
    }

    [Fact]
    public void CloseTab_非アクティブタブを閉じてもアクティブタブは変わらない()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var a = AddTab(service, group.Id, @"C:\a", "a");
        var b = AddTab(service, group.Id, @"C:\b", "b");
        service.SetActiveTab(b.Id);

        service.CloseTab(a.Id);

        Assert.Equal(b.Id, service.ActiveTabId);
        Assert.Single(group.Tabs);
    }

    [Fact]
    public void CloseTab_存在しないタブはfalse()
    {
        var service = new TabManagerService();
        AddGroup(service, "作業1");

        Assert.False(service.CloseTab("no-such-tab"));
    }

    // --- Task 6-6: グループ内の全タブを閉じたときのグループ自動クローズ ---

    [Fact]
    public void CloseTab_グループ内の最後のタブを閉じるとそのグループも閉じる()
    {
        var service = new TabManagerService();
        var group1 = AddGroup(service, "作業1");
        var group2 = AddGroup(service, "作業2");
        AddTab(service, group1.Id, @"C:\a", "a");
        var b = AddTab(service, group2.Id, @"C:\b", "b");

        var ok = service.CloseTab(b.Id);

        Assert.True(ok);
        // group2 は空になったので自動的に閉じる(空グループが残らない)
        Assert.Equal([group1.Id], service.Groups.Select(g => g.Id).ToArray());
    }

    [Fact]
    public void CloseTab_自動クローズで消える最後のタブもClosedTabへ積む()
    {
        var service = new TabManagerService();
        var group1 = AddGroup(service, "作業1");
        var group2 = AddGroup(service, "作業2");
        AddTab(service, group1.Id, @"C:\a", "a");
        var b = AddTab(service, group2.Id, @"C:\b", "b");

        service.CloseTab(b.Id);

        // グループの明示削除(RemoveGroup)とは異なり、閉じたタブは履歴へ積む
        Assert.Contains(service.ClosedTabs, c => c.Path == @"C:\b");
    }

    [Fact]
    public void CloseTab_アクティブグループの最後のタブを閉じると別グループへフォーカスが移る()
    {
        var service = new TabManagerService();
        var group1 = AddGroup(service, "作業1");
        var group2 = AddGroup(service, "作業2");
        var a = AddTab(service, group1.Id, @"C:\a", "a");
        var b = AddTab(service, group2.Id, @"C:\b", "b");
        service.SetActiveTab(b.Id);

        var ok = service.CloseTab(b.Id);

        Assert.True(ok);
        // group2 は閉じ、先頭の残存グループ(group1)の選択タブがアクティブになる
        Assert.Equal([group1.Id], service.Groups.Select(g => g.Id).ToArray());
        Assert.Equal(group1.Id, service.ActiveGroupId);
        Assert.Equal(a.Id, service.ActiveTabId);
    }

    [Fact]
    public void CloseTab_グループ1つタブ1つのときは閉じられず状態が変わらない()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var only = AddTab(service, group.Id, @"C:\a", "a");

        var ok = service.CloseTab(only.Id);

        Assert.False(ok);
        // 状態は変更されない(常にタブ1個以上・グループ1段以上を保持する)
        Assert.Equal([only.Id], group.Tabs.Select(t => t.Id).ToArray());
        Assert.Single(service.Groups);
        Assert.Equal(only.Id, service.ActiveTabId);
        // 拒否した閉じる操作は ClosedTab 履歴へ積まない
        Assert.Empty(service.ClosedTabs);
    }

    [Fact]
    public void CloseTab_複数グループでも全体最後の1タブは閉じられない()
    {
        var service = new TabManagerService();
        var group1 = AddGroup(service, "作業1");
        AddGroup(service, "作業2"); // タブ0個の空グループ(D&D 等で生じ得る)
        var only = AddTab(service, group1.Id, @"C:\a", "a");

        var ok = service.CloseTab(only.Id);

        Assert.False(ok);
        Assert.Single(group1.Tabs);
        Assert.Equal(2, service.Groups.Count);
    }

    [Fact]
    public void SetActiveTab_タブと所属グループがアクティブになる()
    {
        var service = new TabManagerService();
        var group1 = AddGroup(service, "作業1");
        var group2 = AddGroup(service, "作業2");
        var tab1 = AddTab(service, group1.Id, @"C:\a", "a");
        var tab2 = AddTab(service, group2.Id, @"C:\b", "b");
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
        var group = AddGroup(service, "作業1");
        var tab = AddTab(service, group.Id, @"C:\a", "a");

        var ok = service.SetActiveTab("no-such-tab");

        Assert.False(ok);
        Assert.Equal(tab.Id, service.ActiveTabId);
        Assert.Equal(group.Id, service.ActiveGroupId);
    }

    [Fact]
    public void ActiveTabとActiveGroupが現在の状態を返す()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var tab = AddTab(service, group.Id, @"C:\a", "a");

        Assert.Same(group, service.ActiveGroup);
        Assert.Same(tab, service.ActiveTab);
    }

    [Fact]
    public void ReorderTabs_指定順にグループ内タブが並べ替わる()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var t1 = AddTab(service, group.Id, @"C:\a", "a");
        var t2 = AddTab(service, group.Id, @"C:\b", "b");
        var t3 = AddTab(service, group.Id, @"C:\c", "c");

        var ok = service.ReorderTabs(group.Id, [t3.Id, t1.Id, t2.Id]);

        Assert.True(ok);
        Assert.Equal([t3.Id, t1.Id, t2.Id], group.Tabs.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void ReorderTabs_並べ替えてもアクティブタブと選択タブが維持される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var t1 = AddTab(service, group.Id, @"C:\a", "a");
        var t2 = AddTab(service, group.Id, @"C:\b", "b");
        AddTab(service, group.Id, @"C:\c", "c");
        // t2 をアクティブ・選択タブにする
        service.SetActiveTab(t2.Id);

        service.ReorderTabs(group.Id, [t2.Id, t1.Id]);

        Assert.Equal(t2.Id, service.ActiveTabId);
        Assert.Equal(t2.Id, group.SelectedTabId);
        Assert.Equal(group.Id, service.ActiveGroupId);
    }

    [Fact]
    public void ReorderTabs_未知のIdは無視し未指定タブは末尾に元の順で残す()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var t1 = AddTab(service, group.Id, @"C:\a", "a");
        var t2 = AddTab(service, group.Id, @"C:\b", "b");
        var t3 = AddTab(service, group.Id, @"C:\c", "c");

        // t3 のみ先頭指定 + 存在しない Id。t1,t2 は元の相対順で末尾に残る
        var ok = service.ReorderTabs(group.Id, [t3.Id, "no-such"]);

        Assert.True(ok);
        Assert.Equal([t3.Id, t1.Id, t2.Id], group.Tabs.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void ReorderTabs_存在しないグループはfalseを返す()
    {
        var service = new TabManagerService();
        AddGroup(service, "作業1");

        var ok = service.ReorderTabs("no-such-group", ["x"]);

        Assert.False(ok);
    }

    [Fact]
    public void MoveTabToGroup_別グループの指定位置へタブが移動する()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var a = AddTab(service, g1.Id, @"C:\a", "a");
        AddTab(service, g1.Id, @"C:\b", "b");
        AddTab(service, g2.Id, @"C:\x", "x");
        AddTab(service, g2.Id, @"C:\y", "y");

        // a を g2 の先頭(index 0)へ移動する
        var result = service.MoveTabToGroup(a.Id, g2.Id, 0);

        Assert.True(result.IsSuccess);
        Assert.Same(a, result.Value);
        Assert.Equal(["b"], g1.Tabs.Select(t => t.Title).ToArray());
        Assert.Equal(["a", "x", "y"], g2.Tabs.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void MoveTabToGroup_範囲外の挿入位置は末尾へ補正される()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var a = AddTab(service, g1.Id, @"C:\a", "a");
        AddTab(service, g1.Id, @"C:\b", "b");
        AddTab(service, g2.Id, @"C:\x", "x");

        var result = service.MoveTabToGroup(a.Id, g2.Id, 99);

        Assert.True(result.IsSuccess);
        Assert.Equal(["x", "a"], g2.Tabs.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void MoveTabToGroup_移動先が上限のときは移動せず失敗し状態を変えない()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var a = AddTab(service, g1.Id, @"C:\a", "a");
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            AddTab(service, g2.Id, $@"C:\g2-{i}", $"g2-{i}");
        }

        var result = service.MoveTabToGroup(a.Id, g2.Id, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.TabLimitReached, result.Error);
        Assert.Single(g1.Tabs);
        Assert.Same(a, g1.Tabs[0]);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, g2.Tabs.Count);
    }

    [Fact]
    public void MoveTabToGroup_移動元が空になってもグループは維持される()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var only = AddTab(service, g1.Id, @"C:\only", "only");
        AddTab(service, g2.Id, @"C:\x", "x");

        var result = service.MoveTabToGroup(only.Id, g2.Id, 1);

        Assert.True(result.IsSuccess);
        Assert.Empty(g1.Tabs);
        Assert.Equal(2, service.Groups.Count); // 空になった g1 も残る
        Assert.Contains(service.Groups, g => g.Id == g1.Id);
        Assert.Null(g1.SelectedTabId);
    }

    [Fact]
    public void MoveTabToGroup_アクティブタブを移動すると移動先グループで引き続きアクティブになる()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var a = AddTab(service, g1.Id, @"C:\a", "a");
        AddTab(service, g1.Id, @"C:\b", "b");
        AddTab(service, g2.Id, @"C:\x", "x");
        service.SetActiveTab(a.Id);

        service.MoveTabToGroup(a.Id, g2.Id, 0);

        Assert.Equal(a.Id, service.ActiveTabId);
        Assert.Equal(g2.Id, service.ActiveGroupId);
        Assert.Equal(a.Id, g2.SelectedTabId);
    }

    [Fact]
    public void MoveTabToGroup_移動元の選択タブが移動した場合は隣のタブへ追従する()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var a = AddTab(service, g1.Id, @"C:\a", "a");
        var b = AddTab(service, g1.Id, @"C:\b", "b");
        AddTab(service, g2.Id, @"C:\x", "x");
        // g1 の選択タブを a にしてから g2 の x をアクティブにする(g1 の選択タブは a のまま残る)
        service.SetActiveTab(a.Id);
        service.SetActiveTab(g2.Tabs[0].Id);
        Assert.Equal(a.Id, g1.SelectedTabId);

        service.MoveTabToGroup(a.Id, g2.Id, 0);

        Assert.Equal(b.Id, g1.SelectedTabId); // 隣(次)の b へ追従
    }

    [Fact]
    public void MoveTabToGroup_存在しないタブやグループは失敗する()
    {
        var service = new TabManagerService();
        var g1 = AddGroup(service, "作業1");
        var g2 = AddGroup(service, "作業2");
        var a = AddTab(service, g1.Id, @"C:\a", "a");

        Assert.Equal(
            TabOperationError.TabNotFound,
            service.MoveTabToGroup("no-such-tab", g2.Id, 0).Error);
        Assert.Equal(
            TabOperationError.GroupNotFound,
            service.MoveTabToGroup(a.Id, "no-such-group", 0).Error);
    }
}
