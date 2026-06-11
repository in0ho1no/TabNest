using System.Collections.ObjectModel;
using TabNest.Core.Interfaces;

namespace TabNest.ViewModels;

/// <summary>
/// 1つのフォルダ表示(ファイル一覧)を担う ViewModel。
/// </summary>
public sealed class FolderViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private string _currentPath = "";
    private string? _errorMessage;

    public FolderViewModel(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        LoadFolderCommand = new RelayCommand(p =>
        {
            if (p is string path)
            {
                LoadFolder(path);
            }
        });
    }

    /// <summary>現在表示中のフォルダパス。読み込み成功時のみ更新される。</summary>
    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    /// <summary>表示中フォルダの内容。フォルダ先頭・名前昇順で並ぶ。</summary>
    public ObservableCollection<FileItemViewModel> Items { get; } = [];

    /// <summary>直近の読み込み失敗のエラーメッセージ。成功時は null に戻る。</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>指定パスのフォルダを読み込む(パラメータ: string)。</summary>
    public RelayCommand LoadFolderCommand { get; }

    /// <summary>
    /// 指定フォルダを読み込み、成功時に CurrentPath と Items を更新する。
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
        ErrorMessage = null;
        return true;
    }
}
