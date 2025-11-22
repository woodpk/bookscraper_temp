// bookscraper.core/Models/PageFooterMetadata.cs
#nullable enable

namespace Bookscraper.Core.Models;

/// <summary>
/// Represents metadata extracted from the visual footer region of a page image,
/// such as the current page index, total pages, and Kindle-style location values.
/// </summary>
/// <remarks>
/// All numeric properties are nullable to allow the caller to distinguish between
/// successfully parsed values and cases where the footer text could not be read
/// or did not match the expected patterns. A <c>null</c> value means the
/// corresponding value was not confidently extracted from the image.
/// </remarks>
public sealed class PageFooterMetadata
{
    /// <summary>
    /// Gets the current page number as reported in the footer (for example, the
    /// <c>5</c> in <c>&quot;Page 5 of 273&quot;</c>).
    /// </summary>
    /// <value>
    /// The zero-based or one-based page index as presented in the source footer text,
    /// or <c>null</c> if the value could not be extracted or parsed.
    /// </value>
    public int? PageCurrent { get; init; }

    /// <summary>
    /// Gets the total number of pages in the book as reported in the footer
    /// (for example, the <c>273</c> in <c>&quot;Page 5 of 273&quot;</c>).
    /// </summary>
    /// <value>
    /// The total number of pages in the source document, or <c>null</c> if the value
    /// could not be extracted or parsed.
    /// </value>
    public int? PageTotal { get; init; }

    /// <summary>
    /// Gets the current location index as reported in the footer
    /// (for example, the <c>1355</c> in <c>&quot;Location 1355 of 5081&quot;</c>).
    /// </summary>
    /// <value>
    /// The current location value as presented in the footer text, or <c>null</c>
    /// if the value could not be extracted or parsed.
    /// </value>
    public int? LocationCurrent { get; init; }

    /// <summary>
    /// Gets the total number of locations as reported in the footer
    /// (for example, the <c>5081</c> in <c>&quot;Location 1355 of 5081&quot;</c>).
    /// </summary>
    /// <value>
    /// The total number of locations, or <c>null</c> if the value
    /// could not be extracted or parsed.
    /// </value>
    public int? LocationTotal { get; init; }
}