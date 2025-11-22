// bookscraper.core/Decorators/ErrorHandlingDecorator.cs
using System;

namespace Bookscraper.Core.Decorators
{
    /// <summary>
    /// No local try/catch; this class is deliberately "thin".
    /// All exception handling, classification, and retry decisions
    /// are performed by the global executor + global error handler.
    /// </summary>
    public sealed class ErrorHandlingDecorator : IErrorHandlingDecorator
    {
        public T Invoke<T>(Func<T> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            // NOTE: Do NOT add try/catch here. This must remain a direct call
            // so that all failures flow to the global error management pipeline.
            return operation();
        }
    }
}