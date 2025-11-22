// bookscraper.core/Models/Page.cs
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bookscraper.Core.Models;

/// <summary>
/// Represents a single logical page in the data-extraction pipeline:
/// image (Base64) is available immediately; OCR + metadata may be applied later.
/// </summary>
public sealed class Page
{
    /// <summary>
    /// Gets the logical page number within the source book.
    /// This value is 1-based and must always be greater than zero.
    /// </summary>
    public int PageNumber { get; private set; }

    // Property declaration patch
    /// <summary>
    /// Collection of Base64-encoded image payloads for this page,
    /// keyed by a deterministic identifier (e.g., "raw", "deskewed", "binarized").
    /// </summary>
    public IDictionary<string, string> Base64Images { get; }

    /// <summary>
    /// Total number of logical pages in the source book, if known.
    /// This value is populated from reading-application metadata (for example, Kindle HUD footer).
    /// </summary>
    public int? TotalPages { get; private set; }

    /// <summary>
    /// Current location index within the book as reported by the reading application, if known.
    /// </summary>
    public int? LocationCurrent { get; private set; }

    /// <summary>
    /// Total number of locations within the book as reported by the reading application, if known.
    /// </summary>
    public int? LocationTotal { get; private set; }

    /// <summary>
    /// Logical name of the book from which this page was extracted.
    /// Typically derived from the filesystem directory containing the page image(s).
    /// </summary>
    public string? BookName { get; private set; }

    /// <summary>
    /// OCR-extracted text for this page.
    /// Null until the OCR stage of the pipeline has completed successfully.
    /// </summary>
    public string? OcrText { get; private set; }

    /// <summary>
    /// Timestamp (UTC) when OCR processing completed for this page.
    /// Null until OCR has run.
    /// </summary>
    public DateTime? ProcessedTimestamp { get; private set; }

    /// <summary>
    /// Name of the OCR engine used (e.g., "Tesseract").
    /// Null until OCR has run.
    /// </summary>
    public string? OcrEngine { get; private set; }

    /// <summary>
    /// Language code used during OCR (e.g., "eng").
    /// Null until OCR has run.
    /// </summary>
    public string? Language { get; private set; }

    
     /// <summary>
    /// Initializes a new instance of the <see cref="Page"/> class with the required
    /// structural data and optional book-level metadata.
    /// THIS CONSTRUCTOR KEPT TO MAINTAIN BACKWARDS COMPATABILITY
    /// </summary>
    /// <param name="pageNumber">The 1-based logical page number. Must be greater than zero.</param>
    /// <param name="base64Images">
    /// A non-empty dictionary of Base64-encoded image payloads keyed by a deterministic identifier
    /// (for example, "raw", "deskewed", "binarized").
    /// </param>
    public Page(
        int pageNumber,
        IDictionary<string, string> base64Images)
    {
        if (pageNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");

        if (base64Images is null)
            throw new ArgumentNullException(nameof(base64Images));

        if (base64Images.Count == 0)
            throw new ArgumentException("At least one Base64 image entry is required.", nameof(base64Images));

        var materialized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in base64Images)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                throw new ArgumentException("Image key must not be null or whitespace.", nameof(base64Images));

            if (string.IsNullOrWhiteSpace(kvp.Value))
                throw new ArgumentException(
                    $"Base64 image value for key '{kvp.Key}' must not be null or empty.",
                    nameof(base64Images));

            materialized[kvp.Key] = kvp.Value;
        }

        PageNumber = pageNumber;
        Base64Images = materialized;
    }
    
    
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Page"/> class with the required
    /// structural data and optional book-level metadata.
    /// </summary>
    /// <param name="pageNumber">The 1-based logical page number. Must be greater than zero.</param>
    /// <param name="base64Images">
    /// A non-empty dictionary of Base64-encoded image payloads keyed by a deterministic identifier
    /// (for example, "raw", "deskewed", "binarized").
    /// </param>
    /// <param name="bookName">
    /// The logical name of the book from which this page was extracted. Must not be null, empty, or whitespace.
    /// Typically derived from the filesystem.
    /// </param>
    /// <param name="totalPages">
    /// Optional total number of logical pages in the book, as reported by the reading application.
    /// </param>
    /// <param name="locationCurrent">
    /// Optional current location index within the book, as reported by the reading application.
    /// </param>
    /// <param name="locationTotal">
    /// Optional total number of locations within the book, as reported by the reading application.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageNumber"/> is less than or equal to zero.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="base64Images"/> or <paramref name="bookName"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="base64Images"/> is empty, when any key/value pair is invalid,
    /// or when <paramref name="bookName"/> is empty or whitespace.
    /// </exception>
    public Page(
        int pageNumber,
        IDictionary<string, string> base64Images,
        string bookName,
        int? totalPages = null,
        int? locationCurrent = null,
        int? locationTotal = null)
    {
        if (pageNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");

        if (base64Images is null)
            throw new ArgumentNullException(nameof(base64Images));

        if (base64Images.Count == 0)
            throw new ArgumentException("At least one Base64 image entry is required.", nameof(base64Images));

        if (bookName is null)
            throw new ArgumentNullException(nameof(bookName));

        if (string.IsNullOrWhiteSpace(bookName))
            throw new ArgumentException("Book name must not be null, empty, or whitespace.", nameof(bookName));

        var materialized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in base64Images)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                throw new ArgumentException("Image key must not be null or whitespace.", nameof(base64Images));

            if (string.IsNullOrWhiteSpace(kvp.Value))
                throw new ArgumentException(
                    $"Base64 image value for key '{kvp.Key}' must not be null or empty.",
                    nameof(base64Images));

            materialized[kvp.Key] = kvp.Value;
        }

        PageNumber = pageNumber;
        Base64Images = materialized;
        BookName = bookName;
        TotalPages = totalPages;
        LocationCurrent = locationCurrent;
        LocationTotal = locationTotal;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Page"/> class.
    /// This constructor exists primarily to support serialization and tooling scenarios
    /// that require a parameterless constructor. It should not be used directly by
    /// the core data-extraction pipeline.
    /// </summary>
    public Page()
    {
    }

    /// <summary>
    /// Adds or replaces a Base64 image payload for the given key in a deterministic way.
    /// </summary>
    /// <param name="key">Deterministic identifier for the image variant (e.g., "raw", "deskewed").</param>
    /// <param name="base64Image">The Base64-encoded image payload.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="key"/> is null/whitespace or <paramref name="base64Image"/> is null/empty.
    /// </exception>
    public void UpdateBase64Images(string key, string base64Image)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Image key must not be null or whitespace.", nameof(key));

        if (string.IsNullOrWhiteSpace(base64Image))
            throw new ArgumentException("Base64 image must not be null or empty.", nameof(base64Image));

        Base64Images[key] = base64Image;
    }

    /// <summary>
    /// Single, deterministic entry point for applying OCR results to the page.
    /// Used by the data-extraction pipeline (OCR stage).
    /// </summary>
    /// <param name="ocrText">The OCR-extracted text to apply.</param>
    /// <param name="ocrEngine">The name of the OCR engine used.</param>
    /// <param name="language">The language code used by the OCR engine (for example, "eng").</param>
    /// <param name="processedTimestamp">The timestamp when OCR processing completed.</param>
    /// <param name="currentPage">The current page's page number</param>
    /// <exception cref="ArgumentException">
    /// Thrown when any of the required string parameters are null or empty.
    /// </exception>
    public void ApplyOcrResult(string ocrText, string ocrEngine, string language, DateTime processedTimestamp, int currentPage)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            throw new ArgumentException("OCR text must not be null or empty.", nameof(ocrText));

        if (string.IsNullOrWhiteSpace(ocrEngine))
            throw new ArgumentException("OCR engine must not be null or empty.", nameof(ocrEngine));

        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language must not be null or empty.", nameof(language));

        OcrText = ocrText;
        OcrEngine = ocrEngine;
        Language = language;
        ProcessedTimestamp = processedTimestamp.Kind == DateTimeKind.Utc
            ? processedTimestamp
            : processedTimestamp.ToUniversalTime();
        PageNumber = currentPage;
    }

    /// <summary>
    /// Returns a short, human-readable representation of the page, including
    /// the page number and any known OCR engine and language details.
    /// </summary>
    /// <returns>
    /// A string in the form <c>"Page {PageNumber} (Engine={Engine}, Lang={Language})"</c>.
    /// </returns>
    public override string ToString()
    {
        var engine = OcrEngine ?? "unknown";
        var language = Language ?? "unknown";
        return $"Page {PageNumber} (Engine={engine}, Lang={language})";
    }
}
