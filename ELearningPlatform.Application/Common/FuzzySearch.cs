using FuzzySharp;

namespace ELearningPlatform.Application.Common;

/// <summary>
/// Typo-tolerant name search. Callers load only (id, searchable text) pairs, rank them here,
/// and then fetch full rows for the best matches, so whole entities are never pulled into memory.
/// </summary>
public static class FuzzySearch
{
    public const int MinScore = 70;
    public const int MaxResults = 50;

    public static List<TKey> RankIds<TKey>(IEnumerable<(TKey Id, string[] Texts)> candidates, string term,
        int minScore = MinScore, int maxResults = MaxResults)
    {
        if (string.IsNullOrWhiteSpace(term))
            return [];

        var needle = term.Trim().ToLowerInvariant();
        return candidates
            .Select(c => (c.Id, Score: c.Texts.Max(t => Fuzz.PartialRatio(t.ToLowerInvariant(), needle))))
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>Puts rows back into ranking order after an unordered "id in (...)" query.</summary>
    public static List<T> OrderByRank<T, TKey>(IEnumerable<T> rows, Func<T, TKey> key, IReadOnlyList<TKey> rankedIds)
        where TKey : notnull
    {
        var rank = rankedIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return rows.OrderBy(r => rank[key(r)]).ToList();
    }
}
