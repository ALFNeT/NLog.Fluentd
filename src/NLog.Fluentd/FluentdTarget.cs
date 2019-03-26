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
using NLog.Layouts;

namespace NLog.Fluentd
{
    [Target("Fluentd")]
    public partial class FluentdTarget : TargetWithContext, IFluentdTarget
    {
        private string _fluentdHost;
        private string _fluentdTag;
        private bool _fluentdEnabled;
        private TcpClient client;
        private Stream stream;
        private FluentdPacker packer;

        /// <summary>
        /// Construct a Fluentd loggin target.
        /// </summary>
        public FluentdTarget()
        {
        }

        public FluentdTarget(string name) : this()
        {
            Name = name;
        }

        protected void GetConnection(string renderedFluentdHost)
        {
            if (this.client == null || !this.client.Connected || _fluentdHost != renderedFluentdHost)
            {
                Cleanup();
                _fluentdHost = renderedFluentdHost;
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
            if(!ValidateCertificate)
            {
                return true;
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }
        /// <summary>
        /// Establishes a connection to Fluentd and creates a FluentdPacker.
        /// </summary>
        private void ConnectClient()
        {
            NLog.Common.InternalLogger.Debug("Fluentd Connecting to {0}:{1}, SSL:{2}", _fluentdHost, Port, UseSsl);

            try
            {
                this.client.ConnectAsync(_fluentdHost, Port).Wait(ConnectionTimeout);
            }
            catch(SocketException se)
            {
                InternalLogger.Error("Fluentd Extension Failed to connect against {0}:{1}", _fluentdHost, Port);
                Cleanup();
                throw se;
            }

            if (this.UseSsl)
            {
                try
                {
                    SslStream sslStream = new SslStream(new BufferedStream(this.client.GetStream()),
                                                    false,
                                                    new RemoteCertificateValidationCallback(ValidateServerCertificate),
                                                    null,
                                                    EncryptionPolicy.RequireEncryption);

                    sslStream.AuthenticateAsClient(_fluentdHost, null, SslProtocols.Tls12, true);
                    this.stream = sslStream;
                }
                catch (AuthenticationException e)
                {
                    InternalLogger.Error("Fluentd Extension Failed to authenticate against {0}:{1}", _fluentdHost, Port);
                    InternalLogger.Error("Exception: {0}", e.Message);
                    Cleanup();
                    throw;
                }
                catch (Exception ex)
                {
                    InternalLogger.Error("Exception: {0}", ex.Message);
                    Cleanup();
                    throw;
                }
            }
            else
            {
                this.stream = new BufferedStream(this.client.GetStream());
            }
            this.packer = new FluentdPacker(this.stream);
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
                NLog.Common.InternalLogger.Warn("Fluentd Cleanup - " + ex.ToString());
            }
            finally
            {
                this.stream = null;
                this.client = null;
                this.packer = null;
            }
        }

        /// <summary>
        /// Closes / Disposes the Target
        /// </summary>
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
            _fluentdEnabled = bool.Parse(Enabled?.Render(logEvent.LogEvent));
            if (!_fluentdEnabled)
            {
                InternalLogger.Trace("Fluentd is disabled.");
                return;
            }

            string renderedFluentdHost = Host?.Render(logEvent.LogEvent);
            _fluentdTag  = Tag?.Render(logEvent.LogEvent);

            GetConnection(renderedFluentdHost);
            InternalLogger.Trace("Fluentd (Name={0}): Sending to address: '{1}:{2}'", Name, _fluentdHost, Port);
            var record = new Dictionary<string, string>();
            var logMessage = GetFormattedMessage(logEvent.LogEvent);
            record.Add("message", logMessage);
            try
            {
                this.packer.Pack(logEvent.LogEvent.TimeStamp, _fluentdTag, record);
            }
            catch (Exception ex)
            {
                InternalLogger.Warn("Fluentd Emit - " + ex.ToString());
                Cleanup();
                throw;  // Notify NLog of failure
            }
        }
    }
}
