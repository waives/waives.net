﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Waives.Http.RequestHandling
{
    internal class RequestSender : IHttpRequestSender
    {
        private readonly HttpClient _httpClient;

        internal RequestSender(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // This is equivalent to the value used by NuGet
            var productVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Waives.NET", productVersion));

            Timeout = 120;
        }

        public int Timeout
        {
            get => _httpClient.Timeout.Seconds;
            set => _httpClient.Timeout = TimeSpan.FromSeconds(value);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessageTemplate template, CancellationToken cancellationToken = default)
        {
            var request = template.CreateRequest();
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}