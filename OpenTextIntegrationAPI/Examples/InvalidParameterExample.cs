using Swashbuckle.AspNetCore.Filters;
using OpenTextIntegrationAPI.Models; // Si necesitás referenciar modelos, o usá uno propio

public class InvalidParameterExample : IExamplesProvider<string>
{
    public string GetExamples()
    {
        // Ejemplo de mensaje de error para 400
        return "Invalid parameter value: boType must be one of [BUS1006, BUS1001001, BUS1001006].";
    }
}
