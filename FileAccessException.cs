// bookscraper.core/ErrorHandling/FileAccessException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    public sealed class FileAccessException : Exception
    {
        public string FilePath { get; }

        public FileAccessException(string message, Exception? innerException, string filePath)
            : base(message, innerException)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"FilePath: {FilePath}");
            return builder.ToString();
        }
    }
}