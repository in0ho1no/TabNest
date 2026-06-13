using TabNest.Core.Models;
using TabNest.Core.Services;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>タブの複製(右クリックメニュー)の ViewModel 層テスト(Task 6-3)。</summary>
public class TabDuplicateTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static (MainViewModel Vm, StubFileSystemService Stub) Create()
    {
        var stub = new StubFileSystemService();
        stub.Setup(UserProfile, FolderListingResult.Success([]));
        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void 複製は同一Pathのタブを対象タブの直後に挿入しアクティブにする()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\b", FolderListingResult.Success([]));
        var group = vm.Groups[0];
        var target = group.Tabs[0];
        // 並び確認のため別タブを末尾に追加してから先頭タブを複製する
        vm.AddTab(group.Id, @"C:\b");
        var last = group.Tabs[^1];

        var ok = vm.DuplicateTab(target);

        Assert.True(ok);
        Assert.Equal(3, group.Tabs.Count);
        var duplicate = group.Tabs[1];
        Assert.Equal(target.Path, duplicate.Path);
        Assert.Equal(target.Title, duplicate.Title);
        // 対象タブの直後に挿入され、末尾タブはさらに後ろに残る
        Assert.Equal([target, duplicate, last], group.Tabs.ToArray());
        Assert.True(duplicate.IsActive);
        Assert.False(target.IsActive);
        Assert.Equal(duplicate.Path, vm.Folder.CurrentPath);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void 複製タブの戻る進む履歴は引き継がず空で開始する()
    {
        var (vm, stub) = Create();
        stub.Setup(@"C:\sub", FolderListingResult.Success([]));
        var group = vm.Groups[0];
        var source = group.Tabs[0];
        // 初期フォルダを読み込み、アクティブな元タブにその履歴を接続する
        Assert.True(vm.LoadInitialFolder());
        // アクティブな元タブで移動して戻る履歴を積む
        vm.Folder.LoadFolder(@"C:\sub");
        Assert.True(source.History.CanGoBack);

        var ok = vm.DuplicateTab(source);

        Assert.True(ok);
        var duplicate = group.Tabs[1];
        Assert.Equal(@"C:\sub", duplicate.Path);
        // 複製タブの履歴は新規(空)・元タブの履歴は維持される
        Assert.False(duplicate.History.CanGoBack);
        Assert.False(duplicate.History.CanGoForward);
        Assert.True(source.History.CanGoBack);
    }

    [Fact]
    public void 上限到達時はエラーを表示して複製しない()
    {
        var (vm, _) = Create();
        var group = vm.Groups[0];
        for (var i = group.Tabs.Count; i < TabManagerService.MaxTabsPerGroup; i++)
        {
            Assert.True(vm.AddTabToActiveGroup());
        }
        Assert.Equal(TabManagerService.MaxTabsPerGroup, group.Tabs.Count);

        var ok = vm.DuplicateTab(group.Tabs[0]);

        Assert.False(ok);
        Assert.Equal(TabManagerService.MaxTabsPerGroup, group.Tabs.Count);
        Assert.NotNull(vm.OperationError);
    }

    [Fact]
    public void 複製が成功するとOperationErrorがクリアされる()
    {
        var (vm, _) = Create();
        var group = vm.Groups[0];
        // 先にグループ上限でエラーを発生させる
        for (var i = 1; i < TabManagerService.MaxGroups; i++)
        {
            vm.AddGroupWithDefaultTab();
        }
        vm.AddGroupWithDefaultTab(); // 6段目 → エラー
        Assert.NotNull(vm.OperationError);

        var ok = vm.DuplicateTab(group.Tabs[0]);

        Assert.True(ok);
        Assert.Null(vm.OperationError);
    }
}
