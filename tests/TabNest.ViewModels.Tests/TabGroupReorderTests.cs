using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>タブグループ段の並べ替え(グループ段の D&amp;D。Task 7-3)のテスト。</summary>
public class TabGroupReorderTests
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>3グループ(作業1/作業2/作業3。各1タブ)を持つ MainViewModel を作る。</summary>
    private static MainViewModel Create()
    {
        var stub = new StubFileSystemService();
        stub.Setup(UserProfile, FolderListingResult.Success([]));
        var vm = new MainViewModel(stub, new SpyFileLauncher());
        vm.AddGroupWithDefaultTab(); // 作業2
        vm.AddGroupWithDefaultTab(); // 作業3
        return vm;
    }

    [Fact]
    public void MoveGroup_指定段の直前へ段が移動し表示順が更新される()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g3 = vm.Groups[2];

        // g3 を g1 の直前(先頭)へ移動する
        var ok = vm.MoveGroup(g3, g1.Id, below: false);

        Assert.True(ok);
        Assert.Equal([g3, g1, vm.Groups[2]], vm.Groups);
        Assert.Same(g3, vm.Groups[0]);
    }

    [Fact]
    public void MoveGroup_指定段の直後へ段が移動する()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];
        var g3 = vm.Groups[2];

        // g1 を g3 の直後(末尾)へ移動する
        var ok = vm.MoveGroup(g1, g3.Id, below: true);

        Assert.True(ok);
        Assert.Equal([g2, g3, g1], vm.Groups);
    }

    [Fact]
    public void MoveGroup_並べ替えてもアクティブ状態と各グループ内容が保持される()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g3 = vm.Groups[2];
        var activeTab = g3.Tabs[0];
        vm.SelectTab(activeTab);
        Assert.True(activeTab.IsActive);

        var ok = vm.MoveGroup(g3, g1.Id, below: false);

        Assert.True(ok);
        // アクティブタブは並べ替え後も同一インスタンスのままアクティブ
        Assert.True(activeTab.IsActive);
        Assert.Same(activeTab, vm.Groups[0].Tabs[0]);
        // 他グループの内容も保持される
        Assert.Single(g1.Tabs);
    }

    [Fact]
    public void MoveGroup_並べ替え後の段順がセッション保存に反映される()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g3 = vm.Groups[2];

        vm.MoveGroup(g3, g1.Id, below: false);
        var settings = vm.CreateAppSettings(0, 0, 0);

        // CreateAppSettings(=settings.json 保存対象)の段順が表示順と一致する
        Assert.Equal(
            vm.Groups.Select(g => g.Id).ToArray(),
            settings.TabGroups.Select(g => g.Id).ToArray());
    }

    [Fact]
    public void MoveGroup_同じ位置への移動は何もせずfalseを返す()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g2 = vm.Groups[1];

        // g1 を g2 の直前 = 既に g1 がいる位置 → 変化なし
        var ok = vm.MoveGroup(g1, g2.Id, below: false);

        Assert.False(ok);
        Assert.Equal([g1, g2, vm.Groups[2]], vm.Groups);
    }

    [Fact]
    public void MoveGroup_自分自身を対象にした場合はfalseを返す()
    {
        var vm = Create();
        var g1 = vm.Groups[0];

        var ok = vm.MoveGroup(g1, g1.Id, below: true);

        Assert.False(ok);
    }

    [Fact]
    public void MoveGroupHere_TabGroupViewModel経由でも段を並べ替えられる()
    {
        var vm = Create();
        var g1 = vm.Groups[0];
        var g3 = vm.Groups[2];

        // View が呼ぶ経路(ドロップ先段の VM に対して呼ぶ)を再現する: g3 を g1 の直前へ
        var ok = g1.MoveGroupHere(g3, below: false);

        Assert.True(ok);
        Assert.Same(g3, vm.Groups[0]);
    }
}
