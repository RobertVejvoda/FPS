// API001: OpenAPI setup for FPS services.
// Each service calls builder.Services.AddOpenApi(version, options => { ... })
// directly in Program.cs to add service-specific security transformers,
// where Microsoft.AspNetCore.OpenApi and Microsoft.OpenApi model types are
// available from the web SDK framework reference.
//
// This file is intentionally minimal — it exists as a namespace marker only.
namespace FPS.SharedKernel.OpenApi;
