using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class MagneticSnap : MonoBehaviour
{
    [Header("Магнит")]
    public float magnetSpeed = 10f;
    public float snapDistance = 0.03f;

    [Header("Звук")]
    public AudioClip snapSound;
    [Range(0f, 1f)]
    public float snapVolume = 1f;

    [Header("Подсветка материалом")]
    [Tooltip("Материал, который применится, пока объект касается магнита")]
    public Material highlightMaterial;

    [Tooltip("Изначальный материал магнита. Если не задан — берётся тот, " +
             "что стоит на рендерере при старте")]
    public Material originalMaterial;

    private bool isOccupied = false;
    private int touchCount = 0;

    private Renderer[] renderers;
    private Material[] originalMaterials; // что восстанавливать у каждого рендерера

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            // Приоритет: явно заданный originalMaterial → то, что уже стоит на рендерере
            originalMaterials[i] = originalMaterial != null
                ? originalMaterial
                : renderers[i].sharedMaterial;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Grabbable")) return;
        touchCount++;
        if (touchCount == 1) ApplyHighlightMaterial();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Grabbable")) return;
        touchCount = Mathf.Max(0, touchCount - 1);
        if (touchCount == 0) RestoreOriginalMaterials();
    }

    private void OnTriggerStay(Collider other)
    {
        if (isOccupied) return;
        if (!other.CompareTag("Grabbable")) return;

        var grab = other.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;

        other.transform.position = Vector3.Lerp(
            other.transform.position, transform.position, Time.deltaTime * magnetSpeed);
        other.transform.rotation = Quaternion.Slerp(
            other.transform.rotation, transform.rotation, Time.deltaTime * magnetSpeed);

        if (Vector3.Distance(other.transform.position, transform.position) < snapDistance)
            PlaceObject(other.transform, grab);
    }

    private void PlaceObject(Transform obj, XRGrabInteractable grab)
    {
        isOccupied = true;

        obj.position = transform.position;
        obj.rotation = transform.rotation;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (grab != null) grab.enabled = false;

        PlaySnapSound();

        // Возвращаем изначальный вид ДО выключения, чтобы при повторной
        // активации объекта он уже был в «правильном» состоянии
        RestoreOriginalMaterials();

        gameObject.SetActive(false);
    }

    private void PlaySnapSound()
    {
        if (snapSound == null) return;
        AudioSource.PlayClipAtPoint(snapSound, transform.position, snapVolume);
    }

    // ---- Подмена материалов ----

    private void ApplyHighlightMaterial()
    {
        if (highlightMaterial == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sharedMaterial = highlightMaterial;
        }
    }

    private void RestoreOriginalMaterials()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sharedMaterial = originalMaterials[i];
        }
    }

#if UNITY_EDITOR
    // Меняем материал прямо в редакторе, чтобы сразу видеть результат
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        if (originalMaterial == null) return;

        foreach (var r in renderers)
        {
            if (r != null) r.sharedMaterial = originalMaterial;
        }
    }
#endif
}