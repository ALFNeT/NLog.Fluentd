using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using System.ComponentModel;

namespace NLog.Fluentd
{
    [Target("Fluentd")]
    public class FluentdTarget : TargetWithLayout, IFluentdTarget
    {
        /// <summary>
        /// Sets the Host of the Fluentd instance which will receive the logs
        /// </summary>
        [RequiredParameter]
        [DefaultValue("127.0.0.1")]
        public string Host { get; set; }

        /// <summary>
        /// Sets the Port for the connection
        /// </summary>
        [RequiredParameter]
        [DefaultValue(24224)]
        public int Port { get; set; }

        /// <summary>
        /// Sets the Tag for the log redirection within Fluentd
        /// </summary>
        [RequiredParameter]
        [DefaultValue("nlog")]
        public string Tag { get; set; }

        /// <summary>
        /// Formats the payload to Fluentd using `MsgPack` or `JSON`.
        /// </summary>
        [DefaultValue("MsgPack")]
        public string ForwardProtocol { get; set; }

        [DefaultValue(false)]
        public bool useSsl { get; set; }

        [DefaultValue(true)]
        public bool ValidateCertificate { get; set; }

        private TcpClient client;

        private Stream stream;

        private FluentdPacker emitter;

        public FluentdTarget()
        {
            Name = "Fluentd";
        }

        protected void GetConnection()
        {
            if (this.client == null || !this.client.Connected)
            {
                Cleanup();
                this.client = new TcpClient();
                ConnectClient();
            }
        }

        public bool ValidateServerCertificate(
                  object sender,
                  X509Certificate certificate,
                  X509Chain chain,
                  SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }
            if (this.ValidateCertificate)
                return false; 
            else
                return true;
        }

        private void ConnectClient()
        {
            NLog.Common.InternalLogger.Debug("Fluentd Connecting to {0}:{1}, SSL:{2}", this.Host, this.Port, this.useSsl);

            this.client.Connect(this.Host, this.Port);
            if (this.useSsl)
            {
                SslStream sslStream = new SslStream(new BufferedStream(this.client.GetStream()),
                                                    false,
                                                    new RemoteCertificateValidationCallback(ValidateServerCertificate), 
                                                    null,
                                                    EncryptionPolicy.RequireEncryption);
                try
                {
                    sslStream.AuthenticateAsClient(this.Host, null, SslProtocols.Tls12, true);
                    this.stream = sslStream;
                }
                catch (AuthenticationException e)
                {
                    InternalLogger.Error("Fluentd Extension Failed to authenticate against {0}:{1}", this.Host, this.Port);
                    InternalLogger.Error("Exception: {0}", e.Message);
                    client.Close();
                    throw;
                }
            }
            else
            {
                this.stream = new BufferedStream(this.client.GetStream());
            }
            this.emitter = new FluentdPacker(this.stream);
        }

        protected void Cleanup()
        {
            try
            {
                this.stream?.Dispose();
                this.client?.Close();
            }
            catch (Exception ex)
            {
                NLog.Common.InternalLogger.Warn("Fluentd Close - " + ex.ToString());
            }
            finally
            {
                this.stream = null;
                this.client = null;
                this.emitter = null;
            }
        }

        protected override void CloseTarget()
        {
            Cleanup();
            base.CloseTarget();
        }
        
        /// <summary>
        /// Formats the log event for write.
        /// </summary>
        /// <param name="logEvent">The log event to be formatted.</param>
        /// <returns>A string representation of the log event.</returns>
        protected virtual string GetFormattedMessage(LogEventInfo logEvent)
        {
            return Layout.Render(logEvent);
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            GetConnection();
            InternalLogger.Trace("Fluentd (Name={0}): Sending to address: '{1}:{2}'", Name, this.Host, this.Port);
            var record = new Dictionary<string, dynamic>();
            var logMessage = GetFormattedMessage(logEvent.LogEvent);
            record.Add("message", logMessage);
            try
            {
                this.emitter.Pack(logEvent.LogEvent.TimeStamp, this.Tag, record);
            }
            catch (Exception ex)
            {
                InternalLogger.Warn("Fluentd Emit - " + ex.ToString());

                throw;  // Notify NLog of failure
            }
        }
    }
}
