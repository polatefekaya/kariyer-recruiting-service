using System.Reflection;

namespace Kariyer.Recruiting.Api.Common.Web;

public static class HandlerExtensions
{
    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services)
    {
        foreach (TypeInfo handler in Assembly.GetExecutingAssembly().DefinedTypes.Where(IsHandler))
        {
            services.AddScoped(handler);
        }

        return services;
    }

    private static bool IsHandler(TypeInfo type) =>
        type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
        && type.Name.EndsWith("Handler", StringComparison.Ordinal)
        && type.Namespace?.Contains(".Features.", StringComparison.Ordinal) == true;
}
