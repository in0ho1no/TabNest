using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

public class TabRestoreTests
{
    private static (MainViewModel Vm, StubFileSystemService Stub) Create(params string[] paths)
    {
        var stub = new StubFileSystemService();
        foreach (var path in paths)
        {
            stub.Setup(path, FolderListingResult.Success([]));
        }

        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void RestoreClosedTab_閉じたタブが同じ位置に復元され内容が表示される()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        var groupId = vm.Groups[0].Id;
        var a = vm.AddTab(groupId, @"C:\a")!;
        var b = vm.AddTab(groupId, @"C:\b")!;
        vm.CloseTab(a);
        Assert.Equal(2, vm.Groups[0].Tabs.Count);

        var ok = vm.RestoreClosedTab();

        Assert.True(ok);
        Assert.Equal(3, vm.Groups[0].Tabs.Count);
        // a は位置1(初期タブの次)に復元される
        var restored = vm.Groups[0].Tabs[1];
        Assert.Equal(@"C:\a", restored.Path);
        Assert.True(restored.IsActive);
        Assert.Equal(@"C:\a", vm.Folder.CurrentPath);
    }

    [Fact]
    public void RestoreClosedTab_履歴がなければfalse()
    {
        var (vm, _) = Create();

        Assert.False(vm.RestoreClosedTab());
    }

    [Fact]
    public void RestoreClosedTab_グループ名編集中は何も実行しない()
    {
        var (vm, _) = Create(@"C:\a");
        var groupId = vm.Groups[0].Id;
        var a = vm.AddTab(groupId, @"C:\a")!;
        vm.CloseTab(a);
        vm.Groups[0].BeginRename();
        var tabCount = vm.Groups[0].Tabs.Count;

        var ok = vm.RestoreClosedTab();

        Assert.False(ok);
        Assert.Equal(tabCount, vm.Groups[0].Tabs.Count);
        Assert.True(vm.Groups[0].IsEditingName); // 編集状態が維持される
    }

    [Fact]
    public void RestoreClosedTab_編集確定後は復元できる()
    {
        var (vm, _) = Create(@"C:\a");
        var groupId = vm.Groups[0].Id;
        var a = vm.AddTab(groupId, @"C:\a")!;
        vm.CloseTab(a);
        vm.Groups[0].BeginRename();
        Assert.False(vm.RestoreClosedTab());
        vm.Groups[0].CommitRename();

        var ok = vm.RestoreClosedTab();

        Assert.True(ok);
        Assert.Contains(vm.Groups[0].Tabs, t => t.Path == @"C:\a");
    }
}
