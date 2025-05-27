// ============================================================================
// File: SwaggerFilters.cs
// Description: Adds custom Swagger documentation metadata dynamically from appsettings.
// Author: Ignacio Lucano
// Date: 2025-05-14
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpenTextIntegrationAPI.Models.Filter
{
    #region Swagger Operation Filter

    /// <summary>
    /// Applies custom summaries, descriptions, and parameters to Swagger endpoints.
    /// Reads values from the "Swagger" section of appsettings.json.
    /// </summary>
    public class SwaggerFilters : IOperationFilter
    {
        private readonly IConfiguration _config;

        /// <summary>
        /// Constructor for injecting IConfiguration.
        /// </summary>
        /// <param name="config">The application configuration object.</param>
        public SwaggerFilters(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Applies dynamic Swagger metadata (summaries, descriptions, headers, etc.) to each endpoint.
        /// </summary>
        /// <param name="operation">The Swagger operation being processed.</param>
        /// <param name="context">The context for the operation.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Get controller and action names
            var controller = context.ApiDescription.ActionDescriptor.RouteValues["controller"];
            var action = context.ApiDescription.ActionDescriptor.RouteValues["action"];

            #region Endpoint Descriptions

            // Apply custom Swagger summaries/descriptions based on controller/action
            if (controller == "Nodes" && action == "GetNode")
            {
                ApplySummary(operation, "Swagger:NodeGet");
            }
            else if (controller == "Nodes" && action == "DeleteNode")
            {
                ApplySummary(operation, "Swagger:NodeDelete");
            }
            else if (controller == "Auth" && action == "Login")
            {
                ApplySummary(operation, "Swagger:AuthLogin");
            }
            else if (controller == "ChangeRequest" && action == "GetDocumentsChangeRequest")
            {
                ApplySummary(operation, "Swagger:ChangeRequestGet");
            }
            else if (controller == "ChangeRequest" && action == "UpdateChangeRequestData")
            {
                ApplySummary(operation, "Swagger:ChangeRequestUpdate");
            }
            else if (controller == "ChangeRequest" && action == "ApproveChangeRequest")
            {
                ApplySummary(operation, "Swagger:ChangeRequestApprove");
            }
            else if (controller == "ChangeRequest" && action == "RejectChangeRequest")
            {
                ApplySummary(operation, "Swagger:ChangeRequestReject");
            }
            else if (controller == "MasterData" && action == "GetMasterDataDocuments")
            {
                ApplySummary(operation, "Swagger:MasterDataGet");
            }
            else if (controller == "SimpleMDGAssets")
            {
                switch (action)
                {
                    case "UpsertGlobalLogo":
                        ApplySummary(operation, "Swagger:UpsertGlobalLogo");
                        break;
                    case "GetGlobalLogo":
                        ApplySummary(operation, "Swagger:GetGlobalLogo");
                        break;
                    case "DeleteGlobalLogo":
                        ApplySummary(operation, "Swagger:DeleteGlobalLogo");
                        break;
                    case "CreateBackgroundImage":
                        ApplySummary(operation, "Swagger:CreateBackgroundImage");
                        break;
                    case "GetBackgroundImageByName":
                        ApplySummary(operation, "Swagger:GetBackgroundImageByName");
                        break;
                    case "UpdateBackgroundImageByName":
                        ApplySummary(operation, "Swagger:UpdateBackgroundImageByName");
                        break;
                    case "ListBackgroundImages":
                        ApplySummary(operation, "Swagger:ListBackgroundImages");
                        break;
                    case "DeleteBackgroundImageByName":
                        ApplySummary(operation, "Swagger:DeleteBackgroundImageByName");
                        break;
                }
            }

            #endregion

            #region Multipart Body Override for CreateDocumentNode

            if (controller == "Nodes" && action == "CreateDocumentNode")
            {
                ApplySummary(operation, "Swagger:CreateDocumentNode");

                // Clear default parameter list
                //operation.Parameters.Clear();

                // Define a multipart/form-data schema
                //operation.RequestBody = new OpenApiRequestBody
                //{
                //    Required = true,
                //    Content = new Dictionary<string, OpenApiMediaType>
                //    {
                //        ["multipart/form-data"] = new OpenApiMediaType
                //        {
                //            Schema = new OpenApiSchema
                //            {
                //                Type = "object",
                //                Properties = new Dictionary<string, OpenApiSchema>
                //                {
                //                    ["body"] = new OpenApiSchema
                //                    {
                //                        Description = "JSON payload describing the node properties",
                //                        Type = "string",
                //                        Format = "json",
                //                        Example = new OpenApiString(
                //                            @"{
                //                              ""type"": 144,
                //                              ""parent_id"": ""12345"",
                //                              ""name"": ""mydoc.pdf"",
                //                              ""roles"": { ""98765_2"": ""2025-01-01T00:00:00"" }
                //                            }")
                //                    },
                //                    ["file"] = new OpenApiSchema
                //                    {
                //                        Description = "PDF file to upload",
                //                        Type = "string",
                //                        Format = "binary"
                //                    }
                //                },
                //                Required = new HashSet<string> { "body", "file" }
                //            }
                //        }
                //    }
                //};
            }

            #endregion

            #region Global Custom Headers

            // Add global trace ID header to all endpoints
            operation.Parameters ??= new List<OpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "SimpleMDG_TraceLogID",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional Trace ID for request tracking from SimpleMDG",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Example = new OpenApiString("AGgiCU8nuGvXDyArMzaRvx1DSkti")
                }
            });

            #endregion
        }

        /// <summary>
        /// Applies summary and description from appsettings using a configuration key.
        /// </summary>
        private void ApplySummary(OpenApiOperation operation, string configKey)
        {
            operation.Summary = _config[$"{configKey}:Summary"] ?? operation.Summary;
            operation.Description = _config[$"{configKey}:Description"] ?? operation.Description;
        }
    }

    #endregion
}
