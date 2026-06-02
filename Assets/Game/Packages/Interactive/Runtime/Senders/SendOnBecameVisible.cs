using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.GameCommands
{

    public class SendOnBecameVisible : SendGameCommand
    {
        void OnBecameVisible()
        {
            Send();
        }
    }

}
