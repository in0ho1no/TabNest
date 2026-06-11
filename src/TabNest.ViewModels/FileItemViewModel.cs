using TabNest.Core.Models;

namespace TabNest.ViewModels;

/// <summary>
/// ファイル一覧の1行を表す ViewModel。
/// </summary>
public sealed class FileItemViewModel
{
    public FileItemViewModel(FileSystemEntry entry)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        IsDirectory = entry.IsDirectory;
        LastModifiedAt = entry.LastModifiedAt;
        SizeInBytes = entry.SizeInBytes;
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public DateTime LastModifiedAt { get; }

    /// <summary>サイズ(バイト)。フォルダは null(サイズ列は空欄表示)。</summary>
    public long? SizeInBytes { get; }
}
