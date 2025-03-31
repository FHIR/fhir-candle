using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FhirCandle.Models;

/// <summary>Information about the package page.</summary>
/// <param name="ContentFor">        The content for.</param>
/// <param name="PageName">          Name of the page.</param>
/// <param name="Description">       The description.</param>
/// <param name="RoutePath">         Full pathname of the route file.</param>
/// <param name="FhirVersionLiteral">The FHIR version literal.</param>
/// <param name="FhirVersionNumeric">The FHIR version numeric.</param>
/// <param name="OnlyShowOnEndpoint">The only show on endpoint.</param>
public record struct PackagePageInfo(
    string ContentFor,
    string PageName,
    string Description,
    string RoutePath,
    string FhirVersionLiteral,
    string FhirVersionNumeric,
    string OnlyShowOnEndpoint);

/// <summary>Interface for package/ri pages.</summary>
public interface IPackagePage
{
    /// <summary>Gets the package or ri name this page is for.</summary>
    virtual static string ContentFor => string.Empty;

    /// <summary>Gets the name of the page.</summary>
    virtual static string PageName => string.Empty;

    /// <summary>Gets the description.</summary>
    virtual static string Description => string.Empty;

    /// <summary>Gets the full pathname of the route file.</summary>
    virtual static string RoutePath => string.Empty;

    /// <summary>Gets the FHIR version literal.</summary>
    virtual static string FhirVersionLiteral => string.Empty;

    /// <summary>Gets the FHIR version numeric.</summary>
    virtual static string FhirVersionNumeric => string.Empty;

    /// <summary>Gets the only show on endpoint.</summary>
    virtual static string OnlyShowOnEndpoint => string.Empty;
}
