﻿namespace SharpNeedle;

public interface IResource : IDisposable
{
    string Name { get; set; }

    void Read(IFile file);
    void Write(IFile file);

    void ResolveDependencies(IDirectory dir);
    IReadOnlyList<ResourceDependency> GetDependencies();
}