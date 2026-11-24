// <copyright file="CandleThemeState.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace fhir.candle.Ui;

/// <summary>Framework-neutral, circuit-local theme reconciliation without browser or storage access.</summary>
public sealed class CandleThemeState
{
    /// <summary>The latest observed system appearance.</summary>
    private bool _isSystemDark;

    /// <summary>Whether a system callback has superseded the initial hydration snapshot.</summary>
    private bool _hasSystemObservation;

    /// <summary>Occurs when requested mode, effective appearance, or usable colors change.</summary>
    public event EventHandler<CandleThemeChangedEventArgs>? Changed;

    /// <summary>Gets the complete immutable preference, including members not used for styling.</summary>
    public ThemePreference Preference { get; private set; } = ThemePreference.Parse(null);

    /// <summary>Gets the requested appearance independently of the system appearance.</summary>
    public ThemeMode RequestedMode => Preference.RequestedMode;

    /// <summary>Gets a value indicating whether the effective appearance is dark.</summary>
    public bool IsDarkMode => RequestedMode == ThemeMode.Dark ||
        (RequestedMode == ThemeMode.System && _isSystemDark);

    /// <summary>Gets the monotonic user-change revision for guarding hydration and ordering persistence.</summary>
    public long Revision { get; private set; }

    /// <summary>Reconciles a read-only browser snapshot without undoing any intervening user choice.</summary>
    /// <param name="json">Plaintext theme storage content, or null for missing or inaccessible storage.</param>
    /// <param name="isSystemDark">The system appearance at the time the snapshot was read.</param>
    /// <remarks>
    /// Hydration never requests persistence. After a toggle it still retains saved colors and unknown
    /// members, but keeps the user's requested mode. A system callback takes precedence over this snapshot.
    /// </remarks>
    public void Hydrate(string? json, bool isSystemDark)
    {
        ThemePreference previous = Preference;
        bool wasDark = IsDarkMode;
        ThemePreference incoming = ThemePreference.Parse(json);
        Preference = Revision == 0 ? incoming : incoming.WithMode(RequestedMode);
        if (!_hasSystemObservation)
        {
            _isSystemDark = isSystemDark;
        }

        if (previous.RequestedMode != RequestedMode ||
            wasDark != IsDarkMode ||
            previous.PrimaryColor != Preference.PrimaryColor ||
            previous.NeutralColor != Preference.NeutralColor)
        {
            NotifyChanged(persistenceRequested: false);
        }
    }

    /// <summary>Observes the latest system appearance, changing effective appearance only in System mode.</summary>
    /// <param name="isDark">Whether the system currently requests dark appearance.</param>
    public void SystemChanged(bool isDark)
    {
        bool wasDark = IsDarkMode;
        _hasSystemObservation = true;
        _isSystemDark = isDark;
        if (wasDark != IsDarkMode)
        {
            NotifyChanged(persistenceRequested: false);
        }
    }

    /// <summary>Selects the opposite effective appearance explicitly and requests persistence of that edit.</summary>
    public void Toggle()
    {
        Preference = Preference.WithMode(IsDarkMode ? ThemeMode.Light : ThemeMode.Dark);
        Revision++;
        NotifyChanged(persistenceRequested: true);
    }

    private void NotifyChanged(bool persistenceRequested) =>
        Changed?.Invoke(this, new CandleThemeChangedEventArgs(Preference, IsDarkMode, Revision, persistenceRequested));
}

/// <summary>An immutable theme-change snapshot for rendering and ordered, user-only persistence.</summary>
public sealed class CandleThemeChangedEventArgs : EventArgs
{
    internal CandleThemeChangedEventArgs(
        ThemePreference preference, bool isDarkMode, long revision, bool persistenceRequested)
    {
        Preference = preference;
        IsDarkMode = isDarkMode;
        Revision = revision;
        PersistenceRequested = persistenceRequested;
    }

    /// <summary>Gets the complete preference at the time of this change.</summary>
    public ThemePreference Preference { get; }

    /// <summary>Gets a value indicating whether this change's effective appearance is dark.</summary>
    public bool IsDarkMode { get; }

    /// <summary>Gets the user-change revision at the time of this notification.</summary>
    public long Revision { get; }

    /// <summary>Gets a value indicating whether an explicit user change requests a storage write.</summary>
    public bool PersistenceRequested { get; }
}
