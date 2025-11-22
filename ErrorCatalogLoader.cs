// bookscraper.cli/Helpers/ErrorCatalogLoader.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Bookscraper.Core.ErrorHandling;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bookscraper.Cli.Helpers;

/// <summary>
/// Helper for loading the error catalog and mappings from YAML contracts.
/// Designed to be extended with additional contract-loading helpers over time.
/// </summary>
public static class ErrorCatalogLoader
{
    /// <summary>
    /// Loads <see cref="ErrorCatalogMappingProvider"/> by hydrating it from the
    /// common + bookscraper error/exit-code contracts in the specified directory.
    /// </summary>
    /// <param name="contractsDirectory">
    /// Directory that contains the error/exit-code contract YAML files.
    /// Expected files:
    ///   - contract.common.errors-and-exit-codes.yaml
    ///   - contract.bookscraper.errors-and-exit-codes.yaml
    /// </param>
    /// <param name="logger">
    /// Optional logger for diagnostics (missing files, parse issues, etc.).
    /// </param>
    public static ErrorCatalogMappingProvider LoadErrorCatalogMapping(
        string contractsDirectory,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(contractsDirectory))
        {
            throw new ArgumentException("Contracts directory path must be provided.", nameof(contractsDirectory));
        }

        if (!Directory.Exists(contractsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Contracts directory not found at '{contractsDirectory}'.");
        }

        var contractFiles = new[]
        {
            Path.Combine(contractsDirectory, "contract.common.errors-and-exit-codes.yaml"),
            Path.Combine(contractsDirectory, "contract.bookscraper.errors-and-exit-codes.yaml"),
            Path.Combine(contractsDirectory, "contracts.bookscraper.error-mapping.yaml")
        };

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var exceptionToErrorCode = new Dictionary<Type, string>();
        var errorCodeToMetadata =
            new Dictionary<string, ErrorCatalogMappingProvider.ErrorMetadata>(
                StringComparer.OrdinalIgnoreCase);
        var cliExitCodeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);


        // Sensible default; may be overridden by a contract field.
        var fallbackErrorCode = "ERR.UNEXPECTED";

        foreach (var path in contractFiles)
        {
            if (!File.Exists(path))
            {
                logger?.Warning("Error/exit-code contract file not found at {Path}", path);
                continue;
            }

            string yaml;
            try
            {
                yaml = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to read error/exit-code contract file at {Path}", path);
                continue;
            }

            Dictionary<string, object?>? root;
            try
            {
                root = deserializer.Deserialize<Dictionary<string, object?>>(yaml);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "Failed to parse YAML for error/exit-code contract file at {Path}", path);
                continue;
            }

            // CLI exit-code mappings
            if (root.TryGetValue("cli_exit_codes", out var cliObj) &&
                cliObj is IDictionary cliDict &&
                cliDict.Contains("map") &&
                cliDict["map"] is IDictionary mapDict)
            {
                foreach (DictionaryEntry entry in mapDict)
                {
                    var errorCode = entry.Key as string;
                    var exitCodeObj = entry.Value;

                    if (!string.IsNullOrWhiteSpace(errorCode) &&
                        exitCodeObj is int exitCode)
                    {
                        cliExitCodeMap[errorCode] = exitCode;
                    }
                }
            }

            // ALSO detect fallback exit code override if present
            if (root.TryGetValue("fallback_exit_code", out var fallbackObj) &&
                fallbackObj is string f && !string.IsNullOrWhiteSpace(f))
            {
                fallbackErrorCode = f; // existing field!
            }

            
            if (root is null)
            {
                logger?.Warning("Parsed YAML contract at {Path} but root document was null.", path);
                continue;
            }

            // Optional: exception -> error code mappings (if present in the contract).
            if (root.TryGetValue("exception_mappings", out var exceptionMappingsObj) &&
                exceptionMappingsObj is IDictionary exceptionMappingsDict)
            {
                foreach (DictionaryEntry entry in exceptionMappingsDict)
                {
                    var exceptionTypeName = entry.Key as string;
                    var errorCode = entry.Value as string;

                    if (string.IsNullOrWhiteSpace(exceptionTypeName) ||
                        string.IsNullOrWhiteSpace(errorCode))
                    {
                        continue;
                    }

                    Type? exceptionType = null;

                    try
                    {
                        // First, try the standard resolution path.
                        exceptionType = Type.GetType(exceptionTypeName, throwOnError: false);

                        // If that fails, walk all loaded assemblies so names like
                        // "Bookscraper.Core.ErrorHandling.InvalidConfigurationException"
                        // can resolve even though the type lives in bookscraper.core.
                        if (exceptionType is null)
                        {
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                exceptionType = assembly.GetType(exceptionTypeName, throwOnError: false);
                                if (exceptionType is not null)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Warning(ex,
                            "Failed to resolve exception type {ExceptionType} from contract file {Path}",
                            exceptionTypeName,
                            path);
                    }

                    if (exceptionType is not null)
                    {
                        exceptionToErrorCode[exceptionType] = errorCode!;
                    }
                    else
                    {
                        logger?.Warning(
                            "Could not resolve exception type {ExceptionType} from contract file {Path}; mapping will be ignored.",
                            exceptionTypeName,
                            path);
                    }

                }
            }

            // Error code -> metadata.
            if (root.TryGetValue("errors", out var errorsObj) &&
                errorsObj is IDictionary errorsDict)
            {
                foreach (DictionaryEntry entry in errorsDict)
                {
                    var errorCode = entry.Key as string;
                    if (string.IsNullOrWhiteSpace(errorCode) ||
                        entry.Value is not IDictionary metadataDict)
                    {
                        continue;
                    }

                    bool isRetryable = false;
                    if (metadataDict.Contains("is_retryable") &&
                        metadataDict["is_retryable"] is bool retryFlag)
                    {
                        isRetryable = retryFlag;
                    }

                    string? description = null;
                    if (metadataDict.Contains("description") &&
                        metadataDict["description"] is string descriptionValue)
                    {
                        description = descriptionValue;
                    }

                    string? title = null;
                    if (metadataDict.Contains("title") &&
                        metadataDict["title"] is string titleValue)
                    {
                        title = titleValue;
                    }

                    var defaultMessage =
                        !string.IsNullOrWhiteSpace(description) ? description! :
                        !string.IsNullOrWhiteSpace(title) ? title! :
                        errorCode!;

                    errorCodeToMetadata[errorCode!] =
                        new ErrorCatalogMappingProvider.ErrorMetadata(
                            errorCode!,
                            isRetryable,
                            defaultMessage);
                }
            }

            // Allow later files to override the fallback error code if they specify one.
            if (root.TryGetValue("fallback_error_code", out var fallbackObj2) &&
                fallbackObj2 is string fallbackStr &&
                !string.IsNullOrWhiteSpace(fallbackStr))
            {
                fallbackErrorCode = fallbackStr;
            }
        }

        // Determine fallback exit code (must be an int)
        int fallbackExitCode = 1; // safe default

        if (cliExitCodeMap.TryGetValue(fallbackErrorCode, out var mappedExit))
        {
            fallbackExitCode = mappedExit;
        }

        
        return new ErrorCatalogMappingProvider(
            exceptionToErrorCode,
            errorCodeToMetadata,
            fallbackErrorCode,
            cliExitCodeMap,
            fallbackExitCode);

    }
}