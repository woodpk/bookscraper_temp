// bookscraper.core/Services/OcrProcessor.cs
using System;
using System.Collections.Generic;
using Bookscraper.Core.Models;
using Serilog;
using Tesseract;
using Page = Bookscraper.Core.Models.Page;

namespace Bookscraper.Core.Services;

/// <summary>
/// Thin OCR adapter around Tesseract; no local try/catch.
/// All failures are allowed to bubble into the global error handling pipeline
/// via the decorator / executor infrastructure.
/// </summary>
public sealed class OcrProcessor
{
    private readonly string _tessdataPath;
    private readonly string _language;
    private readonly PageFooterMetadataExtractor? _footerMetadataExtractor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessor"/> class.
    /// This constructor keeps the original signature to avoid breaking existing wiring.
    /// Footer metadata extraction will be disabled unless the overload that accepts
    /// a <see cref="PageFooterMetadataExtractor"/> is used.
    /// </summary>
    /// <param name="tessdataPath">Path to the Tesseract tessdata directory.</param>
    /// <param name="language">Language code (for example, "eng").</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tessdataPath"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="language"/> is null, empty, or whitespace.
    /// </exception>
    public OcrProcessor(string tessdataPath, string language)
    {
        _tessdataPath = tessdataPath ?? throw new ArgumentNullException(nameof(tessdataPath));

        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language must not be null or whitespace.", nameof(language));
        }

        _language = language;
        _logger = Log.Logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OcrProcessor"/> class with
    /// optional footer metadata extraction support.
    /// </summary>
    /// <param name="tessdataPath">Path to the Tesseract tessdata directory.</param>
    /// <param name="language">Language code (for example, "eng").</param>
    /// <param name="footerMetadataExtractor">
    /// The extractor responsible for interpreting page/footer metadata
    /// from the page image using a secondary OCR pass.
    /// </param>
    /// <param name="logger"></param>
    public OcrProcessor(string tessdataPath,
        string language,
        PageFooterMetadataExtractor footerMetadataExtractor,
        ILogger logger)

        : this(tessdataPath, language)
    {
        _footerMetadataExtractor = footerMetadataExtractor
            ?? throw new ArgumentNullException(nameof(footerMetadataExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    }

    /// <summary>
    /// Runs OCR on the primary Base64 image for the page and applies the result
    /// to the page model. This maintains the original signature and behavior
    /// expected by the current pipeline while internally delegating to the
    /// structured OCR + footer metadata path.
    /// </summary>
    /// <remarks>
    /// Any failures in image decoding, Tesseract initialization, or processing
    /// are allowed to bubble up to the global error handling system via the
    /// caller's <c>ErrorHandlingDecorator</c>. No local try/catch is used here.
    /// </remarks>
    /// <param name="page">The page model that contains the Base64 image and receives OCR output.</param>
    public void ProcessWithOcr(Page page)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));

        // Delegate to structured OCR path; ignore the structured return value for now
        // since the current pipeline expects mutation via Page.ApplyOcrResult.
        _ = ProcessWithOcrAndFooter(page);
    }

    /// <summary>
    /// Runs OCR on the primary Base64 image for the page, attempts to extract
    /// footer metadata using <see cref="PageFooterMetadataExtractor"/>, applies
    /// OCR output to the page model, and returns a structured result object.
    /// </summary>
    /// <remarks>
    /// This method is designed for callers that need both the full OCR text and
    /// the structured footer metadata (page numbers, locations, etc.) in a single
    /// deterministic DTO, while still keeping all error handling centralized in
    /// the global executor.
    /// </remarks>
    /// <param name="page">The page model that contains the Base64 image and receives OCR output.</param>
    /// <returns>
    /// A <see cref="PageOcrResult"/> instance containing the OCR text and any footer
    /// metadata that could be extracted. Footer metadata may be <c>null</c> if no
    /// extractor is configured or the footer patterns are not detected.
    /// </returns>
    public PageOcrResult ProcessWithOcrAndFooter(Page page)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));

        _logger.Debug("=== Processing page ===");
        _logger.Debug("Input PageNumber (pre-OCR): {PageNumber}", page.PageNumber);
        _logger.Debug("Base64 image count: {Count}", page.Base64Images?.Count ?? 0);

        if (page.Base64Images == null || page.Base64Images.Count == 0)
        {
            throw new InvalidOperationException("Page.Base64Images must contain at least one image.");
        }

        var result = RunOcrCore(page);

        _logger.Debug(
            "Footer OCR result for PageNumber={PageNumber}: HasFooter={HasFooter} PageCurrent.HasValue={HasPageCurrent} PageCurrent={PageCurrent}",
            page.PageNumber,
            result.FooterMetadata is not null,
            result.FooterMetadata?.PageCurrent.HasValue ?? false,
            result.FooterMetadata?.PageCurrent);

        
        page.ApplyOcrResult(
            ocrText: result.OcrText,
            ocrEngine: "Tesseract",
            language: _language,
            processedTimestamp: DateTime.UtcNow,
            currentPage: result.FooterMetadata?.PageCurrent ?? page.PageNumber);

        return result;
    }

    /// <summary>
    /// Executes the core OCR pipeline for a single logical page using Tesseract,
    /// producing the main OCR text and optional footer metadata derived from the
    /// same page image.
    /// </summary>
    /// <param name="page">
    /// The <see cref="Page"/> instance containing one or more Base64-encoded page
    /// images. The method will select the primary image, decode it, and run OCR
    /// against it.
    /// </param>
    /// <returns>
    /// A <see cref="PageOcrResult"/> containing the full OCR text for the page and,
    /// when configured, any extracted footer metadata (such as page/location
    /// information discovered in the footer region).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="page"/> is <c>null</c>.
    /// </exception>
    private PageOcrResult RunOcrCore(Page page)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));

        string base64 = GetPrimaryImage(page.Base64Images!);
        byte[] imageBytes = Convert.FromBase64String(base64);

        using var pix = Pix.LoadFromMemory(imageBytes);
        using var engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);

        // IMPORTANT: Scope the Tesseract Page so it is fully disposed
        // before we invoke the footer extractor (which calls engine.Process again).
        string text;
        using (var ocrPage = engine.Process(pix))
        {
            text = ocrPage.GetText();
        }

        PageFooterMetadata? footerMetadata = null;
        if (_footerMetadataExtractor is not null)
        {
            // At this point no Page is alive; it's safe to call Process again.
            footerMetadata = _footerMetadataExtractor.ExtractFooterMetadata(pix, engine);
        }

        return new PageOcrResult
        {
            OcrText = text,
            FooterMetadata = footerMetadata
        };
    }


    /// <summary>
    /// Selects the primary image from the page's Base64 image dictionary using
    /// a deterministic strategy: prefer the "original" key, then the first
    /// non-empty value.
    /// </summary>
    /// <param name="images">Dictionary of Base64 images keyed by variant name.</param>
    /// <returns>The Base64 string for the primary image.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no non-empty Base64 value is found.
    /// </exception>
    private static string GetPrimaryImage(IDictionary<string, string> images)
    {
        if (images.TryGetValue("original", out var original) &&
            !string.IsNullOrWhiteSpace(original))
        {
            return original;
        }

        foreach (var kvp in images)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                return kvp.Value;
            }
        }

        throw new InvalidOperationException("No non-empty Base64 image entry was found.");
    }

    /// <summary>
    /// Structured OCR result that combines the raw OCR text with any footer
    /// metadata that could be extracted from the page image.
    /// </summary>
    public sealed class PageOcrResult
    {
        /// <summary>
        /// The OCR text extracted from the full page image.
        /// </summary>
        public string OcrText { get; init; } = string.Empty;

        /// <summary>
        /// Footer metadata (page numbers, locations, etc.) extracted from the
        /// HUD/footer region of the image, if available.
        /// </summary>
        public PageFooterMetadata? FooterMetadata { get; init; }
    }
}
