// bookscraper.core/ErrorHandling/OperationTimeoutException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    // NOTE: This is a domain-specific timeout exception, distinct from System.OperationTimeoutException.
    public sealed class OperationTimeoutException : Exception
    {
        public TimeSpan TimeoutDuration { get; }

        public OperationTimeoutException(string message, Exception? innerException, TimeSpan timeoutDuration)
            : base(message, innerException)
        {
            TimeoutDuration = timeoutDuration;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"TimeoutDuration: {TimeoutDuration}");
            return builder.ToString();
        }
    }
}