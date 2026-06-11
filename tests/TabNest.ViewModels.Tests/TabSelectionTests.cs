using TabNest.Core.Models;
using TabNest.Core.Services;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

public class TabSelectionTests
{
    private static (MainViewModel Vm, StubFileSystemService Stub) Create()
    {
        var stub = new StubFileSystemService();
        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void 初期状態では最初のタブがアクティブ()
    {
        var (vm, _) = Create();

        var tab = Assert.Single(vm.Groups[0].Tabs);
        Assert.True(tab.IsActive);
    }

    [Fact]
    public void AddTab_追加したタブがアクティブになり既存タブは非アクティブになる()
    {
        var (vm, _) = Create();
        var groupId = vm.Groups[0].Id;
        var first = vm.Groups[0].Tabs[0];

        var second = vm.AddTab(groupId, @"C:\work");

        Assert.NotNull(second);
        Assert.Equal(2, vm.Groups[0].Tabs.Count);
        Assert.False(first.IsActive);
        Assert.True(second!.IsActive);
        Assert.Equal("work", second.Title);
    }

    [Fact]
    public void AddTab_存在しないグループはnull()
    {
        var (vm, _) = Create();

        Assert.Null(vm.AddTab("no-such-group", @"C:\work"));
    }

    [Fact]
    public void SelectTab_アクティブが切り替わりフォルダ内容が読み込まれる()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\a", FolderListingResult.Success(
        [
            new FileSystemEntry { Name = "a.txt", FullPath = @"C:\a\a.txt", IsDirectory = false, SizeInBytes = 1 },
        ]));
        stub.Setup(@"C:\b", FolderListingResult.Success([]));
        var groupId = vm.Groups[0].Id;
        var tabA = vm.AddTab(groupId, @"C:\a")!;
        var tabB = vm.AddTab(groupId, @"C:\b")!;

        var ok = vm.SelectTab(tabA);

        Assert.True(ok);
        Assert.True(tabA.IsActive);
        Assert.False(tabB.IsActive);
        Assert.Equal(@"C:\a", vm.Folder.CurrentPath);
        Assert.Single(vm.Folder.Items);

        vm.SelectTab(tabB);

        Assert.False(tabA.IsActive);
        Assert.True(tabB.IsActive);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
        Assert.Empty(vm.Folder.Items);
    }

    [Fact]
    public void SelectTab_読み込み失敗でもタブ選択は維持されエラーが表示される()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\ok", FolderListingResult.Success([]));
        stub.Setup(@"C:\broken", FolderListingResult.Failure("フォルダが見つかりません: C:\\broken"));
        var groupId = vm.Groups[0].Id;
        var okTab = vm.AddTab(groupId, @"C:\ok")!;
        var brokenTab = vm.AddTab(groupId, @"C:\broken")!;
        vm.SelectTab(okTab);

        var selected = vm.SelectTab(brokenTab);

        Assert.True(selected);
        Assert.True(brokenTab.IsActive);
        Assert.False(okTab.IsActive);
        // フォルダ内容は読み込めないため直前の状態が維持され、エラーが表示される
        Assert.Equal(@"C:\ok", vm.Folder.CurrentPath);
        Assert.NotNull(vm.Folder.ErrorMessage);
    }

    [Fact]
    public void 上限到達時のAddTabはnullで状態が壊れない()
    {
        var (vm, _) = Create();
        var groupId = vm.Groups[0].Id;
        for (var i = vm.Groups[0].Tabs.Count; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            Assert.NotNull(vm.AddTab(groupId, $@"C:\f{i}"));
        }

        var over = vm.AddTab(groupId, @"C:\over");

        Assert.Null(over);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, vm.Groups[0].Tabs.Count);
        Assert.Equal(1, vm.Groups[0].Tabs.Count(t => t.IsActive));
    }
}
