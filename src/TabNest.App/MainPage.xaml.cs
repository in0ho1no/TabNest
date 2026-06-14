using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
        // グループ以外の領域をクリックしたらタブグループの選択を解除する(Task 8-2)。
        // 子要素が PointerPressed を処理済みでも捕捉できるよう handledEventsToo: true で登録する。
        AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPagePointerPressed), handledEventsToo: true);
    }

    /// <summary>x:Bind 用: 文字列が空でなければ true。</summary>
    public static bool HasText(string? value) => !string.IsNullOrEmpty(value);

    /// <summary>x:Bind 用: true なら Visible。</summary>
    public static Visibility VisibleWhen(bool value)
        => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>x:Bind 用: false なら Visible。</summary>
    public static Visibility VisibleWhenNot(bool value)
        => value ? Visibility.Collapsed : Visibility.Visible;

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

    /// <summary>
    /// タブグループ領域の外をクリックしたらグループ選択を解除する(Task 8-2)。
    /// タブグループ領域(グループ名・タブ)内のクリックは選択操作のため解除しない。
    /// </summary>
    private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.SelectedGroup is null || IsWithinTabGroups(e.OriginalSource as DependencyObject))
        {
            return;
        }

        ViewModel.ClearGroupSelection();
    }

    /// <summary>指定要素がタブグループ一覧(TabGroupsList)の配下にあるかを視覚ツリーで判定する。</summary>
    private bool IsWithinTabGroups(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, TabGroupsList))
            {
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// TreeView 本体は AutomationPeer を持たず UIA ツリーに現れないため、
    /// 内部の TreeViewList(UIA 上は Tree として露出する実体)に
    /// AutomationId="FolderTreeView" を引き継ぐ(SPEC Task 5-2)。
    /// </summary>
    private void FolderTreeView_Loaded(object sender, RoutedEventArgs e)
    {
        var innerList = FindDescendant<TreeViewList>(FolderTreeView);
        if (innerList is null)
        {
            // フォルダツリー非表示(Collapsed。Task 6-5 のトグル)では内部の TreeViewList が
            // 未実体化のため見つからないのは正常。表示中なのに見つからない場合のみ、
            // 内部構造変化(ElementDiscoveryTests でも検出)を開発中に気付けるようアサートする。
            System.Diagnostics.Debug.Assert(
                FolderTreeView.Visibility == Visibility.Collapsed,
                "TreeViewList が見つかりません(FolderTreeView の AutomationId を設定できません)");
            return;
        }

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(innerList, "FolderTreeView");
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>お気に入りクリック: そのタブグループを新しい段として開く(5段上限は InfoBar 表示)。</summary>
    private void FavoritesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FavoriteItemViewModel favorite)
        {
            ViewModel?.OpenFavorite(favorite.Id);
        }
    }

    /// <summary>お気に入りの右クリックメニュー「削除」。</summary>
    private void DeleteFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteItemViewModel favorite })
        {
            ViewModel?.RemoveFavorite(favorite.Id);
        }
    }

    /// <summary>お気に入りの右クリックメニュー「名前の変更」。インライン編集を開始する(Task 6-4)。</summary>
    private void RenameFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FavoriteItemViewModel favorite })
        {
            return;
        }

        favorite.BeginRename();
        // Visibility 反映後にフォーカスを移すため、レイアウト更新後に編集 TextBox を探してフォーカスする
        DispatcherQueue.TryEnqueue(() =>
        {
            if (FindFavoriteEditBox(favorite) is { } editBox)
            {
                editBox.Focus(FocusState.Programmatic);
                editBox.SelectAll();
            }
        });
    }

    /// <summary>お気に入りリネーム編集中のキー操作(Enter 確定 / Esc 取消)。</summary>
    private void FavoriteNameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FavoriteItemViewModel favorite })
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            favorite.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            favorite.CancelRename();
            e.Handled = true;
        }
    }

    /// <summary>お気に入りリネーム編集 TextBox のフォーカス喪失で確定する。</summary>
    private void FavoriteNameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FavoriteItemViewModel favorite })
        {
            favorite.CommitRename();
        }
    }

    /// <summary>行内 D&amp;D 完了時に、表示順を ViewModel(と FavoritesService)へ反映する(Task 6-4)。</summary>
    private void FavoritesListView_DragItemsCompleted(
        ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        // ListView は ItemsSource(Favorites)を直接並べ替え済み。その順序をサービス側へ同期する
        var orderedIds = ViewModel.Favorites.Select(f => f.Id).ToList();
        ViewModel.ReorderFavorites(orderedIds);
    }

    /// <summary>指定したお気に入り項目のコンテナ内にあるリネーム編集 TextBox を探す。</summary>
    private TextBox? FindFavoriteEditBox(FavoriteItemViewModel favorite)
    {
        if (FavoritesListView.ContainerFromItem(favorite) is not DependencyObject container)
        {
            return null;
        }

        return FindDescendantByName(container, "FavoriteNameEditBox") as TextBox;
    }

    /// <summary>名前付き子孫要素を深さ優先で探す(ItemTemplate 内の要素にアクセスするため)。</summary>
    private static FrameworkElement? FindDescendantByName(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: var childName } element && childName == name)
            {
                return element;
            }

            if (FindDescendantByName(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // ---- ファイル一覧: 列ソートと列幅自動調整(Task 4-5) ----

    /// <summary>
    /// 各列の現在幅(px)。ヘッダー行と ItemTemplate の Grid を同じ幅に保つための共有状態。
    /// 初期値は MainPage.xaml のヘッダー(FileListHeader)・行テンプレートの
    /// ColumnDefinitions と一致させること(3箇所同期必須)。
    /// </summary>
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
