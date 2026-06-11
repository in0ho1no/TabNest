using System.Text.Json;
using TabNest.Core.Interfaces;
using TabNest.Core.Models;

namespace TabNest.Core.Services;

/// <summary>
/// %AppData%\TabNest\settings.json への設定保存・読み込み(SPEC「設定保存」準拠)。
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;

    /// <param name="settingsFilePath">
    /// 保存先のフルパス。省略時は %AppData%\TabNest\settings.json。
    /// (結合テストでは一時フォルダのパスを指定する)
    /// </param>
    public SettingsService(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TabNest",
            "settings.json");
    }

    /// <summary>設定ファイルのフルパス。</summary>
    public string SettingsFilePath => _settingsFilePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 破損・読み取り不可は初期起動状態へのフォールバック
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(_settingsFilePath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
