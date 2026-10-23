// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Text;
using UnityEngine;

namespace SynthEnvBuilder
{
    public class ReadOnlyAttribute : PropertyAttribute { }

    public class LabelOverrideAttribute : PropertyAttribute
    {
        public string label;
        public LabelOverrideAttribute(string label)
        {
            this.label = label;
        }
    }

    [Serializable]
    public class UUID
    {
        [SerializeField]
        private byte[] value = new byte[16];

        public UUID()
        {
            value = System.Guid.NewGuid().ToByteArray();
        }

        public UUID(byte[] bytes)
        {
            value = bytes;
        }

        public override string ToString()
        {
            StringBuilder hex = new StringBuilder(value.Length * 2 + 4);
            for (int ii = 0; ii < value.Length; ++ii)
            {
                if (3 < ii && ii < 11 && ii % 2 == 0)
                {
                    hex.Append("-");
                }
                hex.AppendFormat("{0:x2}", value[ii]);
            }
            return hex.ToString();
        }

        public byte[] GetData() { return value; }
    }

    [ExecuteInEditMode]
    public class SpatialEntity : MonoBehaviour
    {
        public string SemanticLabel;
        [HideInInspector]
        public UUID Uuid;
        // This is a hack. Figure out how to properly draw a UUID
        [ReadOnly]
        [LabelOverride("UUID")]
        public string DisplayUuid;

        protected virtual void Awake()
        {
            if (DisplayUuid == null)
            {
                Uuid = new UUID(); // only initialize once
                DisplayUuid = Uuid.ToString();
            }
        }
    }
}
