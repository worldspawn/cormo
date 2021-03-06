using System;
using Cormo.Contexts;
using Cormo.Impl.Weld.Serialization;

namespace Cormo.Impl.Weld.Contexts
{
    [Serializable]
    public class SerializableContextualInstance : ISerializableContextualInstance
    {
        public SerializableContextualInstance(IContextual contextual, object instance, ICreationalContext creationalContext, IContextualStore contextualStore)
        {
            Contextual = new SerializableContextual(contextual, contextualStore);
            Instance = instance;
            CreationalContext = creationalContext;
        }

        public object Instance { get; private set; }
        public ICreationalContext CreationalContext { get; private set; }
        public ISerializableContextual Contextual { get; private set; }

        IContextual IContextualInstance.Contextual
        {
            get { return Contextual; }
        }
    }
}