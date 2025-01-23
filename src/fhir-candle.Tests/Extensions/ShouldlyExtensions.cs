﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Shouldly;

namespace fhir.candle.Tests.Extensions;

internal static class ShouldlyExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldNotBeNullOrEmpty<TKey, TValue>([NotNull] this Dictionary<TKey, TValue>? actual, string? customMessage = null)
        where TKey : notnull
    {
        if ((actual == null) || (actual.Count == 0))
            throw new ShouldAssertException(new ActualShouldlyMessage(actual, customMessage).ToString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldNotBeNullOrEmpty<T>([NotNull] this IEnumerable<T>? actual, string? customMessage = null)
    {
        if ((actual == null) || (!actual.Any()))
            throw new ShouldAssertException(new ActualShouldlyMessage(actual, customMessage).ToString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldHaveCount<T>([NotNull] this IEnumerable<T>? actual, int count, string? customMessage = null)
    {
        if (actual == null || actual.Count() != count)
            throw new ShouldAssertException(new ExpectedShouldlyMessage(actual, customMessage).ToString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldHaveCount<TKey, TValue>([NotNull] this Dictionary<TKey, TValue>? actual, int count, string? customMessage = null)
        where TKey : notnull
    {
        if (actual == null || actual.Count() != count)
            throw new ShouldAssertException(new ExpectedShouldlyMessage(actual, customMessage).ToString());
    }


}
