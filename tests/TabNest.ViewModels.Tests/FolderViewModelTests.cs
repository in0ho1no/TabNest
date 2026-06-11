using TabNest.Core.Interfaces;
using TabNest.Core.Models;

namespace TabNest.ViewModels.Tests;

public class FolderViewModelTests
{
    /// <summary>パスごとに固定の結果を返す IFileSystemService スタブ。</summary>
    private sealed class StubFileSystemService : IFileSystemService
    {
        private readonly Dictionary<string, FolderListingResult> _results = new();

        public void Setup(string path, FolderListingResult result) => _results[path] = result;

        public FolderListingResult ListFolder(string path)
            => _results.TryGetValue(path, out var result)
                ? result
                : FolderListingResult.Failure($"フォルダが見つかりません: {path}");
    }

    private static FileSystemEntry Entry(string name, bool isDirectory, long? size = null) => new()
    {
        Name = name,
        FullPath = @"C:\test\" + name,
        IsDirectory = isDirectory,
        LastModifiedAt = new DateTime(2026, 6, 1, 10, 0, 0),
        SizeInBytes = size,
    };

    [Fact]
    public void LoadFolder_成功時にCurrentPathとItemsが更新される()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\test", FolderListingResult.Success(
        [
            Entry("a.txt", isDirectory: false, size: 10),
            Entry("docs", isDirectory: true),
        ]));
        var vm = new FolderViewModel(stub);

        var ok = vm.LoadFolder(@"C:\test");

        Assert.True(ok);
        Assert.Equal(@"C:\test", vm.CurrentPath);
        Assert.Equal(2, vm.Items.Count);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void LoadFolder_フォルダ先頭_名前昇順に並ぶ()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\test", FolderListingResult.Success(
        [
            Entry("b.txt", isDirectory: false),
            Entry("zebra", isDirectory: true),
            Entry("a.txt", isDirectory: false),
            Entry("alpha", isDirectory: true),
        ]));
        var vm = new FolderViewModel(stub);

        vm.LoadFolder(@"C:\test");

        Assert.Equal(["alpha", "zebra", "a.txt", "b.txt"], vm.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void LoadFolder_失敗時は状態を変更せずErrorMessageを設定する()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\ok", FolderListingResult.Success([Entry("a.txt", isDirectory: false)]));
        var vm = new FolderViewModel(stub);
        vm.LoadFolder(@"C:\ok");

        var ok = vm.LoadFolder(@"C:\missing");

        Assert.False(ok);
        Assert.Equal(@"C:\ok", vm.CurrentPath);
        Assert.Single(vm.Items);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains(@"C:\missing", vm.ErrorMessage);
    }

    [Fact]
    public void LoadFolder_成功するとErrorMessageがクリアされる()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\ok", FolderListingResult.Success([]));
        var vm = new FolderViewModel(stub);
        vm.LoadFolder(@"C:\missing");
        Assert.NotNull(vm.ErrorMessage);

        vm.LoadFolder(@"C:\ok");

        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void LoadFolderCommand_文字列パラメータで読み込みが実行される()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\cmd", FolderListingResult.Success([Entry("x.txt", isDirectory: false)]));
        var vm = new FolderViewModel(stub);

        vm.LoadFolderCommand.Execute(@"C:\cmd");

        Assert.Equal(@"C:\cmd", vm.CurrentPath);
        Assert.Single(vm.Items);
    }

    [Fact]
    public void FileItemViewModel_エントリの値を保持する()
    {
        var item = new FileItemViewModel(Entry("a.txt", isDirectory: false, size: 1024));

        Assert.Equal("a.txt", item.Name);
        Assert.Equal(@"C:\test\a.txt", item.FullPath);
        Assert.False(item.IsDirectory);
        Assert.Equal(new DateTime(2026, 6, 1, 10, 0, 0), item.LastModifiedAt);
        Assert.Equal(1024, item.SizeInBytes);
    }

    [Fact]
    public void FileItemViewModel_フォルダのサイズはnull()
    {
        var item = new FileItemViewModel(Entry("docs", isDirectory: true));

        Assert.True(item.IsDirectory);
        Assert.Null(item.SizeInBytes);
    }
}
