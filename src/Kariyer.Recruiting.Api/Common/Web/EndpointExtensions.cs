using System.Reflection;

namespace Kariyer.Recruiting.Api.Common.Web;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        ServiceDescriptor[] endpoints = Assembly.GetExecutingAssembly()
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerableRange(endpoints);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app, string prefix)
    {
        RouteGroupBuilder group = app.MapGroup(prefix);

        foreach (IEndpoint endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(group);
        }

        return app;
    }

    private static void TryAddEnumerableRange(this IServiceCollection services, IEnumerable<ServiceDescriptor> items)
    {
        foreach (ServiceDescriptor descriptor in items)
        {
            services.Add(descriptor);
        }
    }
}
