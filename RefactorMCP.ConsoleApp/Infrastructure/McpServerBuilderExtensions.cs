using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Infrastructure;

public static class McpServerBuilderExtensions
{
    private const string RequiresUnreferencedCodeMessage =
        "This method uses reflection to discover tools and may not work correctly with trimming.";

    /// <summary>
    /// Adds tools from the assembly with per-solution locking.
    /// Each tool invocation is wrapped with a lock based on the solutionPath parameter.
    /// </summary>
    [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
    public static IMcpServerBuilder WithToolsFromAssemblyWithLocking(
        this IMcpServerBuilder builder,
        Assembly? toolAssembly = null,
        bool requireSolutionPath = true)
    {
        toolAssembly ??= Assembly.GetCallingAssembly();

        var toolTypes = toolAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        foreach (var toolType in toolTypes)
        {
            var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

            foreach (var method in methods)
            {
                // Check if this method has a solutionPath parameter
                var hasSolutionPath = method.GetParameters()
                    .Any(p => p.Name?.Equals("solutionPath", StringComparison.OrdinalIgnoreCase) == true);

                if (method.IsStatic)
                {
                    builder.Services.AddSingleton<McpServerTool>(services =>
                    {
                        var inner = McpServerTool.Create(
                            method,
                            target: null,
                            options: new McpServerToolCreateOptions { Services = services });

                        // Only wrap with locking if the method has solutionPath
                        if (hasSolutionPath)
                        {
                            return new LockedMcpServerTool(inner, requireSolutionPath);
                        }

                        // For methods without solutionPath, still wrap but don't require it
                        return new LockedMcpServerTool(inner, requireSolutionPath: false);
                    });
                }
                else
                {
                    // Instance methods - need factory for target
                    builder.Services.AddSingleton<McpServerTool>(services =>
                    {
                        var inner = McpServerTool.Create(
                            method,
                            request => ActivatorUtilities.CreateInstance(services, toolType),
                            options: new McpServerToolCreateOptions { Services = services });

                        if (hasSolutionPath)
                        {
                            return new LockedMcpServerTool(inner, requireSolutionPath);
                        }

                        return new LockedMcpServerTool(inner, requireSolutionPath: false);
                    });
                }
            }
        }

        return builder;
    }
}
