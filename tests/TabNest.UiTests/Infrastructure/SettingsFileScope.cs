namespace TabNest.UiTests.Infrastructure;

/// <summary>
/// テスト対象アプリの settings.json を一時的に差し替えるスコープ。
/// 生成時に既存ファイルを退避し、Dispose で元の状態(内容またはファイル無し)へ復元する。
/// 起動状態を固定するテスト(初期起動状態・ウィンドウサイズ復元)で使う。
/// 書き込み先はアプリ自身の設定フォルダのみ(SPEC のテスト計画「テスト用settings.jsonを配置する」に基づく)。
/// </summary>
public sealed class SettingsFileScope : IDisposable
{
    private readonly string _path;
    private readonly string? _originalContent;

    public SettingsFileScope()
    {
        _path = UiTestEnvironment.SettingsFilePath;
        _originalContent = File.Exists(_path) ? File.ReadAllText(_path) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    /// <summary>settings.json を削除する(初期起動状態で起動させる)。</summary>
    public void DeleteSettings()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    /// <summary>テスト用の settings.json を配置する。</summary>
    public void WriteSettings(string json)
        => File.WriteAllText(_path, json);

    public void Dispose()
    {
        if (_originalContent is null)
        {
            DeleteSettings();
        }
        else
        {
            File.WriteAllText(_path, _originalContent);
        }
    }
}
