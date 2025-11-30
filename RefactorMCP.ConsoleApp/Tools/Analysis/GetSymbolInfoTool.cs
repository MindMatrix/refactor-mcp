using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorMCP.ConsoleApp.Models;

namespace RefactorMCP.ConsoleApp.Tools.Analysis;

[McpServerToolType]
public static class GetSymbolInfoTool
{
    [McpServerTool, Description("Get detailed information about a symbol (class, method, property, etc.)")]
    public static async Task<string> GetSymbolInfo(
        [Description("Full or partial symbol name")] string symbolName,
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbolName))
                throw new McpException("Error: Symbol name cannot be empty");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            ISymbol? foundSymbol = null;
            SyntaxNode? declarationNode = null;
            Document? foundDocument = null;

            // Find the symbol definition
            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var document in project.Documents)
                {
                    if (document.FilePath == null || !document.FilePath.EndsWith(".cs"))
                        continue;

                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                    if (root == null || semanticModel == null) continue;

                    // Look for symbol declarations matching the name
                    var node = FindDeclarationByName(root, symbolName);
                    if (node != null)
                    {
                        var symbol = semanticModel.GetDeclaredSymbol(node);
                        if (symbol != null)
                        {
                            foundSymbol = symbol;
                            declarationNode = node;
                            foundDocument = document;
                            break;
                        }
                    }
                }

                if (foundSymbol != null) break;
            }

            if (foundSymbol == null)
                return $"No symbol found with name '{symbolName}'";

            return await FormatSymbolInfo(foundSymbol, declarationNode!, foundDocument!, cancellationToken);
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
            throw new McpException($"Error getting symbol info: {ex.Message}", ex);
        }
    }

    private static SyntaxNode? FindDeclarationByName(SyntaxNode root, string symbolName)
    {
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

    private static async Task<string> FormatSymbolInfo(
        ISymbol symbol,
        SyntaxNode declarationNode,
        Document document,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var lineSpan = declarationNode.GetLocation().GetLineSpan();

        sb.AppendLine($"Symbol: {symbol.Name}");
        sb.AppendLine();
        sb.AppendLine($"Kind: {symbol.Kind}");
        sb.AppendLine($"Accessibility: {symbol.DeclaredAccessibility.ToString().ToLowerInvariant()}");

        if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
            sb.AppendLine($"Namespace: {symbol.ContainingNamespace.ToDisplayString()}");

        if (symbol.ContainingType != null)
            sb.AppendLine($"Containing Type: {symbol.ContainingType.Name}");

        sb.AppendLine($"Source: {Path.GetFileName(document.FilePath)}:{lineSpan.StartLinePosition.Line + 1}");
        sb.AppendLine();

        // Type-specific information
        switch (symbol)
        {
            case INamedTypeSymbol typeSymbol:
                FormatTypeInfo(sb, typeSymbol);
                break;

            case IMethodSymbol methodSymbol:
                FormatMethodInfo(sb, methodSymbol);
                break;

            case IPropertySymbol propertySymbol:
                FormatPropertyInfo(sb, propertySymbol);
                break;

            case IFieldSymbol fieldSymbol:
                FormatFieldInfo(sb, fieldSymbol);
                break;

            case IEventSymbol eventSymbol:
                FormatEventInfo(sb, eventSymbol);
                break;
        }

        // Attributes
        var attributes = symbol.GetAttributes();
        if (attributes.Length > 0)
        {
            sb.AppendLine("Attributes:");
            foreach (var attr in attributes)
            {
                sb.AppendLine($"  - [{attr.AttributeClass?.Name}]");
            }
            sb.AppendLine();
        }

        // XML Documentation
        var xmlDoc = symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(xmlDoc))
        {
            var summary = ExtractSummary(xmlDoc);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                sb.AppendLine("Documentation:");
                sb.AppendLine($"  {summary}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void FormatTypeInfo(StringBuilder sb, INamedTypeSymbol typeSymbol)
    {
        sb.AppendLine($"Type Kind: {typeSymbol.TypeKind}");

        if (typeSymbol.IsStatic)
            sb.AppendLine("Modifiers: static");
        if (typeSymbol.IsAbstract && typeSymbol.TypeKind != TypeKind.Interface)
            sb.AppendLine("Modifiers: abstract");
        if (typeSymbol.IsSealed)
            sb.AppendLine("Modifiers: sealed");

        // Base type
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            sb.AppendLine();
            sb.AppendLine("Base Types:");
            sb.AppendLine($"  - {typeSymbol.BaseType.Name}");
        }

        // Interfaces
        var interfaces = typeSymbol.Interfaces;
        if (interfaces.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Interfaces:");
            foreach (var iface in interfaces)
            {
                sb.AppendLine($"  - {iface.Name}");
            }
        }

        // Members summary
        var members = typeSymbol.GetMembers().Where(m => !m.IsImplicitlyDeclared).ToList();
        var methods = members.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList();
        var properties = members.OfType<IPropertySymbol>().ToList();
        var fields = members.OfType<IFieldSymbol>().ToList();

        if (members.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Members ({members.Count}):");

            if (properties.Any())
            {
                sb.AppendLine($"  Properties ({properties.Count}):");
                foreach (var prop in properties.Take(10))
                {
                    var accessors = new List<string>();
                    if (prop.GetMethod != null) accessors.Add("get");
                    if (prop.SetMethod != null) accessors.Add("set");
                    sb.AppendLine($"    - {prop.Name}: {prop.Type.Name} {{ {string.Join("; ", accessors)} }}");
                }
                if (properties.Count > 10)
                    sb.AppendLine($"    ... and {properties.Count - 10} more");
            }

            if (methods.Any())
            {
                sb.AppendLine($"  Methods ({methods.Count}):");
                foreach (var method in methods.Take(10))
                {
                    var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.Name} {p.Name}"));
                    sb.AppendLine($"    - {method.Name}({parameters}): {method.ReturnType.Name}");
                }
                if (methods.Count > 10)
                    sb.AppendLine($"    ... and {methods.Count - 10} more");
            }

            if (fields.Any())
            {
                sb.AppendLine($"  Fields ({fields.Count}):");
                foreach (var field in fields.Take(10))
                {
                    sb.AppendLine($"    - {field.Name}: {field.Type.Name}");
                }
                if (fields.Count > 10)
                    sb.AppendLine($"    ... and {fields.Count - 10} more");
            }
        }
    }

    private static void FormatMethodInfo(StringBuilder sb, IMethodSymbol methodSymbol)
    {
        // Modifiers
        var modifiers = new List<string>();
        if (methodSymbol.IsStatic) modifiers.Add("static");
        if (methodSymbol.IsVirtual) modifiers.Add("virtual");
        if (methodSymbol.IsOverride) modifiers.Add("override");
        if (methodSymbol.IsAbstract) modifiers.Add("abstract");
        if (methodSymbol.IsAsync) modifiers.Add("async");

        if (modifiers.Any())
            sb.AppendLine($"Modifiers: {string.Join(", ", modifiers)}");

        sb.AppendLine($"Return Type: {methodSymbol.ReturnType.ToDisplayString()}");

        // Parameters
        if (methodSymbol.Parameters.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Parameters:");
            foreach (var param in methodSymbol.Parameters)
            {
                var defaultValue = param.HasExplicitDefaultValue ? $" = {param.ExplicitDefaultValue ?? "null"}" : "";
                var refKind = param.RefKind != RefKind.None ? $"{param.RefKind.ToString().ToLower()} " : "";
                sb.AppendLine($"  - {refKind}{param.Type.ToDisplayString()} {param.Name}{defaultValue}");
            }
        }

        // Generic type parameters
        if (methodSymbol.TypeParameters.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Type Parameters:");
            foreach (var typeParam in methodSymbol.TypeParameters)
            {
                var constraints = new List<string>();
                if (typeParam.HasReferenceTypeConstraint) constraints.Add("class");
                if (typeParam.HasValueTypeConstraint) constraints.Add("struct");
                if (typeParam.HasConstructorConstraint) constraints.Add("new()");
                constraints.AddRange(typeParam.ConstraintTypes.Select(t => t.Name));

                var constraintStr = constraints.Any() ? $" where {typeParam.Name} : {string.Join(", ", constraints)}" : "";
                sb.AppendLine($"  - {typeParam.Name}{constraintStr}");
            }
        }
    }

    private static void FormatPropertyInfo(StringBuilder sb, IPropertySymbol propertySymbol)
    {
        sb.AppendLine($"Type: {propertySymbol.Type.ToDisplayString()}");

        var modifiers = new List<string>();
        if (propertySymbol.IsStatic) modifiers.Add("static");
        if (propertySymbol.IsVirtual) modifiers.Add("virtual");
        if (propertySymbol.IsOverride) modifiers.Add("override");
        if (propertySymbol.IsAbstract) modifiers.Add("abstract");

        if (modifiers.Any())
            sb.AppendLine($"Modifiers: {string.Join(", ", modifiers)}");

        var accessors = new List<string>();
        if (propertySymbol.GetMethod != null)
        {
            var getAccess = propertySymbol.GetMethod.DeclaredAccessibility != propertySymbol.DeclaredAccessibility
                ? $"{propertySymbol.GetMethod.DeclaredAccessibility.ToString().ToLower()} " : "";
            accessors.Add($"{getAccess}get");
        }
        if (propertySymbol.SetMethod != null)
        {
            var setAccess = propertySymbol.SetMethod.DeclaredAccessibility != propertySymbol.DeclaredAccessibility
                ? $"{propertySymbol.SetMethod.DeclaredAccessibility.ToString().ToLower()} " : "";
            var setKind = propertySymbol.SetMethod.IsInitOnly ? "init" : "set";
            accessors.Add($"{setAccess}{setKind}");
        }

        sb.AppendLine($"Accessors: {{ {string.Join("; ", accessors)} }}");
    }

    private static void FormatFieldInfo(StringBuilder sb, IFieldSymbol fieldSymbol)
    {
        sb.AppendLine($"Type: {fieldSymbol.Type.ToDisplayString()}");

        var modifiers = new List<string>();
        if (fieldSymbol.IsStatic) modifiers.Add("static");
        if (fieldSymbol.IsReadOnly) modifiers.Add("readonly");
        if (fieldSymbol.IsConst) modifiers.Add("const");
        if (fieldSymbol.IsVolatile) modifiers.Add("volatile");

        if (modifiers.Any())
            sb.AppendLine($"Modifiers: {string.Join(", ", modifiers)}");

        if (fieldSymbol.HasConstantValue)
            sb.AppendLine($"Constant Value: {fieldSymbol.ConstantValue}");
    }

    private static void FormatEventInfo(StringBuilder sb, IEventSymbol eventSymbol)
    {
        sb.AppendLine($"Type: {eventSymbol.Type.ToDisplayString()}");

        var modifiers = new List<string>();
        if (eventSymbol.IsStatic) modifiers.Add("static");
        if (eventSymbol.IsVirtual) modifiers.Add("virtual");
        if (eventSymbol.IsOverride) modifiers.Add("override");
        if (eventSymbol.IsAbstract) modifiers.Add("abstract");

        if (modifiers.Any())
            sb.AppendLine($"Modifiers: {string.Join(", ", modifiers)}");
    }

    private static string ExtractSummary(string xmlDoc)
    {
        // Simple extraction of summary content
        var startTag = "<summary>";
        var endTag = "</summary>";

        var startIndex = xmlDoc.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        var endIndex = xmlDoc.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var content = xmlDoc.Substring(startIndex + startTag.Length, endIndex - startIndex - startTag.Length);
            return content.Trim().Replace("\n", " ").Replace("\r", "").Replace("  ", " ");
        }

        return string.Empty;
    }
}
