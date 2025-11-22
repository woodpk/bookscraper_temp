// bookscraper.core/Services/ImageHelper.cs
using System;
using System.IO;
using Bookscraper.Core.ErrorHandling;

namespace Bookscraper.Core.Services;

/// <summary>
/// Helper for loading image data from disk and converting it to Base64.
/// </summary>


//TODO: NEEDS FULL UPDATE TO WORK W/ CORRECTED PIPELINE
public sealed class ImageHelper
{
    public byte[] LoadImageBytes(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new InvalidConfigurationException(
                "Image path cannot be null or empty.",
                $"Parameter '{nameof(imagePath)}' was null or empty.");
        }

        if (!File.Exists(imagePath))
        {
            throw new FileAccessException(
                $"Image file not found at path '{imagePath}'.",
                null,
                imagePath);
        }

        var fileInfo = new FileInfo(imagePath);

        if (fileInfo.Length == 0)
        {
            throw new ImageProcessingException(
                "Image file is empty.",
                null,
                imagePath);
        }

        const long maxImageBytes = 32L * 1024L * 1024L; // 32 MB upper bound for a single page image

        if (fileInfo.Length > maxImageBytes)
        {
            throw new ImageProcessingException(
                $"Image file at '{imagePath}' is too large to process safely. Size: {fileInfo.Length} bytes.",
                null,
                imagePath);
        }

        return File.ReadAllBytes(imagePath);
    }


    public string ConvertImageToBase64(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ImageProcessingException("Image bytes cannot be null or empty.", null, string.Empty);
        }

        return Convert.ToBase64String(imageBytes);
    }

    public string LoadImageAsBase64(string imagePath)
    {
        var bytes = LoadImageBytes(imagePath);
        return ConvertImageToBase64(bytes);
    }
}