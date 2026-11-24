// <copyright file="BlazorInteropTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Reflection;
using fhir.candle.Layout;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace fhir.candle.Tests;

/// <summary>Browser actions distinguish disconnected circuits from unexpected JavaScript errors.</summary>
public class BlazorInteropTests
{
    /// <summary>A circuit can disconnect while a browser action is awaiting completion.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserActionHandlesDisconnectedCircuit(bool clipboard)
    {
        FailingJsRuntime runtime = new(new JSDisconnectedException("Circuit disconnected."));
        MainLayout layout = CreateLayout(runtime);

        await InvokeBrowserAction(layout, clipboard);

        Assert.Equal(clipboard ? "navigator.clipboard.writeText" : "console.log", runtime.Identifier);
    }

    /// <summary>Unexpected JavaScript failures still reach the caller rather than being hidden.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserActionPreservesUnexpectedJavaScriptError(bool clipboard)
    {
        JSException expected = new("Browser action failed.");
        FailingJsRuntime runtime = new(expected);
        MainLayout layout = CreateLayout(runtime);

        JSException actual = await Assert.ThrowsAsync<JSException>(() => InvokeBrowserAction(layout, clipboard));

        Assert.Same(expected, actual);
    }

    private static MainLayout CreateLayout(IJSRuntime runtime)
    {
        MainLayout layout = new();
        PropertyInfo? js = typeof(MainLayout).GetProperty("JS", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo? logger = typeof(MainLayout).GetProperty("Logger", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(js);
        Assert.NotNull(logger);
        js.SetValue(layout, runtime);
        logger.SetValue(layout, NullLogger<MainLayout>.Instance);
        return layout;
    }

    private static Task InvokeBrowserAction(MainLayout layout, bool clipboard) =>
        clipboard ? layout.JsClipboardCopy("content") : layout.JsLogAsync("message");

    private sealed class FailingJsRuntime(Exception exception) : IJSRuntime
    {
        /// <summary>Gets the last requested JavaScript identifier.</summary>
        public string? Identifier { get; private set; }

        /// <inheritdoc/>
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Identifier = identifier;
            return ValueTask.FromException<TValue>(exception);
        }

        /// <inheritdoc/>
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Identifier = identifier;
            return ValueTask.FromException<TValue>(exception);
        }
    }
}
