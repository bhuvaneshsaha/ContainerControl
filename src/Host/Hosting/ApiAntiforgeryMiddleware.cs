using Microsoft.AspNetCore.Antiforgery;

namespace ContainerControl.Host.Hosting;

public static class ApiAntiforgeryMiddleware
{
    public static IApplicationBuilder UseApiAntiforgery(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/delivery/webhook"))
            {
                await next();
                return;
            }

            if (HttpMethods.IsGet(context.Request.Method)
                || HttpMethods.IsHead(context.Request.Method)
                || HttpMethods.IsOptions(context.Request.Method)
                || HttpMethods.IsTrace(context.Request.Method))
            {
                await next();
                return;
            }

            try
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "The anti-forgery token is missing or invalid.",
                    status = StatusCodes.Status400BadRequest
                });
                return;
            }

            await next();
        });
    }
}
