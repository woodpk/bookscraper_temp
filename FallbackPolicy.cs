// bookscraper.core/Services/FallbackPolicy.cs
using System;
using Bookscraper.Core.Interfaces;

namespace Bookscraper.Core.Services
{
    /// <summary>
    /// Pure fallback selection — NO try/catch. The caller (decorator/executor)
    /// determines when fallback should run based on the global error pipeline.
    /// </summary>
    public sealed class FallbackPolicy : IFallbackPolicy
    {
        public T ExecuteWithFallback<T>(Func<T> primaryOperation, Func<T> fallbackOperation)
        {
            if (primaryOperation == null) throw new ArgumentNullException(nameof(primaryOperation));
            if (fallbackOperation == null) throw new ArgumentNullException(nameof(fallbackOperation));

            // The caller invokes primary first. If global handling decides fallback is needed,
            // the caller then re-invokes THIS method, which simply calls the fallback.
            return fallbackOperation();
        }
    }
}