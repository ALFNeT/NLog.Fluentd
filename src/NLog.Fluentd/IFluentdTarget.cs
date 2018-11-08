namespace NLog.Fluentd
{
    public interface IFluentdTarget
    {
        /// <summary>
        /// Sets the Host of the Fluentd instance which will receive the logs
        /// </summary>
        string Host { get; set; }

        /// <summary>
        /// Sets the Port for the connection
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// Sets the Tag for the log redirection within Fluentd
        /// </summary>
        string Tag { get; set; }

        /// <summary>
        /// Use an encrypted connection to communicate with Fluentd.
        /// </summary>
        bool useSsl { get; set; }

        /// <summary>
        /// Set it to false to disable certificate validation for the TLS connection.
        /// </summary>
        bool ValidateCertificate { get; set; }
    }
}
