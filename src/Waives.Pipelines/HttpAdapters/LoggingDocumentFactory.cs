﻿using System.Threading;
using System.Threading.Tasks;
using Waives.Http.Logging;

namespace Waives.Pipelines.HttpAdapters
{
    /// <inheritdoc />
    internal class LoggingDocumentFactory : IHttpDocumentFactory
    {
        private static readonly ILog Logger = LogProvider.For<HttpDocumentFactory>();
        private readonly IHttpDocumentFactory _wrappedDocumentFactory;

        public LoggingDocumentFactory(IHttpDocumentFactory underlyingDocumentFactory)
        {
            _wrappedDocumentFactory = underlyingDocumentFactory;
        }

        public async Task<IHttpDocument> CreateDocumentAsync(Document source, CancellationToken cancellationToken = default)
        {
            var httpDocument = await _wrappedDocumentFactory
                .CreateDocumentAsync(source, cancellationToken)
                .ConfigureAwait(false);

            Logger.Info(
                "Created Waives document {DocumentId} from '{DocumentSourceId}'",
                httpDocument.Id,
                source.SourceId);

            return httpDocument;
        }
    }
}