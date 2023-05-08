﻿using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json;

namespace FhirServerHarness.Controllers;

/// <summary>A controller processing version requests.
/// Responds to:
///     GET:    /api/
///     GET:    /api/version/
/// </summary>
[Produces("application/json")]
[Route("api/version")]
public class VersionController : ControllerBase
{
    private const string _configPrefix = "Client_";

    /// <summary>Information about the route.</summary>
    private class RouteInfo
    {
        public string FunctionName { get; set; } = string.Empty;
        public string ControllerName { get; set; } = string.Empty;
        public string UriTemplate { get; set; } = string.Empty;
    }

    /// <summary>   The configuration. </summary>
    private readonly IConfiguration _config;

    /// <summary>The provider.</summary>
    private readonly IActionDescriptorCollectionProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionController"/> class.
    /// </summary>
    /// <param name="iConfiguration">Reference to the injected configuration object.</param>
    /// <param name="provider">      The provider.</param>
    public VersionController(
        IConfiguration iConfiguration,
        IActionDescriptorCollectionProvider provider)
    {
        _config = iConfiguration;
        _provider = provider;
    }

    /// <summary>(An Action that handles HTTP GET requests) gets version information.</summary>
    /// <returns>The version information.</returns>
    [HttpGet, Route("")]
    public virtual IActionResult GetVersionInfo()
    {
        // create a basic tuple to return
        Dictionary<string, string> information = new Dictionary<string, string>();

        information.Add("Application", AppDomain.CurrentDomain.FriendlyName);
        information.Add("Runtime", Environment.Version.ToString());

        // get the file version of the assembly that launched us
        information.Add("Version", FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion?.ToString() ?? string.Empty);

        // add the list of configuration keys and values
        IEnumerable<IConfigurationSection> configItems = _config.GetChildren();

        foreach (IConfigurationSection configItem in configItems)
        {
            information.Add(configItem.Key, configItem.Value ?? string.Empty);
            //if (configItem.Key.StartsWith(_configPrefix, StringComparison.Ordinal))
            //{
            //    information.Add(configItem.Key, configItem.Value ?? string.Empty);
            //}
        }

        // try to get a list of routes
        try
        {
            List<RouteInfo> routes = _provider.ActionDescriptors.Items.Select(x => new RouteInfo()
            {
                FunctionName = x.RouteValues["Action"] ?? string.Empty,
                ControllerName = x.RouteValues["Controller"] ?? string.Empty,
                UriTemplate = x.AttributeRouteInfo?.Template ?? string.Empty,
            })
                .ToList();

            information.Add("Routes", JsonConvert.SerializeObject(routes, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return StatusCode((int)HttpStatusCode.OK, information);
    }
}

