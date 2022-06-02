namespace BusinessLogic.Models.SendInBlue
{
    public class SendInBlueItemDetail
    {
        public string[] Uuid { get; set; }

        public string MessageId { get; set; }

        public string InReplyTo { get; set; }

        public SendInBlueFrom From { get; set; }

        public SendInBlueTo[] To { get; set; }

        public object[] Cc { get; set; }

        public object ReplyTo { get; set; }

        public string SentAtDate { get; set; }

        public string Subject { get; set; }

        public string RawHtmlBody { get; set; }

        public string RawTextBody { get; set; }

        public string ExtractedMarkdownMessage { get; set; }

        public string ExtractedMarkdownSignature { get; set; }

        public float SpamScore { get; set; }

        public SendInBlueAttachment[] Attachments { get; set; }

        public SendInBlueHeaders Headers { get; set; }
    }
}
