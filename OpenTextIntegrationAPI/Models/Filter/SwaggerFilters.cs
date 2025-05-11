using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

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
            var controller = context.ApiDescription.ActionDescriptor.RouteValues["controller"];
            var action = context.ApiDescription.ActionDescriptor.RouteValues["action"];

            // GET /v1/Nodes/{id}
            if (controller == "Nodes" && action == "GetNode")
            {
                operation.Summary = _config["Swagger:NodeGet:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:NodeGet:Description"] ?? operation.Description;
            }
            // DELETE /v1/Nodes/{nodeId}
            else if (controller == "Nodes" && action == "DeleteNode")
            {
                operation.Summary = _config["Swagger:NodeDelete:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:NodeDelete:Description"] ?? operation.Description;
            }
            // POST /api/Auth/login
            else if (controller == "Auth" && action == "Login")
            {
                operation.Summary = _config["Swagger:AuthLogin:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:AuthLogin:Description"] ?? operation.Description;
            }
            // GET /v1/ChangeRequest/{id}/documents
            else if (controller == "ChangeRequest" && action == "GetDocumentsChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestGet:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:ChangeRequestGet:Description"] ?? operation.Description;
            }
            // PUT /v1/ChangeRequest/{id}
            else if (controller == "ChangeRequest" && action == "UpdateChangeRequestData")
            {
                operation.Summary = _config["Swagger:ChangeRequestUpdate:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:ChangeRequestUpdate:Description"] ?? operation.Description;
            }
            // POST /v1/ChangeRequest/{id}/approve
            else if (controller == "ChangeRequest" && action == "ApproveChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestApprove:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:ChangeRequestApprove:Description"] ?? operation.Description;
            }
            // POST /v1/ChangeRequest/{id}/reject
            else if (controller == "ChangeRequest" && action == "RejectChangeRequest")
            {
                operation.Summary = _config["Swagger:ChangeRequestReject:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:ChangeRequestReject:Description"] ?? operation.Description;
            }
            // GET /v1/MasterData/documents
            else if (controller == "MasterData" && action == "GetMasterDataDocuments")
            {
                operation.Summary = _config["Swagger:MasterDataGet:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:MasterDataGet:Description"] ?? operation.Description;
            }
            // SimpleMDGAssets endpoints...
            else if (controller == "SimpleMDGAssets")
            {
                switch (action)
                {
                    case "UpsertGlobalLogo":
                        operation.Summary = _config["Swagger:UpsertGlobalLogo:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:UpsertGlobalLogo:Description"] ?? operation.Description;
                        break;
                    case "GetGlobalLogo":
                        operation.Summary = _config["Swagger:GetGlobalLogo:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:GetGlobalLogo:Description"] ?? operation.Description;
                        break;
                    case "DeleteGlobalLogo":
                        operation.Summary = _config["Swagger:DeleteGlobalLogo:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:DeleteGlobalLogo:Description"] ?? operation.Description;
                        break;
                    case "CreateBackgroundImage":
                        operation.Summary = _config["Swagger:CreateBackgroundImage:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:CreateBackgroundImage:Description"] ?? operation.Description;
                        break;
                    case "GetBackgroundImageByName":
                        operation.Summary = _config["Swagger:GetBackgroundImageByName:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:GetBackgroundImageByName:Description"] ?? operation.Description;
                        break;
                    case "UpdateBackgroundImageByName":
                        operation.Summary = _config["Swagger:UpdateBackgroundImageByName:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:UpdateBackgroundImageByName:Description"] ?? operation.Description;
                        break;
                    case "ListBackgroundImages":
                        operation.Summary = _config["Swagger:ListBackgroundImages:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:ListBackgroundImages:Description"] ?? operation.Description;
                        break;
                    case "DeleteBackgroundImageByName":
                        operation.Summary = _config["Swagger:DeleteBackgroundImageByName:Summary"] ?? operation.Summary;
                        operation.Description = _config["Swagger:DeleteBackgroundImageByName:Description"] ?? operation.Description;
                        break;
                }
            }

            // POST /v1/Nodes/create
            if (controller == "Nodes" && action == "CreateDocumentNodeAsync")
            {
                operation.Summary = _config["Swagger:CreateDocumentNode:Summary"] ?? operation.Summary;
                operation.Description = _config["Swagger:CreateDocumentNode:Description"] ?? operation.Description;

                // remove auto-generated query/form parameters
                operation.Parameters.Clear();

                // inject multipart/form-data request body
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, OpenApiSchema>
                                {
                                    ["body"] = new OpenApiSchema
                                    {
                                        Description = "JSON payload describing the node properties",
                                        Type = "string",
                                        Format = "json",
                                        Example = new OpenApiString(
@"{
  ""type"": 144,
  ""parent_id"": ""12345"",
  ""name"": ""mydoc.pdf"",
  ""roles"": { ""98765_2"": ""2025-01-01T00:00:00"" }
}")
                                    },
                                    ["file"] = new OpenApiSchema
                                    {
                                        Description = "PDF file to upload",
                                        Type = "string",
                                        Format = "binary"
                                    }
                                },
                                Required = new HashSet<string> { "body", "file" }
                            }
                        }
                    }
                };
            }
        }
    }
}
