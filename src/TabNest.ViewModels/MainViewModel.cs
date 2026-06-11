namespace TabNest.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private string _title = "TabNest";

    /// <summary>ウィンドウタイトル。初期値は "TabNest"。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}
