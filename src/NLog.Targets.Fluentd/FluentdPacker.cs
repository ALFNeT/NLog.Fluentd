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
        private static DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly Packer packer;
        private readonly SerializationContext serializationContext;

        public void Pack(DateTime timestamp, string tag, IDictionary<string, dynamic> data)
        {
            long unixTimestamp = timestamp.ToUniversalTime().Subtract(unixEpoch).Ticks / 10000000;
            this.packer.PackArrayHeader(3);
            this.packer.PackString(tag, Encoding.UTF8);
            this.packer.Pack((ulong)unixTimestamp);
            this.packer.Pack(data, serializationContext);
        }

        public FluentdPacker(Stream stream)
        {
            this.packer = Packer.Create(stream);
            this.serializationContext = new SerializationContext();
            this.serializationContext.CompatibilityOptions.PackerCompatibilityOptions = PackerCompatibilityOptions.None;
            this.serializationContext.DefaultDateTimeConversionMethod = DateTimeConversionMethod.Native;
            this.serializationContext.SerializationMethod = SerializationMethod.Map;
        }
    }
}
