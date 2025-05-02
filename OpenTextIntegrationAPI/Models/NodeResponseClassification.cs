using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class NodeResponseClassifications
{
    [JsonPropertyName("bCanApplyClass")]
    public bool BCanApplyClass { get; set; }

    [JsonPropertyName("bCanRemoveClass")]
    public bool BCanRemoveClass { get; set; }

    [JsonPropertyName("canModify")]
    public bool CanModify { get; set; }

    [JsonPropertyName("classVolumeID")]
    public int ClassVolumeID { get; set; }

    [JsonPropertyName("data")]
    public List<DocumentData> Data { get; set; }

    [JsonPropertyName("definitions")]
    public Definitions Definitions { get; set; }

    [JsonPropertyName("definitions_map")]
    public Dictionary<string, List<string>> DefinitionsMap { get; set; }

    [JsonPropertyName("definitions_order")]
    public List<string> DefinitionsOrder { get; set; }
}

public class DocumentData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("selectable")]
    public bool Selectable { get; set; }

    [JsonPropertyName("management_type")]
    public string ManagementType { get; set; }

    [JsonPropertyName("score")]
    public object Score { get; set; } // Puede ser null o algún tipo específico

    [JsonPropertyName("inherit_flag")]
    public bool InheritFlag { get; set; }

    [JsonPropertyName("classvolumeid")]
    public int? ClassVolumeId { get; set; }

    [JsonPropertyName("parent_managed")]
    public bool? ParentManaged { get; set; }

    [JsonPropertyName("cell_metadata")]
    public CellMetadata CellMetadata { get; set; }

    [JsonPropertyName("menu")]
    public object Menu { get; set; } // Puede ser null o algún tipo específico
}

public class CellMetadata
{
    [JsonPropertyName("data")]
    public CellMetadataData Data { get; set; }

    [JsonPropertyName("definitions")]
    public CellMetadataDefinitions Definitions { get; set; }
}

public class CellMetadataData
{
    [JsonPropertyName("menu")]
    public string Menu { get; set; }
}

public class CellMetadataDefinitions
{
    [JsonPropertyName("menu")]
    public MenuDefinition Menu { get; set; }
}

public class MenuDefinition
{
    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; }

    [JsonPropertyName("display_hint")]
    public string DisplayHint { get; set; }

    [JsonPropertyName("display_href")]
    public string DisplayHref { get; set; }

    [JsonPropertyName("handler")]
    public string Handler { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; }

    [JsonPropertyName("tab_href")]
    public string TabHref { get; set; }
}

public class Definitions
{
    [JsonPropertyName("classvolumeid")]
    public DefinitionDetail ClassVolumeId { get; set; }

    [JsonPropertyName("id")]
    public DefinitionDetail Id { get; set; }

    [JsonPropertyName("inherit_flag")]
    public DefinitionDetail InheritFlag { get; set; }

    [JsonPropertyName("management_type")]
    public DefinitionDetail ManagementType { get; set; }

    [JsonPropertyName("name")]
    public DefinitionDetail Name { get; set; }

    [JsonPropertyName("parent_managed")]
    public DefinitionDetail ParentManaged { get; set; }

    [JsonPropertyName("score")]
    public DefinitionDetail Score { get; set; }

    [JsonPropertyName("selectable")]
    public DefinitionDetail Selectable { get; set; }

    [JsonPropertyName("type")]
    public DefinitionDetail Type { get; set; }
}

public class DefinitionDetail
{
    [JsonPropertyName("allow_undefined")]
    public bool AllowUndefined { get; set; }

    [JsonPropertyName("bulk_shared")]
    public bool BulkShared { get; set; }

    [JsonPropertyName("default_value")]
    public object DefaultValue { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; }

    [JsonPropertyName("key_value_pairs")]
    public bool KeyValuePairs { get; set; }

    [JsonPropertyName("max_value")]
    public int? MaxValue { get; set; }

    [JsonPropertyName("min_value")]
    public int? MinValue { get; set; }

    [JsonPropertyName("multi_value")]
    public bool MultiValue { get; set; }

    [JsonPropertyName("multi_value_max_length")]
    public int? MultiValueMaxLength { get; set; }

    [JsonPropertyName("multi_value_min_length")]
    public int? MultiValueMinLength { get; set; }

    [JsonPropertyName("multi_value_unique")]
    public bool MultiValueUnique { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("persona")]
    public string Persona { get; set; }

    [JsonPropertyName("read_only")]
    public bool ReadOnly { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; }

    [JsonPropertyName("valid_values")]
    public List<object> ValidValues { get; set; }

    [JsonPropertyName("valid_values_name")]
    public List<object> ValidValuesName { get; set; }

    // Propiedades adicionales (presentes en algunos elementos, como management_type)
    [JsonPropertyName("max_length")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("min_length")]
    public int? MinLength { get; set; }

    [JsonPropertyName("multi_select")]
    public bool? MultiSelect { get; set; }

    [JsonPropertyName("multiline")]
    public bool? Multiline { get; set; }

    [JsonPropertyName("multilingual")]
    public bool? Multilingual { get; set; }

    [JsonPropertyName("password")]
    public bool? Password { get; set; }

    [JsonPropertyName("Regex")]
    public string Regex { get; set; }
}
