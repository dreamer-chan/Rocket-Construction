using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class Freeze : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Настройки заморозки позиции")] // Исправил опечатку в слове Настройки

    public bool freezeX = false;
    public bool freezeZ = false;

    [Header("Настройки UI")]
    [Tooltip("Перетащите сюда UI Кнопку из Canvas")]
    public Button freezeButton;

    private bool isFrozen = false; // Флаг текущего состояния

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        ApplyConstraints();

        if (freezeButton != null)
        {
            freezeButton.onClick.AddListener(ToggleFreeze);
        }
        else
        {
            Debug.LogWarning("Кнопка не назначена в инспекторе у скрипта Freeze!");
        }
    }

    public void ToggleFreeze()
    {
        isFrozen = !isFrozen;

        if (isFrozen)
        {
            FreezeAllPosition();
            Debug.Log("Объект заморожен (X, Z)");
        }
        else
        {
            UnfreezeAllPosition();
            Debug.Log("Объект разморожен (X, Z)");
        }
    }

    // Применяет галочки из инспектора
    public void ApplyConstraints()
    {
        // 1. Сначала аккуратно убираем ограничения ТОЛЬКО по X и Z (не трогая Y)
        rb.constraints &= ~(RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ);

        // 2. Затем добавляем нужные ограничения
        if (freezeX) rb.constraints |= RigidbodyConstraints.FreezePositionX;
        if (freezeZ) rb.constraints |= RigidbodyConstraints.FreezePositionZ;
    }

    // Заморозка X и Z (Y остается без изменений)
    public void FreezeAllPosition()
    {
        rb.constraints |= RigidbodyConstraints.FreezePositionX |
                          RigidbodyConstraints.FreezePositionZ;
    }

    // Разморозка X и Z (Y остается без изменений)
    public void UnfreezeAllPosition()
    {
        // Убираем флаги X и Z, оставляя все остальные флаги (включая Y) нетронутыми
        rb.constraints &= ~(RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ);
    }
}