using System;
using System.Linq;
using log4net;
using MiNET.Utils;
using MiNET.Worlds;
using Multiverse.Commands;
using Multiverse.World;
using OpenAPI;
using OpenAPI.Events;
using OpenAPI.Events.Player;
using OpenAPI.Events.Plugins;
using OpenAPI.Events.Server;
using OpenAPI.Permission;
using OpenAPI.Plugins;

namespace Multiverse
{
    [OpenPluginInfo(Name = "Multiverse", Author = "Kenny van Vulpen", Description = "A multiverse plugin for OpenAPI")]
    public class Multiverse : OpenPlugin, IEventHandler
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Multiverse));
        
        internal OpenApi Api { get; }
        public GeneratorManager GeneratorManager { get; }
        public MultiVerseManager MultiVerseManager { get; }
        
        private MultiVerseCommands Commands { get; }
        private bool PermissionsEnabled { get; }
        
        public Multiverse(OpenAPI.OpenApi api)
        {
            Api = api;
            GeneratorManager = new GeneratorManager();
            MultiVerseManager = new MultiVerseManager(api, GeneratorManager);
            Commands = new MultiVerseCommands(this);

            PermissionsEnabled = Config.GetProperty("mv.permissions.enabled", false);
            
            GeneratorManager.TryRegister<SuperflatGenerator>("flatland", () =>
                {
                    return new SuperflatGenerator(Dimension.Overworld);
                });
        }
        
        /// <summary>
        /// 	The method that gets invoked as soon as a plugin gets Enabled.
        /// 	Any initialization should be done in here.
        /// </summary>
        /// <param name="api">An instance to OpenApi</param>
        public override void Enabled(OpenApi api)
        {
            if (!Config.GetProperty("save.enabled", false))
            {
                Log.Warn(
                    $"!!! World saving is disabled in the server configuration, enable it by updating your server.conf to include \"save.enabled=true\" to resolve this issue. !!!");
            }

            if (!PermissionsEnabled)
            {
                Log.Warn(
                    $"!!! Permissions are disabled! This means anyone can create MultiVerse instances! If you have a permission manager, you should add \"mv.permissions.enabled=true\" to your server.conf file !!!");
            }

            api.EventDispatcher.RegisterEvents(this);
            api.CommandManager.LoadCommands(Commands);
        }

        /// <summary>
        /// 	The method that gets invoked as soon as a plugin gets Disabled.
        /// 	Any content initialized in <see cref="M:OpenAPI.Plugins.OpenPlugin.Enabled(OpenAPI.OpenApi)" /> should be de-initialized in here.
        /// </summary>
        /// <param name="api">An instance to OpenApi</param>
        public override void Disabled(OpenApi api)
        {
            api.EventDispatcher.UnregisterEvents(this);
            api.CommandManager.UnloadCommands(Commands);
            
            GeneratorManager.Close();
            MultiVerseManager.Close();
        }

        [EventHandler(EventPriority.Monitor)]
        public void OnPlayerJoin(OpenAPI.Events.Player.PlayerSpawnedEvent e)
        {
            const bool value = true;

            //If permissions are disabled, give everyone that joins these permissions.
            if (!PermissionsEnabled)
            {
                var group = new PermissionGroup("mv");
                group["mv.create"] = value;
                group["mv.join"] = value;
                group["mv.leave"] = value;
                group["mv.destroy"] = value;
                group["mv.list"] = value;

                e.Player.Permissions.AddPermissionGroup(group);
                
                e.Player.RefreshCommands();
            }
        }

        [EventHandler(EventPriority.Monitor)]
        public void OnServerReady(ServerReadyEvent e)
        {
            var searchName = typeof(OpenAPI.OpenApi).Assembly.GetName().Name;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetReferencedAssemblies().Any(x =>
                    x.FullName.Contains("MiNET", StringComparison.InvariantCultureIgnoreCase) ||
                    x.Name.Equals(searchName)))
                    GeneratorManager.AutoRegister(assembly);
            }

            MultiVerseManager.Load();
        }
    }
}