using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class MasterDataOperationFilter : IOperationFilter
{
    private readonly IConfiguration _config;
    public MasterDataOperationFilter(IConfiguration config) => _config = config;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var controllerName = context.ApiDescription.ActionDescriptor.RouteValues["controller"];
        var actionName = context.ApiDescription.ActionDescriptor.RouteValues["action"];

        if (controllerName == "MasterData" && actionName == "GetMasterDataDocuments")
        {
            operation.Summary = _config["Swagger:MasterDataGet:Summary"] ?? "Default Summary";
            operation.Description = _config["Swagger:MasterDataGet:Description"] ?? "Default Description";
        }
    }
}