using InTheLoop.App.ViewModels;

namespace InTheLoop.App.Views;

public partial class ConversationView : ContentView
{
    private ReviewDetailViewModel? _viewModel;

    public ConversationView() => InitializeComponent();

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_viewModel is not null)
        {
            _viewModel.TimelineChanged -= OnTimelineChanged;
        }

        _viewModel = BindingContext as ReviewDetailViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TimelineChanged += OnTimelineChanged;
        }
    }

    /// <summary>Keeps the newest message in view. Purely visual, so it lives in the view.</summary>
    private void OnTimelineChanged(object? sender, EventArgs e) => Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), async () =>
    {
        if (TimelineStack.Children.LastOrDefault() is Element last)
        {
            await TimelineScroll.ScrollToAsync(last, ScrollToPosition.End, animated: false);
        }
    });
}
