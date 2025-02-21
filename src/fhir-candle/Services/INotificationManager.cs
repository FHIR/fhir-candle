// <copyright file="INotificationManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Microsoft.Extensions.Hosting;

namespace fhir.candle.Services;

/// <summary>Interface for notification manager.</summary>
public interface INotificationManager : IHostedService, IDisposable
{
}
