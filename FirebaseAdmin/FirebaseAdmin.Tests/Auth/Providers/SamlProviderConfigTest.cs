// Copyright 2020, Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FirebaseAdmin.Tests;
using Xunit;

namespace FirebaseAdmin.Auth.Providers.Tests
{
    public class SamlProviderConfigTest
    {
        private const string SamlProviderConfigResponse = @"{
            ""name"": ""projects/mock-project-id/inboundSamlConfigs/saml.provider"",
            ""idpConfig"": {
                ""idpEntityId"": ""IDP_ENTITY_ID"",
                ""ssoUrl"": ""https://example.com/login"",
                ""signRequest"": true,
                ""idpCertificates"": [
                    {""x509Certificate"": ""CERT1""},
                    {""x509Certificate"": ""CERT2""}
                ]
            },
            ""spConfig"": {
                ""spEntityId"": ""RP_ENTITY_ID"",
                ""callbackUri"": ""https://projectId.firebaseapp.com/__/auth/handler""
            },
            ""displayName"": ""samlProviderName"",
            ""enabled"": true
        }";

        [Fact]
        public async Task GetConfig()
        {
            var handler = new MockMessageHandler()
            {
                Response = SamlProviderConfigResponse,
            };
            var auth = ProviderConfigTestUtils.CreateFirebaseAuth(handler);

            var provider = await auth.GetSamlProviderConfigAsync("saml.provider");

            this.AssertSamlProviderConfig(provider);
            Assert.Equal(1, handler.Requests.Count);
            var request = handler.Requests[0];
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "/v2/projects/project1/inboundSamlConfigs/saml.provider",
                request.Url.PathAndQuery);
            ProviderConfigTestUtils.AssertClientVersionHeader(request);
        }

        [Theory]
        [MemberData(
            nameof(ProviderConfigTestUtils.InvalidStrings),
            MemberType=typeof(ProviderConfigTestUtils))]
        public async Task GetConfigNoProviderId(string providerId)
        {
            var auth = ProviderConfigTestUtils.CreateFirebaseAuth();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => auth.GetSamlProviderConfigAsync(providerId));
            Assert.Equal("Provider ID cannot be null or empty.", exception.Message);
        }

        [Fact]
        public async Task GetConfigInvalidProviderId()
        {
            var auth = ProviderConfigTestUtils.CreateFirebaseAuth();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => auth.GetSamlProviderConfigAsync("oidc.provider"));

            Assert.Equal("SAML provider ID must have the prefix 'saml.'.", exception.Message);
        }

        [Fact]
        public async Task GetConfigNotFoundError()
        {
            var handler = new MockMessageHandler()
            {
                StatusCode = HttpStatusCode.NotFound,
                Response = ProviderConfigTestUtils.ConfigNotFoundResponse,
            };
            var auth = ProviderConfigTestUtils.CreateFirebaseAuth(handler);

            var exception = await Assert.ThrowsAsync<FirebaseAuthException>(
                () => auth.GetSamlProviderConfigAsync("saml.provider"));

            Assert.Equal(ErrorCode.NotFound, exception.ErrorCode);
            Assert.Equal(AuthErrorCode.ConfigurationNotFound, exception.AuthErrorCode);
            Assert.Equal(
                "No identity provider configuration found for the given identifier "
                + "(CONFIGURATION_NOT_FOUND).",
                exception.Message);
            Assert.NotNull(exception.HttpResponse);
            Assert.Null(exception.InnerException);
        }

        private void AssertSamlProviderConfig(SamlProviderConfig provider)
        {
            Assert.Equal("saml.provider", provider.ProviderId);
            Assert.Equal("samlProviderName", provider.DisplayName);
            Assert.True(provider.Enabled);
            Assert.Equal("IDP_ENTITY_ID", provider.IdpEntityId);
            Assert.Equal("https://example.com/login", provider.SsoUrl);
            Assert.Equal(
                new List<string> { "CERT1", "CERT2" }, provider.X509Certificates);
            Assert.Equal("RP_ENTITY_ID", provider.RpEntityId);
            Assert.Equal(
                "https://projectId.firebaseapp.com/__/auth/handler", provider.CallbackUrl);
        }
    }
}
