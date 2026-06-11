using TabNest.Core.Models;

namespace TabNest.ViewModels.Tests;

public class FileItemViewModelTests
{
    private static FileItemViewModel Create(string name, bool isDirectory, long? size = null) => new(
        new FileSystemEntry
        {
            Name = name,
            FullPath = @"C:\test\" + name,
            IsDirectory = isDirectory,
            LastModifiedAt = new DateTime(2026, 6, 3, 12, 0, 0),
            SizeInBytes = size,
        });

    [Fact]
    public void IconGlyph_フォルダとファイルで異なるグリフを返す()
    {
        // Segoe Fluent Icons: フォルダ U+E8B7(Folder)、ファイル U+E7C3(Page)
        Assert.Equal("", Create("docs", isDirectory: true).IconGlyph);
        Assert.Equal("", Create("a.txt", isDirectory: false, size: 10).IconGlyph);
    }

    [Fact]
    public void TypeText_フォルダは種別フォルダ()
    {
        Assert.Equal("フォルダ", Create("docs", isDirectory: true).TypeText);
    }

    [Fact]
    public void TypeText_ファイルは拡張子ベースの種別文字列()
    {
        Assert.Equal("TXT ファイル", Create("a.txt", isDirectory: false, size: 10).TypeText);
        Assert.Equal("PNG ファイル", Create("image.png", isDirectory: false, size: 10).TypeText);
    }

    [Fact]
    public void TypeText_拡張子なしファイルはファイル()
    {
        Assert.Equal("ファイル", Create("LICENSE", isDirectory: false, size: 10).TypeText);
    }

    [Fact]
    public void LastModifiedText_日時を表示形式に整形する()
    {
        Assert.Equal("2026/06/03 12:00", Create("a.txt", isDirectory: false, size: 10).LastModifiedText);
    }

    [Fact]
    public void SizeText_ファイルはKB単位で切り上げ表示()
    {
        Assert.Equal("1 KB", Create("a.txt", isDirectory: false, size: 1).SizeText);
        Assert.Equal("1 KB", Create("b.txt", isDirectory: false, size: 1024).SizeText);
        Assert.Equal("2 KB", Create("c.txt", isDirectory: false, size: 1025).SizeText);
        Assert.Equal("1,000 KB", Create("d.bin", isDirectory: false, size: 1024 * 1000).SizeText);
    }

    [Fact]
    public void SizeText_フォルダは空欄()
    {
        Assert.Equal("", Create("docs", isDirectory: true).SizeText);
    }
}
