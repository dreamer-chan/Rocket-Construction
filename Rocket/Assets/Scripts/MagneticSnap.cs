using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class MagneticSnap : MonoBehaviour
{
    [Header("Магнит")]
    public float magnetSpeed = 10f;
    public float snapDistance = 0.03f;

    [Header("Совместимость")]
    public string requiredKey = "Red";

    [Header("Звук")]
    public AudioClip snapSound;
    [Range(0f, 1f)]
    public float snapVolume = 1f;

    [Header("Подсветка материалами")]
    [Tooltip("Материалы подсветки. Если в массиве 1 элемент — применится ко всем " +
             "сабмешам всех рендереров. Если N — раскидывается по кругу.")]
    public Material[] highlightMaterials;

    [Tooltip("Материалы для восстановления. Если пусто — используются сохранённые " +
             "при старте материалы рендереров.")]
    public Material[] originalMaterials;

    [Header("Счётчик")]
    public Timer timer;

    private bool isOccupied = false;
    private int touchCount = 0;

    private Renderer[] renderers;
    private Material[][] savedMaterials;  // [rendererIndex][submeshIndex]

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        savedMaterials = new Material[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
            savedMaterials[i] = renderers[i].sharedMaterials;

        // Если в инспекторе задан массив originalMaterials — применяем сразу,
        // чтобы модель выглядела как задумано, и пересобираем saved.
        if (originalMaterials != null && originalMaterials.Length > 0)
        {
            ApplyMaterialArray(originalMaterials);
            for (int i = 0; i < renderers.Length; i++)
                savedMaterials[i] = renderers[i].sharedMaterials;
        }
    }

    // ---- Совместимость ----

    private bool IsCompatible(Collider other)
    {
        if (!other.CompareTag("Grabbable")) return false;
        var key = other.GetComponent<SnapKey>();
        if (key == null) return false;
        return key.key == requiredKey;
    }

    // ---- Триггеры ----

    private void OnTriggerEnter(Collider other)
    {
        if (!IsCompatible(other)) return;
        touchCount++;
        if (touchCount == 1) ApplyHighlight();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsCompatible(other)) return;
        touchCount = Mathf.Max(0, touchCount - 1);
        if (touchCount == 0) RestoreOriginal();
    }

    private void OnTriggerStay(Collider other)
    {
        if (isOccupied) return;
        if (!IsCompatible(other)) return;

        var grab = other.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;

        other.transform.position = Vector3.Lerp(
            other.transform.position, transform.position, Time.deltaTime * magnetSpeed);
        other.transform.rotation = Quaternion.Slerp(
            other.transform.rotation, transform.rotation, Time.deltaTime * magnetSpeed);

        if (Vector3.Distance(other.transform.position, transform.position) < snapDistance)
            PlaceObject(other.transform, grab);
    }

    // ---- Установка ----

    private void PlaceObject(Transform obj, XRGrabInteractable grab)
    {
        isOccupied = true;

        obj.position = transform.position;
        obj.rotation = transform.rotation;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (grab != null) grab.enabled = false;

        PlaySnapSound();
        RestoreOriginal();

        if (timer != null)
            timer.CurrentValue += 1;

        gameObject.SetActive(false);
    }

    private void PlaySnapSound()
    {
        if (snapSound == null) return;
        AudioSource.PlayClipAtPoint(snapSound, transform.position, snapVolume);
    }

    // ---- Работа с материалами ----

    private void ApplyHighlight()
    {
        if (highlightMaterials == null || highlightMaterials.Length == 0) return;
        ApplyMaterialArray(highlightMaterials);
    }

    private void RestoreOriginal()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sharedMaterials = savedMaterials[i];
        }
    }

    /// <summary>
    /// Применяет массив материалов ко всем рендерерам:
    /// - массив из 1 элемента → всем сабмешам;
    /// - массив из N → i-й сабмеш получает materials[i % N].
    /// </summary>
    private void ApplyMaterialArray(Material[] materials)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;

            int subCount = r.sharedMaterials.Length;
            var mats = new Material[subCount];

            for (int i = 0; i < subCount; i++)
                mats[i] = materials[i % materials.Length];

            r.sharedMaterials = mats;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        // Превью оригиналов в редакторе
        if (originalMaterials != null && originalMaterials.Length > 0)
            ApplyMaterialArray(originalMaterials);
    }
#endif
}