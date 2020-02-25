using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using MiNET.Blocks;
using MiNET.Utils;
using MiNET.Worlds;
using Newtonsoft.Json;
using OpenAPI;
using OpenAPI.Events;
using OpenAPI.Events.Level;
using OpenAPI.World;

namespace Multiverse.World
{
    public class MultiVerseManager
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MultiVerseManager));
        
        private const string Prefix = "MultiVerseLevel-";
        private string WorldFolder { get; set; } = Path.Combine("MultiVerse", "Worlds");
        
        private OpenAPI.OpenApi Api { get; }
        private OpenLevelManager LevelManager { get; }
        private GeneratorManager GeneratorManager { get; }
        
        private ConcurrentDictionary<string, MultiVerseInstance> MultiVerses { get; }
        private object _createLock = new object();
        private bool _busy = false;
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

        internal void Load()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(true);
            foreach (var dir in Directory.EnumerateDirectories(WorldFolder))
            {
                resetEvent.WaitOne();

                Log.Info($"Loading MultiVerse world from \"{dir}\"... ");
                
                var result = Load(dir.Split(Path.DirectorySeparatorChar).Last(), (mv) =>
                {
                    Log.Info($"MultiVerse world \"{mv.Name}\" loaded");
                    resetEvent.Set();
                });
                if (result != CreateResult.Initializing)
                {
                    Log.Warn($"Failed to load world from \"{dir}\": {result}");
                    
                    resetEvent.Reset();
                }
            }
            
            resetEvent.WaitOne();
            Log.Info($"MultiVerse ready!");
        }
        
        public CreateResult Load(string levelName, Action<MultiVerseInstance> callback = null)
        {
            string worldFolder = Path.Combine(WorldFolder, levelName);
            string mvInfoPath = Path.Combine(worldFolder, "mvInfo.json");

            if (!File.Exists(mvInfoPath))
                return CreateResult.InvalidWorld;
            
            bool lockEntered = false;
            try
            {
                if (!_busy && Monitor.TryEnter(_createLock))
                {
                    lockEntered = true;

                    try
                    {
                        var mvInfo = JsonConvert.DeserializeObject<MvFile>(File.ReadAllText(mvInfoPath));
                        var type = Type.GetType(mvInfo.GeneratorType);

                        if (!GeneratorManager.TryGet(type, out var generator))
                            return CreateResult.GeneratorUnavailable;
                        
                        var wp = new AnvilWorldProvider(worldFolder)
                        {
                            MissingChunkProvider = generator
                        };

                        var level = new MultiVerseLevel(Api, LevelManager, levelName,
                            wp, LevelManager.EntityManager);

                        MultiVerseInstance mv;
                        if (!MultiVerses.TryAdd(levelName,
                            mv = new MultiVerseInstance(levelName, level, generator, mvInfo.GeneratorType)))
                        {
                            level.Close();
                            return CreateResult.NameTaken;
                        }

                        _busy = true;
                        
                        //level.Initialize();
                        mv.Initialize().ContinueWith((o) =>
                        {
                            wp.SaveChunks();
                            
                            mv.Ready = true;
                            callback?.Invoke(mv);
                            
                            _busy = false;
                        });

                        return CreateResult.Initializing;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Failed to load world!", ex);
                        return CreateResult.UnknownError;
                    }
                }
            }
            finally
            {
                if (lockEntered)
                    Monitor.Exit(_createLock);
            }

            return CreateResult.Busy;
        }
        
        public CreateResult Create(string levelName, string worldGenerator, Action<MultiVerseInstance> callback = null)
        {
            bool enteredLock = false;
            try
            {
                if (!_busy && Monitor.TryEnter(_createLock))
                {
                    enteredLock = true;
                    
                    try
                    {
                        if (!GeneratorManager.TryGet(worldGenerator, out IWorldGenerator generator))
                        {
                            return CreateResult.InvalidWorldGenerator;
                        }

                        if (MultiVerses.ContainsKey(levelName))
                            return CreateResult.NameTaken;

                        string worldFolder = Path.Combine(WorldFolder, levelName);
                        string mvInfoPath = Path.Combine(worldFolder, "mvInfo.json");
                        
                        if (!Directory.Exists(worldFolder))
                        {
                            Directory.CreateDirectory(worldFolder);
                            File.WriteAllText(mvInfoPath, JsonConvert.SerializeObject(new MvFile()
                            {
                                LevelName = levelName,
                                GeneratorType = generator.GetType().AssemblyQualifiedName
                            }));
                        }

                        var wp = new AnvilWorldProvider(worldFolder)
                        {
                            MissingChunkProvider = generator
                        };

                        var level = new MultiVerseLevel(Api, LevelManager, levelName,
                            wp, LevelManager.EntityManager);

                        MultiVerseInstance mv;
                        if (!MultiVerses.TryAdd(levelName,
                            mv = new MultiVerseInstance(levelName, level, generator, worldGenerator)))
                        {
                            level.Close();
                            return CreateResult.NameTaken;
                        }

                        _busy = true;
                        
                        //level.Initialize();
                        mv.Initialize().ContinueWith((o) =>
                        {
                            wp.SaveChunks();
                            
                            mv.Ready = true;
                            callback?.Invoke(mv);
                            
                            _busy = false;
                        });

                        return CreateResult.Initializing;
                    }
                    catch (Exception ex)
                    {
                        _busy = false;
                        return CreateResult.UnknownError;
                    }
                }
            }
            finally
            {
                if (enteredLock)
                    Monitor.Exit(_createLock);
            }

            return CreateResult.Busy;
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

        public class MvFile
        {
            public string LevelName { get; set; }
            public string GeneratorType { get; set; }
        }
    }

    public class MultiVerseInstance : IEventHandler
    {
        public string Name { get; }
        public string GeneratorName { get; }
        public MultiVerseLevel Level { get; }
        public IWorldGenerator Generator { get; }
        public bool Ready { get; internal set; } = false;

        public MultiVerseInstance(string name, MultiVerseLevel level, IWorldGenerator generator, string generatorName)
        {
            Name = name;
            Level = level;
            Generator = generator;
            GeneratorName = generatorName;
            
            Level.EventDispatcher.RegisterEvents(this);
        }

        [EventHandler(EventPriority.Monitor)]
        public void OnChunkGenerated(ChunkGeneratedEvent e)
        {
            e.Chunk.NeedSave = true;
        }

        internal Task Initialize()
        {
            return Task.Run(() =>
            {
                Level.Initialize();
            });
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
        Created,
        Initializing,
        Busy,
        GeneratorUnavailable,
        WorldExists,
        InvalidWorld
    }
}