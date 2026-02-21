using BlazorClient.Models;
using System.Diagnostics;

namespace BlazorClient.Services.Commands;

/// <summary>
/// Decorator for command dispatcher that adds logging and telemetry.
/// Follows the Decorator pattern for cross-cutting concerns.
/// </summary>
public class LoggingCommandDispatcher : ICommandDispatcher
{
    private readonly ICommandDispatcher _inner;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly List<CommandExecutionLog> _executionHistory;
    private readonly int _maxHistorySize;
    private readonly object _lock = new();

    public LoggingCommandDispatcher(
        ICommandDispatcher inner, 
        IPerformanceMonitor performanceMonitor,
        int maxHistorySize = 100)
    {
        _inner = inner;
        _performanceMonitor = performanceMonitor;
        _maxHistorySize = maxHistorySize;
        _executionHistory = new List<CommandExecutionLog>(maxHistorySize);
    }

    /// <inheritdoc />
    public async Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand
    {
        var commandType = typeof(TCommand).Name;
        var executionId = Guid.NewGuid().ToString("N")[..8];
        
        Console.WriteLine($"[Command:{executionId}] Executing: {commandType}");
        
        var sw = Stopwatch.StartNew();
        Result result;

        try
        {
            result = await _inner.DispatchAsync(command, cancellationToken);
            sw.Stop();

            var status = result.IsSuccess ? "Success" : "Failed";
            var logLevel = result.IsSuccess ? "Info" : "Warning";
            
            Console.WriteLine($"[Command:{executionId}] {status} ({sw.ElapsedMilliseconds}ms): {commandType}");
            
            if (result.IsFailure)
            {
                Console.WriteLine($"[Command:{executionId}] Error: {result.Error}");
            }

            // Record timing if performance monitoring is enabled
            if (_performanceMonitor.DetailedProfilingEnabled)
            {
                _performanceMonitor.RecordTiming($"Command_{commandType}", sw.ElapsedMilliseconds);
            }

            // Log execution history
            LogExecution(new CommandExecutionLog
            {
                ExecutionId = executionId,
                CommandType = commandType,
                Timestamp = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                Success = result.IsSuccess,
                ErrorMessage = result.Error
            });

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.Error.WriteLine($"[Command:{executionId}] Exception ({sw.ElapsedMilliseconds}ms): {commandType}");
            Console.Error.WriteLine($"[Command:{executionId}] {ex.GetType().Name}: {ex.Message}");
            
            LogExecution(new CommandExecutionLog
            {
                ExecutionId = executionId,
                CommandType = commandType,
                Timestamp = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex
            });

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TResult>> DispatchAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand<TResult>
    {
        var commandType = typeof(TCommand).Name;
        var executionId = Guid.NewGuid().ToString("N")[..8];
        
        Console.WriteLine($"[Command:{executionId}] Executing: {commandType}");
        
        var sw = Stopwatch.StartNew();
        Result<TResult> result;

        try
        {
            result = await _inner.DispatchAsync<TCommand, TResult>(command, cancellationToken);
            sw.Stop();

            var status = result.IsSuccess ? "Success" : "Failed";
            
            Console.WriteLine($"[Command:{executionId}] {status} ({sw.ElapsedMilliseconds}ms): {commandType}");
            
            if (result.IsSuccess)
            {
                Console.WriteLine($"[Command:{executionId}] Result: {result.Value}");
            }
            else
            {
                Console.WriteLine($"[Command:{executionId}] Error: {result.Error}");
            }

            // Record timing if performance monitoring is enabled
            if (_performanceMonitor.DetailedProfilingEnabled)
            {
                _performanceMonitor.RecordTiming($"Command_{commandType}", sw.ElapsedMilliseconds);
            }

            // Log execution history
            LogExecution(new CommandExecutionLog
            {
                ExecutionId = executionId,
                CommandType = commandType,
                Timestamp = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                Success = result.IsSuccess,
                ErrorMessage = result.Error,
                ResultValue = result.Value?.ToString()
            });

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.Error.WriteLine($"[Command:{executionId}] Exception ({sw.ElapsedMilliseconds}ms): {commandType}");
            Console.Error.WriteLine($"[Command:{executionId}] {ex.GetType().Name}: {ex.Message}");
            
            LogExecution(new CommandExecutionLog
            {
                ExecutionId = executionId,
                CommandType = commandType,
                Timestamp = DateTime.UtcNow,
                DurationMs = sw.ElapsedMilliseconds,
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex
            });

            throw;
        }
    }

    /// <summary>
    /// Gets the command execution history.
    /// </summary>
    public IReadOnlyList<CommandExecutionLog> GetExecutionHistory()
    {
        lock (_lock)
        {
            return _executionHistory.ToList();
        }
    }

    /// <summary>
    /// Gets statistics about command executions.
    /// </summary>
    public CommandExecutionStats GetStats()
    {
        lock (_lock)
        {
            if (_executionHistory.Count == 0)
            {
                return new CommandExecutionStats();
            }

            var groupedByType = _executionHistory
                .GroupBy(log => log.CommandType)
                .Select(g => new CommandTypeStats
                {
                    CommandType = g.Key,
                    TotalExecutions = g.Count(),
                    SuccessCount = g.Count(l => l.Success),
                    FailureCount = g.Count(l => !l.Success),
                    AverageDurationMs = g.Average(l => l.DurationMs),
                    MinDurationMs = g.Min(l => l.DurationMs),
                    MaxDurationMs = g.Max(l => l.DurationMs)
                })
                .ToList();

            return new CommandExecutionStats
            {
                TotalCommands = _executionHistory.Count,
                SuccessfulCommands = _executionHistory.Count(l => l.Success),
                FailedCommands = _executionHistory.Count(l => !l.Success),
                AverageDurationMs = _executionHistory.Average(l => l.DurationMs),
                CommandTypeStats = groupedByType
            };
        }
    }

    /// <summary>
    /// Clears the execution history.
    /// </summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _executionHistory.Clear();
        }
    }

    private void LogExecution(CommandExecutionLog log)
    {
        lock (_lock)
        {
            _executionHistory.Add(log);

            // Keep only the most recent entries
            if (_executionHistory.Count > _maxHistorySize)
            {
                _executionHistory.RemoveAt(0);
            }
        }
    }
}

/// <summary>
/// Represents a logged command execution.
/// </summary>
public class CommandExecutionLog
{
    public required string ExecutionId { get; init; }
    public required string CommandType { get; init; }
    public DateTime Timestamp { get; init; }
    public long DurationMs { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResultValue { get; init; }
    public Exception? Exception { get; init; }
}

/// <summary>
/// Statistics about command executions.
/// </summary>
public class CommandExecutionStats
{
    public int TotalCommands { get; init; }
    public int SuccessfulCommands { get; init; }
    public int FailedCommands { get; init; }
    public double AverageDurationMs { get; init; }
    public IReadOnlyList<CommandTypeStats> CommandTypeStats { get; init; } = Array.Empty<CommandTypeStats>();

    public double SuccessRate => TotalCommands > 0 ? (double)SuccessfulCommands / TotalCommands * 100 : 0;
}

/// <summary>
/// Statistics for a specific command type.
/// </summary>
public class CommandTypeStats
{
    public required string CommandType { get; init; }
    public int TotalExecutions { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double AverageDurationMs { get; init; }
    public long MinDurationMs { get; init; }
    public long MaxDurationMs { get; init; }

    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 0;
}
