namespace TabNest.ViewModels.Tests;

public class MainViewModelTests
{
    [Fact]
    public void Title_初期値はTabNest()
    {
        var vm = new MainViewModel();

        Assert.Equal("TabNest", vm.Title);
    }

    [Fact]
    public void Title_変更時にPropertyChangedが発火する()
    {
        var vm = new MainViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Title = "Changed";

        Assert.Equal("Changed", vm.Title);
        Assert.Contains(nameof(MainViewModel.Title), raised);
    }

    [Fact]
    public void Title_同値設定ではPropertyChangedが発火しない()
    {
        var vm = new MainViewModel();
        var count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.Title = "TabNest";

        Assert.Equal(0, count);
    }
}
