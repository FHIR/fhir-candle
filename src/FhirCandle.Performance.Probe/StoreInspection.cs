// <copyright file="StoreInspection.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FhirCandle.Performance.Probe;

/// <summary>A stopped, public-contract view of the addressed store.</summary>
public sealed record TenantSnapshot(
    string[] TenantNames,
    Dictionary<string, string?> Configuration,
    SortedDictionary<string, int> ResourceCounts,
    Dictionary<string, ResourceIdentity?> RequestedResources,
    SortedDictionary<string, ReceiptSnapshot[]> ReceivedNotifications,
    int ReceiptKeyCount,
    int ReceiptCount,
    int EventCount,
    string[] LoadedPackageDirectives,
    string[] LoadedPackageIds,
    string[] LoadedSupplements,
    int SubscriptionCount,
    int TopicCount);

/// <summary>Identity obtained from an actual resource-store dictionary entry.</summary>
public sealed record ResourceIdentity(string ResourceType, string Id);

/// <summary>Every field needed to distinguish receipt registration from a dictionary key.</summary>
public sealed record ReceiptSnapshot(
    string BundleId,
    DateTimeOffset ProcessedDateTime,
    string SubscriptionReference,
    string SubscriptionTopicCanonical,
    string Status,
    string NotificationType,
    long? EventsSinceSubscriptionStart,
    NotificationEventSnapshot[] NotificationEvents,
    string[] Errors);

/// <summary>One parsed notification event, including its focus and every additional context reference.</summary>
public sealed record NotificationEventSnapshot(
    string Id,
    long? EventNumber,
    DateTimeOffset? Timestamp,
    string FocusReference,
    string[] AdditionalContextReferences);

/// <summary>A loaded assembly identity; dependency/package correlation belongs to the external runner.</summary>
public sealed record AssemblySnapshot(
    string Name,
    string Path,
    string AssemblyVersion,
    string FileVersion,
    string InformationalVersion,
    string Mvid,
    string Sha256);

/// <summary>Effective host configuration, not merely the requested command line.</summary>
public sealed record HostSnapshot(
    Dictionary<string, string?> Configuration,
    string[] ListenerAddresses,
    Dictionary<string, string?> Logging,
    string WorkingDirectory,
    string LocalApplicationData,
    string UserProfile,
    int CachedPackageCount);

/// <summary>Stopped runtime and store evidence. This is not a measured counter window.</summary>
public sealed record ProbeSnapshot(
    int ProcessId,
    DateTimeOffset CapturedUtc,
    string FrameworkDescription,
    string RuntimeVersion,
    string Architecture,
    bool ServerGc,
    string GcLatencyMode,
    Dictionary<string, string?> RuntimeEnvironment,
    Dictionary<string, string> GcConfiguration,
    long TotalAllocatedBytes,
    long ManagedBytes,
    long HeapSizeBytes,
    long FragmentedBytes,
    long TotalCommittedBytes,
    int[] CollectionCounts,
    long GcPauseTicks,
    TenantSnapshot Tenant,
    HostSnapshot Host,
    AssemblySnapshot[] Assemblies);

/// <summary>Narrow read-only reflection over loaded public singleton and dictionary contracts.</summary>
public static class StoreInspection
{
    private const int MaximumEntries = 100000;

    /// <summary>Finds the already-created product singleton without constructing or loading a manager.</summary>
    public static object ResolveManager()
    {
        Type managerType = ResolveLoadedType("fhir.candle.Services.FhirStoreManager");
        PropertyInfo instance = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidDataException("Missing public FhirStoreManager.Instance.");
        PropertyInfo interfaceInstance = managerType.GetProperty("IInstance", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidDataException("Missing public FhirStoreManager.IInstance.");
        object manager = instance.GetValue(null) ?? throw new InvalidDataException("The manager has not been initialized.");
        if (!ReferenceEquals(manager, interfaceInstance.GetValue(null)))
        {
            throw new InvalidDataException("Manager singleton and interface singleton disagree.");
        }

        _ = DictionaryContract(manager);
        return manager;
    }

    /// <summary>Reads explicit interfaces, exact counts and complete receipt lists outside measurement.</summary>
    public static TenantSnapshot InspectTenant(object manager, string tenant, IReadOnlyList<string> requestedResources)
    {
        ArgumentNullException.ThrowIfNull(manager);
        KeyValuePair<string, object>[] tenants = ReadDictionary(manager);
        if ((tenants.Length != 1) || (tenants[0].Key != tenant))
        {
            throw new InvalidDataException("Expected exactly the addressed tenant: " + tenant);
        }

        object store = tenants[0].Value;
        object config = Required(store, "Config");
        Dictionary<string, string?> configuration = ReadSettings(config,
            ["FhirVersion", "ControllerName", "BaseUrl", "MaxResourceCount", "MaxSubscriptionExpirationMinutes",
             "ProtectLoadedContent", "SupportNotChanged", "SmartRequired", "SmartAllowed", "AllowExistingId", "AllowCreateAsUpdate"]);
        configuration.Add("LoadDirectory", (ReadPublic(config, "LoadDirectory") as DirectoryInfo)?.FullName);
        configuration.Add("Strict", Optional(config, "Strict")?.ToString());
        configuration.Add("FhirPackageCount", CountDictionary(Required(config, "FhirPackages")).ToString(CultureInfo.InvariantCulture));

        SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
        Dictionary<string, ResourceIdentity?> identities = new(StringComparer.Ordinal);
        KeyValuePair<string, object>[] resources = ReadDictionary(store);
        foreach (KeyValuePair<string, object> resource in resources)
        {
            // IResourceStore's object-valued view coexists with the versioned resource view.
            counts.Add(resource.Key, CountDictionary(resource.Value, expectedValueType: typeof(object)));
        }

        if (requestedResources.Count > 32)
        {
            throw new InvalidDataException("Too many requested inspection identities.");
        }

        foreach (string identity in requestedResources)
        {
            string[] parts = identity.Split('/');
            if ((parts.Length != 2) || string.IsNullOrEmpty(parts[1]) || identities.ContainsKey(identity))
            {
                throw new InvalidDataException("Invalid or duplicate requested resource identity.");
            }

            object resourceStore = resources.SingleOrDefault(resource => resource.Key == parts[0]).Value
                ?? throw new InvalidDataException("Missing resource-store contract: " + parts[0]);
            Type dictionary = DictionaryContract(resourceStore, expectedValueType: typeof(object));
            bool contains = (bool)(dictionary.GetMethod("ContainsKey")!.Invoke(resourceStore, [parts[1]])
                ?? throw new InvalidDataException("Missing ContainsKey result."));
            if (!contains)
            {
                identities.Add(identity, null);
                continue;
            }

            object value = dictionary.GetProperty("Item")!.GetValue(resourceStore, [parts[1]])
                ?? throw new InvalidDataException("Null stored resource.");
            identities.Add(identity, new ResourceIdentity(Text(value, "TypeName"), Text(value, "Id")));
        }

        object received = Required(store, "ReceivedNotifications");
        SortedDictionary<string, ReceiptSnapshot[]> receipts = new(StringComparer.Ordinal);
        int receiptCount = 0;
        int eventCount = 0;
        foreach (KeyValuePair<string, object> pair in ReadDictionary(received))
        {
            List<ReceiptSnapshot> list = [];
            foreach (object status in Items(pair.Value))
            {
                NotificationEventSnapshot[] events = Items(Required(status, "NotificationEvents")).Select(notificationEvent =>
                    new NotificationEventSnapshot(
                        Text(notificationEvent, "Id"),
                        NullableLong(notificationEvent, "EventNumber"),
                        NullableTimestamp(notificationEvent, "Timestamp"),
                        Text(notificationEvent, "FocusReference"),
                        Strings(Required(notificationEvent, "AdditionalContextReferences")))).ToArray();
                list.Add(new ReceiptSnapshot(
                    Text(status, "BundleId"),
                    (DateTimeOffset)Required(status, "ProcessedDateTime"),
                    Text(status, "SubscriptionReference"),
                    Text(status, "SubscriptionTopicCanonical"),
                    Text(status, "Status"),
                    Text(status, "NotificationType"),
                    NullableLong(status, "EventsSinceSubscriptionStart"),
                    events,
                    Strings(Required(status, "Errors"))));
                eventCount = checked(eventCount + events.Length);
            }

            // An empty list is retained as evidence, not replaced by a fabricated receipt.
            receipts.Add(pair.Key, list.ToArray());
            receiptCount = checked(receiptCount + list.Count);
        }

        return new TenantSnapshot(
            tenants.Select(pair => pair.Key).ToArray(), configuration, counts, identities, receipts,
            CountDictionary(received), receiptCount, eventCount,
            Strings(Required(store, "LoadedPackageDirectives")),
            Strings(Required(store, "LoadedPackageIds")),
            Strings(Required(store, "LoadedSupplements")),
            Items(Required(store, "CurrentSubscriptions")).Length,
            Items(Required(store, "CurrentTopics")).Length);
    }

    /// <summary>Captures actual host, loaded assemblies and stopped GC observations.</summary>
    public static ProbeSnapshot Capture(IServiceProvider services, string tenant, IReadOnlyList<string> requestedResources)
    {
        TenantSnapshot store = InspectTenant(ResolveManager(), tenant, requestedResources);
        HostSnapshot host = InspectHost(services);
        AssemblySnapshot[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(InspectAssembly).OrderBy(assembly => assembly.Name, StringComparer.Ordinal).ToArray();
        GCMemoryInfo memory = GC.GetGCMemoryInfo();
        return new ProbeSnapshot(
            Environment.ProcessId, DateTimeOffset.UtcNow, RuntimeInformation.FrameworkDescription,
            Environment.Version.ToString(), RuntimeInformation.ProcessArchitecture.ToString(),
            GCSettings.IsServerGC, GCSettings.LatencyMode.ToString(),
            new[] { "DOTNET_gcServer", "DOTNET_gcConcurrent", "DOTNET_TieredCompilation", "DOTNET_TieredPGO",
                "DOTNET_ReadyToRun", "DOTNET_ROLL_FORWARD", "DOTNET_MULTILEVEL_LOOKUP" }
                .ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal),
            GC.GetConfigurationVariables().ToDictionary(pair => pair.Key,
                pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.Ordinal),
            GC.GetTotalAllocatedBytes(true), GC.GetTotalMemory(false),
            memory.HeapSizeBytes, memory.FragmentedBytes, memory.TotalCommittedBytes,
            [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)],
            GC.GetTotalPauseDuration().Ticks, store, host, assemblies);
    }

    /// <summary>Requests the ordinary host shutdown through its existing public lifetime service.</summary>
    public static void RequestShutdown(IServiceProvider services)
    {
        Type lifetimeType = ResolveLoadedType("Microsoft.Extensions.Hosting.IHostApplicationLifetime");
        object lifetime = services.GetService(lifetimeType)
            ?? throw new InvalidDataException("Missing public host lifetime.");
        lifetimeType.GetMethod("StopApplication")!.Invoke(lifetime, null);
    }

    internal static Type ResolveLoadedType(string fullName)
    {
        Type[] matches = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false, false))
            .OfType<Type>().Distinct().ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new InvalidDataException($"Expected one already-loaded public type {fullName}; found {matches.Length}.");
    }

    internal static object Required(object instance, string name) =>
        ReadPublic(instance, name) ?? throw new InvalidDataException("Null public contract: " + name);

    internal static object? ReadPublic(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo[] contracts = type.GetInterfaces()
            .SelectMany(contract => contract.GetProperties())
            .Where(property => (property.Name == name) && (property.GetIndexParameters().Length == 0))
            .Distinct().ToArray();
        if (contracts.Length > 1)
        {
            throw new InvalidDataException("Ambiguous public interface property: " + name);
        }

        PropertyInfo? property = contracts.SingleOrDefault() ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        // Some historical configuration members are public fields, not properties.
        FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return field is not null ? field.GetValue(instance)
            : throw new InvalidDataException($"Missing public contract {type.FullName}.{name}.");
    }

    private static object? Optional(object instance, string name)
    {
        Type type = instance.GetType();
        return (type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) is not null) ||
            (type.GetField(name, BindingFlags.Public | BindingFlags.Instance) is not null)
            ? ReadPublic(instance, name) : null;
    }

    private static Type DictionaryContract(object instance, Type? expectedValueType = null)
    {
        Type type = instance.GetType();
        Type[] contracts = type.GetInterfaces().Where(contract =>
            contract.IsGenericType && (contract.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))).ToArray();
        Type[] matches = contracts.Where(contract => (contract.GenericTypeArguments[0] == typeof(string)) &&
            ((expectedValueType is null) || (contract.GenericTypeArguments[1] == expectedValueType))).ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new InvalidDataException(
                $"Missing or ambiguous IReadOnlyDictionary<string, {expectedValueType?.FullName ?? "T"}> contract on {type.FullName}; " +
                $"found {matches.Length} matches among [{string.Join(", ", contracts.Select(contract => contract.ToString()))}].");
    }

    private static int CountDictionary(object instance, Type? expectedValueType = null)
    {
        Type dictionary = DictionaryContract(instance, expectedValueType);
        Type pair = typeof(KeyValuePair<,>).MakeGenericType(dictionary.GenericTypeArguments);
        Type collection = typeof(IReadOnlyCollection<>).MakeGenericType(pair);
        int count = (int)(collection.GetProperty("Count")!.GetValue(instance)
            ?? throw new InvalidDataException("Missing explicit dictionary Count."));
        return (count >= 0) && (count <= MaximumEntries) ? count
            : throw new InvalidDataException("Dictionary exceeds inspection bounds.");
    }

    private static KeyValuePair<string, object>[] ReadDictionary(object instance)
    {
        Type dictionary = DictionaryContract(instance);
        int count = CountDictionary(instance);
        string[] keys = Strings(dictionary.GetProperty("Keys")!.GetValue(instance)
            ?? throw new InvalidDataException("Missing dictionary Keys."));
        if ((keys.Length != count) || (keys.Distinct(StringComparer.Ordinal).Count() != count))
        {
            throw new InvalidDataException("Dictionary enumeration and explicit Count disagree.");
        }

        return keys.Order(StringComparer.Ordinal).Select(key => new KeyValuePair<string, object>(
            key, dictionary.GetProperty("Item")!.GetValue(instance, [key])
                ?? throw new InvalidDataException("Null dictionary entry."))).ToArray();
    }

    private static object[] Items(object value)
    {
        if (value is not IEnumerable enumerable)
        {
            throw new InvalidDataException("Missing public enumerable contract.");
        }

        List<object> items = [];
        foreach (object? item in enumerable)
        {
            if ((item is null) || (items.Count == MaximumEntries))
            {
                throw new InvalidDataException("Null or oversized inspection collection.");
            }

            items.Add(item);
        }

        return items.ToArray();
    }

    private static string[] Strings(object value) => Items(value).Select(item => item is string text
        ? text : throw new InvalidDataException("Expected a string collection.")).ToArray();

    private static string Text(object instance, string name) =>
        Convert.ToString(Required(instance, name), CultureInfo.InvariantCulture)
            ?? throw new InvalidDataException("Missing string: " + name);

    private static long? NullableLong(object instance, string name)
    {
        object? value = ReadPublic(instance, name);
        return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset? NullableTimestamp(object instance, string name) =>
        (DateTimeOffset?)ReadPublic(instance, name);

    private static Dictionary<string, string?> ReadSettings(object instance, string[] names)
    {
        Dictionary<string, string?> result = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            object? value = ReadPublic(instance, name);
            result.Add(name, value is null ? null : value is IEnumerable and not string
                ? string.Join("|", Strings(value)) : Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static HostSnapshot InspectHost(IServiceProvider services)
    {
        object config = services.GetService(ResolveLoadedType("FhirCandle.Configuration.CandleConfig"))
            ?? throw new InvalidDataException("Missing effective CandleConfig.");
        Dictionary<string, string?> settings = ReadSettings(config,
            ["PublicUrl", "ListenPort", "OpenBrowser", "MaxResourceCount", "FhirCacheDirectory",
             "UseOfficialRegistries", "PublishedPackages", "CiPackages", "LoadPackageExamples",
             "ReferenceImplementation", "EnableMcp", "SourceDirectory", "ProtectLoadedContent",
             "TenantsR4", "TenantsR4B", "TenantsR5", "SupportNotChanged", "SmartRequiredTenants",
             "SmartOptionalTenants", "AllowExistingId", "AllowCreateAsUpdate", "MaxSubscriptionExpirationMinutes",
             "OpenTelemetryEndpoint", "OpenTelemetryTracesEndpoint", "OpenTelemetryMetricsEndpoint", "OpenTelemetryLogsEndpoint"]);
        settings.Add("UiDisabled", Convert.ToString(Optional(config, "Headless") ?? Required(config, "DisableUi"), CultureInfo.InvariantCulture));
        settings.Add("HasSenderConfiguration", new[] { "ZulipEmail", "ZulipKey", "ZulipUrl", "SmtpHost", "SmtpUser", "SmtpPassword" }
            .Any(name => !string.IsNullOrEmpty(Convert.ToString(ReadPublic(config, name), CultureInfo.InvariantCulture))).ToString());

        Type serverType = ResolveLoadedType("Microsoft.AspNetCore.Hosting.Server.IServer");
        object server = services.GetService(serverType) ?? throw new InvalidDataException("Missing active ASP.NET server.");
        object features = serverType.GetProperty("Features")!.GetValue(server)
            ?? throw new InvalidDataException("Missing server features.");
        object[] addressFeatures = Items(features).Where(pair =>
            ((Type)Required(pair, "Key")).FullName == "Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature")
            .Select(pair => Required(pair, "Value")).ToArray();
        if (addressFeatures.Length != 1)
        {
            throw new InvalidDataException("Missing or ambiguous actual server address feature.");
        }

        string[] addresses = Strings(Required(addressFeatures[0], "Addresses"));
        Type configurationType = ResolveLoadedType("Microsoft.Extensions.Configuration.IConfiguration");
        object configuration = services.GetService(configurationType)
            ?? throw new InvalidDataException("Missing effective host configuration.");
        Dictionary<string, string?> logging = new(StringComparer.Ordinal);
        foreach (string category in new[] { "Default", "Microsoft.AspNetCore", "Microsoft.Hosting.Lifetime" })
        {
            string key = "Logging:LogLevel:" + category;
            logging.Add(key, (string?)configurationType.GetProperty("Item")!.GetValue(configuration, [key]));
        }

        object packages = services.GetService(ResolveLoadedType("fhir.candle.Services.IFhirPackageService"))
            ?? throw new InvalidDataException("Missing package service.");
        return new HostSnapshot(settings, addresses, logging, Environment.CurrentDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            CountDictionary(Required(packages, "PackagesByDirective")));
    }

    private static AssemblySnapshot InspectAssembly(Assembly assembly)
    {
        string path = assembly.Location;
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidDataException("A non-dynamic loaded assembly has no auditable file: " + assembly.FullName);
        }

        using FileStream file = File.OpenRead(path);
        return new AssemblySnapshot(
            assembly.GetName().Name ?? throw new InvalidDataException("Unnamed loaded assembly."),
            path, assembly.GetName().Version?.ToString() ?? string.Empty,
            FileVersionInfo.GetVersionInfo(path).FileVersion ?? string.Empty,
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty,
            assembly.ManifestModule.ModuleVersionId.ToString("D"),
            Convert.ToHexString(SHA256.HashData(file)));
    }
}

/// <summary>Captures the existing public host service provider from the BCL hosting diagnostic event.</summary>
internal sealed class HostObservation : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly IDisposable _allListeners;
    private IDisposable? _hostingListener;
    private IServiceProvider? _services;

    internal HostObservation()
    {
        _allListeners = DiagnosticListener.AllListeners.Subscribe(this);
    }

    internal IServiceProvider Services => Volatile.Read(ref _services)
        ?? throw new InvalidDataException("No public HostBuilt service provider was observed.");

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "Microsoft.Extensions.Hosting")
        {
            if (_hostingListener is not null)
            {
                throw new InvalidDataException("Multiple hosting diagnostic listeners.");
            }

            _hostingListener = listener.Subscribe(this, name => name == "HostBuilt");
        }
    }

    public void OnNext(KeyValuePair<string, object?> notification)
    {
        if ((notification.Key == "HostBuilt") && (notification.Value is not null))
        {
            IServiceProvider services = (IServiceProvider)StoreInspection.Required(notification.Value, "Services");
            if (Interlocked.CompareExchange(ref _services, services, null) is not null)
            {
                throw new InvalidDataException("Multiple hosts were built in the measured child.");
            }
        }
    }

    public void OnError(Exception error) => throw new InvalidOperationException("Hosting observation failed.", error);

    public void OnCompleted()
    {
    }

    public void Dispose()
    {
        _hostingListener?.Dispose();
        _allListeners.Dispose();
    }
}
