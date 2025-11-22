// bookscraper.cli/ErrorHandling/GlobalExecutor.cs
using System;
using System.Threading;
using Bookscraper.Cli.Interfaces;
using Bookscraper.Core.ErrorHandling;
using Bookscraper.Core.Interfaces;
using Bookscraper.Core.Models;
using Bookscraper.Core.Services;
using Serilog;

namespace Bookscraper.Cli.ErrorHandling
{
    public sealed class GlobalExecutor : IGlobalExecutor
    {
        private readonly IGlobalErrorHandler _errorHandler;
        private readonly ErrorCatalogMappingProvider _errorCatalog;
        private readonly RetryPolicy _retryPolicy; // TODO: align with final IRetryPolicy interface
        private readonly ILogger _logger;

        public GlobalExecutor(
            IGlobalErrorHandler errorHandler,
            ErrorCatalogMappingProvider errorCatalog,
            RetryPolicy retryPolicy,
            ILogger logger)
        {
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
            _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the given entry point under a single, global try/catch boundary.
        /// All retry decisions and final exit codes are derived from the YAML error contracts.
        /// </summary>
        public int Execute(Func<int> entryPoint)
        {
            if (entryPoint == null)
            {
                throw new ArgumentNullException(nameof(entryPoint));
            }

            int attempt = 0;

            while (true)
            {
                try
                {
                    // No try/catch anywhere else; this is the central guard.
                    int exitCode = entryPoint();
                    return exitCode;
                }
                catch (Exception ex)
                {
                    bool isTransient = _errorCatalog.IsTransient(ex);
                    bool shouldRetry = isTransient && _retryPolicy.ShouldRetry(ex, attempt);

                    if (shouldRetry)
                    {
                        TimeSpan delay = _retryPolicy.GetDelay(attempt);

                        _logger.Warning(
                            ex,
                            "Transient failure on attempt {Attempt}; retrying after {Delay}.",
                            attempt + 1,
                            delay);

                        attempt++;

                        // NOTE: This is the only place in the process where sleeping/backoff is applied.
                        Thread.Sleep(delay);
                        continue;
                    }

                    // No more retries (or non-transient): normalize via global error handler.
                    ErrorResponse error = _errorHandler.HandleException(ex);
                    int exitCode = CliExitCodeMapper.ToExitCode(error);

                    _logger.Error(
                        "Exiting with {ExitCode} due to {ErrorCode}.",
                        exitCode,
                        error.ErrorCode);

                    Log.CloseAndFlush();
                    return exitCode;
                }
            }
        }
    }
}