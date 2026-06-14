using TabNest.Core.Models;
using TabNest.Core.Services;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>フォルダのホイールクリックで新規タブを開く処理のテスト(Task 8-4)。</summary>
public class OpenFolderInNewTabTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static (MainViewModel Vm, StubFileSystemService Stub) Create()
    {
        var stub = new StubFileSystemService();
        stub.Setup(UserProfile, FolderListingResult.Success([]));
        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    private static FileItemViewModel Folder(string path)
        => new(new FileSystemEntry { Name = Path.GetFileName(path), FullPath = path, IsDirectory = true });

    private static FileItemViewModel File(string path)
        => new(new FileSystemEntry { Name = Path.GetFileName(path), FullPath = path, IsDirectory = false });

    [Fact]
    public void フォルダの中クリックでアクティブグループ末尾に新規タブが開く()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\target", FolderListingResult.Success([]));

        var ok = vm.OpenFolderInNewTab(Folder(@"C:\target"));

        Assert.True(ok);
        Assert.Equal(2, vm.Groups[0].Tabs.Count);
        var added = vm.Groups[0].Tabs[^1];
        Assert.Equal(@"C:\target", added.Path);
        Assert.Equal("target", added.Title);
        Assert.True(added.IsActive);
        Assert.Equal(@"C:\target", vm.Folder.CurrentPath);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void ファイルの中クリックでは何もしない()
    {
        var (vm, _) = Create();

        var ok = vm.OpenFolderInNewTab(File(@"C:\target\memo.txt"));

        Assert.False(ok);
        Assert.Single(vm.Groups[0].Tabs);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void 新規タブは現在アクティブなグループへ追加される()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\target", FolderListingResult.Success([]));
        // 2段目を追加するとそのグループがアクティブになる
        vm.AddGroupWithDefaultTab();
        Assert.Equal(2, vm.Groups.Count);

        var ok = vm.OpenFolderInNewTab(Folder(@"C:\target"));

        Assert.True(ok);
        // アクティブな2段目の末尾に追加され、1段目は変化しない
        Assert.Single(vm.Groups[0].Tabs);
        Assert.Equal(2, vm.Groups[1].Tabs.Count);
        Assert.Equal(@"C:\target", vm.Groups[1].Tabs[^1].Path);
    }

    [Fact]
    public void タブ20個到達時は追加されず状態が壊れない()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\target", FolderListingResult.Success([]));
        for (var i = vm.Groups[0].Tabs.Count; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            Assert.True(vm.AddTabToActiveGroup());
        }
        Assert.Equal(TabManagerService.MaxTabsPerGroup, vm.Groups[0].Tabs.Count);

        var ok = vm.OpenFolderInNewTab(Folder(@"C:\target"));

        Assert.False(ok);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, vm.Groups[0].Tabs.Count);
        Assert.DoesNotContain(vm.Groups[0].Tabs, t => t.Path == @"C:\target");
        Assert.NotNull(vm.OperationError);
    }
}
