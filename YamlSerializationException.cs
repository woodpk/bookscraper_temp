// bookscraper.core/ErrorHandling/YamlSerializationException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    public sealed class YamlSerializationException : Exception
    {
        public string ErrorCode { get; }

        public YamlSerializationException(string message, Exception? innerException, string errorCode)
            : base(message, innerException)
        {
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"ErrorCode: {ErrorCode}");
            return builder.ToString();
        }
    }
}