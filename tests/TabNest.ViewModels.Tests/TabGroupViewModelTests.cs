using TabNest.Core.Models;

namespace TabNest.ViewModels.Tests;

public class TabGroupViewModelTests
{
    private static (TabGroupViewModel Vm, TabGroup Model) Create(string name = "作業1")
    {
        var model = new TabGroup
        {
            Id = "g1",
            Name = name,
            Tabs =
            [
                new FolderTab { Id = "t1", Path = @"C:\a", Title = "a" },
                new FolderTab { Id = "t2", Path = @"C:\b", Title = "b" },
            ],
        };
        return (new TabGroupViewModel(model), model);
    }

    [Fact]
    public void 初期状態でモデルの名前とタブが反映される()
    {
        var (vm, _) = Create("作業A");

        Assert.Equal("作業A", vm.Name);
        Assert.False(vm.IsEditingName);
        Assert.Equal(["a", "b"], vm.Tabs.Select(t => t.Title).ToArray());
    }

    [Fact]
    public void BeginRename_編集状態になり現在名が入る()
    {
        var (vm, _) = Create("作業1");

        vm.BeginRename();

        Assert.True(vm.IsEditingName);
        Assert.Equal("作業1", vm.EditingName);
    }

    [Fact]
    public void CommitRename_確定でTabGroupのNameが更新される()
    {
        var (vm, model) = Create("作業1");
        vm.BeginRename();
        vm.EditingName = "リリース準備";

        vm.CommitRename();

        Assert.False(vm.IsEditingName);
        Assert.Equal("リリース準備", vm.Name);
        Assert.Equal("リリース準備", model.Name);
    }

    [Fact]
    public void CancelRename_キャンセルで元の名前が維持される()
    {
        var (vm, model) = Create("作業1");
        vm.BeginRename();
        vm.EditingName = "変更途中";

        vm.CancelRename();

        Assert.False(vm.IsEditingName);
        Assert.Equal("作業1", vm.Name);
        Assert.Equal("作業1", model.Name);
    }

    [Fact]
    public void CommitRename_空白のみの場合は元の名前を維持する()
    {
        var (vm, model) = Create("作業1");
        vm.BeginRename();
        vm.EditingName = "   ";

        vm.CommitRename();

        Assert.False(vm.IsEditingName);
        Assert.Equal("作業1", vm.Name);
        Assert.Equal("作業1", model.Name);
    }

    [Fact]
    public void CommitRename_前後の空白は除去される()
    {
        var (vm, model) = Create("作業1");
        vm.BeginRename();
        vm.EditingName = "  src  ";

        vm.CommitRename();

        Assert.Equal("src", vm.Name);
        Assert.Equal("src", model.Name);
    }

    [Fact]
    public void CommitRename_編集中でなければ何もしない()
    {
        var (vm, model) = Create("作業1");
        vm.EditingName = "勝手な変更";

        vm.CommitRename();

        Assert.Equal("作業1", vm.Name);
        Assert.Equal("作業1", model.Name);
    }

    [Fact]
    public void 再編集ではキャンセル前の入力が残らない()
    {
        var (vm, _) = Create("作業1");
        vm.BeginRename();
        vm.EditingName = "破棄される入力";
        vm.CancelRename();

        vm.BeginRename();

        Assert.Equal("作業1", vm.EditingName);
    }
}
