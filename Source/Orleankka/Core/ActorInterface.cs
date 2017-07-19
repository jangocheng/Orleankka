﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

using Orleans;
using Orleans.Internals;

namespace Orleankka.Core
{
    using Utility;

    public class ActorInterface
    {
        static readonly Dictionary<string, ActorInterface> names =
                    new Dictionary<string, ActorInterface>();

        static readonly Dictionary<int, ActorInterface> ids =
                    new Dictionary<int, ActorInterface>();

        public static ActorInterface Of(string name)
        {
            Requires.NotNull(name, nameof(name));

            var result = names.Find(name);
            if (result == null)
                throw new InvalidOperationException(
                    $"Unable to map actor type name '{name}' to the corresponding actor. " +
                        "Make sure that you've registered an actor type or the assembly containing this type");

            return result;
        }

        public static ActorInterface Of(int id)
        {
            var result = ids.Find(id);
            if (result == null)
                throw new InvalidOperationException(
                    $"Unable to map actor interface id '{id}' to the corresponding actor");

            return result;
        }

        internal static void Register(IEnumerable<Assembly> assemblies, IEnumerable<ActorInterfaceMapping> mappings)
        {
            var unregistered = new List<ActorInterfaceMapping>();

            foreach (var each in mappings)
            {
                var existing = names.Find(each.TypeName);
                if (existing == null)
                {
                    unregistered.Add(each);
                    continue;
                }

                if (existing.Mapping != each)
                    throw new DuplicateActorTypeException(existing.Mapping, each);
            }

            using (Trace.Execution("Generation of actor interface assemblies"))
            {
                var generated = ActorInterfaceDeclaration.Generate(assemblies, unregistered);

                foreach (var each in generated)
                {
                    names.Add(each.Name, each);
                    ids.Add(each.Id, each);
                }
            }
        }

        internal readonly ActorInterfaceMapping Mapping;
        internal readonly Type Grain;

        public readonly int Id;
        public readonly ushort Version;
        public readonly string Name;

        Func<IGrainFactory, string, object> factory;

        internal ActorInterface(ActorInterfaceMapping mapping, Type grain)
        {
            Mapping = mapping;
            Grain = grain;

            Id = grain.InterfaceId();
            Version = grain.InterfaceVersion();
            Name = Mapping.TypeName;

            Array.ForEach(mapping.Types, ActorTypeName.Register);
        }

        public Assembly GrainAssembly() => Grain.Assembly;

        internal static void Bind(IGrainFactory factory)
        {            
            foreach (var @interface in names.Values)
            {
                var method = factory.GetType().GetMethod("GetGrain", new[] {typeof(string), typeof(string)});
                var invoker = method.MakeGenericMethod(@interface.Grain);

                var @this = Expression.Parameter(typeof(object));
                var id = Expression.Parameter(typeof(string));
                var ns = Expression.Constant(null, typeof(string));

                var call = Expression.Call(Expression.Convert(@this, factory.GetType()), invoker, id, ns);
                var func = Expression.Lambda<Func<object, string, object>>(call, @this, id).Compile();

                @interface.factory = func;
            }
        }

        internal IActorEndpoint Proxy(string id, IGrainFactory instance) => (IActorEndpoint) factory(instance, id);
    }
}