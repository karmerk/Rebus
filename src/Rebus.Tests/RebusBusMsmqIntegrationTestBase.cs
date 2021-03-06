using System;
using System.Collections.Generic;
using System.Messaging;
using NUnit.Framework;
using Rebus.Bus;
using Rebus.Configuration;
using Rebus.Persistence.InMemory;
using Rebus.Serialization.Json;
using Rebus.Tests.Integration;
using Rebus.Transports.Msmq;
using log4net.Config;

namespace Rebus.Tests
{
    /// <summary>
    /// Test base class with helpers for running integration tests with
    /// <see cref="RebusBus"/> and <see cref="MsmqMessageQueue"/>.
    /// </summary>
    public abstract class RebusBusMsmqIntegrationTestBase : IDetermineDestination
    {
        static RebusBusMsmqIntegrationTestBase()
        {
            XmlConfigurator.Configure();
        }

        List<IDisposable> toDispose;
        
        protected JsonMessageSerializer serializer;
        protected RearrangeHandlersPipelineInspector pipelineInspector = new RearrangeHandlersPipelineInspector();

        [SetUp]
        public void SetUp()
        {
            toDispose = new List<IDisposable>();

            DoSetUp();
        }

        protected virtual void DoSetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                DoTearDown();
            }
            finally
            {
                toDispose.ForEach(b => b.Dispose());
            }
        }

        protected virtual void DoTearDown()
        {
        }

        protected RebusBus CreateBus(string inputQueueName, IActivateHandlers activateHandlers)
        {
            var messageQueue = new MsmqMessageQueue(inputQueueName).PurgeInputQueue();
            serializer = new JsonMessageSerializer();
            var bus = new RebusBus(activateHandlers, messageQueue, messageQueue,
                                   new InMemorySubscriptionStorage(), new SagaDataPersisterForTesting(),
                                   this, serializer, pipelineInspector);
            toDispose.Add(bus);
            toDispose.Add(messageQueue);

            return bus;
        }

        protected string PrivateQueueNamed(string queueName)
        {
            return string.Format(@".\private$\{0}", queueName);
        }

        public virtual string GetEndpointFor(Type messageType)
        {
            throw new AssertionException(string.Format("Cannot route {0}", messageType));
        }

        protected static void EnsureQueueExists(string errorQueueName)
        {
            if (!MessageQueue.Exists(errorQueueName))
            {
                MessageQueue.Create(errorQueueName, transactional: true);
            }
        }
    }
}