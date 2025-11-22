// bookscraper.core/Models/ErrorResponse.cs
namespace Bookscraper.Core.Models
{
    public sealed class ErrorResponse
    {
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public string Details { get; }

        public ErrorResponse(
            string? errorCode = null,
            string? errorMessage = null,
            string? details = null)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode)
                ? "ERR.UNEXPECTED"
                : errorCode;

            ErrorMessage = errorMessage ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public override string ToString()
            => $"ErrorCode: {ErrorCode}, Message: {ErrorMessage}, Details: {Details}";
    }
}