using System;
using System.Collections.Generic;
using Cormo.Contexts;
using Cormo.Injects;

namespace Cormo.Injects
{
    public interface IComponent: IContextual
    {
        IComponentManager Manager { get; }
        Type Type { get; }
        IEnumerable<QualifierAttribute> Qualifiers { get; }
        IEnumerable<IInjectionPoint> InjectionPoints { get; }
        Type Scope { get; }
    }
}