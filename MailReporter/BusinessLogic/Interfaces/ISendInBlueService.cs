using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ISendInBlueService
    {
        Task<(bool IsSuccess, string ErrorMessage)> ProcessEvent(JObject sendInBlueEvent);
    }
}
