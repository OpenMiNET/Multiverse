using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MiNET.Utils;
using MiNET.Worlds;
using OpenAPI;
using OpenAPI.World;

namespace Multiverse.World
{
    public class MultiVerseManager
    {
        private const string Prefix = "MultiVerseLevel-";
        private string WorldFolder { get; set; } = Path.Combine("MultiVerse", "Worlds");
        
        private OpenAPI.OpenApi Api { get; }
        private OpenLevelManager LevelManager { get; }
        private GeneratorManager GeneratorManager { get; }
        
        private ConcurrentDictionary<string, MultiVerseInstance> MultiVerses { get; }
        public MultiVerseManager(OpenApi api, GeneratorManager generatorManager)
        {
            Api = api;
            LevelManager = api.LevelManager;
            GeneratorManager = generatorManager;
            
            MultiVerses = new ConcurrentDictionary<string, MultiVerseInstance>();

            WorldFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), WorldFolder);
            
            if (!Directory.Exists(WorldFolder))
            {
                Directory.CreateDirectory(WorldFolder);
            }
        }

        public MultiVerseInstance[] Instances => MultiVerses.Values.ToArray();

        public CreateResult Create(string levelName, string worldGenerator)
        {
            try
            {
                if (!GeneratorManager.TryGet(worldGenerator, out IWorldGenerator generator))
                {
                    return CreateResult.InvalidWorldGenerator;
                }

                if (MultiVerses.ContainsKey(levelName))
                    return CreateResult.NameTaken;

                string worldFolder = Path.Combine(WorldFolder, levelName);
                Directory.CreateDirectory(worldFolder);

                var wp = new AnvilWorldProvider(worldFolder)
                {
                    MissingChunkProvider = generator
                };

                var level = new MultiVerseLevel(Api, LevelManager, levelName,
                    wp, LevelManager.EntityManager);

                if (!MultiVerses.TryAdd(levelName, new MultiVerseInstance(levelName, level, generator, worldGenerator)))
                {
                    level.Close();
                    return CreateResult.NameTaken;
                }
                
                level.Initialize();

                return CreateResult.Created;
            }
            catch (Exception ex)
            {
                return CreateResult.UnknownError;
            }
        }

        public bool TryGet(string name, out MultiVerseInstance instance)
        {
            return MultiVerses.TryGetValue(name, out instance);
        }

        public bool Destroy(string name)
        {
            if (MultiVerses.TryRemove(name, out var instance))
            {
                instance.Close();
                
                return true;
            }

            return false;
        }

        public void Close()
        {
            foreach (var mv in MultiVerses)
            {
                mv.Value.Close();
            }
            
            MultiVerses.Clear();
        }
    }

    public class MultiVerseInstance
    {
        public string Name { get; }
        public string GeneratorName { get; }
        public MultiVerseLevel Level { get; }
        public IWorldGenerator Generator { get; }

        public MultiVerseInstance(string name, MultiVerseLevel level, IWorldGenerator generator, string generatorName)
        {
            Name = name;
            Level = level;
            Generator = generator;
            GeneratorName = generatorName;
        }

        public void Close()
        {
            Level.Close();
        }
    }

    public enum CreateResult
    {
        InvalidWorldGenerator,
        NameTaken,
        UnknownError,
        Created
    }
}