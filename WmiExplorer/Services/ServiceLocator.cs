namespace WmiExplorer.Services
{
    /// <summary>
    /// Simple service locator for dependency injection
    /// </summary>
    public class ServiceLocator
    {
        private static ServiceLocator? _instance;
        private readonly Dictionary<Type, object?> _services = new Dictionary<Type, object?>();

        private ServiceLocator()
        { }

        /// <summary>
        /// Gets the singleton instance of the ServiceLocator
        /// </summary>
        public static ServiceLocator Instance => _instance ??= new ServiceLocator();

        /// <summary>
        /// Gets a registered service
        /// </summary>
        public TInterface Get<TInterface>()
        {
            if (!_services.TryGetValue(typeof(TInterface), out var service))
            {
                throw new InvalidOperationException($"Service of type {typeof(TInterface).Name} is not registered");
            }

            if (service == null)
            {
                // Lazy initialization
                var implementationType = _services.Keys
                    .First(t => t.IsAssignableFrom(typeof(TInterface)))
                    .Assembly
                    .GetTypes()
                    .FirstOrDefault(t => typeof(TInterface).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (implementationType == null)
                {
                    throw new InvalidOperationException($"No implementation found for {typeof(TInterface).Name}");
                }

                service = Activator.CreateInstance(implementationType);
                if (service == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of {implementationType.Name}");
                }

                _services[typeof(TInterface)] = service;
            }

            return (TInterface)service;
        }

        /// <summary>
        /// Registers a service instance for the specified interface type
        /// </summary>
        public void Register<TInterface, TImplementation>(TImplementation instance) where TImplementation : class, TInterface
        {
            _services[typeof(TInterface)] = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        /// <summary>
        /// Registers a singleton service that will be instantiated on first use
        /// </summary>
        public void RegisterSingleton<TInterface, TImplementation>() where TImplementation : class, TInterface, new()
        {
            _services[typeof(TInterface)] = null; // Will be instantiated on first request
        }
    }
}