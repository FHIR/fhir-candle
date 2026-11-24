// <copyright file="ZulipSender.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace fhir.candle.Zulip;

/// <summary>Sends each notification with credentials bound to one Zulip message route.</summary>
internal sealed class ZulipSender : IDisposable
{
    internal sealed class Destination(Uri site, string email, string key)
    {
        internal Uri Messages { get; } = new(site, "api/v1/messages");
        internal AuthenticationHeaderValue Authorization { get; } =
            new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(email + ":" + key)));
    }

    private readonly HttpClient _client;
    private readonly Uri? _operatorSite;
    private readonly Destination? _operator;

    internal ZulipSender(string? site, string? email, string? key, HttpMessageHandler? handler)
    {
        _client = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        _operatorSite = NormalizeSite(site);
        if (_operatorSite is not null && ValidCredentials(email, key))
        {
            _operator = new(_operatorSite, email!, key!);
        }
    }

    private static bool ValidCredentials(string? email, string? key) =>
        !string.IsNullOrWhiteSpace(email) && !email.Contains(':') && !string.IsNullOrWhiteSpace(key);

    private static Uri? NormalizeSite(string? site)
    {
        const string Scheme = "https://";
        if (site is null || !site.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ||
            site.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ||
            site.IndexOfAny(['\\', '%', '?', '#', '@']) >= 0 ||
            !Uri.TryCreate(site, UriKind.Absolute, out Uri? uri) || !uri.IsWellFormedOriginalString() ||
            uri.Host.Length == 0)
        {
            return null;
        }

        // Check the original segments before System.Uri can collapse dot segments.
        int slash = site.IndexOf('/', Scheme.Length);
        string path = slash < 0 ? "/" : site[slash..];
        if (path.Contains("//", StringComparison.Ordinal) ||
            path.Split('/').Any(segment => segment is "." or ".." ||
                !segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~')))
        {
            return null;
        }
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + path.TrimEnd('/') + "/");
    }

    internal Destination? Bind(Dictionary<string, List<string>> parameters)
    {
        bool hasSite = parameters.TryGetValue("site", out List<string>? sites);
        bool hasEmail = parameters.TryGetValue("email", out List<string>? emails);
        bool hasKey = parameters.TryGetValue("key", out List<string>? keys);
        Uri? site = sites is [string value] ? NormalizeSite(value) : null;
        if (!hasEmail && !hasKey)
        {
            return !hasSite || (site is not null && site == _operatorSite) ? _operator : null;
        }
        return site is not null && emails is [string email] && keys is [string key] && ValidCredentials(email, key)
            ? new Destination(site, email, key) : null;
    }

    internal async Task<(bool success, string? details, ulong messageId)> SendAsync(
        Destination destination, string message, int id, bool stream, CancellationToken cancellationToken)
    {
        Dictionary<string, string> fields = new()
        {
            ["type"] = stream ? "channel" : "direct",
            ["to"] = "[" + id.ToString(CultureInfo.InvariantCulture) + "]",
            ["content"] = message,
        };
        if (stream)
        {
            fields["topic"] = "Subscription Notification";
        }
        using HttpRequestMessage request = new(HttpMethod.Post, destination.Messages)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.Authorization = destination.Authorization;
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return (false, $"Zulip returned HTTP {(int)response.StatusCode}.", 0);
        }
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        JsonElement root = json.RootElement;
        if (root.TryGetProperty("result", out JsonElement result) && result.ValueEquals("success"))
        {
            return (true, null, root.TryGetProperty("id", out JsonElement number) && number.TryGetUInt64(out ulong sent) ? sent : 0);
        }
        return (false, "Zulip did not accept the message.", 0);
    }

    public void Dispose() => _client.Dispose();
}
