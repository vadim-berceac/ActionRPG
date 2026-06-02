using UnityEngine.Events;

namespace Game.GameCommands
{

    public class TriggerUnityEvent : GameCommandHandler
    {
        public UnityEvent unityEvent;

        public override void PerformInteraction()
        {
            unityEvent.Invoke();
        }
    }
}
