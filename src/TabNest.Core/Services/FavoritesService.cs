using TabNest.Core.Models;

namespace TabNest.Core.Services;

/// <summary>
/// お気に入り(保存済みタブグループ)の保存・取得・削除を管理するサービス
/// (SPEC「主要機能 > お気に入り(保存済みタブグループ)」準拠)。
/// 一覧は保存順(SavedAt 昇順)を維持する。UI への表示は Task 4-6 で実装する。
/// </summary>
public sealed class FavoritesService
{
    /// <summary>お気に入りの最大件数。上限到達時は新規保存を拒否する(自動削除しない)。</summary>
    public const int MaxFavorites = 50;

    private readonly List<SavedTabGroup> _savedGroups = new();

    /// <summary>お気に入り一覧(保存順)。</summary>
    public IReadOnlyList<SavedTabGroup> SavedGroups => _savedGroups;

    /// <summary>
    /// 指定されたグループをお気に入りとして保存する(スナップショット。
    /// 保存後に元グループを変更してもお気に入りは変わらない)。
    /// 名前はグループ名をそのまま使い、同名(完全一致)のお気に入りが既にある場合は
    /// 「&lt;名前&gt; (n)」(n は 2 から始まる未使用の最小値)を付けて別のお気に入りとして保存する。
    /// タブ0個のグループ・上限(50)到達時は保存せず失敗結果を返す。
    /// </summary>
    public TabOperationResult<SavedTabGroup> SaveFavorite(TabGroup group)
    {
        if (group.Tabs.Count == 0)
        {
            return TabOperationResult<SavedTabGroup>.Failure(
                TabOperationError.EmptyGroupNotSavable,
                "タブが0個のグループはお気に入りに保存できません。");
        }

        if (_savedGroups.Count >= MaxFavorites)
        {
            return TabOperationResult<SavedTabGroup>.Failure(
                TabOperationError.FavoriteLimitReached,
                $"お気に入りは最大 {MaxFavorites} 件までです。");
        }

        var favorite = new SavedTabGroup
        {
            Id = Guid.NewGuid().ToString(),
            Name = ResolveUniqueName(group.Name),
            Paths = group.Tabs.Select(t => t.Path).ToList(),
            SavedAt = DateTime.Now,
        };
        _savedGroups.Add(favorite);
        return TabOperationResult<SavedTabGroup>.Success(favorite);
    }

    /// <summary>Id でお気に入りを取得する。無ければ null。</summary>
    public SavedTabGroup? FindFavorite(string id)
        => _savedGroups.FirstOrDefault(f => f.Id == id);

    /// <summary>お気に入りを削除する。存在しない場合は false。</summary>
    public bool RemoveFavorite(string id)
        => _savedGroups.RemoveAll(f => f.Id == id) > 0;

    /// <summary>
    /// 保存済みセッション(settings.json)からお気に入り一覧を復元する。
    /// 上限を超える分は古い順(先頭優先)に残して切り捨て、Id が空の要素には新しい Id を採番する。
    /// </summary>
    public void RestoreSavedGroups(IEnumerable<SavedTabGroup> savedGroups)
    {
        _savedGroups.Clear();
        foreach (var favorite in savedGroups.Take(MaxFavorites))
        {
            if (string.IsNullOrEmpty(favorite.Id))
            {
                favorite.Id = Guid.NewGuid().ToString();
            }

            _savedGroups.Add(favorite);
        }
    }

    /// <summary>
    /// 同名衝突を回避した保存名を決める。同名判定は名前の完全一致で行い、ベース名の抽出はしない
    /// (名前 S が「作業1 (2)」のようにサフィックスを含んでいても S 全体を1つの名前として扱う)。
    /// </summary>
    private string ResolveUniqueName(string name)
    {
        var existing = _savedGroups.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        if (!existing.Contains(name))
        {
            return name;
        }

        var number = 2;
        while (existing.Contains($"{name} ({number})"))
        {
            number++;
        }

        return $"{name} ({number})";
    }
}
