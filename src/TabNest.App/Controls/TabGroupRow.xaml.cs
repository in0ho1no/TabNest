using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

    /// <summary>x:Bind 用: true なら Visible。</summary>
    public static Visibility VisibleWhen(bool value)
        => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>x:Bind 用: false なら Visible。</summary>
    public static Visibility VisibleWhenNot(bool value)
        => value ? Visibility.Collapsed : Visibility.Visible;

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
}
