using TabNest.Core.Interfaces;
using TabNest.Core.Models;

namespace TabNest.Core.Services;

/// <summary>
/// System.IO を用いた <see cref="IFileSystemService"/> の実装。
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    public FolderListingResult ListFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return FolderListingResult.Failure("パスが指定されていません。");
        }

        try
        {
            var directory = new DirectoryInfo(path);
            if (!directory.Exists)
            {
                return FolderListingResult.Failure($"フォルダが見つかりません: {path}");
            }

            var entries = new List<FileSystemEntry>();
            foreach (var info in directory.EnumerateFileSystemInfos())
            {
                var isDirectory = (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                entries.Add(new FileSystemEntry
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = isDirectory,
                    LastModifiedAt = info.LastWriteTime,
                    SizeInBytes = isDirectory ? null : ((FileInfo)info).Length,
                });
            }

            return FolderListingResult.Success(entries);
        }
        catch (UnauthorizedAccessException)
        {
            return FolderListingResult.Failure($"フォルダへのアクセスが拒否されました: {path}");
        }
        catch (IOException ex)
        {
            return FolderListingResult.Failure($"フォルダの読み取りに失敗しました: {ex.Message}");
        }
    }
}
