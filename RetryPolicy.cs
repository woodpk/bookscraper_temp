// bookscraper.core/Services/RetryPolicy.cs
using System;
using Bookscraper.Core.Interfaces;

namespace Bookscraper.Core.Services
{
    /// <summary>
    /// Deterministic retry policy: bounded attempts + exponential backoff.
    /// Classification of transient errors is handled by higher-level components.
    /// </summary>
    public sealed class RetryPolicy : IRetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;

        public RetryPolicy(int maxRetries, TimeSpan baseDelay)
        {
            if (maxRetries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must not be negative.");
            }

            if (baseDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDelay), "baseDelay must not be negative.");
            }

            _maxRetries = maxRetries;
            _baseDelay = baseDelay;
        }

        public int MaxRetries => _maxRetries;
        public TimeSpan BaseDelay => _baseDelay;

        
        /// <summary>
        /// Determines whether the given failure should be retried, based on
        /// the current attempt number and the centralized error catalog.
        /// </summary>
        /// <param name="exception">The exception that was thrown.</param>
        /// <param name="attemptNumber">
        /// Zero-based attempt number (0 for first failure, 1 for second, etc.).
        /// </param>
        public bool ShouldRetry(Exception exception, int attemptNumber)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (attemptNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), "attemptNumber must not be negative.");
            }

            return attemptNumber < _maxRetries;
        }

        
        /// <summary>
        /// Computes the delay before the next retry attempt.
        /// </summary>
        /// <param name="attemptNumber">
        /// Zero-based attempt number (0 for first retry delay, 1 for second, etc.).
        /// </param>
        public TimeSpan GetDelay(int attemptNumber)
        {
            if (attemptNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), "attemptNumber must not be negative.");
            }

            // Deterministic exponential backoff: baseDelay * 2^attemptNumber.
            double multiplier = Math.Pow(2, attemptNumber);
            double milliseconds = _baseDelay.TotalMilliseconds * multiplier;

            if (milliseconds > TimeSpan.MaxValue.TotalMilliseconds)
            {
                return TimeSpan.MaxValue;
            }

            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}
