using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TabNest.ViewModels;

/// <summary>
/// INotifyPropertyChanged の共通実装を提供する ViewModel 基底クラス。
/// WinUI 非依存を維持するため、外部 MVVM ライブラリは使用しない。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
