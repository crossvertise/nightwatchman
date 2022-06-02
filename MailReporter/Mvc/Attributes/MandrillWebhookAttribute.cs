namespace Mvc.Attributes
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    public class MandrillWebhookAttribute : ActionFilterAttribute
    {
        public string Key { get; set; }

        public string KeyAppSetting { get; set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Request.Method == "HEAD")
            {
                base.OnActionExecuting(context);
                return;
            }

            if (Key == null)
            {
                var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>();
                Key = configuration[this.KeyAppSetting];
            }

            var request = context.HttpContext.Request;
            var requestUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
            var signature = this.GenerateSignature(Key, requestUrl, context.HttpContext.Request.Form);

            var mandrillSignature = context.HttpContext.Request.Headers.FirstOrDefault(h => h.Key == "X-Mandrill-Signature").Value.First();

            if (mandrillSignature == null || mandrillSignature != signature)
            {
                context.Result = new UnauthorizedResult();
            }

            base.OnActionExecuting(context);
        }

        private string GenerateSignature(string key, string url, IFormCollection form)
        {
            var sourceString = string.Concat(url,
                string.Concat(form.Select(item => item.Key + item.Value)));

            var keyBytes = Encoding.ASCII.GetBytes(key);
            var valueBytes = Encoding.ASCII.GetBytes(sourceString);

            using (var hmac = new HMACSHA1(keyBytes))
            {
                return Convert.ToBase64String(hmac.ComputeHash(valueBytes));
            }
        }
    }
}
