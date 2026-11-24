using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

public sealed record GetReview(string ReviewId) : IRequest<Review?>;

internal sealed class GetReviewHandler(IReviewStore store) : IRequestHandler<GetReview, Review?>
{
    public Task<Review?> HandleAsync(GetReview request, CancellationToken cancellationToken) =>
        store.GetAsync(request.ReviewId.Trim(), cancellationToken);
}

public sealed record ListReviews(ReviewStatus? Status = null, string? Project = null) : IRequest<IReadOnlyList<Review>>;

internal sealed class ListReviewsHandler(IReviewStore store, IProjectStore projects) : IRequestHandler<ListReviews, IReadOnlyList<Review>>
{
    public async Task<IReadOnlyList<Review>> HandleAsync(ListReviews request, CancellationToken cancellationToken)
    {
        IEnumerable<Review> reviews = await store.ListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            var project = await projects.RequireAsync(request.Project, cancellationToken);
            reviews = reviews.Where(r => r.ProjectId == project.Id);
        }

        if (request.Status is { } status)
        {
            reviews = reviews.Where(r => r.Status == status);
        }

        return reviews.ToList();
    }
}

public sealed record DeleteReview(string ReviewId) : IRequest<bool>;

internal sealed class DeleteReviewHandler(IReviewStore store) : IRequestHandler<DeleteReview, bool>
{
    public Task<bool> HandleAsync(DeleteReview request, CancellationToken cancellationToken) =>
        store.DeleteAsync(request.ReviewId, cancellationToken);
}
