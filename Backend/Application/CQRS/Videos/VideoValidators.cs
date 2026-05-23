using FluentValidation;

namespace Application.CQRS.Videos;

/// <summary>
/// Validates upload session initialization input.
/// </summary>
public sealed class InitVideoUploadCommandValidator : AbstractValidator<InitVideoUploadCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="InitVideoUploadCommand"/>.
    /// </summary>
    public InitVideoUploadCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.FileName).NotEmpty();
        RuleFor(x => x.Request.ContentType).NotEmpty();
        RuleFor(x => x.Request.FileSizeBytes).GreaterThan(0);
    }
}

/// <summary>
/// Validates chunk upload input.
/// </summary>
public sealed class UploadVideoChunkCommandValidator : AbstractValidator<UploadVideoChunkCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="UploadVideoChunkCommand"/>.
    /// </summary>
    public UploadVideoChunkCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
        RuleFor(x => x.ChunkIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Content).NotNull();
    }
}

/// <summary>
/// Validates upload completion input.
/// </summary>
public sealed class CompleteVideoUploadCommandValidator : AbstractValidator<CompleteVideoUploadCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="CompleteVideoUploadCommand"/>.
    /// </summary>
    public CompleteVideoUploadCommandValidator()
    {
        RuleFor(x => x.UploadId).NotEmpty();
    }
}

/// <summary>
/// Validates requests that operate on an existing video.
/// </summary>
public sealed class ProcessVideoCommandValidator : AbstractValidator<ProcessVideoCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="ProcessVideoCommand"/>.
    /// </summary>
    public ProcessVideoCommandValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
    }
}

/// <summary>
/// Validates soft-delete requests for videos.
/// </summary>
public sealed class DeleteVideoCommandValidator : AbstractValidator<DeleteVideoCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="DeleteVideoCommand"/>.
    /// </summary>
    public DeleteVideoCommandValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
    }
}

/// <summary>
/// Validates thumbnail generation requests.
/// </summary>
public sealed class GenerateVideoThumbnailCommandValidator : AbstractValidator<GenerateVideoThumbnailCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="GenerateVideoThumbnailCommand"/>.
    /// </summary>
    public GenerateVideoThumbnailCommandValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
    }
}

/// <summary>
/// Validates metadata persistence requests.
/// </summary>
public sealed class SetVideoMetadataCommandValidator : AbstractValidator<SetVideoMetadataCommand>
{
    /// <summary>
    /// Configures validation rules for <see cref="SetVideoMetadataCommand"/>.
    /// </summary>
    public SetVideoMetadataCommandValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
        RuleFor(x => x.Metadata).NotNull();
    }
}

/// <summary>
/// Validates paginated video list queries.
/// </summary>
public sealed class GetVideosPagedQueryValidator : AbstractValidator<GetVideosPagedQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetVideosPagedQuery"/>.
    /// </summary>
    public GetVideosPagedQueryValidator()
    {
        RuleFor(x => x.Pagination.Page).GreaterThan(0);
        RuleFor(x => x.Pagination.PageSize).GreaterThan(0).LessThanOrEqualTo(200);
    }
}

/// <summary>
/// Validates video detail queries.
/// </summary>
public sealed class GetVideoByIdQueryValidator : AbstractValidator<GetVideoByIdQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="GetVideoByIdQuery"/>.
    /// </summary>
    public GetVideoByIdQueryValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
    }
}

/// <summary>
/// Validates platform compatibility queries for uploaded videos.
/// </summary>
public sealed class ValidateVideoForPlatformsQueryValidator : AbstractValidator<ValidateVideoForPlatformsQuery>
{
    /// <summary>
    /// Configures validation rules for <see cref="ValidateVideoForPlatformsQuery"/>.
    /// </summary>
    public ValidateVideoForPlatformsQueryValidator()
    {
        RuleFor(x => x.VideoId).NotEmpty();
        RuleFor(x => x.Platforms).NotNull().Must(platforms => platforms.Count > 0);
    }
}
