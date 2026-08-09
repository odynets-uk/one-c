using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions.Services;
using OneC.Infrastructure;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Profiles;
using OneC.Infrastructure.Security;

namespace OneC.Cli;

/// <summary>
///     Represents CLI application entry service.
/// </summary>
public sealed class CliApp
{
    private readonly ITestConnectionService _testConnectionService;
    private readonly IMetadataService _metadataService;
    private readonly IGetCatalogService _getCatalogService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CliApp> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CliApp" /> class.
    /// </summary>
    /// <param name="testConnectionService">1C connection test service.</param>
    /// <param name="metadataService">Metadata service.</param>
    /// <param name="getCatalogService">Catalog extraction service.</param>
    /// <param name="configuration">Configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public CliApp(
        ITestConnectionService testConnectionService,
        IMetadataService metadataService,
        IGetCatalogService getCatalogService,
        IConfiguration configuration,
        ILogger<CliApp> logger)
    {
        _testConnectionService = testConnectionService;
        _metadataService = metadataService;
        _getCatalogService = getCatalogService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    ///     Runs the CLI application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        switch (command)
        {
            case "test-connection":
                await TestConnectionAsync(cancellationToken);
                break;

            case "list-catalogs":
                await ListCatalogsAsync(cancellationToken);
                break;

            case "list-enums":
                await ListEnumsAsync(cancellationToken);
                break;

            case "show-catalog":
                await ShowCatalogAsync(args, cancellationToken);
                break;

            case "get-catalog":
                await GetCatalogAsync(args, cancellationToken);
                break;

            case "inspect-register":
                await InspectRegisterAsync(args, cancellationToken);
                break;

            case "help":
            case "--help":
            case "-h":
                PrintHelp();
                break;

            default:
                _logger.LogError("Unknown command '{Command}'.", command);
                PrintHelp();
                break;
        }
    }

    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _testConnectionService.TestAsync(cancellationToken);

            Console.WriteLine();
            Console.WriteLine("=== 1C Connection Test ===");
            Console.WriteLine($"  Platform version:      {result.PlatformVersion}");
            Console.WriteLine($"  Configuration version: {result.ConfigurationVersion}");
            Console.WriteLine("===========================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed: {ErrorMessage}", ex.Message);

            Console.WriteLine();
            Console.WriteLine("=== 1C Connection Test Failed ===");
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine("===================================");
            Console.WriteLine();
        }
    }

    private async Task ListCatalogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _metadataService.GetMetadataAsync(cancellationToken);

            Console.WriteLine();
            Console.WriteLine($"=== Catalogs ({metadata.Catalogs.Count}) ===");
            foreach (var catalog in metadata.Catalogs.OrderBy(c => c.Name))
            {
                Console.WriteLine($"  {catalog.Name} ({catalog.Fields.Count} fields)");
            }

            Console.WriteLine("===========================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list catalogs: {ErrorMessage}", ex.Message);
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private async Task ListEnumsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _metadataService.GetMetadataAsync(cancellationToken);

            Console.WriteLine();
            Console.WriteLine($"=== Enums ({metadata.Enums.Count}) ===");
            foreach (var enumDef in metadata.Enums.OrderBy(e => e.Name))
            {
                Console.WriteLine($"  {enumDef.Name}: {string.Join(", ", enumDef.Values)}");
            }

            Console.WriteLine("===========================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list enums: {ErrorMessage}", ex.Message);
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private async Task ShowCatalogAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: OneC.Cli show-catalog <catalog-name>");
            return;
        }

        try
        {
            var metadata = await _metadataService.GetMetadataAsync(cancellationToken);
            var catalog = metadata.FindCatalog(args[1]);

            if (catalog is null)
            {
                Console.WriteLine($"Catalog '{args[1]}' not found.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"=== Catalog: {catalog.Name} ({catalog.Fields.Count} fields) ===");
            Console.WriteLine($"  XSD Type: {catalog.XsdTypeName}");
            Console.WriteLine($"  Ref Type: {catalog.RefTypeName}");
            Console.WriteLine($"  Hierarchical: {catalog.IsHierarchical}");
            Console.WriteLine();
            Console.WriteLine($"  {"#",-4} {"Field",-45} {"Type",-15} {"XSD Type",-55} {"Req",-4}");
            Console.WriteLine($"  {new string('-', 4),-4} {new string('-', 45),-45} {new string('-', 15),-15} {new string('-', 55),-55} {new string('-', 4),-4}");

            var index = 1;
            foreach (var field in catalog.Fields)
            {
                Console.WriteLine(
                    $"  {index,-4} {field.Name,-45} {field.Type,-15} {field.XsdType,-55} {(field.IsOptional ? "no" : "yes"),-4}");
                index++;
            }

            Console.WriteLine("===========================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show catalog: {ErrorMessage}", ex.Message);
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private async Task GetCatalogAsync(string[] args, CancellationToken cancellationToken)
    {
        // Usage: get-catalog --profile categories [--mode full|incremental] [--batch-size N]
        var profilePath = GetFlagValue(args, "--profile");
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            Console.WriteLine("Usage: OneC.Cli get-catalog --profile <profile-file.json> [--mode full|incremental] [--batch-size N]");
            return;
        }

        var mode = GetFlagValue(args, "--mode") ?? "full";
        var batchSize = GetBatchSize(args);

        try
        {
            var profile = ProfileLoader.Load(profilePath);
            var count = await _getCatalogService.ExecuteAsync(profile, batchSize, cancellationToken);

            Console.WriteLine();
            Console.WriteLine($"=== Catalog Extraction ===");
            Console.WriteLine($"  Profile:     {profile.Name}");
            Console.WriteLine($"  Mode:        {mode}");
            Console.WriteLine($"  Batch size:  {batchSize}");
            Console.WriteLine($"  Records:     {count}");
            Console.WriteLine("===========================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get catalog: {ErrorMessage}", ex.Message);
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Diagnostic: inspects the fields of a 1C register by running a query.
    ///     Usage: inspect-register info|accum <register-name>
    /// </summary>
    private async Task InspectRegisterAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: OneC.Cli inspect-register info|accum <register-name> [remainders|last]");
            return;
        }

        var isCatalog = args[1].Equals("catalog", StringComparison.OrdinalIgnoreCase)
                        || args[1].Equals("cat", StringComparison.OrdinalIgnoreCase)
                        || args[1].Equals("справочник", StringComparison.OrdinalIgnoreCase);

        var registerKind = args[1].ToLowerInvariant() switch
        {
            "info" or "information" => "InformationRegister",
            "accum" or "accumulation" => "AccumulationRegister",
            _ => "InformationRegister",
        };

        var registerName = args[2];
        var virtualMode = args.Length > 3 ? args[3].ToLowerInvariant() : string.Empty;

        try
        {
            var connectionStringName = _configuration["ActiveConnection"] ?? "Kplus";
            var connectionString = _configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine($"Connection string '{connectionStringName}' is not configured.");
                return;
            }

            var decrypted = DecryptPassword(connectionString);

            using var session = new ComSession(
                new ComConnector(Logging.CreateLogger<ComConnector>()),
                Logging.CreateLogger<ComSession>());
            session.Connect(decrypted);

            // Use SELECT * to discover actual columns of the register.
            // Row limit is enforced in the iteration loop below.
            dynamic query = session.Connection.NewObject("Query");

            // Optional virtual tables: remainders (accumulation) / last slice (information).
            // Physical tables use Latin prefixes (AccumulationRegister.),
            // virtual tables require Russian prefixes (РегистрНакопления.).
            var russianKind = registerKind switch
            {
                "AccumulationRegister" => "РегистрНакопления",
                "InformationRegister" => "РегистрСведений",
                _ => registerKind,
            };

            var tableName = isCatalog
                ? $"Справочник.{registerName}"
                : virtualMode switch
                {
                    "remainders" or "остатки" or "virt" => $"{russianKind}.{registerName}.Остатки()",
                    "last" or "срез" or "срезпоследних" => $"{russianKind}.{registerName}.СрезПоследних()",
                    _ => $"{registerKind}.{registerName}",
                };

            query.Text = $"SELECT * FROM {tableName}";

            dynamic result = query.Execute();
            dynamic selection = result.Choose();

            Console.WriteLine();
            Console.WriteLine($"=== {registerKind}.{registerName} (first 5 rows) ===");

            var rowIndex = 0;
            while (selection.Next() && rowIndex < 5)
            {
                Console.WriteLine($"--- Row {rowIndex + 1} ---");
                PrintObjectMembers(selection, session);
                rowIndex++;
            }

            if (rowIndex == 0)
            {
                Console.WriteLine("  (no rows)");
            }

            Console.WriteLine("===========================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect register: {ErrorMessage}", ex.Message);
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Prints all accessible members of a COM selection row, attempting to read
    ///     both Latin and Cyrillic property names.
    /// </summary>
    private void PrintObjectMembers(dynamic selection, ComSession session)
    {
        object obj = selection;
        var type = obj.GetType();

        // Try InvokeMember on a curated set of candidate fields.
        var candidates = new[]
        {
            "Номенклатура", "ТипЦеныНоменклатуры", "ТипЦен", "Цена",
            "Склад", "КоличествоОстаток", "СтоимостьОстаток",
            "ВидДвижения", "Период", "Регистратор", "Active",
            "Количество", "Стоимость", "ТипЦены",
            "ХарактеристикаНоменклатуры", "Качество", "СтатусПартии",
            "Ref", "Description", "Code",
            "ПроцентНаценки", "ТипЦенБазовая", "БазовыйТипЦен", "СпособРасчетаЦены",
            "ВалютаЦены", "ЕдиницаИзмерения", "Наценка", "Рассчитывается",
            "ПроцентСкидкиНаценки", "СкидкаНаценка",
            "ЦенаЗакупочная", "БазоваяЦена", "ЦенаБазовая", "ПроцентНаценки",
            "ЦенаБазовая", "ЗакупочнаяЦена", "ЦенаЗакупки",
        };

        foreach (var name in candidates)
        {
            try
            {
                var value = type.InvokeMember(
                    name,
                    BindingFlags.GetField | BindingFlags.GetProperty,
                    null,
                    obj,
                    null);
                Console.WriteLine($"  {name} = {FormatValue(value, session)}");
            }
            catch (Exception)
            {
                // Field not present — skip.
            }
        }
    }

    private string? FormatValue(object? value, ComSession session)
    {
        if (value is null || value is DBNull)
        {
            return "<null>";
        }

        if (Marshal.IsComObject(value))
        {
            // Ref/GUID — try УникальныйИдентификатор, else String().
            try
            {
                var type = value.GetType();
                var guid = type.InvokeMember(
                    "УникальныйИдентификатор",
                    BindingFlags.InvokeMethod,
                    null,
                    value,
                    null);
                if (guid is not null)
                {
                    return "(Ref) " + session.String(guid);
                }
            }
            catch (Exception)
            {
                // ignore
            }

            try
            {
                return "(COM) " + session.String(value);
            }
            catch (Exception)
            {
                return "(COM) unknown";
            }
        }

        return value.ToString();
    }

    private static string? GetFlagValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int GetBatchSize(string[] args)
    {
        var value = GetFlagValue(args, "--batch-size");
        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return -1; // Default: all records.
    }

    private static string DecryptPassword(string connectionString)
    {
        const string pwdMarker = "Pwd=\"";

        var idx = connectionString.IndexOf(pwdMarker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return connectionString;
        }

        var start = idx + pwdMarker.Length;
        var end = connectionString.IndexOf('"', start);
        if (end < 0)
        {
            return connectionString;
        }

        var pwd = connectionString[start..end];

        if (pwd.Any(c => char.IsLetter(c) && !char.IsWhiteSpace(c)))
        {
            try
            {
                var decrypted = ConnectionStringProtector.Decrypt(pwd);
                return connectionString[..start] + decrypted + connectionString[end..];
            }
            catch (FormatException)
            {
                // treat as plain text
            }
        }

        return connectionString;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
                           OneC CLI - 1C data synchronizer

                           Usage:
                             OneC.Cli <command> [options]

                           Commands:
                             test-connection    Test 1C COM connection
                             list-catalogs      List catalogs from XSD schema
                             list-enums         List enums from XSD schema
                             show-catalog <name> Show catalog fields with types
                             get-catalog --profile <file> [--mode full|incremental] [--batch-size N]
                             inspect-register info|accum <name>  Inspect register fields (diagnostic)
                             help               Show this help

                           Examples:
                             OneC.Cli test-connection
                             OneC.Cli list-catalogs
                             OneC.Cli list-enums
                             OneC.Cli show-catalog Номенклатура
                             OneC.Cli get-catalog --profile profiles/categories.json --batch-size -1
                             OneC.Cli inspect-register info ЦеныНоменклатуры
                             OneC.Cli inspect-register accum ТоварыНаСкладах
                           """);
    }

}
