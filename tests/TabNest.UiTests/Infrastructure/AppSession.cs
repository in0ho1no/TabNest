using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace TabNest.UiTests.Infrastructure;

/// <summary>
/// テスト対象アプリ(TabNest)の起動とセッション管理。
/// アプリはテスト側でシェル活性化(shell:AppsFolder + AUMID)により起動し、
/// 表示されたウィンドウへ WinAppDriver の appTopLevelWindow capability でアタッチする。
/// (app capability による起動は、AUMID が無効・パッケージが壊れている場合に
/// エラーを返さず120秒ハングするため使わない。本方式なら起動失敗が
/// 明確な TimeoutException として切り分けられる)
/// Dispose でセッションを終了し、アプリも閉じる。
/// </summary>
public sealed class AppSession : IDisposable
{
    private readonly Process _appProcess;

    /// <summary>アプリ操作用の Appium ドライバ。</summary>
    public WindowsDriver<WindowsElement> Driver { get; }

    /// <summary>
    /// メインウィンドウのハンドル(起動時に取得した値を保持)。
    /// GetWindowRect による正確なウィンドウサイズ検証に使う
    /// (WinAppDriver の Window.Size は見えないリサイズ枠を除いた値を返すため、
    /// AppWindow.Resize の物理ピクセルと一致しない)。
    /// </summary>
    public IntPtr MainWindowHandle { get; }

    public AppSession()
    {
        _appProcess = LaunchAppAndWaitForWindow();
        MainWindowHandle = _appProcess.MainWindowHandle;

        var options = new AppiumOptions();
        options.AddAdditionalCapability(
            "appTopLevelWindow", MainWindowHandle.ToInt64().ToString("x"));
        options.AddAdditionalCapability("deviceName", "WindowsPC");
        Driver = new WindowsDriver<WindowsElement>(
            new Uri(UiTestEnvironment.WinAppDriverUrl), options, TimeSpan.FromSeconds(60));
        // 起動直後の要素探索を安定させるための暗黙待機
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
    }

    /// <summary>
    /// AUMID でアプリを起動し、メインウィンドウが表示された新規プロセスを返す。
    /// (既存インスタンスと区別するため、起動前のプロセス Id を除外して検出する)
    /// </summary>
    private static Process LaunchAppAndWaitForWindow()
    {
        var existingIds = Process.GetProcessesByName(UiTestEnvironment.AppProcessName)
            .Select(p => p.Id)
            .ToHashSet();

        using var launcher = Process.Start(new ProcessStartInfo
        {
            FileName = $@"shell:AppsFolder\{UiTestEnvironment.AppId}",
            UseShellExecute = true,
        });

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var process = Process.GetProcessesByName(UiTestEnvironment.AppProcessName)
                .FirstOrDefault(p => !existingIds.Contains(p.Id) && p.MainWindowHandle != IntPtr.Zero);
            if (process is not null)
            {
                return process;
            }

            Thread.Sleep(500);
        }

        throw new TimeoutException(
            $"テスト対象アプリ({UiTestEnvironment.AppId})のウィンドウが起動しませんでした。"
            + " パッケージ登録(dotnet run --project src/TabNest.App/TabNest.App.csproj -p:Platform=x64)を確認してください。");
    }

    public void Dispose()
    {
        try
        {
            // アタッチしたセッションの Quit はアプリを終了しないため、ウィンドウを閉じて終了させる
            Driver.Quit();
        }
        catch (WebDriverException)
        {
            // セッションが既に失われていてもアプリの後始末は続行する
        }

        if (!_appProcess.HasExited)
        {
            _appProcess.CloseMainWindow();
            if (!_appProcess.WaitForExit(10000))
            {
                _appProcess.Kill();
            }
        }

        _appProcess.Dispose();
    }
}
