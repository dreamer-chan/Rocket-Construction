using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Timer : MonoBehaviour
{
    [Header("UI")]
    public Button startButton;
    public TMP_Text timerText;

    [Header("Рестарт")]
    public SceneRestarter sceneRestarter;   // ← ссылка на объект рестартера

    [Header("Настройки")]
    public float tickInterval = 1f;
    public int Value = 8;
    public int CountSeconds = 0;

    public bool isRunning = false;

    private bool isCompleted = false;       // ← чтобы рестарт не срабатывал повторно

    public int CurrentValue
    {
        get => currentValue;
        set
        {
            currentValue = value;
            UpdateUI();

            if (currentValue == Value && !isCompleted)
            {
                isCompleted = true;
                isRunning = false;
                Debug.Log("Ракета собрана за " + FormatTime(CountSeconds));

                if (sceneRestarter != null)
                    sceneRestarter.RestartScene();
                else
                    Debug.LogWarning("sceneRestarter не назначен в инспекторе!");
            }
        }
    }

    [SerializeField] private int currentValue = 0;
    private float tickTimer = 0f;

    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartTimer);
        UpdateUI();
    }

    public void StartTimer()
    {
        if (isRunning || isCompleted) return;
        isRunning = true;
        tickTimer = 0f;
    }

    void Update()
    {
        if (!isRunning) return;

        tickTimer += Time.deltaTime;
        while (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            CountSeconds += 1;
            UpdateUI();
        }
    }

    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    void UpdateUI()
    {
        if (timerText != null)
            timerText.text = FormatTime(CountSeconds);
    }

    public void ResetTimer()
    {
        CurrentValue = 0;
        CountSeconds = 0;
        tickTimer = 0f;
        isRunning = false;
        isCompleted = false;
        UpdateUI();
    }

    void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartTimer);
    }
}