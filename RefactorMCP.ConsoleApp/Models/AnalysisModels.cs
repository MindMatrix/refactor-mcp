using System.Collections.Generic;

namespace RefactorMCP.ConsoleApp.Models;

/// <summary>
/// Result from symbol search operations.
/// </summary>
public record SymbolSearchResult
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Category { get; init; }      // class, method, property, etc.
    public required string Location { get; init; }      // Project:File:Line
    public string? Accessibility { get; init; }
    public string? SymbolKind { get; init; }
    public string? Namespace { get; init; }
    public string? Summary { get; init; }
}

/// <summary>
/// Result from find references operations.
/// </summary>
public record ReferenceResult
{
    public required string SymbolName { get; init; }
    public required string DocumentPath { get; init; }
    public required string ProjectName { get; init; }
    public required int LineNumber { get; init; }
    public required int ColumnNumber { get; init; }
    public string? LineText { get; init; }
    public List<string>? Context { get; init; }         // Surrounding lines
    public bool IsDefinition { get; init; }
    public string? ReferenceKind { get; init; }
}

/// <summary>
/// Detailed information about a symbol.
/// </summary>
public record SymbolInfo
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Kind { get; init; }
    public string? ContainingType { get; init; }
    public string? ContainingNamespace { get; init; }
    public string? Accessibility { get; init; }
    public string? ReturnType { get; init; }
    public List<ParameterInfo>? Parameters { get; init; }
    public List<string>? BaseTypes { get; init; }
    public List<string>? Interfaces { get; init; }
    public List<string>? Attributes { get; init; }
    public string? Documentation { get; init; }
    public string? SourceLocation { get; init; }
}

/// <summary>
/// Parameter information for methods.
/// </summary>
public record ParameterInfo(string Name, string Type, string? DefaultValue);

/// <summary>
/// Result from cyclomatic complexity analysis.
/// </summary>
public record ComplexityResult
{
    public required string MethodName { get; init; }
    public required string FileName { get; init; }
    public required int LineNumber { get; init; }
    public required int Complexity { get; init; }
    public string? ClassName { get; init; }
    public string? Namespace { get; init; }
}

/// <summary>
/// Project dependency analysis result.
/// </summary>
public record DependencyAnalysis
{
    public required string ProjectName { get; init; }
    public required List<DependencyInfo> Dependencies { get; init; }
    public required Dictionary<string, int> NamespaceUsages { get; init; }
    public int TotalSymbols { get; init; }
    public int PublicSymbols { get; init; }
    public int InternalSymbols { get; init; }
}

/// <summary>
/// Information about a single dependency.
/// </summary>
public record DependencyInfo(string Name, string? Version, string Type, int UsageCount);
