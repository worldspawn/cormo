﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cormo.Impl.Utils;
using Cormo.Impl.Weld.Injections;
using Cormo.Injects;
using Cormo.Injects.Exceptions;
using Cormo.Interceptions;

namespace Cormo.Impl.Weld.Components
{
    public delegate object InterceptorFunc();

    public class Interceptor : ManagedComponent
    {
        private static Type[] AllInterceptorTypes = {typeof (IAroundInvokeInterceptor)};

        public Type[] InterceptorBindings { get; private set; }

        public Interceptor(Type type, WeldComponentManager manager)
            : base(type, manager)
        {
            InterceptorBindings = Binders.OfType<IInterceptorBinding>().Select(x => x.GetType()).ToArray();
            InterceptorTypes = AllInterceptorTypes.Where(x => x.IsAssignableFrom(type)).ToArray();
            
            if(!InterceptorBindings.Any())
                throw new InvalidComponentException(type, "Interceptor must have at least one interceptor-binding attribute");
            if (!InterceptorTypes.Any())
                throw new InvalidComponentException(type, "Interceptor must implement " + string.Join(" or ", AllInterceptorTypes.Select(x => x.ToString())));
        
        }

        public Type[] InterceptorTypes { get; private set; }

        public Type[] InterfaceTypes { get; private set; }

        protected override BuildPlan MakeConstructPlan()
        {
            return InjectableConstructor.Invoke;
        }
    }
}