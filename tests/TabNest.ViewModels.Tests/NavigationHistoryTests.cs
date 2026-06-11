namespace TabNest.ViewModels.Tests;

public class NavigationHistoryTests
{
    [Fact]
    public void 初期状態では戻る進むともに不可()
    {
        var history = new NavigationHistory();

        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
        Assert.Null(history.PeekBack());
        Assert.Null(history.PeekForward());
    }

    [Fact]
    public void RecordNavigation_移動元がBackStackに積まれる()
    {
        var history = new NavigationHistory();

        history.RecordNavigation(@"C:\a");

        Assert.True(history.CanGoBack);
        Assert.Equal(@"C:\a", history.PeekBack());
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void CommitBack_移動元がForwardStackへ移る()
    {
        var history = new NavigationHistory();
        history.RecordNavigation(@"C:\a");

        history.CommitBack(@"C:\b");

        Assert.False(history.CanGoBack);
        Assert.True(history.CanGoForward);
        Assert.Equal(@"C:\b", history.PeekForward());
    }

    [Fact]
    public void CommitForward_移動元がBackStackへ戻る()
    {
        var history = new NavigationHistory();
        history.RecordNavigation(@"C:\a");
        history.CommitBack(@"C:\b");

        history.CommitForward(@"C:\a");

        Assert.True(history.CanGoBack);
        Assert.Equal(@"C:\a", history.PeekBack());
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void RecordNavigation_ForwardStackがクリアされる()
    {
        var history = new NavigationHistory();
        history.RecordNavigation(@"C:\a");
        history.CommitBack(@"C:\b");
        Assert.True(history.CanGoForward);

        history.RecordNavigation(@"C:\a");

        Assert.False(history.CanGoForward);
        Assert.Equal(@"C:\a", history.PeekBack());
    }

    [Fact]
    public void Peekは履歴を変更しない()
    {
        var history = new NavigationHistory();
        history.RecordNavigation(@"C:\a");

        _ = history.PeekBack();
        _ = history.PeekBack();

        Assert.True(history.CanGoBack);
        Assert.Equal(@"C:\a", history.PeekBack());
    }
}
