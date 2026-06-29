using TMPro;
using UnityEngine;

public class DescriptionFiller : MonoBehaviour
{
   [SerializeField] private TextMeshProUGUI text;
   [SerializeField] private AudioSource audio;
   [SerializeField] private AudioClip clip;

   private void Awake()
   {
      InventoryCellView.ObDescriptionChanged += OnDescriptionChanged;
   }

   private void OnDestroy()
   {
      InventoryCellView.ObDescriptionChanged -= OnDescriptionChanged;
   }

   private void OnDescriptionChanged(string description)
   {
      text.text = description;
      audio.PlayOneShot(clip);
   }
}
