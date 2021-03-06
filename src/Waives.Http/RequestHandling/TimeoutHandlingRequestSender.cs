﻿using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Waives.Http.RequestHandling
{
    internal class TimeoutHandlingRequestSender : IHttpRequestSender
    {
        private readonly IHttpRequestSender _wrappedRequestSender;

        public TimeoutHandlingRequestSender(IHttpRequestSender wrappedRequestSender)
        {
            _wrappedRequestSender = wrappedRequestSender ?? throw new ArgumentNullException(nameof(wrappedRequestSender));
        }

        public int Timeout
        {
            get => _wrappedRequestSender.Timeout;
            set => _wrappedRequestSender.Timeout = value;
        }

        /// <summary>
        /// Send a request to the wrapped IHttpRequestSender and make sure that any exception is transformed
        /// into a WaivesApiException.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessageTemplate request, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _wrappedRequestSender.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is TaskCanceledException || e is OperationCanceledException)
            {
                // Either TaskCanceledException or OperationCanceledException may be thrown by HttpClient if a response
                // is not received before the HttpClient's TimeOut expires
                throw new WaivesApiException($"{request.Method} request to {request.RequestUri} timed-out.");
            }
        }
    }
}