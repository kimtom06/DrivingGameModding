using System;
using UnityEngine;

namespace MobileModSystem
{
    [DisallowMultipleComponent]
    public sealed class ModNode : MonoBehaviour
    {
        [SerializeField] private string persistentId;

        public string PersistentId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(persistentId))
                    persistentId = Guid.NewGuid().ToString("N");

                return persistentId;
            }
        }

        public void SetPersistentId(string id)
        {
            persistentId = string.IsNullOrWhiteSpace(id)
                ? Guid.NewGuid().ToString("N")
                : id;
        }

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(persistentId))
                persistentId = Guid.NewGuid().ToString("N");
        }
    }
}
