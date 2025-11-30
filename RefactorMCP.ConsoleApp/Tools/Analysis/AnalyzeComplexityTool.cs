using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Models;
using RefactorMCP.ConsoleApp.SyntaxWalkers;

namespace RefactorMCP.ConsoleApp.Tools.Analysis;

[McpServerToolType]
public static class AnalyzeComplexityTool
{
    [McpServerTool, Description("Analyze cyclomatic complexity of methods in the solution to identify refactoring candidates")]
    public static async Task<string> AnalyzeComplexity(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Only report methods with complexity >= threshold (default: 7)")] int threshold = 7,
        [Description("Maximum number of results to return (default: 50)")] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (threshold < 1)
                throw new McpException("Error: Threshold must be at least 1");

            if (limit < 1)
                throw new McpException("Error: Limit must be at least 1");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var results = new List<ComplexityResult>();

            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var document in project.Documents)
                {
                    if (document.FilePath == null || !document.FilePath.EndsWith(".cs"))
                        continue;

                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    if (root == null) continue;

                    // Find all methods in the document
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

                    foreach (var method in methods)
                    {
                        if (method.Body == null && method.ExpressionBody == null)
                            continue;

                        var walker = new ComplexityWalker();
                        walker.Visit(method);

                        if (walker.Complexity >= threshold)
                        {
                            var lineSpan = method.GetLocation().GetLineSpan();

                            // Get containing class/struct name
                            var containingType = method.Ancestors()
                                .OfType<TypeDeclarationSyntax>()
                                .FirstOrDefault();

                            // Get namespace
                            var ns = method.Ancestors()
                                .OfType<BaseNamespaceDeclarationSyntax>()
                                .FirstOrDefault();

                            results.Add(new ComplexityResult
                            {
                                MethodName = method.Identifier.Text,
                                FileName = document.FilePath,
                                LineNumber = lineSpan.StartLinePosition.Line + 1,
                                Complexity = walker.Complexity,
                                ClassName = containingType?.Identifier.Text,
                                Namespace = ns?.Name.ToString()
                            });
                        }
                    }
                }
            }

            // Sort by complexity descending and limit results
            results = results
                .OrderByDescending(r => r.Complexity)
                .Take(limit)
                .ToList();

            return FormatResults(results, threshold, limit);
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
            throw new McpException($"Error analyzing complexity: {ex.Message}", ex);
        }
    }

    private static string FormatResults(List<ComplexityResult> results, int threshold, int limit)
    {
        if (results.Count == 0)
            return $"No methods found with complexity >= {threshold}";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} method(s) with complexity >= {threshold}:");
        sb.AppendLine();

        // Group by complexity level
        var highComplexity = results.Where(r => r.Complexity > 10).ToList();
        var mediumComplexity = results.Where(r => r.Complexity >= 7 && r.Complexity <= 10).ToList();
        var lowComplexity = results.Where(r => r.Complexity < 7).ToList();

        var index = 1;

        if (highComplexity.Any())
        {
            sb.AppendLine("High Complexity (>10) - Consider breaking into smaller methods:");
            foreach (var result in highComplexity)
            {
                sb.AppendLine($"  {index++}. {FormatMethodName(result)} (complexity: {result.Complexity})");
                sb.AppendLine($"     {Path.GetFileName(result.FileName)}:{result.LineNumber}");
                sb.AppendLine($"     Suggestion: {GetSuggestion(result.Complexity)}");
                sb.AppendLine();
            }
        }

        if (mediumComplexity.Any())
        {
            sb.AppendLine("Medium Complexity (7-10) - Consider refactoring if business logic is unclear:");
            foreach (var result in mediumComplexity)
            {
                sb.AppendLine($"  {index++}. {FormatMethodName(result)} (complexity: {result.Complexity})");
                sb.AppendLine($"     {Path.GetFileName(result.FileName)}:{result.LineNumber}");
                sb.AppendLine();
            }
        }

        if (lowComplexity.Any())
        {
            sb.AppendLine("Lower Complexity (<7) - Generally acceptable:");
            foreach (var result in lowComplexity)
            {
                sb.AppendLine($"  {index++}. {FormatMethodName(result)} (complexity: {result.Complexity})");
                sb.AppendLine($"     {Path.GetFileName(result.FileName)}:{result.LineNumber}");
            }
            sb.AppendLine();
        }

        // Summary statistics
        if (results.Any())
        {
            sb.AppendLine("Statistics:");
            sb.AppendLine($"  Average complexity: {results.Average(r => r.Complexity):F1}");
            sb.AppendLine($"  Max complexity: {results.Max(r => r.Complexity)}");
            sb.AppendLine($"  High complexity methods: {highComplexity.Count}");
            sb.AppendLine($"  Medium complexity methods: {mediumComplexity.Count}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMethodName(ComplexityResult result)
    {
        if (!string.IsNullOrEmpty(result.ClassName))
            return $"{result.ClassName}.{result.MethodName}";
        return result.MethodName;
    }

    private static string GetSuggestion(int complexity)
    {
        return complexity switch
        {
            > 20 => "Critical complexity - strongly recommend splitting into multiple methods",
            > 15 => "Very high complexity - consider Extract Method refactoring for nested conditionals",
            > 10 => "High complexity - look for opportunities to extract helper methods",
            _ => "Consider simplifying conditional logic"
        };
    }
}
