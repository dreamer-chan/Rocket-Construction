using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class OnOff : MonoBehaviour
{
    [Header("Ссылки на объекты")]
    [Tooltip("Объект, который нужно выключить")]
    public GameObject objectToDisable;

    [Tooltip("Объект, который нужно включить")]
    public GameObject objectToEnable;

    [Header("Настройки")]
    [Tooltip("Задержка в секундах")]
    public float delay = 11f;

    [Tooltip("UI кнопка, при нажатии на которую запускается переключение")]
    public Button uiButton;

    private Coroutine switchCoroutine;

    private void Start()
    {
        // Подписываемся на нажатие кнопки
        if (uiButton != null)
            uiButton.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        // Отписываемся, чтобы не было утечек памяти
        if (uiButton != null)
            uiButton.onClick.RemoveListener(OnButtonClicked);

        if (switchCoroutine != null)
            StopCoroutine(switchCoroutine);
    }

    private void OnButtonClicked()
    {
        // Защита от повторного нажатия, пока идёт таймер
        if (switchCoroutine != null) return;

        switchCoroutine = StartCoroutine(SwitchAfterDelay());
    }

    private IEnumerator SwitchAfterDelay()
    {
        yield return new WaitForSeconds(delay);

        if (objectToDisable != null)
            objectToDisable.SetActive(false);

        if (objectToEnable != null)
            objectToEnable.SetActive(true);

        switchCoroutine = null;
    }
}