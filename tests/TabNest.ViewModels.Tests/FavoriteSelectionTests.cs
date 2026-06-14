using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// お気に入りの選択状態(Task 8-3)の状態遷移テスト。
/// 左クリック=選択のみ(開かない)、ホイールクリック=開く、の分岐を検証する。
/// 中クリック検出自体は View 側(PointerPressed)の責務で GUI 評価で補完する。
/// </summary>
public class FavoriteSelectionTests
{
    private static MainViewModel CreateViewModel()
        => new(new StubFileSystemService(), new SpyFileLauncher());

    /// <summary>お気に入りを <paramref name="count"/> 件保存した VM を返す。</summary>
    private static MainViewModel CreateWithFavorites(int count)
    {
        var vm = CreateViewModel();
        for (var i = 0; i < count; i++)
        {
            Assert.True(vm.AddGroupWithDefaultTab());
            Assert.True(vm.SaveGroupAsFavorite(vm.Groups[^1].Id));
        }

        Assert.Equal(count, vm.Favorites.Count);
        return vm;
    }

    [Fact]
    public void 初期状態では選択お気に入りはない()
    {
        var vm = CreateWithFavorites(1);

        Assert.Null(vm.SelectedFavorite);
        Assert.All(vm.Favorites, f => Assert.False(f.IsSelected));
    }

    [Fact]
    public void SelectFavorite_左クリックでは選択状態になるだけで開かない()
    {
        var vm = CreateWithFavorites(1);
        var groupCountBefore = vm.Groups.Count;
        var favorite = vm.Favorites[0];

        vm.SelectFavorite(favorite.Id);

        Assert.Same(favorite, vm.SelectedFavorite);
        Assert.True(favorite.IsSelected);
        // 開かない: 段は増えない
        Assert.Equal(groupCountBefore, vm.Groups.Count);
    }

    [Fact]
    public void OpenFavorite_ホイールクリック相当で1回開くと新しい段が増える()
    {
        var vm = CreateWithFavorites(1);
        var groupCountBefore = vm.Groups.Count;
        var favorite = vm.Favorites[0];

        var ok = vm.OpenFavorite(favorite.Id);

        Assert.True(ok);
        Assert.Equal(groupCountBefore + 1, vm.Groups.Count);
    }

    [Fact]
    public void SelectFavorite_別お気に入り選択で前の選択が解除され常に1つだけ選択される()
    {
        var vm = CreateWithFavorites(2);
        var first = vm.Favorites[0];
        var second = vm.Favorites[1];

        vm.SelectFavorite(first.Id);
        vm.SelectFavorite(second.Id);

        Assert.Same(second, vm.SelectedFavorite);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Single(vm.Favorites, f => f.IsSelected);
    }

    [Fact]
    public void ClearFavoriteSelection_選択が解除される()
    {
        var vm = CreateWithFavorites(1);
        vm.SelectFavorite(vm.Favorites[0].Id);

        vm.ClearFavoriteSelection();

        Assert.Null(vm.SelectedFavorite);
        Assert.All(vm.Favorites, f => Assert.False(f.IsSelected));
    }

    [Fact]
    public void SelectFavorite_Favoritesに含まれないIdは無視される()
    {
        var vm = CreateWithFavorites(1);

        vm.SelectFavorite("missing");

        Assert.Null(vm.SelectedFavorite);
        Assert.All(vm.Favorites, f => Assert.False(f.IsSelected));
    }

    [Fact]
    public void SelectedFavorite_変更時にPropertyChangedが発火する()
    {
        var vm = CreateWithFavorites(1);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectFavorite(vm.Favorites[0].Id);

        Assert.Contains(nameof(MainViewModel.SelectedFavorite), raised);
    }

    [Fact]
    public void RemoveFavorite_選択中のお気に入りを削除すると選択も解除される()
    {
        var vm = CreateWithFavorites(2);
        var target = vm.Favorites[0];
        vm.SelectFavorite(target.Id);

        var removed = vm.RemoveFavorite(target.Id);

        Assert.True(removed);
        Assert.Null(vm.SelectedFavorite);
        Assert.DoesNotContain(vm.Favorites, f => f.IsSelected);
    }

    [Fact]
    public void RemoveFavorite_非選択のお気に入り削除では選択が維持される()
    {
        var vm = CreateWithFavorites(2);
        var selected = vm.Favorites[0];
        var other = vm.Favorites[1];
        vm.SelectFavorite(selected.Id);

        vm.RemoveFavorite(other.Id);

        Assert.Same(selected, vm.SelectedFavorite);
        Assert.True(selected.IsSelected);
    }
}
