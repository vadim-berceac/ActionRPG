
namespace Game.GameCommands
{
    public class RespawnPlayer : GameCommandHandler
    {
        public HumanoidController humanoid;

        public override void PerformInteraction()
        {
            humanoid.Respawn();
        }
    }
}
