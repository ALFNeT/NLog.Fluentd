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
    public partial class FluentdTarget : TargetWithLayout, IFluentdTarget
    {
        private string _fluentdHost;
        private bool _fluentdEnabled;
        private TcpClient _client;
        private Stream _stream;
        private FluentdPacker _packer;

        /// <summary>
        /// Initializes a new instance of the Fluentd logging target.
        /// </summary>
        public FluentdTarget()
        {
        }

        /// <summary>
        /// Initializes a new instance of the Fluentd logging target.
        /// </summary>
        /// <param name="name">Name of the target.</param>
        public FluentdTarget(string name) : this()
        {
            Name = name;
        }

        /// <summary>
        /// Checks if the tcp connection is healthy and that the host hasn't been modified,
        /// if it has then the connection is reset.
        /// </summary>
        /// <param name="renderedFluentdHost">Host name of fluentd.</param>
        protected void CheckConnectionIsValid(string renderedFluentdHost)
        {
            if (this._client == null || !this._client.Connected || _fluentdHost != renderedFluentdHost)
            {
                ResetConnection();
                _fluentdHost = renderedFluentdHost;
                InitiateTCPConnection();
                if (this.UseSsl)
                {
                    SetUpConnectionStream();
                }
                else
                {
                    SetUpInsecureConnectionStream();
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

        /// <summary>
        /// Connects to the fluentd cluster through a TCP socket.
        /// </summary>
        /// <remarks>
        /// The connection will timeout after `ConnectionTimeout`
        /// </remarks>
        private void InitiateTCPConnection()
        {
            NLog.Common.InternalLogger.Debug("Fluentd Connecting to {0}:{1}, SSL:{2}", _fluentdHost, Port, UseSsl);

            try
            {
                this._client = new TcpClient();
                this._client.ConnectAsync(_fluentdHost, Port).Wait(ConnectionTimeout);
            }
            catch(SocketException se)
            {
                InternalLogger.Error("Fluentd Extension Failed to connect against {0}:{1}", _fluentdHost, Port);
                ResetConnection();
                throw se;
            }
        }

        /// <summary>
        /// Creates and authenticates the stream
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
                ResetConnection();
                throw;
            }
        }

        /// <summary>
        /// Creates an insecure stream.
        /// </summary>
        private void SetUpInsecureConnectionStream()
        {
            try
            {
                this._stream = new BufferedStream(this._client.GetStream());
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Exception: {0}", ex.Message);
                ResetConnection();
                throw;
            }
        }

        /// <summary>
        /// Resets all objects related to the fluentd connection.
        /// </summary>
        protected void ResetConnection()
        {
            try
            {
                this._stream?.Dispose();
                this._client?.Close();
            }
            catch (Exception ex)
            {
                NLog.Common.InternalLogger.Warn("Fluentd: Connection Reset Error  - " + ex.ToString());
            }
            finally
            {
                this._stream = null;
                this._client = null;
                this._packer = null;
            }
        }

        /// <summary>
        /// Closes the Target
        /// </summary>
        protected override void CloseTarget()
        {
            ResetConnection();
            base.CloseTarget();
        }

        protected override void Write(LogEventInfo logEvent)
        {
            _fluentdEnabled = bool.Parse(Enabled?.Render(logEvent));
            if (!_fluentdEnabled)
            {
                InternalLogger.Trace("Fluentd target is disabled.");
                return;
            }

            CheckConnectionIsValid(Host.Render(logEvent));

            string fluentdTag  = Tag?.Render(logEvent);
            Dictionary<string, string> record = new Dictionary<string, string>();
            record.Add("message", Layout.Render(logEvent));

            try
            {
                InternalLogger.Trace("Fluentd (Name={0}): Sending to address: '{1}:{2}'", Name, _fluentdHost, Port);
                this._packer.Pack(logEvent.TimeStamp, fluentdTag, record);
            }
            catch (Exception ex)
            {
                InternalLogger.Warn("Fluentd: Error Packing event - " + ex.ToString());
                throw;  // Notify NLog of failure
            }
        }
    }
}
