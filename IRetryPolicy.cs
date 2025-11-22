// bookscraper.core/Interfaces/IRetryPolicy.cs
using System;

namespace Bookscraper.Core.Interfaces
{
    public interface IRetryPolicy
    {
        int MaxRetries { get; }
        TimeSpan BaseDelay { get; }

        /// <summary>
        /// Returns true if another retry should be attempted for the given failure
        /// and attempt number (0-based).
        /// </summary>
        bool ShouldRetry(Exception exception, int attemptNumber);

        /// <summary>
        /// Returns the delay before the next retry attempt for the given
        /// attempt number (0-based).
        /// </summary>
        TimeSpan GetDelay(int attemptNumber);
    }
}
