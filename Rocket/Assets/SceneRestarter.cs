using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRestarter : MonoBehaviour
{
    /// <summary>
    /// Перезапускает текущую активную сцену.
    /// Привязывается к OnClick() кнопки в инспекторе.
    /// </summary>
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}