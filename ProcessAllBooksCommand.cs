// bookscraper.cli/Commands/ProcessAllBooksCommand.cs
using System.Threading.Tasks;
using Bookscraper.Cli.ErrorHandling;
using Bookscraper.Cli.Interfaces;
using Bookscraper.Core.ErrorHandling;
using Bookscraper.Core.Interfaces;
using Bookscraper.Core.Models;
using Bookscraper.Core.Services;

namespace Bookscraper.Cli.Commands
{
    public sealed class ProcessAllBooksCommand
    {
        private readonly IBookBatchProcessor _bookBatchProcessor;
        private readonly IGlobalExecutor _globalExecutor;
        private readonly IGlobalErrorHandler _errorHandler;

        public ProcessAllBooksCommand(
            IBookBatchProcessor bookBatchProcessor,
            IGlobalExecutor globalExecutor,
            IGlobalErrorHandler errorHandler)
        {
            _bookBatchProcessor = bookBatchProcessor;
            _globalExecutor = globalExecutor;
            _errorHandler = errorHandler;
        }
        
        
        public Task<int> ExecuteAsync(BookProcessingOptions options, string bookRootPath)
        {
            // No try/catch here: delegate core work to global executor.
            return Task.FromResult(
                _globalExecutor.Execute(() =>
                {
                    _bookBatchProcessor.ProcessAllBooksAsync(options)
                        .GetAwaiter()
                        .GetResult();
                    return 0;
                }));
        }
        
        
    }
}