// bookscraper.core/Serialization/YamlSerializer.cs
using System;
using System.Collections.Generic;
using System.IO;
using Bookscraper.Core.ErrorHandling;
using Bookscraper.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bookscraper.Core.Serialization
{
    /// <summary>
    /// Serializes and deserializes <see cref="Page"/> models to and from YAML.
    /// No local try/catch — failures bubble into the global handler.
    /// </summary>
    public sealed class YamlSerializer
    {
        private const string YamlSerializationErrorCode = "ERR.YAML_SERIALIZATION_FAILED";

        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        /// <summary>
        /// Creates a new <see cref="YamlSerializer"/> using a standard
        /// YamlDotNet configuration with deterministic, camel-cased output.
        /// </summary>
        public YamlSerializer()
        {
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        /// <summary>
        /// Writes the provided <see cref="Page"/> instance to <paramref name="outputPath"/>
        /// in YAML format. Creates directories as needed.
        /// </summary>
        public void WritePage(Page page, string outputPath)
        {
            if (page is null) throw new ArgumentNullException(nameof(page));
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var contract = ToContract(page);
            string yaml = _serializer.Serialize(contract);
            File.WriteAllText(outputPath, yaml);
        }

        /// <summary>
        /// Reads a YAML payload representing a <see cref="Page"/> and returns a fully
        /// constructed <see cref="Page"/> instance with validated required fields.
        /// </summary>
        /// <param name="yaml">The YAML content to deserialize.</param>
        /// <returns>A <see cref="Page"/> instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="yaml"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="YamlSerializationException">
        /// Thrown when required fields (page number, images, book name) are missing
        /// or invalid in the YAML payload.
        /// </exception>
        public Page DeserializePage(string yaml)
        {
            if (yaml is null) throw new ArgumentNullException(nameof(yaml));

            var contract = _deserializer.Deserialize<PageYamlContract>(yaml);
            if (contract is null)
            {
                throw new YamlSerializationException(
                    "Deserialized Page contract was null.",
                    innerException: null,
                    errorCode: YamlSerializationErrorCode);
            }

            if (contract.PageNumber <= 0)
            {
                throw new YamlSerializationException(
                    "PageNumber must be greater than zero in YAML payload.",
                    innerException: null,
                    errorCode: YamlSerializationErrorCode);
            }

            if (contract.Base64Images is null || contract.Base64Images.Count == 0)
            {
                throw new YamlSerializationException(
                    "Base64Images must contain at least one entry in YAML payload.",
                    innerException: null,
                    errorCode: YamlSerializationErrorCode);
            }

            if (string.IsNullOrWhiteSpace(contract.BookName))
            {
                throw new YamlSerializationException(
                    "BookName must not be null, empty, or whitespace in YAML payload.",
                    innerException: null,
                    errorCode: YamlSerializationErrorCode);
            }

            // Construct Page using the canonical constructor, ensuring that the
            // enhanced metadata fields are wired correctly. Nullable numeric fields
            // (TotalPages, LocationCurrent, LocationTotal) are passed through as-is.
            var page = new Page(
                pageNumber: contract.PageNumber,
                base64Images: contract.Base64Images,
                bookName: contract.BookName,
                totalPages: contract.TotalPages,
                locationCurrent: contract.LocationCurrent,
                locationTotal: contract.LocationTotal);

            // Rehydrate OCR metadata only if all required pieces are present.
            if (!string.IsNullOrWhiteSpace(contract.OcrText) &&
                !string.IsNullOrWhiteSpace(contract.OcrEngine) &&
                !string.IsNullOrWhiteSpace(contract.Language) &&
                contract.ProcessedTimestamp.HasValue)
            {
                page.ApplyOcrResult(
                    ocrText: contract.OcrText,
                    ocrEngine: contract.OcrEngine,
                    language: contract.Language,
                    processedTimestamp: contract.ProcessedTimestamp.Value,
                    currentPage: contract.PageNumber);
            }

            return page;
        }

        /// <summary>
        /// Reads a YAML file from disk and deserializes it into a validated
        /// <see cref="Page"/> instance.
        /// </summary>
        /// <param name="inputPath">Path to the YAML file on disk.</param>
        /// <returns>A <see cref="Page"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="inputPath"/> is null, empty, or whitespace.
        /// </exception>
        public Page ReadPageFromFile(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("Input path must be provided.", nameof(inputPath));
            }

            var yaml = File.ReadAllText(inputPath);
            return DeserializePage(yaml);
        }

        private static PageYamlContract ToContract(Page page)
        {
            return new PageYamlContract
            {
                // Order here defines the deterministic YAML field ordering.
                PageNumber = page.PageNumber,
                Base64Images = page.Base64Images,
                TotalPages = page.TotalPages,
                LocationCurrent = page.LocationCurrent,
                LocationTotal = page.LocationTotal,
                BookName = page.BookName,
                OcrText = page.OcrText,
                ProcessedTimestamp = page.ProcessedTimestamp,
                OcrEngine = page.OcrEngine,
                Language = page.Language
            };
        }

        /// <summary>
        /// Internal YAML contract used to control field ordering and explicit
        /// inclusion of the enhanced metadata fields (TotalPages, locations, BookName).
        /// </summary>
        private sealed class PageYamlContract
        {
            public int PageNumber { get; set; }

            public IDictionary<string, string> Base64Images { get; set; }
                = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public int? TotalPages { get; set; }

            public int? LocationCurrent { get; set; }

            public int? LocationTotal { get; set; }

            public string BookName { get; set; } = string.Empty;

            public string? OcrText { get; set; }

            public DateTime? ProcessedTimestamp { get; set; }

            public string? OcrEngine { get; set; }

            public string? Language { get; set; }
        }
    }
}
