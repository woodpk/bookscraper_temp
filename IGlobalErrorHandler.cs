// bookscraper.core/Interfaces/IGlobalErrorHandler.cs
using System;
using Bookscraper.Core.Models;

namespace Bookscraper.Core.Interfaces;

public interface IGlobalErrorHandler
{
    ErrorResponse HandleException(Exception exception);
}