// bookscraper.core/ErrorHandling/ImageProcessingException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    public sealed class ImageProcessingException : Exception
    {
        public string ImagePath { get; }

        public ImageProcessingException(string message, Exception? innerException, string imagePath)
            : base(message, innerException)
        {
            ImagePath = imagePath ?? throw new ArgumentNullException(nameof(imagePath));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"ImagePath: {ImagePath}");
            return builder.ToString();
        }
    }
}