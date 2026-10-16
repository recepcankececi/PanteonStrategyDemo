using UnityEngine;

namespace StrategyDemo.Core
{
    /// <summary>Orthographic camera size and position produced by <see cref="CameraFraming"/>.</summary>
    public readonly struct CameraFrame
    {
        /// <summary>Creates a frame.</summary>
        public CameraFrame(float orthographicSize, Vector2 center)
        {
            OrthographicSize = orthographicSize;
            Center = center;
        }

        /// <summary>Half of the visible world height.</summary>
        public float OrthographicSize { get; }

        /// <summary>World position the camera looks at.</summary>
        public Vector2 Center { get; }
    }
}
