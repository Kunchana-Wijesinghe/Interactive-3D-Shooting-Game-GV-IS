using Perera.Core;
using UnityEngine;

namespace Perera.Combat
{
    /// <summary>Test helper: hides the object on death so kills are visible.</summary>
    [RequireComponent(typeof(Health))]
    public class DeathHide : MonoBehaviour
    {
        private Health health;
        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;
        private void OnDied(Health _) => gameObject.SetActive(false);
    }
}
