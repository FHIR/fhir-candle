// <copyright file="CandleThemeTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json;
using fhir.candle.Ui;

namespace fhir.candle.Tests;

/// <summary>Pure legacy preference and state contracts; these do not execute browser storage or media queries.</summary>
public class CandleThemeTests
{
    /// <summary>Legacy modes, colors, unknown data, and unusable storage with independent expectations.</summary>
    public static IEnumerable<object?[]> PreferenceCases =>
    [
        ["light and nested data", """{"mode":"light","primaryColor":"#123ABC","neutralColor":"#DEF","extra":{"values":[1,true,null,{"mode":"dark"}]}}""",
            ThemeMode.Light, "#123abc", "#ddeeff", true],
        ["dark and Office accent", """{"mode":"dark","primaryColor":"eXcEl","neutralColor":"#123456","future":[{"x":2}]}""",
            ThemeMode.Dark, "#217346", "#123456", true],
        ["absent mode", """{"primaryColor":"#aBc","other":null}""", ThemeMode.System, "#aabbcc", null, true],
        ["system mode", """{"mode":"system","primaryColor":"Teams","neutralColor":"#000"}""",
            ThemeMode.System, "#6264a7", "#000000", true],
        ["unknown mode", """{"mode":"future-mode","primaryColor":"Outlook","neutralColor":"#fFfFfF","unrecognized":42}""",
            ThemeMode.System, "#0f6cbd", "#ffffff", true],
        ["null mode", """{"mode":null,"primaryColor":null,"neutralColor":null}""",
            ThemeMode.System, "#6264a7", null, true],
        ["non-string mode", """{"mode":{"requested":"dark"},"extra":"retained"}""",
            ThemeMode.System, "#6264a7", null, true],
        ["mode is case-sensitive", """{"mode":"Dark"}""", ThemeMode.System, "#6264a7", null, true],
        ["fields are case-sensitive", """{"Mode":"dark","PrimaryColor":"#123456","NeutralColor":"#123456"}""",
            ThemeMode.System, "#6264a7", null, true],
        ["empty object", "{}", ThemeMode.System, "#6264a7", null, true],
        ["unsafe CSS", """{"mode":"dark","primaryColor":"#123456; background:url(https://example.test/)","neutralColor":"var(--untrusted)"}""",
            ThemeMode.Dark, "#6264a7", null, true],
        ["invalid hex", """{"primaryColor":"#12GG00","neutralColor":"#12345"}""",
            ThemeMode.System, "#6264a7", null, true],
        ["unsupported color forms", """{"primaryColor":"rgb(1,2,3)","neutralColor":"#12345678"}""",
            ThemeMode.System, "#6264a7", null, true],
        ["non-string colors", """{"primaryColor":{"css":"#123456"},"neutralColor":false}""",
            ThemeMode.System, "#6264a7", null, true],
        ["unknown accent and Office-only neutral", """{"primaryColor":"not-an-office-color","neutralColor":"Teams"}""",
            ThemeMode.System, "#6264a7", null, true],
        ["missing or inaccessible storage", null, ThemeMode.System, "#6264a7", null, false],
        ["empty storage", "", ThemeMode.System, "#6264a7", null, false],
        ["whitespace", " \r\n ", ThemeMode.System, "#6264a7", null, false],
        ["malformed JSON", """{"mode":"dark" """, ThemeMode.System, "#6264a7", null, false],
        ["null JSON", "null", ThemeMode.System, "#6264a7", null, false],
        ["array JSON", """[{"mode":"dark"}]""", ThemeMode.System, "#6264a7", null, false],
        ["string JSON", "\"dark\"", ThemeMode.System, "#6264a7", null, false],
        ["number JSON", "42", ThemeMode.System, "#6264a7", null, false],
        ["boolean JSON", "true", ThemeMode.System, "#6264a7", null, false],
    ];

    /// <summary>Mode-only edits keep the legacy object and never turn CLR property names into a new schema.</summary>
    [Theory]
    [MemberData(nameof(PreferenceCases))]
    public void PreferenceRoundTripPreservesLegacyObject(
        string label, string? json, ThemeMode expectedMode, string expectedPrimary, string? expectedNeutral, bool isObject)
    {
        Assert.NotEmpty(label);
        Assert.Equal("theme", ThemePreference.StorageKey);
        ThemePreference preference = ThemePreference.Parse(json);
        Assert.Equal(expectedMode, preference.RequestedMode);
        Assert.Equal(expectedPrimary, preference.PrimaryColor);
        Assert.Equal(expectedNeutral, preference.NeutralColor);
        string original = isObject ? json! : "{}";
        Assert.Equal(original, preference.ToJson());
        using JsonDocument retained = JsonDocument.Parse(original);

        foreach (ThemeMode mode in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
        {
            ThemePreference edited = preference.WithMode(mode);
            using JsonDocument actual = JsonDocument.Parse(edited.ToJson());
            string expectedValue = mode switch
            {
                ThemeMode.Light => "light",
                ThemeMode.Dark => "dark",
                _ => "system",
            };
            Assert.Equal(expectedValue, actual.RootElement.GetProperty("mode").GetString());
            Assert.Equal(mode, edited.RequestedMode);
            Assert.Equal(expectedPrimary, edited.PrimaryColor);
            Assert.Equal(expectedNeutral, edited.NeutralColor);
            int expectedCount = retained.RootElement.EnumerateObject().Count() +
                (retained.RootElement.TryGetProperty("mode", out _) ? 0 : 1);
            Assert.Equal(expectedCount, actual.RootElement.EnumerateObject().Count());
            foreach (JsonProperty property in retained.RootElement.EnumerateObject())
            {
                if (!property.NameEquals("mode"))
                {
                    Assert.True(JsonElement.DeepEquals(property.Value, actual.RootElement.GetProperty(property.Name)));
                }
            }

            Assert.Equal(original, preference.ToJson());
        }
    }

    /// <summary>Hydration and system updates notify only meaningful changes and never request a write.</summary>
    [Theory]
    [InlineData("""{"mode":"light"}""", ThemeMode.Light, false)]
    [InlineData("""{"mode":"light"}""", ThemeMode.Light, true)]
    [InlineData("""{"mode":"dark"}""", ThemeMode.Dark, false)]
    [InlineData("""{"mode":"dark"}""", ThemeMode.Dark, true)]
    [InlineData("""{"mode":"system"}""", ThemeMode.System, false)]
    [InlineData("""{"mode":"system"}""", ThemeMode.System, true)]
    [InlineData("""{"unknown":{"items":[1,null,true]}}""", ThemeMode.System, true)]
    [InlineData("""{"mode":"unrecognized"}""", ThemeMode.System, false)]
    [InlineData("""{"mode":null}""", ThemeMode.System, true)]
    [InlineData(null, ThemeMode.System, true)]
    [InlineData("{", ThemeMode.System, false)]
    [InlineData("[]", ThemeMode.System, true)]
    public void StateReconciliationPreservesUserIntent(string? json, ThemeMode requestedMode, bool systemDark)
    {
        CandleThemeState state = new();
        List<CandleThemeChangedEventArgs> changes = [];
        state.Changed += (sender, args) =>
        {
            Assert.Same(state, sender);
            changes.Add(args);
        };
        Assert.Equal(ThemeMode.System, state.RequestedMode);
        Assert.False(state.IsDarkMode);
        Assert.Equal(0L, state.Revision);

        state.Hydrate(json, systemDark);
        bool expectedDark = requestedMode == ThemeMode.Dark || (requestedMode == ThemeMode.System && systemDark);
        int expectedChanges = requestedMode != ThemeMode.System || expectedDark ? 1 : 0;
        Assert.Equal(requestedMode, state.RequestedMode);
        Assert.Equal(expectedDark, state.IsDarkMode);
        Assert.Equal(expectedChanges, changes.Count);
        Assert.Equal(0L, state.Revision);
        Assert.All(changes, change => Assert.False(change.PersistenceRequested));

        state.Hydrate(json, systemDark);
        state.SystemChanged(systemDark);
        Assert.Equal(expectedChanges, changes.Count);
        state.SystemChanged(!systemDark);
        if (requestedMode == ThemeMode.System)
        {
            expectedDark = !systemDark;
            expectedChanges++;
        }
        Assert.Equal(expectedDark, state.IsDarkMode);
        Assert.Equal(expectedChanges, changes.Count);
        Assert.All(changes, change => Assert.False(change.PersistenceRequested));
        state.SystemChanged(!systemDark);
        Assert.Equal(expectedChanges, changes.Count);

        state.Toggle();
        ThemeMode selectedMode = expectedDark ? ThemeMode.Light : ThemeMode.Dark;
        Assert.Equal(selectedMode, state.RequestedMode);
        Assert.Equal(!expectedDark, state.IsDarkMode);
        Assert.Equal(++expectedChanges, changes.Count);
        Assert.Equal(1L, state.Revision);
        CandleThemeChangedEventArgs firstToggle = changes[^1];
        Assert.True(firstToggle.PersistenceRequested);
        Assert.Equal(1L, firstToggle.Revision);
        Assert.Equal(selectedMode, firstToggle.Preference.RequestedMode);
        Assert.Equal(!expectedDark, firstToggle.IsDarkMode);
        state.SystemChanged(systemDark);
        Assert.Equal(expectedChanges, changes.Count);
        Assert.Equal(!expectedDark, state.IsDarkMode);

        // A late read retains the saved object, but cannot undo the user's newer explicit mode.
        string lateJson = """{"mode":"system","primaryColor":"#ABC","neutralColor":"#123456","future":{"items":[false,null,{"x":3}]}}""";
        state.Hydrate(lateJson, !systemDark);
        Assert.Equal(selectedMode, state.RequestedMode);
        Assert.Equal(!expectedDark, state.IsDarkMode);
        Assert.Equal("#aabbcc", state.Preference.PrimaryColor);
        Assert.Equal("#123456", state.Preference.NeutralColor);
        Assert.Equal(++expectedChanges, changes.Count);
        Assert.False(changes[^1].PersistenceRequested);
        Assert.Equal(1L, state.Revision);
        using (JsonDocument retained = JsonDocument.Parse(state.Preference.ToJson()))
        {
            Assert.Equal(3, retained.RootElement.GetProperty("future").GetProperty("items")[2].GetProperty("x").GetInt32());
        }

        state.Hydrate(lateJson, !systemDark);
        Assert.Equal(expectedChanges, changes.Count);
        state.Hydrate(lateJson.Replace("#ABC", "#aabbcc", StringComparison.Ordinal), !systemDark);
        Assert.Equal(expectedChanges, changes.Count);
        state.Toggle();
        Assert.Equal(expectedDark, state.IsDarkMode);
        Assert.Equal(expectedDark ? ThemeMode.Dark : ThemeMode.Light, state.RequestedMode);
        Assert.Equal(++expectedChanges, changes.Count);
        Assert.True(changes[^1].PersistenceRequested);
        Assert.Equal(2L, state.Revision);
        Assert.Equal(2L, changes[^1].Revision);
        Assert.Equal(2, changes.Count(change => change.PersistenceRequested));
        Assert.Equal("#aabbcc", state.Preference.PrimaryColor);
        Assert.Equal("#123456", state.Preference.NeutralColor);
        Assert.Equal(selectedMode, firstToggle.Preference.RequestedMode);
        Assert.Equal("#6264a7", firstToggle.Preference.PrimaryColor);

        // The same guard works when the first hydration has not completed at all.
        CandleThemeState earlyToggle = new();
        List<CandleThemeChangedEventArgs> earlyChanges = [];
        earlyToggle.Changed += (_, args) => earlyChanges.Add(args);
        earlyToggle.SystemChanged(systemDark);
        earlyToggle.Toggle();
        ThemeMode earlyMode = earlyToggle.RequestedMode;
        int beforeHydration = earlyChanges.Count;
        earlyToggle.Hydrate(json, !systemDark);
        Assert.Equal(earlyMode, earlyToggle.RequestedMode);
        Assert.Equal(!systemDark, earlyToggle.IsDarkMode);
        Assert.Equal(beforeHydration, earlyChanges.Count);
        Assert.Single(earlyChanges, change => change.PersistenceRequested);
        Assert.Equal(1L, earlyToggle.Revision);

        // A newer system callback, even a no-op, wins over a stale hydration snapshot.
        CandleThemeState observedSystem = new();
        observedSystem.SystemChanged(systemDark);
        List<CandleThemeChangedEventArgs> systemChanges = [];
        observedSystem.Changed += (_, args) => systemChanges.Add(args);
        observedSystem.Hydrate("""{"mode":"system","extra":1}""", !systemDark);
        Assert.Equal(systemDark, observedSystem.IsDarkMode);
        Assert.Empty(systemChanges);
        observedSystem.Hydrate("""{"extra":2}""", !systemDark);
        Assert.Empty(systemChanges);
        Assert.Equal("""{"extra":2}""", observedSystem.Preference.ToJson());
        Assert.Equal(0L, observedSystem.Revision);
    }
}
