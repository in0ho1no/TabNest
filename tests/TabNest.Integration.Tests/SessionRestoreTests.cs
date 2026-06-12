using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Integration.Tests;

/// <summary>
/// Task 4-3: settings.json(実ファイル)からのセッション復元の結合テスト。
/// 保存 → 読み込み → TabManagerService への復元までを一時フォルダで検証する。
/// </summary>
public sealed class SessionRestoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _settingsPath;

    public SessionRestoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "TabNest.Tests", Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_tempRoot, "TabNest", "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static AppSettings CreateSampleSettings() => new()
    {
        TabGroups =
        [
            new TabGroup
            {
                Id = "g1",
                Name = "作業1",
                SelectedTabId = "t2",
                Tabs =
                [
                    new FolderTab { Id = "t1", Path = @"C:\work\src", Title = "src" },
                    new FolderTab { Id = "t2", Path = @"C:\work\docs", Title = "docs" },
                ],
            },
        ],
        ClosedTabs =
        [
            new ClosedTab { Path = @"C:\temp", Title = "temp", GroupId = "g1", TabIndex = 0 },
        ],
        ActiveGroupId = "g1",
        ActiveTabId = "t2",
        WindowWidth = 1600,
        WindowHeight = 900,
        LeftPaneWidth = 260,
    };

    [Fact]
    public void settingsJsonありの場合_保存したセッションがTabManagerに復元される()
    {
        Assert.True(new SettingsService(_settingsPath).Save(CreateSampleSettings()));

        // 別インスタンスで読み込み、タブ状態を復元する(アプリ再起動相当)
        var loaded = new SettingsService(_settingsPath).Load();
        var tabManager = new TabManagerService();
        var restored = tabManager.RestoreSession(loaded);

        Assert.True(restored);
        var group = Assert.Single(tabManager.Groups);
        Assert.Equal("作業1", group.Name);
        Assert.Equal(["t1", "t2"], group.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal(@"C:\work\docs", group.Tabs[1].Path);
        Assert.Equal("g1", tabManager.ActiveGroupId);
        Assert.Equal("t2", tabManager.ActiveTabId);
        Assert.Equal("t2", group.SelectedTabId);
        var closed = Assert.Single(tabManager.ClosedTabs);
        Assert.Equal(@"C:\temp", closed.Path);
        Assert.Equal(1600, loaded.WindowWidth);
        Assert.Equal(900, loaded.WindowHeight);
        Assert.Equal(260, loaded.LeftPaneWidth);
    }

    [Fact]
    public void settingsJsonなしの場合_復元されず初期起動状態にフォールバックする()
    {
        var loaded = new SettingsService(_settingsPath).Load();
        var tabManager = new TabManagerService();

        Assert.False(tabManager.RestoreSession(loaded));
        Assert.Empty(tabManager.Groups);
    }

    [Fact]
    public void 壊れたsettingsJsonの場合_復元されず初期起動状態にフォールバックする()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, "{ broken json !!");

        var loaded = new SettingsService(_settingsPath).Load();
        var tabManager = new TabManagerService();

        Assert.False(tabManager.RestoreSession(loaded));
        Assert.Empty(tabManager.Groups);
    }
}
