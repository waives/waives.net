﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Waives.Client.Responses;

namespace Waives.Client
{
    public class Classifier
    {
        private readonly HttpClient _httpClient;
        private readonly IDictionary<string, HalUri> _behaviours;

        internal Classifier(HttpClient httpClient, IDictionary<string, HalUri> behaviours)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _behaviours = behaviours ?? throw new ArgumentNullException(nameof(behaviours));
        }

        public async Task AddSamplesFromZip(string path)
        {
            var streamContent = new StreamContent(File.OpenRead(path));
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            var response = await _httpClient.PostAsync(
                _behaviours["classifier:add_samples_from_zip"].CreateUri(),
                streamContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new WaivesApiException();
            }
        }
    }
}
