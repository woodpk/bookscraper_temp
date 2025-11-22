// bookscraper.core/ErrorHandling/NetworkConnectionException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    public sealed class NetworkConnectionException : Exception
    {
        public string ServiceUrl { get; }

        public NetworkConnectionException(string message, Exception? innerException, string serviceUrl)
            : base(message, innerException)
        {
            ServiceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"ServiceUrl: {ServiceUrl}");
            return builder.ToString();
        }
    }
}