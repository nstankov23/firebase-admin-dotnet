using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin.Util;

namespace FirebaseAdmin.Auth.Providers
{
    /// <summary>
    /// ApiClient provides low-level methods for interacting with the
    /// <a href="https://developers.google.com/identity/toolkit/web/reference/relyingparty">
    /// Google Identity Toolkit v2 REST API</a>.
    /// </summary>
    internal sealed class ApiClient
    {
        private const string IdToolkitUrl = "https://identitytoolkit.googleapis.com/v2/projects/{0}";

        private const string ClientVersionHeader = "X-Client-Version";

        private static readonly string ClientVersion = $"DotNet/Admin/{FirebaseApp.GetSdkVersion()}";

        private readonly string baseUrl;

        private readonly ErrorHandlingHttpClient<FirebaseAuthException> httpClient;

        internal ApiClient(
            string projectId, ErrorHandlingHttpClient<FirebaseAuthException> httpClient)
        {
            this.baseUrl = string.Format(IdToolkitUrl, projectId);
            this.httpClient = httpClient;
        }

        internal async Task<T> SendAndDeserializeAsync<T>(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri.IsAbsoluteUri)
            {
                request.RequestUri = new Uri($"{this.baseUrl}/{request.RequestUri}");
            }

            if (!request.Headers.Contains(ClientVersionHeader))
            {
                request.Headers.Add(ClientVersionHeader, ClientVersion);
            }

            var response = await this.httpClient
                .SendAndDeserializeAsync<T>(request, cancellationToken)
                .ConfigureAwait(false);
            return response.Result;
        }

        internal async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await this.httpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}