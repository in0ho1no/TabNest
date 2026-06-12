using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TabNest.ViewModels;
using Windows.System;

namespace TabNest.App;

/// <summary>
/// The main content page displayed inside the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainViewModel? ViewModel { get; private set; }

    public MainPage()
    {
        InitializeComponent();
    }

    /// <summary>x:Bind 用: 文字列が空でなければ true。</summary>
    public static bool HasText(string? value) => !string.IsNullOrEmpty(value);

    /// <summary>
    /// 左カラム(お気に入り+フォルダツリー)の現在幅。セッション保存用
    /// (スプリッターでの変更後の実際の表示幅を返す)。
    /// </summary>
    public double LeftPaneWidth => LeftPaneColumn.ActualWidth;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is MainViewModel viewModel)
        {
            ViewModel = viewModel;
            // 前回終了時の左カラム幅を復元する(既定 220・最小 150 は VM 側で補正済み)
            LeftPaneColumn.Width = new Microsoft.UI.Xaml.GridLength(viewModel.LeftPaneWidth);
            Bindings.Update();
        }
    }

    private void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel?.Folder.NavigateToAddressCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is Microsoft.UI.Xaml.FrameworkElement { DataContext: FileItemViewModel item })
        {
            ViewModel?.Folder.OpenItemCommand.Execute(item);
        }
    }

    private void FolderTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is FolderTreeNodeViewModel node)
        {
            ViewModel?.Tree.ActivateNode(node);
        }
    }
}
