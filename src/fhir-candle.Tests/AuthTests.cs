// <copyright file="AuthTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using fhir.candle.Services;
using fhir.candle.Tests.Extensions;
using FhirCandle.Models;
using FhirCandle.Smart;
using FhirCandle.Utils;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

        if (response is null)
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
            (clientInfo is null))
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
            (clientInfo is null))
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
        if (signingKey is null)
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
    /// <summary>Preserves the client assertion audience defaults at the service boundary.</summary>
    /// <param name="audience">The assertion audience.</param>
    /// <param name="expectSuccess">Whether the assertion should be accepted.</param>
    [Theory]
    [InlineData("http://localhost:5826/fhir/r4", true)]
    [InlineData("http://localhost:5826/fhir/R4", false)]
    [InlineData("http://localhost:5826/fhir/r4/", true)]
    public void ClientAssertionAudienceMatchingPreservesCurrentDefaults(string audience, bool expectSuccess)
    {
        using SmartAuthManager manager = CreateAssertionManager(out TenantConfiguration tenant);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys("RS384", "audience-key");
        string clientId = RegisterAssertionClient(manager, publicKey);
        int authorizationCount = manager.SmartAuthorizations.Count;
        string assertion = manager.GenerateSignedJwt(
            clientId, clientId, audience, Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(10), privateKey);

        bool success = manager.TryClientAssertionExchange(
            tenant.ControllerName, string.Empty, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertion, ["system/*.rs"], out AuthorizationInfo.SmartResponse response, out List<string> messages);

        success.ShouldBe(expectSuccess, string.Join("\n", messages));
        if (expectSuccess)
        {
            AssertAssertionResponse(manager, tenant, clientId, response);
            messages.ShouldBeEmpty();
            manager.SmartAuthorizations.Count.ShouldBe(authorizationCount + 1);
        }
        else
        {
            response.ShouldBeNull();
            messages.Count.ShouldBe(1);
            messages[0].ShouldContain("IDX10214: Audience validation failed");
            manager.SmartAuthorizations.Count.ShouldBe(authorizationCount);
        }

        WriteAssertionRuntime("RS384");
    }

    /// <summary>Rejects a wrong signer even when the assertion names a registered key.</summary>
    [Fact]
    public void ClientAssertionRejectsInvalidSignature()
    {
        using SmartAuthManager manager = CreateAssertionManager(out TenantConfiguration tenant);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys("RS384", "registered-key");
        (JsonWebKey wrongPrivateKey, JsonWebKey wrongPublicKey) = CreateAssertionKeys("RS384", publicKey.Kid);
        wrongPublicKey.N.ShouldNotBe(publicKey.N);
        string clientId = RegisterAssertionClient(manager, publicKey);
        manager.SmartClients[clientId].Keys.ShouldContainKey(wrongPrivateKey.Kid);
        int authorizationCount = manager.SmartAuthorizations.Count;
        string assertion = manager.GenerateSignedJwt(
            clientId, clientId, tenant.BaseUrl, Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(10), wrongPrivateKey);

        manager.TryClientAssertionExchange(
            tenant.ControllerName, string.Empty, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertion, ["system/*.rs"], out AuthorizationInfo.SmartResponse response, out List<string> messages).ShouldBeFalse();

        response.ShouldBeNull();
        messages.ShouldNotBeEmpty();
        messages.All(message => message.Contains("IDX10511: Signature validation failed")).ShouldBeTrue(string.Join("\n", messages));
        manager.SmartAuthorizations.Count.ShouldBe(authorizationCount);

        // A correctly signed assertion with an unregistered key ID must also fail before issuance.
        privateKey.Kid = "unregistered-key";
        assertion = manager.GenerateSignedJwt(
            clientId, clientId, tenant.BaseUrl, Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(10), privateKey);
        manager.TryClientAssertionExchange(
            tenant.ControllerName, string.Empty, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertion, ["system/*.rs"], out response, out messages).ShouldBeFalse();
        response.ShouldBeNull();
        messages.Count.ShouldBe(1);
        messages[0].ShouldContain("signing key id (kid) unregistered-key was not found");
        manager.SmartAuthorizations.Count.ShouldBe(authorizationCount);
        WriteAssertionRuntime("RS384");
    }

    /// <summary>Exercises each advertised registered signing algorithm through JWT issuance and exchange.</summary>
    /// <param name="algorithm">The existing registered signing algorithm.</param>
    [Theory]
    [InlineData("RS384")]
    [InlineData("ES384")]
    public void ClientAssertionPreservesRegisteredSigningAlgorithms(string algorithm)
    {
        using SmartAuthManager manager = CreateAssertionManager(out TenantConfiguration tenant);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys(algorithm, "algorithm-key");
        if (algorithm == "ES384")
        {
            privateKey.Crv.ShouldBe("P-384");
            publicKey.Crv.ShouldBe("P-384");
        }

        string clientId = RegisterAssertionClient(manager, publicKey);
        string jti = Guid.NewGuid().ToString();
        string assertion = manager.GenerateSignedJwt(
            clientId, clientId, tenant.BaseUrl, jti, DateTime.UtcNow.AddMinutes(10), privateKey);

        JsonWebToken jwt = new(assertion);
        jwt.Alg.ShouldBe(algorithm);
        jwt.Kid.ShouldBe(publicKey.Kid);
        jwt.Issuer.ShouldBe(clientId);
        jwt.Subject.ShouldBe(clientId);
        jwt.Id.ShouldBe(jti);
        jwt.Audiences.ShouldBe([tenant.BaseUrl]);
        IEnumerable<string>? signingAlgorithms = manager.SmartConfigurationByTenant[tenant.ControllerName].TokenEndpointAuthSigningAlgs;
        signingAlgorithms.ShouldNotBeNull();
        signingAlgorithms.ShouldContain(algorithm);

        manager.TryClientAssertionExchange(
            tenant.ControllerName, string.Empty, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertion, ["system/*.rs"], out AuthorizationInfo.SmartResponse response, out List<string> messages)
            .ShouldBeTrue(string.Join("\n", messages));

        messages.ShouldBeEmpty();
        AssertAssertionResponse(manager, tenant, clientId, response);
        WriteAssertionRuntime(algorithm);
    }

    /// <summary>Rejects a different EC signer even when the assertion names a registered key.</summary>
    [Fact]
    public void Es384ClientAssertionRejectsInvalidSignature()
    {
        using SmartAuthManager manager = CreateAssertionManager(out TenantConfiguration tenant);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys("ES384", "registered-ec-key");
        (JsonWebKey wrongPrivateKey, JsonWebKey wrongPublicKey) = CreateAssertionKeys("ES384", publicKey.Kid);
        wrongPublicKey.X.ShouldNotBe(publicKey.X);
        wrongPrivateKey.D.ShouldNotBe(privateKey.D);
        string clientId = RegisterAssertionClient(manager, publicKey);
        manager.SmartClients[clientId].Keys.ShouldContainKey(wrongPrivateKey.Kid);
        int authorizationCount = manager.SmartAuthorizations.Count;
        string assertion = manager.GenerateSignedJwt(
            clientId, clientId, tenant.BaseUrl, Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(10), wrongPrivateKey);

        manager.TryClientAssertionExchange(
            tenant.ControllerName, string.Empty, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertion, ["system/*.rs"], out AuthorizationInfo.SmartResponse response, out List<string> messages).ShouldBeFalse();

        response.ShouldBeNull();
        messages.ShouldNotBeEmpty();
        messages.All(message => message.Contains("IDX10511: Signature validation failed")).ShouldBeTrue(string.Join("\n", messages));
        manager.SmartAuthorizations.Count.ShouldBe(authorizationCount);
        WriteAssertionRuntime("ES384");
    }

    /// <summary>Preserves registration diagnostics and unusable-key behavior for missing EC fields.</summary>
    /// <param name="missingField">The public JWK field to omit.</param>
    /// <param name="diagnosticField">The existing diagnostic field description.</param>
    [Theory]
    [InlineData("crv", "ECDSA Curve (crv)")]
    [InlineData("x", "ECDSA X coordinate (x)")]
    [InlineData("y", "ECDSA Y coordinate (y)")]
    public void Es384RegistrationPreservesMissingKeyFieldDiagnostics(string missingField, string diagnosticField)
    {
        using SmartAuthManager manager = CreateAssertionManager(out TenantConfiguration tenant);
        (JsonWebKey privateKey, JsonWebKey publicKey) = CreateAssertionKeys("ES384", "missing-field-key");
        switch (missingField)
        {
            case "crv":
                publicKey.Crv = null;
                break;
            case "x":
                publicKey.X = null;
                break;
            case "y":
                publicKey.Y = null;
                break;
        }

        JsonWebKeySet keySet = new();
        keySet.Keys.Add(publicKey);
        SmartClientRegistration registration = new()
        {
            ClientName = "https://client.example.test/" + Guid.NewGuid().ToString(),
            KeySet = keySet,
        };
        int authorizationCount = manager.SmartAuthorizations.Count;
        manager.TryRegisterClient(registration, out string clientId, out List<string> messages).ShouldBeTrue();
        clientId.ShouldBe(registration.ClientName);
        messages.ShouldBe(new[]
        {
            $"TryRegisterClient <<< request {clientId}:ES384 is missing the {diagnosticField}.",
            $"TryRegisterClient <<< request {clientId}:ES384 could not be processed and will not be available.",
            $"TryRegisterClient <<< request {clientId} has no keys.",
        });
        manager.SmartClients[clientId].Keys.ShouldBeEmpty();

        // The unchanged private key can sign, but the unusable public registration cannot authorize it.
        string assertion = manager.GenerateSignedJwt(
            clientId, clientId, tenant.BaseUrl, Guid.NewGuid().ToString(), DateTime.UtcNow.AddMinutes(10), privateKey);
        manager.TryClientAssertionExchange(
            tenant.ControllerName, string.Empty, "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            assertion, ["system/*.rs"], out AuthorizationInfo.SmartResponse response, out messages).ShouldBeFalse();
        response.ShouldBeNull();
        messages.ShouldBe(new[] { "TryClientAssertionExchange <<< client has NO keys." });
        manager.SmartAuthorizations.Count.ShouldBe(authorizationCount);
        WriteAssertionRuntime("ES384");
    }

    /// <summary>Creates isolated SMART state without opening a listener or using shared fixture clients.</summary>
    private static SmartAuthManager CreateAssertionManager(out TenantConfiguration tenant)
    {
        tenant = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost:5826/fhir/r4",
            SmartRequired = true,
        };
        SmartAuthManager manager = new(
            new Dictionary<string, TenantConfiguration> { [tenant.ControllerName] = tenant },
            new()
            {
                PublicUrl = "http://localhost:5826",
                ListenPort = 5826,
                OpenBrowser = false,
                TenantsR4 = [tenant.ControllerName],
                SmartRequiredTenants = [tenant.ControllerName],
            },
            null);
        manager.Init();
        return manager;
    }

    /// <summary>Generates synthetic private/public JWKs in memory using the existing algorithms.</summary>
    private static (JsonWebKey PrivateKey, JsonWebKey PublicKey) CreateAssertionKeys(string algorithm, string keyId)
    {
        JsonWebKey privateKey;
        JsonWebKey publicKey;
        if (algorithm == "RS384")
        {
            using RSA rsa = RSA.Create(2048);
            privateKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true)));
            publicKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)));
        }
        else
        {
            algorithm.ShouldBe("ES384");
            using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            using ECDsa publicEcdsa = ECDsa.Create(ecdsa.ExportParameters(false));
            privateKey = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(ecdsa));
            publicKey = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(new ECDsaSecurityKey(publicEcdsa));
        }

        privateKey.Alg = algorithm;
        privateKey.Kid = keyId;
        privateKey.KeyOps.Add("sign");
        publicKey.Alg = algorithm;
        publicKey.Kid = keyId;
        publicKey.KeyOps.Add("verify");
        publicKey.D.ShouldBeNullOrEmpty();
        return (privateKey, publicKey);
    }

    /// <summary>Registers only the public key and verifies it was actually processed.</summary>
    private static string RegisterAssertionClient(SmartAuthManager manager, JsonWebKey publicKey)
    {
        JsonWebKeySet keySet = new();
        keySet.Keys.Add(publicKey);
        SmartClientRegistration registration = new()
        {
            ClientName = "https://client.example.test/" + Guid.NewGuid().ToString(),
            KeySet = keySet,
        };
        manager.TryRegisterClient(registration, out string clientId, out List<string> messages)
            .ShouldBeTrue(string.Join("\n", messages));
        messages.ShouldBeEmpty();
        clientId.ShouldBe(registration.ClientName);
        manager.SmartClients[clientId].Keys.ShouldContainKey(publicKey.Kid);
        return clientId;
    }

    /// <summary>Checks that an accepted assertion issues a usable token for the intended tenant and scopes.</summary>
    private static void AssertAssertionResponse(
        SmartAuthManager manager, TenantConfiguration tenant, string clientId, AuthorizationInfo.SmartResponse response)
    {
        response.ShouldNotBeNull();
        response.AccessToken.ShouldNotBeNullOrEmpty();
        response.RefreshToken.ShouldNotBeNullOrEmpty();
        response.IdToken.ShouldNotBeNullOrEmpty();
        response.ClientId.ShouldBe(clientId);
        response.TokenType.ShouldBe("bearer");
        response.Scopes.ShouldBe("system/*.rs");
        manager.TryGetAuthorization(tenant.ControllerName, "Bearer " + response.AccessToken, out AuthorizationInfo auth).ShouldBeTrue();
        auth.Tenant.ShouldBe(tenant.ControllerName);
        auth.RequestParameters.ClientId.ShouldBe(clientId);
        auth.RequestParameters.Audience.ShouldBe(tenant.BaseUrl);
        auth.RequestParameters.Scope.ShouldBe("system/*.rs");
        auth.Expires.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    /// <summary>Records loaded assemblies, not merely restored package declarations.</summary>
    private void WriteAssertionRuntime(string algorithm)
    {
        Console.WriteLine($"SMART service algorithm={algorithm}; runtime={RuntimeInformation.FrameworkDescription}; OS={RuntimeInformation.OSDescription}");
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(assembly => assembly.FullName))
        {
            string name = assembly.GetName().Name ?? string.Empty;
            if (name.StartsWith("Microsoft.IdentityModel.", StringComparison.Ordinal) ||
                name == "Microsoft.Bcl.Cryptography" ||
                name == "System.Formats.Asn1" ||
                name == "Microsoft.Extensions.Logging.Abstractions")
            {
                Console.WriteLine($"{assembly.FullName}; location={assembly.Location}");
            }
        }
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
