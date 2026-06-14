using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Core.Tests;

/// <summary>閉じたタブ履歴と復元ルールのテスト(Task 3-6)。</summary>
public class ClosedTabTests
{
    private static TabGroup AddGroup(TabManagerService service, string name)
        => service.AddGroup(name).Value!;

    private static FolderTab AddTab(TabManagerService service, string groupId, string path)
        => service.AddTab(groupId, path, System.IO.Path.GetFileName(path)).Value!;

    [Fact]
    public void タブを閉じるとClosedTabsに追加される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var a = AddTab(service, group.Id, @"C:\a");
        var b = AddTab(service, group.Id, @"C:\b");

        service.CloseTab(a.Id);

        var closed = Assert.Single(service.ClosedTabs);
        Assert.Equal(@"C:\a", closed.Path);
        Assert.Equal("a", closed.Title);
        Assert.Equal(group.Id, closed.GroupId);
        Assert.Equal(0, closed.TabIndex);
    }

    [Fact]
    public void RestoreClosedTab_元のグループの同位置へ復元される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var a = AddTab(service, group.Id, @"C:\a");
        var b = AddTab(service, group.Id, @"C:\b");
        var c = AddTab(service, group.Id, @"C:\c");
        service.CloseTab(b.Id);
        Assert.Equal([@"C:\a", @"C:\c"], group.Tabs.Select(t => t.Path).ToArray());

        var result = service.RestoreClosedTab();

        Assert.True(result.IsSuccess);
        Assert.Equal([@"C:\a", @"C:\b", @"C:\c"], group.Tabs.Select(t => t.Path).ToArray());
        Assert.Equal(result.Value!.Id, service.ActiveTabId);
    }

    [Fact]
    public void RestoreClosedTab_復元後にClosedTabsから削除される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        var a = AddTab(service, group.Id, @"C:\a");
        AddTab(service, group.Id, @"C:\b");
        service.CloseTab(a.Id);
        Assert.Single(service.ClosedTabs);

        service.RestoreClosedTab();

        Assert.Empty(service.ClosedTabs);
    }

    [Fact]
    public void RestoreClosedTab_履歴が空のときはNoClosedTab()
    {
        var service = new TabManagerService();
        AddGroup(service, "作業1");

        var result = service.RestoreClosedTab();

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.NoClosedTab, result.Error);
    }

    [Fact]
    public void RestoreClosedTab_元グループ消滅時はアクティブグループ末尾へ復元される()
    {
        var service = new TabManagerService();
        var group1 = AddGroup(service, "作業1");
        var group2 = AddGroup(service, "作業2");
        var keep = AddTab(service, group1.Id, @"C:\keep");
        var doomed = AddTab(service, group2.Id, @"C:\doomed");
        service.CloseTab(doomed.Id);
        service.RemoveGroup(group2.Id);
        service.SetActiveTab(keep.Id);

        var result = service.RestoreClosedTab();

        Assert.True(result.IsSuccess);
        Assert.Equal([@"C:\keep", @"C:\doomed"], group1.Tabs.Select(t => t.Path).ToArray());
        Assert.Empty(service.ClosedTabs);
    }

    [Fact]
    public void RestoreClosedTab_元グループが上限のときはアクティブグループへ復元される()
    {
        var service = new TabManagerService();
        var fullGroup = AddGroup(service, "作業1");
        var activeGroup = AddGroup(service, "作業2");
        AddTab(service, fullGroup.Id, @"C:\keep"); // 最後の1タブガード回避用に残す
        var victim = AddTab(service, fullGroup.Id, @"C:\victim");
        service.CloseTab(victim.Id);
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            AddTab(service, fullGroup.Id, $@"C:\f{i}");
        }
        var activeTab = AddTab(service, activeGroup.Id, @"C:\active");
        service.SetActiveTab(activeTab.Id);

        var result = service.RestoreClosedTab();

        Assert.True(result.IsSuccess);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, fullGroup.Tabs.Count);
        Assert.Equal([@"C:\active", @"C:\victim"], activeGroup.Tabs.Select(t => t.Path).ToArray());
    }

    [Fact]
    public void RestoreClosedTab_TabIndexが範囲外なら末尾に追加される()
    {
        // 空グループ + 閉じたタブ履歴をセッションから復元して再現する。
        // (Task 6-6 以降、CloseTab では最後の1タブが残るため空グループは作れない。
        //  空グループは RestoreSession / D&D 移動でのみ生じる正規の状態)
        var service = new TabManagerService();
        var group = new TabGroup { Id = "g1", Name = "作業1" }; // タブ0個
        var settings = new AppSettings
        {
            TabGroups = { group },
            ClosedTabs =
            {
                // 履歴は古い順(末尾が最後に閉じたタブ)。c → a → b の順で閉じた想定
                new ClosedTab { Path = @"C:\c", Title = "c", GroupId = "g1", TabIndex = 2 },
                new ClosedTab { Path = @"C:\a", Title = "a", GroupId = "g1", TabIndex = 0 },
                new ClosedTab { Path = @"C:\b", Title = "b", GroupId = "g1", TabIndex = 1 },
            },
        };
        Assert.True(service.RestoreSession(settings));
        Assert.Empty(group.Tabs);

        // 最後に閉じた b(TabIndex=1)から復元 → 空グループでは範囲外なので末尾(先頭)
        var first = service.RestoreClosedTab();
        Assert.True(first.IsSuccess);
        Assert.Equal([@"C:\b"], group.Tabs.Select(t => t.Path).ToArray());

        // 次は a(TabIndex=0)→ 範囲内なので先頭へ
        var second = service.RestoreClosedTab();
        Assert.True(second.IsSuccess);
        Assert.Equal([@"C:\a", @"C:\b"], group.Tabs.Select(t => t.Path).ToArray());

        // 最後は c(TabIndex=2)→ 範囲内(タブ数2)なので位置2(末尾)へ
        var third = service.RestoreClosedTab();
        Assert.True(third.IsSuccess);
        Assert.Equal([@"C:\a", @"C:\b", @"C:\c"], group.Tabs.Select(t => t.Path).ToArray());
    }

    [Fact]
    public void ClosedTabsは20件を超えると古いものから破棄される()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        AddTab(service, group.Id, @"C:\keep"); // 常に残すタブ(最後の1タブガード回避)
        for (var i = 0; i < TabManagerService.MaxClosedTabs + 5; i++)
        {
            var tab = AddTab(service, group.Id, $@"C:\t{i}");
            service.CloseTab(tab.Id);
        }

        Assert.Equal(TabManagerService.MaxClosedTabs, service.ClosedTabs.Count);
        // 最初の5件(t0〜t4)が破棄され、t5 が最古になっている
        Assert.Equal(@"C:\t5", service.ClosedTabs[0].Path);
        Assert.Equal($@"C:\t{TabManagerService.MaxClosedTabs + 4}", service.ClosedTabs[^1].Path);
    }

    [Fact]
    public void RestoreClosedTab_復元先のアクティブグループも上限なら失敗し履歴は消費されない()
    {
        var service = new TabManagerService();
        var group = AddGroup(service, "作業1");
        AddTab(service, group.Id, @"C:\keep"); // 最後の1タブガード回避用に残す
        var victim = AddTab(service, group.Id, @"C:\victim");
        service.CloseTab(victim.Id);
        for (var i = 0; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            AddTab(service, group.Id, $@"C:\f{i}");
        }

        var result = service.RestoreClosedTab();

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.TabLimitReached, result.Error);
        Assert.Single(service.ClosedTabs); // 履歴は消費されない
    }
}
