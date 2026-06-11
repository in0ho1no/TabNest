using System.Diagnostics;
using TabNest.Core.Interfaces;

namespace TabNest.Core.Services;

/// <summary>
/// シェル経由(UseShellExecute)でファイルを既定アプリで開く <see cref="IFileLauncher"/> 実装。
/// </summary>
public sealed class ShellFileLauncher : IFileLauncher
{
    public bool OpenFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
