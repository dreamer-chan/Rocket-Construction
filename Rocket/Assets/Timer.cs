using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Timer : MonoBehaviour
{
    [Header("UI")]
    public Button startButton;
    public TMP_Text timerText;

    [Header("Настройки")]
    public int targetValue = 5;
    public float tickInterval = 1f;

    public event Action OnReachedTarget;

    // 🔓 Публичное свойство — можно читать и менять из любого скрипта
    public int CurrentValue
    {
        get => currentValue;
        set
        {
            currentValue = value;

            // Если внешний скрипт выставил значение >= target — фиксируем достижение
            if (currentValue >= targetValue)
            {
                currentValue = targetValue;
                if (!targetReached)
                {
                    targetReached = true;
                    isRunning = false;
                    Debug.Log("Таймер достиг значения 5 (изменено извне)!");
                    OnReachedTarget?.Invoke();
                }
            }

            UpdateUI();
        }
    }

    [SerializeField] private int currentValue = 0; // реальное значение (скрыто в инспекторе, но видно, если нужно)

    private float tickTimer = 0f;
    private bool isRunning = false;
    private bool targetReached = false;

    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartTimer);

        UpdateUI();
    }

    public void StartTimer()
    {
        if (isRunning || targetReached) return;
        isRunning = true;
    }

    void Update()
    {
        if (!isRunning) return;

        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;

            CurrentValue += 1; // используем свойство, чтобы сработали все проверки
        }
    }

    void UpdateUI()
    {
        if (timerText != null)
            timerText.text = currentValue.ToString("0");
    }

    public void ResetTimer()
    {
        CurrentValue = 0;
        tickTimer = 0f;
        isRunning = false;
        targetReached = false;
        UpdateUI();
    }

    void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartTimer);
    }
}