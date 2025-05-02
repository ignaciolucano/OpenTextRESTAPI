using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics;


    public class NodeOperationFilter : IOperationFilter
    {
        private readonly IConfiguration _config;

        public NodeOperationFilter(IConfiguration config)
        {
            _config = config;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Get controller and action names from the context
            var actionDescriptor = context.ApiDescription.ActionDescriptor;
            var controllerName = actionDescriptor.RouteValues["controller"];
            var actionName = actionDescriptor.RouteValues["action"];

            // We assume your endpoint is in "NodesController" with the method "GetNode"
            if (controllerName == "Nodes" && actionName == "GetNode")
            {
                var summary = _config["Swagger:NodeGet:Summary"] ?? "Default Summary";
                var description = _config["Swagger:NodeGet:Description"] ?? "Default Description";

                operation.Summary = summary;
                operation.Description = description;
                Debug.WriteLine($"[DEBUG] NodeOperationFilter applied. Summary={summary}");
            } else if (controllerName == "Nodes" && actionName == "CreateDocumentNode")
            {
                var summary = _config["Swagger:CreateDocumentNode:Summary"] ?? "Default Summary";
                var description = _config["Swagger:CreateDocumentNode:Description"] ?? "Default Description";

                operation.Summary = summary;
                operation.Description = description;
                Debug.WriteLine($"[DEBUG] NodeOperationFilter applied. Summary={summary}");
            }
            else if (controllerName == "Nodes" && actionName == "DeleteNode")
            {
                var summary = _config["Swagger:NodeDelete:Summary"] ?? "Default Summary";
                var description = _config["Swagger:NodeDelete:Description"] ?? "Default Description";

                operation.Summary = summary;
                operation.Description = description;
                Debug.WriteLine($"[DEBUG] NodeOperationFilter applied. Summary={summary}");
            }
            else if (controllerName == "Auth" && actionName == "Login")
            {
                var summary = _config["Swagger:AuthLogin:Summary"] ?? "Default Summary";
                var description = _config["Swagger:AuthLogin:Description"] ?? "Default Description";

                operation.Summary = summary;
                operation.Description = description;
                Debug.WriteLine($"[DEBUG] NodeOperationFilter applied. Summary={summary}");
            }
    }
}

