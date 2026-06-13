using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>Task 4-5: ファイル一覧の列ソート・列幅クランプ・ダブルクォートパス入力を検証する。</summary>
public class FileListSortTests
{
    private const string FolderPath = @"C:\target";

    private static FileSystemEntry Entry(
        string name, bool isDirectory, DateTime modifiedAt, long? size) => new()
    {
        Name = name,
        FullPath = Path.Combine(FolderPath, name),
        IsDirectory = isDirectory,
        LastModifiedAt = modifiedAt,
        SizeInBytes = size,
    };

    /// <summary>フォルダ2個+ファイル3個(名前・種別・日時・サイズの順序がそれぞれ異なる)。</summary>
    private static FolderViewModel CreateLoadedViewModel()
    {
        var stub = new StubFileSystemService();
        stub.Setup(FolderPath, FolderListingResult.Success(
        [
            Entry("zebra", isDirectory: true, new DateTime(2026, 6, 1), null),
            Entry("alpha", isDirectory: true, new DateTime(2026, 6, 5), null),
            Entry("middle.txt", isDirectory: false, new DateTime(2026, 6, 3), 300),
            Entry("aaa.zip", isDirectory: false, new DateTime(2026, 6, 4), 100),
            Entry("zzz.txt", isDirectory: false, new DateTime(2026, 6, 2), 200),
        ]));
        var vm = new FolderViewModel(stub, new SpyFileLauncher());
        Assert.True(vm.LoadFolder(FolderPath));
        return vm;
    }

    private static string[] Names(FolderViewModel vm) => vm.Items.Select(i => i.Name).ToArray();

    [Fact]
    public void 初期表示はフォルダ先頭_名前昇順()
    {
        var vm = CreateLoadedViewModel();

        Assert.Equal(FileSortColumn.Name, vm.SortColumn);
        Assert.False(vm.SortDescending);
        Assert.Equal(["alpha", "zebra", "aaa.zip", "middle.txt", "zzz.txt"], Names(vm));
    }

    [Fact]
    public void 同じ列をクリックすると昇順と降順が切り替わる()
    {
        var vm = CreateLoadedViewModel();

        vm.ToggleSort(FileSortColumn.Name); // 名前(現在の列)→ 降順へ

        Assert.True(vm.SortDescending);
        Assert.Equal(["zebra", "alpha", "zzz.txt", "middle.txt", "aaa.zip"], Names(vm));

        vm.ToggleSort(FileSortColumn.Name); // もう一度 → 昇順へ戻る

        Assert.False(vm.SortDescending);
        Assert.Equal(["alpha", "zebra", "aaa.zip", "middle.txt", "zzz.txt"], Names(vm));
    }

    [Fact]
    public void 別の列をクリックするとその列の昇順になる()
    {
        var vm = CreateLoadedViewModel();
        vm.ToggleSort(FileSortColumn.Name); // 降順にしておく

        vm.ToggleSort(FileSortColumn.Size);

        Assert.Equal(FileSortColumn.Size, vm.SortColumn);
        Assert.False(vm.SortDescending);
    }

    [Fact]
    public void 更新日時ソートでもフォルダが先頭に維持される()
    {
        var vm = CreateLoadedViewModel();

        vm.ToggleSort(FileSortColumn.LastModified);

        // フォルダ(日時昇順): zebra(6/1) → alpha(6/5)、続いてファイル(日時昇順)
        Assert.Equal(["zebra", "alpha", "zzz.txt", "middle.txt", "aaa.zip"], Names(vm));
        Assert.All(vm.Items.Take(2), i => Assert.True(i.IsDirectory));
    }

    [Fact]
    public void サイズソートは昇順降順とも正しく並ぶ()
    {
        var vm = CreateLoadedViewModel();

        vm.ToggleSort(FileSortColumn.Size);
        Assert.Equal(["alpha", "zebra", "aaa.zip", "zzz.txt", "middle.txt"], Names(vm));

        vm.ToggleSort(FileSortColumn.Size); // 降順(サイズ同値のフォルダ同士は名前昇順で安定)
        Assert.Equal(["alpha", "zebra", "middle.txt", "zzz.txt", "aaa.zip"], Names(vm));
    }

    [Fact]
    public void 種別ソートは種別文字列順_同種別内は名前昇順()
    {
        var vm = CreateLoadedViewModel();

        vm.ToggleSort(FileSortColumn.Type);

        // TXT ファイル < ZIP ファイル、TXT 内は名前昇順
        Assert.Equal(["alpha", "zebra", "middle.txt", "zzz.txt", "aaa.zip"], Names(vm));
    }

    [Fact]
    public void フォルダ移動後もソート設定が維持される()
    {
        var vm = CreateLoadedViewModel();
        vm.ToggleSort(FileSortColumn.Name); // 名前降順

        Assert.True(vm.LoadFolder(FolderPath)); // 再読み込み(別フォルダへの移動相当)

        Assert.Equal(FileSortColumn.Name, vm.SortColumn);
        Assert.True(vm.SortDescending);
        Assert.Equal(["zebra", "alpha", "zzz.txt", "middle.txt", "aaa.zip"], Names(vm));
    }

    [Theory]
    [InlineData(30, 1000, 500, 40)]    // 最小 40px に切り上げ
    [InlineData(100, 1000, 500, 100)]  // 範囲内はそのまま
    [InlineData(900, 1000, 500, 480)]  // 最大 = 1000 - 500 - 20 = 480
    [InlineData(100, 100, 500, 40)]    // 最大が最小を下回る場合は最小 40px を優先
    public void 列幅自動調整は最小40_最大はウィンドウ幅から他列と20pxを引いた値(
        double desired, double windowWidth, double otherColumns, double expected)
    {
        Assert.Equal(expected, FolderViewModel.ClampAutoColumnWidth(desired, windowWidth, otherColumns));
    }

    [Fact]
    public void ダブルクォート括りのパスは外側のクォートを除去して移動する()
    {
        var vm = CreateLoadedViewModel();
        vm.AddressBarText = $"\"{FolderPath}\"";

        var ok = vm.NavigateToAddress();

        Assert.True(ok);
        Assert.Equal(FolderPath, vm.CurrentPath);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void ダブルクォートの中身が空ならエラーになり状態を変えない()
    {
        var vm = CreateLoadedViewModel();
        vm.AddressBarText = "\"\"";

        var ok = vm.NavigateToAddress();

        Assert.False(ok);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(FolderPath, vm.CurrentPath);
    }

    [Fact]
    public void 片側だけのダブルクォートは除去せずそのまま扱う()
    {
        var stub = new StubFileSystemService();
        stub.Setup(FolderPath, FolderListingResult.Success([]));
        var vm = new FolderViewModel(stub, new SpyFileLauncher());
        vm.AddressBarText = $"\"{FolderPath}";

        var ok = vm.NavigateToAddress();

        // 「"C:\target」というパスは存在しないためエラー(クォート除去は両側揃いのみ)
        Assert.False(ok);
        Assert.NotNull(vm.ErrorMessage);
    }
}
