using System.Collections.ObjectModel;
using TabNest.Core.Interfaces;

namespace TabNest.ViewModels;

/// <summary>
/// 左カラムのフォルダツリー。ドライブをルートとし、フォルダのみを表示する。
/// 「ツリー選択 → タブ移動」と「タブ移動 → ツリー選択」の双方向同期を持ち、
/// 追従中はイベントを抑制して無限ループを防ぐ。
/// </summary>
public sealed class FolderTreeViewModel : ViewModelBase
{
    private readonly Action<string> _navigateToPath;
    private FolderTreeNodeViewModel? _selectedNode;
    private bool _isSyncing;

    public FolderTreeViewModel(IFileSystemService fileSystemService, Action<string> navigateToPath)
    {
        _navigateToPath = navigateToPath;
        foreach (var root in fileSystemService.GetReadyDriveRoots())
        {
            Roots.Add(new FolderTreeNodeViewModel(fileSystemService, root, root));
        }
    }

    /// <summary>ルートノード(利用可能なローカルドライブ)。</summary>
    public ObservableCollection<FolderTreeNodeViewModel> Roots { get; } = [];

    /// <summary>
    /// ノードのクリック(選択)でアクティブタブをそのフォルダへ移動する。
    /// ツリー追従中(タブ移動 → ツリー選択の同期中)は何もしない。
    /// </summary>
    public void ActivateNode(FolderTreeNodeViewModel node)
    {
        if (_isSyncing || node.IsPlaceholder)
        {
            return;
        }

        _navigateToPath(node.FullPath);
    }

    /// <summary>
    /// アクティブタブのパスにツリー選択を追従させる。
    /// 対応ノードまでのパス上のノードのみ展開し、対象ノードを選択状態にする。
    /// ドライブが見つからない等で追従できない場合は選択を解除するだけとし、エラーにしない。
    /// </summary>
    public void RevealPath(string path)
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            var root = Roots.FirstOrDefault(
                r => path.StartsWith(r.FullPath, StringComparison.OrdinalIgnoreCase));
            if (root is null)
            {
                ClearSelection();
                return;
            }

            var node = root;
            var relative = path[root.FullPath.Length..].Trim('\\');
            if (relative.Length > 0)
            {
                foreach (var segment in relative.Split('\\'))
                {
                    node.EnsureChildrenLoaded();
                    node.IsExpanded = true; // パス上のノードのみ展開する
                    var child = node.Children.FirstOrDefault(
                        c => string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));
                    if (child is null)
                    {
                        ClearSelection();
                        return;
                    }

                    node = child;
                }
            }

            SelectExclusive(node);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void SelectExclusive(FolderTreeNodeViewModel node)
    {
        if (ReferenceEquals(_selectedNode, node))
        {
            node.IsSelected = true;
            return;
        }

        ClearSelection();
        node.IsSelected = true;
        _selectedNode = node;
    }

    private void ClearSelection()
    {
        if (_selectedNode is not null)
        {
            _selectedNode.IsSelected = false;
            _selectedNode = null;
        }
    }
}
