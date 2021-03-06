﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Waives.Pipelines;
using Waives.Pipelines.Extensions.DocumentSources.FileSystem;

namespace FileSorter
{
    public static class Program
    {
        private const string Usage = @"File system sorter sample app.

    This sample application is supplied with the Waives.NET SDK to illustrate how to integrate
    with Waives' classification functionality. It watches the specified directory, classifying
    each file in that directory (including new ones added whilst the app is running) using the
    specified classifier, and moving the files to a subdirectory within the output directory.
    The subdirectory is named for the type of the document Waives identifies in the file.

    Subdirectories of the watched directory are not processed. Documents which cannot be
    processed will be written to the specified failures directory, or by default to a directory
    named ""failures"" within the input directory. A log message is written to the failures
    directory with the file indicating the error encountered in processing that file.

    Usage:
      FileSorter.exe watch <directory> using <classifier> (-o <outdir> | --output=<outdir>) [-f <faildir> | --failures=<faildir>]
      FileSorter.exe (-h | --help)
      FileSorter.exe --version

    Options:
      -h --help                           Show this screen.
      -o <outdir>, --output=<outdir>      The directory into which files will be sorted.
      -f <faildir>, --failures=<faildir>  The directory into which failed documents will be moved.

    ";

        public static async Task Main(string[] args)
        {
            var options = new DocoptNet.Docopt().Apply(Usage, args, version: "File system sorter sample app 1.0", exit: true);

            var inbox = options["<directory>"].ToString();
            var outbox = options["--output"].ToString();
            var errorBox = options["--failures"]?.ToString()
                           ?? Path.Combine(options["<directory>"].ToString(), "failures");

            EnsureDirectoryExists(inbox);
            EnsureDirectoryExists(outbox);
            EnsureDirectoryExists(errorBox);

            var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Cancelling processing...");
                cancellation.Cancel();
            };

            var fileSorter = new FileSorter(outbox, errorBox);

            // Creates an EventingDocumentSource wrapping a FileSystemWatcher emitting
            // a new document into the pipeline whenever a document is created in the
            // inbox path.
            var filesystem = FileSystem.WatchForChanges(inbox, cancellation.Token);

            var pipeline = await WaivesApi.CreatePipelineAsync(new WaivesOptions
            {
                ClientId = "clientId",
                ClientSecret = "clientSecret"
            }, cancellation.Token);

            pipeline.WithDocumentsFrom(filesystem)
                .ClassifyWith(options["<classifier>"].ToString())
                .Then(d => fileSorter.MoveDocument(d))
                .OnDocumentError(e => fileSorter.HandleFailure(e))
                .OnPipelineCompleted(() => Console.WriteLine("Processing completed, press any key to exit."));

            try
            {
                await pipeline.RunAsync(cancellation.Token);
            }
            catch (PipelineException ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
