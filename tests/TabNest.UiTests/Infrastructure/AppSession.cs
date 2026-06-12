using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace TabNest.UiTests.Infrastructure;

/// <summary>
/// テスト対象アプリ(TabNest)の起動とセッション管理。
/// WinAppDriver 経由でインストール済みパッケージ(AUMID)を起動し、
/// Dispose でアプリを終了する。
/// </summary>
public sealed class AppSession : IDisposable
{
    /// <summary>アプリ操作用の Appium ドライバ。</summary>
    public WindowsDriver<WindowsElement> Driver { get; }

    public AppSession()
    {
        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", UiTestEnvironment.AppId);
        options.AddAdditionalCapability("deviceName", "WindowsPC");
        Driver = new WindowsDriver<WindowsElement>(new Uri(UiTestEnvironment.WinAppDriverUrl), options);
        // 起動直後の要素探索を安定させるための暗黙待機
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
    }

    public void Dispose()
    {
        // Quit はセッションとともにアプリを終了する
        Driver.Quit();
    }
}
