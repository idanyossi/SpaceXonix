using UnityEngine;
using UnityEngine.UI;

namespace SpaceXonix.UI
{
    /// <summary>
    /// Hides a panel when its button is clicked. A serialized component rather than a runtime
    /// listener, so the wiring survives being saved into the scene.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PanelCloseButton : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        private Button button;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(ClosePanel);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(ClosePanel);
        }

        public void ClosePanel()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
