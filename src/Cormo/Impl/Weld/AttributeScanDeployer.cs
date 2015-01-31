using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cormo.Contexts;
using Cormo.Impl.Weld.Components;
using Cormo.Impl.Weld.Contexts;
using Cormo.Impl.Weld.Injections;
using Cormo.Impl.Weld.Utils;
using Cormo.Injects;
using Cormo.Injects.Exceptions;
using Cormo.Utils;

namespace Cormo.Impl.Weld
{
    public class AttributeScanDeployer
    {
        private readonly WeldComponentManager _manager;
        private readonly WeldEnvironment _environment;
        private const BindingFlags AllBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public AttributeScanDeployer(WeldComponentManager manager, WeldEnvironment environment)
        {
            _manager = manager;
            _environment = environment;
        }

        public IEnumerable<Assembly> WhereReferencesRecursive(Assembly[] assemblies, params AssemblyName[] names)
        {
            var referencings =
                assemblies.Where(a => a.GetReferencedAssemblies().Any(
                    r => names.Any(n=> AssemblyName.ReferenceMatchesDefinition(r, n))))
                    .ToArray();

            if (!referencings.Any()) 
                return referencings;

            var others = assemblies.Except(referencings).ToArray();
            var referencingNames = referencings.Select(x => x.GetName()).ToArray();
            referencings = referencings.Union(WhereReferencesRecursive(others, referencingNames)).ToArray();

            return referencings;
        }

        public void AutoScan()
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();

            var types = (from assembly in 
                             WhereReferencesRecursive(AppDomain.CurrentDomain.GetAssemblies(), assemblyName)
                             .AsParallel()
                            from type in assembly.GetLoadableTypes()
                            where type.IsVisible && type.IsClass && !type.IsPrimitive
                            select type).ToArray();

            var componentTypes = types.AsParallel().Where(TypeUtils.IsComponent).ToArray();

            var producerFields = (from type in types.AsParallel()
                                    from field in type.GetFields(AllBindingFlags)
                                    where field.HasAttributeRecursive<ProducesAttribute>()
                                    select field).ToArray();

            var producerMethods = (from type in types.AsParallel()
                                    from method in type.GetMethods(AllBindingFlags)
                                    where method.HasAttributeRecursive<ProducesAttribute>()
                                    select method).ToArray();

            var producerProperties = (from type in types.AsParallel()
                                      from property in type.GetProperties(AllBindingFlags)
                                      where property.HasAttributeRecursive<ProducesAttribute>()
                                      select property).ToArray();

            AddTypes(componentTypes);
            AddProducerMethods(producerMethods);
            AddProducerFields(producerFields);
            AddProducerProperties(producerProperties);

            var configs = GetConfigs(_environment.Components);
            foreach (var c in configs)
                _environment.AddConfiguration(c);
        }

        public void AddProducerMethods(params MethodInfo[] methods)
        {
            var components = methods.AsParallel().Select(MakeProducerMethod).ToArray();
            foreach(var c in components)
                _environment.AddComponent(c);
        }

        public void AddProducerFields(params FieldInfo[] fields)
        {
            var components = fields.AsParallel().Select(MakeProducerField).ToArray();
            foreach (var c in components)
                _environment.AddComponent(c);
        }

        public void AddProducerProperties(params PropertyInfo[] properties)
        {
            var components = properties.AsParallel().Select(MakeProducerProperty).ToArray();
            foreach (var c in components)
                _environment.AddComponent(c);
        }

        public void AddTypes(params Type[] types)
        {
            var components = types.AsParallel().Select(MakeComponent).ToArray();

            foreach (var c in components)
                _environment.AddComponent(c);
                
        }

        public void AddValue(object instance, params QualifierAttribute[] qualifiers)
        {
            _environment.AddValue(instance, qualifiers, _manager);
        }
        

        private static IEnumerable<IWeldComponent> GetConfigs(IEnumerable<IWeldComponent> components)
        {
            var componentMap = components.ToDictionary(x => x.Type, x => x);
            var configs = new List<IWeldComponent>();
            var newConfigs = componentMap.Values.Where(x => x.Type.HasAttributeRecursive<ConfigurationAttribute>()).ToArray();

            while (newConfigs.Any())
            {
                configs.AddRange(newConfigs);

                var imports = from config in newConfigs
                    from import in config.Type.GetAttributesRecursive<ImportAttribute>()
                    from importType in import.Types
                    select new {config.Type, importType};

                newConfigs = imports.Select(x =>
                {
                    IWeldComponent component;
                    if (componentMap.TryGetValue(x.importType, out component))
                        return component;
                    throw new InvalidComponentException(x.importType,
                        string.Format("Could not import a non-component type from Configuration [{0}]", x.Type));
                })
                .Except(configs)
                .ToArray();
            }
            return configs;
        }

        public IWeldComponent MakeProducerField(FieldInfo field)
        {
            var qualifiers = field.GetQualifiers();
            var scope = field.GetAttributesRecursive<ScopeAttribute>().Select(x=> x.GetType()).FirstOrDefault() ?? typeof(DependentAttribute);

            return new ProducerField(field, qualifiers, scope, _manager);
        }

        public IWeldComponent MakeProducerProperty(PropertyInfo property)
        {
            var qualifiers = property.GetQualifiers();
            var scope = property.GetAttributesRecursive<ScopeAttribute>().Select(x => x.GetType()).FirstOrDefault() ?? typeof(DependentAttribute);

            return new ProducerProperty(property, qualifiers, scope, _manager);
        }

        public IWeldComponent MakeProducerMethod(MethodInfo method)
        {
            var qualifiers = method.GetQualifiers();
            var scope = method.GetAttributesRecursive<ScopeAttribute>().Select(x => x.GetType()).FirstOrDefault() ?? typeof(DependentAttribute);

            var producer = new ProducerMethod(method, qualifiers, scope, _manager);
            var injects = ToMethodInjections(producer, method).ToArray();
            producer.AddInjectionPoints(injects);
            return producer;
        }

        public IWeldComponent MakeComponent(Type type)
        {
            var qualifiers = type.GetQualifiers();
            
            var methods = type.GetMethods(AllBindingFlags).ToArray();

            var iMethods = methods.Where(InjectionValidator.ScanPredicate).ToArray();
            var iProperties = type.GetProperties(AllBindingFlags).Where(InjectionValidator.ScanPredicate).ToArray();
            var iCtors = type.GetConstructors(AllBindingFlags).Where(InjectionValidator.ScanPredicate).ToArray();
            var iFields = type.GetFields(AllBindingFlags).Where(InjectionValidator.ScanPredicate).ToArray();
            var postConstructs = methods.Where(x => x.HasAttributeRecursive<PostConstructAttribute>()).ToArray();
            var scope = type.GetAttributesRecursive<ScopeAttribute>().Select(x=> x.GetType()).FirstOrDefault() ?? typeof(DependentAttribute);

            if (iCtors.Length > 1)
                throw new InvalidComponentException(type, "Multiple [Inject] constructors");

            var component = type.HasAttributeRecursive<MixinAttribute>()? (ManagedComponent)
                new Mixin(type, qualifiers, scope, _manager, postConstructs) : 
                new ClassComponent(type, qualifiers, scope, _manager, postConstructs);

            var methodInjects = iMethods.SelectMany(m => ToMethodInjections(component, m)).ToArray();
            var ctorInjects = iCtors.SelectMany(ctor => ToMethodInjections(component, ctor)).ToArray();
            var fieldInjects = iFields.Select(f => new FieldInjectionPoint(component, f, f.GetQualifiers())).ToArray();
            var propertyInjects = iProperties.Select(p => new PropertyInjectionPoint(component, p, p.GetQualifiers())).ToArray();
            
            component.AddInjectionPoints(methodInjects.Union(ctorInjects).Union(fieldInjects).Union(propertyInjects).ToArray());
            return component;
        }

        private IEnumerable<IWeldInjetionPoint> ToMethodInjections(IComponent component, MethodBase method)
        {
            var parameters = method.GetParameters();
            return parameters.Select(p => new MethodParameterInjectionPoint(component, p, p.GetQualifiers()));
        }

        public void Deploy()
        {
            AddContexts();
            AddBuiltInComponents();
            _manager.Deploy(_environment);
        }

        private void AddBuiltInComponents()
        {
            _environment.AddComponent(new InjectionPointComponent(_manager));
        }

        private void AddContexts()
        {
            _manager.AddContext(new DependentContext());
            _manager.AddContext(new SingletonContext());
        }
    }
}