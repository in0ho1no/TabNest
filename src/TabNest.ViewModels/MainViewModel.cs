using TabNest.Core.Interfaces;

namespace TabNest.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel。
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private string _title = "TabNest";

    public MainViewModel(IFileSystemService fileSystemService, IFileLauncher fileLauncher)
    {
        Folder = new FolderViewModel(fileSystemService, fileLauncher);
    }

    /// <summary>ウィンドウタイトル。初期値は "TabNest"。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>表示中フォルダのファイル一覧 ViewModel。</summary>
    public FolderViewModel Folder { get; }

    /// <summary>初期表示フォルダ(%UserProfile%)を読み込む。</summary>
    public bool LoadInitialFolder()
        => Folder.LoadFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
