using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace RefactorMCP.Tests;

public class SummaryResourceTests : TestBase
{
    [Fact]
    public async Task GetSummary_OmitsMethodBodies()
    {
        var result = await SummaryResources.GetSummary(ExampleFilePath, CancellationToken.None);
        var normalized = TestUtilities.NormalizeLineEndings(result);
        Assert.Contains("public int Calculate(int a, int b)\n        {}", normalized);
        Assert.DoesNotContain("throw new ArgumentException", normalized);
    }

    [Fact]
    public async Task GetSummary_FileNotFound_ReturnsMessage()
    {
        var result = await SummaryResources.GetSummary("does_not_exist.cs", CancellationToken.None);
        Assert.StartsWith("// File not found:", result);
    }
}
