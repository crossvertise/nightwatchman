namespace Mvc.Models.SendInBlue
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class SendInBlueAttachment
    {
        public string Name { get; set; }

        public string ContentType { get; set; }

        public int ContentLength { get; set; }

        public string ContentID { get; set; }
    }
}
