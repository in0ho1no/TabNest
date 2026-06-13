using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// Alt+左/右/上(戻る・進む・上へ)の単体テスト。
/// 既存の BackCommand / ForwardCommand / NavigateUpCommand のコマンド可否
/// (CanGoBack / CanGoForward / CanNavigateUp)に従うことを確認する。
/// </summary>
public class ShortcutNavigationTests
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
    public void NavigateBack_戻れる状態では直前のフォルダへ戻る()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        vm.Folder.LoadFolder(@"C:\a");
        vm.Folder.LoadFolder(@"C:\b");
        Assert.True(vm.Folder.CanGoBack);

        var ok = vm.NavigateBack();

        Assert.True(ok);
        Assert.Equal(@"C:\a", vm.Folder.CurrentPath);
    }

    [Fact]
    public void NavigateBack_戻れない状態では何もしない()
    {
        var (vm, _) = Create(@"C:\a");
        vm.Folder.LoadFolder(@"C:\a");
        Assert.False(vm.Folder.CanGoBack);

        var ok = vm.NavigateBack();

        Assert.False(ok);
        Assert.Equal(@"C:\a", vm.Folder.CurrentPath);
    }

    [Fact]
    public void NavigateForward_進める状態では進む()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        vm.Folder.LoadFolder(@"C:\a");
        vm.Folder.LoadFolder(@"C:\b");
        vm.NavigateBack(); // a へ戻り、b が forward に積まれる
        Assert.True(vm.Folder.CanGoForward);

        var ok = vm.NavigateForward();

        Assert.True(ok);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
    }

    [Fact]
    public void NavigateForward_進めない状態では何もしない()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        vm.Folder.LoadFolder(@"C:\a");
        vm.Folder.LoadFolder(@"C:\b");
        Assert.False(vm.Folder.CanGoForward);

        var ok = vm.NavigateForward();

        Assert.False(ok);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath);
    }

    [Fact]
    public void NavigateUp_上の階層があれば移動する()
    {
        var (vm, _) = Create(@"C:\work\src", @"C:\work");
        vm.Folder.LoadFolder(@"C:\work\src");
        Assert.True(vm.Folder.CanNavigateUp);

        var ok = vm.NavigateUp();

        Assert.True(ok);
        Assert.Equal(@"C:\work", vm.Folder.CurrentPath);
    }

    [Fact]
    public void NavigateUp_ドライブルートでは移動しない()
    {
        var (vm, _) = Create(@"C:\");
        vm.Folder.LoadFolder(@"C:\");
        Assert.False(vm.Folder.CanNavigateUp);

        var ok = vm.NavigateUp();

        Assert.False(ok);
        Assert.Equal(@"C:\", vm.Folder.CurrentPath);
    }

    [Fact]
    public void NavigateBack_グループ名編集中は何もしない()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        vm.Folder.LoadFolder(@"C:\a");
        vm.Folder.LoadFolder(@"C:\b");
        vm.Groups[0].BeginRename();

        var ok = vm.NavigateBack();

        Assert.False(ok);
        Assert.Equal(@"C:\b", vm.Folder.CurrentPath); // 編集中は戻らない
        Assert.True(vm.Groups[0].IsEditingName);
    }
}
