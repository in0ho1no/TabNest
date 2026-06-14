using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TabNest.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace TabNest.App.Controls;

/// <summary>
/// タブグループ1段分の表示(グループ名+タブ横並び+水平スクロール)。
/// グループ名はダブルクリックでインライン編集できる。
/// </summary>
public sealed partial class TabGroupRow : UserControl
{
    /// <summary>
    /// D&D 中のタブ(Task 7-1/7-2)。同一グループ内の並べ替え・別グループへの移動の双方で参照する。
    /// グループ間移動ではドロップ先の行と開始元の行が別インスタンスになるため、
    /// 全行で共有できるよう静的に保持する(同時に進行する D&D は1つだけのため衝突しない)。
    /// </summary>
    private static FolderTabViewModel? s_draggingTab;

    /// <summary>D&D 開始元のグループ(同一グループ内の並べ替えかグループ間移動かの判定に使う。Task 7-2)。</summary>
    private static TabGroupViewModel? s_draggingSourceGroup;

    /// <summary>
    /// D&D 中のグループ段(Task 7-3)。グループ段の並べ替えはドロップ先の行と開始元の行が
    /// 別インスタンスになるため、全行で共有できるよう静的に保持する(s_draggingTab とは排他)。
    /// </summary>
    private static TabGroupViewModel? s_draggingGroup;

    /// <summary>グループ段 D&D で現在インジケータを表示している段(別の段へ移ると付け替えるため保持。Task 7-3)。</summary>
    private static TabGroupViewModel? s_groupDropTarget;

    public TabGroupViewModel? ViewModel { get; private set; }

    public TabGroupRow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// UserControl は既定で AutomationPeer を生成せず UIA ツリーに現れないため、
    /// AutomationId="TabGroupRow" を UI テスト(Inspect / WinAppDriver)から
    /// 検索できるように汎用ピアを生成する(SPEC Task 5-2)。
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
        => new FrameworkElementAutomationPeer(this);

    /// <summary>x:Bind 用: true なら Visible。</summary>
    public static Visibility VisibleWhen(bool value)
        => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>x:Bind 用: false なら Visible。</summary>
    public static Visibility VisibleWhenNot(bool value)
        => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>x:Bind 用: アクティブタブはアクセント色、それ以外はカード背景色。</summary>
    public static Brush TabBackground(bool isActive)
        => (Brush)Application.Current.Resources[
            isActive ? "AccentFillColorDefaultBrush" : "CardBackgroundFillColorDefaultBrush"];

    /// <summary>x:Bind 用: アクティブタブはアクセント上の文字色。</summary>
    public static Brush TabForeground(bool isActive)
        => (Brush)Application.Current.Resources[
            isActive ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush"];

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is TabGroupViewModel viewModel && !ReferenceEquals(ViewModel, viewModel))
        {
            ViewModel = viewModel;
            Bindings.Update();
        }
    }

    private void GroupNameText_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.BeginRename();
        // Visibility 反映後にフォーカスを移すため、レイアウト更新後に実行する
        DispatcherQueue.TryEnqueue(() =>
        {
            GroupNameEditBox.Focus(FocusState.Programmatic);
            GroupNameEditBox.SelectAll();
        });
        e.Handled = true;
    }

    private void GroupNameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel?.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel?.CancelRename();
            e.Handled = true;
        }
    }

    private void GroupNameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ViewModel?.CommitRename();
    }

    private void SaveToFavorites_Click(object sender, RoutedEventArgs e)
    {
        // 右クリックされたこのグループ(アクティブグループとは限らない)を保存する
        ViewModel?.SaveAsFavorite();
    }

    private async void RemoveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        // タブを1個以上持つグループの削除は確認ダイアログを表示する(SPEC Task 6-1)
        if (ViewModel.HasTabs)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "グループを削除",
                Content = $"グループ「{ViewModel.Name}」とタブ {ViewModel.Tabs.Count} 個を削除します。よろしいですか?",
                PrimaryButtonText = "削除",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Close,
            };
            AutomationProperties.SetAutomationId(dialog, "RemoveGroupConfirmDialog");

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        // 最後の1グループの削除拒否などは ViewModel 側で OperationError を設定する
        ViewModel.RemoveGroup();
    }

    private void DuplicateTab_Click(object sender, RoutedEventArgs e)
    {
        // ContextFlyout の MenuFlyoutItem は所属タブ(Border)の DataContext を引き継ぐため、
        // 右クリックされたタブを sender の DataContext から特定する
        if (sender is FrameworkElement { DataContext: FolderTabViewModel tab })
        {
            ViewModel?.DuplicateTab(tab);
        }
    }

    private void TabItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FolderTabViewModel tab })
        {
            ViewModel?.SelectTab(tab);
            e.Handled = true;
        }
    }

    private void TabItem_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // 中クリックは PointerPressed の IsMiddleButtonPressed で検出する
        // (Tapped / PointerReleased では取りこぼすため使わない — SPEC 実装ノート)
        if (sender is FrameworkElement { DataContext: FolderTabViewModel tab } element
            && e.GetCurrentPoint(element).Properties.IsMiddleButtonPressed)
        {
            ViewModel?.CloseTab(tab);
            e.Handled = true;
        }
    }

    /// <summary>
    /// タブのドラッグ開始(Task 7-1/7-2)。ドラッグ中のタブと開始元グループを保持し、移動操作として開始する。
    /// データにはタブ Id を載せる(外部 D&D 連携の足がかり用。グループ間移動の本処理は静的状態で行う)。
    /// </summary>
    private void TabItem_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: FolderTabViewModel tab })
        {
            s_draggingTab = tab;
            s_draggingSourceGroup = ViewModel;
            s_draggingGroup = null; // タブの D&D とグループ段の D&D は排他
            args.Data.SetText(tab.Id);
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    /// <summary>
    /// タブ上のドラッグ中(Task 7-1/7-2)。同一グループ内の並べ替え・別グループからの移動の双方を受け付け、
    /// ポインタ位置(タブの左右どちら寄りか)に応じて挿入位置インジケータを表示する。
    /// 別グループからの移動で移動先が上限(20)のときは受け付けない。
    /// </summary>
    private void TabItem_DragOver(object sender, DragEventArgs e)
    {
        if (s_draggingTab is null || ViewModel is null
            || sender is not FrameworkElement { DataContext: FolderTabViewModel target } element)
        {
            return;
        }

        e.Handled = true;
        e.DragUIOverride.IsGlyphVisible = false;
        e.DragUIOverride.IsCaptionVisible = false;

        // 別グループからの移動で移動先が上限に達している場合は拒否する
        if (!ReferenceEquals(s_draggingSourceGroup, ViewModel) && ViewModel.IsTabLimitReached)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            ViewModel.ClearDropIndicators();
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;

        // 自分自身の上ではインジケータを出さない(移動が起きないため)
        if (ReferenceEquals(target, s_draggingTab))
        {
            ViewModel.ClearDropIndicators();
            return;
        }

        var after = e.GetPosition(element).X > element.ActualWidth / 2;
        ViewModel.SetDropIndicator(target, after);
    }

    /// <summary>タブからドラッグが外れたとき、そのタブのインジケータを消す(Task 7-1)。</summary>
    private void TabItem_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FolderTabViewModel target }
            && (target.IsDropBefore || target.IsDropAfter))
        {
            target.IsDropBefore = false;
            target.IsDropAfter = false;
        }
    }

    /// <summary>
    /// タブへのドロップ(Task 7-1/7-2)。ドロップ先タブの左右どちら寄りかで挿入位置を決め、
    /// 同一グループ内なら並べ替え、別グループからなら移動として受け入れる。
    /// </summary>
    private void TabItem_Drop(object sender, DragEventArgs e)
    {
        if (s_draggingTab is null || ViewModel is null
            || sender is not FrameworkElement { DataContext: FolderTabViewModel target } element)
        {
            return;
        }

        e.Handled = true;
        var targetIndex = ViewModel.Tabs.IndexOf(target);
        if (targetIndex >= 0)
        {
            var after = e.GetPosition(element).X > element.ActualWidth / 2;
            var insertIndex = after ? targetIndex + 1 : targetIndex;
            DropTab(s_draggingTab, insertIndex);
        }

        ViewModel.ClearDropIndicators();
    }

    /// <summary>
    /// グループのタブ領域(空白部・空グループを含む)へのドラッグ中(Task 7-2)。
    /// 特定のタブ上に乗っていない場合のフォールバックで、末尾への挿入位置を示す。
    /// </summary>
    private void Group_DragOver(object sender, DragEventArgs e)
    {
        if (s_draggingTab is null || ViewModel is null)
        {
            return;
        }

        e.Handled = true;
        e.DragUIOverride.IsGlyphVisible = false;
        e.DragUIOverride.IsCaptionVisible = false;

        if (!ReferenceEquals(s_draggingSourceGroup, ViewModel) && ViewModel.IsTabLimitReached)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            ViewModel.ClearDropIndicators();
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;

        // 末尾(最後のタブの右)へ挿入することを示す。空グループはインジケータ対象が無く何も出さない。
        // 同一グループで末尾タブ自身が末尾へ移る場合は変化が無いためインジケータを出さない。
        var last = ViewModel.Tabs.LastOrDefault();
        if (last is null || ReferenceEquals(last, s_draggingTab))
        {
            ViewModel.ClearDropIndicators();
            return;
        }

        ViewModel.SetDropIndicator(last, after: true);
    }

    /// <summary>
    /// グループのタブ領域(空白部・空グループを含む)へのドロップ(Task 7-2)。
    /// 特定のタブ上ではない場合のフォールバックで、末尾へ移動・並べ替えする。
    /// </summary>
    private void Group_Drop(object sender, DragEventArgs e)
    {
        if (s_draggingTab is null || ViewModel is null)
        {
            return;
        }

        DropTab(s_draggingTab, ViewModel.Tabs.Count);
        ViewModel.ClearDropIndicators();
    }

    /// <summary>
    /// ドラッグ中のタブを、現在の行のグループの <paramref name="insertIndex"/> へドロップする。
    /// 開始元グループと同じなら並べ替え(Task 7-1)、別グループなら移動(Task 7-2)として処理する。
    /// </summary>
    private void DropTab(FolderTabViewModel tab, int insertIndex)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ReferenceEquals(s_draggingSourceGroup, ViewModel))
        {
            ViewModel.MoveTab(tab, insertIndex);
        }
        else
        {
            ViewModel.MoveTabFromOtherGroup(tab, insertIndex);
        }
    }

    /// <summary>ドラッグ終了(ドロップの成否を問わず)。両グループのインジケータと状態を片付ける(Task 7-1/7-2)。</summary>
    private void TabItem_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        ViewModel?.ClearDropIndicators();
        s_draggingSourceGroup?.ClearDropIndicators();
        s_draggingTab = null;
        s_draggingSourceGroup = null;
    }

    /// <summary>
    /// グループ段のドラッグ開始(Task 7-3)。グループ名部分をドラッグハンドルとし、
    /// ドラッグ中のグループ段を保持して段の並べ替え操作として開始する(タブの D&D とは排他)。
    /// </summary>
    private void GroupName_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        s_draggingGroup = ViewModel;
        s_draggingTab = null;
        s_draggingSourceGroup = null;
        args.Data.SetText(ViewModel.Id);
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    /// <summary>
    /// グループ段上のドラッグ中(Task 7-3)。ポインタが段の上半分か下半分かに応じて、
    /// この段の上端/下端に挿入位置インジケータを表示する。自分自身の上では何も出さない。
    /// </summary>
    private void Row_DragOver(object sender, DragEventArgs e)
    {
        if (s_draggingGroup is null || ViewModel is null || sender is not FrameworkElement element)
        {
            return;
        }

        e.Handled = true;
        e.DragUIOverride.IsGlyphVisible = false;
        e.DragUIOverride.IsCaptionVisible = false;
        e.AcceptedOperation = DataPackageOperation.Move;

        // 自分自身の上ではインジケータを出さない(移動が起きないため)
        if (ReferenceEquals(ViewModel, s_draggingGroup))
        {
            ClearGroupDropIndicator();
            return;
        }

        var below = e.GetPosition(element).Y > element.ActualHeight / 2;
        SetGroupDropIndicator(ViewModel, below);
    }

    /// <summary>段からドラッグが外れたとき、その段のインジケータを消す(Task 7-3)。</summary>
    private void Row_DragLeave(object sender, DragEventArgs e)
    {
        if (s_draggingGroup is not null && ReferenceEquals(ViewModel, s_groupDropTarget))
        {
            ClearGroupDropIndicator();
        }
    }

    /// <summary>
    /// グループ段へのドロップ(Task 7-3)。ドロップ先の段の上半分なら直前、下半分なら直後へ
    /// ドラッグ中の段を移動する。自分自身の上では何もしない。
    /// </summary>
    private void Row_Drop(object sender, DragEventArgs e)
    {
        if (s_draggingGroup is null || ViewModel is null || sender is not FrameworkElement element)
        {
            return;
        }

        e.Handled = true;
        if (!ReferenceEquals(ViewModel, s_draggingGroup))
        {
            var below = e.GetPosition(element).Y > element.ActualHeight / 2;
            ViewModel.MoveGroupHere(s_draggingGroup, below);
        }

        ClearGroupDropIndicator();
    }

    /// <summary>グループ段のドラッグ終了(成否を問わず)。インジケータと状態を片付ける(Task 7-3)。</summary>
    private void GroupName_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        ClearGroupDropIndicator();
        s_draggingGroup = null;
    }

    /// <summary>
    /// グループ段の挿入位置インジケータを <paramref name="target"/> 段に設定する(Task 7-3)。
    /// 別の段に表示中だった場合はそちらを消してから付け替える(同時に1か所だけ表示する)。
    /// </summary>
    private static void SetGroupDropIndicator(TabGroupViewModel target, bool below)
    {
        if (!ReferenceEquals(s_groupDropTarget, target))
        {
            s_groupDropTarget?.ClearGroupDropIndicator();
        }

        s_groupDropTarget = target;
        target.SetGroupDropIndicator(below);
    }

    /// <summary>グループ段の挿入位置インジケータを消す(Task 7-3)。</summary>
    private static void ClearGroupDropIndicator()
    {
        s_groupDropTarget?.ClearGroupDropIndicator();
        s_groupDropTarget = null;
    }
}
