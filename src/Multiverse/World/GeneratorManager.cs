using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using log4net;
using log4net.Core;
using MiNET.Worlds;

namespace Multiverse.World
{
    public class GeneratorManager
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GeneratorManager));
        
        private ConcurrentDictionary<Type, string> TypeMapping { get; }
        private ConcurrentDictionary<string, Func<IWorldGenerator>> WorldGenerators { get; }
        public GeneratorManager()
        {
            WorldGenerators = new ConcurrentDictionary<string, Func<IWorldGenerator>>();
            TypeMapping = new ConcurrentDictionary<Type, string>();
        }

        public string[] GeneratorNames => WorldGenerators.Keys.ToArray();
        
        public bool TryRegister<TGenerator>(string name, Func<TGenerator> factory) where TGenerator : IWorldGenerator
        {
            if (WorldGenerators.TryAdd(name, () => factory()))
            {
                TypeMapping.TryAdd(typeof(TGenerator), name);

                return true;
            }

            return false;
        }

        public bool TryGet(string name, out IWorldGenerator generator)
        {
            generator = default;
            
            if (!WorldGenerators.TryGetValue(name, out var factory))
                return false;

            generator = factory();

            return true;
        }

        public bool TryGet(Type type, out IWorldGenerator generator)
        {
            if (TypeMapping.TryGetValue(type, out string gen))
            {
                return TryGet(gen, out generator);
            }

            generator = default;
            return false;
        }
        
        public void AutoRegister(Assembly assembly)
        {
            var worldGenType = typeof(IWorldGenerator);
           // foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetExportedTypes().Where(x => worldGenType.IsAssignableFrom(x) && x.IsClass))
                {
                    string displayName = type.Name;

                    var displayNameAttribute = type.GetCustomAttribute<DisplayNameAttribute>();
                    if (displayNameAttribute != null)
                    {
                        displayName = displayNameAttribute.DisplayName;
                    }

                    var constructor = type.GetConstructors().FirstOrDefault(x => x.GetParameters().Length == 0);
                    
                    if (constructor == null)
                    {
                        Log.Warn($"Cannot create instance of \"{type.FullName}\", no parameterless constructor found!");
                        continue;
                    }

                    if (WorldGenerators.TryAdd(displayName, () =>
                    {
                        return (IWorldGenerator) constructor.Invoke(new object[] { });
                    }))
                    {
                        TypeMapping.TryAdd(type, displayName);
                        Log.Info($"Registered world generator of type \"{type}\" with name \"{displayName}\"");
                    }
                }
            }
        }

        public void Close()
        {
            WorldGenerators.Clear();
        }
    }
}