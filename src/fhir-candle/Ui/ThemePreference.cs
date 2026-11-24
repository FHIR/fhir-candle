// <copyright file="ThemePreference.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json;

namespace fhir.candle.Ui;

/// <summary>The requested application appearance, independent of the effective system appearance.</summary>
public enum ThemeMode
{
    /// <summary>Follow the system appearance.</summary>
    System,

    /// <summary>Use light appearance.</summary>
    Light,

    /// <summary>Use dark appearance.</summary>
    Dark,
}

/// <summary>Retains the plaintext legacy theme object while exposing safe application preferences.</summary>
public sealed class ThemePreference
{
    /// <summary>The existing plaintext local-storage key.</summary>
    public const string StorageKey = "theme";

    /// <summary>The application's existing Teams accent intent.</summary>
    public const string DefaultPrimaryColor = "#6264a7";

    /// <summary>The complete object, or an undefined element for missing or unusable input.</summary>
    private readonly JsonElement _value;

    private ThemePreference(JsonElement value)
    {
        _value = value;
        RequestedMode = ReadString("mode") switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
        PrimaryColor = ReadPrimaryColor(ReadString("primaryColor"));
        NeutralColor = ReadHexColor(ReadString("neutralColor"));
    }

    /// <summary>Gets the requested mode; missing and unrecognized modes follow the system.</summary>
    public ThemeMode RequestedMode { get; }

    /// <summary>Gets the validated six-digit accent color, falling back to the application accent.</summary>
    public string PrimaryColor { get; }

    /// <summary>Gets the validated six-digit neutral color, or null when no usable color was saved.</summary>
    public string? NeutralColor { get; }

    /// <summary>Reads a legacy object without writing storage or changing its members.</summary>
    /// <param name="json">Plaintext storage content, or null when storage is missing or inaccessible.</param>
    /// <returns>A usable preference even when the input is malformed or is not an object.</returns>
    public static ThemePreference Parse(string? json)
    {
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return new ThemePreference(document.RootElement.Clone());
                }
            }
            catch (JsonException)
            {
                // Invalid storage is a read-only System fallback until the user chooses a mode.
            }
        }

        return new ThemePreference(default);
    }

    /// <summary>Returns the retained object, or an empty object for unusable input; this performs no I/O.</summary>
    /// <returns>The legacy lowercase-field JSON object, not a serialized CLR property schema.</returns>
    public string ToJson() => _value.ValueKind == JsonValueKind.Object ? _value.GetRawText() : "{}";

    /// <summary>Creates a mode-only edit, preserving all other members and their original JSON values.</summary>
    /// <param name="mode">The intentionally selected requested mode.</param>
    /// <returns>A new preference; the original preference remains unchanged.</returns>
    public ThemePreference WithMode(ThemeMode mode)
    {
        string value = mode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark => "dark",
            ThemeMode.System => "system",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            if (_value.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in _value.EnumerateObject())
                {
                    if (!property.NameEquals("mode"))
                    {
                        property.WriteTo(writer);
                    }
                }
            }

            writer.WriteString("mode", value);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return new ThemePreference(document.RootElement.Clone());
    }

    private string? ReadString(string name) =>
        _value.ValueKind == JsonValueKind.Object &&
        _value.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadHexColor(string? value)
    {
        // The v4 design theme accepts only #RGB/#RRGGBB, not arbitrary CSS color expressions.
        if (value is null || (value.Length != 4 && value.Length != 7) || value[0] != '#')
        {
            return null;
        }

        for (int index = 1; index < value.Length; index++)
        {
            if (!char.IsAsciiHexDigit(value[index]))
            {
                return null;
            }
        }

        string color = value.ToLowerInvariant();
        return color.Length == 7 ? color : $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";
    }

    private static string ReadPrimaryColor(string? value) =>
        ReadHexColor(value) ?? (value?.ToLowerInvariant() switch
        {
            // The finite Office palette is accepted by the cached 4.14.5 ColorsUtils.
            "access" => "#a4373a",
            "booking" => "#00a99d",
            "exchange" => "#0078d4",
            "excel" => "#217346",
            "groupme" => "#00bcf2",
            "office" => "#d83b01",
            "onedrive" => "#0078d4",
            "onenote" => "#7719aa",
            "outlook" => "#0f6cbd",
            "planner" => "#31752f",
            "powerapps" => "#742774",
            "powerbi" => "#f2c811",
            "powerpoint" => "#b7472a",
            "project" => "#31752f",
            "publisher" => "#077568",
            "sharepoint" => "#0078d4",
            "skype" => "#0078d4",
            "stream" => "#bc1948",
            "sway" => "#008272",
            "teams" => DefaultPrimaryColor,
            "visio" => "#3955a3",
            "windows" => "#0078d4",
            "word" => "#2b579a",
            "yamme" => "#106ebe",
            _ => DefaultPrimaryColor,
        });
}
