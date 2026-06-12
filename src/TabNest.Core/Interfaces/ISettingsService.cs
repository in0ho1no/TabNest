using TabNest.Core.Models;

namespace TabNest.Core.Interfaces;

/// <summary>
/// 設定(settings.json)の保存・読み込みを抽象化するサービス。
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 設定を読み込む。ファイルが存在しない・破損している場合は
    /// 既定値(初期起動状態用)の AppSettings を返し、例外を送出しない。
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// 設定を保存する。保存先フォルダが無ければ作成する。
    /// 失敗時は false を返し、例外を送出しない。
    /// </summary>
    bool Save(AppSettings settings);
}
