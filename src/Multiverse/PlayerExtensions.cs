using MiNET.Utils;
using OpenAPI.Player;

namespace Multiverse
{
    public static class PlayerExtensions
    {
        public static void SendPluginMessage(this OpenPlayer player, string message)
        {
            player.SendMessage($"{ChatColors.Yellow}[MultiVerse] {message}");
        }
    }
}