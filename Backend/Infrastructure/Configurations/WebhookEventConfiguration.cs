// Infrastructure/Configurations/WebhookEventConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="WebhookEvent"/> entity.
/// </summary>
/// <remarks>
/// <para><b>Core Definition:</b> Configures the database schema mappings for incoming webhooks.</para>
/// <para><b>Business Justification:</b> Buffer for incoming webhook events from social platforms. 
/// TRD: "Critical rule: respond 200 OK within 200ms. Save RawPayload immediately, process in background."</para>
/// <para><b>Execution and Project Impact:</b> Essential for system resilience. Allows reprocessing events if background operations fail.</para>
/// </remarks>
public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        // Table Name mapping
        builder.ToTable("WebhookEvents");

        // Primary Key definition
        builder.HasKey(we => we.Id);

        // Platform source of the event.
        builder.Property(we => we.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Event type string: "comment", "message", "follow", "mention".
        builder.Property(we => we.EventType)
            .IsRequired()
            .HasMaxLength(200);

        // Raw JSON payload — saved immediately before any processing.
        // TRD: "Allows re-processing the event on failure."
        // We explicitly use the PostgreSQL "text" column type (unbounded length) because webhook payloads from 
        // social platforms (e.g., Meta/Instagram) can be very large (exceeding 100KB+) and highly unpredictable. 
        // Applying a strict max length would risk failing to store valid webhooks, which violates reliability standards.
        builder.Property(we => we.RawPayload)
            .IsRequired()
            .HasColumnType("text");

        // Cryptographic signature for verification (X-Hub-Signature-256).
        builder.Property(we => we.Signature).HasMaxLength(500);

        // Verification result — false means the event was rejected as spoofed.
        // TRD Security: "Webhook spoofing → HMAC-SHA256 verification on every incoming webhook."
        builder.Property(we => we.IsVerified)
            .IsRequired()
            .HasDefaultValue(false);

        // Temporal tracking
        builder.Property(we => we.ReceivedAt).IsRequired();

        // Processing status: Received, Processing, Processed, Failed, Ignored.
        builder.Property(we => we.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Retry counter for Hangfire retry logic.
        builder.Property(we => we.ProcessingAttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Temporal processing markers
        builder.Property(we => we.ProcessedAt);

        // Fail/Error logs
        builder.Property(we => we.ProcessingError).HasMaxLength(4000);

        // ── Indexes ─────────────────────────────────────────────────────────

        // Critical index for Hangfire background processing pipeline.
        // Finds all Received events that need processing, ordered by arrival time.
        builder.HasIndex(we => new { we.Status, we.ReceivedAt })
            .HasDatabaseName("IX_WebhookEvents_Status_ReceivedAt");

        // Performance: filtering events by platform for monitoring dashboards.
        builder.HasIndex(we => we.Platform)
            .HasDatabaseName("IX_WebhookEvents_Platform");

        // ── Relationships ───────────────────────────────────────────────────
        // No navigation properties — this is an infrastructure entity isolated from the domain per TRD.
    }
}

