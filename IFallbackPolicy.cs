// bookscraper.core/Interfaces/IFallbackPolicy.cs
using System;

namespace Bookscraper.Core.Interfaces;

public interface IFallbackPolicy
{
    T ExecuteWithFallback<T>(Func<T> primaryOperation, Func<T> fallbackOperation);
}