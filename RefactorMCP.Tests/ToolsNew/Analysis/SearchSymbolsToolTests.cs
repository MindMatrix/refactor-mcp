using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using RefactorMCP.ConsoleApp.Tools.Analysis;
using Xunit;

namespace RefactorMCP.Tests.ToolsNew.Analysis;

public class SearchSymbolsToolTests : TestBase
{
    #region Integration Tests - Searching actual solution

    [Fact]
    public async Task SearchSymbols_FindsWalkersInSolution()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await SearchSymbolsTool.SearchSymbols(
            "*Walker",
            SolutionPath,
            "class",
            true,
            CancellationToken.None);

        Assert.Contains("ComplexityWalker", result);
        Assert.Contains("SymbolCollectorWalker", result);
    }

    [Fact]
    public async Task SearchSymbols_FindsToolsInSolution()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await SearchSymbolsTool.SearchSymbols(
            "*Tool",
            SolutionPath,
            "class",
            true,
            CancellationToken.None);

        Assert.Contains("SearchSymbolsTool", result);
        Assert.Contains("ExtractMethodTool", result);
    }

    [Fact]
    public async Task SearchSymbols_FilterByMultipleTypes_FindsMatchingTypes()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await SearchSymbolsTool.SearchSymbols(
            "*Rewriter",
            SolutionPath,
            "class",
            true,
            CancellationToken.None);

        // Should find the various rewriter classes in SyntaxRewriters folder
        Assert.Contains("ExtractMethodRewriter", result);
    }

    [Fact]
    public async Task SearchSymbols_NoMatches_ReturnsEmptyMessage()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        var result = await SearchSymbolsTool.SearchSymbols(
            "XyzNonExistentSymbol123*",
            SolutionPath,
            null,
            true,
            CancellationToken.None);

        Assert.Contains("No symbols found", result);
    }

    [Fact]
    public async Task SearchSymbols_EmptyPattern_ThrowsException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await SearchSymbolsTool.SearchSymbols(
                "",
                SolutionPath,
                null,
                true,
                CancellationToken.None));
    }

    [Fact]
    public async Task SearchSymbols_WhitespacePattern_ThrowsException()
    {
        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);

        await Assert.ThrowsAsync<McpException>(async () =>
            await SearchSymbolsTool.SearchSymbols(
                "   ",
                SolutionPath,
                null,
                true,
                CancellationToken.None));
    }

    #endregion

    #region Unit Tests - SymbolCollectorWalker pattern matching

    [Fact]
    public void Walker_WildcardStar_MatchesAnySequence()
    {
        const string code = """
            public class UserService { }
            public class AuthService { }
            public class ProductRepository { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("*Service", null, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("UserService", names);
        Assert.Contains("AuthService", names);
        Assert.DoesNotContain("ProductRepository", names);
    }

    [Fact]
    public void Walker_WildcardQuestion_MatchesSingleCharacter()
    {
        const string code = """
            public class Cat { }
            public class Car { }
            public class Cab { }
            public class Cart { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("Ca?", null, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("Cat", names);
        Assert.Contains("Car", names);
        Assert.Contains("Cab", names);
        Assert.DoesNotContain("Cart", names);
    }

    [Fact]
    public void Walker_CaseSensitive_RespectsCase()
    {
        const string code = """
            public class UserService { }
            public class USERSERVICE { }
            public class userservice { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("UserService", null, ignoreCase: false);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Single(names);
        Assert.Contains("UserService", names);
    }

    [Fact]
    public void Walker_CaseInsensitive_MatchesAllCases()
    {
        const string code = """
            public class UserService { }
            public class USERSERVICE { }
            public class userservice { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("userservice", null, ignoreCase: true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Equal(3, names.Count);
        Assert.Contains("UserService", names);
        Assert.Contains("USERSERVICE", names);
        Assert.Contains("userservice", names);
    }

    [Fact]
    public void Walker_FilterByClass_FindsOnlyClasses()
    {
        const string code = """
            public class TestClass { }
            public interface ITestInterface { }
            public struct TestStruct { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("Test*", new HashSet<string> { "class" }, true);
        walker.Visit(tree.GetRoot());

        var symbols = walker.CollectedSymbols;
        Assert.Single(symbols);
        Assert.Equal("TestClass", symbols[0].Name);
        Assert.Equal("class", symbols[0].Kind);
    }

    [Fact]
    public void Walker_FilterByMethod_FindsOnlyMethods()
    {
        const string code = """
            public class TestClass
            {
                public string Name { get; set; }
                public void DoWork() { }
                public int Calculate() => 0;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("*", new HashSet<string> { "method" }, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("DoWork", names);
        Assert.Contains("Calculate", names);
        Assert.DoesNotContain("Name", names);
        Assert.DoesNotContain("TestClass", names);
    }

    [Fact]
    public void Walker_MultipleSymbolTypes_FindsAll()
    {
        const string code = """
            public class TestService { }
            public interface TestInterface { }
            public struct TestStruct { }
            public enum TestEnum { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("Test*", new HashSet<string> { "class", "interface", "struct" }, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("TestService", names);
        Assert.Contains("TestInterface", names);
        Assert.Contains("TestStruct", names);
        Assert.DoesNotContain("TestEnum", names);
    }

    [Fact]
    public void Walker_AllSymbolTypes_FindsEverything()
    {
        const string code = """
            namespace Test
            {
                public class MyClass { }
                public interface IMyInterface { }
                public struct MyStruct { }
                public enum MyEnum { Value1 }
                public record MyRecord { }
                public delegate void MyDelegate();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("My*", null, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("MyClass", names);
        Assert.Contains("MyStruct", names);
        Assert.Contains("MyEnum", names);
        Assert.Contains("MyRecord", names);
        Assert.Contains("MyDelegate", names);
    }

    [Fact]
    public void Walker_InterfacesStartingWithI_FindsCorrectly()
    {
        const string code = """
            public interface IMyInterface { }
            public class ImposterClass { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("I*", new HashSet<string> { "interface" }, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Single(names);
        Assert.Contains("IMyInterface", names);
    }

    [Fact]
    public void Walker_FieldsAndProperties_DistinguishesCorrectly()
    {
        const string code = """
            public class DataClass
            {
                private string _name;
                public string Name { get; set; }
                public int Value;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);

        var fieldWalker = new SymbolCollectorWalker("*", new HashSet<string> { "field" }, true);
        fieldWalker.Visit(tree.GetRoot());

        var propWalker = new SymbolCollectorWalker("*", new HashSet<string> { "property" }, true);
        propWalker.Visit(tree.GetRoot());

        var fieldNames = fieldWalker.CollectedSymbols.Select(s => s.Name).ToList();
        var propNames = propWalker.CollectedSymbols.Select(s => s.Name).ToList();

        Assert.Contains("_name", fieldNames);
        Assert.Contains("Value", fieldNames);
        Assert.DoesNotContain("Name", fieldNames);

        Assert.Contains("Name", propNames);
        Assert.DoesNotContain("_name", propNames);
        Assert.DoesNotContain("Value", propNames);
    }

    [Fact]
    public void Walker_Events_FindsEventDeclarations()
    {
        const string code = """
            public class EventClass
            {
                public event EventHandler OnClick;
                public event Action<string> OnMessage;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("On*", new HashSet<string> { "event" }, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("OnClick", names);
        Assert.Contains("OnMessage", names);
    }

    [Fact]
    public void Walker_NestedClasses_FindsBothLevels()
    {
        const string code = """
            public class OuterClass
            {
                public class InnerClass { }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("*Class", new HashSet<string> { "class" }, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("OuterClass", names);
        Assert.Contains("InnerClass", names);
    }

    [Fact]
    public void Walker_ComplexPattern_CombinesWildcards()
    {
        const string code = """
            public class GetUserById { }
            public class GetOrderById { }
            public class GetUserByName { }
            public class PostUser { }
            """;

        var tree = CSharpSyntaxTree.ParseText(code);
        var walker = new SymbolCollectorWalker("Get*By?d", null, true);
        walker.Visit(tree.GetRoot());

        var names = walker.CollectedSymbols.Select(s => s.Name).ToList();
        Assert.Contains("GetUserById", names);
        Assert.Contains("GetOrderById", names);
        Assert.DoesNotContain("GetUserByName", names);
        Assert.DoesNotContain("PostUser", names);
    }

    #endregion
}
