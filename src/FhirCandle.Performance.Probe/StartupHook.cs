// <copyright file="StartupHook.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Performance.Probe;

/// <summary>The runtime requires this exact, namespace-free startup-hook type.</summary>
public static class StartupHook
{
    private static Task? _session;

    /// <summary>Whether this process explicitly opted in to a probe session.</summary>
    public static bool IsActive => _session is not null;

    /// <summary>Remains completely inert unless child-local probe configuration is present.</summary>
    public static void Initialize()
    {
        ProbeConfiguration? configuration = ProbeConfiguration.FromEnvironment(Environment.GetEnvironmentVariable);
        if (configuration is null)
        {
            return;
        }

        if (_session is not null)
        {
            throw new InvalidOperationException("The performance startup hook was initialized twice.");
        }

        HostObservation host = new();
        _session = RunAsync(configuration, host);
    }

    private static async Task RunAsync(ProbeConfiguration configuration, HostObservation host)
    {
        using (host)
        {
            try
            {
                await ProbeSession.RunAsync(configuration, host);
            }
            catch (Exception exception)
            {
                // An opted-in child must never continue as an apparently uninstrumented success.
                Console.Error.WriteLine($"Performance probe failed: {exception.GetType().Name}: {exception.Message}");
                Environment.Exit(78);
            }
        }
    }
}
