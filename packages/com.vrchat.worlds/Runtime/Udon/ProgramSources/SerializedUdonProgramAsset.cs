using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;

namespace VRC.Udon.ProgramSources
{
    public sealed class SerializedUdonProgramAsset : AbstractSerializedUdonProgramAsset
    {
        private const DataFormat DEFAULT_SERIALIZATION_DATA_FORMAT = DataFormat.Binary;

        [SerializeField, HideInInspector]
        private string serializedProgramBytesString;

        [SerializeField, HideInInspector]
        private List<UnityEngine.Object> programUnityEngineObjects;

        // Store the serialization DataFormat that was actually used to serialize the program.
        // This allows us to change the DataFormat later (ex. switch to binary) without causing already serialized programs to use the wrong DataFormat.
        // Programs will be deserialized using the previous format and will switch to the new format if StoreProgram is called again later.
        [SerializeField, HideInInspector]
        private DataFormat serializationDataFormat = DEFAULT_SERIALIZATION_DATA_FORMAT;

        public override void StoreProgram(IUdonProgram udonProgram)
        {
            if (this == null) return;
            
            byte[] serializedProgramBytes = SerializationUtility.SerializeValue(udonProgram, DEFAULT_SERIALIZATION_DATA_FORMAT, out programUnityEngineObjects);
            serializedProgramBytesString = Convert.ToBase64String(serializedProgramBytes);
            serializationDataFormat = DEFAULT_SERIALIZATION_DATA_FORMAT;

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public override IUdonProgram RetrieveProgram()
        {
            byte[] serializedProgramBytes = Convert.FromBase64String(serializedProgramBytesString ?? "");
            return SerializationUtility.DeserializeValue<IUdonProgram>(serializedProgramBytes, serializationDataFormat, programUnityEngineObjects);
        }
    }
}
