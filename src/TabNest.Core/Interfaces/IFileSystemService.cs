using TabNest.Core.Models;

namespace TabNest.Core.Interfaces;

/// <summary>
/// ファイルシステムへのアクセスを抽象化するサービス。
/// ViewModel から直接 System.IO を呼ばず、本インターフェース経由で利用する。
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// 指定フォルダ直下のファイル・フォルダ一覧を取得する。
    /// 存在しないパス・アクセス不可の場合は失敗結果を返し、例外を送出しない。
    /// </summary>
    FolderListingResult ListFolder(string path);

    /// <summary>
    /// 利用可能なローカルドライブのルートパス(例: C:\)を取得する。
    /// IsReady でないドライブは含めない。失敗時は空リストを返す。
    /// </summary>
    IReadOnlyList<string> GetReadyDriveRoots();
}
