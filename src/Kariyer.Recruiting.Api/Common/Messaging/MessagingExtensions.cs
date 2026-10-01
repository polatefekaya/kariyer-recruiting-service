using Kariyer.Recruiting.Api.Common.Configuration;
using Kariyer.Recruiting.Api.Common.Persistence;
using Kariyer.Recruiting.Domain.Ports;
using Kariyer.Messaging.Contracts.Recruiting;
using MassTransit;

namespace Kariyer.Recruiting.Api.Common.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddRecruitingMessaging(
        this IServiceCollection services, IConfiguration configuration, string? rabbitConnection)
    {
        MessagingOptions messaging = new();
        configuration.GetSection(MessagingOptions.SectionName).Bind(messaging);

        if (!messaging.Enabled || string.IsNullOrWhiteSpace(rabbitConnection))
        {
            services.AddSingleton<IIntegrationEventPublisher, NoOpEventPublisher>();
            return services;
        }

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            // Outbox: an event is staged in the same transaction as the pipeline row, so a
            // committed stage change cannot fail to be announced and a rolled back one cannot
            // announce itself.
            bus.AddEntityFrameworkOutbox<RecruitingDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitConnection));

                // Exchange names are part of the contract, not an artefact of the .NET type name:
                // consumers bind to these by hand, and the platform's other publishers — some of
                // them Node — use the same dotted convention.
                cfg.Message<ApplicationStageChangedEvent>(m => m.SetEntityName("recruiting.application.stage_changed"));
                cfg.Message<InterviewInvitedEvent>(m => m.SetEntityName("recruiting.interview.invited"));
                cfg.Message<InterviewRescheduledEvent>(m => m.SetEntityName("recruiting.interview.rescheduled"));
                cfg.Message<InterviewCancelledEvent>(m => m.SetEntityName("recruiting.interview.cancelled"));
                cfg.Message<InterviewAnsweredEvent>(m => m.SetEntityName("recruiting.interview.answered"));

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();

        return services;
    }
}

public sealed class MassTransitEventPublisher(IPublishEndpoint publisher) : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class =>
        publisher.Publish(message, cancellationToken);
}

public sealed class NoOpEventPublisher : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : class =>
        Task.CompletedTask;
}
