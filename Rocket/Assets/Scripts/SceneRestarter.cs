using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRestarter : MonoBehaviour
{
    [SerializeField] private float restartDelay = 1f;

    private bool isRestarting = false; // защита от повторных нажатий

    /// <summary>
    /// Запускает перезапуск текущей сцены с задержкой.
    /// Привязывается к OnClick() кнопки в инспекторе.
    /// </summary>
    public void RestartScene()
    {
        if (isRestarting) return; // игнорируем повторные клики
        StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        isRestarting = true;

        yield return new WaitForSeconds(restartDelay);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}