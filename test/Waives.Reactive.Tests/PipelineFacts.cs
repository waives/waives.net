using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Waives.Http.Responses;
using Waives.Reactive.HttpAdapters;
using Xunit;

namespace Waives.Reactive.Tests
{
    public class PipelineFacts
    {
        private readonly IHttpDocumentFactory _documentFactory = Substitute.For<IHttpDocumentFactory>();
        private readonly Pipeline _sut;

        public PipelineFacts()
        {
            var httpDocument = Substitute.For<IHttpDocument>();
            httpDocument.Classify(Arg.Any<string>()).Returns(new ClassificationResult());
            _documentFactory
                .CreateDocument(Arg.Any<Document>())
                .Returns(httpDocument);

            _sut = new Pipeline(_documentFactory);
        }

        [Fact]
        public void OnPipelineCompleted_is_run_at_the_end_of_a_successful_pipeline()
        {
            var pipelineCompleted = false;
            _sut.OnPipelineCompleted(() => pipelineCompleted = true);

            _sut.Start();

            Assert.True(pipelineCompleted);
        }

        [Fact]
        public void WithDocumentsFrom_projects_the_document_source_into_the_pipeline()
        {
            var source = Observable.Repeat<Document>(new TestDocument(Generate.Bytes()), 3);
            var pipeline = _sut.WithDocumentsFrom(source);

            source.Zip(pipeline, (s, p) => (s, p.Source)).Subscribe(t =>
            {
                var (expected, actual) = t;
                Assert.Same(expected, actual);
            });
        }

        [Fact]
        public void WithDocumentsFrom_creates_each_document_with_Waives()
        {
            var source = Observable.Repeat(new TestDocument(Generate.Bytes()), 1);

            var pipeline = _sut.WithDocumentsFrom(source);

            pipeline.Subscribe(t =>
            {
                _documentFactory.Received(1).CreateDocument(t.Source);
            });
        }

        [Fact]
        public void ClassifyWith_classifies_each_document_with_Waives()
        {
            var source = Observable.Repeat(new TestDocument(Generate.Bytes()), 1);
            var classifierName = Generate.String();
            var pipeline = _sut
                .WithDocumentsFrom(source)
                .ClassifyWith(classifierName);

            pipeline.Subscribe(t =>
            {
                t.HttpDocument.Received(1).Classify(classifierName);
                Assert.NotNull(t.ClassificationResults);
            });
        }

        [Fact]
        public void Then_invokes_the_supplied_Action()
        {
            var source = Observable.Repeat(new TestDocument(Generate.Bytes()), 1);
            var actionInvoked = false;
            var pipeline = _sut.WithDocumentsFrom(source)
                .Then(d => actionInvoked = true);

            pipeline.Start();

            Assert.True(actionInvoked);
        }

        [Fact]
        public void Then_invokes_the_supplied_async_Action()
        {
            var source = Observable.Repeat(new TestDocument(Generate.Bytes()), 1);
            var actionInvoked = false;
            var pipeline = _sut.WithDocumentsFrom(source)
                .Then(async d => await Task.Run(() => actionInvoked = true));

            pipeline.Start();

            Assert.True(actionInvoked);
        }

        [Fact]
        public void Then_invokes_the_supplied_Func()
        {
            var source = Observable.Repeat(new TestDocument(Generate.Bytes()), 1);
            var actionInvoked = false;
            var pipeline = _sut.WithDocumentsFrom(source)
                .Then(async d =>
                {
                    await Task.Run(() => actionInvoked = true);
                    return d;
                });

            pipeline.Start();

            Assert.True(actionInvoked);
        }
    }
}
