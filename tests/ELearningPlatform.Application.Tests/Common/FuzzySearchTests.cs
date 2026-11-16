namespace ELearningPlatform.Application.Tests.Common;

public class FuzzySearchTests
{
    private static readonly (int Id, string[] Texts)[] Candidates =
    [
        (1, ["Introduction to Programming"]),
        (2, ["Advanced Physics"]),
        (3, ["Data Structures", "ds.mp4"]),
        (4, ["Programming in C#"]),
    ];

    [Theory]
    [InlineData("physics", new[] { 2 })]
    [InlineData("PHYSICS", new[] { 2 })]
    [InlineData("Phisics", new[] { 2 })]
    [InlineData("ds.mp4", new[] { 3 })]
    [InlineData("structures", new[] { 3 })]
    public void Finds_expected_ids(string term, int[] expected)
    {
        Assert.Equal(expected, FuzzySearch.RankIds(Candidates, term));
    }

    [Fact]
    public void Matches_any_of_several_texts()
    {
        Assert.Contains(3, FuzzySearch.RankIds(Candidates, "ds.mp4"));
    }

    [Fact]
    public void Multiple_matches_are_all_returned()
    {
        var ids = FuzzySearch.RankIds(Candidates, "programming");

        Assert.Equal([1, 4], ids.Order());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Blank_term_matches_nothing(string? term)
    {
        Assert.Empty(FuzzySearch.RankIds(Candidates, term!));
    }

    [Fact]
    public void Unrelated_term_matches_nothing()
    {
        Assert.Empty(FuzzySearch.RankIds(Candidates, "xqzw"));
    }

    [Fact]
    public void Max_results_is_respected()
    {
        var many = Enumerable.Range(1, 200).Select(i => (i, new[] { $"Course {i}" }));

        Assert.Equal(5, FuzzySearch.RankIds(many, "course", maxResults: 5).Count);
        Assert.Equal(FuzzySearch.MaxResults, FuzzySearch.RankIds(many, "course").Count);
    }

    [Fact]
    public void Min_score_can_be_raised()
    {
        Assert.Empty(FuzzySearch.RankIds(Candidates, "Phisics", minScore: 100));
    }

    [Fact]
    public void Order_by_rank_restores_ranking_order()
    {
        var rows = new[] { "b", "c", "a" };

        var ordered = FuzzySearch.OrderByRank(rows, r => r, ["a", "b", "c"]);

        Assert.Equal(["a", "b", "c"], ordered);
    }
}
