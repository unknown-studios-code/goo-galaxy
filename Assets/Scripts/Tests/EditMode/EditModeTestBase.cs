using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Runtime.Tests.EditMode
{
    public abstract class EditModeTestBase
    {
        protected const int PrimaryBoardRadius = 4;

        protected List<Vector2Int> PrimaryBoardCoordinates { get; private set; } = new();

        [SetUp]
        public virtual void SetUp()
        {
            PrimaryBoardCoordinates = CreateHexCoordinates(PrimaryBoardRadius);
        }

        protected static List<Vector2Int> CreateHexCoordinates(int radius)
        {
            var coordinates = new List<Vector2Int>();

            for (int q = -radius; q <= radius; q++)
            {
                int minR = Mathf.Max(-radius, -q - radius);
                int maxR = Mathf.Min(radius, -q + radius);

                for (int r = minR; r <= maxR; r++)
                {
                    coordinates.Add(new Vector2Int(q, r));
                }
            }

            return coordinates;
        }

        protected static T CreateConfig<T>()
            where T : ScriptableObject
        {
            return ScriptableObject.CreateInstance<T>();
        }

        protected static T LoadConfig<T>(string resourcePath)
            where T : ScriptableObject
        {
            return Resources.Load<T>(resourcePath);
        }
    }
}
