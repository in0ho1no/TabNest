namespace TabNest.ViewModels;

/// <summary>
/// 戻る・進む履歴(BackStack / ForwardStack)。
/// FolderViewModel から独立させ、Task 3-7 でタブごとに所有者を移せるようにする。
/// 読み込み失敗時に履歴を巻き戻さなくて済むよう、Peek(参照)と Commit(確定)を分離している。
/// </summary>
public sealed class NavigationHistory
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    public bool CanGoBack => _backStack.Count > 0;

    public bool CanGoForward => _forwardStack.Count > 0;

    /// <summary>戻る先のパスを返す(履歴は変更しない)。無ければ null。</summary>
    public string? PeekBack() => _backStack.Count > 0 ? _backStack.Peek() : null;

    /// <summary>進む先のパスを返す(履歴は変更しない)。無ければ null。</summary>
    public string? PeekForward() => _forwardStack.Count > 0 ? _forwardStack.Peek() : null;

    /// <summary>
    /// 新規移動を記録する。移動元を BackStack に積み、ForwardStack をクリアする。
    /// </summary>
    public void RecordNavigation(string fromPath)
    {
        _backStack.Push(fromPath);
        _forwardStack.Clear();
    }

    /// <summary>
    /// 戻る移動を確定する。BackStack から1つ取り除き、移動元を ForwardStack に積む。
    /// 事前に <see cref="PeekBack"/> の読み込みが成功していること。
    /// </summary>
    public void CommitBack(string fromPath)
    {
        _backStack.Pop();
        _forwardStack.Push(fromPath);
    }

    /// <summary>
    /// 進む移動を確定する。ForwardStack から1つ取り除き、移動元を BackStack に積む。
    /// 事前に <see cref="PeekForward"/> の読み込みが成功していること。
    /// </summary>
    public void CommitForward(string fromPath)
    {
        _forwardStack.Pop();
        _backStack.Push(fromPath);
    }
}
