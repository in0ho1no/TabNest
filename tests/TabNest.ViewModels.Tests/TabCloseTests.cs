using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

public class TabCloseTests
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
    public void CloseTab_タブが一覧から消える()
    {
        var (vm, _) = Create(@"C:\a");
        var groupId = vm.Groups[0].Id;
        var tab = vm.AddTab(groupId, @"C:\a")!;
        Assert.Equal(2, vm.Groups[0].Tabs.Count);

        var ok = vm.CloseTab(tab);

        Assert.True(ok);
        Assert.Single(vm.Groups[0].Tabs);
        Assert.DoesNotContain(tab, vm.Groups[0].Tabs);
    }

    [Fact]
    public void CloseTab_アクティブタブを閉じると隣のタブの内容が表示される()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b", @"C:\c");
        var groupId = vm.Groups[0].Id;
        var a = vm.AddTab(groupId, @"C:\a")!;
        var b = vm.AddTab(groupId, @"C:\b")!;
        var c = vm.AddTab(groupId, @"C:\c")!;
        vm.SelectTab(b);

        vm.CloseTab(b);

        // 次のタブ(c)がアクティブになり、その内容が表示される
        Assert.True(c.IsActive);
        Assert.False(a.IsActive);
        Assert.Equal(@"C:\c", vm.Folder.CurrentPath);
    }

    [Fact]
    public void CloseTab_非アクティブタブを閉じても表示は変わらない()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        var groupId = vm.Groups[0].Id;
        var a = vm.AddTab(groupId, @"C:\a")!;
        var b = vm.AddTab(groupId, @"C:\b")!;
        vm.SelectTab(b);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);

        vm.CloseTab(a);

        Assert.True(b.IsActive);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
        Assert.Equal(2, vm.Groups[0].Tabs.Count); // 初期タブ + b
    }

    [Fact]
    public void CloseTab_アプリ内最後の1タブは閉じられない()
    {
        var (vm, _) = Create();
        var initial = Assert.Single(vm.Groups[0].Tabs);

        var ok = vm.CloseTab(initial);

        Assert.False(ok);
        Assert.Single(vm.Groups[0].Tabs); // タブは残る
        Assert.Same(initial, vm.Groups[0].Tabs[0]);
        Assert.Single(vm.Groups); // グループも残る
    }

    [Fact]
    public void CloseTab_グループの最後のタブを閉じると空グループも閉じる()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var (vm, _) = Create(userProfile);
        Assert.True(vm.AddGroupWithDefaultTab()); // 「作業2」を追加、初期タブ1個
        var group2 = vm.Groups[1];
        var only = Assert.Single(group2.Tabs);

        var ok = vm.CloseTab(only);

        Assert.True(ok);
        // group2 は空になったので自動クローズされ、表示からも消える
        Assert.Single(vm.Groups);
        Assert.DoesNotContain(group2, vm.Groups);
    }

    [Fact]
    public void CloseTab_既に閉じたタブの再クローズはfalseで状態が壊れない()
    {
        var (vm, _) = Create(@"C:\a");
        var groupId = vm.Groups[0].Id;
        var tab = vm.AddTab(groupId, @"C:\a")!;
        vm.CloseTab(tab);
        var countAfterFirst = vm.Groups[0].Tabs.Count;

        var ok = vm.CloseTab(tab);

        Assert.False(ok);
        Assert.Equal(countAfterFirst, vm.Groups[0].Tabs.Count);
    }

    [Fact]
    public void SelectTab_閉じたタブの選択はfalseで状態が壊れない()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        var groupId = vm.Groups[0].Id;
        var a = vm.AddTab(groupId, @"C:\a")!;
        var b = vm.AddTab(groupId, @"C:\b")!;
        vm.SelectTab(b);
        vm.CloseTab(a);

        // 閉じたタブへの遅延クリック(競合)を想定
        var ok = vm.SelectTab(a);

        Assert.False(ok);
        Assert.True(b.IsActive);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
    }
}
