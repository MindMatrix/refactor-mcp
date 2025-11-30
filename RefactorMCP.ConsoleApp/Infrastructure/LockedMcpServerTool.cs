using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RefactorMCP.ConsoleApp.Infrastructure;

/// <summary>
/// Decorator that wraps an McpServerTool with per-solution locking.
/// </summary>
public class LockedMcpServerTool : McpServerTool
{
    private readonly McpServerTool _inner;
    private readonly bool _requireSolutionPath;

    public LockedMcpServerTool(McpServerTool inner, bool requireSolutionPath = true)
    {
        _inner = inner;
        _requireSolutionPath = requireSolutionPath;
    }

    public override Tool ProtocolTool => _inner.ProtocolTool;

    public override IReadOnlyList<object> Metadata => _inner.Metadata;

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        // Extract solutionPath from arguments
        string? solutionPath = null;

        if (request.Params?.Arguments != null)
        {
            foreach (var kvp in request.Params.Arguments)
            {
                if (kvp.Key.Equals("solutionPath", StringComparison.OrdinalIgnoreCase))
                {
                    solutionPath = kvp.Value.ToString();
                    break;
                }
            }
        }

        // Sanity check - require solutionPath for now
        if (_requireSolutionPath && string.IsNullOrEmpty(solutionPath))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"Error: Tool '{ProtocolTool.Name}' requires 'solutionPath' parameter for locking. This is a sanity check - remove after testing." }]
            };
        }

        // If no solutionPath, just invoke without locking
        if (string.IsNullOrEmpty(solutionPath))
        {
            return await _inner.InvokeAsync(request, cancellationToken);
        }

        // Invoke with lock
        return await SolutionLock.WithLockAsync(solutionPath, async () =>
        {
            return await _inner.InvokeAsync(request, cancellationToken);
        });
    }
}
