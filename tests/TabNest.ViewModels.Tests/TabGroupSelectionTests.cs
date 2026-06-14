using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>
/// タブグループの選択状態(Task 8-2)の状態遷移テスト。
/// アクティブグループとは別概念の「選択状態」の設定・解除・単一性を検証する。
/// </summary>
public class TabGroupSelectionTests
{
    private static MainViewModel CreateViewModel()
        => new(new StubFileSystemService(), new SpyFileLauncher());

    /// <summary>初期状態(グループ1段)に加えてもう1段追加し、2グループの VM を返す。</summary>
    private static MainViewModel CreateWithTwoGroups()
    {
        var vm = CreateViewModel();
        vm.AddGroupWithDefaultTab();
        Assert.Equal(2, vm.Groups.Count);
        return vm;
    }

    [Fact]
    public void 初期状態では選択グループはない()
    {
        var vm = CreateViewModel();

        Assert.Null(vm.SelectedGroup);
        Assert.All(vm.Groups, g => Assert.False(g.IsSelected));
    }

    [Fact]
    public void SelectGroup_左クリックで選択状態になる()
    {
        var vm = CreateViewModel();
        var group = vm.Groups[0];

        vm.SelectGroup(group);

        Assert.Same(group, vm.SelectedGroup);
        Assert.True(group.IsSelected);
    }

    [Fact]
    public void Select_TabGroupViewModel経由でも選択される()
    {
        var vm = CreateViewModel();
        var group = vm.Groups[0];

        group.Select();

        Assert.Same(group, vm.SelectedGroup);
        Assert.True(group.IsSelected);
    }

    [Fact]
    public void SelectGroup_別グループ選択で前の選択が解除され常に1つだけ選択される()
    {
        var vm = CreateWithTwoGroups();
        var first = vm.Groups[0];
        var second = vm.Groups[1];

        vm.SelectGroup(first);
        vm.SelectGroup(second);

        Assert.Same(second, vm.SelectedGroup);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Single(vm.Groups, g => g.IsSelected);
    }

    [Fact]
    public void ClearGroupSelection_選択が解除される()
    {
        var vm = CreateViewModel();
        var group = vm.Groups[0];
        vm.SelectGroup(group);

        vm.ClearGroupSelection();

        Assert.Null(vm.SelectedGroup);
        Assert.All(vm.Groups, g => Assert.False(g.IsSelected));
    }

    [Fact]
    public void SelectGroup_Groupsに含まれないグループは無視される()
    {
        var vm = CreateViewModel();
        var stray = new TabGroupViewModel(new TabGroup { Id = "x", Name = "外部", Tabs = [] });

        vm.SelectGroup(stray);

        Assert.Null(vm.SelectedGroup);
        Assert.False(stray.IsSelected);
    }

    [Fact]
    public void SelectedGroup_変更時にPropertyChangedが発火する()
    {
        var vm = CreateViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectGroup(vm.Groups[0]);

        Assert.Contains(nameof(MainViewModel.SelectedGroup), raised);
    }

    [Fact]
    public void 選択切替でアクティブタブの状態は壊れない()
    {
        var vm = CreateViewModel();
        var activeTab = vm.Groups[0].Tabs.Single();
        Assert.True(activeTab.IsActive);

        vm.SelectGroup(vm.Groups[0]);
        vm.ClearGroupSelection();

        // 選択状態の切り替えはアクティブタブ表示に影響しない
        Assert.True(activeTab.IsActive);
    }

    [Fact]
    public void RemoveGroup_選択中グループを削除すると選択も解除される()
    {
        var vm = CreateWithTwoGroups();
        var target = vm.Groups[0];
        vm.SelectGroup(target);

        var removed = vm.RemoveGroup(target.Id);

        Assert.True(removed);
        Assert.Null(vm.SelectedGroup);
        Assert.DoesNotContain(vm.Groups, g => g.IsSelected);
    }

    [Fact]
    public void RemoveGroup_非選択グループの削除では選択が維持される()
    {
        var vm = CreateWithTwoGroups();
        var selected = vm.Groups[0];
        var other = vm.Groups[1];
        vm.SelectGroup(selected);

        vm.RemoveGroup(other.Id);

        Assert.Same(selected, vm.SelectedGroup);
        Assert.True(selected.IsSelected);
    }
}
