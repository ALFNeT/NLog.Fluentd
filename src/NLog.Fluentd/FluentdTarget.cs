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
        private TcpClient _client;
        private Stream _stream;
        private FluentdPacker _packer;

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

        protected void CheckConnectionIsValid(string renderedFluentdHost)
        {
            if (this._client == null || !this._client.Connected || _fluentdHost != renderedFluentdHost)
            {
                Cleanup();
                _fluentdHost = renderedFluentdHost;
                this._client = new TcpClient();
                InitiateTCPConnection();
                if (this.UseSsl)
                {
                    SetUpConnectionStream();
                }
                else
                {
                    SetUpUnsecureConnectionStream();
                }
                this._packer = new FluentdPacker(this._stream);
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

        private void InitiateTCPConnection()
        {
            NLog.Common.InternalLogger.Debug("Fluentd Connecting to {0}:{1}, SSL:{2}", _fluentdHost, Port, UseSsl);

            try
            {
                this._client.ConnectAsync(_fluentdHost, Port).Wait(ConnectionTimeout);
            }
            catch(SocketException se)
            {
                InternalLogger.Error("Fluentd Extension Failed to connect against {0}:{1}", _fluentdHost, Port);
                Cleanup();
                throw se;
            }
        }

        /// <summary>
        /// Establishes a connection to Fluentd and creates a FluentdPacker.
        /// </summary>
        private void SetUpConnectionStream()
        {
            try
            {
                SslStream sslStream = new SslStream(new BufferedStream(this._client.GetStream()),
                                                false,
                                                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                                                null,
                                                EncryptionPolicy.RequireEncryption);

                sslStream.AuthenticateAsClient(_fluentdHost, null, SslProtocols.Tls12, true);
                this._stream = sslStream;
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Fluentd Extension Failed to authenticate against {0}:{1}", _fluentdHost, Port);
                InternalLogger.Error("Exception: {0}", ex.Message);
                Cleanup();
                throw;
            }
        }

        /// <summary>
        /// Establishes a connection to Fluentd and creates a FluentdPacker.
        /// </summary>
        private void SetUpUnsecureConnectionStream()
        {
            try
            {
                this._stream = new BufferedStream(this._client.GetStream());
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Exception: {0}", ex.Message);
                Cleanup();
                throw;
            }
        }

        protected void Cleanup()
        {
            try
            {
                this._stream?.Dispose();
                this._client?.Close();
            }
            catch (Exception ex)
            {
                NLog.Common.InternalLogger.Warn("Fluentd Cleanup - " + ex.ToString());
            }
            finally
            {
                this._stream = null;
                this._client = null;
                this._packer = null;
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

            CheckConnectionIsValid(renderedFluentdHost);
            InternalLogger.Trace("Fluentd (Name={0}): Sending to address: '{1}:{2}'", Name, _fluentdHost, Port);
            var record = new Dictionary<string, string>();
            var logMessage = GetFormattedMessage(logEvent.LogEvent);
            record.Add("message", logMessage);
            try
            {
                this._packer.Pack(logEvent.LogEvent.TimeStamp, _fluentdTag, record);
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
