using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// Ctrl+W(アクティブタブを閉じる)の単体テスト。
/// 中クリックでの閉じる(CloseTab)と同一経路を通り、ClosedTab 履歴へ積むことを確認する。
/// </summary>
public class ShortcutCloseActiveTabTests
{
    private static (MainViewModel Vm, StubFileSystemService Stub) Create(params string[] paths)
    {
        var stub = new StubFileSystemService();
        foreach (var path in paths)
        {
            stub.Setup(path, FolderListingResult.Success([]));
        }

        return (new MainViewModel(stub, new SpyFileLauncher()), stub);
    }

    [Fact]
    public void CloseActiveTab_アクティブタブが閉じてClosedTabsへ積まれ復元できる()
    {
        var (vm, _) = Create(@"C:\a", @"C:\b");
        var groupId = vm.Groups[0].Id;
        vm.AddTab(groupId, @"C:\a");
        var b = vm.AddTab(groupId, @"C:\b")!; // 末尾追加でアクティブになる
        Assert.True(b.IsActive);
        var countBefore = vm.Groups[0].Tabs.Count;

        var ok = vm.CloseActiveTab();

        Assert.True(ok);
        Assert.Equal(countBefore - 1, vm.Groups[0].Tabs.Count);
        Assert.DoesNotContain(b, vm.Groups[0].Tabs);

        // ClosedTab 履歴へ積まれているので Ctrl+Shift+T 相当で復元できる
        Assert.True(vm.RestoreClosedTab());
        Assert.Contains(vm.Groups[0].Tabs, t => t.Path == @"C:\b");
    }

    [Fact]
    public void CloseActiveTab_中クリックと同じ経路でClosedTabsに記録される()
    {
        var (vm, _) = Create(@"C:\a");
        var groupId = vm.Groups[0].Id;
        vm.AddTab(groupId, @"C:\a"); // a がアクティブ

        vm.CloseActiveTab();

        var settings = vm.CreateAppSettings(0, 0, 0);
        Assert.Contains(settings.ClosedTabs, c => c.Path == @"C:\a");
    }

    [Fact]
    public void CloseActiveTab_アプリ内最後の1タブは閉じられない()
    {
        var (vm, _) = Create();
        var initial = Assert.Single(vm.Groups[0].Tabs); // 初期タブのみ(アクティブ)

        var ok = vm.CloseActiveTab();

        Assert.False(ok);
        Assert.Single(vm.Groups[0].Tabs); // タブは残る
        Assert.Same(initial, vm.Groups[0].Tabs[0]);
        Assert.Single(vm.Groups); // グループも残る
    }

    [Fact]
    public void CloseActiveTab_グループ名編集中は何も実行しない()
    {
        var (vm, _) = Create(@"C:\a");
        var groupId = vm.Groups[0].Id;
        vm.AddTab(groupId, @"C:\a");
        vm.Groups[0].BeginRename();
        var countBefore = vm.Groups[0].Tabs.Count;

        var ok = vm.CloseActiveTab();

        Assert.False(ok);
        Assert.Equal(countBefore, vm.Groups[0].Tabs.Count);
        Assert.True(vm.Groups[0].IsEditingName); // 編集状態が維持される
    }
}
