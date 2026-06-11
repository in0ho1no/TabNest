using TabNest.Core.Models;
using TabNest.ViewModels.Tests.TestDoubles;

namespace TabNest.ViewModels.Tests;

/// <summary>フォルダツリー(遅延読み込み・選択連携・追従)のテスト(Task 3-9)。</summary>
public class FolderTreeTests
{
    private static FileSystemEntry Dir(string fullPath) => new()
    {
        Name = System.IO.Path.GetFileName(fullPath),
        FullPath = fullPath,
        IsDirectory = true,
        LastModifiedAt = new DateTime(2026, 6, 1),
        SizeInBytes = null,
    };

    private static FileSystemEntry File(string fullPath) => new()
    {
        Name = System.IO.Path.GetFileName(fullPath),
        FullPath = fullPath,
        IsDirectory = false,
        LastModifiedAt = new DateTime(2026, 6, 1),
        SizeInBytes = 1,
    };

    private static (FolderTreeViewModel Tree, StubFileSystemService Stub, List<string> Navigated) Create()
    {
        var stub = new StubFileSystemService();
        stub.DriveRoots.Add(@"C:\");
        stub.DriveRoots.Add(@"D:\");
        var navigated = new List<string>();
        var tree = new FolderTreeViewModel(stub, navigated.Add);
        return (tree, stub, navigated);
    }

    [Fact]
    public void ルートに利用可能なドライブが表示される()
    {
        var (tree, _, _) = Create();

        Assert.Equal([@"C:\", @"D:\"], tree.Roots.Select(r => r.Name).ToArray());
    }

    [Fact]
    public void 未展開ノードはダミー子を1つ持つ()
    {
        var (tree, _, _) = Create();

        var dummy = Assert.Single(tree.Roots[0].Children);
        Assert.True(dummy.IsPlaceholder);
    }

    [Fact]
    public void 展開時にフォルダのみの実子へ差し替えられる()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success(
        [
            Dir(@"C:\work"),
            File(@"C:\pagefile.sys"),
            Dir(@"C:\Users"),
        ]));

        tree.Roots[0].IsExpanded = true;

        // フォルダのみ・名前昇順
        Assert.Equal(["Users", "work"], tree.Roots[0].Children.Select(c => c.Name).ToArray());
        Assert.All(tree.Roots[0].Children, c => Assert.False(c.IsPlaceholder));
    }

    [Fact]
    public void アクセス拒否のフォルダは展開時に子なしになる()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Failure("アクセスが拒否されました"));

        tree.Roots[0].IsExpanded = true;

        Assert.Empty(tree.Roots[0].Children);
    }

    [Fact]
    public void 展開済みノードのフォルダが削除されていたら子なしへ戻る()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([Dir(@"C:\work")]));
        var root = tree.Roots[0];
        root.IsExpanded = true;
        Assert.Single(root.Children);

        // フォルダ削除をシミュレートして再展開
        stub.Setup(@"C:\", FolderListingResult.Failure("削除されました"));
        root.IsExpanded = false;
        root.IsExpanded = true;

        Assert.Empty(root.Children);
    }

    [Fact]
    public void ノード選択でナビゲーションが要求される()
    {
        var (tree, stub, navigated) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([Dir(@"C:\work")]));
        tree.Roots[0].IsExpanded = true;

        tree.ActivateNode(tree.Roots[0].Children[0]);

        Assert.Equal([@"C:\work"], navigated);
    }

    [Fact]
    public void ダミーノードの選択は無視される()
    {
        var (tree, _, navigated) = Create();

        tree.ActivateNode(tree.Roots[0].Children[0]);

        Assert.Empty(navigated);
    }

    [Fact]
    public void RevealPath_パス上のノードのみ展開して対象を選択する()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([Dir(@"C:\work"), Dir(@"C:\Users")]));
        stub.Setup(@"C:\work", FolderListingResult.Success([Dir(@"C:\work\src"), Dir(@"C:\work\docs")]));

        tree.RevealPath(@"C:\work\src");

        var root = tree.Roots[0];
        Assert.True(root.IsExpanded);
        var work = root.Children.First(c => c.Name == "work");
        Assert.True(work.IsExpanded);
        var src = work.Children.First(c => c.Name == "src");
        Assert.True(src.IsSelected);
        Assert.False(src.IsExpanded); // 対象ノード自体は展開しない
        // 無関係なノードは展開されない
        Assert.False(root.Children.First(c => c.Name == "Users").IsExpanded);
    }

    [Fact]
    public void RevealPath_ドライブルートはルートノードを選択する()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"D:\", FolderListingResult.Success([]));

        tree.RevealPath(@"D:\");

        Assert.True(tree.Roots[1].IsSelected);
        Assert.False(tree.Roots[0].IsSelected);
    }

    [Fact]
    public void RevealPath_追従できない場合は選択解除のみでエラーにならない()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([Dir(@"C:\work")]));
        tree.RevealPath(@"C:\work");
        Assert.True(tree.Roots[0].Children[0].IsSelected);

        // 存在しないドライブ
        tree.RevealPath(@"Z:\nowhere");

        Assert.False(tree.Roots[0].Children[0].IsSelected);
        Assert.DoesNotContain(tree.Roots.SelectMany(r => r.Children).Append(tree.Roots[0]).Append(tree.Roots[1]), n => n.IsSelected);
    }

    [Fact]
    public void RevealPath_選択の移動で前の選択が解除される()
    {
        var (tree, stub, _) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([Dir(@"C:\work"), Dir(@"C:\Users")]));

        tree.RevealPath(@"C:\work");
        var work = tree.Roots[0].Children.First(c => c.Name == "work");
        Assert.True(work.IsSelected);

        tree.RevealPath(@"C:\Users");
        var users = tree.Roots[0].Children.First(c => c.Name == "Users");
        Assert.True(users.IsSelected);
        Assert.False(work.IsSelected);
    }

    [Fact]
    public void RevealPath_中はナビゲーションコールバックが発火しない()
    {
        var (tree, stub, navigated) = Create();
        stub.Setup(@"C:\", FolderListingResult.Success([Dir(@"C:\work")]));

        tree.RevealPath(@"C:\work");

        Assert.Empty(navigated); // 追従(選択)だけでタブ移動は起きない
    }
}
