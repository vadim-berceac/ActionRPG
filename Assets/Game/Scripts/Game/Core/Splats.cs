using Game;
using UnityEngine;

public class Splats : MonoBehaviour
{
   [SerializeField] private WaterVolume waterVolume;
   [SerializeField] private AudioClip splatSound;
   [SerializeField] private ParticleSystem splatParticles;

   private void OnEnable()
   {
      if (!waterVolume)
      {
         return;
      }

      waterVolume.PushOutDelayStarted += Splat;
   }

   private void OnDisable()
   {
      if (!waterVolume)
      {
         return;
      }
      
      waterVolume.PushOutDelayStarted -= Splat;
   }

   private void Splat(HumanoidController humanoidController, Vector3 position)
   {
      if(splatSound)
      {
         AudioSource.PlayClipAtPoint(splatSound, position);
      }

      if (splatParticles)
      {
         var particles = Instantiate(splatParticles, position, Quaternion.identity);
         particles.Play();
      }
   }
}
