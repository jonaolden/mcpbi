using System;
using System.Runtime.Serialization;
using Microsoft.AnalysisServices.AdomdClient;

namespace pbi_local_mcp.Core
{
    /// <summary>
    /// Represents errors that occur during DAX or DMV query execution.
    /// This exception provides access to the original query and its type.
    /// </summary>
    [Serializable]
    public class DaxQueryExecutionException : Exception // Inherit from System.Exception
    {
        /// <summary>
        /// Gets the query text that caused the exception.
        /// </summary>
        public string? Query { get; }

        /// <summary>
        /// Gets the type of the query (DAX or DMV) that caused the exception.
        /// </summary>
        public QueryType QueryType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaxQueryExecutionException"/> class.
        /// </summary>
        public DaxQueryExecutionException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaxQueryExecutionException"/> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="query">The query text that caused the exception.</param>
        /// <param name="queryType">The type of the query.</param>
        public DaxQueryExecutionException(string message, string? query, QueryType queryType)
            : base(message)
        {
            Query = query;
            QueryType = queryType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaxQueryExecutionException"/> class
        /// with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        /// <param name="query">The query text that caused the exception.</param>
        /// <param name="queryType">The type of the query.</param>
        public DaxQueryExecutionException(string message, Exception? innerException, string? query, QueryType queryType)
            : base(message, innerException)
        {
            Query = query;
            QueryType = queryType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaxQueryExecutionException"/> class
        /// with a reference to the inner AdomdException that is the cause of this exception.
        /// The message from the inner AdomdException will be used.
        /// </summary>
        /// <param name="adomdInnerException">The AdomdException that is the cause of the current exception.</param>
        /// <param name="query">The query text that caused the exception.</param>
        /// <param name="queryType">The type of the query.</param>
        public DaxQueryExecutionException(AdomdException adomdInnerException, string? query, QueryType queryType)
            : base(adomdInnerException?.Message, adomdInnerException)
        {
            Query = query;
            QueryType = queryType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaxQueryExecutionException"/> class
        /// with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected DaxQueryExecutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Query = info.GetString(nameof(Query));
            QueryType = (QueryType)info.GetInt32(nameof(QueryType));
        }

        /// <summary>
        /// Sets the <see cref="SerializationInfo"/> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Query), Query);
            info.AddValue(nameof(QueryType), (int)QueryType);
        }
    }
}