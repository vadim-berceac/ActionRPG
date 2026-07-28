using Game;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class CrossHair : MonoBehaviour
{
   [SerializeField] private Image image;
   
   [Inject] private readonly CameraSettings _cameraSettings;

   private void OnEnable()
   {
      image.enabled = false;
      image.rectTransform.anchoredPosition = Vector2.zero;
      _cameraSettings.onCameraSwitched.AddListener(OnCameraSwitched);
   }

   private void OnDisable()
   {
      _cameraSettings.onCameraSwitched.RemoveListener(OnCameraSwitched);
   }

   private void OnCameraSwitched(CameraSettings.CameraType cameraType)
   {
      if(cameraType != CameraSettings.CameraType.Bow)
      {
         image.enabled = false;
         return;
      };
      
      image.enabled = true;
   }
}
