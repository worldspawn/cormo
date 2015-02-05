﻿using System.Collections.Generic;

namespace Cormo.Injects
{
    public interface IBinders : IEnumerable<IBinderAttribute>
    {
        IQualifiers Qualifiers { get; }
    }
}