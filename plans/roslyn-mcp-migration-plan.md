# RoslynMCP Feature Migration Plan

## Executive Summary

This plan outlines the migration of analyzer features from the [RoslynMCP](https://github.com/carquiza/RoslynMCP) project into RefactorMCP. The goal is to add "analyzer concepts for finding what we might want to refactor" - capabilities that identify code patterns, complexity issues, and symbol relationships that suggest refactoring opportunities.

**Key Insight**: RoslynMCP provides analysis/discovery capabilities (finding what to refactor), while RefactorMCP provides transformation capabilities (performing the refactoring). Combining these creates a complete refactoring workflow.

---

## Source Analysis

### RoslynMCP Structure
```
submodules/RoslynMCP/RoslynMcpServer/
├── Program.cs                    # Entry point & DI setup
├── Tools/
│   └── CodeNavigationTools.cs    # MCP tool implementations
├── Services/
│   ├── SymbolSearchService.cs    # Core symbol searching
│   ├── CacheManager.cs           # Multi-level caching
│   ├── IncrementalAnalyzer.cs    # Incremental analysis
│   ├── SecurityValidator.cs      # Path validation & security
│   └── DiagnosticLogger.cs       # Logging wrapper
└── Models/
    └── SearchModels.cs           # Data models
```

### Features to Migrate

| Feature | Source File | Priority | Complexity |
|---------|-------------|----------|------------|
| Symbol Search (wildcard patterns) | `SymbolSearchService.cs` | High | Medium |
| Find References | `SymbolSearchService.cs` | High | Medium |
| Get Symbol Info | `SymbolSearchService.cs` | High | Low |
| Cyclomatic Complexity Analysis | `IncrementalAnalyzer.cs` | High | Low |
| Dependency Analysis | `CodeNavigationTools.cs` | Medium | Medium |
| Multi-Level Caching | `CacheManager.cs` | Medium | Medium |
| Incremental Analysis | `IncrementalAnalyzer.cs` | Low | Medium |
| Security Validation | `SecurityValidator.cs` | Low | Low |

### Features NOT to Migrate

| Feature | Reason |
|---------|--------|
| Program.cs DI setup | RefactorMCP has its own DI/hosting |
| MCP tool decorators | Different registration system |
| Output formatting | RefactorMCP has established patterns |
| DiagnosticLogger | RefactorMCP has ToolCallLogger |

### Critical Gap in Source

`CodeAnalysisService` is **referenced but not implemented** in RoslynMCP:
- `SymbolSearchService.cs:17` references `_codeAnalysis`
- `CodeNavigationTools.cs:159,238` calls `GetService<CodeAnalysisService>()`

This means we need to implement solution loading ourselves (which RefactorMCP already has via `RefactoringHelpers`).

---

## Target Architecture

### Proposed RefactorMCP Structure

```
RefactorMCP.ConsoleApp/
├── Tools/
│   ├── Analysis/                    # NEW: Analyzer tools folder
│   │   ├── SearchSymbolsTool.cs     # Symbol search by pattern
│   │   ├── FindReferencesTool.cs    # Find all references
│   │   ├── GetSymbolInfoTool.cs     # Detailed symbol info
│   │   ├── AnalyzeComplexityTool.cs # Cyclomatic complexity
│   │   └── AnalyzeDependenciesTool.cs # Project dependencies
│   └── ... (existing tools)
├── SyntaxWalkers/
│   ├── SymbolCollectorWalker.cs     # NEW: Collect symbols by pattern
│   └── ... (existing walkers)
├── Services/                         # NEW: Services folder
│   └── AnalysisCacheManager.cs      # Multi-level caching
└── Models/                           # NEW: Models folder
    └── AnalysisModels.cs            # Search/analysis data models
```

### Integration with Existing Infrastructure

The migration will leverage existing RefactorMCP infrastructure:

| RoslynMCP | RefactorMCP Equivalent |
|-----------|----------------------|
| `CodeAnalysisService.GetSolutionAsync()` | `RefactoringHelpers.GetOrLoadSolution()` |
| `DiagnosticLogger` | `ToolCallLogger` |
| File-based cache in `cache/` | Can use `.refactor-mcp/` directory |
| MSBuild registration | `RefactoringHelpers.EnsureMsBuildRegistered()` |

---

## Migration Phases

### Phase 1: Data Models & Infrastructure

**Goal**: Create foundational models and services without external dependencies.

#### 1.1 Create Analysis Models

**File**: `RefactorMCP.ConsoleApp/Models/AnalysisModels.cs`

```csharp
// Models to create:
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
    public List<string>? Attributes { get; init; }
    public string? Documentation { get; init; }
    public string? SourceLocation { get; init; }
}

public record ParameterInfo(string Name, string Type, string? DefaultValue);

public record ComplexityResult
{
    public required string MethodName { get; init; }
    public required string FileName { get; init; }
    public required int LineNumber { get; init; }
    public required int Complexity { get; init; }
    public string? ClassName { get; init; }
    public string? Namespace { get; init; }
}

public record DependencyAnalysis
{
    public required string ProjectName { get; init; }
    public required List<DependencyInfo> Dependencies { get; init; }
    public required Dictionary<string, int> NamespaceUsages { get; init; }
    public int TotalSymbols { get; init; }
    public int PublicSymbols { get; init; }
    public int InternalSymbols { get; init; }
}

public record DependencyInfo(string Name, string? Version, string Type, int UsageCount);
```

#### 1.2 Create Analysis Cache Manager (Optional)

**File**: `RefactorMCP.ConsoleApp/Services/AnalysisCacheManager.cs`

Multi-level caching with:
- L1: In-memory (fast, short TTL)
- L2: File-based persistent (slow, long TTL)

**Decision**: May not need initially - RefactorMCP already has `SolutionCache` and `MetricsCache`. Can add later if performance requires.

---

### Phase 2: Symbol Search Tool

**Goal**: Implement wildcard-based symbol search across solutions.

#### 2.1 Create Symbol Collector Walker

**File**: `RefactorMCP.ConsoleApp/SyntaxWalkers/SymbolCollectorWalker.cs`

```csharp
internal class SymbolCollectorWalker : CSharpSyntaxWalker
{
    private readonly string _pattern;
    private readonly HashSet<string>? _symbolTypes;  // class, method, property, field, etc.
    private readonly bool _ignoreCase;

    public List<(SyntaxNode Node, string Name, string Kind)> CollectedSymbols { get; } = new();

    // Override Visit methods for:
    // - ClassDeclarationSyntax
    // - InterfaceDeclarationSyntax
    // - MethodDeclarationSyntax
    // - PropertyDeclarationSyntax
    // - FieldDeclarationSyntax
    // - EventDeclarationSyntax
    // - EnumDeclarationSyntax
    // - StructDeclarationSyntax
    // - RecordDeclarationSyntax
    // - DelegateDeclarationSyntax
}
```

**Pattern Matching**: Implement `WildcardMatch(pattern, name)` method:
- `*` matches any sequence
- `?` matches single character
- Convert to regex: `*` -> `.*`, `?` -> `.`

#### 2.2 Create Search Symbols Tool

**File**: `RefactorMCP.ConsoleApp/Tools/Analysis/SearchSymbolsTool.cs`

```csharp
[McpServerToolType]
public static class SearchSymbolsTool
{
    [McpServerTool]
    [Description("Search for symbols matching a pattern across the solution")]
    public static async Task<string> SearchSymbols(
        [Description("Pattern with wildcards (* and ?). Examples: '*Service', 'Get*User'")]
        string pattern,
        [Description("Path to the solution file")]
        string solutionPath,
        [Description("Symbol types to search: class, interface, method, property, field, event (comma-separated). Leave empty for all.")]
        string? symbolTypes = null,
        [Description("Ignore case when matching")]
        bool ignoreCase = true,
        CancellationToken cancellationToken = default)
    {
        // Implementation:
        // 1. Load solution via RefactoringHelpers.GetOrLoadSolution()
        // 2. For each project with compilation support
        // 3. For each document, parse and walk with SymbolCollectorWalker
        // 4. Collect and format results
        // 5. Return grouped by category
    }
}
```

**Output Format**:
```
Found 15 symbols matching '*Service':

Classes (5):
  - UserService [RefactorMCP.ConsoleApp/Services/UserService.cs:10]
  - AuthenticationService [RefactorMCP.ConsoleApp/Services/AuthService.cs:15]
  ...

Interfaces (3):
  - IUserService [RefactorMCP.ConsoleApp/Services/IUserService.cs:5]
  ...

Methods (7):
  - GetUserService [RefactorMCP.ConsoleApp/Controllers/UserController.cs:25]
  ...
```

---

### Phase 3: Find References Tool

**Goal**: Find all references to a symbol across the solution.

#### 3.1 Create Find References Tool

**File**: `RefactorMCP.ConsoleApp/Tools/Analysis/FindReferencesTool.cs`

```csharp
[McpServerToolType]
public static class FindReferencesTool
{
    [McpServerTool]
    [Description("Find all references to a symbol in the solution")]
    public static async Task<string> FindReferences(
        [Description("Full or partial symbol name to find references for")]
        string symbolName,
        [Description("Path to the solution file")]
        string solutionPath,
        [Description("Include the symbol definition in results")]
        bool includeDefinition = true,
        [Description("Number of context lines to show around each reference")]
        int contextLines = 2,
        CancellationToken cancellationToken = default)
    {
        // Implementation:
        // 1. Load solution
        // 2. Find symbol definition using semantic model
        // 3. Use Roslyn's SymbolFinder.FindReferencesAsync()
        // 4. Format results with context
    }
}
```

**Key Roslyn APIs**:
```csharp
// Find symbol in solution
var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);

// Find all references
var references = await SymbolFinder.FindReferencesAsync(symbol, solution);

// Get reference locations
foreach (var reference in references)
{
    foreach (var location in reference.Locations)
    {
        var document = solution.GetDocument(location.Document.Id);
        var span = location.Location.SourceSpan;
        // Extract line text and context
    }
}
```

**Output Format**:
```
Found 12 references to 'UserService':

Definition:
  RefactorMCP.ConsoleApp/Services/UserService.cs:10
    public class UserService : IUserService

References:
  RefactorMCP.ConsoleApp/Controllers/UserController.cs:25
    private readonly UserService _userService;

  RefactorMCP.ConsoleApp/Startup.cs:45
    services.AddScoped<UserService>();

  ...
```

---

### Phase 4: Get Symbol Info Tool

**Goal**: Get detailed information about a specific symbol.

#### 4.1 Create Get Symbol Info Tool

**File**: `RefactorMCP.ConsoleApp/Tools/Analysis/GetSymbolInfoTool.cs`

```csharp
[McpServerToolType]
public static class GetSymbolInfoTool
{
    [McpServerTool]
    [Description("Get detailed information about a symbol")]
    public static async Task<string> GetSymbolInfo(
        [Description("Full or partial symbol name")]
        string symbolName,
        [Description("Path to the solution file")]
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        // Implementation:
        // 1. Load solution
        // 2. Search for symbol
        // 3. Use semantic model to get detailed info
        // 4. Extract documentation, parameters, return type, etc.
    }
}
```

**Information to Extract**:
- Symbol kind (class, method, property, etc.)
- Accessibility (public, private, internal, etc.)
- Containing type and namespace
- For methods: parameters, return type, generic constraints
- For properties: type, accessors
- For classes: base types, implemented interfaces
- XML documentation
- Attributes
- Source location

**Output Format**:
```
Symbol: UserService

Kind: Class
Accessibility: public
Namespace: RefactorMCP.ConsoleApp.Services
Source: RefactorMCP.ConsoleApp/Services/UserService.cs:10

Base Types:
  - object

Interfaces:
  - IUserService

Members (12):
  - ctor()
  - GetUserAsync(int userId) : Task<User>
  - CreateUserAsync(UserDto dto) : Task<User>
  ...

Documentation:
  Service for managing user operations.
```

---

### Phase 5: Complexity Analysis Tool

**Goal**: Identify high-complexity methods as refactoring candidates.

#### 5.1 Enhance Existing Complexity Walker

RefactorMCP already has `ComplexityWalker` in `SyntaxWalkers/`. Verify it calculates:
- Base complexity = 1
- +1 per: if, while, for, foreach, switch, catch
- +1 per: && and || operators

If not complete, enhance it.

#### 5.2 Create Analyze Complexity Tool

**File**: `RefactorMCP.ConsoleApp/Tools/Analysis/AnalyzeComplexityTool.cs`

```csharp
[McpServerToolType]
public static class AnalyzeComplexityTool
{
    [McpServerTool]
    [Description("Analyze cyclomatic complexity of methods in the solution")]
    public static async Task<string> AnalyzeComplexity(
        [Description("Path to the solution file")]
        string solutionPath,
        [Description("Only report methods with complexity >= threshold")]
        int threshold = 7,
        [Description("Maximum number of results to return")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // Implementation:
        // 1. Load solution
        // 2. For each document, analyze method complexity
        // 3. Filter by threshold
        // 4. Sort by complexity descending
        // 5. Return top N results
    }
}
```

**Output Format**:
```
Found 15 methods with complexity >= 7:

High Complexity (>10):
  1. ProcessOrderAsync (complexity: 18)
     RefactorMCP.ConsoleApp/Services/OrderService.cs:145
     Suggestion: Consider extracting logic into smaller methods

  2. ValidateUserInput (complexity: 14)
     RefactorMCP.ConsoleApp/Validators/UserValidator.cs:30
     ...

Medium Complexity (7-10):
  3. HandleAuthentication (complexity: 9)
     ...
```

---

### Phase 6: Dependency Analysis Tool

**Goal**: Analyze project dependencies and namespace usage.

#### 6.1 Create Analyze Dependencies Tool

**File**: `RefactorMCP.ConsoleApp/Tools/Analysis/AnalyzeDependenciesTool.cs`

```csharp
[McpServerToolType]
public static class AnalyzeDependenciesTool
{
    [McpServerTool]
    [Description("Analyze project dependencies and namespace usage")]
    public static async Task<string> AnalyzeDependencies(
        [Description("Path to the solution file")]
        string solutionPath,
        [Description("Project name to analyze (leave empty for all)")]
        string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        // Implementation:
        // 1. Load solution
        // 2. For each project, analyze:
        //    - Project references
        //    - Package references
        //    - Namespace usages
        // 3. Calculate dependency counts
        // 4. Format results
    }
}
```

**Output Format**:
```
Dependency Analysis for: RefactorMCP.ConsoleApp

Project References (2):
  - RefactorMCP.Core [used in 15 files]
  - RefactorMCP.Models [used in 8 files]

Package References (5):
  - Microsoft.CodeAnalysis (4.14.0) [45 usages]
  - ModelContextProtocol (0.2.0-preview.3) [12 usages]
  ...

Most Used Namespaces:
  - System.Linq (78 usages)
  - Microsoft.CodeAnalysis.CSharp (45 usages)
  ...

Symbol Distribution:
  - Total: 234 symbols
  - Public: 156 (67%)
  - Internal: 78 (33%)
```

---

## Test Migration Plan

### Source Tests to Replicate

Review tests in `submodules/RoslynMCP/` and create equivalents:

| Source Test | Target Test File |
|-------------|------------------|
| Symbol search tests | `RefactorMCP.Tests/ToolsNew/SearchSymbolsToolTests.cs` |
| Find references tests | `RefactorMCP.Tests/ToolsNew/FindReferencesToolTests.cs` |
| Symbol info tests | `RefactorMCP.Tests/ToolsNew/GetSymbolInfoToolTests.cs` |
| Complexity tests | `RefactorMCP.Tests/ToolsNew/AnalyzeComplexityToolTests.cs` |
| Dependency tests | `RefactorMCP.Tests/ToolsNew/AnalyzeDependenciesToolTests.cs` |

### Test Patterns to Follow

Based on RefactorMCP's existing test patterns:

```csharp
public class SearchSymbolsToolTests : TestBase
{
    [Fact]
    public async Task SearchSymbols_WildcardPattern_FindsMatches()
    {
        // Arrange
        const string testCode = """
            public class UserService { }
            public class AuthService { }
            public class ProductRepository { }
            """;

        await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        var testFile = Path.Combine(TestOutputPath, "Test.cs");
        await TestUtilities.CreateTestFile(testFile, testCode);

        // Act
        var result = await SearchSymbolsTool.SearchSymbols(
            "*Service", SolutionPath, "class", true, CancellationToken.None);

        // Assert
        Assert.Contains("UserService", result);
        Assert.Contains("AuthService", result);
        Assert.DoesNotContain("ProductRepository", result);
    }

    [Fact]
    public async Task SearchSymbols_NoMatches_ReturnsEmptyMessage()
    {
        // ...
    }

    [Fact]
    public async Task SearchSymbols_InvalidPattern_ThrowsException()
    {
        // ...
    }
}
```

### Test Scenarios per Tool

#### SearchSymbolsTool Tests
1. Wildcard `*` matches any sequence
2. Wildcard `?` matches single character
3. Case-insensitive matching
4. Case-sensitive matching
5. Filter by symbol type (class, method, property)
6. Multiple symbol types
7. No matches returns appropriate message
8. Invalid pattern handling
9. Empty solution handling
10. Large solution performance

#### FindReferencesTool Tests
1. Find references to class
2. Find references to method
3. Find references to property
4. Include/exclude definition
5. Context lines extraction
6. Symbol not found handling
7. Ambiguous symbol handling
8. Cross-project references

#### GetSymbolInfoTool Tests
1. Class information extraction
2. Method information with parameters
3. Property information with accessors
4. Interface information
5. Generic type information
6. XML documentation extraction
7. Attribute extraction
8. Symbol not found handling

#### AnalyzeComplexityTool Tests
1. Simple method (complexity 1)
2. Method with if statements
3. Method with loops
4. Method with logical operators
5. Threshold filtering
6. Sorting by complexity
7. Limit results
8. Empty solution handling

#### AnalyzeDependenciesTool Tests
1. Project reference detection
2. Package reference detection
3. Namespace usage counting
4. Symbol distribution calculation
5. Single project analysis
6. Multi-project solution

---

## Implementation Order

### Recommended Sequence

```
Week 1: Foundation
├── 1.1 Create Models/AnalysisModels.cs
├── 1.2 Create SyntaxWalkers/SymbolCollectorWalker.cs
└── 1.3 Create Tests infrastructure

Week 2: Symbol Search
├── 2.1 Implement SearchSymbolsTool
├── 2.2 Write SearchSymbolsToolTests
└── 2.3 Verify and refine

Week 3: References & Info
├── 3.1 Implement FindReferencesTool
├── 3.2 Implement GetSymbolInfoTool
├── 3.3 Write tests for both
└── 3.4 Verify and refine

Week 4: Analysis Tools
├── 4.1 Enhance ComplexityWalker if needed
├── 4.2 Implement AnalyzeComplexityTool
├── 4.3 Implement AnalyzeDependenciesTool
├── 4.4 Write tests for both
└── 4.5 Final verification

Post-Migration:
├── Remove submodule (optional)
├── Update documentation
└── Add examples to CLAUDE.md
```

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| RoslynMCP CodeAnalysisService missing | Confirmed | Low | Use RefactoringHelpers.GetOrLoadSolution() |
| Performance on large solutions | Medium | Medium | Leverage existing caching, add incremental analysis later |
| Pattern matching edge cases | Medium | Low | Comprehensive test coverage |
| Roslyn API version differences | Low | Medium | Pin to compatible versions |
| MCP attribute compatibility | Low | Low | Follow existing tool patterns |

---

## Success Criteria

### Feature Parity
- [ ] All 6 core features implemented
- [ ] Pattern matching works for wildcards
- [ ] Reference finding across projects works
- [ ] Complexity calculation matches expectations
- [ ] Dependency analysis is accurate

### Test Coverage
- [ ] All tools have corresponding test files
- [ ] Each test scenario from source replicated
- [ ] Edge cases covered
- [ ] Performance tests for large solutions

### Integration
- [ ] Tools discoverable via MCP server
- [ ] JSON mode works for all tools
- [ ] Output formatting consistent with existing tools
- [ ] Error handling follows project patterns

### Documentation
- [ ] CLAUDE.md updated with new tools
- [ ] Tool descriptions accurate
- [ ] Examples provided

---

## Cleanup Tasks

After successful migration:

1. **Remove Submodule** (optional - keep for reference):
   ```bash
   git submodule deinit submodules/RoslynMCP
   git rm submodules/RoslynMCP
   rm -rf .git/modules/submodules/RoslynMCP
   ```

2. **Update .gitmodules** if keeping other submodules

3. **Update README** with new analysis capabilities

4. **Update CLAUDE.md** with new tool documentation

---

## Appendix: Source File Reference

### Files to Review During Implementation

| Source File | Lines | Key Functions |
|-------------|-------|---------------|
| `SymbolSearchService.cs` | ~120 | `SearchSymbolsAsync`, `FindReferencesAsync`, `GetSymbolInfoAsync` |
| `IncrementalAnalyzer.cs` | ~100 | `CalculateCyclomaticComplexity`, batch processing |
| `CodeNavigationTools.cs` | ~150 | Tool signatures, output formatting |
| `SearchModels.cs` | ~80 | Data model definitions |
| `CacheManager.cs` | ~100 | Multi-level cache pattern |

### RefactorMCP Files to Reference

| File | Purpose |
|------|---------|
| `RefactoringHelpers.cs` | Solution loading, caching patterns |
| `ExtractMethodTool.cs` | Tool implementation pattern |
| `MethodMetricsWalker.cs` | Walker pattern |
| `TestBase.cs` | Test setup pattern |
| `ExtractMethodToolTests.cs` | Test implementation pattern |
