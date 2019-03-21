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
        private int _fluentdPort;
        private string _fluentdTag;
        private bool _fluentdEnabled;
        private int _fluentdConnTimeout;
        private TcpClient client;
        private Stream stream;
        private FluentdPacker packer;

        /// <summary>
        /// Sets the Host of the Fluentd instance which will receive the logs
        /// </summary>        
        public Layout Host { get; set; }

        /// <summary>
        /// Sets the Port for the connection
        /// </summary>
        [DefaultValue("24224")]
        public Layout Port { get; set; }

        /// <summary>
        /// Sets the Tag for the log redirection within Fluentd
        /// </summary>
        public Layout Tag { get; set; }

        /// <summary>
        /// When Enabled is false the target will not send messages to the fluentd host. 
        /// Note: The Write operations will still happen within NLog. It's better to disable it from the logger attribute,
        /// this setting is aimed to be able to be used with a Layout renderer (GCD, MDC, MDLC and Variables)
        /// </summary>
        [DefaultValue("true")]
        public Layout Enabled { get; set; }

        /// <summary>
        /// Sets the Connection Timeout
        /// </summary>
        [DefaultValue("30000")]
        public Layout ConnectionTimeout { get; set; }

        [DefaultValue(false)]
        public bool UseSsl { get; set; }

        [DefaultValue(true)]
        public bool ValidateCertificate { get; set; }

        public FluentdTarget()
        {
            Name = "Fluentd";
            UseSsl = false;
            ConnectionTimeout = "30000";
            Enabled = "true";
            Port = "24224";
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
                this.client.ConnectAsync(_fluentdHost, _fluentdPort).Wait(_fluentdConnTimeout);
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
                this.stream?.FlushAsync();
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
            _fluentdEnabled = bool.Parse(Enabled?.Render(logEvent.LogEvent));
            if (!_fluentdEnabled)
            {
                InternalLogger.Trace("Fluentd is disabled.");
                return;
            }

            _fluentdHost = Host?.Render(logEvent.LogEvent);
            _fluentdPort = int.Parse(string.IsNullOrEmpty(Port?.Render(logEvent.LogEvent)) ? "24224" : Port?.Render(logEvent.LogEvent));
            _fluentdTag  = Tag?.Render(logEvent.LogEvent);
            _fluentdConnTimeout = int.Parse(string.IsNullOrEmpty(ConnectionTimeout?.Render(logEvent.LogEvent)) ? "30000" : ConnectionTimeout?.Render(logEvent.LogEvent));

            GetConnection();
            InternalLogger.Trace("Fluentd (Name={0}): Sending to address: '{1}:{2}'", Name, _fluentdHost, _fluentdPort);
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

                throw;  // Notify NLog of failure
            }
        }
    }
}
