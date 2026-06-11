using TabNest.Core.Services;

namespace TabNest.Integration.Tests;

/// <summary>
/// FileSystemService の結合テスト。テストごとに一時フォルダを作成し、終了時に削除する。
/// 実ユーザーフォルダには触れない。
/// </summary>
public sealed class FileSystemServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemService _service = new();

    public FileSystemServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TabNest.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ListFolder_ファイルとフォルダを件数どおり取得できる()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "sub1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "sub2"));
        File.WriteAllText(Path.Combine(_tempRoot, "a.txt"), "hello");

        var result = _service.ListFolder(_tempRoot);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(2, result.Entries.Count(e => e.IsDirectory));
        Assert.Equal(1, result.Entries.Count(e => !e.IsDirectory));
    }

    [Fact]
    public void ListFolder_名前と種別が正しい()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "docs"));
        File.WriteAllText(Path.Combine(_tempRoot, "readme.md"), "# readme");

        var result = _service.ListFolder(_tempRoot);

        Assert.True(result.IsSuccess);
        var folder = Assert.Single(result.Entries, e => e.Name == "docs");
        Assert.True(folder.IsDirectory);
        Assert.Equal(Path.Combine(_tempRoot, "docs"), folder.FullPath);

        var file = Assert.Single(result.Entries, e => e.Name == "readme.md");
        Assert.False(file.IsDirectory);
        Assert.Equal(Path.Combine(_tempRoot, "readme.md"), file.FullPath);
    }

    [Fact]
    public void ListFolder_ファイルのサイズと更新日時を取得できる()
    {
        var filePath = Path.Combine(_tempRoot, "data.bin");
        File.WriteAllBytes(filePath, new byte[128]);

        var result = _service.ListFolder(_tempRoot);

        var file = Assert.Single(result.Entries);
        Assert.Equal(128, file.SizeInBytes);
        Assert.Equal(File.GetLastWriteTime(filePath), file.LastModifiedAt);
    }

    [Fact]
    public void ListFolder_フォルダのサイズはnull()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "sub"));

        var result = _service.ListFolder(_tempRoot);

        var folder = Assert.Single(result.Entries);
        Assert.Null(folder.SizeInBytes);
    }

    [Fact]
    public void ListFolder_空フォルダでは空一覧を返す()
    {
        var result = _service.ListFolder(_tempRoot);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void ListFolder_存在しないパスはエラー情報を返す()
    {
        var missing = Path.Combine(_tempRoot, "no-such-folder");

        var result = _service.ListFolder(missing);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(missing, result.ErrorMessage);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void ListFolder_空文字パスはエラー情報を返す()
    {
        var result = _service.ListFolder("");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.Entries);
    }
}
