﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading;
using System.Transactions;
using NUnit.Framework;
using Rebus.Messages;
using Rebus.Serialization;
using Rebus.Serialization.Json;
using Rebus.Transports.Msmq;
using Shouldly;
using Message = Rebus.Messages.Message;

namespace Rebus.Tests.Transports.Msmq
{
    [TestFixture]
    public class TestMsmqMessageQueue
    {
        MsmqMessageQueue senderQueue;
        MessageQueue destinationQueue;
        string destinationQueuePath;
        JsonMessageSerializer serializer;
        string destinationQueueName;

        [SetUp]
        public void SetUp()
        {
            serializer = new JsonMessageSerializer();
            senderQueue = new MsmqMessageQueue("test.msmq.tx.sender");
            destinationQueueName = "test.msmq.tx.destination";
            destinationQueuePath = MsmqMessageQueue.PrivateQueue(destinationQueueName);

            if (!MessageQueue.Exists(destinationQueuePath))
            {
                var messageQueue = MessageQueue.Create(destinationQueuePath, transactional: true);
                messageQueue.SetPermissions(Thread.CurrentPrincipal.Identity.Name, MessageQueueAccessRights.FullControl);
            }

            destinationQueue = new MessageQueue(destinationQueuePath)
                                   {
                                       Formatter = new RebusTransportMessageFormatter(),
                                       MessageReadPropertyFilter = RebusTransportMessageFormatter.PropertyFilter,
                                   };

            senderQueue.PurgeInputQueue();
            destinationQueue.Purge();
        }

        [Test]
        public void ThrowsIfExistingQueueIsNotTransactional()
        {
            // arrange
            var queueName = "test.some.random.queue";
            var queuePath = MsmqMessageQueue.PrivateQueue(queueName);

            if (MessageQueue.Exists(queuePath))
            {
                MessageQueue.Delete(queuePath);
            }

            MessageQueue.Create(queuePath, transactional: false);

            // act
            var invalidOperationException = Assert.Throws<InvalidOperationException>(() => new MsmqMessageQueue(queueName));

            // assert
            invalidOperationException.Message.ShouldContain(queueName);
        }

        [Test]
        public void MessageExpirationWorks()
        {
            // arrange
            var timeToBeReceived = 2.Seconds();
            var timeToBeReceivedAsString = timeToBeReceived.ToString();

            senderQueue.Send(destinationQueueName,
                             serializer.Serialize(new Message
                                                      {
                                                          Messages = new object[] { "HELLO WORLD!" },
                                                          Headers = new Dictionary<string, string> { { Headers.TimeToBeReceived, timeToBeReceivedAsString } },
                                                      }));

            // act
            Thread.Sleep(timeToBeReceived + 1.Seconds());

            // assert
            Assert.Throws<MessageQueueException>(() => destinationQueue.Receive(0.1.Seconds()));
        }

        [Test]
        public void MessageIsSentWhenAmbientTransactionIsCommitted()
        {
            using (var tx = new TransactionScope())
            {
                senderQueue.Send(destinationQueueName,
                                 serializer.Serialize(new Message
                                                          {
                                                              Messages = new object[]
                                                                             {
                                                                                 "W00t!"
                                                                             },
                                                          }));

                tx.Complete();
            }

            var msmqMessage = Receive();

            Assert.IsNotNull(msmqMessage, "No message was received within timeout!");
            var transportMessage = (ReceivedTransportMessage)msmqMessage.Body;
            var message = serializer.Deserialize(transportMessage);
            message.Messages[0].ShouldBe("W00t!");
        }

        [Test]
        public void HeadersAreTransferred()
        {
            var headers = new Dictionary<string, string>
                              {
                                  {"someRandomHeaderKey", "someRandomHeaderValue"},
                              };

            senderQueue.Send(destinationQueueName,
                             serializer.Serialize(new Message
                                                      {
                                                          Messages = new object[] {"W00t!"},
                                                          Headers = headers
                                                      }));
            var msmqMessage = Receive();

            Assert.IsNotNull(msmqMessage, "No message was received within timeout!");
            
            var receivedTransportMessage = (ReceivedTransportMessage)msmqMessage.Body;
            receivedTransportMessage.Headers = new DictionarySerializer().Deserialize(Encoding.UTF7.GetString(msmqMessage.Extension));
            var message = serializer.Deserialize(receivedTransportMessage);

            message.Headers.ShouldNotBe(null);
            message.Headers.Count.ShouldBe(1);
            
            var firstHeader = message.Headers.First();
            firstHeader.Key.ShouldBe("someRandomHeaderKey");
            firstHeader.Value.ShouldBe("someRandomHeaderValue");
        }

        [Test]
        public void MessageIsNotSentWhenAmbientTransactionIsNotCommitted()
        {
            using (new TransactionScope())
            {
                senderQueue.Send(destinationQueueName,
                                 serializer.Serialize(new Message
                                                          {
                                                              Messages = new object[]
                                                                             {
                                                                                 "W00t! should not be delivered!"
                                                                             }
                                                          }));

                //< we exit the scope without completing it!
            }

            var transportMessage = Receive();

            if (transportMessage != null)
            {
                Assert.Fail("No messages should have been received! ARGGH: {0}", transportMessage.Body);
            }
        }

        System.Messaging.Message Receive()
        {
            try
            {
                return destinationQueue.Receive(TimeSpan.FromSeconds(5));
            }
            catch (MessageQueueException)
            {
                return null;
            }
        }
    }
}