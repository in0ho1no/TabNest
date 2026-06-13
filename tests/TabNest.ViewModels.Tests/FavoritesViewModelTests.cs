using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>Task 4-4: MainViewModel のお気に入り操作(保存・開く・削除・セッション連携)を検証する。</summary>
public class FavoritesViewModelTests
{
    private static MainViewModel CreateViewModel(
        StubFileSystemService? stub = null, AppSettings? session = null)
        => new(stub ?? new StubFileSystemService(), new SpyFileLauncher(), session);

    [Fact]
    public void 指定グループをお気に入りに保存できる_アクティブグループでなくてもよい()
    {
        var vm = CreateViewModel();
        Assert.True(vm.AddGroupWithDefaultTab()); // 作業2 を追加(こちらがアクティブになる)
        var inactiveGroupId = vm.Groups[0].Id;

        var ok = vm.SaveGroupAsFavorite(inactiveGroupId);

        Assert.True(ok);
        var favorite = Assert.Single(vm.Favorites);
        Assert.Equal("作業1", favorite.Name);
        Assert.Equal([vm.Groups[0].Tabs[0].Path], favorite.Paths);
        Assert.Null(vm.OperationError);
    }

    [Fact]
    public void 存在しないグループの保存はエラーになる()
    {
        var vm = CreateViewModel();

        var ok = vm.SaveGroupAsFavorite("missing");

        Assert.False(ok);
        Assert.NotNull(vm.OperationError);
        Assert.Empty(vm.Favorites);
    }

    [Fact]
    public void お気に入りを開くと新しい段が追加され先頭タブのフォルダが表示される()
    {
        var stub = new StubFileSystemService();
        stub.Setup(@"C:\fav1", FolderListingResult.Success([]));
        var vm = CreateViewModel(stub);
        vm.Folder.LoadFolder(@"C:\fav1"); // アクティブタブを C:\fav1 にしてから保存する
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        var favorite = Assert.Single(vm.Favorites);

        var ok = vm.OpenFavorite(favorite.Id);

        Assert.True(ok);
        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal("作業1", vm.Groups[1].Name); // お気に入り名を引き継ぐ
        var openedTab = Assert.Single(vm.Groups[1].Tabs);
        Assert.Equal(@"C:\fav1", openedTab.Path);
        Assert.True(openedTab.IsActive);
        Assert.Equal(@"C:\fav1", vm.Folder.CurrentPath);
    }

    [Fact]
    public void 既に5段ある場合はお気に入りを開かずエラーを表示する()
    {
        var vm = CreateViewModel();
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        var favorite = Assert.Single(vm.Favorites);
        while (vm.Groups.Count < 5)
        {
            Assert.True(vm.AddGroupWithDefaultTab());
        }

        var ok = vm.OpenFavorite(favorite.Id);

        Assert.False(ok);
        Assert.NotNull(vm.OperationError);
        Assert.Equal(5, vm.Groups.Count);
    }

    [Fact]
    public void お気に入りを削除できる()
    {
        var vm = CreateViewModel();
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        var favorite = Assert.Single(vm.Favorites);

        Assert.True(vm.RemoveFavorite(favorite.Id));
        Assert.Empty(vm.Favorites);
        Assert.False(vm.RemoveFavorite(favorite.Id));
    }

    [Fact]
    public void お気に入りはCreateAppSettingsに含まれセッションから復元される()
    {
        var vm = CreateViewModel();
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        var settings = vm.CreateAppSettings(1280, 800, 220);
        var saved = Assert.Single(settings.SavedGroups);

        // アプリ再起動相当: 保存した AppSettings から新しい VM を作る
        var restored = CreateViewModel(session: settings);

        var favorite = Assert.Single(restored.Favorites);
        Assert.Equal(saved.Name, favorite.Name);
        Assert.Equal(saved.Paths, favorite.Paths);
        Assert.Equal(saved.Id, favorite.Id);
    }

    [Fact]
    public void お気に入り一覧は保存順に表示名どおり並ぶ()
    {
        var vm = CreateViewModel();
        var groupId = vm.Groups[0].Id;
        Assert.True(vm.SaveGroupAsFavorite(groupId));
        Assert.True(vm.SaveGroupAsFavorite(groupId)); // 同名 → 連番付き
        Assert.True(vm.AddGroupWithDefaultTab());
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[1].Id));

        Assert.Equal(["作業1", "作業1 (2)", "作業2"], vm.Favorites.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void セッション復元後もお気に入りの並びと表示名が維持される()
    {
        var session = new AppSettings
        {
            SavedGroups =
            [
                new SavedTabGroup { Id = "f1", Name = "作業B", Paths = [@"C:\b"] },
                new SavedTabGroup { Id = "f2", Name = "作業A", Paths = [@"C:\a"] },
            ],
        };

        var vm = CreateViewModel(session: session);

        // 並び替えはせず保存順(リスト順)のまま表示する
        Assert.Equal(["作業B", "作業A"], vm.Favorites.Select(f => f.Name).ToArray());
        Assert.Equal(["f1", "f2"], vm.Favorites.Select(f => f.Id).ToArray());
    }

    [Fact]
    public void 削除するとFavoritesコレクションからも消える()
    {
        var vm = CreateViewModel();
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        var first = vm.Favorites[0];

        Assert.True(vm.RemoveFavorite(first.Id));

        var remaining = Assert.Single(vm.Favorites);
        Assert.Equal("作業1 (2)", remaining.Name);
    }

    [Fact]
    public void 一覧の項目からお気に入りを開くと新しい段が生成される()
    {
        var vm = CreateViewModel();
        Assert.True(vm.SaveGroupAsFavorite(vm.Groups[0].Id));
        var item = Assert.Single(vm.Favorites);

        var ok = vm.OpenFavorite(item.Id);

        Assert.True(ok);
        Assert.Equal(2, vm.Groups.Count);
        Assert.Equal(item.Name, vm.Groups[1].Name);
        Assert.Equal(item.Paths, vm.Groups[1].Tabs.Select(t => t.Path).ToArray());
    }

    [Fact]
    public void グループVMのSaveAsFavoriteで右クリック対象のグループが保存される()
    {
        var vm = CreateViewModel();
        Assert.True(vm.AddGroupWithDefaultTab()); // 作業2 がアクティブになる

        vm.Groups[0].SaveAsFavorite(); // 非アクティブの作業1 を右クリック保存

        var favorite = Assert.Single(vm.Favorites);
        Assert.Equal("作業1", favorite.Name);
    }

    [Fact]
    public void タブ状態が復元できなくてもお気に入りは復元される()
    {
        // TabGroups が空(初期起動状態へフォールバック)でも SavedGroups は保持する
        var session = new AppSettings
        {
            SavedGroups = [new SavedTabGroup { Id = "f1", Name = "作業A", Paths = [@"C:\a"] }],
        };

        var vm = CreateViewModel(session: session);

        Assert.Equal("作業1", Assert.Single(vm.Groups).Name); // 初期起動状態
        Assert.Equal("作業A", Assert.Single(vm.Favorites).Name);
    }
}
