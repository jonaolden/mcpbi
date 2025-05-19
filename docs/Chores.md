# Chores

This document lists the maintenance tasks extracted from the development plan.

## General Cleanup

- Address the warning suppressions by adding proper XML documentation.
- Implement consistent error handling across all methods.
- Consider using a configuration provider rather than direct environment variables.
- Add more robust error handling with detailed error messages.
- Implement logging to track usage patterns and errors.
- Add retry logic for connection issues.

## Review Naming Conventions

The plan recommends adopting a consistent **Verb-Noun** pattern for tool names:

- **Get**: GetMeasureDetails, GetTableDetails, GetImplicitMeasureDAX
- **List**: ListMeasures, ListTables, ListTableColumns, ListTableRelationships
- **Analyze**: AnalyzeQueryPerformance, AnalyzeMeasureDependencies, AnalyzeModelHealth
- **Find**: FindUnusedObjects
- **Query**: QueryTableData, QueryDAX
