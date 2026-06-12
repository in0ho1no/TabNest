using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;

namespace TabNest.UiTests.Infrastructure;

/// <summary>
/// UI テスト共通の操作ヘルパー。座標クリックの空振りや JIS 配列での
/// SendKeys の文字化けといった既知の不安定要因への対策を含む。
/// </summary>
public static class UiActions
{
    /// <summary>条件が成立するまで待つ(既定 10 秒・200ms 間隔)。成立しなければ false。</summary>
    public static bool WaitUntil(Func<bool> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return condition();
    }

    /// <summary>
    /// ショートカットキーを送信する(例: Keys.Control + "t")。
    /// WinAppDriver の SendKeys では修飾キーがトグルのため、末尾で必ず解除する。
    /// </summary>
    public static void SendShortcut(AppSession session, string modifier, string key)
    {
        NativeMethods.SetForegroundWindow(session.MainWindowHandle);
        new OpenQA.Selenium.Interactions.Actions(session.Driver)
            .SendKeys(modifier + key + modifier)
            .Perform();
    }

    /// <summary>
    /// アドレスバーにパスを入力して Enter で移動し、移動完了まで待つ。
    /// JIS 配列では SendKeys の「\」が「]」として入力されるため「/」区切りに変換して送る。
    /// 入力結果を検証し、化けていた場合は最大3回リトライする。
    /// 戻り値は移動後にアドレスバーへ表示されるパス(「/」区切り)。
    /// </summary>
    public static string NavigateTo(AppSession session, string path)
    {
        var addressBar = session.Driver.FindElementByAccessibilityId("PathTextBox");
        var sendablePath = path.Replace('\\', '/');

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            addressBar.Click();
            addressBar.SendKeys(Keys.Control + "a" + Keys.Control);
            addressBar.SendKeys(sendablePath);
            if (addressBar.Text == sendablePath)
            {
                addressBar.SendKeys(Keys.Enter);
                // 移動完了はタブタイトル(移動先フォルダ名)の更新で判定する
                // (アドレスバーは入力値と移動後表示が同じため完了シグナルにならない)
                var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(sendablePath));
                Assert.True(
                    WaitUntil(() => FindTabs(session).Any(t => t.Text == folderName)),
                    $"{sendablePath} への移動が完了しませんでした(タブ: {string.Join(", ", FindTabs(session).Select(t => t.Text))})。");
                return sendablePath;
            }
        }

        throw new InvalidOperationException(
            $"アドレスバーへの入力に失敗しました(期待: {sendablePath}、実際: {addressBar.Text})。");
    }

    /// <summary>
    /// 要素の中心をホイール(中)クリックする。
    /// WinAppDriver は中クリック API を持たないため、Win32 で物理クリックを送る。
    /// 要素座標はウィンドウ可視矩形(見えない枠を除く)からの相対値のため、
    /// DWM の拡張フレーム境界を起点に絶対座標へ変換する(実測で確認済み)。
    /// </summary>
    public static void MiddleClick(AppSession session, IWebElement element)
    {
        NativeMethods.SetForegroundWindow(session.MainWindowHandle);
        Thread.Sleep(200);

        var visible = NativeMethods.GetVisibleWindowRect(session.MainWindowHandle);
        var centerX = visible.Left + element.Location.X + element.Size.Width / 2;
        var centerY = visible.Top + element.Location.Y + element.Size.Height / 2;
        NativeMethods.SetCursorPos(centerX, centerY);
        Thread.Sleep(100);
        NativeMethods.mouse_event(NativeMethods.MouseEventMiddleDown, 0, 0, 0, IntPtr.Zero);
        NativeMethods.mouse_event(NativeMethods.MouseEventMiddleUp, 0, 0, 0, IntPtr.Zero);
    }

    /// <summary>FolderTabItem(タブ)の一覧を取得する(表示順)。</summary>
    public static IReadOnlyList<WindowsElement> FindTabs(AppSession session)
        => session.Driver.FindElementsByAccessibilityId("FolderTabItem").ToList();

    /// <summary>タブ数が期待値になるまで待って検証する。</summary>
    public static void WaitForTabCount(AppSession session, int expected)
    {
        Assert.True(
            WaitUntil(() => FindTabs(session).Count == expected),
            $"タブ数が {expected} になりませんでした(実際: {FindTabs(session).Count})。");
    }
}
