namespace BusinessLogic.Interfaces
{
    using Newtonsoft.Json.Linq;

    using System.Threading.Tasks;

    public interface ISendInBlueService
    {
        Task<(bool IsSuccess, string ErrorMessage)> ProcessEvent(JObject sendInBlueEvent);
    }
}
