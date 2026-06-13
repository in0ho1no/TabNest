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

    /// <summary>タブ3個(t1,t2,t3)と並べ替え通知の記録を持つ VM を作る(Task 7-1 用)。</summary>
    private static (TabGroupViewModel Vm, List<IReadOnlyList<string>> Reorders) CreateForReorder()
    {
        var model = new TabGroup
        {
            Id = "g1",
            Name = "作業1",
            Tabs =
            [
                new FolderTab { Id = "t1", Path = @"C:\a", Title = "a" },
                new FolderTab { Id = "t2", Path = @"C:\b", Title = "b" },
                new FolderTab { Id = "t3", Path = @"C:\c", Title = "c" },
            ],
        };
        var reorders = new List<IReadOnlyList<string>>();
        var vm = new TabGroupViewModel(model, reorderTabs: ids => reorders.Add(ids));
        return (vm, reorders);
    }

    [Fact]
    public void MoveTab_先頭タブを末尾へ移動すると表示順と通知が更新される()
    {
        var (vm, reorders) = CreateForReorder();
        var first = vm.Tabs[0];

        // 移動前座標で末尾(Count)を挿入位置に指定する
        var moved = vm.MoveTab(first, vm.Tabs.Count);

        Assert.True(moved);
        Assert.Equal(["t2", "t3", "t1"], vm.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal(["t2", "t3", "t1"], Assert.Single(reorders).ToArray());
    }

    [Fact]
    public void MoveTab_末尾タブを先頭へ移動できる()
    {
        var (vm, reorders) = CreateForReorder();
        var last = vm.Tabs[2];

        var moved = vm.MoveTab(last, 0);

        Assert.True(moved);
        Assert.Equal(["t3", "t1", "t2"], vm.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal(["t3", "t1", "t2"], Assert.Single(reorders).ToArray());
    }

    [Fact]
    public void MoveTab_同じ位置への移動は何もせず通知しない()
    {
        var (vm, reorders) = CreateForReorder();
        var first = vm.Tabs[0];

        // 先頭タブの挿入位置として 0(自分自身の直前)を指定 → 変化なし
        var moved = vm.MoveTab(first, 0);

        Assert.False(moved);
        Assert.Equal(["t1", "t2", "t3"], vm.Tabs.Select(t => t.Id).ToArray());
        Assert.Empty(reorders);
    }

    [Fact]
    public void MoveTab_並べ替えてもアクティブタブが維持される()
    {
        var (vm, _) = CreateForReorder();
        var active = vm.Tabs[1];
        active.IsActive = true;

        vm.MoveTab(vm.Tabs[0], vm.Tabs.Count);

        // 並べ替え後も同じタブ(t2)がアクティブのまま
        Assert.True(active.IsActive);
        Assert.Single(vm.Tabs, t => t.IsActive);
        Assert.Equal("t2", vm.Tabs.Single(t => t.IsActive).Id);
    }

    [Fact]
    public void SetDropIndicator_指定タブの片側だけにインジケータが立つ()
    {
        var (vm, _) = CreateForReorder();

        vm.SetDropIndicator(vm.Tabs[1], after: true);

        Assert.False(vm.Tabs[1].IsDropBefore);
        Assert.True(vm.Tabs[1].IsDropAfter);
        Assert.All(vm.Tabs.Where((_, i) => i != 1), t =>
        {
            Assert.False(t.IsDropBefore);
            Assert.False(t.IsDropAfter);
        });
    }

    [Fact]
    public void ClearDropIndicators_すべてのインジケータが消える()
    {
        var (vm, _) = CreateForReorder();
        vm.SetDropIndicator(vm.Tabs[0], after: false);

        vm.ClearDropIndicators();

        Assert.All(vm.Tabs, t =>
        {
            Assert.False(t.IsDropBefore);
            Assert.False(t.IsDropAfter);
        });
    }
}
