using UnityEngine;

namespace LegIK
{
    [System.Serializable]
    public class LegIKFootSettings
    {
        [Header("Identity")]
        [Tooltip("К какой цели Animator IK привязана эта нога")]
        public AvatarIKGoal ikGoal = AvatarIKGoal.LeftFoot;

        [Tooltip("Используется ли хинт (колено) для этой ноги")]
        public bool useHint = true;

        [Tooltip("К какому хинту Animator IK привязано колено")]
        public AvatarIKHint ikHint = AvatarIKHint.LeftKnee;

        [Header("Raycast — поиск земли под стопой")]
        [Tooltip("На сколько выше анимированной позиции стопы стартует луч")]
        [Range(0.05f, 1f)]
        public float raycastUpOffset = 0.3f;

        [Tooltip("Максимальная дистанция луча вниз от точки старта")]
        [Range(0.1f, 2f)]
        public float raycastDownDistance = 0.6f;

        [Tooltip("Слои, которые считаются землёй")]
        public LayerMask groundMask = ~0;

        [Tooltip("Использовать SphereCast вместо Raycast (мягче на неровностях, дороже по CPU)")]
        public bool useSphereCast = false;

        [Tooltip("Радиус SphereCast, если он включён")]
        [Range(0.01f, 0.3f)]
        public float sphereCastRadius = 0.08f;

        [Header("Смещение стопы")]
        [Tooltip("Локальное смещение точки IK относительно точки попадания луча (для подгонки под конкретную модель)")]
        public Vector3 footOffset = Vector3.zero;

        [Tooltip("Толщина подошвы — стопа приподнимается над землёй на это значение")]
        [Range(0f, 0.1f)]
        public float soleThickness = 0.02f;

        [Header("Веса IK")]
        [Tooltip("Максимальный вес позиции (0 = IK выключен, 1 = полностью override анимации)")]
        [Range(0f, 1f)]
        public float maxPositionWeight = 1f;

        [Tooltip("Максимальный вес поворота стопы")]
        [Range(0f, 1f)]
        public float maxRotationWeight = 1f;

        [Tooltip("Вес хинта колена")]
        [Range(0f, 1f)]
        public float hintWeight = 0.5f;

        [Header("Сглаживание (демпфирование)")]
        [Tooltip("Время сглаживания позиции стопы. Меньше — резче реакция на рельеф")]
        [Range(0.001f, 0.5f)]
        public float positionSmoothTime = 0.08f;

        [Tooltip("Время сглаживания поворота стопы")]
        [Range(0.001f, 0.5f)]
        public float rotationSmoothTime = 0.08f;

        [Tooltip("Время сглаживания веса IK (сек). Небольшое значение — вес быстро долетает до 0/1 при смене опорной/переносной фазы; само по себе не создаёт ступенек, т.к. целевой вес и так непрерывная функция высоты стопы")]
        [Range(0.001f, 0.3f)]
        public float weightSmoothTime = 0.03f;

        [Header("Поворот стопы под поверхность")]
        [Tooltip("Максимальный угол наклона стопы относительно анимации (градусы)")]
        [Range(0f, 90f)]
        public float maxRotationAngle = 45f;

        [Header("Отрыв стопы от земли (фаза переноса)")]
        [Tooltip("Если анимированная стопа ближе к земле, чем это значение — считаем ногу опорной, вес IK стремится к 1")]
        [Range(0f, 0.2f)]
        public float groundedSpeedThreshold = 0.15f;

        [Tooltip("Если анимированная стопа выше земли, чем это значение — считаем ногу в переносе (swing), вес IK стремится к 0 и нога свободно поднимается по анимации")]
        [Range(0.02f, 0.5f)]
        public float liftSpeedThreshold  = 0.5f;

        [Header("Ограничения по рельефу")]
        [Tooltip("Если земля наклонена больше этого угла — IK для стопы отключается (считаем поверхность непроходимой)")]
        [Range(0f, 90f)]
        public float maxSlopeAngle = 60f;

        [Header("Колено (хинт)")]
        [Tooltip("Смещение точки хинта вперёд по direction персонажа — помогает избежать 'вывернутого' колена при сильном IK")]
        [Range(-0.3f, 0.3f)]
        public float kneeForwardOffset = 0.05f;

        public LegIKFootSettings() { }

        public LegIKFootSettings(AvatarIKGoal goal, AvatarIKHint hint)
        {
            ikGoal = goal;
            ikHint = hint;
        }
    }
}