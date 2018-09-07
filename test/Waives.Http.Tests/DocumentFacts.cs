﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Waives.Http.RequestHandling;
using Waives.Http.Responses;
using Waives.Http.Tests.RequestHandling;
using Xunit;

namespace Waives.Http.Tests
{
    public class DocumentFacts : IDisposable
    {
        private readonly IHttpRequestSender _requestSender = Substitute.For<IHttpRequestSender>();
        private readonly Document _sut;
        private readonly string _classifyUrl;
        private readonly string _extractUrl;
        private readonly string _selfUrl;
        private readonly string _classifierName;
        private readonly string _extractorName;
        private readonly string _readResultsFilename;

        public DocumentFacts()
        {
            const string documentId = "id";
            _classifierName = "classifier";
            _extractorName = "extractor";

            var readUrl = $"/documents/{documentId}/reads";

            var templatedClassifyUrl = $"/documents/{documentId}/classify/" + "{classifier_name}";
            var templatedExtractUrl = $"/documents/{documentId}/extract/" + "{extractor_name}";

            _classifyUrl = $"/documents/{documentId}/classify/{_classifierName}";
            _extractUrl = $"/documents/{documentId}/extract/{_extractorName}";
            _selfUrl = $"/documents/{documentId}";

            IDictionary<string, HalUri> behaviours = new Dictionary<string, HalUri>
            {
                { "document:read", new HalUri(new Uri(readUrl, UriKind.Relative), false) },
                { "document:classify", new HalUri(new Uri(templatedClassifyUrl, UriKind.Relative), true) },
                { "document:extract", new HalUri(new Uri(templatedExtractUrl, UriKind.Relative), true) },
                { "self", new HalUri(new Uri(_selfUrl, UriKind.Relative), false) }
            };

            _sut = new Document(_requestSender, "id", behaviours);

            _readResultsFilename = Path.GetTempFileName();
        }

        [Fact]
        public async Task Delete_sends_request_with_correct_url()
        {
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.Success(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.Delete();

            await _requestSender
                .Received(1)
                .Send(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Delete &&
                    m.RequestUri.ToString() == _selfUrl));
        }

        [Fact]
        public async Task Delete_throws_if_response_is_not_success_code()
        {
            var exceptionMessage = $"Anonymous string {Guid.NewGuid()}";
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(exceptionMessage));

            var exception = await Assert.ThrowsAsync<WaivesApiException>(() => _sut.Delete());
            Assert.Equal(exceptionMessage, exception.Message);
        }


        [Fact]
        public async Task Classify_sends_request_with_correct_url()
        {
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.Classify(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.Classify(_classifierName);

            await _requestSender
                .Received(1)
                .Send(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri.ToString() == _classifyUrl));
        }

        [Fact]
        public async Task Classify_returns_a_result_with_correct_properties_set()
        {
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.Classify(ci.Arg<HttpRequestMessageTemplate>()));

            var result = await _sut.Classify(_classifierName);

            Assert.Equal("expectedDocumentType", result.DocumentType);
            Assert.Equal(2.85512137M, result.RelativeConfidence);
            Assert.True(result.IsConfident);
            Assert.Equal(5, result.DocumentTypeScores.Count());
            Assert.Equal("Assignment of Deed of Trust", result.DocumentTypeScores.First().DocumentType);
            Assert.Equal(61.4187M, result.DocumentTypeScores.First().Score);
        }

        [Fact]
        public async Task Classify_throws_if_response_is_not_success_code()
        {
            var exceptionMessage = $"Anonymous string {Guid.NewGuid()}";
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(exceptionMessage));

            var exception = await Assert.ThrowsAsync<WaivesApiException>(() => _sut.Classify(_classifierName));
            Assert.Equal(exceptionMessage, exception.Message);
        }

        [Fact]
        public async Task Extract_sends_request_with_correct_uri()
        {
            var exceptionMessage = $"Anonymous string {Guid.NewGuid()}";
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.Extract(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.Extract(_extractorName);

            await _requestSender
                .Received(1)
                .Send(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri.ToString() == _extractUrl));
        }

        [Fact]
        public async Task Extract_returns_a_result_with_correct_properties_set()
        {
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.Extract(ci.Arg<HttpRequestMessageTemplate>()));

            var response = await _sut.Extract(_extractorName);


            var extractionPage = response.DocumentMetadata.Pages.First();

            Assert.Equal(1, response.DocumentMetadata.PageCount);
            Assert.Equal(1, extractionPage.PageNumber);
            Assert.Equal(611.0, extractionPage.Width);
            Assert.Equal(1008.0, extractionPage.Height);

            var fieldResult = response.FieldResults.First();
            Assert.Equal("Amount", fieldResult.FieldName);
            Assert.False(fieldResult.Rejected);
            Assert.Equal("None", fieldResult.RejectReason);

            var primaryResult = fieldResult.Result;
            Assert.Equal("$5.50", primaryResult.Text);
            Assert.Equal(100.0, primaryResult.ProximityScore);
            Assert.Equal(100.0, primaryResult.MatchScore);
            Assert.Equal(100.0, primaryResult.TextScore);

            var primaryResultArea = primaryResult.Areas.First();
            Assert.Equal(558.7115, primaryResultArea.Top);
            Assert.Equal(276.48, primaryResultArea.Left);
            Assert.Equal(571.1989, primaryResultArea.Bottom);
            Assert.Equal(298.58, primaryResultArea.Right);
            Assert.Equal(1, primaryResultArea.PageNumber);

            var firstAlternativeResult = fieldResult.Alternatives.First();
            Assert.Equal("$10.50", firstAlternativeResult.Text);

            var firstAlternativeResultArea = firstAlternativeResult.Areas.First();
            Assert.Equal(123.4567, firstAlternativeResultArea.Top);
        }

        [Fact]
        public async Task Extract_throws_if_response_is_not_success_code()
        {
            var exceptionMessage = $"Anonymous string {Guid.NewGuid()}";
            _requestSender
                .Send(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(exceptionMessage));

            var exception = await Assert.ThrowsAsync<WaivesApiException>(() => _sut.Extract(_extractorName));
            Assert.Equal(exceptionMessage, exception.Message);
        }

        public void Dispose()
        {
            if (File.Exists(_readResultsFilename))
            {
                File.Delete(_readResultsFilename);
            }
        }
    }
}