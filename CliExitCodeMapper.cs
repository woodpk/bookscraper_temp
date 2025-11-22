// bookscraper.cli/ErrorHandling/CliExitCodeMapper.cs
using System;
using System.Collections.Generic;
using Bookscraper.Core.Models;

namespace Bookscraper.Cli.ErrorHandling
{
    /// <summary>
    /// Provides a contract-driven mapping from error codes (ERR.*)
    /// to CLI process exit codes.
    /// </summary>
    internal static class CliExitCodeMapper
    {
        private static IReadOnlyDictionary<string, int> _errorCodeToExitCode =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static int _fallbackExitCode = 1;
        private static bool _isConfigured;

        /// <summary>
        /// Configures the mapper with data hydrated from the YAML error/exit mapping contract.
        /// Must be called once at startup before any calls to <see cref="ToExitCode"/>.
        /// </summary>
        /// <param name="errorCodeToExitCode">
        /// Mapping from error_code to exit code, built from contracts.bookscraper.error-mapping.yaml.
        /// </param>
        /// <param name="fallbackExitCode">
        /// Exit code to use when an error code is missing or unmapped.
        /// </param>
        public static void Configure(
            IReadOnlyDictionary<string, int> errorCodeToExitCode,
            int fallbackExitCode)
        {
            _errorCodeToExitCode = errorCodeToExitCode
                ?? throw new ArgumentNullException(nameof(errorCodeToExitCode));

            _fallbackExitCode = fallbackExitCode;
            _isConfigured = true;
        }

        /// <summary>
        /// Maps the specified <see cref="ErrorResponse"/> to a deterministic CLI exit code
        /// using the contract-driven error → exit-code mapping.
        /// </summary>
        /// <param name="error">
        /// The <see cref="ErrorResponse"/> produced by the global error handler; its
        /// <see cref="ErrorResponse.ErrorCode"/> property is used as the lookup key.
        /// </param>
        /// <returns>
        /// The mapped exit code if the error code is known; otherwise the configured
        /// fallback exit code.
        /// </returns>
        public static int ToExitCode(ErrorResponse error)
        {
            if (!_isConfigured)
            {
                throw new InvalidOperationException(
                    "CliExitCodeMapper has not been configured. Call Configure(...) at startup.");
            }

            if (error == null || string.IsNullOrWhiteSpace(error.ErrorCode))
            {
                return _fallbackExitCode;
            }

            return _errorCodeToExitCode.TryGetValue(error.ErrorCode, out var code)
                ? code
                : _fallbackExitCode;
        }
    }
}
