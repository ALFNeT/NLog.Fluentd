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
        private static Lazy<SerializationContext> serializationContextWrapper = new Lazy<SerializationContext>(
            () => InstantiateSerializationContext());

        private SerializationContext SerializationContext => serializationContextWrapper.Value;

        private static DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly Packer packer;

        /// <summary>
        /// Packs an event in the stream.
        /// </summary>
        /// <remarks>
        /// The event is packed using the Message Mode.
        /// </remarks>
        public void Pack(DateTime timestamp, string tag, IDictionary<string, string> data)

        {
            long unixTimestamp = timestamp.ToUniversalTime().Subtract(unixEpoch).Ticks / 10000000;
            this.packer.PackArrayHeader(3);
            this.packer.PackString(tag, Encoding.UTF8);
            this.packer.Pack((ulong)unixTimestamp);
            this.packer.Pack(data, SerializationContext);
        }

        /// <summary>
        /// Initializes a new instance of MsgPack.Packer
        /// </summary>
        /// <param name="stream">Stream object to be wrapped by the Packer</param>
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
