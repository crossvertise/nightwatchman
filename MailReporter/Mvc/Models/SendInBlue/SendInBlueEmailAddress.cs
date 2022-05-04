namespace Mvc.Models.SendInBlue
{
    using System.Collections;

    public class SendInBlueEmailAddress
    {
        static SendInBlueEmailAddress()
        {
            Staudigl = new Hashtable();
            ApprovedStaudigl = new Hashtable();

            Staudigl.Add("druckfreigabe-webhook@replydev.crossvertise.com", "druckfreigabe-webhook@replydev.crossvertise.com");
            Staudigl.Add("druckfreigabe-webhook-qa@replydev.crossvertise.com", "druckfreigabe-webhook-qa@replydev.crossvertise.com");
            Staudigl.Add("druckfreigabe-webhook-staging@replydev.crossvertise.com", "druckfreigabe-webhook-staging@replydev.crossvertise.com");
            Staudigl.Add("druckfreigabe-webhook-demo@replydev.crossvertise.com", "druckfreigabe-webhook-demo@replydev.crossvertise.com");

            ApprovedStaudigl.Add("staudigl-approval-qa@replydev.crossvertise.com", "staudigl-approval-qa@replydev.crossvertise.com");
            ApprovedStaudigl.Add("staudigl-approval-staging@replydev.crossvertise.com", "staudigl-approval-staging@replydev.crossvertise.com");
            ApprovedStaudigl.Add("staudigl-approval@replydev.crossvertise.com", "staudigl-approval@replydev.crossvertise.com");
        }

        public static Hashtable Staudigl { get; set; }

        public static Hashtable ApprovedStaudigl { get; set; }
    }
}