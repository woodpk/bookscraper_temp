// bookscraper.core/Services/BookBatchProcessor.cs
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bookscraper.Core.Interfaces;
using Bookscraper.Core.Models;

namespace Bookscraper.Core.Services
{
    /// <summary>
    /// Coordinates batch processing of multiple books by delegating to <see cref="IBookProcessor"/>.
    /// </summary>
    public sealed class BookBatchProcessor : IBookBatchProcessor
    {
        private readonly IBookProcessor _bookProcessor;

        /// <summary>
        /// Creates a new <see cref="BookBatchProcessor"/>.
        /// </summary>
        public BookBatchProcessor(IBookProcessor bookProcessor)
        {
            _bookProcessor = bookProcessor ?? throw new ArgumentNullException(nameof(bookProcessor));
        }

        /// <inheritdoc />
        public async Task ProcessAllBooksAsync(BookProcessingOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.InputDirectory))
            {
                throw new ArgumentException(
                    "BookProcessingOptions.InputDirectory must be provided.",
                    nameof(options));
            }

            if (!Directory.Exists(options.InputDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Input directory '{options.InputDirectory}' does not exist.");
            }

            var bookDirectories = Directory
                .GetDirectories(options.InputDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var bookRoot in bookDirectories)
            {
                await _bookProcessor
                    .ProcessBookAsync(options, bookRoot)
                    .ConfigureAwait(false);
            }
        }
    }
}