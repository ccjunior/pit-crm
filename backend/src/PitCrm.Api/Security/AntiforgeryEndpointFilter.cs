using Microsoft.AspNetCore.Antiforgery;

namespace PitCrm.Api.Security;

public sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new
            {
                error = "Proteção da solicitação inválida. Atualize a página e tente novamente."
            });
        }

        return await next(context);
    }
}
