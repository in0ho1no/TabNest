using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Integration.Tests;

/// <summary>SettingsService の結合テスト(一時フォルダへの実ファイル保存・読み戻し)。</summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _settingsPath;

    public SettingsServiceTests()
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
                    new FolderTab { Id = "t1", Path = @"C:\work\src", Title = "src", CreatedAt = new DateTime(2026, 6, 1, 10, 0, 0) },
                    new FolderTab { Id = "t2", Path = @"C:\work\docs", Title = "docs", CreatedAt = new DateTime(2026, 6, 1, 10, 5, 0) },
                ],
            },
            new TabGroup { Id = "g2", Name = "リリース準備", Tabs = [] },
        ],
        ClosedTabs =
        [
            new ClosedTab { Path = @"C:\temp", Title = "temp", GroupId = "g1", TabIndex = 1, ClosedAt = new DateTime(2026, 6, 2, 9, 0, 0) },
        ],
        SavedGroups =
        [
            new SavedTabGroup { Id = "f1", Name = "作業A", Paths = [@"C:\a", @"C:\b"], SavedAt = new DateTime(2026, 6, 3, 8, 0, 0) },
        ],
        ActiveGroupId = "g1",
        ActiveTabId = "t2",
        WindowWidth = 1280,
        WindowHeight = 800,
        LeftPaneWidth = 250,
    };

    [Fact]
    public void Save後にLoadすると内容が一致する()
    {
        var service = new SettingsService(_settingsPath);
        var original = CreateSampleSettings();

        Assert.True(service.Save(original));
        var loaded = new SettingsService(_settingsPath).Load();

        Assert.Equal(2, loaded.TabGroups.Count);
        Assert.Equal("作業1", loaded.TabGroups[0].Name);
        Assert.Equal("t2", loaded.TabGroups[0].SelectedTabId);
        Assert.Equal([@"C:\work\src", @"C:\work\docs"], loaded.TabGroups[0].Tabs.Select(t => t.Path).ToArray());
        Assert.Equal("docs", loaded.TabGroups[0].Tabs[1].Title);
        Assert.Equal(new DateTime(2026, 6, 1, 10, 0, 0), loaded.TabGroups[0].Tabs[0].CreatedAt);
        Assert.Empty(loaded.TabGroups[1].Tabs);

        var closed = Assert.Single(loaded.ClosedTabs);
        Assert.Equal(@"C:\temp", closed.Path);
        Assert.Equal(1, closed.TabIndex);

        var saved = Assert.Single(loaded.SavedGroups);
        Assert.Equal("作業A", saved.Name);
        Assert.Equal([@"C:\a", @"C:\b"], saved.Paths);
        Assert.Equal(new DateTime(2026, 6, 3, 8, 0, 0), saved.SavedAt);

        Assert.Equal("g1", loaded.ActiveGroupId);
        Assert.Equal("t2", loaded.ActiveTabId);
        Assert.Equal(1280, loaded.WindowWidth);
        Assert.Equal(800, loaded.WindowHeight);
        Assert.Equal(250, loaded.LeftPaneWidth);
    }

    [Fact]
    public void Saveは保存先フォルダが無ければ作成する()
    {
        var service = new SettingsService(_settingsPath);
        Assert.False(Directory.Exists(Path.GetDirectoryName(_settingsPath)));

        var ok = service.Save(new AppSettings());

        Assert.True(ok);
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void ファイルが無い場合は既定値を返す()
    {
        var service = new SettingsService(_settingsPath);

        var settings = service.Load();

        Assert.Empty(settings.TabGroups);
        Assert.Empty(settings.ClosedTabs);
        Assert.Empty(settings.SavedGroups);
        Assert.Null(settings.ActiveGroupId);
        Assert.Equal(220, settings.LeftPaneWidth);
    }

    [Fact]
    public void 壊れたJSONの場合は既定値にフォールバックする()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, "{ this is not valid json !!");
        var service = new SettingsService(_settingsPath);

        var settings = service.Load();

        Assert.Empty(settings.TabGroups);
        Assert.Equal(220, settings.LeftPaneWidth);
    }

    [Fact]
    public void 既定の保存先はAppDataのTabNest配下()
    {
        var service = new SettingsService();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TabNest",
            "settings.json");
        Assert.Equal(expected, service.SettingsFilePath);
    }
}
