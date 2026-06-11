namespace TabNest.Core.Models;

/// <summary>タブ・グループ操作の失敗理由。</summary>
public enum TabOperationError
{
    None,

    /// <summary>指定されたグループが存在しない。</summary>
    GroupNotFound,

    /// <summary>指定されたタブが存在しない。</summary>
    TabNotFound,

    /// <summary>グループ内のタブ数が上限(20)に達している。</summary>
    TabLimitReached,

    /// <summary>グループ数が上限(5)に達している。</summary>
    GroupLimitReached,

    /// <summary>最後の1グループは削除できない(下限1)。</summary>
    LastGroupProtected,
}

/// <summary>
/// タブ・グループ操作の結果。上限・下限違反などの失敗理由を保持する。
/// </summary>
public sealed class TabOperationResult<T>
    where T : class
{
    private TabOperationResult(bool isSuccess, T? value, TabOperationError error, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    /// <summary>成功時の結果値。失敗時は null。</summary>
    public T? Value { get; }

    public TabOperationError Error { get; }

    /// <summary>失敗時のエラーメッセージ。成功時は null。</summary>
    public string? ErrorMessage { get; }

    public static TabOperationResult<T> Success(T value)
        => new(true, value, TabOperationError.None, null);

    public static TabOperationResult<T> Failure(TabOperationError error, string errorMessage)
        => new(false, null, error, errorMessage);
}
