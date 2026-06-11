using TabNest.Core.Interfaces;
using TabNest.Core.Services;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// 実ファイルシステム(一時フォルダ)を使ったフォルダ移動の結合テスト。
/// (FolderViewModel を使うため、参照方向ルール上 ViewModels.Tests に配置する)
/// </summary>
public sealed class FolderNavigationIntegrationTests : IDisposable
{
    /// <summary>結合テストではファイルを実際に開かないためのダミー。</summary>
    private sealed class NullFileLauncher : IFileLauncher
    {
        public bool OpenFile(string path) => true;
    }

    private readonly string _tempRoot;
    private readonly FolderViewModel _vm;

    public FolderNavigationIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TabNest.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "child", "grandchild"));
        File.WriteAllText(Path.Combine(_tempRoot, "child", "note.txt"), "x");
        _vm = new FolderViewModel(new FileSystemService(), new NullFileLauncher());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ダブルクリック相当のOpenItemで子フォルダへ移動できる()
    {
        _vm.LoadFolder(_tempRoot);
        var child = Assert.Single(_vm.Items, i => i.IsDirectory);

        var ok = _vm.OpenItem(child);

        Assert.True(ok);
        Assert.Equal(Path.Combine(_tempRoot, "child"), _vm.CurrentPath);
        Assert.Contains(_vm.Items, i => i.Name == "note.txt");
        Assert.Contains(_vm.Items, i => i.Name == "grandchild" && i.IsDirectory);
    }

    [Fact]
    public void NavigateUpで親フォルダへ戻れる()
    {
        _vm.LoadFolder(Path.Combine(_tempRoot, "child"));

        var ok = _vm.NavigateUp();

        Assert.True(ok);
        Assert.Equal(_tempRoot, _vm.CurrentPath);
        Assert.Single(_vm.Items);
    }

    [Fact]
    public void アドレスバー入力で移動できる()
    {
        _vm.LoadFolder(_tempRoot);
        _vm.AddressBarText = Path.Combine(_tempRoot, "child", "grandchild");

        var ok = _vm.NavigateToAddress();

        Assert.True(ok);
        Assert.Equal(Path.Combine(_tempRoot, "child", "grandchild"), _vm.CurrentPath);
        Assert.Empty(_vm.Items);
    }

    [Fact]
    public void 不正なパス入力後も正常に移動を継続できる()
    {
        _vm.LoadFolder(_tempRoot);
        _vm.AddressBarText = Path.Combine(_tempRoot, "missing");
        Assert.False(_vm.NavigateToAddress());
        Assert.Equal(_tempRoot, _vm.CurrentPath);
        Assert.NotNull(_vm.ErrorMessage);

        _vm.AddressBarText = Path.Combine(_tempRoot, "child");
        var ok = _vm.NavigateToAddress();

        Assert.True(ok);
        Assert.Equal(Path.Combine(_tempRoot, "child"), _vm.CurrentPath);
        Assert.Null(_vm.ErrorMessage);
    }
}
