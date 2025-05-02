    using Microsoft.Extensions.Configuration;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Diagnostics;


        public class ChangeRequestOperationFilter : IOperationFilter
        {
            private readonly IConfiguration _config;

            public ChangeRequestOperationFilter(IConfiguration config)
            {
                _config = config;
            }

            public void Apply(OpenApiOperation operation, OperationFilterContext context)
            {
                var actionDescriptor = context.ApiDescription.ActionDescriptor;
                var controllerName = actionDescriptor.RouteValues["controller"];
                var actionName = actionDescriptor.RouteValues["action"];

                // We assume your endpoint is in "ChangeRequestController" 
                // with the method "GetChangeRequestDocuments" 
                // or whichever naming you use.
                if (controllerName == "ChangeRequest" && actionName == "GetDocumentsChangeRequest")
                {
                    var summary = _config["Swagger:ChangeRequestGet:Summary"] ?? "Default Summary";
                    var description = _config["Swagger:ChangeRequestGet:Description"] ?? "Default Description";

                    operation.Summary = summary;
                    operation.Description = description;
                    Debug.WriteLine($"[DEBUG] ChangeRequestOperationFilter applied. Summary={summary}");
                }
                else if (controllerName == "ChangeRequest" && actionName == "UpdateChangeRequestData")
                {
                    var summary = _config["Swagger:ChangeRequestUpdate:Summary"] ?? "Default Summary";
                    var description = _config["Swagger:ChangeRequestUpdate:Description"] ?? "Default Description";

                    operation.Summary = summary;
                    operation.Description = description;
                    Debug.WriteLine($"[DEBUG] ChangeRequestOperationFilter applied. Summary={summary}");
                } else if (controllerName == "MasterData" && actionName == "GetMasterDataDocuments")
                {
               
                    operation.Summary = _config["Swagger:MasterDataGet:Summary"] ?? "Default Summary";
                    operation.Description = _config["Swagger:MasterDataGet:Description"] ?? "Default Description";
                }
    }
        }

