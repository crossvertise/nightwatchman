using System;
using System.Collections.Generic;
using System.Text;

namespace DomainModel
{
    public class NotificationEmail
    {
        public string Sender { get; set; }

        public string Recipient { get; set; }

        public string Subject { get; set; }

        public string BodyText { get; set; }

        public string BodyHtml { get; set; }
    }
}
