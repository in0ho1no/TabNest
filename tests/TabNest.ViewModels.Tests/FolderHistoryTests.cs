using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

public class FolderHistoryTests
{
    private static (FolderViewModel Vm, StubFileSystemService Stub) Create(params string[] paths)
    {
        var stub = new StubFileSystemService();
        foreach (var path in paths)
        {
            stub.Setup(path, FolderListingResult.Success([]));
        }

        return (new FolderViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void ABCと移動後_戻る戻るでAに着く()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b", @"C:\c");
        vm.LoadFolder(@"C:\a");
        vm.LoadFolder(@"C:\b");
        vm.LoadFolder(@"C:\c");

        Assert.True(vm.GoBack());
        Assert.Equal(@"C:\b", vm.CurrentPath);
        Assert.True(vm.GoBack());
        Assert.Equal(@"C:\a", vm.CurrentPath);
        Assert.False(vm.CanGoBack);
        Assert.True(vm.CanGoForward);
    }

    [Fact]
    public void 戻った後に進むで元のフォルダへ戻る()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b", @"C:\c");
        vm.LoadFolder(@"C:\a");
        vm.LoadFolder(@"C:\b");
        vm.LoadFolder(@"C:\c");
        vm.GoBack();
        vm.GoBack();

        Assert.True(vm.GoForward());
        Assert.Equal(@"C:\b", vm.CurrentPath);
        Assert.True(vm.GoForward());
        Assert.Equal(@"C:\c", vm.CurrentPath);
        Assert.False(vm.CanGoForward);
    }

    [Fact]
    public void 戻った後の新規移動でForwardがクリアされる()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b", @"C:\c", @"C:\d");
        vm.LoadFolder(@"C:\a");
        vm.LoadFolder(@"C:\b");
        vm.GoBack();
        Assert.True(vm.CanGoForward);

        vm.LoadFolder(@"C:\d");

        Assert.False(vm.CanGoForward);
        Assert.True(vm.GoBack());
        Assert.Equal(@"C:\a", vm.CurrentPath);
    }

    [Fact]
    public void 初期読み込みは履歴に積まれない()
    {
        var (vm, _) = Create(@"C:\a");

        vm.LoadFolder(@"C:\a");

        Assert.False(vm.CanGoBack);
        Assert.False(vm.CanGoForward);
    }

    [Fact]
    public void 同一フォルダへの再読み込みは履歴に積まれない()
    {
        var (vm, _) = Create(@"C:\a");
        vm.LoadFolder(@"C:\a");

        vm.LoadFolder(@"C:\a");

        Assert.False(vm.CanGoBack);
    }

    [Fact]
    public void 戻る先が読み込めない場合は履歴と状態を維持する()
    {
        var (vm, stub) = Create(@"C:\a", @"C:\b");
        vm.LoadFolder(@"C:\a");
        vm.LoadFolder(@"C:\b");
        stub.Setup(@"C:\a", FolderListingResult.Failure("削除されました"));

        var ok = vm.GoBack();

        Assert.False(ok);
        Assert.Equal(@"C:\b", vm.CurrentPath);
        Assert.True(vm.CanGoBack);
        Assert.NotNull(vm.ErrorMessage);

        // フォルダが復活すれば同じ履歴で戻れる(履歴が消費されていないこと)
        stub.Setup(@"C:\a", FolderListingResult.Success([]));
        Assert.True(vm.GoBack());
        Assert.Equal(@"C:\a", vm.CurrentPath);
    }

    [Fact]
    public void 履歴なしでGoBackは何もしない()
    {
        var (vm, _) = Create(@"C:\a");
        vm.LoadFolder(@"C:\a");

        Assert.False(vm.GoBack());
        Assert.False(vm.GoForward());
        Assert.Equal(@"C:\a", vm.CurrentPath);
    }

    [Fact]
    public void コマンド可否が履歴状態と連動する()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        Assert.False(vm.BackCommand.CanExecute(null));

        vm.LoadFolder(@"C:\a");
        vm.LoadFolder(@"C:\b");
        Assert.True(vm.BackCommand.CanExecute(null));
        Assert.False(vm.ForwardCommand.CanExecute(null));

        vm.BackCommand.Execute(null);
        Assert.Equal(@"C:\a", vm.CurrentPath);
        Assert.True(vm.ForwardCommand.CanExecute(null));

        vm.ForwardCommand.Execute(null);
        Assert.Equal(@"C:\b", vm.CurrentPath);
    }

    [Fact]
    public void 末尾セパレータ付きパスは正規化される()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\work\src", FolderListingResult.Success([]));

        var ok = vm.LoadFolder(@"C:\work\src\");

        Assert.True(ok);
        Assert.Equal(@"C:\work\src", vm.CurrentPath);
    }

    [Fact]
    public void ドライブルートの正規化では区切り文字が維持される()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([]));

        var ok = vm.LoadFolder(@"C:\");

        Assert.True(ok);
        Assert.Equal(@"C:\", vm.CurrentPath);
    }
}
