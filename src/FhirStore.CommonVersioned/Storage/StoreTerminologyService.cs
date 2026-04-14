// <copyright file="StoreTerminologyService.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Terminology;
using System.Collections.Concurrent;

namespace FhirCandle.Storage;

/// <summary>A service for accessing store terminologies information.</summary>
public class StoreTerminologyService : ITerminologyService
{
    /// <summary>A value set contents.</summary>
    internal record class ValueSetContents
    {
        /// <summary>Set containing bare code values.</summary>
        public HashSet<string> Codes { get; init; } = new();

        /// <summary>Set containing joined system|code values.</summary>
        public HashSet<string> SystemAndCodes { get; init; } = new();
    }

    /// <summary>(Immutable) The value set contents.</summary>
    internal readonly ConcurrentDictionary<string, ValueSetContents> _valueSetContents = new();

    /// <summary>Vs contains.</summary>
    /// <param name="vsUrl"> URL of the vs.</param>
    /// <param name="system">The system.</param>
    /// <param name="code">  The code.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool VsContains(string? vsUrl, string? system, string? code)
    {
        if ((vsUrl is null) || (code is null))
        {
            return false;
        }

        if (!_valueSetContents.ContainsKey(vsUrl))
        {
            return false;
        }

        if (string.IsNullOrEmpty(system))
        {
            return _valueSetContents[vsUrl].Codes.Contains(code);
        }

        return _valueSetContents[vsUrl].SystemAndCodes.Contains($"{system}|{code}");
    }

    /// <summary>Stores process value set.</summary>
    /// <param name="vs">    The vs.</param>
    /// <param name="remove">(Optional) True to remove.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    internal bool StoreProcessValueSet(ValueSet vs, bool remove = false)
    {
        if (remove)
        {
            if (vs.Url is null)
            {
                return true;
            }

            if (_valueSetContents.ContainsKey(vs.Url))
            {
                _ = _valueSetContents.TryRemove(vs.Url, out _);
            }

            return true;
        }

        if (vs.Url is null)
        {
            return false;
        }

        HashSet<string> codes = new();
        HashSet<string> systemAndCodes = new();

        if (vs.Expansion?.Contains?.Any() ?? false)
        {
            AddContains(vs.Expansion.Contains, codes, systemAndCodes);
        }
        else if (vs.Compose?.Include?.Any() ?? false)
        {
            foreach (ValueSet.ConceptSetComponent csc in vs.Compose.Include)
            {
                string system = csc.System ?? string.Empty;

                if (csc.Concept?.Any() ?? false)
                {
                    foreach (ValueSet.ConceptReferenceComponent crc in csc.Concept)
                    {
                        if (crc.Code is null)
                        {
                            continue;
                        }

                        if (!codes.Contains(crc.Code))
                        {
                            codes.Add(crc.Code);
                        }

                        string sc = $"{system}|{crc.Code}";

                        if (!systemAndCodes.Contains(sc))
                        {
                            systemAndCodes.Add(sc);
                        }
                    }
                }
            }
        }

        _ = _valueSetContents.TryAdd(vs.Url, new() { Codes = codes, SystemAndCodes = systemAndCodes });

        return true;

        void AddContains(IEnumerable<ValueSet.ContainsComponent> contains, HashSet<string> codes, HashSet<string> systemAndCodes)
        {
            foreach (ValueSet.ContainsComponent c in contains)
            {
                if (c.Code is null)
                {
                    continue;
                }

                if (!codes.Contains(c.Code))
                {
                    codes.Add(c.Code);
                }

                string sc = $"{c.System}|{c.Code}";
                if (!systemAndCodes.Contains(sc))
                {
                    systemAndCodes.Add(sc);
                }

                if (c.Contains?.Any() ?? false)
                {
                    AddContains(c.Contains, codes, systemAndCodes);
                }
            }
        }
    }

    /// <summary>Value set validate code.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="id">        The identifier.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields the Parameters.</returns>
    Task<Parameters> ICodeValidationTerminologyService.ValueSetValidateCode(Parameters parameters, string? id, bool useGet)
    {
        Parameters retVal = new();

        string vsUrl = parameters.GetSingleValue<FhirUri>("url")?.Value?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(vsUrl))
        {
            ValueSet? vs = parameters.Parameter
                .Where(p => p.Name == "valueSet")
                .Select(p => p.Resource as ValueSet)
                .FirstOrDefault();

            if (vs is null)
            {
                retVal.Parameter.Add(new() { Name = "result", Value = new FhirBoolean(false) });
                retVal.Parameter.Add(new() { Name = "message", Value = new FhirString("No value set specified") });
                return System.Threading.Tasks.Task.FromResult(retVal);
            }

            if (vs.Url is null)
            {
                retVal.Parameter.Add(new() { Name = "result", Value = new FhirBoolean(false) });
                retVal.Parameter.Add(new() { Name = "message", Value = new FhirString("No value set specified") });
                return System.Threading.Tasks.Task.FromResult(retVal);
            }

            vsUrl = vs.Url;
            StoreProcessValueSet(vs);
        }

        string system = parameters.GetSingleValue<FhirUri>("system")?.Value?.ToString() ?? string.Empty;
        string code = parameters.GetSingleValue<Code>("code")?.Value?.ToString() ?? string.Empty;

        Coding? coding = parameters.GetSingleValue<Coding>("coding");
        CodeableConcept? concept = parameters.GetSingleValue<CodeableConcept>("codeableConcept");

        if (string.IsNullOrEmpty(code))
        {
            if (coding is not null)
            {
                system = coding.System ?? string.Empty;
                code = coding.Code ?? string.Empty;
            }
            else if (concept is not null)
            {
                system = concept.Coding.FirstOrDefault()?.System ?? string.Empty;
                code = concept.Coding.FirstOrDefault()?.Code ?? string.Empty;
            }
        }

        if (string.IsNullOrEmpty(system) && string.IsNullOrEmpty(code))
        {
            retVal.Parameter.Add(new() { Name = "result", Value = new FhirBoolean(false) });
            retVal.Parameter.Add(new() { Name = "message", Value = new FhirString("Could not determine system and code for testing!") });
            return System.Threading.Tasks.Task.FromResult(retVal);
        }

        if (VsContains(vsUrl, system, code))
        {
            retVal.Parameter.Add(new() { Name = "result", Value = new FhirBoolean(true) });
            return System.Threading.Tasks.Task.FromResult(retVal);
        }

        retVal.Parameter.Add(new() { Name = "result", Value = new FhirBoolean(false) });
        return System.Threading.Tasks.Task.FromResult(retVal);
    }

    /// <summary>Subsumes.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="id">        The identifier.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields the Parameters.</returns>
    Task<Parameters> ICodeValidationTerminologyService.Subsumes(Parameters parameters, string? id, bool useGet)
    {
        throw new NotImplementedException();
    }

    /// <summary>Code system validate code.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="id">        The identifier.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields the Parameters.</returns>
    Task<Parameters> ICodeSystemTerminologyService.CodeSystemValidateCode(Parameters parameters, string? id, bool useGet)
    {
        throw new NotImplementedException();
    }

    /// <summary>Looks up a given key to find its associated value.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields the Parameters.</returns>
    Task<Parameters> ICodeSystemTerminologyService.Lookup(Parameters parameters, bool useGet)
    {
        throw new NotImplementedException();
    }

    /// <summary>Translates.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="id">        The identifier.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields the Parameters.</returns>
    Task<Parameters> IMappingTerminologyService.Translate(Parameters parameters, string? id, bool useGet)
    {
        throw new NotImplementedException();
    }

    /// <summary>Closures.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields a Resource.</returns>
    Task<Resource> ITerminologyServiceWithClosure.Closure(Parameters parameters, bool useGet)
    {
        throw new NotImplementedException();
    }

    /// <summary>Expands.</summary>
    /// <param name="parameters">Options for controlling the operation.</param>
    /// <param name="id">        The identifier.</param>
    /// <param name="useGet">    True to use get.</param>
    /// <returns>An asynchronous result that yields a Resource.</returns>
    Task<Resource> IExpandingTerminologyService.Expand(Parameters parameters, string? id, bool useGet)
    {
        throw new NotImplementedException();
    }
}
