﻿namespace SharpNeedle.Ninja.Chao;
using SceneCollection = ChaoCollection<BinaryPointer<Scene>>;
using NodeCollection = ChaoCollection<SceneNode>;

public class SceneNode : IBinarySerializable
{
    public SceneNode Parent { get; set; }
    public SceneCollection Scenes { get; set; }
    public NodeCollection Children { get; set; }

    public void Read(BinaryObjectReader reader)
    {
        Scenes = reader.ReadObject<SceneCollection>();
        Children = reader.ReadObject<NodeCollection>();

        foreach (var child in Children)
            child.Value.Parent = this;
    }

    public void Write(BinaryObjectWriter writer)
    {
        writer.WriteObject(Scenes);
        writer.WriteObject(Children);
    }
}