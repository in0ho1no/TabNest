using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Core.Tests;

/// <summary>Task 4-4: お気に入りからタブグループを開く(TabManagerService.OpenSavedGroup)を検証する。</summary>
public class OpenSavedGroupTests
{
    private static string TitleForPath(string path) => Path.GetFileName(path);

    private static SavedTabGroup CreateFavorite(string name, params string[] paths) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        Paths = paths.ToList(),
        SavedAt = DateTime.Now,
    };

    [Fact]
    public void 開いたグループはお気に入りの名前を引き継ぎ先頭タブがアクティブになる()
    {
        var manager = new TabManagerService();
        manager.AddGroup("作業1");

        var result = manager.OpenSavedGroup(
            CreateFavorite("お気に入りA", @"C:\work\src", @"C:\work\docs"), TitleForPath);

        Assert.True(result.IsSuccess);
        var group = result.Value!;
        Assert.Equal("お気に入りA", group.Name);
        Assert.Equal([@"C:\work\src", @"C:\work\docs"], group.Tabs.Select(t => t.Path).ToArray());
        Assert.Equal("src", group.Tabs[0].Title);
        Assert.Equal(group.Id, manager.ActiveGroupId);
        Assert.Equal(group.Tabs[0].Id, manager.ActiveTabId);
        Assert.Equal(group.Tabs[0].Id, group.SelectedTabId);
    }

    [Fact]
    public void 同じお気に入りを再度開くと別の段として開く()
    {
        var manager = new TabManagerService();
        var favorite = CreateFavorite("お気に入りA", @"C:\a");

        var first = manager.OpenSavedGroup(favorite, TitleForPath);
        var second = manager.OpenSavedGroup(favorite, TitleForPath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, manager.Groups.Count);
        Assert.NotEqual(first.Value!.Id, second.Value!.Id);
        Assert.All(manager.Groups, g => Assert.Equal("お気に入りA", g.Name));
    }

    [Fact]
    public void 既に5段ある場合は開かずエラー結果を返す()
    {
        var manager = new TabManagerService();
        for (var i = 1; i <= TabManagerService.MaxGroups; i++)
        {
            Assert.True(manager.AddGroup($"作業{i}").IsSuccess);
        }

        var result = manager.OpenSavedGroup(CreateFavorite("お気に入りA", @"C:\a"), TitleForPath);

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.GroupLimitReached, result.Error);
        Assert.Equal(TabManagerService.MaxGroups, manager.Groups.Count);
    }
}
