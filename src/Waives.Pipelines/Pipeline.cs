﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Waives.Http.Logging;
using Waives.Pipelines.HttpAdapters;

namespace Waives.Pipelines
{
    /// <summary>
    /// Configures a new document-processing pipeline.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// using System;
    /// using System.Threading.Tasks;
    /// using Waives.Pipelines;
    /// using Waives.Pipelines.Extensions.DocumentSources.FileSystem
    ///
    /// namespace Waives.Pipelines.Example
    /// {
    ///     public class Program
    ///     {
    ///         public static void Main(string[] args)
    ///         {
    ///             Task.Run(() => MainAsync(args)).Wait();
    ///         }
    ///
    ///         public static async Task MainAsync(string[] args)
    ///         {
    ///             var pipeline = WaivesApi.CreatePipeline(new WaivesOptions
    ///             {
    ///                 ClientId = "clientId",
    ///                 ClientSecret = "clientSecret"
    ///             });
    ///
    ///             pipeline
    ///                 .WithDocumentsFrom(FileSystemSource.Create(@"C:\temp\inbox"))
    ///                 .ClassifyWith("mortgages")
    ///                 .Then(d => Console.WriteLine(d.ClassificationResults.DocumentType))
    ///                 .OnPipelineCompeleted(_ => Console.WriteLine("Processing complete!"));
    ///
    ///             try
    ///             {
    ///                 await pipeline.RunAsync();
    ///             }
    ///             catch (WaivesException ex)
    ///             {
    ///                 Console.WriteLine($"Pipeline processing failed: {ex.InnerException.Message}");
    ///             }
    ///         }
    ///     }
    /// }
    /// ]]>
    /// </example>
    public class Pipeline
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        private readonly IHttpDocumentFactory _documentFactory;
        private readonly int _maxConcurrency;
        private IObservable<Document> _documentSource = Observable.Empty<Document>();
        private Action _onPipelineCompletedUserAction = () => { };
        private readonly Action<DocumentError> _onDocumentError;
        private Action<DocumentError> _userErrorAction = err => { };

        private readonly List<Func<WaivesDocument, CancellationToken, Task<WaivesDocument>>> _documentActions =
            new List<Func<WaivesDocument, CancellationToken, Task<WaivesDocument>>>();

        internal Pipeline(IHttpDocumentFactory documentFactory, int maxConcurrency)
        {
            _documentFactory = documentFactory;
            _maxConcurrency = maxConcurrency;

            _onDocumentError = err =>
            {
                Logger.ErrorException(
                    "An error occurred during processing of document '{DocumentId}'",
                    err.Exception,
                    err.Document.SourceId);

                _userErrorAction(err);
            };
        }

        /// <summary>
        /// Add the source of documents to the pipeline. This represents the start
        /// of the pipeline
        /// </summary>
        /// <param name="documentSource"></param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline WithDocumentsFrom(IObservable<Document> documentSource)
        {
            _documentSource = documentSource ?? throw new ArgumentNullException(nameof(documentSource));

            return this;
        }

        /// <summary>
        /// Classify a document with the specified classifier, optionally only if the specified filter returns True
        /// </summary>
        /// <param name="classifierName"></param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline ClassifyWith(string classifierName)
        {
            if (string.IsNullOrWhiteSpace(classifierName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.",
                    nameof(classifierName));
            }

            _documentActions.Add(async (d, ct) =>
            {
                var document = await d.ClassifyAsync(classifierName, ct)
                    .ConfigureAwait(false);

                Logger.Info(
                    "Classified document {DocumentId} from '{DocumentSource}'",
                    d.Id,
                    d.Source.SourceId);
                return document;
            });
            return this;
        }

        /// <summary>
        /// Extract data from documents using the specified extractor. The extractor must have
        /// been created previously in your Waives account in order to be used here.
        /// </summary>
        /// <param name="extractorName">The name of the extractor to use.</param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline ExtractWith(string extractorName)
        {
            if (string.IsNullOrWhiteSpace(extractorName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.",
                    nameof(extractorName));
            }

            _documentActions.Add(async (d, ct) =>
            {
                var document = await d.ExtractAsync(extractorName, ct).ConfigureAwait(false);
                Logger.Info(
                    "Extracted data from document {DocumentId} from '{DocumentSource}'",
                    d.Id,
                    d.Source.SourceId);
                return document;
            });

            return this;
        }

        /// <summary>
        /// Redacts data from documents, based on extraction results provided by the specified extractor. The extractor
        /// must have been created previously in your Waives account in order to be used here.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The result of a redaction operation is the original document, converted to a PDF file if required, and with
        /// the extraction results removed from the electronic content of the file. Additionally, the content is removed
        /// from the document image, replaced with a black bar. This PDF document is returned as a <see cref="Stream"/>
        /// from the Waives API, and so a callback must be supplied accepting this <see cref="Stream"/>.
        /// </para>
        /// <para>
        /// The redaction functionality is currently a beta-stage feature of the Waives API and has a couple of known
        /// issues.
        /// </para>
        /// </remarks>
        /// <example>
        /// <![CDATA[
        /// using System;
        /// using System.Threading.Tasks;
        /// using Waives.Pipelines;
        /// using Waives.Pipelines.Extensions.DocumentSources.FileSystem
        ///
        /// namespace Waives.Pipelines.Example
        /// {
        ///     public class Program
        ///     {
        ///         public static void Main(string[] args)
        ///         {
        ///             Task.Run(() => MainAsync(args)).Wait();
        ///         }
        ///
        ///         public static async Task MainAsync(string[] args)
        ///         {
        ///             var pipeline = WaivesApi.CreatePipeline(new WaivesOptions
        ///             {
        ///                 ClientId = "clientId",
        ///                 ClientSecret = "clientSecret"
        ///             });
        ///
        ///             pipeline
        ///                 .WithDocumentsFrom(FileSystemSource.Create(@"C:\temp\inbox"))
        ///                 .RedactWith("my-extractor", async (d, redactedPdf) =>
        ///                 {
        ///                     var redactedFilePath = $"{d.Source.SourceId}.redacted.pdf";
        ///                     using (var fileStream = File.OpenWrite(redactedFilePath))
        ///                     {
        ///                         await redactedPdf.CopyTo(fileStream);
        ///                     }
        ///                 });
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </example>
        /// <param name="extractorName"></param>
        /// <param name="resultFunc"></param>
        /// <returns></returns>
        public Pipeline RedactWith(string extractorName, Func<WaivesDocument, Stream, Task> resultFunc)
        {
            _documentActions.Add(async (d, ct) =>
            {
                var document = await d.RedactAsync(extractorName, resultFunc, ct).ConfigureAwait(false);
                Logger.Info(
                    "Redacted data from document {DocumentId} from '{DocumentSource}' using extractor '{ExtractorName}'",
                    d.Id,
                    d.Source.SourceId,
                    extractorName);
                return document;
            });

            return this;
        }

        /// <summary>
        /// Run an arbitrary action on each document, doing something with results for example
        /// </summary>
        /// <param name="action"></param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline Then(Action<WaivesDocument> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _documentActions.Add((document, cancellationToken) =>
            {
                action(document);
                return Task.FromResult(document);
            });

            return this;
        }

        /// <summary>
        /// Run an arbitrary action on each document, doing something with results for example
        /// </summary>
        /// <param name="action"></param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline Then(Func<WaivesDocument, Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _documentActions.Add(async (d, ct) =>
            {
                await action(d).ConfigureAwait(false);
                return d;
            });

            return this;
        }

        /// <summary>
        /// Run an arbitrary action on each document, doing something with results for example
        /// </summary>
        /// <param name="action"></param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline Then(Func<WaivesDocument, Task<WaivesDocument>> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _documentActions.Add(async (d, ct) => await action(d).ConfigureAwait(false));

            return this;
        }

        /// <summary>
        /// Run an arbitrary action when all documents have been processed.
        /// </summary>
        /// <param name="action">The action to execute when the pipeline completes.</param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline OnPipelineCompleted(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _onPipelineCompletedUserAction = action;
            return this;
        }

        /// <summary>
        /// Run an arbitrary action when a document has an error during processing.
        /// </summary>
        /// <param name="action">The action to execute when document has an error.</param>
        /// <returns>The modified <see cref="Pipeline"/>.</returns>
        public Pipeline OnDocumentError(Action<DocumentError> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var previousAction = _userErrorAction;
            _userErrorAction = err =>
            {
                previousAction(err);
                action(err);
            };

            return this;
        }


        /// <summary>
        /// Start processing the documents in the pipeline.
        /// </summary>
        /// <param name="cancellationToken">
        /// The token to monitor for cancellation requests. The default value is
        /// <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="Task"/> which completes when processing of all the documents
        /// in the pipeline is complete.
        /// </returns>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var taskCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void OnPipelineComplete()
            {
                try
                {
                    _onPipelineCompletedUserAction();
                    Logger.Info("Pipeline complete");
                    taskCompletion.SetResult(true);
                }
                catch (Exception e)
                {
                    taskCompletion.TrySetException(e);
                }
            }

            void OnPipelineError(Exception e)
            {
                Logger.Error(e, "An error occurred processing the pipeline");

                taskCompletion.TrySetException(e);
            }

            void OnDocumentException(Exception exception, Document document)
            {
                try
                {
                    _onDocumentError(new DocumentError(document, exception));
                }
                catch (Exception e)
                {
                    Logger.Error(e, "An error occurred when calling the error handler");

                    taskCompletion.TrySetException(e);
                }
            }

            async Task<WaivesDocument> CreateDocument(Document d, CancellationToken ct)
            {
                Logger.Info("Started processing '{DocumentSourceId}'", d.SourceId);

                var httpDocument = await _documentFactory.CreateDocumentAsync(d, ct).ConfigureAwait(false);

                return new WaivesDocument(d, httpDocument);
            }

            async Task DeleteDocument(WaivesDocument d)
            {
                try
                {
                    await d.HttpDocument.DeleteAsync().ConfigureAwait(false);

                    Logger.Info(
                        "Deleted document {DocumentId}. Processing of '{DocumentSourceId}' complete.",
                        d.Id,
                        d.Source.SourceId);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "An error occurred when deleting '{DocumentId}''", d.Id);

                    taskCompletion.TrySetException(e);
                }
            }

            Logger.Info("Pipeline started");

            var documentProcessor = new DocumentProcessor(
                CreateDocument,
                _documentActions,
                DeleteDocument,
                OnDocumentException);

            var pipelineObserver = new ConcurrentPipelineObserver(
                documentProcessor,
                OnPipelineComplete,
                OnPipelineError,
                _maxConcurrency,
                cancellationToken);

            var connection = _documentSource.Subscribe(pipelineObserver);
            try
            {
                await taskCompletion.Task;
            }
            finally
            {
                connection.Dispose();
            }
        }
    }
}