// bookscraper.core/Models/BookProcessingOptions.cs
using System;
using System.Text.Json.Serialization;
using Bookscraper.Core.ErrorHandling;

namespace Bookscraper.Core.Models
{
    public sealed class BookProcessingOptions
    {
        [JsonPropertyName("input_directory")]
        public string InputDirectory { get; set; } = string.Empty;

        [JsonPropertyName("output_directory")]
        public string OutputDirectory { get; set; } = string.Empty;

        [JsonPropertyName("ocr_engine")]
        public string OcrEngine { get; set; } = "Tesseract";

        [JsonPropertyName("language")]
        public string Language { get; set; } = "eng";

        [JsonPropertyName("enable_logging")]
        public bool EnableLogging { get; set; } = true;

        [JsonPropertyName("max_retries")]
        public int MaxRetries { get; set; } = 3;

        [JsonPropertyName("retry_delay")]
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

        public BookProcessingOptions()
        {
        }

        public BookProcessingOptions(
            string inputDirectory,
            string outputDirectory,
            string ocrEngine,
            string language,
            bool enableLogging,
            int maxRetries,
            TimeSpan retryDelay)
        {
            InputDirectory = inputDirectory ?? throw new ArgumentNullException(nameof(inputDirectory));
            OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            OcrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
            Language = language ?? throw new ArgumentNullException(nameof(language));
            EnableLogging = enableLogging;
            MaxRetries = maxRetries;
            RetryDelay = retryDelay;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(InputDirectory))
            {
                throw new InvalidConfigurationException(
                    "Input directory is required.",
                    "BookProcessingOptions.InputDirectory was null, empty, or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                throw new InvalidConfigurationException(
                    "Output directory is required.",
                    "BookProcessingOptions.OutputDirectory was null, empty, or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(OcrEngine))
            {
                throw new InvalidConfigurationException(
                    "OCR engine is required.",
                    "BookProcessingOptions.OcrEngine was null, empty, or whitespace.");
            }

            if (string.IsNullOrWhiteSpace(Language))
            {
                throw new InvalidConfigurationException(
                    "OCR language is required.",
                    "BookProcessingOptions.Language was null, empty, or whitespace.");
            }

            if (MaxRetries < 0)
            {
                throw new InvalidConfigurationException(
                    "MaxRetries must not be negative.",
                    $"BookProcessingOptions.MaxRetries was {MaxRetries}.");
            }

            if (RetryDelay < TimeSpan.Zero)
            {
                throw new InvalidConfigurationException(
                    "RetryDelay must not be negative.",
                    $"BookProcessingOptions.RetryDelay was {RetryDelay}.");
            }

            // NOTE: The contract indicates MaxRetries can be optional; treating 0 as "no retries" respects that
            // without changing the property type to a nullable integer.
        }

        public override string ToString()
        {
            return $"InputDirectory: {InputDirectory}, OutputDirectory: {OutputDirectory}, OcrEngine: {OcrEngine}, Language: {Language}, EnableLogging: {EnableLogging}, MaxRetries: {MaxRetries}, RetryDelay: {RetryDelay}";
        }
    }
}
