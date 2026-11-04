namespace BusinessLogic.Models.Http
{
    /// <summary>What a polled health endpoint reports about itself.</summary>
    public class HttpHealthResponse
    {
        /// <summary>Problems that need someone. Anything above zero turns the tile red.</summary>
        public int Errors { get; set; }

        public int Warnings { get; set; }

        /// <summary>One-line summary, recorded as the execution's subject.</summary>
        public string Message { get; set; }
    }
}
