using TabNest.Core.Models;
using TabNest.Core.Services;

namespace TabNest.Core.Tests;

/// <summary>Task 4-4: お気に入り(保存済みタブグループ)の保存・削除・連番付与を検証する。</summary>
public class FavoritesServiceTests
{
    private static TabGroup CreateGroup(string name, params string[] paths) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        Tabs = paths.Select(p => new FolderTab
        {
            Id = Guid.NewGuid().ToString(),
            Path = p,
            Title = Path.GetFileName(p),
        }).ToList(),
    };

    [Fact]
    public void 保存_名前はグループ名と一致しパスの並び順を保持する()
    {
        var service = new FavoritesService();
        var group = CreateGroup("作業A", @"C:\work\src", @"C:\work\docs");

        var result = service.SaveFavorite(group);

        Assert.True(result.IsSuccess);
        var favorite = Assert.Single(service.SavedGroups);
        Assert.Equal("作業A", favorite.Name);
        Assert.Equal([@"C:\work\src", @"C:\work\docs"], favorite.Paths);
        Assert.NotEmpty(favorite.Id);
        Assert.NotEqual(default, favorite.SavedAt);
    }

    [Fact]
    public void 保存はスナップショット_元グループを変更してもお気に入りは変わらない()
    {
        var service = new FavoritesService();
        var group = CreateGroup("作業A", @"C:\a");
        service.SaveFavorite(group);

        group.Name = "変更後";
        group.Tabs[0].Path = @"C:\moved";
        group.Tabs.Add(new FolderTab { Id = "new", Path = @"C:\b", Title = "b" });

        var favorite = Assert.Single(service.SavedGroups);
        Assert.Equal("作業A", favorite.Name);
        Assert.Equal([@"C:\a"], favorite.Paths);
    }

    [Fact]
    public void 同名を順に保存すると連番付きの別お気に入りになり既存は変化しない()
    {
        var service = new FavoritesService();
        service.SaveFavorite(CreateGroup("作業A", @"C:\a"));

        var second = service.SaveFavorite(CreateGroup("作業A", @"C:\b"));

        Assert.True(second.IsSuccess);
        Assert.Equal(2, service.SavedGroups.Count);
        Assert.Equal("作業A", service.SavedGroups[0].Name);
        Assert.Equal([@"C:\a"], service.SavedGroups[0].Paths);
        Assert.Equal("作業A (2)", service.SavedGroups[1].Name);
        Assert.Equal([@"C:\b"], service.SavedGroups[1].Paths);
    }

    [Fact]
    public void 連番に欠番がある場合は未使用の最小値が使われる()
    {
        var service = new FavoritesService();
        service.SaveFavorite(CreateGroup("作業A", @"C:\a"));
        var second = service.SaveFavorite(CreateGroup("作業A", @"C:\b")).Value!;
        service.SaveFavorite(CreateGroup("作業A", @"C:\c"));
        Assert.True(service.RemoveFavorite(second.Id)); // 「作業A (2)」を削除して欠番を作る

        var result = service.SaveFavorite(CreateGroup("作業A", @"C:\d"));

        Assert.True(result.IsSuccess);
        Assert.Equal("作業A (2)", result.Value!.Name);
    }

    [Fact]
    public void サフィックスを含む名前は全体を1つの名前として扱う()
    {
        // ベース名抽出はしない: 「作業1 (2)」が衝突したら「作業1 (2) (2)」になる
        var service = new FavoritesService();
        service.SaveFavorite(CreateGroup("作業1 (2)", @"C:\a"));

        var result = service.SaveFavorite(CreateGroup("作業1 (2)", @"C:\b"));

        Assert.True(result.IsSuccess);
        Assert.Equal("作業1 (2) (2)", result.Value!.Name);
    }

    [Fact]
    public void タブ0個のグループは保存できない()
    {
        var service = new FavoritesService();

        var result = service.SaveFavorite(CreateGroup("空グループ"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.EmptyGroupNotSavable, result.Error);
        Assert.Empty(service.SavedGroups);
    }

    [Fact]
    public void 上限50件到達時は保存が拒否される()
    {
        var service = new FavoritesService();
        for (var i = 0; i < FavoritesService.MaxFavorites; i++)
        {
            Assert.True(service.SaveFavorite(CreateGroup($"G{i}", @"C:\a")).IsSuccess);
        }

        var result = service.SaveFavorite(CreateGroup("超過", @"C:\a"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.FavoriteLimitReached, result.Error);
        Assert.Equal(FavoritesService.MaxFavorites, service.SavedGroups.Count);
    }

    [Fact]
    public void 削除すると一覧から消え_存在しないIdはfalse()
    {
        var service = new FavoritesService();
        var saved = service.SaveFavorite(CreateGroup("作業A", @"C:\a")).Value!;

        Assert.True(service.RemoveFavorite(saved.Id));
        Assert.Empty(service.SavedGroups);
        Assert.False(service.RemoveFavorite(saved.Id));
    }

    [Fact]
    public void リネーム_衝突しない名前ならそのまま変更される()
    {
        var service = new FavoritesService();
        var saved = service.SaveFavorite(CreateGroup("作業A", @"C:\a")).Value!;

        var result = service.RenameFavorite(saved.Id, "資料");

        Assert.True(result.IsSuccess);
        Assert.Equal("資料", result.Value!.Name);
        Assert.Equal("資料", Assert.Single(service.SavedGroups).Name);
    }

    [Fact]
    public void リネーム_既存の別お気に入りと同名なら連番が付く()
    {
        var service = new FavoritesService();
        service.SaveFavorite(CreateGroup("作業A", @"C:\a"));
        var target = service.SaveFavorite(CreateGroup("作業B", @"C:\b")).Value!;

        var result = service.RenameFavorite(target.Id, "作業A");

        Assert.True(result.IsSuccess);
        Assert.Equal("作業A (2)", result.Value!.Name);
    }

    [Fact]
    public void リネーム_自分自身の現在名に変更しても連番は付かない()
    {
        var service = new FavoritesService();
        var saved = service.SaveFavorite(CreateGroup("作業A", @"C:\a")).Value!;

        var result = service.RenameFavorite(saved.Id, "作業A");

        Assert.True(result.IsSuccess);
        Assert.Equal("作業A", result.Value!.Name);
    }

    [Fact]
    public void リネーム_存在しないIdは失敗する()
    {
        var service = new FavoritesService();

        var result = service.RenameFavorite("missing", "新名称");

        Assert.False(result.IsSuccess);
        Assert.Equal(TabOperationError.FavoriteNotFound, result.Error);
    }

    [Fact]
    public void 並び替え_指定したId順に並び順が変わる()
    {
        var service = new FavoritesService();
        var a = service.SaveFavorite(CreateGroup("A", @"C:\a")).Value!;
        var b = service.SaveFavorite(CreateGroup("B", @"C:\b")).Value!;
        var c = service.SaveFavorite(CreateGroup("C", @"C:\c")).Value!;

        service.ReorderFavorites([c.Id, a.Id, b.Id]);

        Assert.Equal(["C", "A", "B"], service.SavedGroups.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void 並び替え_orderedに含まれない項目は末尾に元の順序で残る()
    {
        var service = new FavoritesService();
        var a = service.SaveFavorite(CreateGroup("A", @"C:\a")).Value!;
        var b = service.SaveFavorite(CreateGroup("B", @"C:\b")).Value!;
        var c = service.SaveFavorite(CreateGroup("C", @"C:\c")).Value!;

        // b のみ先頭へ。未知の Id は無視され、残り(a, c)は元の相対順で末尾に残る
        service.ReorderFavorites([b.Id, "unknown"]);

        Assert.Equal(["B", "A", "C"], service.SavedGroups.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void 復元_上限超過分は切り捨てられIdが空の要素には採番される()
    {
        var service = new FavoritesService();
        var source = Enumerable.Range(1, FavoritesService.MaxFavorites + 5)
            .Select(i => new SavedTabGroup { Id = i == 1 ? "" : $"f{i}", Name = $"G{i}", Paths = [@"C:\a"] })
            .ToList();

        service.RestoreSavedGroups(source);

        Assert.Equal(FavoritesService.MaxFavorites, service.SavedGroups.Count);
        Assert.NotEmpty(service.SavedGroups[0].Id);
        Assert.Equal("G1", service.SavedGroups[0].Name);
    }
}
