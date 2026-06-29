using Game;
using UnityEngine;

public class PlayerTag : MonoBehaviour
{
   [field: SerializeField] public HumanoidController Player { get; set; }
   [field: SerializeField] public Inventory PlayerInventory { get; set; }
   [field: SerializeField] public Damageable PlayerHealth { get; set; }
}
