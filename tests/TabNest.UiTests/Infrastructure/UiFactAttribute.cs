namespace TabNest.UiTests.Infrastructure;

/// <summary>
/// WinAppDriver を必要とする UI テスト用の Fact。
/// WinAppDriver が起動していない環境(CI や通常の開発時)では自動的にスキップし、
/// `dotnet test TabNest.slnx` を常に成功させる。
/// </summary>
public sealed class UiFactAttribute : FactAttribute
{
    public UiFactAttribute()
    {
        if (!UiTestEnvironment.IsWinAppDriverRunning())
        {
            Skip = $"WinAppDriver が {UiTestEnvironment.WinAppDriverUrl} で起動していないためスキップしました。"
                + " 実行手順は tests/TabNest.UiTests/README.md を参照してください。";
        }
    }
}
