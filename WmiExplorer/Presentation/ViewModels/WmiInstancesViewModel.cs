using System.Collections.ObjectModel;
using System.Management;
using System.Windows.Input;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Shared;
using WmiExplorer.Core.Models;
using WmiExplorer.Services;

namespace WmiExplorer.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for a WMI instance. Exposes instance properties and supports selection messaging.
    /// </summary>
    public class WmiInstancesViewModel : MessagingViewModelBase
    {
        private readonly IApplicationService _applicationService;
        private readonly WmiInstance _model;
        private readonly IWmiService _wmiService;
        private readonly WmiClassesViewModel _parentClass;

        /// <summary>
        /// The underlying ManagementObject for this instance.
        /// </summary>
        public ManagementObject ActualObject => _model.ActualObject;

        /// <summary>
        /// The WMI path for this instance.
        /// </summary>
        public string NamespacePath => ActualObject.Path.Path;

        /// <summary>
        /// The display name for this instance.
        /// </summary>
        public string InstanceName => _model.InstanceName;

        /// <summary>
        /// Command to copy the instance path to clipboard.
        /// </summary>
        public ICommand CopyRelativePathCommand { get; }

        /// <summary>
        /// The parent class ViewModel.
        /// </summary>
        public WmiClassesViewModel ParentClass => _parentClass;

        /// <summary>
        /// The parent namespace ViewModel.
        /// </summary>
        public WmiNamespacesViewModel? ParentNamespace => ParentClass.ParentNamespace;

        /// <summary>
        /// Initializes a new instance of the <see cref="WmiInstancesViewModel"/> class.
        /// </summary>
        /// <param name="model">The WMI instance model.</param>
        /// <param name="parentClass">The parent class ViewModel.</param>
        /// <param name="wmiService">The WMI service.</param>
        /// <param name="messagingService">The messaging service.</param>
        /// <param name="applicationService">The application service.</param>
        public WmiInstancesViewModel(
            WmiInstance model,
            WmiClassesViewModel parentClass,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (parentClass == null) throw new ArgumentNullException(nameof(parentClass));
            if (wmiService == null) throw new ArgumentNullException(nameof(wmiService));
            if (messagingService == null) throw new ArgumentNullException(nameof(messagingService));
            if (applicationService == null) throw new ArgumentNullException(nameof(applicationService));

            _model = model;
            _wmiService = wmiService;
            _applicationService = applicationService;
            _parentClass = parentClass;

            InitializeMessaging(messagingService);

            CopyRelativePathCommand = new RelayCommand(CopyRelativePath);
        }

        private void CopyRelativePath(object? parameter)
        {
            // Copies the instance path to clipboard and notifies the user.
            if (string.IsNullOrEmpty(NamespacePath))
                return;

            _applicationService.CopyToClipboard(NamespacePath);
            PublishSuccessState($"Copied path: {NamespacePath}");
        }

        /// <summary>
        /// Creates a collection of WmiInstancesViewModel from a collection of WmiInstance models.
        /// </summary>
        /// <param name="models">The collection of WMI instance models.</param>
        /// <param name="wmiService">The WMI service.</param>
        /// <param name="messagingService">The messaging service.</param>
        /// <param name="applicationService">The application service.</param>
        /// <param name="parentClass">The parent class ViewModel.</param>
        /// <returns>A collection of WmiInstancesViewModel.</returns>
        public static ObservableCollection<WmiInstancesViewModel> CreateFromCollection(
            IEnumerable<WmiInstance> models,
            IWmiService wmiService,
            IMessagingService messagingService,
            IApplicationService applicationService,
            WmiClassesViewModel parentClass)
        {
            if (models == null)
                throw new ArgumentNullException(nameof(models));

            var viewModels = new ObservableCollection<WmiInstancesViewModel>();

            foreach (var model in models)
            {
                viewModels.Add(new WmiInstancesViewModel(
                    model,
                    parentClass,
                    wmiService,
                    messagingService,
                    applicationService));
            }

            return viewModels;
        }

        /// <summary>
        /// Forces selection of this instance and publishes a selection message.
        /// </summary>
        public void ForceSelection()
        {
            // Always publish the message even if already selected (for UI refresh scenarios).
            PublishMessage(new SelectedInstanceChangedMessage(this));
        }

        /// <summary>
        /// Returns a string representation of the instance.
        /// </summary>
        /// <returns>A string representation of the instance.</returns>
        public override string ToString() => _model.ToString();
    }
}