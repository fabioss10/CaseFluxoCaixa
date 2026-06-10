using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FluxoCaixa.Api.Infrastructure.OpenApi
{
    public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var jwtScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Cole APENAS o seu token JWT abaixo (sem a palavra Bearer)."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["Bearer"] = jwtScheme;

            var requirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            };

            document.Security = new List<OpenApiSecurityRequirement> { requirement };

            document.SetReferenceHostDocument();

            return Task.CompletedTask;
        }
    }
}
