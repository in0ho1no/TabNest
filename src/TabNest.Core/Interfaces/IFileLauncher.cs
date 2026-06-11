namespace TabNest.Core.Interfaces;

/// <summary>
/// ファイルを OS の既定アプリで開く操作を抽象化するサービス。
/// </summary>
public interface IFileLauncher
{
    /// <summary>
    /// 指定ファイルを既定アプリで開く。失敗時は false を返し、例外を送出しない。
    /// </summary>
    bool OpenFile(string path);
}
