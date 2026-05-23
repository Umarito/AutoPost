// Infrastructure/Configurations/WebhookEventConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the WebhookEvent entity.
/// Infrastructure entity — buffer for incoming webhook events from social platforms.
/// TRD: "Critical rule: respond 200 OK within 200ms. Save RawPayload immediately, process in background."
/// </summary>
public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");
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
        builder.Property(we => we.RawPayload)
            .IsRequired();

        // Cryptographic signature for verification (X-Hub-Signature-256).
        builder.Property(we => we.Signature).HasMaxLength(500);

        // Verification result — false means the event was rejected as spoofed.
        // TRD Security: "Webhook spoofing → HMAC-SHA256 verification on every incoming webhook."
        builder.Property(we => we.IsVerified)
            .IsRequired()
            .HasDefaultValue(false);

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

        builder.Property(we => we.ProcessedAt);

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
