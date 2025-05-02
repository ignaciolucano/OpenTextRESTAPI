using System.Collections.Generic;
using System.Text.Json.Serialization;

public class DocumentTypeResponse
{
    [JsonPropertyName("links")]
    public Links Links { get; set; }

    [JsonPropertyName("results")]
    public List<Result> Results { get; set; }
}

public class Links
{
    [JsonPropertyName("data")]
    public LinkData Data { get; set; }
}

public class LinkData
{
    [JsonPropertyName("self")]
    public Link Self { get; set; }
}

public class Link
{
    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; }

    [JsonPropertyName("href")]
    public string Href { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Result
{
    [JsonPropertyName("data")]
    public ResultData Data { get; set; }
}

public class ResultData
{
    [JsonPropertyName("properties")]
    public Properties Properties { get; set; }
}

public class Properties
{
    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; }

    [JsonPropertyName("classification_id")]
    public int ClassificationId { get; set; }

    [JsonPropertyName("classification_name")]
    public string ClassificationName { get; set; }

    [JsonPropertyName("document_generation")]
    public object DocumentGeneration { get; set; } // Puede ser null

    [JsonPropertyName("location")]
    public string Location { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("required")]
    public int Required { get; set; }

    [JsonPropertyName("roles")]
    public string Roles { get; set; }

    [JsonPropertyName("rule_data")]
    public string RuleData { get; set; }

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }
}
