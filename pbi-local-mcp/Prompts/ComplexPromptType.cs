using System.ComponentModel;

using ModelContextProtocol.Server;

namespace pbi_local_mcp.Prompts;

/// <summary>
/// Provides advanced parameterizable prompt templates for DAX analysis and PowerBI measure design tasks.
/// Allows fine-tuning of analysis depth, focus areas, measure complexity, and specialized scenarios 
/// through comprehensive parameters that leverage deep DAX and tabular model expertise.
/// </summary>
[McpServerPromptType]
public class ComplexPromptType
{
    /// <summary>
    /// Returns a highly customized prompt template for analyzing DAX queries with specific focus areas
    /// and analysis depth levels, incorporating comprehensive DAX best practices and optimization techniques.
    /// </summary>
    /// <param name="depth">The depth of analysis requested: 'basic', 'detailed', or 'comprehensive'</param>
    /// <param name="focus">The focus areas for analysis: 'performance', 'style', 'security', 'relationships', or 'both' (default)</param>
    /// <param name="modelType">The model type context: 'import', 'directquery', 'composite', or 'unknown' (default)</param>
    /// <param name="includeAlternatives">Whether to provide alternative implementation suggestions</param>
    /// <returns>A specialized prompt for DAX query analysis with specified parameters and expert guidance</returns>
    [McpServerPrompt(Name = "analyze_dax_with_focus"), Description("Advanced DAX analysis with customizable focus areas and depth")]
    public static string AnalyzeDaxWithFocus(
        [Description("Analysis depth: basic (quick review), detailed (thorough analysis), comprehensive (complete audit)")] string depth,
        [Description("Focus areas: performance (optimization focus), style (code quality), security (RLS/filtering), relationships (model impact), both (performance + style)")] string focus = "both",
        [Description("Model type context: import (in-memory), directquery (source queries), composite (mixed mode), unknown")] string modelType = "unknown",
        [Description("Include alternative implementation suggestions")] bool includeAlternatives = true)
    {
        var basePrompt = "You are an expert DAX analyzer with deep knowledge of tabular models, performance optimization, and enterprise best practices.";

        var depthGuidance = depth.ToLower() switch
        {
            "basic" => @"
**Quick Analysis Focus:**
- Identify immediate performance red flags
- Check for basic syntax and logic issues
- Highlight critical optimization opportunities
- Provide concise, actionable recommendations",

            "detailed" => @"
**Detailed Analysis Focus:**
- Comprehensive performance evaluation with specific bottleneck identification
- Code quality assessment with maintainability considerations
- Relationship and context behavior analysis
- Security and filtering implications review
- Detailed optimization recommendations with implementation guidance",

            "comprehensive" => @"
**Comprehensive Analysis Focus:**
- Complete calculation chain analysis and dependency mapping
- Advanced performance profiling with execution plan considerations
- Enterprise-grade code quality and governance review
- Full tabular model integration assessment
- Security, compliance, and data governance implications
- Detailed refactoring roadmap with priority recommendations
- Alternative architecture and pattern suggestions",

            _ => @"
**Standard Analysis Focus:**
- Performance and code quality evaluation
- Basic optimization recommendations
- Context and relationship considerations"
        };

        var focusGuidance = focus.ToLower() switch
        {
            "performance" => @"
**Performance-Focused Analysis:**
- Evaluate calculation engine vs storage engine usage
- Assess context transition efficiency (CALCULATE patterns)
- Review iterator functions and row-by-row operations
- Analyze filter propagation and relationship traversal
- Check for expensive operations: FILTER, RELATED, CROSSFILTER usage
- Assess aggregation patterns and materialization opportunities
- Review DirectQuery pushdown capabilities (if applicable)",

            "style" => @"
**Code Quality and Style Analysis:**
- Variable naming conventions and readability
- Logical structure and decomposition patterns
- Error handling and edge case management
- Documentation and maintainability assessment
- Consistency with DAX best practices and patterns
- Modular design and reusability considerations",

            "security" => @"
**Security and Data Governance Analysis:**
- Row-level security (RLS) filter compatibility
- Object-level security considerations
- Data privacy and sensitive information handling
- Cross-filtering security implications
- Audit trail and compliance requirements
- Security filter performance impact",

            "relationships" => @"
**Relationship and Model Integration Analysis:**
- Filter propagation paths and directionality
- Cardinality impact on calculation behavior
- Cross-filtering and bidirectional relationship effects
- Many-to-many relationship handling
- Inactive relationship utilization
- Role-playing dimension considerations",

            _ => @"
**Balanced Performance and Style Analysis:**
- Performance optimization opportunities
- Code quality and maintainability
- Context behavior and relationship impact
- Error handling and robustness"
        };

        var modelTypeGuidance = modelType.ToLower() switch
        {
            "import" => @"
**Import Mode Considerations:**
- Leverage in-memory storage engine optimizations
- Consider calculated column vs measure trade-offs
- Optimize for memory usage and refresh performance
- Utilize VertiPaq compression benefits",

            "directquery" => @"
**DirectQuery Mode Considerations:**
- Ensure query pushdown compatibility
- Minimize complex calculations that can't be pushed down
- Optimize for source system performance
- Consider result caching implications
- Review security and connection string handling",

            "composite" => @"
**Composite Mode Considerations:**
- Optimize table storage mode selection
- Manage aggregation table utilization
- Balance import vs DirectQuery table relationships
- Consider dual storage mode implications",

            _ => @"
**General Model Considerations:**
- Analyze for all storage mode best practices
- Provide recommendations for optimal storage mode selection"
        };

        var alternativesGuidance = includeAlternatives
            ? @"
**Alternative Implementation Analysis:**
- Provide 2-3 alternative calculation approaches
- Compare performance characteristics of each approach
- Assess maintainability and readability trade-offs
- Include modern DAX patterns and functions where applicable
- Suggest calculation group alternatives where relevant"
            : "";

        return $@"{basePrompt}

{depthGuidance}

{focusGuidance}

{modelTypeGuidance}

{alternativesGuidance}

**Available MCP Tools for Analysis:**
Leverage these tools for comprehensive model analysis:
- ListTables: Get overview of all tables in the model
- ListMeasures: Get essential measure information (name, table, data type, visibility) - optimized for browsing
- GetMeasureDetails: Get complete measure details including full DAX expressions for specific measures
- GetTableDetails: Get detailed information for specific tables
- GetTableColumns: Get column information for specific tables
- GetTableRelationships: Get relationship details for specific tables
- PreviewTableData: Sample data from tables for context
- RunQuery: Execute custom DAX queries for testing and validation
- ValidateDaxSyntax: Validate DAX syntax with performance recommendations
- AnalyzeQueryPerformance: Analyze query performance and optimization opportunities

**INFO Function Integration:**
Utilize these DAX patterns for comprehensive analysis:
- INFO.RELATIONSHIPS() for relationship validation
- INFO.MEASURES() for dependency analysis
- INFO.TABLES() and INFO.COLUMNS() for metadata review
- Performance profiling patterns and testing scenarios

Please perform a {depth} analysis focused on {focus} aspects of the following DAX query:";
    }

    /// <summary>
    /// Returns a comprehensive prompt template for designing DAX measures with specified complexity
    /// and technical considerations focused on PowerBI model understanding and best practices.
    /// </summary>
    /// <param name="complexity">The desired measure complexity: 'simple', 'moderate', 'complex', or 'advanced'</param>
    /// <param name="calculationType">The calculation focus: 'aggregation', 'time_intelligence', 'ranking', 'statistical', or 'general'</param>
    /// <param name="timeIntelligence">Whether to include time intelligence patterns in the measure design</param>
    /// <param name="includeErrorHandling">Whether to include comprehensive error handling patterns</param>
    /// <param name="optimizeFor">Optimization priority: 'performance', 'readability', 'flexibility', or 'balanced'</param>
    /// <returns>A specialized prompt for DAX measure design with specified parameters and expert guidance</returns>
    [McpServerPrompt(Name = "design_measure_with_params"), Description("Advanced DAX measure design focused on technical patterns and PowerBI best practices")]
    public static string DesignMeasureWithParams(
        [Description("Measure complexity: simple (basic aggregations), moderate (with filters/conditions), complex (advanced calculations), advanced (sophisticated logic)")] string complexity,
        [Description("Calculation type: aggregation (sums/counts), time_intelligence (periods/growth), ranking (topN/percentiles), statistical (averages/distributions), general")] string calculationType = "general",
        [Description("Include time intelligence patterns (YTD, MTD, previous periods)")] bool timeIntelligence = false,
        [Description("Include comprehensive error handling and edge case management")] bool includeErrorHandling = true,
        [Description("Optimization priority: performance (speed focus), readability (maintainability), flexibility (parameterizable), balanced")] string optimizeFor = "balanced")
    {
        var basePrompt = "You are a PowerBI measure design expert with extensive knowledge of DAX patterns, tabular model architecture, and performance optimization techniques.";

        var complexityGuidance = complexity.ToLower() switch
        {
            "simple" => @"
**Simple Measure Design:**
- Focus on straightforward aggregations (SUM, COUNT, AVERAGE)
- Use basic filter patterns with CALCULATE
- Implement simple conditional logic with IF statements
- Ensure clear, readable structure with minimal nesting",

            "moderate" => @"
**Moderate Measure Design:**
- Implement multi-level calculations with variables
- Use advanced filter patterns (KEEPFILTERS, REMOVEFILTERS)
- Include conditional aggregations and filtering logic
- Design for multiple filter contexts and scenarios",

            "complex" => @"
**Complex Measure Design:**
- Implement sophisticated calculation logic with multiple paths
- Use advanced DAX patterns (iterators, ranking, statistical functions)
- Handle complex filter contexts and context transitions
- Design calculation chains and measure dependencies",

            "advanced" => @"
**Advanced Measure Design:**
- Implement sophisticated logic with comprehensive validation
- Design for scalability and performance optimization
- Include advanced security and governance considerations
- Implement calculation groups and advanced modeling patterns
- Design for reusability and maintainability",

            _ => @"
**Standard Measure Design:**
- Balanced approach with moderate complexity
- Include essential logic and validation patterns"
        };

        var calculationTypeGuidance = calculationType.ToLower() switch
        {
            "aggregation" => @"
**Aggregation-Focused Patterns:**
- SUM, COUNT, AVERAGE optimizations
- Distinct count operations and performance considerations
- Conditional aggregations with CALCULATE
- Aggregation table utilization strategies
- Memory-efficient aggregation patterns",

            "time_intelligence" => @"
**Time Intelligence Patterns:**
- YTD, QTD, MTD calculations using native functions
- Period-over-period comparisons and growth rates
- Rolling averages and moving calculations
- Fiscal year and custom calendar handling
- Time-based filtering and context behavior",

            "ranking" => @"
**Ranking and Statistical Patterns:**
- RANKX implementations for various scenarios
- TOPN and filtering for top/bottom analysis
- Percentile and quartile calculations
- Distribution analysis and statistical measures
- Performance optimization for ranking operations",

            "statistical" => @"
**Statistical Analysis Patterns:**
- Advanced statistical functions (MEDIAN, PERCENTILE)
- Distribution analysis and variance calculations
- Correlation and regression analysis patterns
- Data quality assessment measures
- Statistical significance testing approaches",

            _ => @"
**General Calculation Patterns:**
- Versatile measure design principles
- Common DAX patterns and best practices
- Flexible calculation frameworks"
        };

        var timeIntelligenceGuidance = timeIntelligence
            ? @"
**Time Intelligence Integration:**
- Implement YTD, QTD, MTD calculations using TOTALYTD, TOTALQTD, TOTALMTD
- Design previous period comparisons with DATEADD, SAMEPERIODLASTYEAR
- Create period-over-period growth calculations
- Handle fiscal year and custom calendar scenarios
- Implement rolling averages and moving calculations
- Consider time-based filtering and context behavior"
            : @"
**Time Considerations:**
- Design for current period calculations
- Consider basic date filtering scenarios";

        var errorHandlingGuidance = includeErrorHandling
            ? @"
**Comprehensive Error Handling:**
- Implement BLANK() handling with ISBLANK and COALESCE
- Use DIVIDE function for safe division operations
- Include ISERROR and IFERROR for robust error management
- Handle edge cases: empty tables, missing relationships, invalid dates
- Implement data quality validation within calculations
- Design graceful degradation for missing or invalid data"
            : @"
**Basic Error Handling:**
- Include essential BLANK() and division by zero protection";

        var optimizationGuidance = optimizeFor.ToLower() switch
        {
            "performance" => @"
**Performance Optimization Focus:**
- Minimize context transitions and expensive operations
- Use efficient aggregation patterns and avoid row-by-row iteration
- Implement proper variable usage for calculation reuse
- Design for storage engine optimization
- Consider materialization opportunities",

            "readability" => @"
**Readability and Maintainability Focus:**
- Use clear variable names and logical decomposition
- Implement comprehensive comments and documentation
- Design modular, reusable calculation patterns
- Follow consistent formatting and naming conventions",

            "flexibility" => @"
**Flexibility and Parameterization Focus:**
- Design parameterizable calculations with variables
- Implement switch statements for multi-scenario logic
- Create reusable patterns and templates
- Design for easy modification and extension",

            _ => @"
**Balanced Optimization:**
- Balance performance, readability, and flexibility
- Implement DAX best practices and proven patterns"
        };

        return $@"{basePrompt}

{complexityGuidance}

{calculationTypeGuidance}

{timeIntelligenceGuidance}

{errorHandlingGuidance}

{optimizationGuidance}

**Advanced DAX Patterns to Consider:**
- SUMMARIZE vs SUMMARIZECOLUMNS for different aggregation needs
- CALCULATE with multiple filter arguments vs chained CALCULATE
- Iterator functions (SUMX, AVERAGEX) vs direct aggregations
- Virtual relationship patterns using TREATAS
- Advanced filtering with KEEPFILTERS and CROSSFILTER
- Calculation groups for time intelligence and formatting

**Model Integration Considerations:**
- Relationship leveraging for optimal performance
- Storage mode compatibility (Import/DirectQuery/Composite)
- Security context and RLS implications
- Cross-table filtering behavior
- Aggregation table utilization opportunities

**Validation and Testing:**
- Include test scenarios for edge cases and boundary conditions
- Provide sample filter contexts for validation
- Suggest performance testing approaches
- Include data quality validation patterns

Please help design a {complexity} DAX measure focused on {calculationType} calculations{(timeIntelligence ? " with time intelligence patterns" : "")}, optimized for {optimizeFor}:";
    }

    /// <summary>
    /// Returns a specialized prompt for DAX optimization with specific performance bottleneck analysis
    /// and advanced optimization techniques tailored to enterprise scenarios.
    /// </summary>
    /// <param name="bottleneckType">Type of performance issue: 'memory', 'cpu', 'query', 'refresh', or 'general'</param>
    /// <param name="modelSize">Model size category: 'small', 'medium', 'large', 'enterprise'</param>
    /// <param name="userConcurrency">Expected user concurrency: 'low', 'medium', 'high', 'enterprise'</param>
    /// <returns>A specialized prompt for advanced DAX performance optimization</returns>
    [McpServerPrompt(Name = "optimize_dax_performance"), Description("Advanced DAX performance optimization with bottleneck-specific guidance")]
    public static string OptimizeDaxPerformance(
        [Description("Performance bottleneck type: memory (RAM usage), cpu (processing), query (response time), refresh (data load), general")] string bottleneckType,
        [Description("Model size: small (<1GB), medium (1-5GB), large (5-20GB), enterprise (>20GB)")] string modelSize = "medium",
        [Description("User concurrency: low (<10), medium (10-50), high (50-200), enterprise (>200)")] string userConcurrency = "medium")
    {
        var bottleneckGuidance = bottleneckType.ToLower() switch
        {
            "memory" => @"
**Memory Optimization Focus:**
- Analyze VertiPaq compression efficiency and column cardinality
- Review calculated column vs measure memory impact
- Assess relationship memory overhead and optimization
- Evaluate aggregation table opportunities
- Consider composite model memory distribution",

            "cpu" => @"
**CPU Performance Focus:**
- Optimize calculation engine vs storage engine distribution
- Minimize complex iterators and row-by-row operations
- Review context transition efficiency
- Analyze formula engine bottlenecks
- Optimize for parallel query execution",

            "query" => @"
**Query Response Time Focus:**
- Optimize DirectQuery pushdown capabilities
- Minimize expensive cross-filtering operations
- Review measure calculation dependencies
- Analyze filter propagation efficiency
- Implement result caching strategies",

            "refresh" => @"
**Data Refresh Performance Focus:**
- Optimize partition and incremental refresh strategies
- Review calculated column refresh impact
- Analyze source query performance
- Implement efficient data transformation patterns
- Consider parallel refresh opportunities",

            _ => @"
**General Performance Focus:**
- Comprehensive performance analysis across all dimensions
- Balanced optimization approach for overall efficiency"
        };

        var modelSizeGuidance = modelSize.ToLower() switch
        {
            "small" => @"
**Small Model Optimization:**
- Focus on query response time and user experience
- Emphasize code readability and maintainability
- Consider advanced patterns for future scalability",

            "medium" => @"
**Medium Model Optimization:**
- Balance performance and functionality
- Implement efficient aggregation patterns
- Consider partitioning and incremental refresh
- Optimize for moderate user concurrency",

            "large" => @"
**Large Model Optimization:**
- Implement advanced performance optimization techniques
- Consider calculation groups and aggregation tables
- Optimize memory usage and compression
- Design for scalability and enterprise usage",

            "enterprise" => @"
**Enterprise Model Optimization:**
- Implement comprehensive performance architecture
- Design for high availability and scalability
- Consider distributed computing and scale-out scenarios
- Implement advanced monitoring and optimization patterns",

            _ => ""
        };

        var concurrencyGuidance = userConcurrency.ToLower() switch
        {
            "low" => @"
**Low Concurrency Optimization:**
- Focus on individual query performance
- Optimize for single-user scenarios and development",

            "medium" => @"
**Medium Concurrency Optimization:**
- Balance individual performance with resource sharing
- Implement efficient caching and result reuse
- Consider connection pooling and resource management",

            "high" => @"
**High Concurrency Optimization:**
- Optimize for resource contention and parallel execution
- Implement advanced caching and materialization strategies
- Consider load balancing and scale-out patterns",

            "enterprise" => @"
**Enterprise Concurrency Optimization:**
- Design for massive concurrent user scenarios
- Implement comprehensive resource management
- Consider distributed caching and computation
- Design for high availability and fault tolerance",

            _ => ""
        };

        return $@"You are a DAX performance optimization expert specializing in enterprise-scale tabular model optimization and advanced performance tuning.

{bottleneckGuidance}

{modelSizeGuidance}

{concurrencyGuidance}

**Advanced Optimization Techniques:**
- VertiPaq analyzer patterns and compression optimization
- Calculation group implementation for performance
- Aggregation table design and automatic aggregation
- Composite model optimization strategies
- Advanced DirectQuery optimization patterns

**Performance Monitoring and Analysis:**
- DAX Studio profiling and optimization techniques
- Performance analyzer interpretation and optimization
- Query plan analysis and bottleneck identification
- Resource usage monitoring and optimization

**Enterprise Performance Patterns:**
- Calculation precedence and dependency optimization
- Advanced caching and materialization strategies
- Parallel processing and resource utilization
- Scale-out and high-availability considerations

Please analyze and optimize the following DAX code for {bottleneckType} performance in a {modelSize} model with {userConcurrency} user concurrency:";
    }

    /// <summary>
    /// Returns a specialized prompt for debugging visual context issues with comprehensive visual calculation simulation,
    /// designed to help understand and replicate unexpected aggregation behaviors in Power BI visuals.
    /// </summary>
    /// <param name="visualType">The type of visual experiencing issues: 'matrix', 'table', 'chart', 'card', or 'unknown'</param>
    /// <param name="issueType">The specific aggregation issue: 'total_wrong', 'blank_values', 'context_transition', 'hierarchy_issue', or 'general'</param>
    /// <param name="analysisDepth">Analysis depth: 'quick', 'detailed', 'comprehensive'</param>
    /// <param name="includeSimulation">Whether to include visual calculation simulation examples</param>
    /// <returns>A specialized prompt for visual context debugging with visual calculation guidance</returns>
    [McpServerPrompt(Name = "debug_visual_context_advanced"), Description("Advanced visual context debugging with comprehensive visual calculation simulation")]
    public static string DebugVisualContextAdvanced(
        [Description("Visual type: matrix (rows/columns), table (list), chart (bar/line), card (single value), unknown")] string visualType,
        [Description("Issue type: total_wrong (aggregation in totals), blank_values (unexpected blanks), context_transition (filter context), hierarchy_issue (drill levels), general")] string issueType,
        [Description("Analysis depth: quick (basic checks), detailed (thorough analysis), comprehensive (complete simulation)")] string analysisDepth = "detailed",
        [Description("Include visual calculation simulation examples")] bool includeSimulation = true)
    {
        var basePrompt = "You are a PowerBI visual context expert specializing in debugging complex aggregation behaviors and visual-specific calculation issues using advanced visual calculation techniques.";

        var visualTypeGuidance = visualType.ToLower() switch
        {
            "matrix" => @"
**Matrix Visual Analysis:**
- Focus on row and column header interactions
- Analyze cross-filtering between row and column hierarchies
- Examine subtotal and grand total calculation behavior
- Test ROWS and COLUMNS axis parameters in visual calculations
- Consider hierarchy-level context transitions",

            "table" => @"
**Table Visual Analysis:**
- Focus on row-by-row calculation behavior
- Analyze measure behavior with grouped data
- Examine totals row aggregation patterns
- Test ROWS axis calculations for detail-level analysis
- Consider grouped vs ungrouped aggregation",

            "chart" => @"
**Chart Visual Analysis:**
- Focus on axis aggregation and legend interactions
- Analyze measure behavior across chart categories
- Examine tooltip and data label calculations
- Test visual calculation behavior with chart groupings
- Consider time-series and categorical aggregation",

            "card" => @"
**Card Visual Analysis:**
- Focus on single-value aggregation context
- Analyze filter context from slicers and other visuals
- Examine measure calculation in isolation
- Test context propagation from page-level filters
- Consider cross-filtering impact on card values",

            _ => @"
**General Visual Analysis:**
- Examine visual-specific aggregation patterns
- Analyze filter context and measure interactions
- Consider visual type impact on calculation behavior"
        };

        var issueTypeGuidance = issueType.ToLower() switch
        {
            "total_wrong" => @"
**Total Row Aggregation Issues:**
- Analyze difference between detail-level and total-level calculations
- Use COLLAPSE() and COLLAPSEALL() to understand aggregation behavior
- Test measure behavior with explicit context transitions
- Examine iterator vs aggregator function usage
- Check for implicit vs explicit measure aggregation",

            "blank_values" => @"
**Blank Value Analysis:**
- Identify where BLANK values originate in the calculation chain
- Use ISBLANK() checks at different visual levels
- Test with COALESCE() and default value patterns
- Examine filter context causing empty results
- Check relationship and join behavior",

            "context_transition" => @"
**Context Transition Issues:**
- Analyze filter context vs row context behavior
- Test CALCULATE usage and context modification
- Examine measure vs calculated column context differences
- Use HASONEVALUE() and VALUES() for context inspection
- Check cross-filtering and relationship propagation",

            "hierarchy_issue" => @"
**Hierarchy Level Issues:**
- Analyze calculation behavior at different drill levels
- Test ISATLEVEL() for level-specific calculations
- Use parent-child relationship analysis
- Examine aggregation behavior across hierarchy levels
- Check drill-down and drill-up calculation consistency",

            _ => @"
**General Issue Analysis:**
- Comprehensive visual context examination
- Multi-level calculation behavior analysis
- Filter and aggregation pattern investigation"
        };

        var depthGuidance = analysisDepth.ToLower() switch
        {
            "quick" => @"
**Quick Analysis Approach:**
- Rapid identification of common visual context issues
- Basic visual calculation tests to confirm behavior
- Essential debugging patterns and quick fixes
- Focus on most likely root causes",

            "detailed" => @"
**Detailed Analysis Approach:**
- Systematic examination of visual context behavior
- Comprehensive visual calculation simulation
- Step-by-step debugging with multiple test scenarios
- Analysis of related visual interactions and dependencies",

            "comprehensive" => @"
**Comprehensive Analysis Approach:**
- Complete visual context analysis with all possible scenarios
- Advanced visual calculation patterns and edge case testing
- Full dependency mapping and interaction analysis
- Performance impact assessment and optimization recommendations
- Documentation of findings and resolution patterns",

            _ => @"
**Standard Analysis Approach:**
- Balanced investigation of visual context issues
- Practical debugging steps and solution recommendations"
        };

        var simulationGuidance = includeSimulation
            ? @"
**Visual Calculation Simulation Framework:**

1. **Replication Strategy:**
   ```dax
   // Replicate the exact visual structure
   Visual Structure Check = 
   VAR CurrentLevel = CONCATENATEX(VALUES([Column]), [Column], "", "")
   VAR ParentLevel = COLLAPSE(CONCATENATEX(VALUES([Column]), [Column], "", ""), ROWS)
   VAR TotalLevel = COLLAPSEALL(CONCATENATEX(VALUES([Column]), [Column], "", ""), ROWS)
   RETURN ""Current: "" & CurrentLevel & "" | Parent: "" & ParentLevel & "" | Total: "" & TotalLevel
   ```

2. **Aggregation Behavior Analysis:**
   ```dax
   // Compare measure behavior at different levels
   Aggregation Comparison = 
   VAR DetailValue = [Original Measure]
   VAR ParentSum = COLLAPSE(SUM([Base Value]), ROWS)
   VAR ParentAvg = COLLAPSE(AVERAGE([Base Value]), ROWS)
   VAR TotalValue = COLLAPSEALL([Original Measure], ROWS)
   RETURN ""Detail: "" & DetailValue & "" | Parent Sum: "" & ParentSum & "" | Parent Avg: "" & ParentAvg & "" | Total: "" & TotalValue
   ```

3. **Context Investigation:**
   ```dax
   // Examine filter context at each level
   Context Debug = 
   VAR RowContext = IF(HASONEVALUE([Column]), ""Single: "" & SELECTEDVALUE([Column]), ""Multiple: "" & COUNTROWS(VALUES([Column])))
   VAR FilteredRows = COUNTROWS(ALLSELECTED([Column]))
   VAR TotalRows = COUNTROWS(ALL([Column]))
   RETURN RowContext & "" | Filtered: "" & FilteredRows & "" | Total: "" & TotalRows
   ```

4. **Hierarchy Level Testing:**
   ```dax
   // Test calculations at specific hierarchy levels
   Level Specific Test = 
   SWITCH(
       TRUE(),
       ISATLEVEL([Level1]), ""Level 1: "" & [Measure],
       ISATLEVEL([Level2]), ""Level 2: "" & [Measure],
       ISATLEVEL([Level3]), ""Level 3: "" & [Measure],
       ""Other Level: "" & [Measure]
   )
   ```

5. **Cross-Visual Impact Analysis:**
   ```dax
   // Check how other visuals affect this calculation
   Cross Visual Impact = 
   VAR BaseValue = [Original Measure]
   VAR UnfilteredValue = CALCULATE([Original Measure], ALL([Table]))
   VAR AllSelectedValue = CALCULATE([Original Measure], ALLSELECTED([Table]))
   RETURN ""Base: "" & BaseValue & "" | All: "" & UnfilteredValue & "" | Selected: "" & AllSelectedValue
   ```"
            : @"
**Basic Visual Analysis:**
- Use standard debugging techniques without extensive simulation
- Focus on identifying and fixing the core issue";

        return $@"{basePrompt}

{visualTypeGuidance}

{issueTypeGuidance}

{depthGuidance}

{simulationGuidance}

**Advanced Visual Calculation Functions:**
- COLLAPSE(), COLLAPSEALL() for parent/total analysis
- EXPAND(), EXPANDALL() for child-level investigation
- LOOKUP(), LOOKUPWITHTOTALS() for specific value retrieval
- FIRST(), LAST(), PREVIOUS(), NEXT() for positional analysis
- RUNNINGSUM(), MOVINGAVERAGE() for sequential calculations
- ISATLEVEL() for hierarchy-specific logic

**Systematic Debugging Process:**

1. **Reproduce the Issue:**
   - Map exact visual configuration and measure placement
   - Document unexpected vs expected behavior
   - Identify specific rows/columns showing problems

2. **Create Visual Calculation Tests:**
   - Build step-by-step visual calculations to isolate issues
   - Test different aggregation patterns (SUM, AVERAGE, COUNT)
   - Compare original measure with visual calculation equivalents

3. **Analyze Context Behavior:**
   - Use visual calculations to inspect filter context at each level
   - Test with AXIS parameters (ROWS, COLUMNS, ROWS COLUMNS)
   - Apply RESET parameters for hierarchy boundary analysis

4. **Resolution and Validation:**
   - Modify original measure based on findings
   - Validate fix across different filter contexts and visual configurations
   - Document solution pattern for future reference

Please help debug the following {visualType} visual showing {issueType} behavior with {analysisDepth} analysis:";
    }

    /// <summary>
    /// Interactive DAX debugging with step-by-step analysis and comprehensive recommendations.
    /// </summary>
    /// <param name="approach">Debugging approach: variable_inspection, context_analysis, execution_flow, error_tracing</param>
    /// <param name="includeVisualCalc">Include visual calculation simulation</param>
    /// <param name="depth">Analysis depth: quick, detailed, comprehensive</param>
    /// <returns>A specialized prompt for interactive DAX debugging with step-by-step guidance</returns>
    [McpServerPrompt(Name = "debug_dax_step_by_step"), Description("Interactive DAX debugging with step-by-step analysis")]
    public static string DebugDaxStepByStep(
        [Description("Debugging approach: variable_inspection (focus on variables), context_analysis (filter context), execution_flow (step-by-step execution), error_tracing (error source identification)")] string approach,
        [Description("Include visual calculation simulation")] bool includeVisualCalc = true,
        [Description("Analysis depth: quick, detailed, comprehensive")] string depth = "detailed")
    {
        var basePrompt = @"You are a DAX debugging expert specializing in systematic problem diagnosis and resolution. Your expertise includes step-by-step analysis, variable inspection, context evaluation, and comprehensive error tracing.";

        var approachGuidance = approach.ToLower() switch
        {
            "variable_inspection" => @"
**Variable Inspection Debugging:**
- Systematic analysis of all variable definitions and their values
- Step-by-step variable evaluation and dependency tracking
- Identification of variable scope issues and calculation errors
- Variable value validation at each calculation step",

            "context_analysis" => @"
**Filter Context Analysis:**
- Comprehensive filter context evaluation at each calculation step
- Row context and filter context interaction analysis
- Context transition identification and validation
- Filter propagation and relationship impact assessment",

            "execution_flow" => @"
**Execution Flow Debugging:**
- Step-by-step DAX execution simulation
- Calculation order analysis and optimization
- Function call hierarchy and parameter evaluation
- Performance bottleneck identification in execution path",

            "error_tracing" => @"
**Error Source Tracing:**
- Root cause analysis of DAX errors and unexpected results
- Error propagation tracking through calculation chains
- Data type mismatch and conversion issue identification
- Null value handling and edge case analysis",

            _ => @"
**Comprehensive Debugging Approach:**
- Multi-faceted analysis combining all debugging techniques
- Systematic problem identification and resolution strategies"
        };

        var depthGuidance = depth.ToLower() switch
        {
            "quick" => @"
**Quick Analysis:**
- Rapid identification of most common DAX issues
- Essential debugging steps and immediate fixes
- Focus on high-probability root causes",

            "detailed" => @"
**Detailed Analysis:**
- Systematic step-by-step debugging process
- Comprehensive variable and context inspection
- Multiple test scenarios and validation approaches",

            "comprehensive" => @"
**Comprehensive Analysis:**
- Complete DAX expression analysis with all possible scenarios
- Advanced debugging patterns and edge case testing
- Full dependency mapping and optimization recommendations
- Documentation of findings and resolution patterns",

            _ => @"
**Standard Analysis:**
- Balanced debugging approach with practical solutions"
        };

        var visualCalcGuidance = includeVisualCalc
            ? @"
**Visual Calculation Simulation:**

```dax
// Debug Variable Values
Debug Variables =
VAR Step1 = [YourVariable1]
VAR Step2 = [YourVariable2]
VAR Step3 = Step1 + Step2
RETURN
    ""Step1: "" & Step1 &
    "" | Step2: "" & Step2 &
    "" | Result: "" & Step3

// Debug Filter Context
Debug Context =
VAR CurrentFilters = CONCATENATEX(FILTERS(), [Column] & ""="" & [Value], "", "")
VAR RowContext = CONCATENATEX(VALUES([Key]), [Key], "", "")
RETURN ""Filters: "" & CurrentFilters & "" | Rows: "" & RowContext
```"
            : "";

        return $@"{basePrompt}

{approachGuidance}

{depthGuidance}

{visualCalcGuidance}

**Systematic Debugging Framework:**

1. **Problem Identification:**
   - Document expected vs actual behavior
   - Identify specific scenarios where issues occur
   - Gather sample data and filter contexts

2. **Step-by-Step Analysis:**
   - Break down complex expressions into components
   - Test each component independently
   - Validate intermediate results

3. **Context Investigation:**
   - Analyze filter context at each calculation level
   - Test with different filter combinations
   - Verify relationship behavior and propagation

4. **Resolution and Validation:**
   - Implement fixes based on findings
   - Test across multiple scenarios
   - Document solution for future reference

Please provide step-by-step debugging assistance for the following DAX issue using {approach} approach with {depth} analysis:";
    }

    /// <summary>
    /// Comprehensive DAX analysis with optimization recommendations and performance insights.
    /// </summary>
    /// <param name="focus">Analysis focus: performance, readability, maintainability, modern_patterns</param>
    /// <param name="includeAlternatives">Include alternative implementation suggestions</param>
    /// <param name="modelSize">Target model size: small, medium, large, enterprise</param>
    /// <returns>A specialized prompt for comprehensive DAX optimization analysis</returns>
    [McpServerPrompt(Name = "analyze_dax_optimization"), Description("Comprehensive DAX analysis with optimization recommendations")]
    public static string AnalyzeDaxOptimization(
        [Description("Analysis focus: performance (speed optimization), readability (maintainability), maintainability (long-term support), modern_patterns (latest DAX features)")] string focus,
        [Description("Include alternative implementation suggestions")] bool includeAlternatives = true,
        [Description("Target model size: small (< 100MB), medium (100MB-1GB), large (1-10GB), enterprise (> 10GB)")] string modelSize = "medium")
    {
        var basePrompt = @"You are a DAX optimization expert with deep knowledge of performance patterns, modern DAX features, and enterprise-scale implementation best practices.";

        var focusGuidance = focus.ToLower() switch
        {
            "performance" => @"
**Performance Optimization Focus:**
- Query execution time reduction strategies
- Memory usage optimization and storage engine efficiency
- Iterator function optimization and context transition analysis
- Relationship leveraging for optimal filter propagation
- Advanced performance patterns and anti-pattern identification",

            "readability" => @"
**Readability Enhancement Focus:**
- Code structure improvement and logical organization
- Variable naming conventions and documentation standards
- Expression simplification without performance impact
- Modular design patterns for complex calculations
- Self-documenting code practices and commenting strategies",

            "maintainability" => @"
**Maintainability Improvement Focus:**
- Long-term code sustainability and modification ease
- Dependency reduction and modular design patterns
- Standardization across measure implementations
- Error handling and edge case management
- Future-proofing against model changes and requirements evolution",

            "modern_patterns" => @"
**Modern DAX Patterns Focus:**
- Latest DAX function utilization and optimization
- Advanced time intelligence and calculation groups
- Modern aggregation patterns and virtual relationships
- Best practice implementation of new DAX features
- Migration from legacy patterns to modern approaches",

            _ => @"
**Comprehensive Analysis Focus:**
- Balanced optimization across all dimensions
- Holistic improvement recommendations"
        };

        var modelSizeGuidance = modelSize.ToLower() switch
        {
            "small" => @"
**Small Model Optimization (< 100MB):**
- Focus on readability and maintainability over micro-optimizations
- Emphasis on clear, understandable code patterns
- Basic performance considerations without over-engineering",

            "medium" => @"
**Medium Model Optimization (100MB-1GB):**
- Balanced approach to performance and maintainability
- Moderate optimization strategies for better user experience
- Performance monitoring and bottleneck identification",

            "large" => @"
**Large Model Optimization (1-10GB):**
- Advanced performance optimization strategies
- Memory usage optimization and efficient filter context management
- Iterator function optimization and relationship efficiency",

            "enterprise" => @"
**Enterprise Model Optimization (> 10GB):**
- Maximum performance optimization with enterprise-scale considerations
- Advanced caching strategies and computation optimization
- Sophisticated error handling and edge case management
- Scalability and concurrent user impact analysis",

            _ => @"
**General Model Optimization:**
- Adaptable strategies suitable for various model sizes"
        };

        var alternativeGuidance = includeAlternatives
            ? @"
**Alternative Implementation Analysis:**

1. **Pattern Alternatives:**
   - Compare multiple approaches for the same calculation
   - Analyze trade-offs between different DAX patterns
   - Suggest modern alternatives to legacy implementations

2. **Performance Alternatives:**
   - Provide faster execution alternatives
   - Compare iterator vs non-iterator approaches
   - Suggest relationship-based vs calculation-based solutions

3. **Maintenance Alternatives:**
   - Offer more maintainable code structures
   - Suggest modular alternatives for complex calculations
   - Provide standardized patterns for common scenarios"
            : "";

        return $@"{basePrompt}

{focusGuidance}

{modelSizeGuidance}

{alternativeGuidance}

**Comprehensive Analysis Framework:**

1. **Current State Assessment:**
   - Expression complexity analysis and pattern identification
   - Performance characteristics and bottleneck detection
   - Maintainability and readability evaluation

2. **Optimization Opportunities:**
   - Performance improvement recommendations
   - Code structure enhancement suggestions
   - Modern pattern adoption opportunities

3. **Implementation Roadmap:**
   - Prioritized optimization steps
   - Risk assessment and mitigation strategies
   - Testing and validation approaches

4. **Quality Assurance:**
   - Best practice compliance verification
   - Future-proofing and scalability considerations
   - Documentation and knowledge transfer recommendations

Please analyze and optimize the following DAX expression with {focus} focus for a {modelSize} model:";
    }

    /// <summary>
    /// Analysis-based test scenario recommendations for DAX measures with comprehensive coverage strategies
    /// focused on technical validation and PowerBI model behavior.
    /// </summary>
    /// <param name="analysisType">Analysis type: coverage, edge_cases, performance_impact, validation</param>
    /// <param name="measureType">Measure type: aggregation, time_intelligence, ranking, statistical, general</param>
    /// <param name="includeTestPatterns">Include recommended test patterns</param>
    /// <returns>A specialized prompt for generating comprehensive test scenarios based on DAX analysis</returns>
    [McpServerPrompt(Name = "analyze_test_scenarios"), Description("Technical test scenario recommendations for DAX measures and PowerBI model validation")]
    public static string AnalyzeTestScenarios(
        [Description("Analysis type: coverage (comprehensive testing), edge_cases (boundary conditions), performance_impact (scalability testing), validation (accuracy verification)")] string analysisType,
        [Description("Measure type: aggregation (sums/counts), time_intelligence (periods/growth), ranking (topN/percentiles), statistical (averages/distributions), general")] string measureType = "general",
        [Description("Include recommended test patterns")] bool includeTestPatterns = true)
    {
        var basePrompt = @"You are a DAX testing and validation expert specializing in comprehensive test scenario design, edge case identification, and technical validation for PowerBI solutions.";

        var analysisGuidance = analysisType.ToLower() switch
        {
            "coverage" => @"
**Comprehensive Coverage Testing:**
- Complete functional test scenario identification
- Cross-table integration testing strategies
- Multi-dimensional filter context validation
- Hierarchical aggregation behavior verification
- Relationship propagation testing across the model",

            "edge_cases" => @"
**Edge Case and Boundary Testing:**
- Null value handling and empty table scenarios
- Division by zero and mathematical edge cases
- Date boundary conditions and invalid periods
- Filter context edge cases and unexpected combinations
- Data type conversion and overflow scenarios",

            "performance_impact" => @"
**Performance Impact Testing:**
- Scalability testing with varying data volumes
- Memory usage patterns under different loads
- Query execution time validation across scenarios
- Bottleneck identification in complex calculations
- Storage engine vs calculation engine optimization",

            "validation" => @"
**Accuracy and Validation Testing:**
- Mathematical precision and calculation accuracy
- Cross-validation with expected results
- Data consistency and integrity verification
- Context transition behavior validation
- Measure dependency chain testing",

            _ => @"
**Comprehensive Testing Analysis:**
- Multi-faceted testing approach covering all critical technical aspects"
        };

        var measureTypeGuidance = measureType.ToLower() switch
        {
            "aggregation" => @"
**Aggregation Measure Testing:**
- Sum, count, and average calculation validation
- Distinct count accuracy and performance testing
- Conditional aggregation behavior verification
- Filter context impact on aggregation results
- Empty and partial dataset handling",

            "time_intelligence" => @"
**Time Intelligence Testing:**
- YTD, QTD, MTD calculation accuracy
- Period-over-period comparison validation
- Fiscal year boundary condition testing
- Date table relationship dependency verification
- Time-based filter context behavior",

            "ranking" => @"
**Ranking Measure Testing:**
- RANKX function accuracy across different contexts
- TOPN filtering behavior and edge cases
- Tie-breaking logic validation
- Performance with large datasets
- Hierarchy-level ranking consistency",

            "statistical" => @"
**Statistical Measure Testing:**
- Median, percentile calculation accuracy
- Distribution analysis validation
- Statistical significance testing
- Outlier handling and data quality impact
- Sample size and confidence interval testing",

            _ => @"
**General Measure Testing:**
- Universal testing patterns for any measure type
- Standard calculation validation approaches
- Common aggregation and filtering scenarios"
        };

        var testPatternGuidance = includeTestPatterns
            ? @"
**Recommended Test Patterns:**

1. **Boundary Value Testing:**
   ```dax
   // Test minimum and maximum values
   Test_MinMax =
   VAR MinValue = MIN([YourMeasure])
   VAR MaxValue = MAX([YourMeasure])
   RETURN ""Min: "" & MinValue & "" | Max: "" & MaxValue
   
   // Test zero and negative values
   Test_ZeroNegative =
   CALCULATE([YourMeasure], FILTER(ALL(Table), [Value] <= 0))
   ```

2. **Filter Context Validation:**
   ```dax
   // Test different filter combinations
   Test_FilterCombos =
   VAR NoFilter = CALCULATE([YourMeasure], ALL(Table))
   VAR WithFilter = [YourMeasure]
   RETURN ""Unfiltered: "" & NoFilter & "" | Filtered: "" & WithFilter
   ```

3. **Context Transition Testing:**
   ```dax
   // Test context behavior
   Test_Context =
   VAR RowContext = HASONEVALUE([Column])
   VAR FilterContext = COUNTROWS(VALUES([Column]))
   RETURN ""HasOneValue: "" & RowContext & "" | ValueCount: "" & FilterContext
   ```

4. **Relationship Testing:**
   ```dax
   // Test cross-table filtering
   Test_Relationships =
   VAR DirectValue = [YourMeasure]
   VAR CrossFilterValue = CALCULATE([YourMeasure], ALL([RelatedTable]))
   RETURN ""Direct: "" & DirectValue & "" | CrossFilter: "" & CrossFilterValue
   ```"
            : "";

        return $@"{basePrompt}

{analysisGuidance}

{measureTypeGuidance}

{testPatternGuidance}

**Technical Test Framework:**

1. **Model Structure Analysis:**
   - Table relationship dependency mapping
   - Filter propagation path verification
   - Security context and RLS impact testing
   - Data lineage and source validation

2. **Performance Validation:**
   - Query execution plan analysis
   - Memory usage optimization testing
   - Concurrent user impact assessment
   - Scalability threshold identification

3. **Data Quality Testing:**
   - Missing value handling verification
   - Data type consistency validation
   - Referential integrity checking
   - Calculation accuracy across data ranges

4. **Context Behavior Testing:**
   - Filter context transition validation
   - Visual context aggregation testing
   - Hierarchy drilling behavior verification
   - Cross-filtering impact assessment

5. **Regression Testing:**
   - Model change impact analysis
   - Measure dependency chain validation
   - Performance regression identification
   - Calculation result consistency verification

Please analyze and recommend comprehensive test scenarios for the following DAX measure using {analysisType} analysis for {measureType} measures:";
    }
}
