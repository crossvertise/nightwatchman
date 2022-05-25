namespace DomainModel.DTO.PRTG
{
    using System.Collections.Generic;

    public class PrtgResult
    {
        public Prtg Prtg { get; set; }
    }

    public class Prtg
    {
        public List<PrtgChannel> Result { get; set; }
    }

    public class PrtgChannel
    {
        public string Channel { get; set; }

        public int Value { get; set; }

        public string Unit { get; set; }

        public int Warning { get; set; }

        public int? LimitMaxError { get; set; }

        public int? LimitMaxWarning { get; set; }

        public int? LimitMinError { get; set; }

        public int? LimitMinWarning { get; set; }

        public int? LimitMode { get; set; }
    }
}
