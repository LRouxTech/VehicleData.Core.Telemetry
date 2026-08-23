namespace LodewykRoux.Core.Api.Middleware;

public class ApiKeyMiddleware(RequestDelegate next)
{
    private const string ApiKeyHeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }
        
        string? expectedApiKey = configuration["ApiKey"];

        if (string.IsNullOrEmpty(expectedApiKey))
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("API Key configuration is missing on server.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key header missing.");
            return;
        }

        if (!expectedApiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Invalid API Key.");
            return;
        }

        await next(context);
    }
}