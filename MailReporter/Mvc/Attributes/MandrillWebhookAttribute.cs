using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Mvc.Attributes
{
    public class MandrillWebhookAttribute : ActionFilterAttribute
    {
        public string Key { get; set; }

        public string KeyAppSetting { get; set; }
 
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.Request.Method == "HEAD")
            {
                base.OnActionExecuting(filterContext);
                return;
            }
                
            if(Key == null)
            {
                var configuration = filterContext.HttpContext.RequestServices.GetService<IConfiguration>();
                Key = configuration[this.KeyAppSetting];
            }

            var request = filterContext.HttpContext.Request;
            var requestUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
            var signature = this.GenerateSignature(Key, requestUrl, filterContext.HttpContext.Request.Form);

            var mandrillSignature = filterContext.HttpContext.Request.Headers.FirstOrDefault(h => h.Key == "X-Mandrill-Signature").Value.First();

            if (mandrillSignature == null || mandrillSignature != signature)
            {
                filterContext.Result = new UnauthorizedResult();
            }

            base.OnActionExecuting(filterContext);
        }

        private string GenerateSignature(string key, string url, IFormCollection form)
        {
            var sourceString = url;
            foreach (var kvp in form)
            {
                sourceString += kvp.Key + kvp.Value;
            }

            byte[] byteKey = System.Text.Encoding.ASCII.GetBytes(key);
            byte[] byteValue = System.Text.Encoding.ASCII.GetBytes(sourceString);
            HMACSHA1 myhmacsha1 = new HMACSHA1(byteKey);
            byte[] hashValue = myhmacsha1.ComputeHash(byteValue);
            string generatedSignature = Convert.ToBase64String(hashValue);

            return generatedSignature;
        }
    }
}
