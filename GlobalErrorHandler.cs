// bookscraper.core/ErrorHandling/GlobalErrorHandler.cs
using System;
using Bookscraper.Core.Interfaces;
using Bookscraper.Core.Models;
using Serilog;

namespace Bookscraper.Core.ErrorHandling
{
    public sealed class GlobalErrorHandler : IGlobalErrorHandler
    {
        private readonly ILogger _logger;
        private readonly ErrorCatalogMappingProvider _errorCatalog;

        public GlobalErrorHandler(ILogger logger, ErrorCatalogMappingProvider errorCatalog)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
        }

        public ErrorResponse HandleException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            // Use new provider API: BuildErrorResponse handles mapping + metadata + messages
            var response = _errorCatalog.BuildErrorResponse(exception);

            _logger.Error(
                exception,
                "Unhandled exception mapped to {ErrorCode} ({ExceptionType})",
                response.ErrorCode,
                exception.GetType().Name);

            return response;
        }
    }
}