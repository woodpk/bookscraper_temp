// bookscraper.core/ErrorHandling/ErrorCatalogMappingProvider.cs
using System;
using System.Collections.Generic;
using Bookscraper.Core.Models;

namespace Bookscraper.Core.ErrorHandling
{
    /// <summary>
    /// Thin provider that maps exceptions to error codes and metadata using
    /// configuration-driven dictionaries hydrated from the YAML error contracts.
    /// This type does NOT own any hard-coded error catalog data.
    /// </summary>
    public sealed class ErrorCatalogMappingProvider
    {
        private readonly IReadOnlyDictionary<Type, string> _exceptionToErrorCode;
        private readonly IReadOnlyDictionary<string, ErrorMetadata> _errorCodeToMetadata;
        private readonly string _fallbackErrorCode;
        private readonly IReadOnlyDictionary<string, int> _errorCodeToExitCode;
        private readonly int _fallbackExitCode;

        /// /////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Gets the contract-driven mapping from error codes (ERR.*) to CLI exit codes.
        /// This is hydrated from the <c>cli_exit_codes.map</c> sections of the YAML
        /// contracts (common + bookscraper overlays).
        /// </summary>
        public IReadOnlyDictionary<string, int> ErrorCodeToExitCode => _errorCodeToExitCode;

        /// <summary>
        /// Gets the fallback CLI exit code used when an error code is missing or
        /// not present in the <see cref="ErrorCodeToExitCode"/> map.
        /// Typically this is derived from the <c>fallback_error_code</c> entry in
        /// the contracts and its corresponding mapped exit code (for example,
        /// <c>ERR.UNEXPECTED → 1</c>).
        /// </summary>
        public int FallbackExitCode => _fallbackExitCode;
        /// /////////////////////////////////////////////////////////////////////////////



                /// /////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Creates a new mapping provider that exposes:
        ///   - exception → error_code mappings (for building <see cref="ErrorResponse"/>),
        ///   - error_code → metadata (transience, default message, etc.), and
        ///   - error_code → CLI exit code + a fallback exit code for unmapped errors.
        /// </summary>
        /// <remarks>
        /// All dictionaries passed here are expected to be hydrated from the YAML
        /// contracts at startup:
        ///   - <c>contract.common.errors-and-exit-codes.yaml</c>
        ///   - <c>contract.bookscraper.errors-and-exit-codes.yaml</c>
        ///   - <c>contracts.bookscraper.error-mapping.yaml</c>
        ///
        /// The CLI exit-code mapping is typically sourced from the
        /// <c>content.errors_and_exit_codes.mappings.cli_exit_codes.map</c> section of
        /// the common + overlay contracts, with <c>fallbackErrorCode</c> pointing to
        /// the canonical error code used when no explicit mapping exists.
        /// </remarks>
        /// <param name="exceptionToErrorCode">
        /// Map from CLR exception type to error_code
        /// (for example, <c>InvalidConfigurationException → ERR.CONFIG_INVALID_BOOK_OPTIONS</c>).
        /// </param>
        /// <param name="errorCodeToMetadata">
        /// Map from error_code to metadata describing transience and default message text.
        /// </param>
        /// <param name="fallbackErrorCode">
        /// Error code to use when an exception is not explicitly mapped in
        /// <paramref name="exceptionToErrorCode"/>. This is used by
        /// <see cref="MapExceptionToErrorCode"/>.
        /// </param>
        /// <param name="errorCodeToExitCode">
        /// Map from error_code to CLI exit code (0–5 for this tool), built from the
        /// <c>cli_exit_codes.map</c> sections of the contracts.
        /// </param>
        /// <param name="fallbackExitCode">
        /// Fallback CLI exit code to use when an <see cref="ErrorResponse.ErrorCode"/>
        /// does not exist in <paramref name="errorCodeToExitCode"/>. Typically this is
        /// the exit code associated with <paramref name="fallbackErrorCode"/>.
        /// </param>
        public ErrorCatalogMappingProvider(
            IReadOnlyDictionary<Type, string> exceptionToErrorCode,
            IReadOnlyDictionary<string, ErrorMetadata> errorCodeToMetadata,
            string fallbackErrorCode,
            IReadOnlyDictionary<string, int> errorCodeToExitCode,
            int fallbackExitCode)
        {
            if (exceptionToErrorCode is null)
            {
                throw new ArgumentNullException(nameof(exceptionToErrorCode));
            }

            if (errorCodeToMetadata is null)
            {
                throw new ArgumentNullException(nameof(errorCodeToMetadata));
            }

            if (string.IsNullOrWhiteSpace(fallbackErrorCode))
            {
                throw new ArgumentException(
                    "Fallback error code must not be null or whitespace.",
                    nameof(fallbackErrorCode));
            }

            if (errorCodeToExitCode is null)
            {
                throw new ArgumentNullException(nameof(errorCodeToExitCode));
            }

            _exceptionToErrorCode = exceptionToErrorCode;
            _errorCodeToMetadata = errorCodeToMetadata;
            _fallbackErrorCode = fallbackErrorCode;

            _errorCodeToExitCode = errorCodeToExitCode;
            _fallbackExitCode = fallbackExitCode;
        }


        /// <summary>
        /// Maps an exception instance to an error_code defined in the YAML contracts.
        /// </summary>
        public string MapExceptionToErrorCode(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            var exceptionType = exception.GetType();

            if (_exceptionToErrorCode.TryGetValue(exceptionType, out var code) &&
                !string.IsNullOrWhiteSpace(code))
            {
                return code;
            }

            // TODO: If contract requires, walk InnerException or base types here.
            return _fallbackErrorCode;
        }

        /// <summary>
        /// Builds an ErrorResponse using the error catalog metadata for the given exception.
        /// </summary>
        public ErrorResponse BuildErrorResponse(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            var errorCode = MapExceptionToErrorCode(exception);
            var metadata = LookupMetadata(errorCode);

            var message = string.IsNullOrWhiteSpace(metadata.DefaultMessage)
                ? exception.Message
                : metadata.DefaultMessage;

            // NOTE: Details intentionally use exception.ToString() to preserve stack and context.
            return new ErrorResponse(
                errorCode: metadata.ErrorCode,
                errorMessage: message,
                details: exception.ToString());
        }

        /// <summary>
        /// Indicates whether the given exception should be treated as transient (retryable)
        /// according to the YAML-driven error catalog.
        /// </summary>
        public bool IsTransient(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            var errorCode = MapExceptionToErrorCode(exception);
            var metadata = LookupMetadata(errorCode);
            return metadata.IsTransient;
        }

        private ErrorMetadata LookupMetadata(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                throw new ArgumentException("Error code must not be null or whitespace.", nameof(errorCode));
            }

            if (_errorCodeToMetadata.TryGetValue(errorCode, out var metadata))
            {
                return metadata;
            }

            // TODO: Confirm fallback semantics for unknown codes in the YAML contract.
            return new ErrorMetadata(
                errorCode: errorCode,
                isTransient: false,
                defaultMessage: string.Empty);
        }

        /// <summary>
        /// Simple metadata view over an error_code entry from the YAML contracts.
        /// The actual shape must be kept in sync with contract.common / contract.bookscraper.
        /// </summary>
        public sealed class ErrorMetadata
        {
            public string ErrorCode { get; }
            public bool IsTransient { get; }
            public string DefaultMessage { get; }

            public ErrorMetadata(string errorCode, bool isTransient, string defaultMessage)
            {
                if (string.IsNullOrWhiteSpace(errorCode))
                {
                    throw new ArgumentException("Error code must not be null or whitespace.", nameof(errorCode));
                }

                ErrorCode = errorCode;
                IsTransient = isTransient;
                DefaultMessage = defaultMessage ?? string.Empty;
            }
        }
    }
}
