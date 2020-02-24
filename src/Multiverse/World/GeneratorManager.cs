using System;
using System.Collections.Concurrent;
using System.Linq;
using MiNET.Worlds;

namespace Multiverse.World
{
    public class GeneratorManager
    {
        private ConcurrentDictionary<string, Func<IWorldGenerator>> WorldGenerators { get; }
        public GeneratorManager()
        {
            WorldGenerators = new ConcurrentDictionary<string, Func<IWorldGenerator>>();
        }

        public string[] GeneratorNames => WorldGenerators.Keys.ToArray();
        
        public bool TryRegister<TGenerator>(string name, Func<TGenerator> factory) where TGenerator : IWorldGenerator
        {
            return WorldGenerators.TryAdd(name, () => factory());
        }

        public bool TryGet(string name, out IWorldGenerator generator)
        {
            generator = default;
            
            if (!WorldGenerators.TryGetValue(name, out var factory))
                return false;

            generator = factory();

            return true;
        }

        public void Close()
        {
            WorldGenerators.Clear();
        }
    }
}