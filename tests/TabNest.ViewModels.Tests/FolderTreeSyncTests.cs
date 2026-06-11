using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>ツリーとタブ移動の双方向同期(MainViewModel 経由)のテスト(Task 3-9)。</summary>
public class FolderTreeSyncTests
{
    private static (MainViewModel Vm, StubFileSystemService Stub) Create()
    {
        var stub = new StubFileSystemService();
        stub.DriveRoots.Add(@"C:\");
        stub.Setup(@"C:\", FolderListingResult.Success(
        [
            new FileSystemEntry { Name = "work", FullPath = @"C:\work", IsDirectory = true },
            new FileSystemEntry { Name = "Users", FullPath = @"C:\Users", IsDirectory = true },
        ]));
        stub.Setup(@"C:\work", FolderListingResult.Success([]));
        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void ツリーのノード選択でアクティブタブが移動し履歴にも積まれる()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\start", FolderListingResult.Success([]));
        var tab = vm.AddTab(vm.Groups[0].Id, @"C:\start")!;
        vm.Tree.Roots[0].IsExpanded = true;
        var workNode = vm.Tree.Roots[0].Children.First(c => c.Name == "work");

        vm.Tree.ActivateNode(workNode);

        Assert.Equal(@"C:\work", vm.Folder.CurrentPath);
        Assert.Equal(@"C:\work", tab.Path);
        Assert.Equal("work", tab.Title);
        Assert.True(vm.Folder.CanGoBack); // ツリー起点の移動も履歴に積む
        vm.Folder.GoBack();
        Assert.Equal(@"C:\start", vm.Folder.CurrentPath);
    }

    [Fact]
    public void ファイル一覧側の移動にツリー選択が追従する()
    {
        var (vm, _) = Create();
        vm.AddTab(vm.Groups[0].Id, @"C:\");

        vm.Folder.LoadFolder(@"C:\work");

        var root = vm.Tree.Roots[0];
        Assert.True(root.IsExpanded);
        var workNode = root.Children.First(c => c.Name == "work");
        Assert.True(workNode.IsSelected);
    }

    [Fact]
    public void タブ切替でもツリー選択が追従する()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\Users", FolderListingResult.Success([]));
        var groupId = vm.Groups[0].Id;
        var tabWork = vm.AddTab(groupId, @"C:\work")!;
        var tabUsers = vm.AddTab(groupId, @"C:\Users")!;

        vm.SelectTab(tabWork);
        Assert.True(vm.Tree.Roots[0].Children.First(c => c.Name == "work").IsSelected);

        vm.SelectTab(tabUsers);
        Assert.True(vm.Tree.Roots[0].Children.First(c => c.Name == "Users").IsSelected);
        Assert.False(vm.Tree.Roots[0].Children.First(c => c.Name == "work").IsSelected);
    }

    [Fact]
    public void 追従できないパスでは選択解除のみで移動は継続する()
    {
        var (vm, stub) = Create();
        stub.Setup(@"D:\elsewhere", FolderListingResult.Success([]));
        vm.AddTab(vm.Groups[0].Id, @"C:\work");

        var ok = vm.Folder.LoadFolder(@"D:\elsewhere");

        Assert.True(ok);
        Assert.Equal(@"D:\elsewhere", vm.Folder.CurrentPath);
        Assert.DoesNotContain(
            vm.Tree.Roots.SelectMany(r => r.Children).Concat(vm.Tree.Roots),
            n => n.IsSelected);
    }
}
