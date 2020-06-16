﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Experimental.Physics
{

    /// <summary>
    /// Properties of the extent in which a damped
    /// harmonic oscillator is free to move.
    /// </summary>
    [Serializable]
    public struct ElasticExtentProperties<T>
    {
        /// <value>
        /// Represents the lower bound of the extent,
        /// specified as the norm of the n-dimensional extent
        /// </value>
        [SerializeField]
        public float MinStretch;

        /// <value>
        /// Represents the upper bound of the extent,
        /// specified as the norm of the n-dimensional extent
        /// </value>
        [SerializeField]
        public float MaxStretch;

        /// <value>
        /// Whether the system, when approaching the upper bound,
        /// will treat the end limits like snap points and magnetize to them.
        /// </value>
        [SerializeField]
        public bool SnapToEnd;

        /// <value>
        /// Points inside the extent to which the system will snap.
        /// </value>
        [SerializeField]
        public T[] SnapPoints;
    }

    /// <summary>
    /// Properties of the damped harmonic oscillator differential system.
    /// </summary>
    [Serializable]
    public struct ElasticProperties
    {
        /// <value>
        /// Mass of the simulated oscillator element
        /// </value>
        [SerializeField]
        public float Mass;
        /// <value>
        /// Hand spring constant
        /// </value>
        [SerializeField]
        public float HandK;
        /// <value>
        /// End cap spring constant
        /// </value>
        [SerializeField]
        public float EndK;
        /// <value>
        /// Snap point spring constant
        /// </value>
        [SerializeField]
        public float SnapK;
        /// <value>
        /// Extent at which snap points begin forcing the spring.
        /// </value>
        [SerializeField]
        public float SnapRadius;
        /// <value>
        /// Drag/damper factor, proportional to velocity.
        /// </value>
        [SerializeField]
        public float Drag;
    }

    /// <summary>
    /// Represents a damped harmonic oscillator over an
    /// N-dimensional vector space, specified by generic type T.
    /// 
    /// This extensibility allows not just for 1, 2, and 3-D springs, but
    /// allows for 4-dimensional quaternion springs.
    /// </summary>
    internal abstract class ElasticSystem<T>
    {
        protected ElasticExtentProperties<T> extentInfo;
        protected ElasticProperties elasticProperties;

        protected T currentValue;
        protected T currentVelocity;

        public ElasticSystem(T initialValue, T initialVelocity,
                                ElasticExtentProperties<T> extentInfo, ElasticProperties elasticProperties)
        {
            this.extentInfo = extentInfo;
            this.elasticProperties = elasticProperties;
            currentValue = initialValue;
            currentVelocity = initialVelocity;
        }

        /// <summary>
        /// Update the internal state of the damped harmonic oscillator,
        /// given the forcing/desired value, returning the new value.
        /// </summary>
        /// <param name="forcingValue">Input value, for example, a desired manipulation position.</param>
        /// <param name="deltaTime">Amount of time that has passed since the last update.</param>
        /// <returns>The new value of the system.</returns>
        public abstract T ComputeIteration(T forcingValue, float deltaTime);

        /// <summary>
        /// Query the elastic system for the current instantaneous value
        /// </summary>
        /// <returns>Current value of the elastic system</returns>
        public T GetCurrentValue() => currentValue;

        /// <summary>
        /// Query the elastic system for the current instantaneous velocity
        /// </summary>
        /// <returns>Current value of the elastic system</returns>
        public T GetCurrentVelocity() => currentVelocity;
    }

    internal class LinearElasticSystem : ElasticSystem<float>
    {
        public LinearElasticSystem(float initialValue, float initialVelocity,
                                   ElasticExtentProperties<float> extentInfo,
                                   ElasticProperties elasticProperties)
                                   : base(initialValue, initialVelocity,
                                          extentInfo, elasticProperties) { }

        /// <summary>
        /// Update the internal state of the damped harmonic oscillator, given the forcing/desired value.
        /// </summary>
        /// <param name="forcingValue">Input value, for example, a desired manipulation position.</param>
        /// <param name="deltaTime">Amount of time that has passed since the last update.</param>
        public override float ComputeIteration(float forcingValue, float deltaTime)
        {
            // F = -kx - (drag * v)
            var force = (forcingValue - currentValue) * elasticProperties.HandK - elasticProperties.Drag * currentVelocity;

            // Distance that the current stretch value is from the end limit.
            float distFromEnd = extentInfo.MaxStretch - currentValue;

            // If we are extended beyond the end cap,
            // add one-sided force back to the center.
            if (currentValue > extentInfo.MaxStretch)
            {
                force += distFromEnd * elasticProperties.EndK;
            }
            else
            {
                // Otherwise, add standard bidirectional magnetic/snapping force towards the end marker. (optional)
                if (extentInfo.SnapToEnd)
                {
                    force += (distFromEnd) * elasticProperties.EndK * (1.0f - Mathf.Clamp01(Mathf.Abs(distFromEnd / elasticProperties.SnapRadius)));
                }
            }

            distFromEnd = extentInfo.MinStretch - currentValue;
            if (currentValue < extentInfo.MinStretch)
            {
                force += distFromEnd * elasticProperties.EndK;
            }
            else
            {
                // Otherwise, add standard bidirectional magnetic/snapping force towards the end marker. (optional)
                if (extentInfo.SnapToEnd)
                {
                    force += (distFromEnd) * elasticProperties.EndK * (1.0f - Mathf.Clamp01(Mathf.Abs(distFromEnd / elasticProperties.SnapRadius)));
                }
            }

            // Iterate over each snapping point, and apply forces as necessary.
            foreach (float snappingPoint in extentInfo.SnapPoints)
            {
                // Calculate distance from snapping point.
                var distFromSnappingPoint = snappingPoint - currentValue;

                // Snap force is calculated by multiplying the "-kx" factor by
                // a clamped distance factor. This results in an overall
                // hyperbolic profile to the force imparted by the snap point.
                force += (distFromSnappingPoint) * elasticProperties.SnapK
                          * (1.0f - Mathf.Clamp01(Mathf.Abs(distFromSnappingPoint / elasticProperties.SnapRadius)));
            }

            // a = F/m
            var accel = force / elasticProperties.Mass;

            // Integrate our acceleration over time.
            currentVelocity += accel * deltaTime;
            // Integrate our velocity over time.
            currentValue += currentVelocity * deltaTime;

            return currentValue;
        }
    }
}
