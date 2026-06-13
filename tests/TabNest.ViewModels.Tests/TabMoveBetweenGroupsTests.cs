using TabNest.Core.Models;
using TabNest.Core.Services;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>グループ間のタブ移動(グループ間 D&amp;D。Task 7-2)のテスト。</summary>
public class TabMoveBetweenGroupsTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static MainViewModel Create()
    {
        var stub = new StubFileSystemService();
        stub.Setup(UserProfile, FolderListingResult.Success([]));
        var vm = new MainViewModel(stub, new SpyFileLauncher());
        vm.AddGroupWithDefaultTab(); // 作業2 を追加(2グループ・各1タブ)
        return vm;
    }

    [Fact]
    public void MoveTabToGroup_別グループの指定位置へタブが移動し表示順が更新される()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];
        var moved = g1.Tabs[0];

        var ok = vm.MoveTabToGroup(moved, g2.Id, 0);

        Assert.True(ok);
        Assert.DoesNotContain(moved, g1.Tabs);
        Assert.Same(moved, g2.Tabs[0]);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void MoveTabToGroup_移動元が空になってもグループは維持される()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];
        var only = g1.Tabs[0]; // g1 は1タブのみ

        var ok = vm.MoveTabToGroup(only, g2.Id, g2.Tabs.Count);

        Assert.True(ok);
        Assert.Empty(g1.Tabs);
        Assert.Equal(2, vm.Groups.Count); // 空の g1 も残る
        Assert.Contains(g1, vm.Groups);
    }

    [Fact]
    public void MoveTabToGroup_移動先が上限のときは移動せずエラーを表示する()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];
        var moved = g1.Tabs[0];
        // g2 を上限(20)まで埋める
        vm.SelectTab(g2.Tabs[0]);
        for (var i = g2.Tabs.Count; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            Assert.True(vm.AddTabToActiveGroup());
        }
        Assert.Equal(TabManagerService.MaxTabsPerGroup, g2.Tabs.Count);

        var ok = vm.MoveTabToGroup(moved, g2.Id, 0);

        Assert.False(ok);
        Assert.Contains(moved, g1.Tabs); // 移動元に残る
        Assert.Equal(TabManagerService.MaxTabsPerGroup, g2.Tabs.Count);
        Assert.NotNull(vm.OperationError);
    }

    [Fact]
    public void MoveTabToGroup_アクティブタブを移動すると移動先で引き続きアクティブになる()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];
        var moved = g1.Tabs[0];
        vm.SelectTab(moved);
        Assert.True(moved.IsActive);

        var ok = vm.MoveTabToGroup(moved, g2.Id, 0);

        Assert.True(ok);
        Assert.True(moved.IsActive);
        Assert.Same(moved, g2.Tabs[0]);
    }

    [Fact]
    public void MoveTabToGroup_同一グループへの移動は受け付けない()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var moved = g1.Tabs[0];

        var ok = vm.MoveTabToGroup(moved, g1.Id, 0);

        Assert.False(ok);
        Assert.Contains(moved, g1.Tabs);
    }

    [Fact]
    public void MoveTabFromOtherGroup_TabGroupViewModel経由でも移動できる()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];
        var moved = g1.Tabs[0];

        // View が呼ぶ経路(ドロップ先グループの VM に対して呼ぶ)を再現する
        var ok = g2.MoveTabFromOtherGroup(moved, g2.Tabs.Count);

        Assert.True(ok);
        Assert.DoesNotContain(moved, g1.Tabs);
        Assert.Same(moved, g2.Tabs[^1]);
    }
}
