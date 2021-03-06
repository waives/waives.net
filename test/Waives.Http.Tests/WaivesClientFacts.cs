﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Waives.Http.RequestHandling;
using Waives.Http.Requests;
using Waives.Http.Tests.RequestHandling;
using Xunit;

namespace Waives.Http.Tests
{
    public class WaivesClientFacts
    {
        private readonly IHttpRequestSender _requestSender;
        private readonly WaivesClient _sut;
        private readonly byte[] _documentContents;

        public WaivesClientFacts()
        {
            _requestSender = Substitute.For<IHttpRequestSender>();
            _sut = new WaivesClient(_requestSender);

            _documentContents = new byte[] { 0, 1, 2 };
        }

        [Fact]
        public async Task CreateDocument_sends_a_request_to_correct_url()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.CreateDocument(ci.Arg<HttpRequestMessageTemplate>()));

            using (var stream = new MemoryStream(_documentContents))
            {
                await _sut.CreateDocumentAsync(stream);
            }

            await _requestSender
                .Received(1)
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Post &&
                    m.RequestUri.ToString() == "/documents"));
        }

        [Fact]
        public async Task CreateDocument_sends_the_supplied_stream_as_content()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.CreateDocument(ci.Arg<HttpRequestMessageTemplate>()));

            using (var stream = new MemoryStream(_documentContents))
            {
                await _sut.CreateDocumentAsync(stream);

                await _requestSender
                    .Received(1)
                    .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                        RequestContentEquals(m, _documentContents)));
            }
        }

        [Fact]
        public async Task CreateDocument_sends_the_supplied_unseekable_stream_as_content()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.CreateDocument(ci.Arg<HttpRequestMessageTemplate>()));

            using (var stream = new UnseekableMemoryStream(_documentContents))
            {
                await _sut.CreateDocumentAsync(stream);

                await _requestSender
                    .Received(1)
                    .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                        RequestContentEquals(m, _documentContents)));
            }
        }

        [Fact]
        public async Task CreateDocument_sends_the_supplied_file_as_content()
        {
            const string filePath = "DummyDocument.txt";

            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.CreateDocument(ci.Arg<HttpRequestMessageTemplate>()));

            _requestSender
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m => m.RequestUri.ToString().Contains("/classify")))
                .Returns(ci => Response.Classify(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.CreateDocumentAsync(filePath);

            await _requestSender
                .Received(1)
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                    RequestContentEquals(m, File.ReadAllBytes(filePath))));
        }

        [Fact]
        public async Task CreateDocument_sends_a_request_containing_the_supplied_file_url()
        {
            var fileUri = new Uri("https://myfileserver.com/mydocument.pdf");

            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.CreateDocument(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.CreateDocumentAsync(fileUri);

            var expectedJsonContent = new JsonContent(new ImportDocumentRequest
            {
                Url = fileUri
            });

            var expectedContents = await expectedJsonContent.ReadAsByteArrayAsync();

            await _requestSender
                .Received(1)
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                    RequestContentEquals(m, expectedContents) &&
                    m.Method == HttpMethod.Post &&
                    m.Content.Headers.ContentType.MediaType == "application/json"));
        }

        [Fact]
        public async Task CreateDocument_returns_a_document_that_can_be_used()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.CreateDocument(ci.Arg<HttpRequestMessageTemplate>()));

            using (var stream = new MemoryStream(_documentContents))
            {
                var document = await _sut.CreateDocumentAsync(stream);

                Assert.Equal("expectedDocumentId", document.Id);

                _requestSender
                    .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                    .Returns(ci => Response.Classify(ci.Arg<HttpRequestMessageTemplate>()));

                await document.ClassifyAsync("classifier");

                _requestSender
                    .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                    .Returns(ci => Response.Success(ci.Arg<HttpRequestMessageTemplate>()));

                await document.DeleteAsync();
            }
        }

        [Fact]
        public async Task CreateDocument_throws_if_response_is_not_success_code()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(Response.ErrorMessage));

            using (var stream = new MemoryStream(_documentContents))
            {
                var exception = await Assert.ThrowsAsync<WaivesApiException>(() =>
                    _sut.CreateDocumentAsync(stream));

                Assert.Equal(Response.ErrorMessage, exception.Message);
            }
        }

        [Fact]
        public async Task CreateDocument_throws_if_stream_is_empty()
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateDocumentAsync(Stream.Null));
            Assert.Contains("The provided stream has no content.", exception.Message);
            Assert.Equal("documentSource", exception.ParamName);
        }

        [Fact]
        public async Task CreateDocument_throws_if_stream_is_null()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateDocumentAsync((Stream)null));
            Assert.Equal("documentSource", exception.ParamName);
        }

        [Fact]
        public async Task CreateDocument_throws_if_unseekable_stream_is_empty()
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateDocumentAsync(new UnseekableMemoryStream()));
            Assert.Contains("The provided stream has no content.", exception.Message);
            Assert.Equal("documentSource", exception.ParamName);
        }

        [Fact]
        public async Task GetDocument_sends_a_request_to_the_correct_url()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.GetDocument(ci.Arg<HttpRequestMessageTemplate>()));

            var documentId = $"anonymousString{Guid.NewGuid()}";
            await _sut.GetDocumentAsync(documentId);

            await _requestSender
                .Received(1)
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Get &&
                    m.RequestUri.ToString() == $"/documents/{documentId}"));
        }

        [Fact]
        public async Task GetDocument_returns_the_requested_document()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.GetDocument(ci.Arg<HttpRequestMessageTemplate>()));

            var document = await _sut.GetDocumentAsync($"anonymousString{Guid.NewGuid()}");

            Assert.Equal("expectedDocumentId1", document.Id);
        }

        [Fact]
        public async Task GetDocument_throws_if_response_is_not_success_code()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(Response.ErrorMessage));

            var exception = await Assert.ThrowsAsync<WaivesApiException>(() =>
                _sut.GetDocumentAsync($"anonymousString{Guid.NewGuid()}"));

            Assert.Equal(Response.ErrorMessage, exception.Message);
        }

        [Fact]
        public async Task GetAllDocuments_sends_a_request_to_correct_url()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.GetAllDocuments(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.GetAllDocumentsAsync();

            await _requestSender
                .Received(1)
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Get &&
                    m.RequestUri.ToString() == "/documents"));
        }

        [Fact]
        public async Task GetAllDocuments_returns_one_document_for_each_returned_by_the_API()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.GetAllDocuments(ci.Arg<HttpRequestMessageTemplate>()));

            var documents = await _sut.GetAllDocumentsAsync();

            var documentsArray = documents.ToArray();
            Assert.Equal("expectedDocumentId1", documentsArray.First().Id);
            Assert.Equal("expectedDocumentId2", documentsArray.Last().Id);
        }

        [Fact]
        public async Task GetAllDocuments_throws_if_response_is_not_success_code()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(Response.ErrorMessage));

            var exception = await Assert.ThrowsAsync<WaivesApiException>(() =>
                _sut.GetAllDocumentsAsync());

            Assert.Equal(Response.ErrorMessage, exception.Message);
        }

        private static bool RequestContentEquals(HttpRequestMessageTemplate request, byte[] expectedContents)
        {
            var actualRequestContents = request.Content.ReadAsByteArrayAsync().Result;

            return actualRequestContents.SequenceEqual(expectedContents);
        }

        private class UnseekableMemoryStream : Stream
        {
            private const string NotSupportedMessage = "The stream does not support seeking.";
            private readonly MemoryStream _stream = new MemoryStream();

            public UnseekableMemoryStream()
            {
            }

            public UnseekableMemoryStream(byte[] contents)
            {
                _stream = new MemoryStream(contents);
            }

            public override void Flush()
            {
                _stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _stream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException(NotSupportedMessage);
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException(NotSupportedMessage);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _stream.Write(buffer, offset, count);
            }

            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek { get; } = false;

            public override bool CanWrite => _stream.CanWrite;

            public override long Length => throw new NotSupportedException(NotSupportedMessage);

            public override long Position
            {
                get => throw new NotSupportedException(NotSupportedMessage);
                set => throw new NotSupportedException(NotSupportedMessage);
            }
        }
    }
}