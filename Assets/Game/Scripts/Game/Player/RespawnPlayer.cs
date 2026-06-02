namespace Game.GameCommands
{
    public class RespawnPlayer : GameCommandHandler
    {
        public PlayerController player;

        public override void PerformInteraction()
        {
            player.Respawn();
        }
    }
}
