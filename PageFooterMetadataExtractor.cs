// bookscraper.core/Services/PageFooterMetadataExtractor.cs
#nullable enable

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Bookscraper.Core.Models;
using Microsoft.Extensions.Logging;
using Tesseract;

namespace Bookscraper.Core.Services;

/// <summary>
/// Extracts page-level metadata (page numbers and location values) from the
/// visual footer region of a page image using a dedicated Tesseract OCR pass.
/// </summary>
/// <remarks>
/// This extractor is designed to be tolerant of footer-level OCR and parsing
/// issues by returning a DTO with nullable properties when the footer text
/// cannot be interpreted. It does <b>not</b> perform any local try/catch;
/// infrastructure and engine failures are allowed to bubble up into the
/// global error management pipeline.
/// </remarks>
public sealed class PageFooterMetadataExtractor
{
    /// <summary>
    /// Fraction of the image height treated as the footer region.
    /// </summary>
    private const double FooterHeightFraction = 0.05;

        
    private static readonly Regex StrictPagePattern = new(
        @"Page\s+(?<current>\d+)\s+of\s+(?<total>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TolerantPagePattern = new(
        @"Page\D*(?<current>\d+)\D+(?<total>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IntPattern = new(
        @"\d+",
        RegexOptions.Compiled);
        
    private static readonly Regex PagePattern = new(
        @"Page\s+(?<current>\d+)\s+of\s+(?<total>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LocationPattern = new(
        @"Location\s+(?<current>\d+)\s+of\s+(?<total>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<PageFooterMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageFooterMetadataExtractor"/> class.
    /// </summary>
    /// <param name="logger">
    /// Logger used for debug-level diagnostics related to footer OCR and parsing.
    /// </param>
    public PageFooterMetadataExtractor(ILogger<PageFooterMetadataExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attempts to extract footer-based metadata (page and location values) from
    /// the supplied page image using a second, specialized Tesseract OCR pass.
    /// </summary>
    /// <param name="pageImage">
    /// The full page <see cref="Pix"/> image from which the footer region will be
    /// interpreted. The image is not modified by this method.
    /// </param>
    /// <param name="engine">
    /// The <see cref="TesseractEngine"/> instance to use for OCR. The engine is
    /// temporarily configured with a character whitelist and a footer-friendly
    /// page segmentation mode for the duration of this call.
    /// </param>
    /// <returns>
    /// A <see cref="PageFooterMetadata"/> instance containing any successfully
    /// extracted values. If footer OCR or parsing fails in a non-exceptional way
    /// (e.g., patterns do not match), all properties of the returned object
    /// will be <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method:
    /// <list type="number">
    ///   <item>
    ///     <description>Computes a footer rectangle covering the bottom 10–15% of the page.</description>
    ///   </item>
    ///   <item>
    ///     <description>Runs a second OCR pass with an optimized page segmentation mode.</description>
    ///   </item>
    ///   <item>
    ///     <description>Parses the resulting text using regular expressions for page and location patterns.</description>
    ///   </item>
    ///   <item>
    ///     <description>Returns a DTO with nullable properties instead of throwing on footer-level parse failures.</description>
    ///   </item>
    /// </list>
    /// It does not perform any local try/catch; exceptions are allowed to propagate
    /// to the global error handling system.
    /// </remarks>
    public PageFooterMetadata ExtractFooterMetadata(Pix pageImage, TesseractEngine engine)
    {
        if (pageImage is null)
        {
            _logger.LogDebug(
                "PageFooterMetadataExtractor received a null page image; returning empty metadata DTO.");
            return new PageFooterMetadata();
        }

        if (engine is null)
        {
            _logger.LogDebug(
                "PageFooterMetadataExtractor received a null TesseractEngine; returning empty metadata DTO.");
            return new PageFooterMetadata();
        }

        // Pure, non-exception-based "soft" failures (invalid dimensions etc.) are
        // handled locally by returning an empty DTO. Any real failures (engine issues,
        // disposed objects, etc.) are allowed to bubble into the global error pipeline.
        var footerRect = BuildFooterRectangle(pageImage);
        if (footerRect is null)
        {
            _logger.LogDebug("Footer rectangle calculation failed; returning empty metadata DTO.");
            return new PageFooterMetadata();
        }

        var rawFooterText = RunFooterOcr(pageImage, footerRect.Value, engine);
        if (string.IsNullOrWhiteSpace(rawFooterText))
        {
            _logger.LogDebug(
                "Footer OCR produced empty or whitespace-only text; returning empty metadata DTO.");
            return new PageFooterMetadata();
        }

        _logger.LogDebug("Footer OCR text: '{FooterText}'", rawFooterText);

        var (pageCurrent, pageTotal) = ParsePageValues(rawFooterText);
        var (locationCurrent, locationTotal) = ParseLocationValues(rawFooterText);

        return new PageFooterMetadata
        {
            PageCurrent = pageCurrent,
            PageTotal = pageTotal,
            LocationCurrent = locationCurrent,
            LocationTotal = locationTotal
        };
    }

    /// <summary>
    /// Computes the rectangle representing the footer region of the page image
    /// that typically contains page and location information.
    /// </summary>
    /// <param name="pageImage">
    /// The full page image from which the footer rectangle will be derived.
    /// </param>
    /// <returns>
    /// A <see cref="Rect"/> describing the footer region, or <c>null</c> if the
    /// image dimensions are invalid.
    /// </returns>
    private static Rect? BuildFooterRectangle(Pix pageImage)
    {
        var height = pageImage.Height;
        var width = pageImage.Width;

        if (height <= 0 || width <= 0)
        {
            return null;
        }

        var footerHeight = (int)Math.Round(height * FooterHeightFraction);
        footerHeight = Math.Clamp(footerHeight, 1, height);

        var footerY = Math.Max(0, height - footerHeight);

        var left = 0;
        var top = footerY;
        var right = width;
        var bottom = height;

        return Rect.FromCoords(left, top, right, bottom);
    }

    /// <summary>
    /// Executes a specialized OCR pass over the footer region of the page image
    /// using a Tesseract configuration tuned for short, metadata-like text.
    /// </summary>
    /// <param name="pageImage">
    /// The full page <see cref="Pix"/> image.
    /// </param>
    /// <param name="footerRect">
    /// The rectangle describing the footer region to process.
    /// </param>
    /// <param name="engine">
    /// The Tesseract engine to use for OCR.
    /// </param>
    /// <returns>
    /// The raw footer text returned by Tesseract, trimmed of leading and
    /// trailing whitespace. May be an empty string.
    /// </returns>
    private string RunFooterOcr(Pix pageImage, Rect footerRect, TesseractEngine engine)
    {
        if (pageImage is null) throw new ArgumentNullException(nameof(pageImage));
        if (engine is null) throw new ArgumentNullException(nameof(engine));

        // Restrict character set to digits, spaces, percent signs, and the
        // specific words we expect in Kindle-style footers.
        engine.SetVariable("tessedit_char_whitelist", "0123456789PageLocationof %");

        using var page = engine.Process(pageImage, footerRect, PageSegMode.SingleLine);
        var text = page.GetText() ?? string.Empty;

        // Reset whitelist for subsequent OCR operations. This is best-effort;
        // if an exception has already been thrown, the global pipeline owns it.
        engine.SetVariable("tessedit_char_whitelist", string.Empty);

        return text.Trim();
    }

    /// <summary>
    /// Attempts to parse page number information from the footer text.
    /// </summary>
    /// <param name="footerText">
    /// The raw text produced by the footer OCR pass.
    /// </param>
    /// <returns>
    /// A tuple containing the current page number and total page count. Either
    /// value may be <c>null</c> if parsing fails.
    /// </returns>
    private (int? current, int? total) ParsePageValues(string footerText)
    {
        if (string.IsNullOrWhiteSpace(footerText))
        {
            _logger.LogDebug("Footer text was null or whitespace; page values not parsed.");
            return (null, null);
        }

        var normalized = NormalizeFooterText(footerText);
        _logger.LogDebug("Normalized footer text for page parsing: '{FooterText}'", normalized);

        // 1) Strict pattern: ideal case, clean OCR
        var match = StrictPagePattern.Match(normalized);
        if (TryGetPagePair(match, out var strictResult))
        {
            _logger.LogDebug("Strict page pattern matched footer text: '{Value}'", match.Value);
            return strictResult;
        }

        // 2) Tolerant structural pattern: ignores exact 'of' spelling, spacing, etc.
        match = TolerantPagePattern.Match(normalized);
        if (TryGetPagePair(match, out var tolerantResult))
        {
            _logger.LogDebug("Tolerant page pattern matched footer text: '{Value}'", match.Value);
            return tolerantResult;
        }

        // 3) Heuristic fallback: use number positions + weak 'Page' keyword
        var heuristicResult = ParsePageValuesHeuristic(normalized);
        if (heuristicResult.current.HasValue || heuristicResult.total.HasValue)
        {
            _logger.LogDebug(
                "Heuristic page parsing succeeded: current={Current}, total={Total}",
                heuristicResult.current,
                heuristicResult.total);

            return heuristicResult;
        }

        _logger.LogDebug("Failed to parse page values from footer text after all strategies.");
        return (null, null);
    }


    /// <summary>
    /// Attempts to parse location information from the footer text.
    /// </summary>
    /// <param name="footerText">
    /// The raw text produced by the footer OCR pass.
    /// </param>
    /// <returns>
    /// A tuple containing the current location value and total location count.
    /// Either value may be <c>null</c> if parsing fails.
    /// </returns>
    private (int? current, int? total) ParseLocationValues(string footerText)
    {
        if (string.IsNullOrWhiteSpace(footerText))
        {
            return (null, null);
        }

        var match = LocationPattern.Match(footerText);
        if (!match.Success)
        {
            _logger.LogDebug("Location pattern did not match footer text.");
            return (null, null);
        }

        _logger.LogDebug("Location pattern matched footer text: '{Value}'", match.Value);

        int? current = null;
        int? total = null;

        if (int.TryParse(match.Groups["current"].Value, out var currentValue))
        {
            current = currentValue;
        }

        if (int.TryParse(match.Groups["total"].Value, out var totalValue))
        {
            total = totalValue;
        }

        return (current, total);
    }
    

    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Normalizes Kindle-style footer OCR text into a stable, parser-friendly form so that
    /// page/location extractors can reliably read current/total values even when OCR is noisy.
    /// </summary>
    /// <remarks>
    /// Typical raw input from the footer OCR pipeline looks like:
    ///   "84% Page11290f 1270 Location 12615 of 15081"
    /// The goal is to produce a canonical form:
    ///   "84% Page1129 of 1270 Location 12615 of 15081"
    ///
    /// The method performs three main steps:
    ///  1) Trim & whitespace normalization:
    ///       - <c>raw</c> is trimmed, then <c>\s+</c> is collapsed to a single space so later
    ///         regexes see a predictable layout (no random tabs or multiple spaces).
    ///
    ///  2) Targeted fix for the Kindle <c>"digits 0f digits"</c> glitch:
    ///       - Example problem:  <c>Page11290f 1270</c>
    ///       - Regex:     <c>(?&lt;digits&gt;\d+)[0o]\s*f(\s+\d+)</c>
    ///       - Replace:   <c>"${digits} of$1"</c>
    ///       - Effect:    <c>"Page11290f 1270" → "Page1129 of 1270"</c>
    ///       - This is restricted to <c>digits + 0f/of + space + digits</c> so only the
    ///         page-number segment (current + total) is rewritten; other <c>0f</c> sequences
    ///         elsewhere are left untouched.
    ///
    ///  3) Fix compact <c>"digitsof digits"</c> form:
    ///       - Example:   <c>"Page1129of 1270"</c>
    ///       - Regex:     <c>(?&lt;digits&gt;\d+)of(\s+\d+)</c>
    ///       - Replace:   <c>"${digits} of$1"</c>
    ///       - Effect:    ensures there is always a space before <c>"of"</c>, so downstream
    ///         patterns like <c>Page\s+(?<current>\d+)\s+of\s+(?<total>\d+)</c> work reliably.
    ///
    /// After normalization, higher-level parsers (e.g. <c>ParsePageValues</c>) can focus on
    /// extracting numbers instead of compensating for OCR quirks, which greatly increases
    /// robustness while keeping Kindle-specific behavior isolated in one place.
    /// </remarks>
    /// <param name="raw">
    /// Raw footer text produced by the OCR engine; may contain irregular whitespace and
    /// Kindle-specific artifacts such as <c>"Page11290f 1270"</c>.
    /// </param>
    /// <returns>
    /// A normalized footer string with whitespace collapsed and known Kindle page-segment
    /// glitches corrected (for example, <c>"Page1129 of 1270"</c>). If <paramref name="raw"/>
    /// is <c>null</c>, empty, or whitespace, the original value is returned unchanged.
    /// </returns>
    /// ////////////////////////////////////////////////////////////////////////////////////////////////
    private static string NormalizeFooterText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var text = raw.Trim();

        // Collapse crazy whitespace
        text = Regex.Replace(text, @"\s+", " ");

        // 1) Fix 'Page11290f 1270' / 'Page1129 0f 1270' / 'Page1129 0 f 1270'
        //    → 'Page1129 of 1270'
        text = Regex.Replace(
            text,
            @"(?<digits>\d+)[0o]\s*f(\s+\d+)",
            "${digits} of$1",
            RegexOptions.IgnoreCase);

        // 2) Fix 'Page1129of 1270' (no space before 'of') → 'Page1129 of 1270'
        text = Regex.Replace(
            text,
            @"(?<digits>\d+)of(\s+\d+)",
            "${digits} of$1",
            RegexOptions.IgnoreCase);

        return text;
    }


    private static bool TryGetPagePair(Match match, out (int? current, int? total) result)
    {
        if (match.Success &&
            int.TryParse(match.Groups["current"].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var current) &&
            int.TryParse(match.Groups["total"].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var total) &&
            IsPlausiblePagePair(current, total))
        {
            result = (current, total);
            return true;
        }

        result = (null, null);
        return false;
    }

    private static bool IsPlausiblePagePair(int current, int total)
    {
        return current > 0
               && total > 0
               && current <= total
               && total <= 50000; // tweak ceiling as needed
    }

    private (int? current, int? total) ParsePageValuesHeuristic(string text)
    {
        var matches = IntPattern.Matches(text);
        if (matches.Count < 2)
        {
            return (null, null);
        }

        var numbers = matches
            .Cast<Match>()
            .Select(m => new
            {
                Value = int.Parse(m.Value, CultureInfo.InvariantCulture),
                Index = m.Index
            })
            .ToList();

        // Prefer numbers that appear after something that looks like "Page"
        var pageIndex = IndexOfPageKeyword(text);
        if (pageIndex >= 0)
        {
            var afterPage = numbers.Where(n => n.Index > pageIndex).ToList();
            if (afterPage.Count >= 2)
            {
                var current = afterPage[0].Value;
                var total = afterPage[1].Value;

                if (IsPlausiblePagePair(current, total))
                {
                    return (current, total);
                }
            }
        }

        // Fallback layout assumption:
        // [0] = percent, [1] = pageCurrent, [2] = pageTotal, [3] [4] = locations
        if (numbers.Count >= 3)
        {
            var current = numbers[1].Value;
            var total = numbers[2].Value;

            if (IsPlausiblePagePair(current, total))
            {
                return (current, total);
            }
        }

        return (null, null);
    }

    private static int IndexOfPageKeyword(string text)
    {
        var index = text.IndexOf("page", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return index;
        }

        // Optional: tolerate a simple OCR glitch like "pa9e"
        var alt = text.IndexOf("pa9e", StringComparison.OrdinalIgnoreCase);
        return alt;
    }
}