using System.Collections.ObjectModel;
using TabNest.Core.Interfaces;

namespace TabNest.ViewModels;

/// <summary>
/// 1つのフォルダ表示(ファイル一覧)を担う ViewModel。
/// </summary>
public sealed class FolderViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IFileLauncher _fileLauncher;
    private string _currentPath = "";
    private string _addressBarText = "";
    private string? _errorMessage;

    public FolderViewModel(IFileSystemService fileSystemService, IFileLauncher fileLauncher)
    {
        _fileSystemService = fileSystemService;
        _fileLauncher = fileLauncher;
        LoadFolderCommand = new RelayCommand(p =>
        {
            if (p is string path)
            {
                LoadFolder(path);
            }
        });
        NavigateUpCommand = new RelayCommand(_ => NavigateUp(), _ => CanNavigateUp);
        NavigateToAddressCommand = new RelayCommand(_ => NavigateToAddress());
        OpenItemCommand = new RelayCommand(p =>
        {
            if (p is FileItemViewModel item)
            {
                OpenItem(item);
            }
        });
    }

    /// <summary>現在表示中のフォルダパス。読み込み成功時のみ更新される。</summary>
    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (SetProperty(ref _currentPath, value))
            {
                OnPropertyChanged(nameof(CanNavigateUp));
                NavigateUpCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>アドレスバーの入力文字列。移動成功時に CurrentPath と同期される。</summary>
    public string AddressBarText
    {
        get => _addressBarText;
        set => SetProperty(ref _addressBarText, value);
    }

    /// <summary>表示中フォルダの内容。フォルダ先頭・名前昇順で並ぶ。</summary>
    public ObservableCollection<FileItemViewModel> Items { get; } = [];

    /// <summary>直近の操作失敗のエラーメッセージ。成功時は null に戻る。</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>上の階層が存在するか(ドライブルートでは false)。</summary>
    public bool CanNavigateUp
        => !string.IsNullOrEmpty(CurrentPath) && Path.GetDirectoryName(CurrentPath) is not null;

    /// <summary>指定パスのフォルダを読み込む(パラメータ: string)。</summary>
    public RelayCommand LoadFolderCommand { get; }

    /// <summary>上の階層へ移動する。</summary>
    public RelayCommand NavigateUpCommand { get; }

    /// <summary>アドレスバーの入力パスへ移動する。</summary>
    public RelayCommand NavigateToAddressCommand { get; }

    /// <summary>一覧の項目を開く(フォルダ: 移動、ファイル: 既定アプリ。パラメータ: FileItemViewModel)。</summary>
    public RelayCommand OpenItemCommand { get; }

    /// <summary>
    /// 指定フォルダを読み込み、成功時に CurrentPath・AddressBarText・Items を更新する。
    /// 失敗時は状態を変更せず ErrorMessage のみ設定する。
    /// </summary>
    public bool LoadFolder(string path)
    {
        var result = _fileSystemService.ListFolder(path);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return false;
        }

        var sorted = result.Entries
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(e => new FileItemViewModel(e));

        Items.Clear();
        foreach (var item in sorted)
        {
            Items.Add(item);
        }

        CurrentPath = path;
        AddressBarText = path;
        ErrorMessage = null;
        return true;
    }

    /// <summary>上の階層へ移動する。ドライブルートでは何もしない。</summary>
    public bool NavigateUp()
    {
        var parent = Path.GetDirectoryName(CurrentPath);
        return parent is not null && LoadFolder(parent);
    }

    /// <summary>アドレスバーの入力パスへ移動する。失敗時は現在のフォルダに留まる。</summary>
    public bool NavigateToAddress()
    {
        var path = AddressBarText.Trim();
        if (path.Length == 0)
        {
            ErrorMessage = "パスが入力されていません。";
            return false;
        }

        return LoadFolder(path);
    }

    /// <summary>
    /// 一覧の項目を開く。フォルダはそのフォルダへ移動し、ファイルは既定アプリで開く。
    /// </summary>
    public bool OpenItem(FileItemViewModel item)
    {
        if (item.IsDirectory)
        {
            return LoadFolder(item.FullPath);
        }

        if (!_fileLauncher.OpenFile(item.FullPath))
        {
            ErrorMessage = $"ファイルを開けませんでした: {item.FullPath}";
            return false;
        }

        ErrorMessage = null;
        return true;
    }
}
