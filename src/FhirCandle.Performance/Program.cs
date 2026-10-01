// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Performance;

/// <summary>Entry point for the sealed-artifact experiment, not a product server.</summary>
public static class Program
{
    /// <summary>Prepares inputs or performs the six phase-one feasibility launches.</summary>
    public static async Task<int> Main(string[] args)
    {
        if ((args.Length == 0) || args.SequenceEqual(new[] { "--help" }))
        {
            Console.WriteLine("prepare --manifest <absolute-path>");
            Console.WriteLine("run --manifest <absolute-path> --preflight-only");
            Console.WriteLine("Full run and render are unavailable until phases 2 and 3.");
            return args.Length == 0 ? 2 : 0;
        }

        if ((args[0] == "render") || ((args[0] == "run") && !args.Contains("--preflight-only", StringComparer.Ordinal)))
        {
            Console.Error.WriteLine("Not implemented: full workload, measurement and rendering require later phases. No benchmark is complete.");
            return 2;
        }

        bool prepare = (args.Length == 3) && (args[0] == "prepare");
        bool preflight = (args.Length == 4) && (args[0] == "run") && (args[3] == "--preflight-only");
        if ((!prepare && !preflight) || (args[1] != "--manifest"))
        {
            Console.Error.WriteLine("Expected prepare --manifest <path> or run --manifest <path> --preflight-only.");
            return 2;
        }

        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler handler = (object? sender, ConsoleCancelEventArgs eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            ExperimentRunner runner = await ExperimentRunner.FromManifestAsync(args[2], cancellation.Token);
            if (prepare)
            {
                await runner.PrepareAsync(cancellation.Token);
            }
            else
            {
                await runner.RunPreflightAsync(cancellation.Token);
            }

            Console.WriteLine(prepare
                ? "Inputs prepared. No server or measured trial has run."
                : "Six phase-one feasibility gates passed. The 72-row / 432-trial benchmark remains unmeasured.");
            return 0;
        }
        catch (Exception exception)
        {
            // The CLI is a failure boundary; evidence writers and child cleanup never turn errors into success.
            Console.Error.WriteLine($"{exception.GetType().Name}: {exception.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
