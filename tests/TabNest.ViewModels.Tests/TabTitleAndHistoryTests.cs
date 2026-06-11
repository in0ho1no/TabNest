using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>タブタイトルの動的更新とタブ別履歴のテスト(Task 3-7)。</summary>
public class TabTitleAndHistoryTests
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
    public void フォルダ移動でアクティブタブのPathとTitleが更新される()
    {
        var (vm, _) = Create(@"C:\a", @"C:\work\src");
        var tab = vm.AddTab(vm.Groups[0].Id, @"C:\a")!;

        vm.Folder.LoadFolder(@"C:\work\src");

        Assert.Equal(@"C:\work\src", tab.Path);
        Assert.Equal("src", tab.Title);
    }

    [Fact]
    public void ドライブルートへ移動するとタイトルはドライブ表記になる()
    {
        var (vm, _) = Create(@"C:\a", @"C:\");
        var tab = vm.AddTab(vm.Groups[0].Id, @"C:\a")!;

        vm.Folder.LoadFolder(@"C:\");

        Assert.Equal(@"C:\", tab.Path);
        Assert.Equal(@"C:\", tab.Title);
    }

    [Fact]
    public void 上へ戻る進むでもタブのPathとTitleが追従する()
    {
        var (vm, _) = Create(@"C:\work\src", @"C:\work");
        var tab = vm.AddTab(vm.Groups[0].Id, @"C:\work\src")!;

        vm.Folder.NavigateUp();
        Assert.Equal(@"C:\work", tab.Path);
        Assert.Equal("work", tab.Title);

        vm.Folder.GoBack();
        Assert.Equal(@"C:\work\src", tab.Path);
        Assert.Equal("src", tab.Title);

        vm.Folder.GoForward();
        Assert.Equal(@"C:\work", tab.Path);
        Assert.Equal("work", tab.Title);
    }

    [Fact]
    public void タブごとに履歴が独立している()
    {
        var (vm, _) = Create(@"C:\a", @"C:\a2", @"C:\b", @"C:\b2");
        var groupId = vm.Groups[0].Id;
        var tabA = vm.AddTab(groupId, @"C:\a")!;
        var tabB = vm.AddTab(groupId, @"C:\b")!;

        // タブAで移動
        vm.SelectTab(tabA);
        vm.Folder.LoadFolder(@"C:\a2");
        Assert.Equal(@"C:\a2", tabA.Path);

        // タブBへ切替(タブAの表示と履歴は維持される)
        vm.SelectTab(tabB);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
        Assert.False(vm.Folder.CanGoBack); // タブBは未移動なので戻れない

        // タブBで移動して戻る(タブAの履歴に影響しない)
        vm.Folder.LoadFolder(@"C:\b2");
        Assert.True(vm.Folder.CanGoBack);
        vm.Folder.GoBack();
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
        Assert.Equal(@"C:\b", tabB.Path);

        // タブAへ戻ると、タブAの表示フォルダと履歴が維持されている
        vm.SelectTab(tabA);
        Assert.Equal(@"C:\a2", vm.Folder.CurrentPath);
        Assert.True(vm.Folder.CanGoBack);
        vm.Folder.GoBack();
        Assert.Equal(@"C:\a", vm.Folder.CurrentPath);
        Assert.Equal(@"C:\a", tabA.Path);
    }

    [Fact]
    public void タブ切替でCanGoBackとCanGoForwardが切り替わる()
    {
        var (vm, _) = Create(@"C:\a", @"C:\a2", @"C:\b");
        var groupId = vm.Groups[0].Id;
        var tabA = vm.AddTab(groupId, @"C:\a")!;
        var tabB = vm.AddTab(groupId, @"C:\b")!;

        vm.SelectTab(tabA);
        vm.Folder.LoadFolder(@"C:\a2");
        vm.Folder.GoBack();
        Assert.False(vm.Folder.CanGoBack);
        Assert.True(vm.Folder.CanGoForward);

        vm.SelectTab(tabB);
        Assert.False(vm.Folder.CanGoBack);
        Assert.False(vm.Folder.CanGoForward);
        Assert.False(vm.Folder.BackCommand.CanExecute(null));
        Assert.False(vm.Folder.ForwardCommand.CanExecute(null));

        vm.SelectTab(tabA);
        Assert.False(vm.Folder.CanGoBack);
        Assert.True(vm.Folder.CanGoForward);
        Assert.True(vm.Folder.ForwardCommand.CanExecute(null));
    }

    [Fact]
    public void タブ切替自体は履歴に積まれない()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        var groupId = vm.Groups[0].Id;
        var tabA = vm.AddTab(groupId, @"C:\a")!;
        var tabB = vm.AddTab(groupId, @"C:\b")!;

        vm.SelectTab(tabA);
        vm.SelectTab(tabB);
        vm.SelectTab(tabA);

        Assert.False(vm.Folder.CanGoBack);
        Assert.False(tabA.History.CanGoBack);
        Assert.False(tabB.History.CanGoBack);
    }

    [Fact]
    public void 非アクティブタブのタイトルは移動の影響を受けない()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b", @"C:\work\src");
        var groupId = vm.Groups[0].Id;
        var tabA = vm.AddTab(groupId, @"C:\a")!;
        var tabB = vm.AddTab(groupId, @"C:\b")!;
        vm.SelectTab(tabB);

        vm.Folder.LoadFolder(@"C:\work\src");

        Assert.Equal(@"C:\a", tabA.Path);
        Assert.Equal("a", tabA.Title);
        Assert.Equal(@"C:\work\src", tabB.Path);
        Assert.Equal("src", tabB.Title);
    }

    [Fact]
    public void 移動失敗時はタブのPathとTitleが変わらない()
    {
        var (vm, stub) = Create(@"C:\a");
        stub.Setup(@"C:\missing", FolderListingResult.Failure("見つかりません"));
        var tab = vm.AddTab(vm.Groups[0].Id, @"C:\a")!;

        vm.Folder.LoadFolder(@"C:\missing");

        Assert.Equal(@"C:\a", tab.Path);
        Assert.Equal("a", tab.Title);
    }
}
