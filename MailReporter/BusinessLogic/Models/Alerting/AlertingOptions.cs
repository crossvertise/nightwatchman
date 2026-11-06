namespace BusinessLogic.Models.Alerting
{
    using System.Collections.Generic;

    /// <summary>The <c>Alerting</c> configuration section.</summary>
    public class AlertingOptions
    {
        public const string SectionName = "Alerting";

        /// <summary>Off by default, so an unconfigured environment never posts anywhere.</summary>
        public bool Enabled { get; set; }

        /// <summary>How often the worker evaluates every tile.</summary>
        public int IntervalMinutes { get; set; } = 10;

        /// <summary>Incoming-webhook URL of the Teams channel. Empty disables posting.</summary>
        public string TeamsWebhookUrl { get; set; }

        /// <summary>
        /// Jobs that are alerted the moment they break, around the clock. Everything else is treated
        /// as a nightly batch job and held until <see cref="BatchReleaseHour"/>.
        /// </summary>
        public List<string> ImmediateJobs { get; set; } = new List<string>();

        /// <summary>Local hour from which a held batch problem may be announced.</summary>
        public int BatchReleaseHour { get; set; } = 8;

        /// <summary>
        /// Local hour from which batch problems are held again. An ETL that dies at 22:30 is not worth
        /// a phone buzzing at night; it will be there in the morning.
        /// </summary>
        public int BatchQuietHour { get; set; } = 22;

        /// <summary>Time zone the two hours above are expressed in.</summary>
        public string TimeZone { get; set; } = "Europe/Berlin";

        /// <summary>
        /// Link appended to problem alerts so the dashboard is one click away. Empty omits it.
        /// </summary>
        public string DashboardUrl { get; set; }
    }
}
