using System;
using System.Threading;
using NUnit.Framework;

namespace Joi.H.AppUI.Tests
{
    public sealed class AppUIOperationContractTests
    {
        [Test]
        public void Completion_Succeeded_PreservesTypedResult()
        {
            AppUIOperationCompletion<int> completion =
                AppUIOperationCompletion<int>.Succeeded(7);

            Assert.That(completion.Status,
                Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(completion.Result, Is.EqualTo(7));
            Assert.That(completion.Exception, Is.Null);
        }

        [Test]
        public void Completion_Failed_RejectsMissingException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AppUIOperationCompletion<int>.Failed(null));
        }

        [Test]
        public void Descriptor_Create_NormalizesNameAndPreservesCancellation()
        {
            using (CancellationTokenSource cancellation =
                   new CancellationTokenSource())
            {
                AppUIOperationDescriptor descriptor =
                    AppUIOperationDescriptor.Create(null, cancellation.Token);

                Assert.That(descriptor.Name, Is.EqualTo(string.Empty));
                Assert.That(descriptor.CancellationToken,
                    Is.EqualTo(cancellation.Token));
            }
        }

        [Test]
        public void Source_FirstTerminalWriteWins_AndLateSubscriberSeesSameCompletion()
        {
            ManualUIOperationFactory factory =
                new ManualUIOperationFactory();
            IUIOperationSource<int> source = factory.Create<int>(
                AppUIOperationDescriptor.Create("contract"));
            AppUIOperationCompletion<int> first = default;
            AppUIOperationCompletion<int> late = default;

            source.Operation.Register(value => first = value);
            source.TrySetRunning();

            Assert.That(source.TrySetSucceeded(7), Is.True);
            Assert.That(
                source.TrySetFailed(new InvalidOperationException()),
                Is.False);
            source.Operation.Register(value => late = value);

            Assert.That(first.Status,
                Is.EqualTo(AppUIOperationStatus.Succeeded));
            Assert.That(first.Result, Is.EqualTo(7));
            Assert.That(late.Status, Is.EqualTo(first.Status));
            Assert.That(late.Result, Is.EqualTo(first.Result));
        }

        [Test]
        public void Subscription_Dispose_PreventsOnlyThatCallback()
        {
            ManualUIOperationFactory factory =
                new ManualUIOperationFactory();
            IUIOperationSource<int> source = factory.Create<int>(
                AppUIOperationDescriptor.Create("subscription"));
            int disposedCount = 0;
            int activeCount = 0;
            IDisposable disposed = source.Operation.Register(
                _ => disposedCount++);
            source.Operation.Register(_ => activeCount++);

            disposed.Dispose();
            source.TrySetSucceeded(1);

            Assert.That(disposedCount, Is.Zero);
            Assert.That(activeCount, Is.EqualTo(1));
        }

        [Test]
        public void RequestCancellation_SignalsOperationToken_BeforeTerminalCompletion()
        {
            ManualUIOperationFactory factory =
                new ManualUIOperationFactory();
            IUIOperationSource<int> source = factory.Create<int>(
                AppUIOperationDescriptor.Create("cancel"));

            Assert.That(source.Operation.RequestCancellation(), Is.True);
            Assert.That(
                source.Operation.CancellationToken.IsCancellationRequested,
                Is.True);
            Assert.That(source.Operation.Status,
                Is.EqualTo(AppUIOperationStatus.Cancelling));
            Assert.That(source.TrySetCancelled(), Is.True);
        }
    }
}
