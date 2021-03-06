﻿using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Waives.Http.RequestHandling;
using Xunit;

namespace Waives.Http.Tests.RequestHandling
{
    public class AccessTokenServiceFacts
    {
        private const string ExpectedClientId = nameof(ExpectedClientId);
        private const string ExpectedClientSecret = nameof(ExpectedClientSecret);

        private readonly IHttpRequestSender _requestSender = Substitute.For<IHttpRequestSender>();
        private readonly AccessTokenService _sut;

        public AccessTokenServiceFacts()
        {
            _sut = new AccessTokenService(
                ExpectedClientId, ExpectedClientSecret,
                _requestSender);
        }

        [Fact]
        public async Task Login_sends_a_request_with_the_specified_credentials()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Returns(ci => Response.GetToken(ci.Arg<HttpRequestMessageTemplate>()));

            await _sut.FetchAccessTokenAsync();

            await _requestSender
                .Received(1)
                .SendAsync(Arg.Is<HttpRequestMessageTemplate>(m =>
                    m.Method == HttpMethod.Post &&
                    IsFormWithClientCredentials(m.Content, ExpectedClientId, ExpectedClientSecret)));
        }

        [Fact]
        public async Task Login_throws_if_response_is_not_success_code()
        {
            _requestSender
                .SendAsync(Arg.Any<HttpRequestMessageTemplate>())
                .Throws(new WaivesApiException(Response.ErrorMessage));

            var exception = await Assert.ThrowsAsync<WaivesApiException>(() =>
                _sut.FetchAccessTokenAsync());

            Assert.Equal(Response.ErrorMessage, exception.Message);
        }

        private static bool IsFormWithClientCredentials(HttpContent content, string expectedClientId, string expectedClientSecret)
        {
            if (!(content is FormUrlEncodedContent))
            {
                return false;
            }

            var contentStream = content.ReadAsStreamAsync().Result;
            using (var reader = new FormReader(contentStream))
            {
                var formData = reader.ReadFormAsync().Result;

                Assert.Equal(expectedClientId, formData["client_id"]);
                Assert.Equal(expectedClientSecret, formData["client_secret"]);
                return true;
            }
        }
    }
}