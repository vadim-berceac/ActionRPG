using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class DescriptionFiller : MonoBehaviour
{
   [SerializeField] private TextMeshProUGUI text;
   [SerializeField] private AudioSource source;
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
      source.PlayOneShot(clip);
   }
}
