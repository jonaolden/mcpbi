# Feats

This document lists the feature work proposed in the development plan.

## Lineage Tracing

A tool to obtain full DAX from implicit measures:

```csharp
[McpServerTool, Description("Get full DAX from implicit measures.")]
public static async Task<object> GetImplicitMeasureDAX(string tableName, string columnName)
{
    // Implementation details omitted for brevity
}
```

## Measure Dependency Analysis

```csharp
[McpServerTool, Description("Analyze measure dependencies.")]
public static async Task<object> GetMeasureDependencies(string measureName)
{
    // Query dependencies using DMV
}
```

Additional placeholder to detect unused objects is also planned.

## Performance Analysis Tool

A tool that measures the execution time of a DAX expression.

```csharp
[McpServerTool, Description("Analyze query performance for a DAX expression.")]
public static async Task<object> AnalyzeQueryPerformance(string dax)
{
    // Use server timing and profiling
}
```

## Model Health Check

A diagnostic utility intended to surface potential issues with the model structure.
