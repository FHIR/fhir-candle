// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Performance;

/// <summary>Entry point for the sealed-artifact experiment, not a product server.</summary>
public static class Program
{
    /// <summary>Prepares, calibrates, measures or re-renders one append-only sealed experiment.</summary>
    public static async Task<int> Main(string[] args)
    {
        if ((args.Length == 0) || args.SequenceEqual(new[] { "--help" }))
        {
            Console.WriteLine("prepare --manifest <absolute-path>");
            Console.WriteLine("run --manifest <absolute-path> --preflight-only");
            Console.WriteLine("run --manifest <absolute-path>");
            Console.WriteLine("render --run-directory <absolute-path>");
            Console.WriteLine("Preflight includes all 72 semantic pairs and paired normal/off/control calibration; no accepted trials.");
            return args.Length == 0 ? 2 : 0;
        }

        bool prepare = (args.Length == 3) && (args[0] == "prepare");
        bool preflight = (args.Length == 4) && (args[0] == "run") && (args[3] == "--preflight-only");
        bool run = (args.Length == 3) && (args[0] == "run");
        bool render = (args.Length == 3) && (args[0] == "render") && (args[1] == "--run-directory");
        if (!render && ((!prepare && !preflight && !run) || (args[1] != "--manifest")))
        {
            Console.Error.WriteLine("Expected prepare/run --manifest <path> [--preflight-only on run], or render --run-directory <path>.");
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
            if (render)
            {
                CompletenessResult result = await MeasurementReport.RenderAsync(args[2], cancellation.Token);
                Console.WriteLine(result.Complete
                    ? "Complete saved-evidence report: " + ExperimentRunner.RequiredReportPath
                    : "INCOMPLETE saved-evidence report: " + ExperimentRunner.RequiredReportPath);
                foreach (string error in result.Errors.Take(12))
                {
                    Console.Error.WriteLine(error);
                }

                return result.Complete ? 0 : 1;
            }

            ExperimentRunner runner = await ExperimentRunner.FromManifestAsync(args[2], cancellation.Token);
            if (prepare)
            {
                await runner.PrepareAsync(cancellation.Token);
            }
            else if (preflight)
            {
                await runner.RunPreflightAsync(cancellation.Token);
            }
            else
            {
                await runner.RunAsync(cancellation.Token);
            }

            Console.WriteLine(prepare
                ? "Inputs prepared. No server or measured trial has run."
                : preflight
                    ? "All 72 semantic pairs and paired normal/off/control calibration passed; counts/schedule frozen. Zero accepted measured trials."
                    : "All frozen independent trials and saved-evidence completeness checks passed. Use render to write the reproducible report.");
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
