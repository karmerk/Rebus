﻿using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Rebus.Messages;
using System.Linq;

namespace Rebus.Serialization.Json
{
    public class JsonMessageSerializer : ISerializeMessages
    {
        static readonly JsonSerializerSettings Settings =
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        static readonly CultureInfo SerializationCulture = CultureInfo.InvariantCulture;

        static readonly Encoding Encoding = Encoding.UTF7;

        public TransportMessageToSend Serialize(Message message)
        {
            using (new CultureContext(SerializationCulture))
            {
                var messageAsString = JsonConvert.SerializeObject(message.Messages, Formatting.Indented, Settings);

                return new TransportMessageToSend
                           {
                               Body = Encoding.GetBytes(messageAsString),
                               Headers = message.Headers.ToDictionary(k => k.Key, v => v.Value),
                               Label = message.GetLabel(),
                           };
            }
        }

        public Message Deserialize(ReceivedTransportMessage transportMessage)
        {
            using (new CultureContext(SerializationCulture))
            {
                var messages = (object[])JsonConvert.DeserializeObject(Encoding.GetString(transportMessage.Body), Settings);

                return new Message
                           {
                               Headers = transportMessage.Headers.ToDictionary(k => k.Key, v => v.Value),
                               Messages = messages
                           };
            }
        }

        class CultureContext : IDisposable
        {
            readonly CultureInfo currentCulture;
            readonly CultureInfo currentUiCulture;

            public CultureContext(CultureInfo cultureInfo)
            {
                var thread = Thread.CurrentThread;

                currentCulture = thread.CurrentCulture;
                currentUiCulture = thread.CurrentUICulture;

                thread.CurrentCulture = cultureInfo;
                thread.CurrentUICulture = cultureInfo;
            }

            public void Dispose()
            {
                var thread = Thread.CurrentThread;

                thread.CurrentCulture = currentCulture;
                thread.CurrentUICulture = currentUiCulture;
            }
        }
    }
}