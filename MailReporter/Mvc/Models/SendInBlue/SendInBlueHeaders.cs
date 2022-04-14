namespace Xv.Mvc.SendInBlue.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SendInBlueHeaders
    {
        public string ReturnPath { get; set; }

        public string DeliveredTo { get; set; }

        public string[] Received { get; set; }

        public string ARCSeal { get; set; }

        public string ARCMessageSignature { get; set; }

        public string ARCAuthenticationResults { get; set; }

        public string DKIMSignature { get; set; }

        public string XGmMessageState { get; set; }

        public string XGoogleSmtpSource { get; set; }

        public string XReceived { get; set; }

        public string MIMEVersion { get; set; }

        public string References { get; set; }

        public string InReplyTo { get; set; }

        public string From { get; set; }

        public string Date { get; set; }

        public string XGmailOriginalMessageID { get; set; }

        public string MessageID { get; set; }

        public string Subject { get; set; }

        public string To { get; set; }

        public string ContentType { get; set; }

        public string XZohoVirusStatus { get; set; }

        public string XZohoMailClient { get; set; }

        public string XGNDStatus { get; set; }

        public string ReceivedSPF { get; set; }
    }
}
