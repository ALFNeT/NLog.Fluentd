using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NLog.Fluentd
{
    internal class FluentdPacker
    {
        //https://github.com/msgpack/msgpack-cli recommends the serialization context be made static
        private static Lazy<SerializationContext> serializationContextWrapper = new Lazy<SerializationContext>(
            () => InstantiateSerializationContext());

        private SerializationContext SerializationContext => serializationContextWrapper.Value;

        private static DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly Packer packer;
        //private readonly SerializationContext serializationContext;

        public void Pack(DateTime timestamp, string tag, IDictionary<string, string> data)

        {
            long unixTimestamp = timestamp.ToUniversalTime().Subtract(unixEpoch).Ticks / 10000000;
            this.packer.PackArrayHeader(3);
            this.packer.PackString(tag, Encoding.UTF8);
            this.packer.Pack((ulong)unixTimestamp);
            this.packer.Pack(data, SerializationContext);
        }

        public FluentdPacker(Stream stream)
        {
            this.packer = Packer.Create(stream);           
        }

        private static SerializationContext InstantiateSerializationContext()
        {
            var serializationContext = new SerializationContext
            {
                DefaultDateTimeConversionMethod = DateTimeConversionMethod.Native,
                SerializationMethod = SerializationMethod.Map
            };

            return serializationContext;
        }
    }
}
