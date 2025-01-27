// <copyright file="AuthTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using fhir.candle.Services;
using fhir.candle.Tests.Extensions;
using FhirCandle.Models;
using FhirCandle.Smart;
using FhirCandle.Utils;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit.Abstractions;

namespace fhir.candle.Tests;


public class AuthTests : IClassFixture<AuthTestFixture>
{
    /// <summary>(Immutable) The fixture.</summary>
    private readonly AuthTestFixture _fixture;

    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    public AuthTests(
        AuthTestFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Tests smart configuration.</summary>
    [Fact]
    public void TestSmartConfig()
    {
        _fixture.ShouldNotBeNull();
        _fixture.AuthR4.ShouldNotBeNull();
        _fixture.AuthR4.SmartConfigurationByTenant.ShouldNotBeNullOrEmpty();

        FhirStore.Smart.SmartWellKnown smartWellKnown = _fixture.AuthR4.SmartConfigurationByTenant[_fixture.ConfigR4.ControllerName];

        smartWellKnown.ShouldNotBeNull();
        smartWellKnown.GrantTypes.ShouldNotBeNullOrEmpty();
        smartWellKnown.GrantTypes.ShouldContain("authorization_code");
        smartWellKnown.AuthorizationEndpoint.ShouldNotBeNullOrEmpty();
        smartWellKnown.TokenEndpoint.ShouldNotBeNullOrEmpty();
        smartWellKnown.TokenEndpointAuthMethods.ShouldNotBeNullOrEmpty();
        smartWellKnown.SupportedScopes.ShouldNotBeNullOrEmpty();
        smartWellKnown.SupportedScopes.ShouldContain("launch");
        smartWellKnown.SupportedScopes.ShouldContain("launch/patient");
        smartWellKnown.SupportedResponseTypes.ShouldNotBeNullOrEmpty();
        smartWellKnown.SupportedResponseTypes.ShouldContain("code");
        smartWellKnown.SupportedResponseTypes.ShouldContain("id_token");
        smartWellKnown.Capabilities.ShouldNotBeNullOrEmpty();
        smartWellKnown.Capabilities.ShouldContain("launch-standalone");
        smartWellKnown.Capabilities.ShouldContain("client-public");
        smartWellKnown.Capabilities.ShouldContain("permission-v1");
        smartWellKnown.Capabilities.ShouldContain("permission-v2");
        smartWellKnown.SupportedChallengeMethods.ShouldNotBeNullOrEmpty();
        smartWellKnown.SupportedChallengeMethods.ShouldContain("S256");
    }

    /// <summary>Tests smart authorize.</summary>
    /// <param name="expectSuccess">True if the expect operation was a success, false if it failed.</param>
    /// <param name="audience">     The audience.</param>
    /// <param name="scope">        The scope.</param>
    [Theory]
    [InlineData(true, "http://localhost:5826/fhir/r4", "openid fhirUser profile launch/patient patient/*.read")]
    [InlineData(false, "http://localhost:5826/fhir/notAnEndpoint", "openid fhirUser profile launch/patient patient/*.read")]
    public void TestSmartAuthorize(
        bool expectSuccess,
        string audience,
        string scope)
    {
        string clientId = "clientid";
        string redirectUri = "http://localhost/dev/null";
        string? launch = null;
        string state = string.Empty;

        string pkceChallenge = string.Empty;
        string pkceMethod = string.Empty;

        bool success = _fixture.AuthR4.RequestAuth(
                _fixture.Name,
                string.Empty,
                "code",
                clientId,
                redirectUri,
                launch,
                scope,
                state,
                audience,
                pkceChallenge,
                pkceMethod,
                out string redirectDestination,
                out string _);

        success.ShouldBe(expectSuccess);
        
        // stop testing if we failed, regardless of expectation
        if (!success)
        {
            return;
        }

        redirectDestination.ShouldNotBeNullOrEmpty();

        string key = redirectDestination.Substring(redirectDestination.IndexOf("key=") + 4);
        key.ShouldNotBeNull();
        key.ShouldNotBeEmpty();

        _fixture.AuthR4.TryGetAuthorization(_fixture.Name, key, out AuthorizationInfo authInfo).ShouldBeTrue();
        authInfo.ShouldNotBeNull();
        authInfo.Tenant.ShouldBe(_fixture.Name);
        authInfo.Expires.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        authInfo.RequestParameters.ShouldNotBeNull();
        authInfo.Scopes.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Tests token request.</summary>
    /// <param name="launchPatient">     The launch patient.</param>
    /// <param name="launchPractitioner">The launch practitioner.</param>
    [Theory]
    [InlineData("Patient/1", "")]
    public void TestTokenRequest(
        string launchPatient,
        string launchPractitioner)
    {
        _fixture.AuthR4.RequestAuth(
            _fixture.Name,
            string.Empty,
            "code",
            "clientId",
            "http://localhost/dev/null",
            string.Empty,
            "openid fhirUser profile launch/patient patient/*.read",
            string.Empty,
            _fixture.ConfigR4.BaseUrl,
            string.Empty,
            string.Empty,
            out string redirectDestination,
            out string authKey).ShouldBeTrue();

        redirectDestination.ShouldNotBeNullOrEmpty();

        string queryAuthKey = redirectDestination.Substring(redirectDestination.IndexOf("key=") + 4);
        authKey.ShouldNotBeNullOrEmpty();
        queryAuthKey.ShouldNotBeNullOrEmpty();
        authKey.ShouldBeEquivalentTo(queryAuthKey);

        _fixture.AuthR4.TryGetAuthorization(
            _fixture.Name,
            authKey,
            out AuthorizationInfo auth).ShouldBeTrue();

        auth.ShouldNotBeNull();

        // get the redirect

        // set patient and practitioner
        auth.LaunchPatient = launchPatient;
        auth.LaunchPractitioner = launchPractitioner;

        // authorize all scopes
        foreach (string key in auth.Scopes.Keys)
        {
            auth.Scopes[key] = true;
        }

        // update auth
        _fixture.AuthR4.TryUpdateAuth(_fixture.Name, authKey, auth).ShouldBeTrue();

        // try to exchange the auth code for a token
        _fixture.AuthR4.TryCreateSmartResponse(
            _fixture.Name, 
            auth.AuthCode,
            "clientId",
            string.Empty,
            string.Empty,
            out AuthorizationInfo.SmartResponse response).ShouldBeTrue();

        response.ShouldNotBeNull();

        if (response == null)
        {
            return;
        }

        response.AccessToken.ShouldNotBeNullOrEmpty();
    }

    /// <summary>Tests a dynamic client registration and token request with an invalid token.</summary>
    [Theory]
    [FileData("data/smart/smart.rs384.public.json")]
    public void TestSmartClientRegistrationWithKeysFails(string publicJson)
    {
        string iss = "https://bili-monitor.example.com";
        bool success;
        string clientId;
        List<string> messages = new();

        JsonWebKeySet publicKeySet = new(publicJson);

        SmartClientRegistration clientRegistration = new()
        {
            ClientName = iss,
            KeySet = publicKeySet,
        };

        if (!_fixture.AuthR4.SmartClients.TryGetValue(iss, out ClientInfo? clientInfo) ||
            (clientInfo == null))
        {
            success = _fixture.AuthR4.TryRegisterClient(
                    clientRegistration,
                    out clientId,
                    out messages);

            success.ShouldBeTrue("client registration should pass\n" + string.Join("\n", messages));
        }
        else
        {
            // client is already registered
            success = true;
            clientId = iss;
        }

        // stop testing if we failed, regardless of expectation
        if (!success)
        {
            return;
        }

        clientId.ShouldBe(clientRegistration.ClientName);

        // try to get a token for this client
        string clientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
        string clientAssertion = "eyJhbGciOiJSUzM4NCIsImtpZCI6ImVlZTlmMTdhM2I1OThmZDg2NDE3YTk4MGI1OTFmYmU2IiwidHlwIjoiSldUIn0.eyJpc3MiOiJodHRwczovL2JpbGktbW9uaXRvci5leGFtcGxlLmNvbSIsInN1YiI6Imh0dHBzOi8vYmlsaS1tb25pdG9yLmV4YW1wbGUuY29tIiwiYXVkIjoiaHR0cHM6Ly9hdXRob3JpemUuc21hcnRoZWFsdGhpdC5vcmcvdG9rZW4iLCJleHAiOjE0MjI1Njg4NjAsImp0aSI6InJhbmRvbS1ub24tcmV1c2FibGUtand0LWlkLTEyMyJ9.D5kAqNJwaftCqsRdVVQDq6dMBxuGFOF5svQJuXbcYp-oEyg5qOwK9ZE5cGLTHxqwfpUPNzRKgVdIGuhawAA-8g0s1nKQae8CuKs33hhKh4J34xSEwW3MYs1gwI4GHTtR_g3kYSX6QCi14Ed3GIAvYFgqRqt-gD7sewMUXL4SB8I8cXcDbCqVizm7uPVhjw6QaeKZygJJ_AVLhM4Xs9LTy4HAhdCHpN0FrNmCerUIYJvHDpcod7A0jDmxdoeW1KIBYlhdhQNwjtsTvT1ce4qacN_3KIv_fIzCKLIgDv9eWxkjAtxOmIm8aW5gX9xX7X0nbd0QglIyiic_bZVNNEh0kg";
        string[] scopes = new string[] { "system/*.rs" };

        success = _fixture.AuthR4.TryClientAssertionExchange(
            _fixture.Name,
            string.Empty,
            clientAssertionType,
            clientAssertion,
            scopes,
            out AuthorizationInfo.SmartResponse response,
            out messages);

        success.ShouldBeFalse("submitting with an invalid assertion should fail");
        messages.ShouldNotBeNull();
        messages.ShouldNotBeEmpty();
        messages.Count().ShouldBe(2, "found:\n" + string.Join("\n", messages) + "\n");
        messages.Where(m => m.Contains("IDX10214: Audience validation failed")).ShouldNotBeNullOrEmpty("found:\n" + string.Join("\n", messages) + "\n");
        messages.Where(m => m.Contains("IDX10223: Lifetime validation failed")).ShouldNotBeNullOrEmpty("found:\n" + string.Join("\n", messages) + "\n");
    }


    [Theory]
    [FileData("data/smart/smart.rs384.private.json", "data/smart/smart.rs384.public.json")]
    public void TestSmartClientRegistrationWithKeys(string privateJson, string publicJson)
    {
        string iss = "https://bili-monitor.example.com";
        bool success;
        string clientId;
        List<string> messages = new();

        JsonWebKeySet privateKeySet = new(privateJson);
        JsonWebKeySet publicKeySet = new(publicJson);

        SmartClientRegistration clientRegistration = new()
        {
            ClientName = iss,
            KeySet = publicKeySet,
        };

        if (!_fixture.AuthR4.SmartClients.TryGetValue(iss, out ClientInfo? clientInfo) ||
            (clientInfo == null))
        {
            success = _fixture.AuthR4.TryRegisterClient(
                    clientRegistration,
                    out clientId,
                    out messages);

            success.ShouldBeTrue("client registration should pass\n" + string.Join("\n", messages));
        }
        else
        {
            // client is already registered
            success = true;
            clientId = iss;
        }

        // stop testing if we failed, regardless of expectation
        if (!success)
        {
            return;
        }

        clientId.ShouldBe(clientRegistration.ClientName);

        // build a client assertion
        string clientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
        string[] scopes = new string[] { "system/*.rs" };

        JsonWebKey? signingKey = privateKeySet.Keys.Where(wk => wk.KeyOps.Contains("sign")).FirstOrDefault();

        signingKey.ShouldNotBeNull("there should be a 'sign' capable key in the private set");
        if (signingKey == null)
        {
            return;
        }

        string clientAssertion = _fixture.AuthR4.GenerateSignedJwt(
            iss,
            iss,
            _fixture.ConfigR4.BaseUrl,
            Guid.NewGuid().ToString(),
            DateTime.UtcNow.AddMinutes(10),
            signingKey);

        clientAssertion.ShouldNotBeNullOrEmpty();

        success = _fixture.AuthR4.TryClientAssertionExchange(
            _fixture.Name,
            string.Empty,
            clientAssertionType,
            clientAssertion,
            scopes,
            out AuthorizationInfo.SmartResponse response,
            out messages);

        success.ShouldBeTrue("should be a valid client assertion!\n" + string.Join('\n', messages) + "\n");
        response.ShouldNotBeNull();
        response.AccessToken.ShouldNotBeNullOrEmpty();
    }
}

/// <summary>An authentication test fixture.</summary>
public class AuthTestFixture
{
    /// <summary>(Immutable) The name.</summary>
    public readonly string Name = "r4";

    /// <summary>(Immutable) The configuration for FHIR R4.</summary>
    public TenantConfiguration ConfigR4 { get; set; }

    /// <summary>Gets or sets the tenants.</summary>
    public Dictionary<string, TenantConfiguration> Tenants { get; set; }

    /// <summary>The FHIR store for FHIR R4.</summary>
    public ISmartAuthManager AuthR4 { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthTestFixture"/> class.
    /// </summary>
    public AuthTestFixture()
    {
        ConfigR4 = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = Name,
            BaseUrl = "http://localhost:5826/fhir/r4",
            SmartRequired = true,
        };

        Tenants = new()
        {
            { Name, ConfigR4 }
        };

        AuthR4 = new SmartAuthManager(
            Tenants, 
            new()
            {
                PublicUrl = "http://localhost:5826/fhir/r4",
                ListenPort = 5826,
                OpenBrowser = false,
                TenantsR4 = [ Name ],
                SmartRequiredTenants = [ Name ],
            },
            null);

        AuthR4.Init();
    }
}
