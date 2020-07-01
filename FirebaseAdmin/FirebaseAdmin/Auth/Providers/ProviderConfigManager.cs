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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin.Util;
using Google.Api.Gax;
using Google.Api.Gax.Rest;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Json;
using Google.Apis.Util;
using Newtonsoft.Json.Linq;

namespace FirebaseAdmin.Auth.Providers
{
    /// <summary>
    /// ProviderConfigManager provides methods for interacting with the
    /// <a href="https://developers.google.com/identity/toolkit/web/reference/relyingparty">
    /// Google Identity Toolkit v2</a> via its REST API. This class does not hold any mutable
    /// state, and is thread safe.
    /// </summary>
    internal sealed class ProviderConfigManager : IDisposable
    {
        internal const string ClientVersionHeader = "X-Client-Version";

        internal static readonly string ClientVersion = $"DotNet/Admin/{FirebaseApp.GetSdkVersion()}";

        private const string IdToolkitUrl = "https://identitytoolkit.googleapis.com/v2/projects/{0}";

        private readonly ErrorHandlingHttpClient<FirebaseAuthException> httpClient;
        private readonly string baseUrl;

        internal ProviderConfigManager(Args args)
        {
            args.ThrowIfNull(nameof(args));
            if (string.IsNullOrEmpty(args.ProjectId))
            {
                throw new ArgumentException(
                    "Must initialize FirebaseApp with a project ID to manage provider"
                    + " configurations.");
            }

            this.httpClient = new ErrorHandlingHttpClient<FirebaseAuthException>(
                new ErrorHandlingHttpClientArgs<FirebaseAuthException>()
                {
                    HttpClientFactory = args.ClientFactory,
                    Credential = args.Credential,
                    ErrorResponseHandler = AuthErrorHandler.Instance,
                    RequestExceptionHandler = AuthErrorHandler.Instance,
                    DeserializeExceptionHandler = AuthErrorHandler.Instance,
                    RetryOptions = args.RetryOptions,
                });
            this.baseUrl = string.Format(IdToolkitUrl, args.ProjectId);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        internal static ProviderConfigManager Create(FirebaseApp app)
        {
            var args = new Args
            {
                ClientFactory = app.Options.HttpClientFactory,
                Credential = app.Options.Credential,
                ProjectId = app.GetProjectId(),
                RetryOptions = RetryOptions.Default,
            };

            return new ProviderConfigManager(args);
        }

        internal async Task<OidcProviderConfig> GetOidcProviderConfigAsync(
            string providerId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null or empty.");
            }

            if (!providerId.StartsWith("oidc."))
            {
                throw new ArgumentException("OIDC provider ID must have the prefix 'oidc.'.");
            }

            var request = this.CreateHttpRequestMessage(
                HttpMethod.Get, $"oauthIdpConfigs/{providerId}");
            var response = await this.httpClient
                .SendAndDeserializeAsync<OidcProviderConfig.Request>(request, cancellationToken)
                .ConfigureAwait(false);
            return new OidcProviderConfig(response.Result);
        }

        internal async Task<T> CreateProviderConfigAsync<T>(
            AuthProviderConfigArgs<T> args, CancellationToken cancellationToken)
            where T : AuthProviderConfig
        {
            args.ThrowIfNull(nameof(args));
            var query = new Dictionary<string, object>()
            {
                { "oauthIdpConfigId", args.ValidateProviderId() },
            };
            var body = args.ToCreateRequest();
            var request = this.CreateHttpRequestMessage(
                HttpMethod.Post, "oauthIdpConfigs", body, query);
            var response = await this.httpClient
                .SendAndDeserializeAsync<JObject>(request, cancellationToken)
                .ConfigureAwait(false);
            return args.CreateAuthProviderConfig(response.Body);
        }

        internal async Task<T> UpdateProviderConfigAsync<T>(
            AuthProviderConfigArgs<T> args, CancellationToken cancellationToken)
            where T : AuthProviderConfig
        {
            args.ThrowIfNull(nameof(args));
            var providerId = args.ValidateProviderId();
            var body = args.ToUpdateRequest();
            var updateMask = this.CreateUpdateMask(body);
            if (updateMask.Count == 0)
            {
                throw new ArgumentException("At least one field must be specified for update.");
            }

            var query = new Dictionary<string, object>()
            {
                { "updateMask", string.Join(",", updateMask) },
            };

            var request = this.CreateHttpRequestMessage(
                new HttpMethod("PATCH"), $"oauthIdpConfigs/{providerId}", body, query);
            var response = await this.httpClient
                .SendAndDeserializeAsync<JObject>(request, cancellationToken)
                .ConfigureAwait(false);
            return args.CreateAuthProviderConfig(response.Body);
        }

        internal PagedAsyncEnumerable<AuthProviderConfigs<OidcProviderConfig>, OidcProviderConfig>
            ListOidcProviderConfigsAsync(ListProviderConfigsOptions options)
        {
            var request = new ListOidcProviderConfigsRequest(
                this.baseUrl, this.httpClient, options);
            return new RestPagedAsyncEnumerable
                <
                    ListProviderConfigsRequest<OidcProviderConfig>,
                    AuthProviderConfigs<OidcProviderConfig>,
                    OidcProviderConfig
                >(() => request, new ListProviderConfigsPageManager<OidcProviderConfig>());
        }

        internal async Task<SamlProviderConfig> GetSamlProviderConfigAsync(
            string providerId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null or empty.");
            }

            if (!providerId.StartsWith("saml."))
            {
                throw new ArgumentException("SAML provider ID must have the prefix 'saml.'.");
            }

            var request = this.CreateHttpRequestMessage(
                HttpMethod.Get, $"inboundSamlConfigs/{providerId}");
            var response = await this.httpClient
                .SendAndDeserializeAsync<SamlProviderConfig.Request>(request, cancellationToken)
                .ConfigureAwait(false);
            return new SamlProviderConfig(response.Result);
        }

        private static string EncodeQueryParams(IDictionary<string, object> queryParams)
        {
            var queryString = string.Empty;
            if (queryParams != null && queryParams.Count > 0)
            {
                var list = queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}");
                queryString = "?" + string.Join("&", list);
            }

            return queryString;
        }

        private HttpRequestMessage CreateHttpRequestMessage(
            HttpMethod method,
            string path,
            object body = null,
            Dictionary<string, object> queryParams = null)
        {
            var request = new HttpRequestMessage()
            {
                Method = method,
                RequestUri = new Uri($"{this.baseUrl}/{path}{EncodeQueryParams(queryParams)}"),
            };
            if (body != null)
            {
                request.Content = NewtonsoftJsonSerializer.Instance.CreateJsonHttpContent(body);
            }

            request.Headers.Add(ClientVersionHeader, ClientVersion);
            return request;
        }

        private IList<string> CreateUpdateMask(AuthProviderConfig.Request request)
        {
            var json = NewtonsoftJsonSerializer.Instance.Serialize(request);
            var dictionary = JObject.Parse(json);
            var mask = this.CreateUpdateMask(dictionary);
            mask.Sort();
            return mask;
        }

        private List<string> CreateUpdateMask(JObject dictionary)
        {
            var mask = new List<string>();
            foreach (var entry in dictionary)
            {
                if (entry.Value.Type == JTokenType.Object)
                {
                    var childMask = this.CreateUpdateMask((JObject)entry.Value);
                    mask.AddRange(childMask.Select((item) => $"{entry.Key}.{item}"));
                }
                else
                {
                    mask.Add(entry.Key);
                }
            }

            return mask;
        }

        internal sealed class Args
        {
            internal HttpClientFactory ClientFactory { get; set; }

            internal GoogleCredential Credential { get; set; }

            internal string ProjectId { get; set; }

            internal RetryOptions RetryOptions { get; set; }
        }
    }
}
