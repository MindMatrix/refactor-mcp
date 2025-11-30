using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.Tools.Analysis;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew.Analysis;

public class GetSymbolInfoToolTests : TestBase
{
    [Fact]
    public async Task GetSymbolInfo_Class_ReturnsClassInfo()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await GetSymbolInfoTool.GetSymbolInfo(
            "ComplexityWalker",
            SolutionPath,
            CancellationToken.None);

        Assert.Contains("ComplexityWalker", result);
        Assert.Contains("Kind: NamedType", result);
        Assert.Contains("class", result.ToLowerInvariant());
    }

    [Fact]
    public async Task GetSymbolInfo_Method_ReturnsMethodInfo()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await GetSymbolInfoTool.GetSymbolInfo(
            "GetOrLoadSolution",
            SolutionPath,
            CancellationToken.None);

        Assert.Contains("GetOrLoadSolution", result);
        Assert.Contains("Kind: Method", result);
        Assert.Contains("Return Type", result);
    }

    [Fact]
    public async Task GetSymbolInfo_StaticClass_ShowsStaticModifier()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await GetSymbolInfoTool.GetSymbolInfo(
            "SearchSymbolsTool",
            SolutionPath,
            CancellationToken.None);

        Assert.Contains("SearchSymbolsTool", result);
        Assert.Contains("static", result.ToLowerInvariant());
    }

    [Fact]
    public async Task GetSymbolInfo_NonExistentSymbol_ReturnsNotFound()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await GetSymbolInfoTool.GetSymbolInfo(
            "NonExistentSymbol123456",
            SolutionPath,
            CancellationToken.None);

        Assert.Contains("No symbol found", result);
    }

    [Fact]
    public async Task GetSymbolInfo_EmptySymbolName_ThrowsException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await GetSymbolInfoTool.GetSymbolInfo(
                "",
                SolutionPath,
                CancellationToken.None));
    }

    [Fact]
    public async Task GetSymbolInfo_Record_ReturnsRecordInfo()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await GetSymbolInfoTool.GetSymbolInfo(
            "SymbolSearchResult",
            SolutionPath,
            CancellationToken.None);

        Assert.Contains("SymbolSearchResult", result);
        Assert.Contains("Kind: NamedType", result);
    }
}
