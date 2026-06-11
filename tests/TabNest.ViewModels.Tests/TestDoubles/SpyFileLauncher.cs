using TabNest.Core.Interfaces;

namespace TabNest.ViewModels.Tests.TestDoubles;

/// <summary>呼び出しを記録する IFileLauncher スパイ。</summary>
public sealed class SpyFileLauncher : IFileLauncher
{
    public List<string> OpenedPaths { get; } = [];

    public bool NextResult { get; set; } = true;

    public bool OpenFile(string path)
    {
        OpenedPaths.Add(path);
        return NextResult;
    }
}
