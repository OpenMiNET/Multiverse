using System;
using System.Text;
using MiNET.Plugins.Attributes;
using MiNET.Utils;
using Multiverse.World;
using OpenAPI.Player;

namespace Multiverse.Commands
{
    public class MultiVerseCommands
    {
        private Multiverse Plugin { get; }
        public MultiVerseCommands(Multiverse plugin)
        {
            Plugin = plugin;
        }

        [Command(Name = "mv info", Description = "Get multiverse plugin info")]
        public void MultiVerseCommand(OpenPlayer player)
        {
            player.SendPluginMessage($"{Plugin.Info.Name} {Plugin.Info.Version}");
        }

        [Command(Name = "mv create", Description = "Create a new multiverse world instance", Permission = "mv.create")]
        public void Create(OpenPlayer player, string levelname, string generator)
        {
            if (levelname.Contains(' ') || levelname.Contains('\t'))
            {
                player.SendPluginMessage($"The level name may not contain any whitespace!");
                return;
            }

            switch (Plugin.MultiVerseManager.Create(levelname, generator, (mv) =>
            {
                if (mv.Ready)
                    player.SendPluginMessage("The world has been created, you can join it by typing '/mv join {levelname}'. You may now create new worlds again!");
            }))
            {
                case CreateResult.InvalidWorldGenerator:
                    player.SendPluginMessage(
                        $"No generator was found with the provided name, available values are: {ChatFormatting.Reset}{String.Join(',', Plugin.GeneratorManager.GeneratorNames)}");
                    break;
                case CreateResult.NameTaken:
                    player.SendPluginMessage($"The provided world name has already been taken...");
                    break;
                case CreateResult.UnknownError:
                    player.SendPluginMessage($"Something unexpected happened while trying to create your world!");
                    break;
                case CreateResult.Initializing:
                    player.SendPluginMessage(
                        $"Initializing world... We will let you know once we are done!");
                    break;
                case CreateResult.Busy:
                    player.SendPluginMessage($"Cannot handle your request, server is busy...");
                    break;
                case CreateResult.WorldExists:
                    player.SendPluginMessage($"The world already exists, maybe you meant to execute '/mv load {levelname}'");
                    break;
                case CreateResult.GeneratorUnavailable:
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [Command(Name = "mv destroy", Description = "Destroy a level created by MultiVerse", Permission = "mv.destroy")]
        public void Destroy(OpenPlayer player, string levelname)
        {
            if (Plugin.MultiVerseManager.Destroy(levelname))
            {
                player.SendPluginMessage($"The MultiVerse \"{levelname}\" has been destroyed!");
            }
            else
            {
                player.SendPluginMessage($"Could not find a MultiVerse with the name: {levelname}");
            }
        }

        [Command(Name = "mv join", Description = "Join a level created by MultiVerse", Permission = "mv.join")]
        public void Join(OpenPlayer player, string levelname)
        {
            if (Plugin.MultiVerseManager.TryGet(levelname, out var instance))
            {
                player.SendPluginMessage($"Joining multiverse world: {instance.Name}");
                
                player.Level.RemovePlayer(player, true);
                
                player.SpawnLevel(instance.Level, instance.Level.SpawnPoint, true, null, () =>
                {
                    player.SendPluginMessage($"You have joined world: {instance.Name}, to leave it type '/mv leave' or join another multiverse world.");
                });
                return;
            }
            
            player.SendPluginMessage($"No world with the provided name has been found!");
        }

        [Command(Name = "mv leave", Description = "Leave the multiverse you are in", Permission = "mv.leave")]
        public void Leave(OpenPlayer player)
        {
            if (player.Level is MultiVerseLevel mvl)
            {
                var lvl = Plugin.Api.LevelManager.GetDefaultLevel();
                
                player.SpawnLevel(lvl, lvl.SpawnPoint, true);
                return;
            }
            
            player.SendPluginMessage($"You are not in a multiverse world!");
        }

        [Command(Name = "mv list", Description = "Lists all available multiverses", Permission = "mv.list")]
        public void List(OpenPlayer player)
        {
            var instances = Plugin.MultiVerseManager.Instances;

            if (instances.Length == 0)
            {
                player.SendPluginMessage($"No MultiVerses have been created!");
                return;
            }
            
            StringBuilder sb = new StringBuilder();
            foreach (var instance in instances)
            {
                sb.AppendLine($"{instance.Name} - {instance.GeneratorName}");
            }
            
            player.SendPluginMessage(sb.ToString());
        }
    }
}