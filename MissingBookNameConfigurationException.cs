// bookscraper.core/ErrorHandling/MissingBookNameConfigurationException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    /// <summary>
    /// Configuration error thrown when a logical book name cannot be derived
    /// from the filesystem layout (book root path and/or image path).
    /// </summary>
    public sealed class MissingBookNameConfigurationException : Exception
    {
        /// <summary>
        /// The book root path used for processing (as supplied to the pipeline).
        /// </summary>
        public string BookRootPath { get; }

        /// <summary>
        /// The specific image path being processed when the failure occurred, if any.
        /// </summary>
        public string? ImagePath { get; }

        public MissingBookNameConfigurationException(
            string message,
            string bookRootPath,
            string? imagePath,
            Exception? innerException = null)
            : base(message, innerException)
        {
            BookRootPath = bookRootPath ?? throw new ArgumentNullException(nameof(bookRootPath));
            ImagePath = imagePath;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"BookRootPath: {BookRootPath}");
            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                builder.AppendLine($"ImagePath: {ImagePath}");
            }

            return builder.ToString();
        }
    }
}