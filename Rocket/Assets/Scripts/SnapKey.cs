using UnityEngine;

/// <summary>
/// Маркер на grabbable-объекте: к какому «типу» магнитов он подходит.
/// </summary>
public class SnapKey : MonoBehaviour
{
    [Tooltip("Идентификатор типа. Должен совпадать с Required Key у магнита")]
    public string key = "Red";
}