using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

public class FolderNavigationTests
{
    private static FileSystemEntry Entry(string fullPath, bool isDirectory) => new()
    {
        Name = Path.GetFileName(fullPath),
        FullPath = fullPath,
        IsDirectory = isDirectory,
        LastModifiedAt = new DateTime(2026, 6, 1),
        SizeInBytes = isDirectory ? null : 1,
    };

    private static (FolderViewModel Vm, StubFileSystemService Stub, SpyFileLauncher Launcher) Create()
    {
        var stub = new StubFileSystemService();
        var launcher = new SpyFileLauncher();
        return (new FolderViewModel(stub, launcher), stub, launcher);
    }

    [Fact]
    public void NavigateUp_親フォルダへ移動する()
    {
        var (vm, stub, _) = Create();
        stub.Setup(@"C:\work\src", FolderListingResult.Success([]));
        stub.Setup(@"C:\work", FolderListingResult.Success([]));
        vm.LoadFolder(@"C:\work\src");

        var ok = vm.NavigateUp();

        Assert.True(ok);
        Assert.Equal(@"C:\work", vm.CurrentPath);
    }

    [Fact]
    public void NavigateUp_ドライブルートでは移動しない()
    {
        var (vm, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([]));
        vm.LoadFolder(@"C:\");

        var ok = vm.NavigateUp();

        Assert.False(ok);
        Assert.Equal(@"C:\", vm.CurrentPath);
        Assert.False(vm.CanNavigateUp);
        Assert.False(vm.NavigateUpCommand.CanExecute(null));
    }

    [Fact]
    public void NavigateUp_サブフォルダではCanNavigateUpがtrue()
    {
        var (vm, stub, _) = Create();
        stub.Setup(@"C:\work", FolderListingResult.Success([]));
        vm.LoadFolder(@"C:\work");

        Assert.True(vm.CanNavigateUp);
        Assert.True(vm.NavigateUpCommand.CanExecute(null));
    }

    [Fact]
    public void NavigateToAddress_入力パスへ移動しアドレスバーが同期される()
    {
        var (vm, stub, _) = Create();
        stub.Setup(@"C:\target", FolderListingResult.Success([]));
        vm.AddressBarText = @"C:\target";

        var ok = vm.NavigateToAddress();

        Assert.True(ok);
        Assert.Equal(@"C:\target", vm.CurrentPath);
        Assert.Equal(@"C:\target", vm.AddressBarText);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void NavigateToAddress_存在しないパスはエラー表示し現在フォルダに留まる()
    {
        var (vm, stub, _) = Create();
        stub.Setup(@"C:\ok", FolderListingResult.Success([Entry(@"C:\ok\a.txt", isDirectory: false)]));
        vm.LoadFolder(@"C:\ok");
        vm.AddressBarText = @"C:\no-such-path";

        var ok = vm.NavigateToAddress();

        Assert.False(ok);
        Assert.Equal(@"C:\ok", vm.CurrentPath);
        Assert.Single(vm.Items);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void NavigateToAddress_空入力はエラー表示する()
    {
        var (vm, _, _) = Create();
        vm.AddressBarText = "   ";

        var ok = vm.NavigateToAddress();

        Assert.False(ok);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void OpenItem_フォルダはそのフォルダへ移動する()
    {
        var (vm, stub, launcher) = Create();
        stub.Setup(@"C:\work\docs", FolderListingResult.Success([]));

        var ok = vm.OpenItem(new FileItemViewModel(Entry(@"C:\work\docs", isDirectory: true)));

        Assert.True(ok);
        Assert.Equal(@"C:\work\docs", vm.CurrentPath);
        Assert.Empty(launcher.OpenedPaths);
    }

    [Fact]
    public void OpenItem_ファイルは既定アプリで開く()
    {
        var (vm, stub, launcher) = Create();
        stub.Setup(@"C:\work", FolderListingResult.Success([]));
        vm.LoadFolder(@"C:\work");

        var ok = vm.OpenItem(new FileItemViewModel(Entry(@"C:\work\a.txt", isDirectory: false)));

        Assert.True(ok);
        Assert.Equal([@"C:\work\a.txt"], launcher.OpenedPaths);
        Assert.Equal(@"C:\work", vm.CurrentPath);
    }

    [Fact]
    public void OpenItem_ファイルを開けない場合はエラー表示する()
    {
        var (vm, _, launcher) = Create();
        launcher.NextResult = false;

        var ok = vm.OpenItem(new FileItemViewModel(Entry(@"C:\work\broken.txt", isDirectory: false)));

        Assert.False(ok);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("broken.txt", vm.ErrorMessage);
    }
}
