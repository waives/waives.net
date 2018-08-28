﻿using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit.Sdk;

namespace Waives.Http.Tests
{
    public static class Responses
    {
        public static HttpResponseMessage Success()
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        public const string ErrorMessage = "The error message";

        public static HttpResponseMessage ErrorWithMessage()
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(ErrorResponse)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                }
            };
        }

        public static HttpResponseMessage CreateDocument()
        {
            return
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateDocumentResponse)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                    }
                };
        }

        public static HttpResponseMessage GetAllDocuments()
        {
            return
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetAllDocumentsResponse)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                    }
                };
        }

        public static HttpResponseMessage GetToken()
        {
            return
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetTokenResponse)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                    },
                };
        }

        public static HttpResponseMessage GetReadResults(string content)
        {
            return
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("text/plain") }
                    },
                };
        }

        public static HttpResponseMessage Classify()
        {
            return
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ClassifyResponse)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
                    },
                };
        }

        private const string ClassifyResponse = @"{
	        ""document_id"": ""expectedDocumentId"",
            ""classification_results"": {
                ""document_type"": ""expectedDocumentType"",
                ""relative_confidence"": 2.85512137,
                ""is_confident"": true,
                ""document_type_scores"": [
                {
                    ""document_type"": ""Assignment of Deed of Trust"",
                    ""score"": 61.4187

                },
                {
                    ""document_type"": ""Notice of Default"",
                    ""score"": 32.94312
                },
                {
                    ""document_type"": ""Correspondence"",
                    ""score"": 28.2860489
                },
                {
                    ""document_type"": ""Deed of Trust"",
                    ""score"": 28.0011711
                },
                {
                    ""document_type"": ""Notice of Lien"",
                    ""score"": 27.9561481
                }
                ]
            }
        }";

        private const string GetTokenResponse = @"{
	        ""access_token"": ""token"",
	        ""token_type"": ""Bearer"",
	        ""expires_in"": 86400}";

        private const string CreateDocumentResponse = @"{
            ""id"": ""expectedDocumentId"",
            ""_links"": {
                ""document:read"": {
                    ""href"": ""/documents/LAHV1hoYikqukLpuhiFpAw/reads""
                },
                ""document:classify"": {
                    ""href"": ""/documents/LAHV1hoYikqukLpuhiFpAw/classify/{classifier_name}"",
                    ""templated"": true
                },
                ""self"": {
                    ""href"": ""/documents/LAHV1hoYikqukLpuhiFpAw""
                }
            },
            ""_embedded"": {
                ""files"": [
                {
                    ""id"": ""HEE7UnX680y7yecR-yXsPA"",
                    ""file_type"": ""Image:TIFF"",
                    ""size"": 41203,
                    ""sha256"": ""eeea8dbbf4f0da70bf3dcc25ee0ecf5c6f8a4eae2817fe782a59589cbd4cb9fd""
                }]
            }
        }";

        private const string GetAllDocumentsResponse = @"{
	        ""documents"": [
              {
                ""id"": ""expectedDocumentId1"",
                ""_links"": {
                    ""document:read"": {
                        ""href"": ""/documents/expectedDocumentId1/reads""
                    },
                    ""document:classify"": {
                        ""href"": ""/documents/expectedDocumentId1/classify/{classifier_name}"",
                        ""templated"": true
                    },
                    ""self"": {
                        ""href"": ""/documents/expectedDocumentId1""
                    }
                },
                ""_embedded"": {
                    ""files"": [
                    {
                        ""id"": ""HEE7UnX680y7yecR-yXsPA"",
                        ""file_type"": ""Image:TIFF"",
                        ""size"": 41203,
                        ""sha256"": ""eeea8dbbf4f0da70bf3dcc25ee0ecf5c6f8a4eae2817fe782a59589cbd4cb9fd""

                    }
                    ]
                 }
               },
               {
                 ""id"": ""expectedDocumentId2"",
                 ""_links"": {
                    ""document:read"": {
                        ""href"": ""/documents/expectedDocumentId2/reads""
                    },
                    ""document:classify"": {
                        ""href"": ""/documents/expectedDocumentId2/classify/{classifier_name}"",
                        ""templated"": true
                    },
                    ""self"": {
                        ""href"": ""/documents/expectedDocumentId2""
                    }
                 },
                 ""_embedded"": {
                    ""files"": [
                    {
                        ""id"": ""YY-WZbHuukCOXMalCZ3rBA"",
                        ""file_type"": ""Image:TIFF"",
                        ""size"": 41203,
                        ""sha256"": ""eeea8dbbf4f0da70bf3dcc25ee0ecf5c6f8a4eae2817fe782a59589cbd4cb9fd""
                    }
                    ]
                }
            }
            ]
        }";

        private const string ErrorResponse = @"{
	        ""message"": """ + ErrorMessage + "\"}";
    }
}