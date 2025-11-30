using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RefactorMCP.ConsoleApp.SyntaxWalkers;

/// <summary>
/// Syntax walker that collects symbols matching a pattern with optional type filtering.
/// Supports wildcard patterns: * matches any sequence, ? matches single character.
/// </summary>
internal class SymbolCollectorWalker : CSharpSyntaxWalker
{
    private readonly Regex _patternRegex;
    private readonly HashSet<string>? _symbolTypes;

    public List<CollectedSymbol> CollectedSymbols { get; } = new();

    /// <summary>
    /// Creates a new SymbolCollectorWalker.
    /// </summary>
    /// <param name="pattern">Wildcard pattern (* and ? supported)</param>
    /// <param name="symbolTypes">Optional set of symbol types to include (class, interface, method, property, field, event, enum, struct, record, delegate)</param>
    /// <param name="ignoreCase">Whether to ignore case when matching</param>
    public SymbolCollectorWalker(string pattern, HashSet<string>? symbolTypes = null, bool ignoreCase = true)
    {
        _patternRegex = WildcardToRegex(pattern, ignoreCase);
        _symbolTypes = symbolTypes;
    }

    /// <summary>
    /// Converts a wildcard pattern to a regex.
    /// * matches any sequence, ? matches single character.
    /// </summary>
    private static Regex WildcardToRegex(string pattern, bool ignoreCase)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var options = RegexOptions.Compiled;
        if (ignoreCase)
            options |= RegexOptions.IgnoreCase;

        return new Regex(regexPattern, options);
    }

    private bool MatchesPattern(string name) => _patternRegex.IsMatch(name);

    private bool ShouldIncludeSymbolType(string kind)
    {
        if (_symbolTypes == null || _symbolTypes.Count == 0)
            return true;
        return _symbolTypes.Contains(kind.ToLowerInvariant());
    }

    private void TryCollect(SyntaxNode node, string name, string kind, SyntaxToken identifier)
    {
        if (MatchesPattern(name) && ShouldIncludeSymbolType(kind))
        {
            CollectedSymbols.Add(new CollectedSymbol(node, name, kind, identifier));
        }
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "class", node.Identifier);
        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "interface", node.Identifier);
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "method", node.Identifier);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "property", node.Identifier);
        base.VisitPropertyDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            TryCollect(node, variable.Identifier.ValueText, "field", variable.Identifier);
        }
        base.VisitFieldDeclaration(node);
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "event", node.Identifier);
        base.VisitEventDeclaration(node);
    }

    public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
    {
        foreach (var variable in node.Declaration.Variables)
        {
            TryCollect(node, variable.Identifier.ValueText, "event", variable.Identifier);
        }
        base.VisitEventFieldDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "enum", node.Identifier);
        base.VisitEnumDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "struct", node.Identifier);
        base.VisitStructDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "record", node.Identifier);
        base.VisitRecordDeclaration(node);
    }

    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        TryCollect(node, node.Identifier.ValueText, "delegate", node.Identifier);
        base.VisitDelegateDeclaration(node);
    }
}

/// <summary>
/// Represents a collected symbol from the walker.
/// </summary>
internal record CollectedSymbol(SyntaxNode Node, string Name, string Kind, SyntaxToken Identifier);
