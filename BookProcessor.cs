// bookscraper.core/Services/BookProcessor.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bookscraper.Core.Decorators;
using Bookscraper.Core.ErrorHandling;
using Bookscraper.Core.Interfaces;
using Bookscraper.Core.Models;
using Bookscraper.Core.Serialization;

namespace Bookscraper.Core.Services;

/// <summary>
/// Coordinates processing of a single book: image loading, OCR, and YAML generation.
/// No local try/catch; failures bubble to the global executor.
/// </summary>
public sealed class BookProcessor : IBookProcessor
{
    private static readonly string[] SupportedImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"
    };

    private readonly IErrorHandlingDecorator _decorator;
    private readonly ImageHelper _imageHelper;
    private readonly OcrProcessor _ocrProcessor;
    private readonly YamlSerializer _yamlSerializer;

    /// <summary>
    /// Creates a new <see cref="BookProcessor"/> with all required collaborators.
    /// </summary>
    public BookProcessor(
        IErrorHandlingDecorator decorator,
        ImageHelper imageHelper,
        OcrProcessor ocrProcessor,
        YamlSerializer yamlSerializer)
    {
        _decorator = decorator ?? throw new ArgumentNullException(nameof(decorator));
        _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
        _ocrProcessor = ocrProcessor ?? throw new ArgumentNullException(nameof(ocrProcessor));
        _yamlSerializer = yamlSerializer ?? throw new ArgumentNullException(nameof(yamlSerializer));
    }

    /// <inheritdoc />
    public Task ProcessBookAsync(BookProcessingOptions options, string bookRootPath)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(bookRootPath))
        {
            throw new ArgumentException("Book root path must be provided.", nameof(bookRootPath));
        }

        if (!Directory.Exists(bookRootPath))
        {
            throw new DirectoryNotFoundException(
                $"Book root directory '{bookRootPath}' does not exist.");
        }

        var imageFiles = Directory
            .GetFiles(bookRootPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imageFiles.Length == 0)
        {
            // Let the global pipeline classify this as configuration / input error.
            throw new InvalidOperationException(
                $"No supported image files were found in '{bookRootPath}'.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new InvalidOperationException(
                "BookProcessingOptions.OutputDirectory must be set before processing.");
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var pageNumber = 1;
        foreach (var imagePath in imageFiles)
        {
            ProcessSinglePage(options, bookRootPath, imagePath, pageNumber);
            pageNumber++;
        }

        return Task.CompletedTask;
    }

    private void ProcessSinglePage(
        BookProcessingOptions options,
        string bookRootPath,
        string imagePath,
        int pageNumber)
    {
        // Derive canonical book name from filesystem.
        // Single-book mode → directory name (bookRootPath).
        // Multi-book mode → parent folder of image file.
        var bookName = DeriveBookName(bookRootPath, imagePath);

        // Image → Base64 (via decorator seam).
        var base64 = _decorator.Invoke(() => _imageHelper.LoadImageAsBase64(imagePath));

        // Initialize page model with images and book-level metadata.
        var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["original"] = base64
        };

        var page = new Page(
            pageNumber: pageNumber,
            base64Images: images,
            bookName: bookName);

        // OCR (decorator seam) – main text + footer metadata.
        var _ = _decorator.Invoke(() => _ocrProcessor.ProcessWithOcrAndFooter(page));

        // Note: We do NOT update pageNumber from page.PageNumber here.
        // We use the sequential 'pageNumber' argument passed to this method for the filename
        // to ensure uniqueness and correct ordering.
        
        // Derive deterministic output path: <bookName>_page_0001.yaml etc.
        var fileName = $"{bookName}_page_{pageNumber:D4}.yaml";
        var outputPath = Path.Combine(options.OutputDirectory, fileName);

        // YAML write (decorator seam).
        _decorator.Invoke(() =>
        {
            _yamlSerializer.WritePage(page, outputPath);
            return true;
        });
    }

    private static bool IsSupportedImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Derives the canonical logical book name from the filesystem, supporting both
    /// single-book and multi-book layouts.
    /// </summary>
    /// <param name="bookRootPath">
    /// The root path passed into <see cref="ProcessBookAsync"/>. In single-book mode,
    /// this is the book directory. In multi-book scenarios it may be a higher-level root.
    /// </param>
    /// <param name="imagePath">The full path to the image file for the current page.</param>
    /// <returns>A non-empty, canonical book name.</returns>
    /// <exception cref="InvalidConfigurationException">
    /// Thrown when a valid book directory or name cannot be derived.
    /// </exception>
    private static string DeriveBookName(string bookRootPath, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(bookRootPath))
        {
            throw new InvalidConfigurationException("Book root path must not be null or whitespace.", $"bookRootPath: '{bookRootPath}'");
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new InvalidConfigurationException("Image path must not be null or whitespace.", $"imagePath: '{imagePath}'");
        }

        var rootFullPath = Path.GetFullPath(bookRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var imageFullPath = Path.GetFullPath(imagePath);
        var imageDirectory = Path.GetDirectoryName(imageFullPath);

        if (string.IsNullOrWhiteSpace(imageDirectory))
        {
            throw new InvalidConfigurationException($"Unable to determine book directory for image path '{imagePath}'.", $"imagePath: '{imagePath}', imageDirectory: '{imageDirectory}'");
        }

        imageDirectory = imageDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        string bookName;

        if (string.Equals(imageDirectory, rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            // Single-book mode: images live directly under the provided root directory.
            bookName = new DirectoryInfo(rootFullPath).Name;
        }
        else
        {
            // Multi-book mode: treat the immediate parent directory of the image
            // as the logical book folder.
            bookName = new DirectoryInfo(imageDirectory).Name;
        }

        if (string.IsNullOrWhiteSpace(bookName))
        {
            throw new InvalidConfigurationException(
                $"Derived book name from path '{imageDirectory}' is null, empty, or whitespace.",
                $"imageDirectory: '{imageDirectory}', bookName: '{bookName}'");
        }

        return bookName;
    }
}