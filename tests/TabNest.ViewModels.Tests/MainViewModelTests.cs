using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel(StubFileSystemService? stub = null)
        => new(stub ?? new StubFileSystemService());

    [Fact]
    public void Title_初期値はTabNest()
    {
        var vm = CreateViewModel();

        Assert.Equal("TabNest", vm.Title);
    }

    [Fact]
    public void Title_変更時にPropertyChangedが発火する()
    {
        var vm = CreateViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Title = "Changed";

        Assert.Equal("Changed", vm.Title);
        Assert.Contains(nameof(MainViewModel.Title), raised);
    }

    [Fact]
    public void Title_同値設定ではPropertyChangedが発火しない()
    {
        var vm = CreateViewModel();
        var count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.Title = "TabNest";

        Assert.Equal(0, count);
    }

    [Fact]
    public void Folder_初期状態では空()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.Folder);
        Assert.Equal("", vm.Folder.CurrentPath);
        Assert.Empty(vm.Folder.Items);
    }

    [Fact]
    public void LoadInitialFolder_ユーザープロファイルを読み込む()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var stub = new StubFileSystemService();
        stub.Setup(userProfile, FolderListingResult.Success(
        [
            new FileSystemEntry
            {
                Name = "Documents",
                FullPath = Path.Combine(userProfile, "Documents"),
                IsDirectory = true,
                LastModifiedAt = DateTime.Now,
                SizeInBytes = null,
            },
        ]));
        var vm = CreateViewModel(stub);

        var ok = vm.LoadInitialFolder();

        Assert.True(ok);
        Assert.Equal(userProfile, vm.Folder.CurrentPath);
        Assert.Single(vm.Folder.Items);
    }
}
