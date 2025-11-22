// bookscraper.cli/Commands/ProcessBookCommand.cs
using System.Threading.Tasks;
using Bookscraper.Cli.Interfaces;
using Bookscraper.Core.Interfaces;
using Bookscraper.Core.Models;

namespace Bookscraper.Cli.Commands
{
    public sealed class ProcessBookCommand
    {
        private readonly IBookProcessor _bookProcessor;
        private readonly IGlobalExecutor _globalExecutor;
        private readonly IGlobalErrorHandler _errorHandler;

        public ProcessBookCommand(
            IBookProcessor bookProcessor,
            IGlobalExecutor globalExecutor,
            IGlobalErrorHandler errorHandler)
        {
            _bookProcessor = bookProcessor;
            _globalExecutor = globalExecutor;
            _errorHandler = errorHandler;
        }

        public Task<int> ExecuteAsync(BookProcessingOptions options, string bookRootPath)
        {
            // No try/catch here: delegate core work to global executor.
            return Task.FromResult(
                _globalExecutor.Execute(() =>
                {
                    _bookProcessor.ProcessBookAsync(options, bookRootPath)
                        .GetAwaiter()
                        .GetResult();
                    return 0;
                }));
        }

    }
}