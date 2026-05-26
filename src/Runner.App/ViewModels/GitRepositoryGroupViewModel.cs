using Runner.App.Services;

namespace Runner.App.ViewModels;

public sealed class GitRepositoryGroupViewModel : DashboardItemViewModel
{
    private bool _isCollapsed;

    public GitRepositoryGroupViewModel(
        string key,
        GitRepositoryInfo repositoryInfo,
        int projectCount,
        bool isCollapsed)
    {
        Key = key;
        RepositoryInfo = repositoryInfo;
        ProjectCount = projectCount;
        _isCollapsed = isCollapsed;
    }

    public string Key { get; }

    public GitRepositoryInfo RepositoryInfo { get; }

    public int ProjectCount { get; }

    public string RepositoryName => RepositoryInfo.RepositoryName;

    public string? RepositoryRoot => RepositoryInfo.RepositoryRoot;

    public string BranchDisplay => RepositoryInfo.BranchDisplay;

    public string ToolTipText => string.IsNullOrWhiteSpace(RepositoryRoot)
        ? RepositoryName
        : RepositoryRoot;

    public string RepositoryIconValue => RepositoryInfo.HasRepository
        ? "fa-brands fa-git-alt"
        : "fa-regular fa-folder-open";

    public bool CanCollapse => RepositoryInfo.HasRepository || ProjectCount > 1;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetProperty(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(CollapseIconValue));
                OnPropertyChanged(nameof(CollapseToolTip));
            }
        }
    }

    public string CollapseIconValue => IsCollapsed
        ? "fa-solid fa-caret-right"
        : "fa-solid fa-caret-down";

    public string CollapseToolTip => IsCollapsed
        ? "Expand repository group"
        : "Collapse repository group";
}
