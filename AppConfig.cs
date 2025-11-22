// bookscraper.cli/Config/AppConfig.cs
using System;
using Bookscraper.Core.ErrorHandling;
using Bookscraper.Core.Models;
using Microsoft.Extensions.Configuration;

namespace Bookscraper.Cli.Configuration
{
    /// <summary>
    /// Binds CLI + configuration data into strongly typed options
    /// used by the bookscraper commands.
    /// </summary>
    public sealed class AppConfig
    {
        private readonly IConfiguration _configuration;

        public AppConfig(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        
        public BookProcessingOptions BindBookProcessingOptions(string[] args)
{
    if (args == null) throw new ArgumentNullException(nameof(args));

    // Start from appsettings.json (or equivalent) binding.
    var options = _configuration
                      .GetSection("BookProcessingOptions")
                      .Get<BookProcessingOptions>() ?? new BookProcessingOptions();

    static string GetNextValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidConfigurationException(
                $"Option '{optionName}' requires a value.",
                $"CLI argument '{optionName}' was specified without a following value.");
        }

        index++;
        return args[index];
    }

    static int ParseIntOption(string value, string optionName)
    {
        if (!int.TryParse(value, out var result))
        {
            throw new InvalidConfigurationException(
                $"Option '{optionName}' must be an integer.",
                $"CLI argument '{optionName}' had value '{value}', which could not be parsed as an integer.");
        }

        return result;
    }

    static TimeSpan ParseTimeSpanOption(string value, string optionName)
    {
        if (!TimeSpan.TryParse(value, out var result))
        {
            throw new InvalidConfigurationException(
                $"Option '{optionName}' must be a valid TimeSpan.",
                $"CLI argument '{optionName}' had value '{value}', which could not be parsed as a TimeSpan.");
        }

        return result;
    }

    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];

        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            // Positional arguments (such as the single-book path) are handled elsewhere.
            continue;
        }

        switch (arg)
        {
            case "--input-dir":
                options.InputDirectory = GetNextValue(args, ref i, "--input-dir");
                break;

            case "--output-dir":
                options.OutputDirectory = GetNextValue(args, ref i, "--output-dir");
                break;

            case "--lang":
                options.Language = GetNextValue(args, ref i, "--lang");
                break;

            case "--max-retries":
            {
                var value = GetNextValue(args, ref i, "--max-retries");
                options.MaxRetries = ParseIntOption(value, "--max-retries");
                break;
            }

            case "--retry-delay":
            {
                var value = GetNextValue(args, ref i, "--retry-delay");
                options.RetryDelay = ParseTimeSpanOption(value, "--retry-delay");
                break;
            }

            case "--enable-logging":
                options.EnableLogging = true;
                break;

            case "--disable-logging":
                options.EnableLogging = false;
                break;

            case "--book-path":
                // Consume the value so iteration stays in sync; the actual path is resolved elsewhere.
                _ = GetNextValue(args, ref i, "--book-path");
                break;

            case "--single":
            case "--batch":
            case "--help":
                // Mode/help flags are handled by the CLI command mediator.
                break;

            default:
                throw new InvalidConfigurationException(
                    $"Unrecognized command-line option '{arg}'.",
                    $"BindBookProcessingOptions encountered an unrecognized CLI option '{arg}'.");
        }
    }

    // Final validation uses the central model contract.
    options.Validate();

    return options;
}

public string ResolveSingleBookPath(string[] args)
{
    if (args == null) throw new ArgumentNullException(nameof(args));

    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];

        if (!string.Equals(arg, "--book-path", StringComparison.Ordinal))
        {
            continue;
        }

        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidConfigurationException(
                "Option '--book-path' requires a value.",
                "The CLI argument '--book-path' was specified without a following path value.");
        }

        var path = args[i + 1];

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidConfigurationException(
                "Single-book mode requires a non-empty path value for '--book-path'.",
                "The CLI argument '--book-path' was provided, but the following value was null, empty, or whitespace.");
        }

        return path;
    }

    throw new InvalidConfigurationException(
        "Single-book mode requires '--book-path <path>' to be specified.",
        "ResolveSingleBookPath could not find a '--book-path' argument in the provided CLI arguments.");
}

    }
}
