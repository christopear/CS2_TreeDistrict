using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TreeDistrict
{
    // Reserved for future generator output only. Never added to pre-existing trees.
    // The Entity serializer remaps this reference on save/load; no runtime indices
    // are persisted as IDs. Bake will remove this component without deleting a tree.
    public struct TreeDistrictTreeOwner : IComponentData, ISerializable
    {
        public Entity Area;
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter { writer.Write(Area); }
        public void Deserialize<TReader>(TReader reader) where TReader : IReader { reader.Read(out Area); }
    }
}
