using Microsoft.UI.Xaml;
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

    // ---- ファイル一覧: 列ソートと列幅自動調整(Task 4-5) ----

    /// <summary>各列の現在幅(px)。ヘッダー行と ItemTemplate の Grid を同じ幅に保つための共有状態。</summary>
    private readonly double[] _columnWidths = [28, 292, 140, 150, 90];

    private void NameColumnHeader_Click(object sender, RoutedEventArgs e)
        => ViewModel?.Folder.ToggleSort(FileSortColumn.Name);

    private void TypeColumnHeader_Click(object sender, RoutedEventArgs e)
        => ViewModel?.Folder.ToggleSort(FileSortColumn.Type);

    private void LastModifiedColumnHeader_Click(object sender, RoutedEventArgs e)
        => ViewModel?.Folder.ToggleSort(FileSortColumn.LastModified);

    private void SizeColumnHeader_Click(object sender, RoutedEventArgs e)
        => ViewModel?.Folder.ToggleSort(FileSortColumn.Size);

    private void NameColumnSeparator_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => AutoFitColumn(1, item => item.Name);

    private void TypeColumnSeparator_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => AutoFitColumn(2, item => item.TypeText);

    private void LastModifiedColumnSeparator_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => AutoFitColumn(3, item => item.LastModifiedText);

    private void SizeColumnSeparator_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        => AutoFitColumn(4, item => item.SizeText);

    /// <summary>
    /// 仮想化で実体化された行に現在の列幅を適用する(自動調整後にスクロールで現れる行にも反映する)。
    /// </summary>
    private void FileListView_ContainerContentChanging(
        ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer?.ContentTemplateRoot is Grid rowGrid)
        {
            ApplyColumnWidths(rowGrid);
        }
    }

    /// <summary>
    /// 列幅の自動調整(SPEC「ファイル一覧」)。ヘッダーと表示中の全行の表示文字列を測定し、
    /// 最小 40px・最大「ウィンドウ幅 − 他列の合計幅 − 20px」に収めた幅を適用する。
    /// </summary>
    private void AutoFitColumn(int columnIndex, Func<FileItemViewModel, string> textForItem)
    {
        if (ViewModel is null)
        {
            return;
        }

        var desired = MeasureColumnContentWidth(columnIndex, textForItem);
        var otherColumnsTotal = _columnWidths.Where((_, i) => i != columnIndex).Sum();
        var width = FolderViewModel.ClampAutoColumnWidth(desired, ActualWidth, otherColumnsTotal);

        _columnWidths[columnIndex] = width;
        FileListHeader.ColumnDefinitions[columnIndex].Width = new GridLength(width);
        foreach (var item in FileListView.Items)
        {
            if (FileListView.ContainerFromItem(item) is ListViewItem container
                && container.ContentTemplateRoot is Grid rowGrid)
            {
                ApplyColumnWidths(rowGrid);
            }
        }
    }

    /// <summary>ヘッダー文字列と一覧の表示文字列のうち最長の表示幅(+余白)を求める。</summary>
    private double MeasureColumnContentWidth(int columnIndex, Func<FileItemViewModel, string> textForItem)
    {
        // 行(本文フォント)と同じ条件で測定する。ヘッダーは Caption スタイルで本文より小さいため
        // 本文フォントで測っておけばヘッダーが収まらないことはない
        var headerText = columnIndex switch
        {
            1 => "名前",
            2 => "種別",
            3 => "更新日時",
            _ => "サイズ",
        };
        var texts = new List<string> { headerText };
        if (ViewModel is not null)
        {
            texts.AddRange(ViewModel.Folder.Items.Select(textForItem));
        }

        double max = 0;
        var measureBlock = new TextBlock();
        foreach (var text in texts)
        {
            measureBlock.Text = text;
            measureBlock.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            max = Math.Max(max, measureBlock.DesiredSize.Width);
        }

        // 区切り(8px)と文字の見切れ防止の余白
        return max + 12;
    }

    private void ApplyColumnWidths(Grid rowGrid)
    {
        for (var i = 0; i < _columnWidths.Length && i < rowGrid.ColumnDefinitions.Count; i++)
        {
            rowGrid.ColumnDefinitions[i].Width = new GridLength(_columnWidths[i]);
        }
    }
}
