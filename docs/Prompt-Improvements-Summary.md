# DAX Prompt Improvements Summary

## Overview
The DAX prompts in the MCP server have been significantly enhanced based on the comprehensive DAX documentation available in the project and MCP C# SDK best practices, including advanced visual calculation debugging capabilities.

## Key Improvements Made

### 1. Enhanced SimplePromptType.cs

**Original Issues:**
- Basic, generic prompts with minimal DAX-specific guidance
- Limited context about tabular model considerations
- No specific performance or optimization guidance
- No visual context debugging capabilities

**Improvements:**
- **Comprehensive DAX Analysis**: Added detailed guidance for performance analysis, code quality assessment, and tabular model integration
- **Performance Focus**: Specific guidance on CALCULATE usage, context transitions, filter functions, and aggregation patterns
- **Best Practices Integration**: Incorporated INFO functions, relationship considerations, and security implications
- **Visual Context Debugging**: New specialized prompt for debugging visual aggregation issues
- **Additional Specialized Prompts**: Added new prompts for measure dependencies and DAX debugging

**New Prompts Added:**
- `analyze_measure_dependencies` - For mapping calculation chains and dependencies
- `debug_dax_calculation` - For systematic DAX troubleshooting
- `debug_visual_context` - **NEW**: For debugging visual aggregation issues with visual calculation simulation

### 2. Enhanced ComplexPromptType.cs

**Original Issues:**
- Simple parameterization with basic depth and focus options
- Limited business context considerations
- No model type or optimization preferences
- No visual-specific debugging capabilities

**Improvements:**
- **Advanced Parameterization**: Multiple sophisticated parameters for different scenarios
- **Business Context Awareness**: Specialized guidance for sales, finance, operations, HR contexts
- **Model Type Optimization**: Specific guidance for Import, DirectQuery, and Composite models
- **Comprehensive Analysis Options**: Multi-dimensional analysis with depth, focus, and optimization preferences
- **Visual Context Debugging**: Advanced parameterized prompt for visual calculation simulation

**Enhanced Prompts:**
- `analyze_dax_with_focus` - Advanced analysis with model type and alternative suggestions
- `design_measure_with_params` - Comprehensive measure design with business context
- `optimize_dax_performance` - Specialized performance optimization prompt
- `debug_visual_context_advanced` - **NEW**: Advanced visual context debugging with comprehensive parameters

### 3. Visual Context Debugging Capabilities

**New Visual Calculation Integration:**
Based on the comprehensive visual calculations documentation, the prompts now include:

**Visual Calculation Functions Integration:**
- COLLAPSE(), COLLAPSEALL() for parent/total analysis
- EXPAND(), EXPANDALL() for child-level investigation
- LOOKUP(), LOOKUPWITHTOTALS() for specific value retrieval
- FIRST(), LAST(), PREVIOUS(), NEXT() for positional analysis
- RUNNINGSUM(), MOVINGAVERAGE() for sequential calculations
- ISATLEVEL() for hierarchy-specific logic

**Visual Context Analysis Patterns:**
```dax
// Replicate visual structure
Visual Structure Check = 
VAR CurrentLevel = CONCATENATEX(VALUES([Column]), [Column], "", "")
VAR ParentLevel = COLLAPSE(CONCATENATEX(VALUES([Column]), [Column], "", ""), ROWS)
VAR TotalLevel = COLLAPSEALL(CONCATENATEX(VALUES([Column]), [Column], "", ""), ROWS)
RETURN "Current: " & CurrentLevel & " | Parent: " & ParentLevel & " | Total: " & TotalLevel

// Compare aggregation behavior
Aggregation Comparison = 
VAR DetailValue = [Original Measure]
VAR ParentSum = COLLAPSE(SUM([Base Value]), ROWS)
VAR ParentAvg = COLLAPSE(AVERAGE([Base Value]), ROWS)
VAR TotalValue = COLLAPSEALL([Original Measure], ROWS)
RETURN "Detail: " & DetailValue & " | Parent Sum: " & ParentSum & " | Parent Avg: " & ParentAvg & " | Total: " & TotalValue
```

**Visual Type-Specific Guidance:**
- **Matrix Visuals**: Row/column hierarchy interactions, subtotal behavior
- **Table Visuals**: Row-by-row vs totals aggregation
- **Chart Visuals**: Axis aggregation and legend interactions
- **Card Visuals**: Single-value context and cross-filtering

**Issue Type-Specific Analysis:**
- **Total Wrong**: COLLAPSE/COLLAPSEALL analysis for aggregation behavior
- **Blank Values**: ISBLANK checks and context investigation
- **Context Transition**: CALCULATE usage and filter context analysis
- **Hierarchy Issues**: ISATLEVEL testing and drill-level behavior

### 4. Specific DAX Knowledge Integration

**Performance Patterns:**
- Storage engine vs formula engine optimization
- Context transition efficiency analysis
- Iterator function best practices
- DirectQuery pushdown considerations

**Tabular Model Expertise:**
- Relationship filter propagation analysis
- Cardinality impact assessment
- Cross-filtering behavior optimization
- Security filtering implications

**Business Intelligence Patterns:**
- Time intelligence implementations
- Financial ratio calculations
- Sales and operations metrics
- Error handling and data quality validation

## Technical Implementation Details

### MCP C# SDK Integration
- Proper use of `[McpServerPromptType]` and `[McpServerPrompt]` attributes
- Comprehensive parameter descriptions using `[Description]` attributes
- Following MCP prompt naming conventions

### Parameter Enhancement
- **Depth Levels**: basic, detailed, comprehensive, enterprise
- **Focus Areas**: performance, style, security, relationships, balanced
- **Business Contexts**: sales, finance, operations, hr, general
- **Model Types**: import, directquery, composite, unknown
- **Optimization Priorities**: performance, readability, flexibility, balanced
- **Visual Types**: matrix, table, chart, card, unknown (NEW)
- **Issue Types**: total_wrong, blank_values, context_transition, hierarchy_issue, general (NEW)

### Advanced Features
- Alternative implementation suggestions
- Error handling patterns
- Time intelligence integration
- Security and governance considerations
- Enterprise-scale optimization techniques
- **Visual calculation simulation framework** (NEW)
- **Visual context debugging methodology** (NEW)

## Benefits for Clients

### For DAX Developers
- Expert-level guidance for complex calculations
- Performance optimization recommendations
- Best practices enforcement
- Debugging assistance
- **Visual context issue resolution** (NEW)

### For Business Analysts
- Business context-aware measure design
- Industry-specific calculation patterns
- Error handling and data quality guidance
- Time intelligence implementations
- **Visual aggregation behavior understanding** (NEW)

### For Enterprise Users
- Scalability considerations
- Security and governance guidance
- Performance at scale optimization
- Comprehensive audit capabilities
- **Cross-visual interaction analysis** (NEW)

## Usage Examples

### Basic DAX Analysis
```
Use prompt: analyze_dax
For: Quick analysis of DAX code with comprehensive best practices
```

### Advanced Performance Analysis
```
Use prompt: analyze_dax_with_focus
Parameters: depth="comprehensive", focus="performance", modelType="directquery"
For: Deep performance analysis for DirectQuery models
```

### Business-Specific Measure Design
```
Use prompt: design_measure_with_params
Parameters: complexity="complex", businessContext="finance", timeIntelligence=true
For: Designing sophisticated financial measures with time intelligence
```

### Performance Optimization
```
Use prompt: optimize_dax_performance
Parameters: bottleneckType="query", modelSize="enterprise", userConcurrency="high"
For: Enterprise-scale query performance optimization
```

### Visual Context Debugging (NEW)
```
Use prompt: debug_visual_context
For: Basic visual aggregation issue analysis with visual calculation guidance

Use prompt: debug_visual_context_advanced
Parameters: visualType="matrix", issueType="total_wrong", analysisDepth="comprehensive", includeSimulation=true
For: Advanced matrix visual debugging with complete visual calculation simulation
```

## Integration with Existing Tools

The enhanced prompts work seamlessly with the existing MCP tools:
- `evaluateDAX` for testing recommended optimizations and visual calculations
- `listMeasures` and `getMeasureDetails` for dependency analysis
- `getTableRelationships` for relationship optimization
- INFO function queries for metadata-driven analysis
- **Visual calculation testing and validation** (NEW)

## Real-World Use Case Example

**Scenario**: "Why is my Cost of Sales by District showing the average rather than the sum in the total row?"

**Solution Approach**:
1. Use `debug_visual_context_advanced` with parameters:
   - visualType="matrix"
   - issueType="total_wrong" 
   - analysisDepth="detailed"
   - includeSimulation=true

2. The prompt guides creation of visual calculations to:
   - Replicate the exact visual structure
   - Compare detail vs total level aggregations
   - Use COLLAPSE() to understand parent aggregation behavior
   - Test different aggregation patterns (SUM vs AVERAGE)

3. Client can then use `evaluateDAX` to test the suggested visual calculations and understand the root cause

## Future Enhancements

- Integration with DAX formatter and style guide enforcement
- Automated performance profiling integration
- Business rule validation patterns
- Advanced calculation group recommendations
- Cross-model optimization strategies
- **Real-time visual calculation simulation** (Future)
- **Interactive visual debugging workflows** (Future)

## Conclusion

These prompt improvements transform the MCP server from providing basic DAX assistance to offering enterprise-grade, expert-level guidance that leverages comprehensive DAX knowledge, tabular model best practices, business intelligence patterns, and advanced visual calculation debugging capabilities. The addition of visual context debugging makes the server uniquely capable of helping users understand and resolve complex visual aggregation issues that are common in Power BI development.