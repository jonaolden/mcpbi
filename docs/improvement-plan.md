# Code Improvement Plan

## Phase 1: Basic Structure Improvements

### 1. Implement ITabularConnection Interface

```csharp
public interface ITabularConnection
{
    Task<IEnumerable<Dictionary<string, object?>>> ExecAsync(string dax);
    Task<IEnumerable<Dictionary<string, object?>>> ExecInfoAsync(string func, string? filterExpr);
    Task<IEnumerable<Dictionary<string, object?>>> ExecDaxAsync(string dax);
}

public class TabularConnection : ITabularConnection
{
    private readonly IConfiguration _configuration;

    public TabularConnection(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetConnectionString()
    {
        var port = _configuration["PBI_PORT"] 
            ?? throw new InvalidOperationException("PBI_PORT not set");
        var db = _configuration["PBI_DB_ID"]
            ?? throw new InvalidOperationException("PBI_DB_ID not set");
        return $"Data Source=localhost:{port};Initial Catalog={db};Integrated Security=SSPI;Provider=MSOLAP;";
    }

    // Existing implementation methods...
}
```

### 2. Tools Class Refactoring

```csharp
public class ToolService
{
    private readonly ITabularConnection _tabular;
    private readonly ILogger<ToolService> _logger;

    public ToolService(ITabularConnection tabular, ILogger<ToolService> logger)
    {
        _tabular = tabular;
        _logger = logger;
    }

    private async Task<object> Safe(Func<Task<IEnumerable<Dictionary<string, object?>>>> op,
                                  [CallerMemberName] string member = "")
    {
        try
        {
            return await op();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Member}", member);
            return new { error = GetErrorMessage(ex) };
        }
    }

    private string GetErrorMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException _ => "InsufficientPermissions",
        AdomdErrorResponseException aex when IsSyntaxError(aex) => $"InvalidDaxFilterExpression: {aex.Message}",
        AdomdErrorResponseException aex => $"DaxExecutionError: {aex.Message}",
        AdomdException aex => $"AdomdClientError: {aex.Message}",
        Exception _ when IsSyntaxError(ex) => $"InvalidDaxFilterExpression: {ex.Message}",
        _ => $"UnexpectedError: {ex.Message}"
    };
}
```

### 3. Basic Configuration

```csharp
public class PowerBiConfig
{
    public string Port { get; set; } = string.Empty;
    public string DbId { get; set; } = string.Empty;
}

// Program.cs
builder.Services.Configure<PowerBiConfig>(builder.Configuration.GetSection("PowerBi"));
```

## Phase 2: Code Quality Improvements

### 1. Break Down SelectFromList Method

```csharp
public static class ConsoleUI
{
    private class ConsoleState
    {
        public int InitialTop { get; set; }
        public int Selected { get; set; }
        public bool IsCancelled { get; set; }
    }

    public static int SelectFromList(string prompt, List<string> items)
    {
        if (!items.Any()) 
        {
            Console.WriteLine("No items to select.");
            return -1;
        }

        var state = InitializeSelection(prompt);
        return HandleSelectionLoop(items, state);
    }

    private static ConsoleState InitializeSelection(string prompt)
    {
        Console.WriteLine(prompt);
        Console.CursorVisible = false;
        return new ConsoleState 
        { 
            InitialTop = Console.CursorTop,
            Selected = 0
        };
    }

    private static int HandleSelectionLoop(List<string> items, ConsoleState state)
    {
        try
        {
            while (!state.IsCancelled)
            {
                DrawItems(items, state);
                if (HandleKeyPress(items, state))
                    break;
            }
            return state.IsCancelled ? -1 : state.Selected;
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }
}
```

### 2. String Extension Methods

```csharp
public static class StringExtensions
{
    public static string EscapeQuotes(this string value) 
        => value.Replace("\"", "\"\"");

    public static string WrapInQuotes(this string value) 
        => $"\"{value.EscapeQuotes()}\"";
}
```

## Phase 3: Error Handling

### 1. Custom Exceptions

```csharp
public class ConfigurationMissingException : Exception
{
    public ConfigurationMissingException(string key)
        : base($"Required configuration '{key}' is missing")
    {
        Key = key;
    }

    public string Key { get; }
}

public class DaxQueryException : Exception
{
    public DaxQueryException(string message, string query)
        : base(message)
    {
        Query = query;
    }

    public string Query { get; }
}
```

### 2. Logging Setup

```csharp
// Program.cs
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opt => 
{
    opt.LogToStandardErrorThreshold = LogLevel.Warning;
    opt.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
});
```

## Implementation Steps

1. Create new interfaces and configuration classes
2. Refactor TabularConnection to implement ITabularConnection
3. Convert Tools to ToolService with dependency injection
4. Update Program.cs with service registration
5. Implement string extensions and helper methods
6. Add custom exceptions and centralized error handling
7. Configure logging

## Testing Approach

1. Unit test core functionality
2. Integration tests for database operations
3. Error handling verification
4. Configuration validation tests

Would you like me to proceed with implementing these changes? We can take them step by step, starting with the most impactful changes first.