using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Api.Gax.Rest;
using Google.Apis.Json;
using Newtonsoft.Json;

namespace FirebaseAdmin.Auth.Providers
{
    internal sealed class OidcServiceStub : ServiceStub<OidcProviderConfig>
    {
        internal static readonly OidcServiceStub Instance = new OidcServiceStub();

        private OidcServiceStub() { }

        internal override async Task<OidcProviderConfig> GetProviderConfigAsync(
            ApiClient client, string providerId, CancellationToken cancellationToken)
        {
            // TODO: Can we do this everywhere?
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = BuildUri($"oauthIdpConfigs/{this.ValidateProviderId(providerId)}"),
            };
            var response = await client
                .SendAndDeserializeAsync<OidcProviderConfig.Request>(request, cancellationToken)
                .ConfigureAwait(false);
            return new OidcProviderConfig(response);
        }

        internal override async Task<OidcProviderConfig> CreateProviderConfigAsync(
            ApiClient client,
            AuthProviderConfigArgs<OidcProviderConfig> args,
            CancellationToken cancellationToken)
        {
            var query = new Dictionary<string, object>()
            {
                { "oauthIdpConfigId", this.ValidateProviderId(args.ProviderId) },
            };
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = BuildUri("oauthIdpConfigs", query),
                Content = NewtonsoftJsonSerializer.Instance.CreateJsonHttpContent(
                    args.ToCreateRequest()),
            };
            var response = await client
                .SendAndDeserializeAsync<OidcProviderConfig.Request>(request, cancellationToken)
                .ConfigureAwait(false);
            return new OidcProviderConfig(response);
        }

        internal override async Task<OidcProviderConfig> UpdateProviderConfigAsync(
            ApiClient client,
            AuthProviderConfigArgs<OidcProviderConfig> args,
            CancellationToken cancellationToken)
        {
            var providerId = this.ValidateProviderId(args.ProviderId);
            var content = args.ToUpdateRequest();
            var updateMask = CreateUpdateMask(content);
            if (updateMask.Count == 0)
            {
                throw new ArgumentException("At least one field must be specified for update.");
            }

            var query = new Dictionary<string, object>()
            {
                { "updateMask", string.Join(",", updateMask) },
            };
            var request = new HttpRequestMessage()
            {
                Method = Patch,
                RequestUri = BuildUri($"oauthIdpConfigs/{providerId}", query),
                Content = NewtonsoftJsonSerializer.Instance.CreateJsonHttpContent(content),
            };
            var response = await client
                .SendAndDeserializeAsync<OidcProviderConfig.Request>(request, cancellationToken)
                .ConfigureAwait(false);
            return new OidcProviderConfig(response);
        }

        internal override PagedAsyncEnumerable<AuthProviderConfigs<OidcProviderConfig>, OidcProviderConfig>
            ListProviderConfigsAsync(ApiClient client, ListProviderConfigsOptions options)
        {
            var request = new ListRequest(client, options);
            return new RestPagedAsyncEnumerable
                <
                    AbstractListRequest,
                    AuthProviderConfigs<OidcProviderConfig>,
                    OidcProviderConfig
                >(() => request, new PageManager());
        }

        private string ValidateProviderId(string providerId)
        {
            if (string.IsNullOrEmpty(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null or empty.");
            }

            if (!providerId.StartsWith("oidc."))
            {
                throw new ArgumentException("OIDC provider ID must have the prefix 'oidc.'.");
            }

            return providerId;
        }

        private sealed class ListRequest : AbstractListRequest
        {
            internal ListRequest(ApiClient client, ListProviderConfigsOptions options)
            : base(client, options) { }

            public override string MethodName => "ListOidcProviderConfigs";

            public override string RestPath => "oauthIdpConfigs";

            public override async Task<AuthProviderConfigs<OidcProviderConfig>> ExecuteAsync(
                CancellationToken cancellationToken)
            {
                var request = this.CreateRequest();
                var response = await this.ApiClient
                    .SendAndDeserializeAsync<ListResponse>(request, cancellationToken)
                    .ConfigureAwait(false);
                var configs = response.Configs?.Select(config => new OidcProviderConfig(config));
                return new AuthProviderConfigs<OidcProviderConfig>
                {
                    NextPageToken = response.NextPageToken,
                    ProviderConfigs = configs,
                };
            }
        }

        private sealed class ListResponse
        {
            /// <summary>
            /// Gets or sets the next page link.
            /// </summary>
            [JsonProperty("nextPageToken")]
            public string NextPageToken { get; set; }

            /// <summary>
            /// Gets or sets the users.
            /// </summary>
            [JsonProperty("oauthIdpConfigs")]
            public IEnumerable<OidcProviderConfig.Request> Configs { get; set; }
        }
    }
}