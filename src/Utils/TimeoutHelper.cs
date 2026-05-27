using System;
using System.Threading;
using System.Threading.Tasks;

namespace ObeliskLauncher.Utils;

/// <summary>
/// Helper for adding timeout support to async operations with cancellation tokens.
/// </summary>
public static class TimeoutHelper
{
    /// <summary>
    /// Wraps a Task with a timeout. Returns true if completed, false if timed out.
    /// </summary>
    public static async Task<bool> ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        int timeoutMs = 30000,
        string? operationName = null)
    {
        using (var cts = new CancellationTokenSource(timeoutMs))
        {
            try
            {
                await operation(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                var name = operationName ?? "Operation";
                LauncherLog.Warning($"{name} timed out after {timeoutMs}ms");
                return false;
            }
            catch (Exception ex)
            {
                LauncherLog.Error(ex, $"Error during {operationName ?? "operation"}: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Wraps a Task<T> with a timeout. Returns (success, result).
    /// </summary>
    public static async Task<(bool Success, T? Result)> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int timeoutMs = 30000,
        string? operationName = null)
    {
        using (var cts = new CancellationTokenSource(timeoutMs))
        {
            try
            {
                var result = await operation(cts.Token);
                return (true, result);
            }
            catch (OperationCanceledException)
            {
                var name = operationName ?? "Operation";
                LauncherLog.Warning($"{name} timed out after {timeoutMs}ms");
                return (false, default);
            }
            catch (Exception ex)
            {
                LauncherLog.Error(ex, $"Error during {operationName ?? "operation"}: {ex.Message}");
                return (false, default);
            }
        }
    }
}
