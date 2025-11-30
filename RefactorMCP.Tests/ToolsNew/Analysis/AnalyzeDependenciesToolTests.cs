using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Analysis;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew.Analysis;

public class AnalyzeDependenciesToolTests : TestBase
{
    [Fact]
    public async Task AnalyzeDependencies_AllProjects_ReturnsAnalysis()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            null,
            CancellationToken.None);

        Assert.Contains("Dependency Analysis", result);
        Assert.Contains("RefactorMCP", result);
    }

    [Fact]
    public async Task AnalyzeDependencies_SpecificProject_ReturnsProjectAnalysis()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            "RefactorMCP.ConsoleApp",
            CancellationToken.None);

        Assert.Contains("RefactorMCP.ConsoleApp", result);
        Assert.Contains("Dependency Analysis", result);
    }

    [Fact]
    public async Task AnalyzeDependencies_IncludesPackageReferences()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            "RefactorMCP.ConsoleApp",
            CancellationToken.None);

        // Should include Microsoft.CodeAnalysis packages
        Assert.Contains("Package References", result);
        Assert.Contains("Microsoft.CodeAnalysis", result);
    }

    [Fact]
    public async Task AnalyzeDependencies_IncludesNamespaceUsages()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            "RefactorMCP.ConsoleApp",
            CancellationToken.None);

        Assert.Contains("Most Used Namespaces", result);
        Assert.Contains("System", result);
    }

    [Fact]
    public async Task AnalyzeDependencies_IncludesSymbolDistribution()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            "RefactorMCP.ConsoleApp",
            CancellationToken.None);

        Assert.Contains("Symbol Distribution", result);
        Assert.Contains("Total", result);
    }

    [Fact]
    public async Task AnalyzeDependencies_NonExistentProject_ReturnsNotFound()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            "NonExistentProject123",
            CancellationToken.None);

        Assert.Contains("No project found", result);
    }

    [Fact]
    public async Task AnalyzeDependencies_TestProject_ShowsTestDependencies()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await AnalyzeDependenciesTool.AnalyzeDependencies(
            SolutionPath,
            "RefactorMCP.Tests",
            CancellationToken.None);

        Assert.Contains("RefactorMCP.Tests", result);
        // Should show xunit package
        Assert.Contains("xunit", result.ToLowerInvariant());
    }
}
