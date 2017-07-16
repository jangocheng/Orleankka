﻿using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;

namespace Orleankka
{
    using Core;
    using Utility;

    [Serializable, Immutable]
    [DebuggerDisplay("a->{ToString()}")]
    public class ActorRef : ObserverRef, IEquatable<ActorRef>, IEquatable<ActorPath>
    {
        public static ActorRef Deserialize(ActorPath path, IGrainFactory factory)
        {
            var @interface = ActorInterface.Registered(path.Type);
            return new ActorRef(path, @interface.Proxy(path, factory));
        }

        readonly IActorEndpoint endpoint;

        protected internal ActorRef(ActorPath path)
        {
            Path = path;
        }

        ActorRef(ActorPath path, IActorEndpoint endpoint)
            : this(path)
        {
            this.endpoint = endpoint;
        }

        public ActorPath Path { get; }

        public virtual Task Tell(object message)
        {
            Requires.NotNull(message, nameof(message));

            return endpoint.ReceiveVoid(message);
        }

        public virtual async Task<TResult> Ask<TResult>(object message)
        {
            Requires.NotNull(message, nameof(message));

            var result = await endpoint.Receive(message);

            return (TResult) result;
        }

        public override void Notify(object message)
        {
            Tell(message).Ignore();
        }

        internal Task Autorun()
        {
            return endpoint.Autorun();
        }

        public bool Equals(ActorRef other)
        {
            return !ReferenceEquals(null, other) && (ReferenceEquals(this, other)
                    || Path.Equals(other.Path));
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj)
                    || obj.GetType() == GetType() && Equals((ActorRef) obj));
        }

        public bool Equals(ActorPath other) => Path.Equals(other);
        public override int GetHashCode()   => Path.GetHashCode();
        public override string ToString()   => Path.ToString();

        public static bool operator ==(ActorRef left, ActorRef right) => Equals(left, right);
        public static bool operator !=(ActorRef left, ActorRef right) => !Equals(left, right);

        [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
        public static implicit operator GrainReference(ActorRef arg) => (GrainReference) arg.endpoint;
    }

    [Serializable, Immutable]
    [DebuggerDisplay("a->{ToString()}")]
    public class ActorRef<TActor> : ObserverRef<TActor>, IEquatable<ActorRef<TActor>>, IEquatable<ActorPath> where TActor : IActor
    {
        readonly ActorRef @ref;

        protected internal ActorRef(ActorRef @ref)
        {
            this.@ref = @ref;
        }

        public virtual Task Tell(ActorMessage<TActor> message) => @ref.Tell(message);
        public virtual Task<TResult> Ask<TResult>(ActorMessage<TActor> message) => @ref.Ask<TResult>(message);
        public virtual Task<TResult> Ask<TResult>(ActorMessage<TActor, TResult> message) => @ref.Ask<TResult>(message);
        public override void Notify(ActorMessage<TActor> message) => @ref.Notify(message);

        public ActorPath Path => @ref.Path;

        public bool Equals(ActorRef<TActor> other)
        {
            return !ReferenceEquals(null, other) && (ReferenceEquals(this, other)
                    || Path.Equals(other.Path));
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj)
                    || obj.GetType() == GetType() && Equals((ActorRef<TActor>)obj));
        }

        public bool Equals(ActorPath other) => Path.Equals(other);
        public override string ToString() => Path.ToString();
        public override int GetHashCode() => Path.GetHashCode();

        public static bool operator ==(ActorRef<TActor> left, ActorRef<TActor> right) => Equals(left, right);
        public static bool operator !=(ActorRef<TActor> left, ActorRef<TActor> right) => !Equals(left, right);

        public static implicit operator ActorRef(ActorRef<TActor> arg) => arg.@ref;
        public static implicit operator ActorRef<TActor>(ActorRef arg) => new ActorRef<TActor>(arg);
        public static implicit operator GrainReference(ActorRef<TActor> arg) => arg.@ref;
    }
}