using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Analysis;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew.Analysis;

public class AnalyzeComplexityToolTests : TestBase
{
    [Fact]
    public async Task AnalyzeComplexity_FindsComplexMethods()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeComplexityTool.AnalyzeComplexity(
            SolutionPath,
            threshold: 5,
            limit: 10,
            CancellationToken.None);

        // Should find at least some methods above threshold
        Assert.Contains("complexity", result.ToLowerInvariant());
    }

    [Fact]
    public async Task AnalyzeComplexity_HighThreshold_FewerResults()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var lowThresholdResult = await AnalyzeComplexityTool.AnalyzeComplexity(
            SolutionPath,
            threshold: 3,
            limit: 100,
            CancellationToken.None);

        var highThresholdResult = await AnalyzeComplexityTool.AnalyzeComplexity(
            SolutionPath,
            threshold: 15,
            limit: 100,
            CancellationToken.None);

        // Lower threshold should find more methods
        // (or high threshold might find none)
        Assert.True(lowThresholdResult.Length >= highThresholdResult.Length);
    }

    [Fact]
    public async Task AnalyzeComplexity_VeryHighThreshold_NoResults()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeComplexityTool.AnalyzeComplexity(
            SolutionPath,
            threshold: 100,
            limit: 50,
            CancellationToken.None);

        Assert.Contains("No methods found", result);
    }

    [Fact]
    public async Task AnalyzeComplexity_InvalidThreshold_ThrowsException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await AnalyzeComplexityTool.AnalyzeComplexity(
                SolutionPath,
                threshold: 0,
                limit: 50,
                CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeComplexity_InvalidLimit_ThrowsException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await AnalyzeComplexityTool.AnalyzeComplexity(
                SolutionPath,
                threshold: 7,
                limit: 0,
                CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeComplexity_LimitResults_RespectsLimit()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeComplexityTool.AnalyzeComplexity(
            SolutionPath,
            threshold: 1,
            limit: 5,
            CancellationToken.None);

        // Count the number of method entries (each starts with a number followed by period)
        var methodCount = System.Text.RegularExpressions.Regex.Matches(result, @"^\s+\d+\.", System.Text.RegularExpressions.RegexOptions.Multiline).Count;
        Assert.True(methodCount <= 5);
    }

    [Fact]
    public async Task AnalyzeComplexity_IncludesStatistics()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeComplexityTool.AnalyzeComplexity(
            SolutionPath,
            threshold: 3,
            limit: 50,
            CancellationToken.None);

        // If there are results, should include statistics
        if (!result.Contains("No methods found"))
        {
            Assert.Contains("Statistics", result);
            Assert.Contains("Average complexity", result);
        }
    }
}
