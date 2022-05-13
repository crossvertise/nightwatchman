namespace BusinessLogic.Models.SendInBlue
{
    using System.Collections;

    public class SendInBlueEmailAddress
    {
        static SendInBlueEmailAddress()
        {
            Staudigl = new Hashtable();
            ApprovedStaudigl = new Hashtable();

        }

        public static Hashtable Staudigl { get; set; }

        public static Hashtable ApprovedStaudigl { get; set; }
    }
}