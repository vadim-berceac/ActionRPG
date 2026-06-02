using UnityEngine;


namespace Game.GameCommands
{

    public class SetGameObjectActive : GameCommandHandler
    {
        public GameObject[] targets;
        public bool isEnabled = true;

        public override void PerformInteraction()
        {
            foreach (var g in targets)
                g.SetActive(isEnabled);
        }
    }
}
