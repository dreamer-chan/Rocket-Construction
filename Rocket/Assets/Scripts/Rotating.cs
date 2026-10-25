using System.Collections;
using UnityEngine;

public class Rotating : MonoBehaviour
{
    [Tooltip("Длительность поворота в секундах. 0 = мгновенный поворот")]
    [SerializeField] private float rotationDuration = 0.5f;

    private bool isRotating = false;

    /// <summary>
    /// Поворачивает объект на 180 градусов по оси Y.
    /// Можно вызывать из других скриптов, кнопок UI или через UnityEvent.
    /// </summary>
    public void Rotate180()
    {
        if (isRotating) return; // защита от повторного вызова
        StartCoroutine(RotateCoroutine());
    }

    private IEnumerator RotateCoroutine()
    {
        isRotating = true;

        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 180f, 0f);

        if (rotationDuration <= 0f)
        {
            transform.rotation = endRotation;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rotationDuration);
                // Плавное замедление (SmoothStep). Можно заменить на t для линейности
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, smoothT);
                yield return null;
            }
            transform.rotation = endRotation;
        }

        isRotating = false;
    }
}