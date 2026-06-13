using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TabNest.ViewModels;
using Windows.System;

namespace TabNest.App.Controls;

/// <summary>
/// タブグループ1段分の表示(グループ名+タブ横並び+水平スクロール)。
/// グループ名はダブルクリックでインライン編集できる。
/// </summary>
public sealed partial class TabGroupRow : UserControl
{
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
}
