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

    /// <summary>
    /// アイコンのグリフ(Segoe Fluent Icons)。
    /// フォルダ: U+E8B7(Folder)、ファイル: U+E7C3(Page)。
    /// </summary>
    public string IconGlyph => IsDirectory ? "" : "";

    /// <summary>種別の表示文字列。フォルダは「フォルダ」、ファイルは拡張子ベース。</summary>
    public string TypeText
    {
        get
        {
            if (IsDirectory)
            {
                return "フォルダ";
            }

            var extension = Path.GetExtension(Name).TrimStart('.');
            return extension.Length == 0 ? "ファイル" : $"{extension.ToUpperInvariant()} ファイル";
        }
    }

    /// <summary>更新日時の表示文字列(yyyy/MM/dd HH:mm)。</summary>
    public string LastModifiedText => LastModifiedAt.ToString("yyyy/MM/dd HH:mm");

    /// <summary>サイズの表示文字列。ファイルは KB 単位(切り上げ)、フォルダは空欄。</summary>
    public string SizeText
    {
        get
        {
            if (SizeInBytes is not long size)
            {
                return "";
            }

            var kiloBytes = (size + 1023) / 1024;
            return $"{kiloBytes:N0} KB";
        }
    }
}
