using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game
{
    public class StaminaUI : MonoBehaviour
    {
        [field: SerializeField] public Slider StaminaSlider { get; private set; }

        private PlayerTag _playerTag;
        private Stamina _stamina;

        [Inject]
        private void Construct(PlayerTag playerTag)
        {
            _playerTag =  playerTag;
        }

        private void Start()
        {
            SetStamina(_playerTag.Player.Stamina);
        }

        public void SetStamina(Stamina stamina)
        {
            Unsubscribe();

            _stamina = stamina;

            if (_stamina == null)
            {
                return;
            }

            StaminaSlider.minValue = 0f;
            StaminaSlider.maxValue = 1f;
            RefreshFromCurrent(_stamina.GetCurrentStamina());

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_stamina == null)
            {
                return;
            }
           
            _stamina.OnCurrentStaminaChanged += HandleCurrentStaminaChanged;
            _stamina.OnMaxStaminaChanged += HandleMaxStaminaChanged;
        }

        private void Unsubscribe()
        {
            if (_stamina == null)
            {
                return;
            }

            _stamina.OnCurrentStaminaChanged -= HandleCurrentStaminaChanged;
            _stamina.OnMaxStaminaChanged -= HandleMaxStaminaChanged;
        }

        private void HandleCurrentStaminaChanged(float currentStamina)
        {
            RefreshFromCurrent(currentStamina);
        }

        private void HandleMaxStaminaChanged(float _)
        {
            RefreshFromCurrent(_stamina.GetCurrentStamina());
        }

        private void RefreshFromCurrent(float currentStamina)
        {
            var max = _stamina.GetMaxStamina();
            StaminaSlider.value = max > 0f ? currentStamina / max : 0f;
        }
    }
}