using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// При изменении bool-переменной:
///  - объекты в радиусе становятся дочерними этому объекту;
///  - объекты вне радиуса отключаются (SetActive(false)).
/// При выключении bool-переменной всё восстанавливается в исходное состояние.
/// </summary>
[DisallowMultipleComponent]
public class Decorator : MonoBehaviour
{
    [Header("Состояние")]
    [SerializeField] private bool _isActive = false;

    [Header("Настройки")]
    [Tooltip("Радиус действия")]
    [SerializeField] private float radius = 5f;

    [Tooltip("Слои, объекты которых нужно обрабатывать")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Tooltip("Центр радиуса. Если не задан — используется трансформ этого объекта")]
    [SerializeField] private Transform radiusCenter;

    [Tooltip("Учитывать неактивные объекты при поиске")]
    [SerializeField] private bool includeInactive = false;

    /// <summary>Публичное свойство — меняй его из любого скрипта.</summary>
    public void Activation()
    {
        _isActive = true;
    }

    public float Radius
    {
        get => radius;
        set
        {
            radius = Mathf.Max(0f, value);
            if (Application.isPlaying && _isActive) Apply();
        }
    }

    private class SavedState
    {
        public Transform originalParent;
        public bool wasActiveSelf;
        public bool wasParented;
        public bool wasDisabled;
    }

    private readonly Dictionary<GameObject, SavedState> _affected =
        new Dictionary<GameObject, SavedState>();

    private bool _appliedState;

    private void Awake()
    {
        if (radiusCenter == null) radiusCenter = transform;
        _appliedState = _isActive;
        if (_isActive) Apply();
    }

    // Позволяет реагировать на переключение флажка в инспекторе в Play Mode
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (radiusCenter == null) radiusCenter = transform;
        if (_isActive != _appliedState) Apply();
    }

    private void OnDestroy()
    {
        RestoreAll();
    }

    [ContextMenu("Применить сейчас")]
    public void Apply()
    {
        _appliedState = _isActive;
        RestoreAll();
        if (!_isActive) return;
        if (radiusCenter == null) radiusCenter = transform;

        Vector3 center = radiusCenter.position;
        float r2 = radius * radius;

        // Собираем все трансформы в сцене
#if UNITY_2022_2_OR_NEWER
        var findMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Transform[] all = FindObjectsByType<Transform>(findMode, FindObjectsSortMode.None);
#else
        Transform[] all = FindObjectsOfType<Transform>(includeInactive);
#endif

        // Кандидаты — подходящие по слою, не мы и не наши текущие дети
        var candidates = new HashSet<Transform>();
        foreach (var t in all)
        {
            if (t == null || t == transform) continue;
            if (t.IsChildOf(transform)) continue;
            if ((targetLayers.value & (1 << t.gameObject.layer)) == 0) continue;
            candidates.Add(t);
        }

        foreach (var t in candidates)
        {
            // Пропускаем тех, у кого уже есть родитель-кандидат (обработаем сверху)
            bool hasCandidateAncestor = false;
            var p = t.parent;
            while (p != null)
            {
                if (candidates.Contains(p)) { hasCandidateAncestor = true; break; }
                p = p.parent;
            }
            if (hasCandidateAncestor) continue;

            bool inside = (t.position - center).sqrMagnitude <= r2;

            var go = t.gameObject;
            var state = new SavedState
            {
                originalParent = t.parent,
                wasActiveSelf = go.activeSelf
            };

            if (inside)
            {
                // Делаем дочерним себе
                state.wasParented = true;
                _affected[go] = state;
                t.SetParent(transform, worldPositionStays: true);
            }
            else
            {
                // Отключаем
                state.wasDisabled = true;
                _affected[go] = state;
                go.SetActive(false);
            }
        }
    }

    private void RestoreAll()
    {
        foreach (var kv in _affected)
        {
            var go = kv.Key;
            if (go == null) continue;
            var st = kv.Value;

            if (st.wasParented)
                go.transform.SetParent(st.originalParent, true);

            if (st.wasDisabled)
                go.SetActive(st.wasActiveSelf);
        }
        _affected.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Transform c = radiusCenter != null ? radiusCenter : transform;
        Gizmos.color = _isActive
            ? new Color(0f, 1f, 0f, 0.35f)
            : new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(c.position, radius);
    }
}