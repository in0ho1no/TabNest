using System.Collections.ObjectModel;
using TabNest.Core.Interfaces;

namespace TabNest.ViewModels;

/// <summary>
/// フォルダツリーの1ノード(ドライブまたはフォルダ)。
/// 子ノードは展開時に遅延読み込みする(ダミー子ノード方式)。
/// </summary>
public sealed class FolderTreeNodeViewModel : ViewModelBase
{
    private readonly IFileSystemService? _fileSystemService;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _childrenLoaded;

    /// <summary>ダミー子ノード(展開矢印を表示させるためのプレースホルダー)。</summary>
    private FolderTreeNodeViewModel()
    {
        Name = "";
        FullPath = "";
        IsPlaceholder = true;
    }

    public FolderTreeNodeViewModel(IFileSystemService fileSystemService, string fullPath, string name)
    {
        _fileSystemService = fileSystemService;
        FullPath = fullPath;
        Name = name;
        // 遅延読み込み: ダミー子を1つ入れておき、展開時に実子へ差し替える
        Children.Add(new FolderTreeNodeViewModel());
    }

    public string Name { get; }

    public string FullPath { get; }

    /// <summary>遅延読み込み用のダミーノードかどうか。</summary>
    public bool IsPlaceholder { get; }

    public ObservableCollection<FolderTreeNodeViewModel> Children { get; } = [];

    /// <summary>展開状態。展開時に子を遅延読み込みする。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
            {
                EnsureChildrenLoaded();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 子ノードを読み込む。アクセス不可・フォルダ削除済みの場合は子なしとして扱い、
    /// クラッシュ・ダイアログ表示はしない。読み込み済みでもフォルダ消滅は再確認する。
    /// </summary>
    public void EnsureChildrenLoaded()
    {
        if (_fileSystemService is null)
        {
            return;
        }

        var result = _fileSystemService.ListFolder(FullPath);
        if (!result.IsSuccess)
        {
            // アクセス拒否・削除済み → 子なしへ戻す
            Children.Clear();
            _childrenLoaded = true;
            return;
        }

        if (_childrenLoaded)
        {
            // 読み込み済みなら維持(ノードの同一性を保ち選択状態を壊さない)
            return;
        }

        Children.Clear();
        foreach (var entry in result.Entries
            .Where(e => e.IsDirectory)
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Children.Add(new FolderTreeNodeViewModel(_fileSystemService, entry.FullPath, entry.Name));
        }

        _childrenLoaded = true;
    }
}
