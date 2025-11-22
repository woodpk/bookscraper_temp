// bookscraper.core/Decorators/IErrorHandlingDecorator.cs
using System;

namespace Bookscraper.Core.Decorators;

public interface IErrorHandlingDecorator
{
    T Invoke<T>(Func<T> operation);
}