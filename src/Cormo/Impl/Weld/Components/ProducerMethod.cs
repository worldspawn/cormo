using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cormo.Contexts;
using Cormo.Impl.Weld.Introspectors;
using Cormo.Impl.Weld.Utils;
using Cormo.Injects;

namespace Cormo.Impl.Weld.Components
{
    public class ProducerMethod : AbstractProducer
    {
        private readonly InjectableMethod _method;

        public ProducerMethod(IWeldComponent declaringComponent, MethodInfo method, WeldComponentManager manager)
            : base(declaringComponent, method, method.ReturnType, manager)
        {
            _method = new InjectableMethod(declaringComponent, method, null);
        }

        protected override AbstractProducer TranslateTypes(GenericResolver.Resolution resolution)
        {
            var resolvedMethod = GenericUtils.TranslateMethodGenericArguments(_method.Method, resolution.GenericParameterTranslations);
            if (resolvedMethod == null || GenericUtils.MemberContainsGenericArguments(resolvedMethod))
                return null;

            return new ProducerMethod(DeclaringComponent.Resolve(resolvedMethod.DeclaringType), resolvedMethod, Manager);
        }

        protected override BuildPlan GetBuildPlan()
        {
            return _method.Invoke;
        }

        public override IEnumerable<IChainValidatable> NextLinearValidatables
        {
            get
            {
                return base.NextLinearValidatables.Union(
                    _method.InjectionPoints
                        .Where(x => !ScopeAttribute.IsNormal(x.Scope))
                        .Select(x => x.Component).OfType<IWeldComponent>());
            }
        }

        public override IEnumerable<IChainValidatable> NextNonLinearValidatables
        {
            get { return _method.NonLinearValidatables; }
        }

        public override string ToString()
        {
            return string.Format("Producer Method [{0}] with Qualifiers [{1}]", _method.Method, string.Join(",", Qualifiers));
        }
    }
}