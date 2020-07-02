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
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin.Util;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Util;

namespace FirebaseAdmin.Auth.Providers
{
    /// <summary>
    /// ProviderConfigManager is a facade for managing auth provider configurations in a
    /// Firebase project. It is used by other high-level classes like <see cref="FirebaseAuth"/>.
    /// Remote API calls are delegated to the appropriate <see cref="ServiceStub{T}"/>
    /// implementations, with <see cref="ApiClient"/> providing the required HTTP primitives.
    /// </summary>
    internal sealed class ProviderConfigManager : IDisposable
    {
        private readonly ErrorHandlingHttpClient<FirebaseAuthException> httpClient;
        private readonly ApiClient client;

        internal ProviderConfigManager(Args args)
        {
            var projectId = args.ThrowIfNull(nameof(args)).ProjectId;
            if (string.IsNullOrEmpty(projectId))
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
            this.client = new ApiClient(projectId, this.httpClient);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        internal static ProviderConfigManager Create(FirebaseApp app)
        {
            var args = new Args()
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
            return await OidcServiceStub.Instance
                .GetProviderConfigAsync(this.client, providerId, cancellationToken)
                .ConfigureAwait(false);
        }

        internal async Task<T> CreateProviderConfigAsync<T>(
            AuthProviderConfigArgs<T> args, CancellationToken cancellationToken)
            where T : AuthProviderConfig
        {
            var stub = args.ThrowIfNull(nameof(args)).GetServiceStub();
            return await stub.CreateProviderConfigAsync(this.client, args, cancellationToken)
                .ConfigureAwait(false);
        }

        internal async Task<T> UpdateProviderConfigAsync<T>(
            AuthProviderConfigArgs<T> args, CancellationToken cancellationToken)
            where T : AuthProviderConfig
        {
            var stub = args.ThrowIfNull(nameof(args)).GetServiceStub();
            return await stub.UpdateProviderConfigAsync(this.client, args, cancellationToken)
                .ConfigureAwait(false);
        }

        internal PagedAsyncEnumerable<AuthProviderConfigs<OidcProviderConfig>, OidcProviderConfig>
            ListOidcProviderConfigsAsync(ListProviderConfigsOptions options)
        {
            return OidcServiceStub.Instance.ListProviderConfigsAsync(this.client, options);
        }

        internal async Task<SamlProviderConfig> GetSamlProviderConfigAsync(
            string providerId, CancellationToken cancellationToken)
        {
            return await SamlServiceStub.Instance
                .GetProviderConfigAsync(this.client, providerId, cancellationToken)
                .ConfigureAwait(false);
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
