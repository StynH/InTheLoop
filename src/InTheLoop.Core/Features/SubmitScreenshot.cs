using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Services;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

/// <summary>The AI posts a screenshot: a new review, or the next revision of an existing one.</summary>
public sealed record SubmitScreenshot : IRequest<Review>
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? ImagePath { get; init; }
    public string? ImageBase64 { get; init; }
    public string? ReviewId { get; init; }
    public string? Source { get; init; }

    /// <summary>Id or name of the project the review belongs to.</summary>
    public string? Project { get; init; }
}

internal sealed class SubmitScreenshotHandler(IReviewStore store, IProjectStore projects, TimeProvider clock) : IRequestHandler<SubmitScreenshot, Review>
{
    public async Task<Review> HandleAsync(SubmitScreenshot request, CancellationToken cancellationToken)
    {
        var image = await ReadImageAsync(request, cancellationToken);
        var extension = ImageInspector.DetectExtension(image)
            ?? throw new RequestException("The image is not a PNG, JPEG, GIF, BMP or WebP file.");

        var project = string.IsNullOrWhiteSpace(request.Project)
            ? null
            : await projects.RequireAsync(request.Project, cancellationToken);

        var now = clock.GetUtcNow();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (!string.IsNullOrWhiteSpace(request.ReviewId))
        {
            var id = request.ReviewId.Trim();
            var existing = await store.GetAsync(id, cancellationToken)
                ?? throw new RequestException($"Review '{id}' does not exist. Omit review_id to start a new review.");

            var revision = existing.RevisionCount + 1;
            var file = $"revision-{revision}-{Ids.NewEntryId()}{extension}";
            await store.SaveImageAsync(id, file, image, cancellationToken);

            return await store.UpdateAsync(id, review => review with
            {
                Title = string.IsNullOrWhiteSpace(request.Title) ? review.Title : request.Title.Trim(),
                ProjectId = project?.Id ?? review.ProjectId,
                Status = ReviewStatus.Pending,
                UpdatedAt = now,
                Entries = [.. review.Entries, Screenshot(review.RevisionCount + 1, file, description, now)]
            }, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new RequestException("A title is required when starting a new review.");
        }

        var reviewId = Ids.NewReviewId();
        var fileName = $"revision-1-{Ids.NewEntryId()}{extension}";
        await store.SaveImageAsync(reviewId, fileName, image, cancellationToken);

        return await store.CreateAsync(new Review
        {
            Id = reviewId,
            Title = request.Title.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "AI" : request.Source.Trim(),
            ProjectId = project?.Id,
            Status = ReviewStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            Entries = [Screenshot(1, fileName, description, now)]
        }, cancellationToken);
    }

    private static ReviewEntry Screenshot(int revision, string file, string? description, DateTimeOffset now) => new()
    {
        Id = Ids.NewEntryId(),
        Kind = EntryKind.Screenshot,
        Author = Author.Ai,
        Text = description,
        ImageFile = file,
        Revision = revision,
        CreatedAt = now
    };

    private static async Task<byte[]> ReadImageAsync(SubmitScreenshot request, CancellationToken cancellationToken)
    {
        var hasPath = !string.IsNullOrWhiteSpace(request.ImagePath);
        var hasData = !string.IsNullOrWhiteSpace(request.ImageBase64);

        if (hasPath == hasData)
        {
            throw new RequestException("Provide exactly one of image_path or image_base64.");
        }

        byte[] bytes;
        if (hasPath)
        {
            var path = request.ImagePath!.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(path))
            {
                throw new RequestException($"image_path must be an absolute path, got '{path}'.");
            }

            if (!File.Exists(path))
            {
                throw new RequestException($"No file exists at '{path}'.");
            }

            if (new FileInfo(path).Length > ImageInspector.MaxBytes)
            {
                throw new RequestException("The image is larger than 25 MB.");
            }

            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        else
        {
            var data = request.ImageBase64!.Trim();
            var comma = data.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? data.IndexOf(',') : -1;
            if (comma >= 0)
            {
                data = data[(comma + 1)..];
            }

            try
            {
                bytes = Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                throw new RequestException("image_base64 is not valid base64.");
            }
        }

        if (bytes.Length == 0)
        {
            throw new RequestException("The image is empty.");
        }

        if (bytes.Length > ImageInspector.MaxBytes)
        {
            throw new RequestException("The image is larger than 25 MB.");
        }

        return bytes;
    }
}
