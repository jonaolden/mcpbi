# Final Tool Specifications - Development Handoff

**Date:** 2025-01-25  
**Status:** Ready for Development  
**Scope:** 4 Core Tools for Maximum User Value  

## Executive Summary

Based on user experience analysis, we've focused on **4 high-value tools** that solve real daily pain points rather than comprehensive analysis scenarios. These tools fill obvious gaps and improve existing workflows without duplicating Power BI Desktop functionality.

## Core Tools - APPROVED FOR DEVELOPMENT ✅

### **Tool 1: GetCalculatedColumns() - PRIORITY 1**

**User Pain Point:** No easy way to see all calculated columns across tables  
**Current Workflow:** Manual clicking through each table in Power BI Desktop  
**Effort Estimate:** 4-6 hours

#### Tool Signature
```csharp
[McpServerTool, Description("List all calculated columns with expressions and dependencies.")]
public async Task<object> GetCalculatedColumns(
    [Description("Optional table name to filter calculated columns")] string? tableName = null,
    [Description("Include DAX expressions in results")] bool includeExpressions = true)
```

#### Implementation Code
```csharp
public async Task<object> GetCalculatedColumns(string? tableName = null, bool includeExpressions = true)
{
    try
    {
        // Validate table name if provided
        if (!string.IsNullOrEmpty(tableName) && !DaxSecurityUtils.IsValidIdentifier(tableName))
            throw new ArgumentException("Invalid table name format", nameof(tableName));

        // Base DMV query for calculated columns (TYPE = 2)
        var dmvQuery = "SELECT [TableID], [Name], [Expression], [DataType], [IsHidden] " +
                      "FROM $SYSTEM.TMSCHEMA_COLUMNS WHERE [TYPE] = 2";
        
        // Add table filter if specified
        if (!string.IsNullOrEmpty(tableName))
        {
            var escapedTableName = DaxSecurityUtils.EscapeDaxIdentifier(tableName);
            dmvQuery += $" AND [TableID] IN (SELECT [ID] FROM $SYSTEM.TMSCHEMA_TABLES WHERE [Name] = {escapedTableName})";
        }

        dmvQuery += " ORDER BY [TableID], [Name]";

        var columns = await _connection.ExecAsync(dmvQuery, QueryType.DMV);
        
        // Get table names for better output
        var tablesQuery = "SELECT [ID], [Name] FROM $SYSTEM.TMSCHEMA_TABLES";
        var tables = await _connection.ExecAsync(tablesQuery, QueryType.DMV);
        var tableDict = tables.ToDictionary(t => t["ID"]?.ToString() ?? "", t => t["Name"]?.ToString() ?? "");

        var result = columns.Select(col => new
        {
            TableName = tableDict.GetValueOrDefault(col["TableID"]?.ToString() ?? "", "Unknown"),
            ColumnName = col["Name"]?.ToString() ?? "",
            DataType = col["DataType"]?.ToString() ?? "",
            IsHidden = bool.Parse(col["IsHidden"]?.ToString() ?? "false"),
            Expression = includeExpressions ? col["Expression"]?.ToString() ?? "" : "[Hidden]"
        }).ToList();

        return new
        {
            CalculatedColumnsCount = result.Count,
            FilteredBy = tableName ?? "All Tables",
            Columns = result
        };
    }
    catch (Exception ex)
    {
        throw new DaxQueryExecutionException($"Failed to get calculated columns: {ex.Message}", ex);
    }
}
```

---

### **Tool 2: AnalyzeMeasureDependencies() - PRIORITY 1**

**User Pain Point:** "What breaks if I change this measure?" - No dependency tracking  
**Current Workflow:** Manual searching through expressions - very time consuming  
**Effort Estimate:** 10-12 hours

#### Tool Signature
```csharp
[McpServerTool, Description("Analyze measure dependencies and identify circular references.")]
public async Task<object> AnalyzeMeasureDependencies(
    [Description("Optional measure name to analyze specific dependencies")] string? measureName = null,
    [Description("Maximum dependency depth to analyze")] int maxDepth = 5)
```

#### Implementation Code
```csharp
public async Task<object> AnalyzeMeasureDependencies(string? measureName = null, int maxDepth = 5)
{
    try
    {
        // Validate inputs
        if (!string.IsNullOrEmpty(measureName) && !DaxSecurityUtils.IsValidIdentifier(measureName))
            throw new ArgumentException("Invalid measure name format", nameof(measureName));

        if (maxDepth < 1 || maxDepth > 10)
            throw new ArgumentException("Max depth must be between 1 and 10", nameof(maxDepth));

        // Get all measures
        var measuresQuery = "SELECT [Name], [Expression] FROM $SYSTEM.TMSCHEMA_MEASURES";
        var measures = await _connection.ExecAsync(measuresQuery, QueryType.DMV);
        
        var measureDict = measures.ToDictionary(
            m => m["Name"]?.ToString() ?? "",
            m => m["Expression"]?.ToString() ?? ""
        );

        // Measure reference regex pattern - matches [MeasureName] not followed by [
        var measurePattern = new Regex(@"\[([^\]]+)\](?!\s*\[)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        // Build dependency graph
        var dependencies = new Dictionary<string, List<string>>();
        var dependents = new Dictionary<string, List<string>>();
        
        foreach (var measure in measureDict)
        {
            dependencies[measure.Key] = new List<string>();
            dependents[measure.Key] = new List<string>();
        }

        foreach (var measure in measureDict)
        {
            var referencedMeasures = measurePattern.Matches(measure.Value)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(name => measureDict.ContainsKey(name))
                .Distinct()
                .ToList();

            dependencies[measure.Key] = referencedMeasures;
            
            // Build reverse dependency map
            foreach (var referenced in referencedMeasures)
            {
                dependents[referenced].Add(measure.Key);
            }
        }

        // Detect circular references
        var circularRefs = DetectCircularReferences(dependencies);

        // If specific measure requested, analyze it
        if (!string.IsNullOrEmpty(measureName))
        {
            if (!measureDict.ContainsKey(measureName))
                throw new ArgumentException($"Measure '{measureName}' not found", nameof(measureName));

            var dependencyChain = GetDependencyChain(measureName, dependencies, maxDepth);
            var impactedMeasures = GetImpactedMeasures(measureName, dependents, maxDepth);

            return new
            {
                MeasureName = measureName,
                DirectDependencies = dependencies[measureName],
                DependencyChain = dependencyChain,
                ImpactedMeasures = impactedMeasures,
                IsPartOfCircularReference = circularRefs.Any(cycle => cycle.Contains(measureName)),
                CircularReferences = circularRefs.Where(cycle => cycle.Contains(measureName)).ToList()
            };
        }

        // Return overall analysis
        return new
        {
            TotalMeasures = measureDict.Count,
            CircularReferences = circularRefs,
            ComplexMeasures = dependencies.Where(d => d.Value.Count > 3)
                .Select(d => new { Measure = d.Key, DependencyCount = d.Value.Count })
                .OrderByDescending(m => m.DependencyCount)
                .Take(10)
                .ToList(),
            OrphanedMeasures = dependencies.Where(d => d.Value.Count == 0 && dependents[d.Key].Count == 0)
                .Select(d => d.Key)
                .ToList()
        };
    }
    catch (Exception ex)
    {
        throw new DaxQueryExecutionException($"Failed to analyze measure dependencies: {ex.Message}", ex);
    }
}

private List<string> GetDependencyChain(string measureName, Dictionary<string, List<string>> dependencies, int maxDepth)
{
    var chain = new List<string>();
    var visited = new HashSet<string>();
    BuildDependencyChain(measureName, dependencies, chain, visited, 0, maxDepth);
    return chain.Distinct().ToList();
}

private void BuildDependencyChain(string measure, Dictionary<string, List<string>> dependencies, 
    List<string> chain, HashSet<string> visited, int depth, int maxDepth)
{
    if (depth >= maxDepth || visited.Contains(measure)) return;
    
    visited.Add(measure);
    foreach (var dependency in dependencies[measure])
    {
        chain.Add(dependency);
        BuildDependencyChain(dependency, dependencies, chain, visited, depth + 1, maxDepth);
    }
}

private List<string> GetImpactedMeasures(string measureName, Dictionary<string, List<string>> dependents, int maxDepth)
{
    var impacted = new List<string>();
    var visited = new HashSet<string>();
    BuildImpactChain(measureName, dependents, impacted, visited, 0, maxDepth);
    return impacted.Distinct().ToList();
}

private void BuildImpactChain(string measure, Dictionary<string, List<string>> dependents, 
    List<string> impacted, HashSet<string> visited, int depth, int maxDepth)
{
    if (depth >= maxDepth || visited.Contains(measure)) return;
    
    visited.Add(measure);
    foreach (var dependent in dependents[measure])
    {
        impacted.Add(dependent);
        BuildImpactChain(dependent, dependents, impacted, visited, depth + 1, maxDepth);
    }
}

private List<List<string>> DetectCircularReferences(Dictionary<string, List<string>> dependencies)
{
    var cycles = new List<List<string>>();
    var visited = new HashSet<string>();
    var recursionStack = new HashSet<string>();

    foreach (var measure in dependencies.Keys)
    {
        if (!visited.Contains(measure))
        {
            var currentPath = new List<string>();
            DetectCycle(measure, dependencies, visited, recursionStack, currentPath, cycles);
        }
    }

    return cycles;
}

private bool DetectCycle(string measure, Dictionary<string, List<string>> dependencies,
    HashSet<string> visited, HashSet<string> recursionStack, List<string> currentPath, List<List<string>> cycles)
{
    visited.Add(measure);
    recursionStack.Add(measure);
    currentPath.Add(measure);

    foreach (var dependency in dependencies[measure])
    {
        if (!visited.Contains(dependency))
        {
            if (DetectCycle(dependency, dependencies, visited, recursionStack, currentPath, cycles))
                return true;
        }
        else if (recursionStack.Contains(dependency))
        {
            // Found cycle - extract it
            var cycleStart = currentPath.IndexOf(dependency);
            var cycle = currentPath.Skip(cycleStart).ToList();
            cycle.Add(dependency); // Close the cycle
            cycles.Add(cycle);
            return true;
        }
    }

    recursionStack.Remove(measure);
    currentPath.RemoveAt(currentPath.Count - 1);
    return false;
}
```

---

### **Tool 3: ValidateDaxSyntax() - Enhanced - PRIORITY 1**

**User Pain Point:** Power BI error messages are cryptic - need better debugging  
**Current Workflow:** Trial and error debugging in Power BI Desktop  
**Effort Estimate:** 6-8 hours

#### Tool Signature
```csharp
[McpServerTool, Description("Validate DAX syntax and provide enhanced error reporting with recommendations.")]
public async Task<object> ValidateDaxSyntax(
    [Description("DAX expression to validate")] string daxExpression,
    [Description("Include performance and best practice recommendations")] bool includeRecommendations = true)
```

#### Implementation Code
```csharp
public async Task<object> ValidateDaxSyntax(string daxExpression, bool includeRecommendations = true)
{
    try
    {
        if (string.IsNullOrWhiteSpace(daxExpression))
            throw new ArgumentException("DAX expression cannot be empty", nameof(daxExpression));

        var result = new
        {
            Expression = daxExpression.Trim(),
            IsValid = false,
            SyntaxErrors = new List<object>(),
            Warnings = new List<object>(),
            Recommendations = new List<object>(),
            ComplexityScore = 0,
            ValidationDetails = new object()
        };

        // Basic syntax validation - try to execute as a table expression
        var testQuery = $"EVALUATE ROW(\"TestResult\", {daxExpression})";
        
        bool isValidSyntax = true;
        var syntaxErrors = new List<object>();
        var warnings = new List<object>();

        try
        {
            // Test syntax by attempting execution
            await _connection.ExecAsync(testQuery, QueryType.DAX);
        }
        catch (Exception ex)
        {
            isValidSyntax = false;
            var enhancedError = AnalyzeError(ex.Message, daxExpression);
            syntaxErrors.Add(enhancedError);
        }

        // Pattern-based analysis for common issues
        var patternIssues = AnalyzeDaxPatterns(daxExpression);
        warnings.AddRange(patternIssues.Warnings);
        
        var recommendations = new List<object>();
        if (includeRecommendations)
        {
            recommendations.AddRange(patternIssues.Recommendations);
            recommendations.AddRange(GetPerformanceRecommendations(daxExpression));
        }

        // Calculate complexity score
        var complexityScore = CalculateComplexityScore(daxExpression);

        return new
        {
            Expression = daxExpression.Trim(),
            IsValid = isValidSyntax,
            SyntaxErrors = syntaxErrors,
            Warnings = warnings,
            Recommendations = recommendations,
            ComplexityScore = complexityScore,
            ValidationDetails = new
            {
                ExpressionLength = daxExpression.Length,
                FunctionCount = CountDaxFunctions(daxExpression),
                NestingLevel = CalculateNestingLevel(daxExpression),
                HasTimeIntelligence = ContainsTimeIntelligence(daxExpression),
                HasIterators = ContainsIterators(daxExpression)
            }
        };
    }
    catch (Exception ex)
    {
        throw new DaxQueryExecutionException($"Failed to validate DAX syntax: {ex.Message}", ex);
    }
}

private object AnalyzeError(string errorMessage, string expression)
{
    // Enhanced error analysis with common patterns
    var commonErrors = new Dictionary<string, string>
    {
        { "syntax error", "Check for missing parentheses, commas, or invalid function names" },
        { "circular dependency", "This expression creates a circular reference with another measure" },
        { "wrong number of arguments", "Check function parameters - you may have too many or too few arguments" },
        { "type mismatch", "Check data types - you may be comparing text with numbers" },
        { "ambiguous column name", "Specify table name like Table[Column] to avoid ambiguity" },
        { "column not found", "Check spelling and ensure the column exists in the specified table" }
    };

    var suggestion = commonErrors.FirstOrDefault(kvp => 
        errorMessage.ToLowerInvariant().Contains(kvp.Key)).Value ?? 
        "Review DAX syntax and function usage";

    return new
    {
        ErrorMessage = errorMessage,
        Suggestion = suggestion,
        ErrorType = ClassifyError(errorMessage),
        PossibleCauses = GetPossibleCauses(errorMessage, expression)
    };
}

private (List<object> Warnings, List<object> Recommendations) AnalyzeDaxPatterns(string expression)
{
    var warnings = new List<object>();
    var recommendations = new List<object>();

    // Check for common anti-patterns
    if (expression.ToUpperInvariant().Contains("CALCULATE(SUM"))
    {
        recommendations.Add(new
        {
            Type = "Performance",
            Message = "Consider using SUM directly instead of CALCULATE(SUM(...)) if no filter modification is needed",
            Severity = "Medium"
        });
    }

    if (Regex.IsMatch(expression, @"SUMX\s*\(\s*ALL\s*\(", RegexOptions.IgnoreCase))
    {
        warnings.Add(new
        {
            Type = "Performance",
            Message = "SUMX(ALL(...)) pattern can be slow on large tables",
            Severity = "High"
        });
    }

    // Check for nested CALCULATE
    var calculateCount = Regex.Matches(expression, @"\bCALCULATE\s*\(", RegexOptions.IgnoreCase).Count;
    if (calculateCount > 2)
    {
        warnings.Add(new
        {
            Type = "Complexity",
            Message = $"Found {calculateCount} nested CALCULATE functions - consider simplifying",
            Severity = "Medium"
        });
    }

    // Check for missing error handling
    if (!expression.ToUpperInvariant().Contains("IFERROR") && 
        (expression.Contains("DIVIDE") || expression.Contains("/")))
    {
        recommendations.Add(new
        {
            Type = "Best Practice",
            Message = "Consider adding error handling with IFERROR for division operations",
            Severity = "Low"
        });
    }

    return (warnings, recommendations);
}

private List<object> GetPerformanceRecommendations(string expression)
{
    var recommendations = new List<object>();

    // Check for expensive patterns
    if (expression.ToUpperInvariant().Contains("FILTER") && 
        expression.ToUpperInvariant().Contains("ALL"))
    {
        recommendations.Add(new
        {
            Type = "Performance",
            Message = "FILTER(ALL(...)) can be expensive. Consider using KEEPFILTERS or table functions",
            Impact = "High",
            Alternative = "Use KEEPFILTERS or specific table functions when possible"
        });
    }

    if (Regex.IsMatch(expression, @"\b\w+X\s*\(\s*\w+\s*,", RegexOptions.IgnoreCase))
    {
        recommendations.Add(new
        {
            Type = "Performance",
            Message = "Iterator functions (X functions) can be expensive on large datasets",
            Impact = "Medium",
            Alternative = "Consider using aggregation functions where possible"
        });
    }

    return recommendations;
}

private int CalculateComplexityScore(string expression)
{
    var score = 0;
    score += expression.Length / 100; // Length factor
    score += CountDaxFunctions(expression) * 2; // Function count
    score += CalculateNestingLevel(expression) * 3; // Nesting penalty
    score += Regex.Matches(expression, @"\b\w+X\s*\(", RegexOptions.IgnoreCase).Count * 2; // Iterator penalty
    
    return Math.Min(score, 100); // Cap at 100
}

private int CountDaxFunctions(string expression)
{
    return Regex.Matches(expression, @"\b[A-Z]+\s*\(", RegexOptions.IgnoreCase).Count;
}

private int CalculateNestingLevel(string expression)
{
    int maxLevel = 0, currentLevel = 0;
    foreach (char c in expression)
    {
        if (c == '(') currentLevel++;
        if (c == ')') currentLevel--;
        maxLevel = Math.Max(maxLevel, currentLevel);
    }
    return maxLevel;
}

private bool ContainsTimeIntelligence(string expression)
{
    var timeIntFunctions = new[] { "DATEADD", "SAMEPERIODLASTYEAR", "PARALLELPERIOD", "TOTALYTD", "TOTALQTD", "TOTALMTD" };
    return timeIntFunctions.Any(func => expression.ToUpperInvariant().Contains(func));
}

private bool ContainsIterators(string expression)
{
    return Regex.IsMatch(expression, @"\b\w+X\s*\(", RegexOptions.IgnoreCase);
}

private string ClassifyError(string errorMessage)
{
    var lowerMessage = errorMessage.ToLowerInvariant();
    if (lowerMessage.Contains("syntax")) return "Syntax";
    if (lowerMessage.Contains("circular")) return "Circular Dependency";
    if (lowerMessage.Contains("argument")) return "Function Arguments";
    if (lowerMessage.Contains("type")) return "Data Type";
    if (lowerMessage.Contains("column")) return "Column Reference";
    return "General";
}

private List<string> GetPossibleCauses(string errorMessage, string expression)
{
    var causes = new List<string>();
    var lowerMessage = errorMessage.ToLowerInvariant();
    
    if (lowerMessage.Contains("syntax"))
    {
        causes.Add("Missing or extra parentheses");
        causes.Add("Typo in function name");
        causes.Add("Missing comma between parameters");
    }
    
    if (lowerMessage.Contains("column"))
    {
        causes.Add("Column name is misspelled");
        causes.Add("Table name is missing or incorrect");
        causes.Add("Column doesn't exist in the model");
    }
    
    return causes;
}
```

---

### **Tool 4: AnalyzeQueryPerformance() - PRIORITY 2**

**User Pain Point:** Poorly written DAX causes slow reports - need performance insights  
**Current Use Case:** Help improve DAX performance for specific queries  
**Effort Estimate:** 8-10 hours

#### Tool Signature
```csharp
[McpServerTool, Description("Analyze DAX query performance and provide optimization recommendations.")]
public async Task<object> AnalyzeQueryPerformance(
    [Description("DAX query to analyze for performance")] string daxQuery,
    [Description("Number of execution iterations for timing analysis")] int iterations = 3)
```

#### Implementation Code
```csharp
public async Task<object> AnalyzeQueryPerformance(string daxQuery, int iterations = 3)
{
    try
    {
        if (string.IsNullOrWhiteSpace(daxQuery))
            throw new ArgumentException("DAX query cannot be empty", nameof(daxQuery));

        if (iterations < 1 || iterations > 10)
            throw new ArgumentException("Iterations must be between 1 and 10", nameof(iterations));

        var performanceResults = new List<long>();
        var executionDetails = new List<object>();

        // Run performance tests
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var result = await _connection.ExecAsync(daxQuery, QueryType.DAX);
                stopwatch.Stop();
                
                performanceResults.Add(stopwatch.ElapsedMilliseconds);
                executionDetails.Add(new
                {
                    Iteration = i + 1,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    RowsReturned = result.Count(),
                    Success = true
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                executionDetails.Add(new
                {
                    Iteration = i + 1,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Error = ex.Message,
                    Success = false
                });
                break; // Stop on first error
            }
        }

        if (!performanceResults.Any())
        {
            return new
            {
                Query = daxQuery,
                Success = false,
                Error = "Query failed to execute",
                ExecutionDetails = executionDetails
            };
        }

        // Calculate performance metrics
        var avgTime = performanceResults.Average();
        var minTime = performanceResults.Min();
        var maxTime = performanceResults.Max();
        var variance = performanceResults.Select(x => Math.Pow(x - avgTime, 2)).Average();
        var standardDeviation = Math.Sqrt(variance);

        // Analyze query characteristics
        var queryAnalysis = AnalyzeQueryCharacteristics(daxQuery);
        
        // Generate performance recommendations
        var recommendations = GeneratePerformanceRecommendations(daxQuery, avgTime, queryAnalysis);

        // Classify performance
        var performanceRating = ClassifyPerformance(avgTime);

        return new
        {
            Query = daxQuery,
            Success = true,
            PerformanceMetrics = new
            {
                AverageTimeMs = Math.Round(avgTime, 2),
                MinTimeMs = minTime,
                MaxTimeMs = maxTime,
                StandardDeviationMs = Math.Round(standardDeviation, 2),
                PerformanceRating = performanceRating,
                Iterations = iterations
            },
            QueryAnalysis = queryAnalysis,
            Recommendations = recommendations,
            ExecutionDetails = executionDetails
        };
    }
    catch (Exception ex)
    {
        throw new DaxQueryExecutionException($"Failed to analyze query performance: {ex.Message}", ex);
    }
}

private object AnalyzeQueryCharacteristics(string query)
{
    var upperQuery = query.ToUpperInvariant();
    
    // Count different types of operations
    var iteratorCount = Regex.Matches(query, @"\b\w+X\s*\(", RegexOptions.IgnoreCase).Count;
    var calculateCount = Regex.Matches(query, @"\bCALCULATE\s*\(", RegexOptions.IgnoreCase).Count;
    var filterCount = Regex.Matches(query, @"\bFILTER\s*\(", RegexOptions.IgnoreCase).Count;
    var allCount = Regex.Matches(query, @"\bALL\s*\(", RegexOptions.IgnoreCase).Count;
    
    // Detect specific patterns
    var hasTimeIntelligence = ContainsTimeIntelligence(query);
    var hasComplexFiltering = filterCount > 0 || allCount > 0;
    var hasIterators = iteratorCount > 0;
    var hasNestedCalculate = calculateCount > 1;
    
    // Calculate complexity indicators
    var nestingLevel = CalculateNestingLevel(query);
    var functionCount = CountDaxFunctions(query);
    var queryLength = query.Length;

    return new
    {
        QueryLength = queryLength,
        FunctionCount = functionCount,
        NestingLevel = nestingLevel,
        IteratorCount = iteratorCount,
        CalculateCount = calculateCount,
        FilterCount = filterCount,
        AllCount = allCount,
        HasTimeIntelligence = hasTimeIntelligence,
        HasComplexFiltering = hasComplexFiltering,
        HasIterators = hasIterators,
        HasNestedCalculate = hasNestedCalculate,
        ComplexityScore = CalculateComplexityScore(query)
    };
}

private List<object> GeneratePerformanceRecommendations(string query, double avgTimeMs, dynamic analysis)
{
    var recommendations = new List<object>();

    // Performance-based recommendations
    if (avgTimeMs > 5000) // > 5 seconds
    {
        recommendations.Add(new
        {
            Type = "Critical Performance",
            Message = "Query execution time is very slow (>5s). Consider breaking into smaller parts or optimizing logic.",
            Priority = "High",
            ImpactLevel = "Critical"
        });
    }
    else if (avgTimeMs > 1000) // > 1 second
    {
        recommendations.Add(new
        {
            Type = "Performance Warning",
            Message = "Query execution time is slow (>1s). Consider optimization.",
            Priority = "Medium",
            ImpactLevel = "High"
        });
    }

    // Pattern-based recommendations
    if (analysis.IteratorCount > 2)
    {
        recommendations.Add(new
        {
            Type = "Iterator Optimization",
            Message = $"Found {analysis.IteratorCount} iterator functions. Consider reducing iterations or using aggregation functions.",
            Priority = "High",
            ImpactLevel = "High",
            Example = "Replace SUMX with SUM where possible"
        });
    }

    if (analysis.HasNestedCalculate)
    {
        recommendations.Add(new
        {
            Type = "Function Nesting",
            Message = "Multiple CALCULATE functions detected. Consider simplifying filter logic.",
            Priority = "Medium",
            ImpactLevel = "Medium",
            Example = "Combine filters in single CALCULATE when possible"
        });
    }

    if (analysis.FilterCount > 0 && analysis.AllCount > 0)
    {
        recommendations.Add(new
        {
            Type = "Filter Optimization",
            Message = "FILTER with ALL functions can be expensive. Consider alternative approaches.",
            Priority = "High",
            ImpactLevel = "High",
            Example = "Use KEEPFILTERS or specific table functions instead of FILTER(ALL(...))"
        });
    }

    if (analysis.ComplexityScore > 50)
    {
        recommendations.Add(new
        {
            Type = "Complexity Reduction",
            Message = "Query complexity is high. Consider breaking into variables or simpler expressions.",
            Priority = "Medium",
            ImpactLevel = "Medium",
            Example = "Use VAR statements to break down complex logic"
        });
    }

    // Add specific optimization suggestions
    if (query.ToUpperInvariant().Contains("SUMX") && query.ToUpperInvariant().Contains("FILTER"))
    {
        recommendations.Add(new
        {
            Type = "Specific Pattern",
            Message = "SUMX with FILTER pattern detected. Consider using CALCULATE with filters instead.",
            Priority = "Medium",
            ImpactLevel = "Medium",
            Example = "CALCULATE(SUM(Table[Column]), Filter1, Filter2) instead of SUMX(FILTER(...))"
        });
    }

    if (recommendations.Count == 0)
    {
        recommendations.Add(new
        {
            Type = "Performance Status",
            Message = "Query performance appears acceptable. No specific optimizations identified.",
            Priority = "Info",
            ImpactLevel = "Low"
        });
    }

    return recommendations;
}

private string ClassifyPerformance(double avgTimeMs)
{
    if (avgTimeMs < 100) return "Excellent";
    if (avgTimeMs < 500) return "Good";
    if (avgTimeMs < 1000) return "Acceptable";
    if (avgTimeMs < 5000) return "Slow";
    return "Very Slow";
}
```

## Testing Framework

### Unit Test Template
```csharp
[Test]
public async Task GetCalculatedColumns_ReturnsAllColumns_WhenNoTableFilter()
{
    // Arrange
    var daxTools = new DaxTools(_mockConnection.Object, _mockInstanceDiscovery.Object, _mockConfig.Object);
    
    // Setup mock DMV responses
    _mockConnection.Setup(x => x.ExecAsync(It.IsAny<string>(), QueryType.DMV))
        .ReturnsAsync(new[] 
        {
            new Dictionary<string, object?> 
            { 
                ["TableID"] = "1", 
                ["Name"] = "TestColumn", 
                ["Expression"] = "SUM(Table[Value])", 
                ["DataType"] = "Integer",
                ["IsHidden"] = false
            }
        });

    // Act
    var result = await daxTools.GetCalculatedColumns();

    // Assert
    Assert.IsNotNull(result);
    // Add specific assertions
}
```

## Integration Points

### Security Integration
All tools use existing [`DaxSecurityUtils`](../pbi-local-mcp/Core/DaxSecurityUtils.cs):
```csharp
// Input validation pattern
if (!string.IsNullOrEmpty(inputParam) && !DaxSecurityUtils.IsValidIdentifier(inputParam))
    throw new ArgumentException("Invalid parameter format", nameof(inputParam));

var escapedParam = DaxSecurityUtils.EscapeDaxIdentifier(inputParam);
```

### Error Handling Pattern
```csharp
try
{
    // Tool logic
}
catch (Exception ex)
{
    throw new DaxQueryExecutionException($"Failed to {operation}: {ex.Message}", ex);
}
```

### Connection Usage
```csharp
// DMV query pattern
var result = await _connection.ExecAsync(dmvQuery, QueryType.DMV);

// DAX query pattern  
var result = await _connection.ExecAsync(daxQuery, QueryType.DAX);
```

## Development Timeline

**Week 1:**
- Day 1-2: [`GetCalculatedColumns()`](../pbi-local-mcp/DaxTools.cs) - 4-6 hours
- Day 3-5: [`ValidateDaxSyntax()`](../pbi-local-mcp/DaxTools.cs) enhanced - 6-8 hours

**Week 2:**
- Day 1-3: [`AnalyzeMeasureDependencies()`](../pbi-local-mcp/DaxTools.cs) - 10-12 hours  
- Day 4-5: [`AnalyzeQueryPerformance()`](../pbi-local-mcp/DaxTools.cs) - 8-10 hours

**Week 3:**
- Testing, integration, and polish

**Total Effort:** 28-36 hours over 2-3 weeks for 4 high-value tools that solve real user pain points.