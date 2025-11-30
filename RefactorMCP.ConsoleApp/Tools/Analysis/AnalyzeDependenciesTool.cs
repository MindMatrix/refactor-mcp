using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Models;

namespace RefactorMCP.ConsoleApp.Tools.Analysis;

[McpServerToolType]
public static class AnalyzeDependenciesTool
{
    [McpServerTool, Description("Analyze project dependencies and namespace usage in the solution")]
    public static async Task<string> AnalyzeDependencies(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Project name to analyze (leave empty for all projects)")] string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var analysisResults = new List<DependencyAnalysis>();

            var projectsToAnalyze = string.IsNullOrWhiteSpace(projectName)
                ? solution.Projects.ToList()
                : solution.Projects.Where(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!projectsToAnalyze.Any())
            {
                if (!string.IsNullOrWhiteSpace(projectName))
                    return $"No project found with name '{projectName}'";
                return "No projects found in solution";
            }

            foreach (var project in projectsToAnalyze)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var analysis = await AnalyzeProject(project, solution, cancellationToken);
                analysisResults.Add(analysis);
            }

            return FormatResults(analysisResults);
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
            throw new McpException($"Error analyzing dependencies: {ex.Message}", ex);
        }
    }

    private static async Task<DependencyAnalysis> AnalyzeProject(
        Project project,
        Solution solution,
        CancellationToken cancellationToken)
    {
        var dependencies = new List<DependencyInfo>();
        var namespaceUsages = new Dictionary<string, int>();
        var totalSymbols = 0;
        var publicSymbols = 0;
        var internalSymbols = 0;

        // Analyze project references
        foreach (var projectRef in project.ProjectReferences)
        {
            var refProject = solution.GetProject(projectRef.ProjectId);
            if (refProject != null)
            {
                var usageCount = await CountProjectUsage(project, refProject.Name, cancellationToken);
                dependencies.Add(new DependencyInfo(refProject.Name, null, "Project", usageCount));
            }
        }

        // Analyze package references from .csproj file
        if (!string.IsNullOrEmpty(project.FilePath) && File.Exists(project.FilePath))
        {
            var packageRefs = ParsePackageReferences(project.FilePath);
            foreach (var (name, version) in packageRefs)
            {
                dependencies.Add(new DependencyInfo(name, version, "Package", 0));
            }
        }

        // Analyze namespace usages and symbol counts
        foreach (var document in project.Documents)
        {
            if (document.FilePath == null || !document.FilePath.EndsWith(".cs"))
                continue;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (root == null) continue;

            // Count using directives
            var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            foreach (var usingDirective in usingDirectives)
            {
                var ns = usingDirective.Name?.ToString();
                if (!string.IsNullOrEmpty(ns))
                {
                    namespaceUsages.TryGetValue(ns!, out var count);
                    namespaceUsages[ns!] = count + 1;
                }
            }

            // Count symbols and their accessibility
            var typeDeclarations = root.DescendantNodes()
                .Where(n => n is TypeDeclarationSyntax or EnumDeclarationSyntax or DelegateDeclarationSyntax);

            foreach (var typeNode in typeDeclarations)
            {
                totalSymbols++;

                if (semanticModel != null)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(typeNode);
                    if (symbol != null)
                    {
                        if (symbol.DeclaredAccessibility == Accessibility.Public)
                            publicSymbols++;
                        else if (symbol.DeclaredAccessibility == Accessibility.Internal)
                            internalSymbols++;
                    }
                }
            }
        }

        return new DependencyAnalysis
        {
            ProjectName = project.Name,
            Dependencies = dependencies,
            NamespaceUsages = namespaceUsages,
            TotalSymbols = totalSymbols,
            PublicSymbols = publicSymbols,
            InternalSymbols = internalSymbols
        };
    }

    private static async Task<int> CountProjectUsage(
        Project project,
        string referencedProjectName,
        CancellationToken cancellationToken)
    {
        var usageCount = 0;

        foreach (var document in project.Documents)
        {
            if (document.FilePath == null || !document.FilePath.EndsWith(".cs"))
                continue;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) continue;

            // Simple heuristic: count files that use types from the referenced project
            // This is a simplified check - more accurate would require semantic analysis
            var text = root.ToFullString();
            if (text.Contains(referencedProjectName))
                usageCount++;
        }

        return usageCount;
    }

    private static List<(string Name, string? Version)> ParsePackageReferences(string projectFilePath)
    {
        var packages = new List<(string Name, string? Version)>();

        try
        {
            var doc = XDocument.Load(projectFilePath);
            var packageRefs = doc.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference");

            foreach (var packageRef in packageRefs)
            {
                var name = packageRef.Attribute("Include")?.Value;
                var version = packageRef.Attribute("Version")?.Value ??
                              packageRef.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

                if (!string.IsNullOrEmpty(name))
                    packages.Add((name, version));
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return packages;
    }

    private static string FormatResults(List<DependencyAnalysis> results)
    {
        var sb = new StringBuilder();

        foreach (var analysis in results)
        {
            sb.AppendLine($"Dependency Analysis for: {analysis.ProjectName}");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine();

            // Project References
            var projectRefs = analysis.Dependencies.Where(d => d.Type == "Project").ToList();
            if (projectRefs.Any())
            {
                sb.AppendLine($"Project References ({projectRefs.Count}):");
                foreach (var dep in projectRefs.OrderByDescending(d => d.UsageCount))
                {
                    var usageInfo = dep.UsageCount > 0 ? $" [used in {dep.UsageCount} file(s)]" : "";
                    sb.AppendLine($"  - {dep.Name}{usageInfo}");
                }
                sb.AppendLine();
            }

            // Package References
            var packageRefs = analysis.Dependencies.Where(d => d.Type == "Package").ToList();
            if (packageRefs.Any())
            {
                sb.AppendLine($"Package References ({packageRefs.Count}):");
                foreach (var dep in packageRefs.OrderBy(d => d.Name))
                {
                    var versionInfo = !string.IsNullOrEmpty(dep.Version) ? $" ({dep.Version})" : "";
                    sb.AppendLine($"  - {dep.Name}{versionInfo}");
                }
                sb.AppendLine();
            }

            // Most used namespaces
            if (analysis.NamespaceUsages.Any())
            {
                sb.AppendLine("Most Used Namespaces:");
                foreach (var ns in analysis.NamespaceUsages.OrderByDescending(kv => kv.Value).Take(15))
                {
                    sb.AppendLine($"  - {ns.Key} ({ns.Value} usage(s))");
                }
                sb.AppendLine();
            }

            // Symbol distribution
            sb.AppendLine("Symbol Distribution:");
            sb.AppendLine($"  - Total: {analysis.TotalSymbols} type(s)");
            if (analysis.PublicSymbols > 0)
            {
                var publicPercent = (double)analysis.PublicSymbols / analysis.TotalSymbols * 100;
                sb.AppendLine($"  - Public: {analysis.PublicSymbols} ({publicPercent:F0}%)");
            }
            if (analysis.InternalSymbols > 0)
            {
                var internalPercent = (double)analysis.InternalSymbols / analysis.TotalSymbols * 100;
                sb.AppendLine($"  - Internal: {analysis.InternalSymbols} ({internalPercent:F0}%)");
            }
            var otherSymbols = analysis.TotalSymbols - analysis.PublicSymbols - analysis.InternalSymbols;
            if (otherSymbols > 0)
            {
                var otherPercent = (double)otherSymbols / analysis.TotalSymbols * 100;
                sb.AppendLine($"  - Private/Other: {otherSymbols} ({otherPercent:F0}%)");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
