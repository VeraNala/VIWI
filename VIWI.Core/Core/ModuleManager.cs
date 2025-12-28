using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ECommons.Logging;

namespace VIWI.Core
{
    public static class ModuleManager
    {
        private static readonly List<IVIWIModule> modules = new();
        private static bool initialized;

        public static IReadOnlyList<IVIWIModule> Modules => modules;
        public static VIWIConfig Config { get; private set; } = null!;

        public static void Initialize(VIWIConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "[VIWI] ModuleManager.Initialize received null config.");

            if (initialized)
            {
                PluginLog.Warning("ModuleManager.Initialize called more than once; ignoring.");
                return;
            }

            initialized = true;
            Config = config;

            PluginLog.Information("Loading VIWI modules...");

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name ?? string.Empty;
                    if (!name.StartsWith("VIWI", StringComparison.OrdinalIgnoreCase))
                        continue;

                    LoadModulesFromAssembly(asm, config);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Unexpected error during module scan: {ex}");
            }

            PluginLog.Information($"Loaded {modules.Count} module(s).");
        }

        public static void Dispose()
        {
            if (!initialized)
                return;

            for (var i = modules.Count - 1; i >= 0; i--)
            {
                var module = modules[i];
                try
                {
                    module.Dispose();
                    PluginLog.Information($"Unloaded module: {module.Name}");
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error unloading module {module.Name}: {ex}");
                }
            }

            modules.Clear();
            initialized = false;
        }

        private static void LoadModulesFromAssembly(Assembly asm, VIWIConfig config)
        {
            IEnumerable<Type> moduleTypes;

            try
            {
                moduleTypes = asm
                    .GetTypes()
                    .Where(t =>
                        typeof(IVIWIModule).IsAssignableFrom(t) &&
                        !t.IsInterface &&
                        !t.IsAbstract);
            }
            catch (ReflectionTypeLoadException rtle)
            {
                moduleTypes = rtle.Types
                    .Where(t => t != null &&
                                typeof(IVIWIModule).IsAssignableFrom(t) &&
                                !t.IsInterface &&
                                !t.IsAbstract)!;
            }

            foreach (var type in moduleTypes)
            {
                IVIWIModule? module = null;

                try
                {
                    module = CreateModuleInstance(type, config);
                    if (module == null)
                        continue;

                    modules.Add(module);
                    module.Initialize(config);

                    PluginLog.Information($"Loaded module: {module.Name} v{module.Version}");
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error loading module {type.FullName}: {ex}");

                    if (module != null)
                    {
                        try 
                        { 
                            module.Dispose(); 
                        } 
                        catch { }
                        modules.Remove(module);
                    }
                }
            }
        }

        private static IVIWIModule? CreateModuleInstance(Type type, VIWIConfig config)
        {
            var ctorWithConfig = type.GetConstructor(new[] { typeof(VIWIConfig) });
            if (ctorWithConfig != null)
                return ctorWithConfig.Invoke(new object[] { config }) as IVIWIModule;

            if (Activator.CreateInstance(type) is IVIWIModule m)
                return m;

            return null;
        }
    }


}
