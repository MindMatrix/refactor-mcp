using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using RefactorMCP.ConsoleApp.SyntaxWalkers;
using RefactorMCP.ConsoleApp.Models;
using Microsoft.CodeAnalysis;

namespace RefactorMCP.ConsoleApp.Tools.Analysis;

[McpServerToolType]
public static class SearchSymbolsTool
{
    [McpServerTool, Description("Search for symbols matching a pattern across the solution using wildcards (* and ?)")]
    public static async Task<string> SearchSymbols(
        [Description("Pattern with wildcards (* and ?). Examples: '*Service', 'Get*', 'I?Repository'")] string pattern,
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Symbol types to search (comma-separated): class, interface, method, property, field, event, enum, struct, record, delegate. Leave empty for all.")] string? symbolTypes = null,
        [Description("Ignore case when matching (default: true)")] bool ignoreCase = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new McpException("Error: Pattern cannot be empty");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var typeFilter = ParseSymbolTypes(symbolTypes);
            var results = new List<SymbolSearchResult>();

            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var document in project.Documents)
                {
                    if (document.FilePath == null || !document.FilePath.EndsWith(".cs"))
                        continue;

                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    if (root == null)
                        continue;

                    var walker = new SymbolCollectorWalker(pattern, typeFilter, ignoreCase);
                    walker.Visit(root);

                    foreach (var symbol in walker.CollectedSymbols)
                    {
                        var lineSpan = symbol.Identifier.GetLocation().GetLineSpan();
                        var lineNumber = lineSpan.StartLinePosition.Line + 1;

                        results.Add(new SymbolSearchResult
                        {
                            Name = symbol.Name,
                            FullName = GetFullName(symbol.Node),
                            Category = symbol.Kind,
                            Location = $"{project.Name}:{Path.GetFileName(document.FilePath)}:{lineNumber}",
                            Accessibility = GetAccessibility(symbol.Node),
                            SymbolKind = symbol.Kind,
                            Namespace = GetNamespace(symbol.Node)
                        });
                    }
                }
            }

            return FormatResults(pattern, results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error searching symbols: {ex.Message}", ex);
        }
    }

    private static HashSet<string>? ParseSymbolTypes(string? symbolTypes)
    {
        if (string.IsNullOrWhiteSpace(symbolTypes))
            return null;

        var types = symbolTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();

        return types.Count > 0 ? types : null;
    }

    private static string GetFullName(SyntaxNode node)
    {
        var parts = new List<string>();
        var current = node;

        while (current != null)
        {
            var name = current switch
            {
                Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax type => type.Identifier.Text,
                Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method => method.Identifier.Text,
                Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax prop => prop.Identifier.Text,
                Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax => null, // Skip field declarations
                Microsoft.CodeAnalysis.CSharp.Syntax.EventDeclarationSyntax evt => evt.Identifier.Text,
                Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax del => del.Identifier.Text,
                Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax ns => ns.Name.ToString(),
                Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax fsns => fsns.Name.ToString(),
                _ => null
            };

            if (name != null)
                parts.Add(name);

            current = current.Parent;
        }

        parts.Reverse();
        return string.Join(".", parts);
    }

    private static string? GetAccessibility(SyntaxNode node)
    {
        var modifiers = node switch
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax type => type.Modifiers,
            Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method => method.Modifiers,
            Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax prop => prop.Modifiers,
            Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax field => field.Modifiers,
            Microsoft.CodeAnalysis.CSharp.Syntax.EventDeclarationSyntax evt => evt.Modifiers,
            Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax del => del.Modifiers,
            _ => default
        };

        if (modifiers.Any(m => m.Text == "public")) return "public";
        if (modifiers.Any(m => m.Text == "private")) return "private";
        if (modifiers.Any(m => m.Text == "protected")) return "protected";
        if (modifiers.Any(m => m.Text == "internal")) return "internal";

        return null;
    }

    private static string? GetNamespace(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax ns)
                return ns.Name.ToString();
            if (current is Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax fsns)
                return fsns.Name.ToString();
            current = current.Parent;
        }
        return null;
    }

    private static string FormatResults(string pattern, List<SymbolSearchResult> results)
    {
        if (results.Count == 0)
            return $"No symbols found matching pattern '{pattern}'";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} symbol(s) matching '{pattern}':");
        sb.AppendLine();

        var grouped = results.GroupBy(r => r.Category).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var categoryName = char.ToUpperInvariant(group.Key[0]) + group.Key[1..] + "s";
            if (group.Key == "class") categoryName = "Classes";
            if (group.Key == "property") categoryName = "Properties";

            sb.AppendLine($"{categoryName} ({group.Count()}):");

            foreach (var result in group.OrderBy(r => r.Name))
            {
                var accessibility = result.Accessibility != null ? $"[{result.Accessibility}] " : "";
                sb.AppendLine($"  - {accessibility}{result.Name} [{result.Location}]");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
