using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;


namespace OpenTextIntegrationAPI.Models.Filter 
{
    public class SwaggerFilters : IOperationFilter
    {
        private readonly IConfiguration _config;

        public SwaggerFilters(IConfiguration config)
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
                operation.Summary = _config["Swagger:NodeGet:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:NodeGet:Description"] ?? "Default Description";
            }
            else if (controllerName == "Nodes" && actionName == "CreateDocumentNode")
            {
                operation.Summary = _config["Swagger:CreateDocumentNode:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:CreateDocumentNode:Description"] ?? "Default Description";
            }
            else if (controllerName == "Nodes" && actionName == "DeleteNode")
            {
                operation.Summary = _config["Swagger:NodeDelete:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:NodeDelete:Description"] ?? "Default Description";
            }
            else if (controllerName == "Auth" && actionName == "Login")
            {
                operation.Summary = _config["Swagger:AuthLogin:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:AuthLogin:Description"] ?? "Default Description";
            } else if (controllerName == "ChangeRequest" && actionName == "GetDocumentsChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestGet:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestGet:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "UpdateChangeRequestData")
            {
                operation.Summary = _config["Swagger:ChangeRequestUpdate:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestUpdate:Description"] ?? "Default Description";
            }
            else if (controllerName == "MasterData" && actionName == "GetMasterDataDocuments")
            {
                operation.Summary = _config["Swagger:MasterDataGet:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:MasterDataGet:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "ApproveChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestApprove:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestApprove:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "RejectChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestReject:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestReject:Description"] ?? "Default Description";
            }
            // We assume your endpoint is in "NodesController" with the method "GetNode"
            if (controllerName == "Nodes" && actionName == "GetNode")
            {
                operation.Summary = _config["Swagger:NodeGet:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:NodeGet:Description"] ?? "Default Description";
            }
            else if (controllerName == "Nodes" && actionName == "CreateDocumentNode")
            {
                operation.Summary = _config["Swagger:CreateDocumentNode:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:CreateDocumentNode:Description"] ?? "Default Description";
            }
            else if (controllerName == "Nodes" && actionName == "DeleteNode")
            {
                operation.Summary = _config["Swagger:NodeDelete:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:NodeDelete:Description"] ?? "Default Description";
            }
            else if (controllerName == "Auth" && actionName == "Login")
            {
                operation.Summary = _config["Swagger:AuthLogin:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:AuthLogin:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "GetDocumentsChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestGet:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestGet:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "UpdateChangeRequestData")
            {
                operation.Summary = _config["Swagger:ChangeRequestUpdate:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestUpdate:Description"] ?? "Default Description";
            }
            else if (controllerName == "MasterData" && actionName == "GetMasterDataDocuments")
            {
                operation.Summary = _config["Swagger:MasterDataGet:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:MasterDataGet:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "ApproveChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestApprove:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestApprove:Description"] ?? "Default Description";
            }
            else if (controllerName == "ChangeRequest" && actionName == "RejectChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestReject:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ChangeRequestReject:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "UpsertGlobalLogo")
            {
                operation.Summary = _config["Swagger:UpsertGlobalLogo:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:UpsertGlobalLogo:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "GetGlobalLogo")
            {
                operation.Summary = _config["Swagger:GetGlobalLogo:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:GetGlobalLogo:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "DeleteGlobalLogo")
            {
                operation.Summary = _config["Swagger:DeleteGlobalLogo:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:DeleteGlobalLogo:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "CreateBackgroundImage")
            {
                operation.Summary = _config["Swagger:CreateBackgroundImage:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:CreateBackgroundImage:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "GetBackgroundImageByName")
            {
                operation.Summary = _config["Swagger:GetBackgroundImageByName:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:GetBackgroundImageByName:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "UpdateBackgroundImageByName")
            {
                operation.Summary = _config["Swagger:UpdateBackgroundImageByName:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:UpdateBackgroundImageByName:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "ListBackgroundImages")
            {
                operation.Summary = _config["Swagger:ListBackgroundImages:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:ListBackgroundImages:Description"] ?? "Default Description";
            }
            else if (controllerName == "SimpleMDGAssets" && actionName == "DeleteBackgroundImageByName")
            {
                operation.Summary = _config["Swagger:DeleteBackgroundImageByName:Summary"] ?? "Default Summary";
                operation.Description = _config["Swagger:DeleteBackgroundImageByName:Description"] ?? "Default Description";
            }
        }
    }
}

