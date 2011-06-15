﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;

namespace MvcMiniProfiler
{
    /// <summary>
    /// An individual profiling step that can contain child steps.
    /// </summary>
    [DataContract]
    public class Timing : IDisposable
    {
        /// <summary>
        /// Unique identifer for this timing; set during construction.
        /// </summary>
        [DataMember(Order = 1)]
        public Guid Id { get; set; }

        /// <summary>
        /// Text displayed when this Timing is rendered.
        /// </summary>
        [DataMember(Order = 2)]
        public string Name { get; set; }

        /// <summary>
        /// How long this Timing step took in ms; includes any <see cref="Children"/> Timings' durations.
        /// </summary>
        [DataMember(Order = 3)]
        public double? DurationMilliseconds { get; set; }

        /// <summary>
        /// The offset from the start of profiling.
        /// </summary>
        [DataMember(Order = 4)]
        public double StartMilliseconds { get; set; }

        /// <summary>
        /// All sub-steps that occur within this Timing step. Add new children through <see cref="AddChild"/>
        /// </summary>
        [DataMember(Order = 5)]
        public List<Timing> Children { get; set; }

        /// <summary>
        /// Stores arbitrary key/value strings on this Timing step. Add new tuples through <see cref="AddKeyValue"/>.
        /// </summary>
        [DataMember(Order = 6)]
        public Dictionary<string, string> KeyValues { get; set; }

        /// <summary>
        /// Any queries that occurred during this Timing step.
        /// </summary>
        [DataMember(Order = 7)]
        public List<SqlTiming> SqlTimings { get; set; }

        /// <summary>
        /// Which Timing this Timing is under - the duration that this step takes will be added to its parent's duration.
        /// </summary>
        /// <remarks>This will be null for the root (initial) Timing.</remarks>
        [ScriptIgnore]
        public Timing Parent { get; internal set; }

        /// <summary>
        /// Gets the ellapsed milliseconds in this step without any children's durations.
        /// </summary>
        public double DurationWithoutChildrenMilliseconds
        {
            get
            {
                double result = DurationMilliseconds.GetValueOrDefault();

                if (HasChildren)
                {
                    foreach (var child in Children)
                    {
                        result -= child.DurationMilliseconds.GetValueOrDefault();
                    }
                }

                return Math.Round(result, 1);
            }
        }

        /// <summary>
        /// Returns true when this <see cref="DurationWithoutChildrenMilliseconds"/> is less than the configured
        /// <see cref="MiniProfiler.Settings.TrivialDurationThresholdMilliseconds"/>, by default 2.0 ms.
        /// </summary>
        public bool IsTrivial
        {
            get { return DurationWithoutChildrenMilliseconds <= MiniProfiler.Settings.TrivialDurationThresholdMilliseconds; }
        }

        /// <summary>
        /// Reference to the containing profiler, allowing this Timing to affect the Head and get Stopwatch readings.
        /// </summary>
        private readonly MiniProfiler _profiler;

        /// <summary>
        /// Offset from parent MiniProfiler's creation that this Timing was created.
        /// </summary>
        private readonly long _startTicks;

        /// <summary>
        /// Returns true when this Timing has inner Timing steps.
        /// </summary>
        public bool HasChildren
        {
            get { return Children != null && Children.Count > 0; }
        }

        /// <summary>
        /// Returns true if this Timing step collected sql execution timings.
        /// </summary>
        public bool HasSqlTimings
        {
            get { return SqlTimings != null && SqlTimings.Count > 0; }
        }

        /// <summary>
        /// Returns true when this Timing is the first one created in a MiniProfiler session.
        /// </summary>
        public bool IsRoot
        {
            get { return Parent == null; }
        }

        /// <summary>
        /// How far away this Timing is from the Profiler's Root.
        /// </summary>
        public int Depth
        {
            get
            {
                int result = 0;
                var parent = Parent;

                while (parent != null)
                {
                    parent = parent.Parent;
                    result++;
                }

                return result;
            }
        }

        /// <summary>
        /// Creates a new Timing named 'name' in the 'profiler's session, with 'parent' as this Timing's immediate ancestor.
        /// </summary>
        public Timing(MiniProfiler profiler, Timing parent, string name)
        {
            this.Id = Guid.NewGuid();
            _profiler = profiler;
            _profiler.Head = this;

            if (parent != null) // root will have no parent
            {
                Parent = parent;
                Parent.AddChild(this);
            }

            Name = name;

            _startTicks = profiler.ElapsedTicks;
            StartMilliseconds = MiniProfiler.GetRoundedMilliseconds(_startTicks);
        }
        /// <summary>
        /// Obsolete - used for serialization.
        /// </summary>
        [Obsolete("Used for serialization")]
        public Timing()
        {
        }

        /// <summary>
        /// Adds arbitrary string 'value' under 'key', allowing custom properties to be stored in this Timing step.
        /// </summary>
        public void AddKeyValue(string key, string value)
        {
            if (KeyValues == null)
                KeyValues = new Dictionary<string, string>();

            KeyValues[key] = value;
        }

        /// <summary>
        /// Completes this Timing's duration and sets the MiniProfiler's Head up one level.
        /// </summary>
        public void Stop()
        {
            if (DurationMilliseconds == null)
            {
                DurationMilliseconds = MiniProfiler.GetRoundedMilliseconds(_profiler.ElapsedTicks - _startTicks);
                _profiler.Head = Parent;
            }
        }

        void IDisposable.Dispose()
        {
            Stop();
        }

        internal void AddChild(Timing timing)
        {
            if (Children == null)
                Children = new List<Timing>();

            Children.Add(timing);
        }

        internal void AddSqlTiming(SqlTiming stat)
        {
            if (SqlTimings == null)
                SqlTimings = new List<SqlTiming>();

            SqlTimings.Add(stat);
        }
    }
}