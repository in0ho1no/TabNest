using TabNest.Core.Services;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// タブ選択と FolderViewModel の連携を実ファイルシステム(一時フォルダ)で検証する結合テスト。
/// </summary>
public sealed class TabSelectionIntegrationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly MainViewModel _vm;

    public TabSelectionIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TabNest.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "alpha"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "beta"));
        File.WriteAllText(Path.Combine(_tempRoot, "alpha", "a1.txt"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "alpha", "a2.txt"), "x");
        File.WriteAllText(Path.Combine(_tempRoot, "beta", "b1.txt"), "x");
        _vm = new MainViewModel(new FileSystemService(), new SpyFileLauncher());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void タブ切替でファイル一覧が切り替わる()
    {
        var groupId = _vm.Groups[0].Id;
        var alphaTab = _vm.AddTab(groupId, Path.Combine(_tempRoot, "alpha"))!;
        var betaTab = _vm.AddTab(groupId, Path.Combine(_tempRoot, "beta"))!;

        _vm.SelectTab(alphaTab);
        Assert.Equal(2, _vm.Folder.Items.Count);
        Assert.Contains(_vm.Folder.Items, i => i.Name == "a1.txt");

        _vm.SelectTab(betaTab);
        Assert.Single(_vm.Folder.Items);
        Assert.Contains(_vm.Folder.Items, i => i.Name == "b1.txt");

        _vm.SelectTab(alphaTab);
        Assert.Equal(2, _vm.Folder.Items.Count);
        Assert.Equal(Path.Combine(_tempRoot, "alpha"), _vm.Folder.CurrentPath);
    }
}
