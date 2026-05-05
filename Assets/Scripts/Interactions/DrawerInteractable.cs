using UnityEngine;

namespace Game.Interaction
{
    [AddComponentMenu("Game/Interaction/Drawer Interactable")]
    [DisallowMultipleComponent]
    public class DrawerInteractable : MonoBehaviour, IInteractable
    {
        [Header("Motion")]
        [Tooltip("Расстояние выдвижения в метрах (мировые единицы)")]
        [SerializeField] private float openDistance = 0.8f;
        [Tooltip("Направление в локальном пространстве объекта")]
        [SerializeField] private Vector3 openDirection = new Vector3(0, 0, 1);
        [Tooltip("Скорость выдвижения (локальные единицы/сек, подбирается под масштаб FBX)")]
        [SerializeField] private float speed = 0.005f;

        [Header("Prompt")]
        [SerializeField] private string openPrompt  = "Open";
        [SerializeField] private string closePrompt = "Close";

        private bool    isOpen;
        private bool    hasInteracted;
        private Vector3 closedLocalPos;
        private Vector3 openLocalPos;

        private void Start()
        {
            closedLocalPos = transform.localPosition;

            // Переводим мировые метры в локальные единицы через lossyScale родителя
            float localDist = WorldToLocal(openDistance);
            openLocalPos = closedLocalPos + openDirection.normalized * localDist;
        }

        private void Update()
        {
            // Не двигаем ничего до первого взаимодействия
            if (!hasInteracted) return;

            Vector3 target = isOpen ? openLocalPos : closedLocalPos;
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, target, speed * Time.deltaTime);
        }

        // Переводит мировую величину в локальную по среднему масштабу родителя
        private float WorldToLocal(float worldValue)
        {
            if (transform.parent == null) return worldValue;
            Vector3 s = transform.parent.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return avg > 0.0001f ? worldValue / avg : worldValue;
        }

        public string GetPrompt()                        => isOpen ? closePrompt : openPrompt;
        public bool   CanInteract(GameObject interactor) => true;
        public void   Interact(GameObject interactor)
        {
            isOpen = !isOpen;
            hasInteracted = true;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 worldPos = transform.position;
            Vector3 worldDir = transform.parent != null
                ? transform.parent.TransformDirection(openDirection.normalized)
                : transform.TransformDirection(openDirection.normalized);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(worldPos, worldDir * openDistance);
            Gizmos.DrawWireSphere(worldPos + worldDir * openDistance, 0.04f);
        }
    }
}
