using TabNest.Core.Models;

namespace TabNest.ViewModels;

/// <summary>
/// 1つのタブを表す ViewModel。FolderTab(永続化用 DTO)をラップする。
/// </summary>
public sealed class FolderTabViewModel : ViewModelBase
{
    private readonly FolderTab _model;
    private string _title;
    private bool _isActive;

    public FolderTabViewModel(FolderTab model)
    {
        _model = model;
        _title = model.Title;
    }

    public string Id => _model.Id;

    /// <summary>このタブ専用の戻る・進む履歴(タブを切り替えても履歴は混ざらない)。</summary>
    public NavigationHistory History { get; } = new();

    /// <summary>現在表示中のフォルダパス。フォルダ移動のたびに更新される。</summary>
    public string Path => _model.Path;

    /// <summary>タブに表示するタイトル(現在表示中のフォルダ名)。</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                _model.Title = value;
            }
        }
    }

    /// <summary>アクティブタブかどうか(見た目の切り替えに使用)。</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>
    /// フォルダ移動に追従して、タブの Path とタイトルを移動先に更新する。
    /// </summary>
    public void UpdateLocation(string path, string title)
    {
        if (_model.Path != path)
        {
            _model.Path = path;
            OnPropertyChanged(nameof(Path));
        }

        Title = title;
    }
}
