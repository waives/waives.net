﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Waives.Http.Logging;
using Waives.Http.Responses;

[assembly: InternalsVisibleTo("Waives.Http.Tests")]
[assembly: InternalsVisibleTo("Waives.Pipelines")]
namespace Waives.Http
{
    public class WaivesClient
    {
        internal ILogger Logger { get; }
        internal HttpClient HttpClient { get; }
        private const string DefaultUrl = "https://api.waives.io";

        public WaivesClient() : this(new HttpClient { BaseAddress = new Uri(DefaultUrl)}, Loggers.NoopLogger)
        { }

        public WaivesClient(ILogger logger) : this(new HttpClient { BaseAddress = new Uri(DefaultUrl) }, logger)
        {
        }

        internal WaivesClient(HttpClient httpClient) : this(httpClient, Loggers.NoopLogger)
        { }

        private WaivesClient(HttpClient httpClient, ILogger logger)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Document> CreateDocument(Stream documentSource)
        {
            var request =
                new HttpRequestMessage(HttpMethod.Post, new Uri($"/documents", UriKind.Relative))
                {
                    Content = new StreamContent(documentSource)
                };

            var response = await SendRequest(request).ConfigureAwait(false);
            await EnsureSuccessStatus(response).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsAsync<HalResponse>().ConfigureAwait(false);
            var id = responseContent.Id;
            var behaviours = responseContent.Links;

            var document = new Document(this, behaviours, id);

            Logger.Log(LogLevel.Trace, $"Created Waives document {id}");
            return document;
        }

        public async Task<Document> CreateDocument(string path)
        {
            var request =
                new HttpRequestMessage(HttpMethod.Post, new Uri($"/documents", UriKind.Relative))
                {
                    Content = new StreamContent(File.OpenRead(path))
                };

            var response = await SendRequest(request).ConfigureAwait(false);
            await EnsureSuccessStatus(response).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsAsync<HalResponse>().ConfigureAwait(false);
            var id = responseContent.Id;
            var behaviours = responseContent.Links;

            return new Document(this, behaviours, id);
        }

        public async Task<Classifier> CreateClassifier(string name, string samplesPath = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/classifiers/{name}", UriKind.Relative));
            var response = await SendRequest(request).ConfigureAwait(false);
            await EnsureSuccessStatus(response).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsAsync<HalResponse>().ConfigureAwait(false);
            var behaviours = responseContent.Links;

            var classifier = new Classifier(this, name, behaviours);

            if (!string.IsNullOrWhiteSpace(samplesPath))
            {
                await classifier.AddSamplesFromZip(samplesPath).ConfigureAwait(false);
            }

            return classifier;
        }

        public async Task<Classifier> GetClassifier(string name)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/classifiers/{name}", UriKind.Relative));
            var response = await SendRequest(request).ConfigureAwait(false);
            await EnsureSuccessStatus(response).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsAsync<HalResponse>().ConfigureAwait(false);
            var behaviours = responseContent.Links;

            return new Classifier(this, name, behaviours);
        }

        public async Task<IEnumerable<Document>> GetAllDocuments()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/documents", UriKind.Relative));
            var response = await SendRequest(request).ConfigureAwait(false);
            await EnsureSuccessStatus(response).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsAsync<DocumentCollection>().ConfigureAwait(false);
            return responseContent.Documents.Select(d => new Document(this, d.Links, d.Id));
        }

        public async Task Login(string clientId, string clientSecret)
        {
            Logger.Log(LogLevel.Info, $"Logging in to Waives at {HttpClient.BaseAddress}");

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/oauth/token", UriKind.Relative))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"client_id", clientId},
                    {"client_secret", clientSecret}
                })
            };

            var response = await SendRequest(request).ConfigureAwait(false);
            await EnsureSuccessStatus(response).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsAsync<AccessToken>().ConfigureAwait(false);
            var accessToken = responseContent.Token;

            HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            Logger.Log(LogLevel.Info, "Logged in.");
        }

        internal async Task<HttpResponseMessage> SendRequest(HttpRequestMessage request)
        {
            var stopWatch = new Stopwatch();
            Logger.Log(LogLevel.Trace, $"Sending {request.Method} request to {request.RequestUri}");

            stopWatch.Start();
            var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            stopWatch.Stop();

            Logger.Log(LogLevel.Trace, $"Received response from {request.Method} {request.RequestUri} ({response.StatusCode}) ({stopWatch.ElapsedMilliseconds} ms)");

            return response;
        }

        private static async Task EnsureSuccessStatus(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var responseContentType = response.Content.Headers.ContentType.MediaType;
            if (responseContentType == "application/json")
            {
                var error = await response.Content.ReadAsAsync<Error>().ConfigureAwait(false);
                throw new WaivesApiException(error.Message);
            }

            throw new WaivesApiException($"Unknown Waives error occured: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }
}