using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace fhir.candle.Controllers;

/// <summary>A controller processing version requests.
/// Responds to:
///     GET:    /api/error/
///     GET:    /api/version/
/// </summary>
[Produces("application/json")]
[Route("api")]
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
    [HttpGet, Route("version")]
    public virtual IActionResult GetVersionInfo()
    {
        // create a basic tuple to return
        Dictionary<string, string> information = new()
        {
            { "Application", AppDomain.CurrentDomain.FriendlyName },
            { "Runtime", Environment.Version.ToString() },
            // get the file version of the assembly that launched us
            { "Version", FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion?.ToString() ?? string.Empty },
        };

        // add the list of configuration keys and values
        IEnumerable<IConfigurationSection> configItems = _config.GetChildren();

        foreach (IConfigurationSection configItem in configItems)
        {
            if (configItem.Key.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
                configItem.Key.Contains("key", StringComparison.OrdinalIgnoreCase))
            {
                information.Add(configItem.Key, "************");
                continue;
            }

            information.Add(configItem.Key, configItem.Value ?? string.Empty);
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

            foreach (RouteInfo route in routes)
            {
                information.Add($"{route.ControllerName}.{route.FunctionName}", route.UriTemplate);
            }

            //information.Add("Routes", JsonConvert.SerializeObject(routes, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return StatusCode((int)HttpStatusCode.OK, information);
    }
}

