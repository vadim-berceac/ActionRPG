using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.GameCommands
{

    public class SendOnCollisionEnter : SendGameCommand
    {
        public LayerMask layers;

        void OnCollisionEnter(Collision collision)
        {
            if (0 != (layers.value & 1 << collision.gameObject.layer))
            {
                Send();
            }
        }
    }

}
