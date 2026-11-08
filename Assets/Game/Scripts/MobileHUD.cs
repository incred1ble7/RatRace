using UnityEngine;
using UnityEngine.UI;

namespace MouseGame
{
    /// <summary>Action button: "Взять" near food, "Положить" while carrying, dimmed otherwise.</summary>
    public class MobileHUD : MonoBehaviour
    {
        [SerializeField] MouseController player;
        [SerializeField] Button actionButton;
        [SerializeField] Text actionLabel;
        [SerializeField] string pickUpText = "Взять";
        [SerializeField] string dropText = "Положить";

        CanvasGroup buttonGroup;

        void Awake()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(() => { if (player != null) player.Interact(); });
                buttonGroup = actionButton.GetComponent<CanvasGroup>();
                if (buttonGroup == null) buttonGroup = actionButton.gameObject.AddComponent<CanvasGroup>();
            }
        }

        void Update()
        {
            if (player == null || actionButton == null) return;
            bool carrying = player.IsCarrying;
            bool canAct = carrying || player.NearbyFood != null;

            if (actionLabel != null) actionLabel.text = carrying ? dropText : pickUpText;
            actionButton.interactable = canAct;
            buttonGroup.alpha = canAct ? 1f : 0.45f;
        }
    }
}
