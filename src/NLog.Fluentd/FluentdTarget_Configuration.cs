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
    // Partial class to split out configuration options.
    // Target implementation is in FluentdTarget.cs
    public partial class FluentdTarget
    {
        /// <summary>
        /// Sets the Host of the Fluentd instance which will receive the logs
        /// </summary>
        [RequiredParameter]
        public Layout Host { get; set; }

        /// <summary>
        /// Sets the Tag for the log redirection within Fluentd
        /// </summary>
        [RequiredParameter] 
        public Layout Tag { get; set; }

        /// <summary>
        /// Sets the Port for the connection
        /// </summary>
        [DefaultValue(24224)]
        public int Port { get; set; } = 24224;

        /// <summary>
        /// When Enabled is false the target will not send messages to the fluentd host.
        /// Note: The Write operations will still happen within NLog. It's better to disable it from the logger attribute,
        /// this setting is aimed to be able to be used with a Layout renderer (GCD, MDC, MDLC and Variables)
        /// </summary>
        [DefaultValue("true")]
        public Layout Enabled { get; set; } = "true";

        /// <summary>
        /// Sets the Connection Timeout
        /// </summary>
        [DefaultValue(30000)]
        public int ConnectionTimeout { get; set; } = 30000;

        [DefaultValue(false)]
        public bool UseSsl { get; set; } = false;

        [DefaultValue(true)]
        public bool ValidateCertificate { get; set; } = true;

    }
}
