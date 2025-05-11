using WmiExplorer.Common.Base;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    public class WmiPropertiesViewModel : MessagingViewModelBase
    {
        private readonly IWmiService _wmiService;

        public WmiPropertiesViewModel(IMessagingService messagingService, IWmiService wmiService)
        {
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            // Subscribe to messages about selected class changes
            // This will allow the properties view to update when a different class is selected
        }

        // Future implementation will include:
        // 1. Property to store list of class properties
        // 2. Logic to fetch and display class properties
        // 3. Filtering and sorting capabilities
    }
}