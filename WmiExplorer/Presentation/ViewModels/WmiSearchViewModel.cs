using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    public class WmiSearchViewModel : MessagingViewModelBase
    {
        private readonly IWmiService _wmiService;
        private string _searchQuery = string.Empty;

        public WmiSearchViewModel(IMessagingService messagingService, IWmiService wmiService)
        {
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            // Initialize commands
            SearchCommand = new RelayCommand(_ => ExecuteSearch(), _ => CanExecuteSearch());
        }

        public ICommand SearchCommand { get; }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    // Refresh the command's CanExecute state
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool CanExecuteSearch()
        {
            return !string.IsNullOrWhiteSpace(SearchQuery);
        }

        private void ExecuteSearch()
        {
            // Show busy state
            PublishBusyState("Executing search...");

            // Future implementation:
            // 1. Execute WMI search using the query
            // 2. Display results

            // For now just show a message that this is not implemented
            PublishSuccessState("Search feature coming soon!");
        }
    }
}