using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class FPSTarget : MonoBehaviour
    {
        public int targetFPS = 60;

        // Use this for initialization
        void OnEnable()
        {
            SetTargetFPS(targetFPS);
        }

        public void SetTargetFPS(int fps)
        {
            Application.targetFrameRate = fps;
        }
    } 
}
