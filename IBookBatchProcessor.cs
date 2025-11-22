// bookscraper.core/Interfaces/IBookBatchProcessor.cs
using System.Threading.Tasks;
using Bookscraper.Core.Models;

namespace Bookscraper.Core.Interfaces
{
    /// <summary>
    /// Contract for processing multiple books in a batch.
    /// </summary>
    public interface IBookBatchProcessor
    {
        /// <summary>
        /// Processes all books discovered under the input directory
        /// defined in <paramref name="options"/>.
        /// </summary>
        /// <param name="options">Book processing configuration, including input directory.</param>
        Task ProcessAllBooksAsync(BookProcessingOptions options);
    }
}