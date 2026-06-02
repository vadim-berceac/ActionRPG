using UnityEngine;

namespace Game
{
    public class OpenURL : MonoBehaviour
    {
        public string websiteAddress;

        public void OpenURLOnClick()
        {
            Application.OpenURL(websiteAddress);
        }
    } 
}