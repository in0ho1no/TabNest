using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// Task 4-3: 起動時のセッション復元(MainViewModel への AppSettings 注入)を検証する単体テスト。
/// </summary>
public class SessionRestoreTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static MainViewModel CreateViewModel(AppSettings? session, StubFileSystemService? stub = null)
        => new(stub ?? new StubFileSystemService(), new SpyFileLauncher(), session);

    private static AppSettings CreateSampleSession() => new()
    {
        TabGroups =
        [
            new TabGroup
            {
                Id = "g1",
                Name = "作業1",
                SelectedTabId = "t1",
                Tabs =
                [
                    new FolderTab { Id = "t1", Path = @"C:\work\src", Title = "src" },
                    new FolderTab { Id = "t2", Path = @"C:\work\docs", Title = "docs" },
                ],
            },
            new TabGroup
            {
                Id = "g2",
                Name = "調査",
                SelectedTabId = "t3",
                Tabs = [new FolderTab { Id = "t3", Path = @"C:\temp", Title = "temp" }],
            },
        ],
        ActiveGroupId = "g2",
        ActiveTabId = "t3",
        WindowWidth = 1600,
        WindowHeight = 900,
        LeftPaneWidth = 300,
    };

    [Fact]
    public void セッションありの場合_タブグループとタブが復元される()
    {
        var vm = CreateViewModel(CreateSampleSession());

        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal("作業1", vm.Groups[0].Name);
        Assert.Equal("調査", vm.Groups[1].Name);
        Assert.Equal(["src", "docs"], vm.Groups[0].Tabs.Select(t => t.Title).ToArray());
        Assert.Equal(@"C:\work\src", vm.Groups[0].Tabs[0].Path);
    }

    [Fact]
    public void セッションありの場合_前回のアクティブタブが復元される()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\temp", FolderListingResult.Success([]));
        var vm = CreateViewModel(CreateSampleSession(), stub);

        var ok = vm.LoadInitialFolder();

        Assert.True(ok);
        var activeTab = Assert.Single(vm.Groups.SelectMany(g => g.Tabs), t => t.IsActive);
        Assert.Equal("t3", activeTab.Id);
        Assert.Equal(@"C:\temp", vm.Folder.CurrentPath);
    }

    [Fact]
    public void セッションなしの場合_初期起動状態で開始する()
    {
        var vm = CreateViewModel(session: null);

        var group = Assert.Single(vm.Groups);
        Assert.Equal("作業1", group.Name);
        var tab = Assert.Single(group.Tabs);
        Assert.Equal(UserProfile, tab.Path);
        Assert.True(tab.IsActive);
    }

    [Fact]
    public void 空のAppSettings_壊れたJSONフォールバック相当_は初期起動状態で開始する()
    {
        // SettingsService.Load は壊れた JSON で既定の AppSettings(TabGroups 空)を返す
        var vm = CreateViewModel(new AppSettings());

        var group = Assert.Single(vm.Groups);
        Assert.Equal("作業1", group.Name);
        Assert.Equal(UserProfile, Assert.Single(group.Tabs).Path);
        Assert.Equal(MainViewModel.DefaultLeftPaneWidth, vm.LeftPaneWidth);
        Assert.Equal(0, vm.RestoredWindowWidth);
        Assert.Equal(0, vm.RestoredWindowHeight);
    }

    [Fact]
    public void 閉じたタブ履歴が復元されCtrlShiftTで復元できる()
    {
        var session = CreateSampleSession();
        session.ClosedTabs =
        [
            new ClosedTab { Path = @"C:\closed", Title = "closed", GroupId = "g1", TabIndex = 1 },
        ];
        var vm = CreateViewModel(session);

        var ok = vm.RestoreClosedTab();

        Assert.True(ok);
        Assert.Equal(3, vm.Groups[0].Tabs.Count);
        Assert.Equal(@"C:\closed", vm.Groups[0].Tabs[1].Path);
        Assert.True(vm.Groups[0].Tabs[1].IsActive);
    }

    [Fact]
    public void アクティブタブが実在しない場合_アクティブグループの選択タブにフォールバックする()
    {
        var session = CreateSampleSession();
        session.ActiveTabId = "missing";
        session.ActiveGroupId = "g1";
        var vm = CreateViewModel(session);

        var activeTab = Assert.Single(vm.Groups.SelectMany(g => g.Tabs), t => t.IsActive);
        Assert.Equal("t1", activeTab.Id);
    }

    [Fact]
    public void 上限を超えるグループとタブは切り捨てて復元する()
    {
        var session = new AppSettings
        {
            TabGroups = Enumerable.Range(1, 6).Select(i => new TabGroup
            {
                Id = $"g{i}",
                Name = $"作業{i}",
                Tabs = Enumerable.Range(1, 21).Select(j => new FolderTab
                {
                    Id = $"g{i}-t{j}",
                    Path = @"C:\temp",
                    Title = "temp",
                }).ToList(),
            }).ToList(),
        };
        var vm = CreateViewModel(session);

        Assert.Equal(5, vm.Groups.Count);
        Assert.All(vm.Groups, g => Assert.Equal(20, g.Tabs.Count));
        // 切り捨て後も上限制御が破綻しない(タブ追加は上限エラーになる)
        Assert.False(vm.AddTabToActiveGroup());
        Assert.NotNull(vm.OperationError);
    }

    [Fact]
    public void ウィンドウサイズの保存値が復元用プロパティに反映される()
    {
        var vm = CreateViewModel(CreateSampleSession());

        Assert.Equal(1600, vm.RestoredWindowWidth);
        Assert.Equal(900, vm.RestoredWindowHeight);
        Assert.Equal(300, vm.LeftPaneWidth);
    }

    [Theory]
    [InlineData(0, 220)]
    [InlineData(-10, 220)]
    [InlineData(double.NaN, 220)]
    [InlineData(100, 150)]
    [InlineData(300, 300)]
    public void 左カラム幅は既定220_最小150に補正される(double saved, double expected)
    {
        var session = CreateSampleSession();
        session.LeftPaneWidth = saved;

        var vm = CreateViewModel(session);

        Assert.Equal(expected, vm.LeftPaneWidth);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void フォルダツリー表示状態が復元される(bool saved)
    {
        var session = CreateSampleSession();
        session.IsFolderTreeVisible = saved;

        var vm = CreateViewModel(session);

        Assert.Equal(saved, vm.IsFolderTreeVisible);
    }

    [Fact]
    public void セッションなしの場合_フォルダツリーは既定で表示される()
    {
        var vm = CreateViewModel(session: null);

        Assert.True(vm.IsFolderTreeVisible);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void ウィンドウサイズの保存値が不正な場合は復元しない(double saved)
    {
        var session = CreateSampleSession();
        session.WindowWidth = saved;
        session.WindowHeight = saved;

        var vm = CreateViewModel(session);

        Assert.Equal(0, vm.RestoredWindowWidth);
        Assert.Equal(0, vm.RestoredWindowHeight);
    }
}
