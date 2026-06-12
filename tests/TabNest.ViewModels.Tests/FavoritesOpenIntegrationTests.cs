using TabNest.Core.Interfaces;
using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// Task 4-4: 実ファイルシステム(一時フォルダ)を使った、存在しないパスを含む
/// お気に入りを開く結合テスト。
/// (MainViewModel を使うため、参照方向ルール上 ViewModels.Tests に配置する)
/// </summary>
public sealed class FavoritesOpenIntegrationTests : IDisposable
{
    private sealed class NullFileLauncher : IFileLauncher
    {
        public bool OpenFile(string path) => true;
    }

    private readonly string _tempRoot;

    public FavoritesOpenIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TabNest.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "exists"));
        File.WriteAllText(Path.Combine(_tempRoot, "exists", "note.txt"), "x");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void 存在しないパスを含むお気に入りはエラー表示で開き他のタブは正常に動く()
    {
        var existsPath = Path.Combine(_tempRoot, "exists");
        var missingPath = Path.Combine(_tempRoot, "missing");
        var session = new AppSettings
        {
            SavedGroups =
            [
                new SavedTabGroup { Id = "f1", Name = "混在", Paths = [missingPath, existsPath] },
            ],
        };
        var vm = new MainViewModel(new FileSystemService(), new NullFileLauncher(), session);

        var ok = vm.OpenFavorite("f1");

        // 5段上限ではないため開く操作自体は成功し、タブは2個とも作られる
        Assert.True(ok);
        var openedGroup = vm.Groups[^1];
        Assert.Equal("混在", openedGroup.Name);
        Assert.Equal(2, openedGroup.Tabs.Count);

        // 先頭タブ(存在しないパス)がアクティブになり、エラーが表示される
        Assert.True(openedGroup.Tabs[0].IsActive);
        Assert.Equal(missingPath, openedGroup.Tabs[0].Path);
        Assert.NotNull(vm.Folder.ErrorMessage);

        // もう一方のタブは正常に開ける
        vm.SelectTab(openedGroup.Tabs[1]);
        Assert.Null(vm.Folder.ErrorMessage);
        Assert.Equal(existsPath, vm.Folder.CurrentPath);
        Assert.Contains(vm.Folder.Items, i => i.Name == "note.txt");
    }
}
