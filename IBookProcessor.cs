// bookscraper.core/Interfaces/IBookProcessor.cs
using System.Threading.Tasks;
using Bookscraper.Core.Models;

namespace Bookscraper.Core.Interfaces
{
    /// <summary>
    /// Contract for processing a single book (all of its pages) into YAML output.
    /// </summary>
    public interface IBookProcessor
    {
        /// <summary>
        /// Processes all pages for a single book rooted at <paramref name="bookRootPath"/>.
        /// </summary>
        /// <param name="options">Book processing configuration.</param>
        /// <param name="bookRootPath">Directory containing the page images for a single book.</param>
        Task ProcessBookAsync(BookProcessingOptions options, string bookRootPath);
    }
}