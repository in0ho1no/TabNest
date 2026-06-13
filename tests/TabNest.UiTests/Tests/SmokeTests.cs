using TabNest.UiTests.Infrastructure;

namespace TabNest.UiTests.Tests;

/// <summary>Task 5-1: UI テスト基盤の動作確認。</summary>
public class SmokeTests
{
    /// <summary>
    /// 完了条件「空のUIテストが実行できる」: WinAppDriver の有無に関わらず
    /// dotnet test tests/TabNest.UiTests が成功することを保証するプレースホルダー。
    /// </summary>
    [Fact]
    public void 空のUIテストが実行できる()
    {
    }

    /// <summary>
    /// WinAppDriver 起動時のみ実行: テスト対象アプリ(AUMID)を起動して
    /// セッションが確立できることを確認する。未起動時はスキップされる。
    /// </summary>
    [UiFact]
    [Trait("Category", "UITest")]
    public void アプリを起動してセッションを確立できる()
    {
        using var session = new AppSession();

        Assert.NotNull(session.Driver.SessionId);
        Assert.NotEqual(IntPtr.Zero.ToString(), session.Driver.CurrentWindowHandle);
    }
}
