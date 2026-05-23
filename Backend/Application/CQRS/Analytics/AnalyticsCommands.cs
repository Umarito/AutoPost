using Application.Common;
using MediatR;

namespace Application.CQRS.Analytics;

/// <summary>
/// Collects a new analytics snapshot for a published post target.
/// </summary>
/// <param name="PostTargetId">Published target whose metrics should be sampled.</param>
public sealed record CollectPostSnapshotCommand(Guid PostTargetId) : IRequest<Result>;
