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
    public class FluentdTarget : TargetWithLayout, IFluentdTarget
    {
        private string _fluentdHost;
        private string _tag;
        private int _fluentdPort;
        private int _connTimeout;
        private TcpClient client;
        private Stream stream;
        private FluentdPacker packer;

        /// <summary>
        /// Sets the Host of the Fluentd instance which will receive the logs
        /// </summary>
        [RequiredParameter]
        [DefaultValue("127.0.0.1")]
        public Layout Host
        {
            get { return _fluentdHost; }
            set
            {
                _fluentdHost = value?.Render(LogEventInfo.CreateNullEvent());
                Cleanup();
            }
        }

        /// <summary>
        /// Sets the Port for the connection
        /// </summary>
        public Layout Port { get; set; }

        /// <summary>
        /// Sets the Tag for the log redirection within Fluentd
        /// </summary>
        [RequiredParameter]
        [DefaultValue("nlog")]
        public Layout Tag
        {
            get { return _tag; }
            set
            {
                _tag = value?.Render(LogEventInfo.CreateNullEvent());
            }
        }

        /// <summary>
        /// Sets the Timeout for Assynchronous connections
        /// </summary>
        public Layout ConnectionTimeout { get; set; }

        /// <summary>
        /// When Enabled is false the target will not send messages to the fluentd host. 
        /// Note: The Write operations will still happen within NLog. It's better to disable it from the logger attribute,
        /// this setting is aimed to be able to be used with a Layout renderer (GCD, MDC, MDLC and Variables)
        /// </summary>
        [DefaultValue(true)]
        public Layout Enabled { get; set; }

        [DefaultValue(false)]
        public bool UseSsl { get; set; }

        [DefaultValue(true)]
        public bool ValidateCertificate { get; set; }

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
            if(!ValidateCertificate)
            {
                return true;
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }

        private void ConnectClient()
        {
            NLog.Common.InternalLogger.Debug("Fluentd Connecting to {0}:{1}, SSL:{2}", _fluentdHost, _fluentdPort, UseSsl);

            try
            {
                if (this._connTimeout > 0)
                {
                    this.client.ConnectAsync(_fluentdHost, _fluentdPort).Wait(_connTimeout);
                }
                else
                {
                    this.client.Connect(_fluentdHost, _fluentdPort);
                }
            }
            catch(SocketException se)
            {
                InternalLogger.Error("Fluentd Extension Failed to connect against {0}:{1}", _fluentdHost, _fluentdPort);
                throw se;
            }

            if (this.UseSsl)
            {
                SslStream sslStream = new SslStream(new BufferedStream(this.client.GetStream()),
                                                    false,
                                                    new RemoteCertificateValidationCallback(ValidateServerCertificate), 
                                                    null,
                                                    EncryptionPolicy.RequireEncryption);
                try
                {
                    sslStream.AuthenticateAsClient(_fluentdHost, null, SslProtocols.Tls12, true);
                    this.stream = sslStream;
                }
                catch (AuthenticationException e)
                {
                    InternalLogger.Error("Fluentd Extension Failed to authenticate against {0}:{1}", _fluentdHost, _fluentdPort);
                    InternalLogger.Error("Exception: {0}", e.Message);
                    client.Close();
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
                NLog.Common.InternalLogger.Warn("Fluentd Close - " + ex.ToString());
            }
            finally
            {
                this.stream = null;
                this.client = null;
                this.packer = null;
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
            if (!bool.Parse(Enabled?.Render(logEvent.LogEvent)))
            {
                InternalLogger.Trace("Fluentd is disabled.");
                return;
            }

            if (Int32.TryParse(Port?.Render(logEvent.LogEvent), out int _outFluentdPort))
            {
                _fluentdPort = _outFluentdPort;
            }
            else
            {
                _fluentdPort = 24224;
            }

            if (Int32.TryParse(ConnectionTimeout?.Render(logEvent.LogEvent), out int _outConnTimeout))
            {
                _connTimeout = _outConnTimeout;
            }
            else
            {
                _connTimeout = 5000;
            }

            GetConnection();
            InternalLogger.Trace("Fluentd (Name={0}): Sending to address: '{1}:{2}'", Name, _fluentdHost, _fluentdPort);
            var record = new Dictionary<string, string>();
            var logMessage = GetFormattedMessage(logEvent.LogEvent);
            record.Add("message", logMessage);
            try
            {
                this.packer.Pack(logEvent.LogEvent.TimeStamp, _tag, record);
            }
            catch (Exception ex)
            {
                InternalLogger.Warn("Fluentd Emit - " + ex.ToString());

                throw;  // Notify NLog of failure
            }
        }
    }
}
