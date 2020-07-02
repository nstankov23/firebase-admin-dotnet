using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;

namespace FirebaseAdmin.Auth.Providers
{
    internal sealed class SamlServiceStub : ServiceStub<SamlProviderConfig>
    {
        internal static readonly SamlServiceStub Instance = new SamlServiceStub();

        private SamlServiceStub() { }

        internal override async Task<SamlProviderConfig> GetProviderConfigAsync(
            ApiClient client, string providerId, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = BuildUri($"inboundSamlConfigs/{this.ValidateProviderId(providerId)}"),
            };
            var response = await client
                .SendAndDeserializeAsync<SamlProviderConfig.Request>(request, cancellationToken)
                .ConfigureAwait(false);
            return new SamlProviderConfig(response);
        }

        internal override Task<SamlProviderConfig> CreateProviderConfigAsync(
            ApiClient client,
            AuthProviderConfigArgs<SamlProviderConfig> args,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override Task<SamlProviderConfig> UpdateProviderConfigAsync(
            ApiClient client,
            AuthProviderConfigArgs<SamlProviderConfig> args,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override PagedAsyncEnumerable<AuthProviderConfigs<SamlProviderConfig>, SamlProviderConfig>
            ListProviderConfigsAsync(ApiClient client, ListProviderConfigsOptions options)
        {
            throw new NotImplementedException();
        }

        private string ValidateProviderId(string providerId)
        {
            if (string.IsNullOrEmpty(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null or empty.");
            }

            if (!providerId.StartsWith("saml."))
            {
                throw new ArgumentException("SAML provider ID must have the prefix 'saml.'.");
            }

            return providerId;
        }
    }
}