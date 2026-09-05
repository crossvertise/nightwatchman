namespace BusinessLogic.Models.SendInBlue
{
    using System;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Accepts a JSON value that is either a single string or an array of strings and always
    /// yields a <c>string[]</c>. Brevo serialises multi-valued mail headers (e.g. <c>Received</c>)
    /// as an array when the header occurs several times, but as a plain string when it occurs
    /// only once — without this converter such payloads fail to deserialise and the webhook
    /// answers 500.
    /// </summary>
    public class SingleOrArrayConverter : JsonConverter<string[]>
    {
        public override string[] ReadJson(JsonReader reader, Type objectType, string[] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            switch (token.Type)
            {
                case JTokenType.Null:
                    return null;
                case JTokenType.Array:
                    return token.ToObject<string[]>(serializer);
                default:
                    return new[] { token.ToString() };
            }
        }

        public override void WriteJson(JsonWriter writer, string[] value, JsonSerializer serializer) =>
            serializer.Serialize(writer, value);
    }
}
