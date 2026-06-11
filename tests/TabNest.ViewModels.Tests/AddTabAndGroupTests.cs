using TabNest.Core.Models;
using TabNest.Core.Services;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>タブ追加・グループ追加(ボタン / Ctrl+T / Ctrl+G)のテスト(Task 3-10)。</summary>
public class AddTabAndGroupTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static (MainViewModel Vm, StubFileSystemService Stub) Create()
    {
        var stub = new StubFileSystemService();
        stub.Setup(UserProfile, FolderListingResult.Success([]));
        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void AddTabToActiveGroup_アクティブグループ末尾にUserProfileタブが追加される()
    {
        var (vm, _) = Create();

        var ok = vm.AddTabToActiveGroup();

        Assert.True(ok);
        Assert.Equal(2, vm.Groups[0].Tabs.Count);
        var added = vm.Groups[0].Tabs[^1];
        Assert.Equal(UserProfile, added.Path);
        Assert.True(added.IsActive);
        Assert.Equal(UserProfile, vm.Folder.CurrentPath);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void AddGroupWithDefaultTab_作業2が追加され初期タブはUserProfile()
    {
        var (vm, _) = Create();

        var ok = vm.AddGroupWithDefaultTab();

        Assert.True(ok);
        Assert.Equal(2, vm.Groups.Count);
        var group = vm.Groups[1];
        Assert.Equal("作業2", group.Name);
        var tab = Assert.Single(group.Tabs);
        Assert.Equal(UserProfile, tab.Path);
        Assert.True(tab.IsActive);
    }

    [Fact]
    public void GenerateGroupName_作業N完全一致の最大値プラス1()
    {
        var (vm, _) = Create();
        vm.AddGroupWithDefaultTab(); // 作業2
        vm.Groups[1].BeginRename();
        vm.Groups[1].EditingName = "作業7";
        vm.Groups[1].CommitRename();

        Assert.Equal("作業8", vm.GenerateGroupName());
    }

    [Fact]
    public void GenerateGroupName_完全一致しない名前は採番対象外()
    {
        var (vm, _) = Create();
        // 作業1 を「作業1 (2)」へリネーム → 「作業<整数>」完全一致なし → 作業1
        vm.Groups[0].BeginRename();
        vm.Groups[0].EditingName = "作業1 (2)";
        vm.Groups[0].CommitRename();

        Assert.Equal("作業1", vm.GenerateGroupName());
    }

    [Fact]
    public void GenerateGroupName_任意名とお気に入り由来名が混在しても衝突しない()
    {
        var (vm, _) = Create();
        vm.AddGroupWithDefaultTab(); // 作業2
        vm.AddGroupWithDefaultTab(); // 作業3
        // 作業2 を任意名にリネーム(採番対象から外れる)
        vm.Groups[1].BeginRename();
        vm.Groups[1].EditingName = "リリース準備";
        vm.Groups[1].CommitRename();

        // 残る完全一致は 作業1, 作業3 → 次は 作業4
        Assert.Equal("作業4", vm.GenerateGroupName());
    }

    [Fact]
    public void グループ5段到達時はエラーを表示して追加しない()
    {
        var (vm, _) = Create();
        for (var i = 1; i < TabManagerService.MaxGroups; i++)
        {
            Assert.True(vm.AddGroupWithDefaultTab());
        }
        Assert.Equal(5, vm.Groups.Count);

        var ok = vm.AddGroupWithDefaultTab();

        Assert.False(ok);
        Assert.Equal(5, vm.Groups.Count);
        Assert.NotNull(vm.OperationError);
    }

    [Fact]
    public void タブ20個到達時はエラーを表示して追加しない()
    {
        var (vm, _) = Create();
        for (var i = vm.Groups[0].Tabs.Count; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            Assert.True(vm.AddTabToActiveGroup());
        }

        var ok = vm.AddTabToActiveGroup();

        Assert.False(ok);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, vm.Groups[0].Tabs.Count);
        Assert.NotNull(vm.OperationError);
    }

    [Fact]
    public void 成功するとOperationErrorがクリアされる()
    {
        var (vm, _) = Create();
        for (var i = 1; i < TabManagerService.MaxGroups; i++)
        {
            vm.AddGroupWithDefaultTab();
        }
        vm.AddGroupWithDefaultTab(); // 6段目 → エラー
        Assert.NotNull(vm.OperationError);

        vm.AddTabToActiveGroup(); // 成功する操作

        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void グループ名編集中はCtrlTとCtrlGが無効()
    {
        var (vm, _) = Create();
        vm.Groups[0].BeginRename();
        var tabCount = vm.Groups[0].Tabs.Count;
        var groupCount = vm.Groups.Count;

        Assert.False(vm.AddTabToActiveGroup());
        Assert.False(vm.AddGroupWithDefaultTab());

        Assert.Equal(tabCount, vm.Groups[0].Tabs.Count);
        Assert.Equal(groupCount, vm.Groups.Count);
        Assert.True(vm.Groups[0].IsEditingName); // 編集状態は維持される
    }

    [Fact]
    public void 新グループ追加後の移動は新グループのタブに反映される()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\elsewhere", FolderListingResult.Success([]));
        vm.AddGroupWithDefaultTab();
        var newGroupTab = vm.Groups[1].Tabs[0];
        var firstGroupTab = vm.Groups[0].Tabs[0];

        vm.Folder.LoadFolder(@"C:\elsewhere");

        Assert.Equal(@"C:\elsewhere", newGroupTab.Path);
        Assert.NotEqual(@"C:\elsewhere", firstGroupTab.Path);
    }
}
