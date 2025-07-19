using System;
using Newtonsoft.Json.Linq;

public class Model
{
    public string Api { get; set; }
    public string Name { get; set; }
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
                Name = parts[1];
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
}
