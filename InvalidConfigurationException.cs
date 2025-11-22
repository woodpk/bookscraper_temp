// bookscraper.core/ErrorHandling/InvalidConfigurationException.cs
using System;
using System.Text;

namespace Bookscraper.Core.ErrorHandling
{
    public sealed class InvalidConfigurationException : Exception
    {
        public string ConfigurationDetails { get; }

        public InvalidConfigurationException(string message, string configurationDetails, Exception? innerException = null)
            : base(message, innerException)
        {
            ConfigurationDetails = configurationDetails ?? throw new ArgumentNullException(nameof(configurationDetails));
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(base.ToString());
            builder.AppendLine($"ConfigurationDetails: {ConfigurationDetails}");
            return builder.ToString();
        }
    }
}