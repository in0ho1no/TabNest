using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>Task 6-1: MainViewModel のグループ削除(右クリックメニュー経由)を検証する。</summary>
public class GroupRemoveTests
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
    public void RemoveGroup_グループが一覧から消える()
    {
        var (vm, _) = Create();
        Assert.True(vm.AddGroupWithDefaultTab()); // 作業2 を追加
        var target = vm.Groups[1];
        Assert.Equal(2, vm.Groups.Count);

        var ok = vm.RemoveGroup(target.Id);

        Assert.True(ok);
        Assert.Single(vm.Groups);
        Assert.DoesNotContain(target, vm.Groups);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void RemoveGroup_最後の1グループは削除できずエラーになる()
    {
        var (vm, _) = Create();
        var only = Assert.Single(vm.Groups);

        var ok = vm.RemoveGroup(only.Id);

        Assert.False(ok);
        Assert.Single(vm.Groups); // 状態は壊れない
        Assert.Contains(only, vm.Groups);
        Assert.NotNull(vm.OperationError);
    }

    [Fact]
    public void RemoveGroup_アクティブグループを削除すると別グループの内容が表示される()
    {
        var (vm, _) = Create(@"C:\a");
        // 作業1 のタブを C:\a にしておく
        vm.Folder.LoadFolder(@"C:\a");
        var first = vm.Groups[0];
        Assert.True(vm.AddGroupWithDefaultTab()); // 作業2(これがアクティブになる)
        var second = vm.Groups[1];
        Assert.True(second.Tabs[0].IsActive);

        var ok = vm.RemoveGroup(second.Id);

        Assert.True(ok);
        // アクティブが 作業1 のタブへ移り、その内容が表示される
        Assert.True(first.Tabs[0].IsActive);
        Assert.Equal(@"C:\a", vm.Folder.CurrentPath);
    }

    [Fact]
    public void RemoveGroup_非アクティブグループを削除しても表示は変わらない()
    {
        var (vm, _) = Create(@"C:\a");
        var first = vm.Groups[0]; // アクティブ(初期タブ %UserProfile%)
        Assert.True(vm.AddGroupWithDefaultTab()); // 作業2
        var second = vm.Groups[1];
        // アクティブを 作業1 に戻す
        vm.SelectTab(first.Tabs[0]);
        var pathBefore = vm.Folder.CurrentPath;

        var ok = vm.RemoveGroup(second.Id);

        Assert.True(ok);
        Assert.True(first.Tabs[0].IsActive);
        Assert.Equal(pathBefore, vm.Folder.CurrentPath);
    }

    [Fact]
    public void RemoveGroup_削除したグループのタブはClosedTab履歴へ積まれない()
    {
        var (vm, _) = Create();
        Assert.True(vm.AddGroupWithDefaultTab()); // 作業2(タブ1個)
        var second = vm.Groups[1];

        Assert.True(vm.RemoveGroup(second.Id));

        // グループ削除では履歴へ積まないため、復元できるタブは無い
        Assert.False(vm.RestoreClosedTab());
        Assert.Single(vm.Groups);
    }

    [Fact]
    public void RemoveGroup_存在しないグループの削除はエラーになる()
    {
        var (vm, _) = Create();
        Assert.True(vm.AddGroupWithDefaultTab());

        var ok = vm.RemoveGroup("no-such-group");

        Assert.False(ok);
        Assert.Equal(2, vm.Groups.Count);
        Assert.NotNull(vm.OperationError);
    }
}
