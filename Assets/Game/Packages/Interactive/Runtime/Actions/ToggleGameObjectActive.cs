using UnityEngine;


namespace Game.GameCommands
{
    public class ToggleGameObjectActive : GameCommandHandler
    {
        public GameObject[] targets;

        public override void PerformInteraction()
        {
            foreach (var g in targets)
                g.SetActive(!g.activeSelf);
        }
    }
}
