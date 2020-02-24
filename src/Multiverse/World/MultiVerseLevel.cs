using log4net;
using MiNET.Utils;
using MiNET.Worlds;
using OpenAPI;
using OpenAPI.World;

namespace Multiverse.World
{
    public class MultiVerseLevel : OpenLevel
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MultiVerseLevel));
        
        public MultiVerseLevel(OpenApi openApi, OpenLevelManager levelManager, string levelId,
            IWorldProvider worldProvider, EntityManager entityManager, GameMode gameMode = GameMode.Survival,
            Difficulty difficulty = Difficulty.Normal, int viewDistance = 11) : base(openApi, levelManager, levelId,
            worldProvider, entityManager, gameMode, difficulty, viewDistance)
        {
            
        }

        public override void Close()
        {
            if (WorldProvider is AnvilWorldProvider wp)
            {
                if (!Config.GetProperty("save.enabled", false))
                {
                    Log.Warn($"Cannot save MultiVerse level \"{LevelId}\", saving is disabled in the server configuration.");
                }

                wp.SaveChunks();
            }
            
            base.Close();
        }
    }
}