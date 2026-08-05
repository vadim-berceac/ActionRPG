using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game
{
    public class SliderUI : MonoBehaviour
    {
        public enum Mode
        {
            HP,
            Stamina
        }
        
        [field: SerializeField] public Slider Slider { get; private set; }
        [field: SerializeField] public Image FillImage { get; private set; }
        [field: SerializeField] public Gradient Gradient { get; private set; }
        [field: SerializeField] public Mode WorkMode { get; private set; }

        private PlayerTag _playerTag;
        private IUIUpdater _uiUpdater;

        [Inject]
        private void Construct(PlayerTag playerTag)
        {
            _playerTag =  playerTag;
        }

        private void Start()
        {
            switch (WorkMode)
            {
                case Mode.HP:
                    Set(_playerTag.PlayerHealth);
                    break;
                case Mode.Stamina:
                    Set(_playerTag.Player.Stamina);
                    break;
            }
        }

        public void Set(IUIUpdater updater)
        {
            Unsubscribe();

            _uiUpdater = updater;

            if (_uiUpdater == null)
            {
                return;
            }

            Slider.minValue = 0f;
            Slider.maxValue = 1f;
            RefreshFromCurrent(_uiUpdater.GetCurrentValue());

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
            if (_uiUpdater == null)
            {
                return;
            }
           
            _uiUpdater.OnCurrentValueChanged += HandleCurrentUIUpdaterChanged;
            _uiUpdater.OnMaxValueChanged += HandleMaxUIUpdaterChanged;
        }

        private void Unsubscribe()
        {
            if (_uiUpdater == null)
            {
                return;
            }

            _uiUpdater.OnCurrentValueChanged -= HandleCurrentUIUpdaterChanged;
            _uiUpdater.OnMaxValueChanged -= HandleMaxUIUpdaterChanged;
        }

        private void HandleCurrentUIUpdaterChanged(float current)
        {
            RefreshFromCurrent(current);
        }

        private void HandleMaxUIUpdaterChanged(float _)
        {
            RefreshFromCurrent(_uiUpdater.GetCurrentValue());
        }

        private void RefreshFromCurrent(float current)
        {
            var max = _uiUpdater.GetMaxValue();
            var fillAmount = max > 0f ? current / max : 0f;

            Slider.value = fillAmount;
            UpdateFillColor(fillAmount);
        }

        private void UpdateFillColor(float fillAmount)
        {
            if (!FillImage|| Gradient == null)
            {
                return;
            }

            FillImage.color = Gradient.Evaluate(fillAmount);
        }
    }
}