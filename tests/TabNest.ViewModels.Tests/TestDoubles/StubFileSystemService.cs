using TabNest.Core.Interfaces;
using TabNest.Core.Models;

namespace TabNest.ViewModels.Tests.TestDoubles;

/// <summary>パスごとに固定の結果を返す IFileSystemService スタブ。</summary>
public sealed class StubFileSystemService : IFileSystemService
{
    private readonly Dictionary<string, FolderListingResult> _results = new();

    /// <summary>GetReadyDriveRoots が返すドライブルート(テストで必要なときに設定する)。</summary>
    public List<string> DriveRoots { get; } = [];

    public void Setup(string path, FolderListingResult result) => _results[path] = result;

    public FolderListingResult ListFolder(string path)
        => _results.TryGetValue(path, out var result)
            ? result
            : FolderListingResult.Failure($"フォルダが見つかりません: {path}");

    public IReadOnlyList<string> GetReadyDriveRoots() => DriveRoots;
}
