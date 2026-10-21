using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LibraryApi.Infrastructure;

/// <summary>OpenAPI отражает реальные статусы CRUD и авторизацию каждого метода.</summary>
public sealed class ApiDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var authorized = metadata.OfType<AuthorizeAttribute>().Any() && !metadata.OfType<AllowAnonymousAttribute>().Any();
        if (authorized)
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
            }];
            AddProblem("401", "Нет действительного токена");
            if (metadata.OfType<AuthorizeAttribute>().Any(a => !string.IsNullOrEmpty(a.Roles)))
                AddProblem("403", "Недостаточно прав");
        }
        else operation.Security = [];

        var method = context.ApiDescription.HttpMethod;
        var creates = method == "POST" && context.MethodInfo.Name == "Create";
        if (creates && operation.Responses.TryGetValue("200", out var response))
        {
            operation.Responses.Remove("200");
            response.Description = "Создано";
            response.Headers["Location"] = new OpenApiHeader { Description = "URI созданного ресурса", Schema = new OpenApiSchema { Type = "string" } };
            operation.Responses["201"] = response;
        }
        if (method == "DELETE")
        {
            operation.Responses.Remove("200");
            operation.Responses["204"] = new OpenApiResponse { Description = "Удалено, тело отсутствует" };
        }
        AddProblem("400", "Некорректные входные данные");
        if (context.ApiDescription.RelativePath?.Contains("{id}", StringComparison.Ordinal) == true || context.MethodInfo.Name == "Create")
            AddProblem("404", "Связанный или запрошенный ресурс отсутствует");
        if (method is "POST" or "PUT" or "DELETE") AddProblem("409", "Конфликт состояния или уникальности");
        return;

        void AddProblem(string status, string description) => operation.Responses.TryAdd(status, new OpenApiResponse
        {
            Description = description,
            Content = { ["application/problem+json"] = new OpenApiMediaType
                { Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository) } }
        });
    }
}
