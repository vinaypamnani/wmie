using System.Collections.ObjectModel;
using System.Management;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    public class WmiInstancesViewModel : MessagingViewModelBase
    {
        private readonly IApplicationService _applicationService;
        private readonly WmiInstance _model;
        private readonly IWmiService _wmiService;

        // Parent references
        private WmiClassesViewModel? _parentClass;

        /// <summary>
        /// Constructor for a single WmiInstance
        /// </summary>
        public WmiInstancesViewModel(
            WmiInstance model,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _wmiService = wmiService ?? throw new ArgumentNullException(nameof(wmiService));
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));

            // Initialize messaging
            InitializeMessaging(messagingService);

            CopyRelativePathCommand = new RelayCommand(CopyRelativePath);
        }

        /// <summary>
        /// Gets the underlying WMI object
        /// </summary>
        public ManagementObject ActualObject => _model.ActualObject;

        /// <summary>
        /// Command to copy the relative path to clipboard
        /// </summary>
        public ICommand CopyRelativePathCommand { get; }

        /// <summary>
        /// Gets the full path of the instance
        /// </summary>
        public string FullPath => ActualObject.Path.Path;

        /// <summary>
        /// Gets the display name of the instance
        /// </summary>
        public string InstanceName => _model.InstanceName;

        /// <summary>
        /// Gets or sets the parent class
        /// </summary>
        public WmiClassesViewModel? ParentClass
        {
            get => _parentClass;
            set => _parentClass = value;
        }

        /// <summary>
        /// Gets the parent namespace through the parent class reference
        /// </summary>
        public WmiNamespacesViewModel? ParentNamespace => ParentClass?.ParentNamespace;

        /// <summary>
        /// Copies the relative path of this instance to the clipboard
        /// </summary>
        private void CopyRelativePath(object? parameter)
        {
            if (string.IsNullOrEmpty(FullPath))
                return;

            _applicationService.CopyToClipboard(FullPath);
            PublishSuccessState($"Copied path: {FullPath}");
        }

        /// <summary>
        /// Factory method to create a collection of WmiInstancesViewModels from a collection of WmiInstance models
        /// </summary>
        public static ObservableCollection<WmiInstancesViewModel> CreateFromCollection(
            IEnumerable<WmiInstance> models,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            if (models == null)
                throw new ArgumentNullException(nameof(models));

            var viewModels = new ObservableCollection<WmiInstancesViewModel>();

            foreach (var model in models)
            {
                viewModels.Add(new WmiInstancesViewModel(
                    model,
                    wmiService,
                    messagingService,
                    applicationService));
            }

            return viewModels;
        }

        /// <summary>
        /// Force selection notification even when already selected
        /// </summary>
        public void ForceSelection()
        {
            // Always publish the message even if already selected
            PublishMessage(new SelectedInstanceChangedMessage(this));
        }

        /// <summary>
        /// Returns the instance's string representation
        /// </summary>
        public override string ToString() => _model.ToString();
    }
}