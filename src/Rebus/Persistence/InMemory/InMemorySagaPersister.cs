﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ponder;

namespace Rebus.Persistence.InMemory
{
    public class InMemorySagaPersister : IStoreSagaData, IEnumerable<ISagaData>
    {
        readonly ConcurrentDictionary<Guid, ISagaData> data = new ConcurrentDictionary<Guid, ISagaData>();

        public virtual void Save(ISagaData sagaData, string[] sagaDataPropertyPathsToIndex)
        {
            var key = sagaData.Id;
            data[key] = sagaData;
        }

        public void Delete(ISagaData sagaData)
        {
            ISagaData temp;
            data.TryRemove(sagaData.Id, out temp);
        }

        public virtual ISagaData Find(string sagaDataPropertyPath, object fieldFromMessage, Type sagaDataType)
        {
            foreach (var sagaData in data)
            {
                var valueFromSagaData = (Reflect.Value(sagaData.Value, sagaDataPropertyPath) ?? "").ToString();

                if (valueFromSagaData.Equals((fieldFromMessage ?? "").ToString()))
                {
                    return sagaData.Value;
                }
            }
            return null;
        }

        public void UseIndex(string[] sagaDataPathsToIndex)
        {
            
        }

        public IEnumerator<ISagaData> GetEnumerator()
        {
            return data.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}