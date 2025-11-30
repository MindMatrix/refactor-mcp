using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using RefactorMCP.ConsoleApp.Models;

namespace RefactorMCP.ConsoleApp.Tools.Analysis;

[McpServerToolType]
public static class FindReferencesTool
{
    [McpServerTool, Description("Find all references to a symbol (class, method, property, etc.) in the solution")]
    public static async Task<string> FindReferences(
        [Description("Full or partial symbol name to find references for")] string symbolName,
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Include the symbol definition in results (default: true)")] bool includeDefinition = true,
        [Description("Number of context lines to show around each reference (default: 2)")] int contextLines = 2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbolName))
                throw new McpException("Error: Symbol name cannot be empty");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var results = new List<ReferenceResult>();
            ISymbol? foundSymbol = null;

            // Find the symbol definition
            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null) continue;

                foreach (var document in project.Documents)
                {
                    if (document.FilePath == null || !document.FilePath.EndsWith(".cs"))
                        continue;

                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                    if (root == null || semanticModel == null) continue;

                    // Look for symbol declarations matching the name
                    var declarationNode = FindDeclarationByName(root, symbolName);
                    if (declarationNode != null)
                    {
                        foundSymbol = semanticModel.GetDeclaredSymbol(declarationNode);
                        if (foundSymbol != null)
                            break;
                    }
                }

                if (foundSymbol != null) break;
            }

            if (foundSymbol == null)
                return $"No symbol found with name '{symbolName}'";

            // Find all references
            var references = await SymbolFinder.FindReferencesAsync(foundSymbol, solution, cancellationToken);

            foreach (var referencedSymbol in references)
            {
                // Add definition if requested
                if (includeDefinition)
                {
                    foreach (var location in referencedSymbol.Definition.Locations)
                    {
                        if (location.IsInSource)
                        {
                            var result = await CreateReferenceResult(
                                solution,
                                location,
                                foundSymbol.Name,
                                true,
                                contextLines,
                                cancellationToken);
                            if (result != null)
                                results.Add(result);
                        }
                    }
                }

                // Add references
                foreach (var refLocation in referencedSymbol.Locations)
                {
                    var result = await CreateReferenceResult(
                        solution,
                        refLocation.Location,
                        foundSymbol.Name,
                        false,
                        contextLines,
                        cancellationToken);
                    if (result != null)
                        results.Add(result);
                }
            }

            return FormatResults(symbolName, foundSymbol, results, includeDefinition);
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
            throw new McpException($"Error finding references: {ex.Message}", ex);
        }
    }

    private static SyntaxNode? FindDeclarationByName(SyntaxNode root, string symbolName)
    {
        // Check for exact match first, then partial match
        foreach (var node in root.DescendantNodes())
        {
            var name = node switch
            {
                ClassDeclarationSyntax c => c.Identifier.Text,
                InterfaceDeclarationSyntax i => i.Identifier.Text,
                MethodDeclarationSyntax m => m.Identifier.Text,
                PropertyDeclarationSyntax p => p.Identifier.Text,
                FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
                EnumDeclarationSyntax e => e.Identifier.Text,
                StructDeclarationSyntax s => s.Identifier.Text,
                RecordDeclarationSyntax r => r.Identifier.Text,
                DelegateDeclarationSyntax d => d.Identifier.Text,
                EventDeclarationSyntax ev => ev.Identifier.Text,
                _ => null
            };

            if (name != null && name.Equals(symbolName, StringComparison.OrdinalIgnoreCase))
                return node;
        }

        return null;
    }

    private static async Task<ReferenceResult?> CreateReferenceResult(
        Solution solution,
        Location location,
        string symbolName,
        bool isDefinition,
        int contextLines,
        CancellationToken cancellationToken)
    {
        var document = solution.GetDocument(location.SourceTree);
        if (document == null) return null;

        var sourceText = await document.GetTextAsync(cancellationToken);
        var lineSpan = location.GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line;
        var columnNumber = lineSpan.StartLinePosition.Character;

        // Get context lines
        var context = new List<string>();
        var startLine = Math.Max(0, lineNumber - contextLines);
        var endLine = Math.Min(sourceText.Lines.Count - 1, lineNumber + contextLines);

        for (var i = startLine; i <= endLine; i++)
        {
            var line = sourceText.Lines[i];
            var prefix = i == lineNumber ? ">>> " : "    ";
            context.Add($"{prefix}{i + 1,4}: {line}");
        }

        return new ReferenceResult
        {
            SymbolName = symbolName,
            DocumentPath = document.FilePath ?? document.Name,
            ProjectName = document.Project.Name,
            LineNumber = lineNumber + 1,
            ColumnNumber = columnNumber + 1,
            LineText = sourceText.Lines[lineNumber].ToString().Trim(),
            Context = context,
            IsDefinition = isDefinition,
            ReferenceKind = isDefinition ? "Definition" : "Reference"
        };
    }

    private static string FormatResults(
        string symbolName,
        ISymbol symbol,
        List<ReferenceResult> results,
        bool includeDefinition)
    {
        if (results.Count == 0)
            return $"No references found for '{symbolName}'";

        var sb = new StringBuilder();
        var defCount = results.Count(r => r.IsDefinition);
        var refCount = results.Count(r => !r.IsDefinition);

        sb.AppendLine($"Found {refCount} reference(s) to '{symbol.Name}' ({symbol.Kind})");
        if (includeDefinition && defCount > 0)
            sb.AppendLine($"Including {defCount} definition(s)");
        sb.AppendLine();

        // Group by definition first, then references
        if (includeDefinition)
        {
            var definitions = results.Where(r => r.IsDefinition).ToList();
            if (definitions.Any())
            {
                sb.AppendLine("Definition:");
                foreach (var def in definitions)
                {
                    sb.AppendLine($"  {def.DocumentPath}:{def.LineNumber}:{def.ColumnNumber}");
                    if (def.Context != null)
                    {
                        foreach (var line in def.Context)
                            sb.AppendLine($"    {line}");
                    }
                    sb.AppendLine();
                }
            }
        }

        var references = results.Where(r => !r.IsDefinition).ToList();
        if (references.Any())
        {
            sb.AppendLine("References:");
            var grouped = references.GroupBy(r => r.ProjectName);
            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                sb.AppendLine($"  [{group.Key}]");
                foreach (var reference in group.OrderBy(r => r.DocumentPath).ThenBy(r => r.LineNumber))
                {
                    sb.AppendLine($"    {Path.GetFileName(reference.DocumentPath)}:{reference.LineNumber}:{reference.ColumnNumber}");
                    sb.AppendLine($"      {reference.LineText}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
