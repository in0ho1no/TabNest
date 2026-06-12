using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// Task 4-2: セッション保存用の AppSettings 生成
/// (MainViewModel.CreateAppSettings)を検証する単体テスト。
/// </summary>
public class SessionSaveTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static MainViewModel CreateViewModel(StubFileSystemService? stub = null)
        => new(stub ?? new StubFileSystemService(), new SpyFileLauncher());

    private static StubFileSystemService CreateStubWithFolder(string path)
    {
        var stub = new StubFileSystemService();
        stub.Setup(path, FolderListingResult.Success([]));
        return stub;
    }

    [Fact]
    public void 初期状態_初期グループとアクティブタブが保存内容に含まれる()
    {
        var vm = CreateViewModel();

        var settings = vm.CreateAppSettings(1280, 800, 250);

        var group = Assert.Single(settings.TabGroups);
        Assert.Equal("作業1", group.Name);
        var tab = Assert.Single(group.Tabs);
        Assert.Equal(UserProfile, tab.Path);
        Assert.Equal(group.Id, settings.ActiveGroupId);
        Assert.Equal(tab.Id, settings.ActiveTabId);
        Assert.Equal(tab.Id, group.SelectedTabId);
        Assert.Empty(settings.ClosedTabs);
        Assert.Empty(settings.SavedGroups);
    }

    [Fact]
    public void ウィンドウサイズと左カラム幅が保存内容に反映される()
    {
        var vm = CreateViewModel();

        var settings = vm.CreateAppSettings(1920, 1080, 300);

        Assert.Equal(1920, settings.WindowWidth);
        Assert.Equal(1080, settings.WindowHeight);
        Assert.Equal(300, settings.LeftPaneWidth);
    }

    [Fact]
    public void タブとグループの追加が保存内容に反映される()
    {
        var vm = CreateViewModel();

        Assert.True(vm.AddTabToActiveGroup());
        Assert.True(vm.AddGroupWithDefaultTab());
        var settings = vm.CreateAppSettings(1280, 800, 220);

        Assert.Equal(2, settings.TabGroups.Count);
        Assert.Equal(2, settings.TabGroups[0].Tabs.Count);
        Assert.Equal("作業2", settings.TabGroups[1].Name);
        var newGroupTab = Assert.Single(settings.TabGroups[1].Tabs);
        Assert.Equal(settings.TabGroups[1].Id, settings.ActiveGroupId);
        Assert.Equal(newGroupTab.Id, settings.ActiveTabId);
    }

    [Fact]
    public void フォルダ移動後は現在表示中のパスが保存される()
    {
        var movedPath = Path.Combine(UserProfile, "Documents");
        var vm = CreateViewModel(CreateStubWithFolder(movedPath));
        vm.LoadInitialFolder();

        vm.Folder.LoadFolder(movedPath);
        var settings = vm.CreateAppSettings(1280, 800, 220);

        var tab = Assert.Single(Assert.Single(settings.TabGroups).Tabs);
        Assert.Equal(movedPath, tab.Path);
        Assert.Equal("Documents", tab.Title);
    }

    [Fact]
    public void 閉じたタブが履歴として保存内容に含まれる()
    {
        var vm = CreateViewModel();
        Assert.True(vm.AddTabToActiveGroup());
        var closingTab = vm.Groups[0].Tabs[1];

        Assert.True(vm.CloseTab(closingTab));
        var settings = vm.CreateAppSettings(1280, 800, 220);

        var closed = Assert.Single(settings.ClosedTabs);
        Assert.Equal(UserProfile, closed.Path);
        Assert.Equal(vm.Groups[0].Id, closed.GroupId);
        Assert.Equal(1, closed.TabIndex);
        var remainingTab = Assert.Single(Assert.Single(settings.TabGroups).Tabs);
        Assert.Equal(remainingTab.Id, settings.ActiveTabId);
    }
}
