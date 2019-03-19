using NLog.Layouts;

namespace NLog.Fluentd
{
    public interface IFluentdTarget
    {
        /// <summary>
        /// Sets the Host of the Fluentd instance which will receive the logs
        /// </summary>
        Layout Host { get; set; }

        /// <summary>
        /// Sets the Port for the connection
        /// </summary>
        Layout Port { get; set; }

        /// <summary>
        /// Sets the Tag for the log redirection within Fluentd
        /// </summary>
        Layout Tag { get; set; }

        /// <summary>
        /// Use an encrypted connection to communicate with Fluentd.
        /// </summary>
        bool UseSsl { get; set; }

        /// <summary>
        /// Set it to false to disable certificate validation for the TLS connection.
        /// </summary>
        bool ValidateCertificate { get; set; }

        /// <summary>
        /// When Enabled is false the target will not send messages to the fluentd host. 
        /// Note: The Write operations will still happen within NLog. It's better to disable it from the logger attribute,
        /// this setting is aimed to be able to be used with a Layout renderer (GCD, MDC, MDLC and Variables)
        /// </summary>
        Layout Enabled { get; set; }

        /// <summary>
        /// Used in conjunction with the connection timeout (setting below). If enabled, a custom timeout for the connection attempt can be set
        /// </summary>
        Layout AsyncConnection { get; set; }

        /// <summary>
        /// How long (in miliseconds) the connection will wait before it timesout. 
        /// Used in conjunction with the "AsyncConnection" setting above. This number is only considered if the above is enabled.
        /// </summary>
        Layout AsyncConnectionTimeout { get; set; }
    }
}
