using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.GameCommands
{

    public class SendOnBecameInvisible : SendGameCommand
    {
        void OnBecameInvisible()
        {
            Send();
        }
    }
}
