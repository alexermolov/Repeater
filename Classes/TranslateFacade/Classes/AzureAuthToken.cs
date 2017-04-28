﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Repeater.Classes.TranslateFacade.Classes
{
    public class AzureAuthToken
    {
        /// Name of header used to pass the subscription key to the token service
        private const string OcpApimSubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";

        /// URL of the token service
        private static readonly Uri ServiceUrl = new Uri("https://api.cognitive.microsoft.com/sts/v1.0/issueToken");

        /// After obtaining a valid token, this class will cache it for this duration.
        /// Use a duration of 5 minutes, which is less than the actual token lifetime of 10 minutes.
        private static readonly TimeSpan TokenCacheDuration = new TimeSpan(0, 5, 0);

        /// When the last valid token was obtained.
        private DateTime _storedTokenTime = DateTime.MinValue;

        /// Cache the value of the last valid token obtained from the token service.
        private string _storedTokenValue = string.Empty;

        /// <summary>
        ///     Creates a client to obtain an access token.
        /// </summary>
        /// <param name="key">Subscription key to use to get an authentication token.</param>
        public AzureAuthToken(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key), "A subscription key is required");

            SubscriptionKey = key;
            RequestStatusCode = HttpStatusCode.InternalServerError;
        }

        /// Gets the subscription key.
        public string SubscriptionKey { get; }

        /// Gets the HTTP status code for the most recent request to the token service.
        public HttpStatusCode RequestStatusCode { get; private set; }

        /// <summary>
        ///     Gets a token for the specified subscription.
        /// </summary>
        /// <returns>The encoded JWT token prefixed with the string "Bearer ".</returns>
        /// <remarks>
        ///     This method uses a cache to limit the number of request to the token service.
        ///     A fresh token can be re-used during its lifetime of 10 minutes. After a successful
        ///     request to the token service, this method caches the access token. Subsequent
        ///     invocations of the method return the cached token for the next 5 minutes. After
        ///     5 minutes, a new token is fetched from the token service and the cache is updated.
        /// </remarks>
        public async Task<string> GetAccessTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(SubscriptionKey))
                return string.Empty;

            // Re-use the cached token if there is one.
            if (DateTime.Now - _storedTokenTime < TokenCacheDuration)
                return _storedTokenValue;

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = ServiceUrl;
                request.Content = new StringContent(string.Empty);
                request.Headers.TryAddWithoutValidation(OcpApimSubscriptionKeyHeader, SubscriptionKey);
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.SendAsync(request);
                RequestStatusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();
                var token = await response.Content.ReadAsStringAsync();
                _storedTokenTime = DateTime.Now;
                _storedTokenValue = "Bearer " + token;
                return _storedTokenValue;
            }
        }

        /// <summary>
        ///     Gets a token for the specified subscription. Synchronous version.
        ///     Use of async version preferred
        /// </summary>
        /// <returns>The encoded JWT token prefixed with the string "Bearer ".</returns>
        /// <remarks>
        ///     This method uses a cache to limit the number of request to the token service.
        ///     A fresh token can be re-used during its lifetime of 10 minutes. After a successful
        ///     request to the token service, this method caches the access token. Subsequent
        ///     invocations of the method return the cached token for the next 5 minutes. After
        ///     5 minutes, a new token is fetched from the token service and the cache is updated.
        /// </remarks>
        public string GetAccessToken()
        {
            // Re-use the cached token if there is one.
            if (DateTime.Now - _storedTokenTime < TokenCacheDuration)
                return _storedTokenValue;

            string accessToken = null;
            var task = Task.Run(async () => { accessToken = await GetAccessTokenAsync(); });

            while (!task.IsCompleted)
            {
                Thread.Yield();
            }
            if (task.IsFaulted)
                throw task.Exception;
            if (task.IsCanceled)
                throw new Exception("Timeout obtaining access token.");
            return accessToken;
        }
    }
}