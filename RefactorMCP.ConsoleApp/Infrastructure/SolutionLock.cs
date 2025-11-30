using System.Collections.Concurrent;

namespace RefactorMCP.ConsoleApp.Infrastructure;

/// <summary>
/// Provides per-solution locking to prevent concurrent modifications.
/// </summary>
public static class SolutionLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <summary>
    /// Executes an action with an exclusive lock on the specified solution.
    /// </summary>
    public static async Task<T> WithLockAsync<T>(string solutionPath, Func<Task<T>> action)
    {
        var normalizedPath = NormalizePath(solutionPath);
        var semaphore = _locks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Executes an action with an exclusive lock on the specified solution.
    /// </summary>
    public static async Task WithLockAsync(string solutionPath, Func<Task> action)
    {
        var normalizedPath = NormalizePath(solutionPath);
        var semaphore = _locks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).ToLowerInvariant();
    }
}
