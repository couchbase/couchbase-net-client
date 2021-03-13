using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;

namespace Couchbase.Core.CircuitBreakers
{
    public class CircuitBreakerConfiguration
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The minimum amount of operations to measure before the threshold percentage kicks in.
        /// </summary>
        public int VolumeThreshold { get; set; } = 20;

        /// <summary>
        /// The percentage of operations that need to fail in a window until the circuit opens.
        /// </summary>
        public uint ErrorThresholdPercentage { get; set; } = 50;

        /// <summary>
        /// The initial sleep time after which a canary is sent as a probe.
        /// </summary>
        public TimeSpan SleepWindow { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The rolling time-frame which is used to calculate the error threshold percentage.
        /// </summary>
        public TimeSpan RollingWindow { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The timeout for the canary request until it is deemed failed
        /// </summary>
        public TimeSpan CanaryTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Called on every response to determine if it is successful or not. The default
        /// implementation counts SocketException, TimeoutException and TaskCanceledExceptions, RequestCanceledException
        /// as failures.
        /// </summary>
        public Func<Exception, bool> CompletionCallback { get; set; } = delegate(Exception e)
        {
            return e switch
            {
                SocketException _ => true,
                Exceptions.TimeoutException _ => true,
                TaskCanceledException _ => true,
                RequestCanceledException _ => true,
                AuthenticationFailureException _ => true,
                _ => false
            };
        };

        public static CircuitBreakerConfiguration Default = new();
    }
}
