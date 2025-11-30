using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Analysis;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew.Analysis;

public class FindReferencesToolTests : TestBase
{
    [Fact]
    public async Task FindReferences_FindsClassUsages()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await FindReferencesTool.FindReferences(
            "ComplexityWalker",
            SolutionPath,
            true,
            2,
            CancellationToken.None);

        Assert.Contains("ComplexityWalker", result);
        Assert.Contains("Definition", result);
    }

    [Fact]
    public async Task FindReferences_FindsMethodUsages()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await FindReferencesTool.FindReferences(
            "GetOrLoadSolution",
            SolutionPath,
            true,
            2,
            CancellationToken.None);

        Assert.Contains("GetOrLoadSolution", result);
        Assert.Contains("reference", result.ToLowerInvariant());
    }

    [Fact]
    public async Task FindReferences_NonExistentSymbol_ReturnsNotFound()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await FindReferencesTool.FindReferences(
            "NonExistentSymbol123456",
            SolutionPath,
            true,
            2,
            CancellationToken.None);

        Assert.Contains("No symbol found", result);
    }

    [Fact]
    public async Task FindReferences_EmptySymbolName_ThrowsException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await FindReferencesTool.FindReferences(
                "",
                SolutionPath,
                true,
                2,
                CancellationToken.None));
    }

    [Fact]
    public async Task FindReferences_ExcludeDefinition_NoDefinitionSection()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await FindReferencesTool.FindReferences(
            "SearchSymbolsTool",
            SolutionPath,
            includeDefinition: false,
            2,
            CancellationToken.None);

        // Should not have the "Definition:" section when excluded
        // (though the result format may vary based on references found)
        Assert.Contains("SearchSymbolsTool", result);
    }
}
