using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Model
{
    [JsonProperty("api")]
    public string Api { get; set; }
    [JsonProperty("model")]
    public string Name { get; set; }
    [JsonProperty("url")]
    public string Url { get; set; }

    public Model() { }

    public Model(object input)
    {
        if (input is JObject jObj)
        {
            Name = jObj["model"]?.ToString();
            Api = jObj["api"]?.ToString();
            Url = jObj["url"]?.ToString();
        }
        else if (input is string str)
        {
            if (str.Contains("/"))
            {
                var parts = str.Split('/');
                Api = parts[0];
                Name = string.Join("/", parts, 1, parts.Length - 1);
                Url = null;
            }
            else
            {
                Name = str;
                Api = null;
                Url = null;
            }
        }
        else
        {
            throw new ArgumentException("Unsupported input format for Model constructor");
        }
    }

    public JObject ToJson()
    {
        var json = new JObject
        {
            ["model"] = this.Name,
            ["api"] = this.Api,
            ["url"] = this.Url
        };
        return json;
    }

    public override string ToString()
    {
        return $"{this.Api}/{this.Name}";
    }
}
