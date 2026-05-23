using System.Reflection;
using Application.BackgroundJobs;
using Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>
/// Dependency Injection extension methods for the Application layer.
///
/// <para><b>What:</b>
/// Encapsulates all service registrations that belong to the Application layer of
/// Clean Architecture — MediatR (CQRS mediator), FluentValidation (request validation),
/// AutoMapper (entity↔DTO mapping), and their pipeline behaviors.</para>
///
/// <para><b>Why:</b>
/// Keeps Program.cs clean by grouping Application-layer registrations into a single
/// extension method. This follows the Separation of Concerns principle — each layer
/// is responsible for declaring its own dependencies.</para>
///
/// <para><b>Layer dependencies:</b>
/// Application depends only on Domain (entities, enums, value objects).
/// It does NOT depend on Infrastructure or WebApi — those layers depend on Application.</para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Application-layer services into the DI container.
    ///
    /// <para><b>Services registered:</b></para>
    /// <list type="bullet">
    ///   <item><b>MediatR</b> — Mediator pattern for CQRS (Commands, Queries, Notifications).
    ///   Scans the Application assembly for all <c>IRequestHandler</c> and <c>INotificationHandler</c>
    ///   implementations and registers them automatically.</item>
    ///   <item><b>MediatR Pipeline Behaviors</b> — Cross-cutting concerns injected into the
    ///   request pipeline: <c>ValidationBehavior</c> (runs FluentValidation before handlers)
    ///   and <c>LoggingBehavior</c> (logs request name, duration, errors).</item>
    ///   <item><b>FluentValidation</b> — Scans the Application assembly for all
    ///   <c>AbstractValidator&lt;T&gt;</c> implementations and registers them. These validators
    ///   are consumed by the <c>ValidationBehavior</c> pipeline.</item>
    ///   <item><b>AutoMapper</b> — Scans the Application assembly for all <c>Profile</c>
    ///   subclasses (e.g., AuthMappingProfile, PostMappingProfile) and registers them.
    ///   Uses the AutoMapper 16.x lambda-based configuration API.</item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // ── MediatR ─────────────────────────────────────────────────────────
        // Registers all IRequestHandler<,> and INotificationHandler<> implementations
        // found in the Application assembly. Also registers the pipeline behaviors
        // in order: Logging → Validation → Handler.
        // Order matters: LoggingBehavior wraps everything (including validation),
        // so we see timing even for requests that fail validation.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline Behavior 1: Logging — wraps ALL requests with timing and error logging.
            // Registered first so it captures the total time including validation.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));

            // Pipeline Behavior 2: Validation — runs FluentValidation validators before the handler.
            // If validation fails, a ValidationException is thrown and the handler never executes.
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // ── FluentValidation ────────────────────────────────────────────────
        // Scans the Application assembly for all classes extending AbstractValidator<T>
        // and registers them as IValidator<T> in the DI container (Scoped lifetime).
        // These are injected into ValidationBehavior<,> which runs them before each handler.
        services.AddValidatorsFromAssembly(assembly);

        // ── AutoMapper ──────────────────────────────────────────────────────
        // Scans the Application assembly for all Profile subclasses and registers them.
        // AutoMapper 16.x requires the lambda-based cfg.AddMaps() API.
        // This replaces the deprecated services.AddAutoMapper(assembly) overload.
        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(assembly);
        });

        services.AddScoped<ContentBackgroundJobDispatcher>();

        return services;
    }
}
