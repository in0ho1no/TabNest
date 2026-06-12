using System.Net.Sockets;

namespace TabNest.UiTests.Infrastructure;

/// <summary>
/// UI テストの実行環境定義(WinAppDriver の接続先とテスト対象アプリの起動方法)。
/// 実行手順は tests/TabNest.UiTests/README.md を参照。
/// </summary>
public static class UiTestEnvironment
{
    /// <summary>WinAppDriver の既定の待ち受け URL。</summary>
    public const string WinAppDriverUrl = "http://127.0.0.1:4723";

    /// <summary>
    /// テスト対象アプリの AUMID を上書きする環境変数名。
    /// パッケージ再作成等で AUMID が変わった場合は
    /// `Get-StartApps | Where-Object { $_.Name -like "*TabNest*" }` で確認して設定する。
    /// </summary>
    public const string AppIdEnvironmentVariable = "TABNEST_UI_TEST_APP_ID";

    /// <summary>
    /// 既定の AUMID。Package.appxmanifest の Identity(Name="893E2482-..."、
    /// Publisher="CN=AppPublisher")から決まるため、リポジトリの manifest を変更しない限り共通。
    /// 事前に `dotnet run --project src/TabNest.App/TabNest.App.csproj -p:Platform=x64` 等で
    /// パッケージが登録されている必要がある。
    /// </summary>
    public const string DefaultAppId = "893E2482-2E47-47E9-A41F-73DAA6BE7D3A_1z32rh13vfry6!App";

    /// <summary>テスト対象アプリの AUMID(環境変数が設定されていればそちらを優先)。</summary>
    public static string AppId
        => Environment.GetEnvironmentVariable(AppIdEnvironmentVariable) is { Length: > 0 } overridden
            ? overridden
            : DefaultAppId;

    /// <summary>テスト対象アプリのプロセス名(起動完了の検出とアタッチ対象の特定に使う)。</summary>
    public const string AppProcessName = "TabNest.App";

    /// <summary>
    /// WinAppDriver が起動しているか(待ち受けポートへの TCP 接続で判定)。
    /// 未起動の環境では UI テストをスキップするために使う。
    /// </summary>
    public static bool IsWinAppDriverRunning()
    {
        try
        {
            var uri = new Uri(WinAppDriverUrl);
            using var client = new TcpClient();
            return client.ConnectAsync(uri.Host, uri.Port).Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex) when (ex is SocketException or AggregateException)
        {
            return false;
        }
    }
}
