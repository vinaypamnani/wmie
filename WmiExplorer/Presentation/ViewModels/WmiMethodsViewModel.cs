using WmiExplorer.Common.Base;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    public class WmiMethodsViewModel : MessagingViewModelBase
    {
        private readonly IWmiService _wmiService;

        public WmiMethodsViewModel(IMessagingService messagingService, IWmiService wmiService)
        {
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            // Subscribe to messages about selected class changes
            // This will allow the methods view to update when a different class is selected
        }

        // Future implementation will include:
        // 1. Property to store list of class methods
        // 2. Logic to fetch and display class methods
        // 3. Method invocation capabilities
        // 4. Parameter handling for method execution
    }
}