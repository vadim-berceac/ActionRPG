using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.GameCommands
{

    public class SendOnCollisionExit : SendGameCommand
    {
        public LayerMask layers;

        void OnCollisionExit(Collision collision)
        {
            if (0 != (layers.value & 1 << collision.gameObject.layer))
            {
                Send();
            }
        }
    }

}
