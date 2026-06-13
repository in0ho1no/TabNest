namespace TabNest.Core.Models;

/// <summary>
/// フォルダ一覧取得の結果。失敗時はエラー情報を保持し、例外を漏らさない。
/// </summary>
public sealed class FolderListingResult
{
    private FolderListingResult(bool isSuccess, string? errorMessage, IReadOnlyList<FileSystemEntry> entries)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Entries = entries;
    }

    public bool IsSuccess { get; }

    /// <summary>失敗時のエラーメッセージ。成功時は null。</summary>
    public string? ErrorMessage { get; }

    /// <summary>取得したエントリ一覧。失敗時は空。</summary>
    public IReadOnlyList<FileSystemEntry> Entries { get; }

    public static FolderListingResult Success(IReadOnlyList<FileSystemEntry> entries)
        => new(true, null, entries);

    public static FolderListingResult Failure(string errorMessage)
        => new(false, errorMessage, []);
}
