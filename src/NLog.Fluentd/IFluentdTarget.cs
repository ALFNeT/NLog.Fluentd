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
        /// Sets the amount of time a TcpClient will wait for a send operation to complete successfully.
        /// </summary>
        Layout SendTimeout { get; set; }
    }
}
