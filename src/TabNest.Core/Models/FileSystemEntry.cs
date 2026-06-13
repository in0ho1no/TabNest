namespace TabNest.Core.Models;

/// <summary>
/// フォルダ一覧の1エントリ(ファイルまたはフォルダ)。
/// </summary>
public sealed class FileSystemEntry
{
    /// <summary>表示名(ファイル名・フォルダ名)。</summary>
    public required string Name { get; init; }

    /// <summary>絶対パス。</summary>
    public required string FullPath { get; init; }

    /// <summary>フォルダなら true。</summary>
    public required bool IsDirectory { get; init; }

    /// <summary>最終更新日時(ローカル時刻)。</summary>
    public DateTime LastModifiedAt { get; init; }

    /// <summary>サイズ(バイト)。フォルダはサイズ計算しないため null。</summary>
    public long? SizeInBytes { get; init; }
}
