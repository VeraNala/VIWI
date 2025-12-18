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
        public static void Initialize()
        {
            if (initialized)
            {
                PluginLog.Warning("ModuleManager.Initialize called more than once; ignoring.");
                return;
            }

            initialized = true;
            PluginLog.Information("Loading VIWI modules...");

            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    LoadModulesFromAssembly(asm);
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

        private static void LoadModulesFromAssembly(Assembly asm)
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
                try
                {
                    if (Activator.CreateInstance(type) is not IVIWIModule module)
                        continue;
                    modules.Add(module);
                    module.Initialize();
                    PluginLog.Information($"Loaded module: {module.Name} v{module.Version}");
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error loading module {type.FullName}: {ex}");
                }
            }
        }
    }
    public interface IVIWIModule : IDisposable
    {
        string Name { get; }
        string Version { get; }
        void Initialize();
    }
}
