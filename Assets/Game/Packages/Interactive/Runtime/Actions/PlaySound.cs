using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game.GameCommands
{
    public class PlaySound : GameCommandHandler
    {
        public AudioSource[] audioSources;

        public override void PerformInteraction()
        {
            foreach (var a in audioSources)
                a.Play();
        }

    }
}
